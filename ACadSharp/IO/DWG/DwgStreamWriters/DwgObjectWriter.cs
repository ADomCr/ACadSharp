﻿using ACadSharp.Blocks;
using ACadSharp.Entities;
using ACadSharp.Tables;
using ACadSharp.Tables.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ACadSharp.IO.DWG
{
	internal partial class DwgObjectWriter : DwgSectionIO
	{
		public event NotificationEventHandler OnNotification;

		/// <summary>
		/// Key : handle | Value : Offset
		/// </summary>
		public Dictionary<ulong, long> Map { get; } = new Dictionary<ulong, long>();

		private Queue<CadObject> _objects = new Queue<CadObject>();

		private MemoryStream _msmain;

		private IDwgStreamWriter _writer;

		private Stream _stream;

		private CadDocument _document;

		public DwgObjectWriter(Stream stream, CadDocument document) : base(document.Header.Version)
		{
			this._stream = stream;
			this._document = document;

			this._msmain = new MemoryStream();
			this._writer = DwgStreamWriterBase.GetMergedWriter(document.Header.Version, this._msmain, Encoding.Default);
		}

		public void Write()
		{
			this.writeTable(this._document.AppIds);
			this.writeTable(this._document.Layers);
			this.writeTable(this._document.LineTypes);
			this.writeTable(this._document.TextStyles);
			this.writeTable(this._document.UCSs);
			this.writeTable(this._document.Views);
			//this.writeTable(this._document.DimensionStyles);
			//this.writeTable(this._document.VPorts);
			this.writeBlockControl();
		}

		private void writeBlockControl()
		{
			this.writeCommonNonEntityData(this._document.BlockRecords);

			//Common:
			//Numentries BL 70
			this._writer.WriteBitLong(this._document.BlockRecords.Count - 2);

			foreach (var item in this._document.BlockRecords)
			{
				if (item.Name.Equals(BlockRecord.ModelSpaceName, StringComparison.OrdinalIgnoreCase)
					|| item.Name.Equals(BlockRecord.PaperSpaceName, StringComparison.OrdinalIgnoreCase))
				{
					//Handle refs H NULL(soft pointer)
					this._writer.HandleReference(DwgReferenceType.SoftOwnership, item);
				}
			}

			//*MODEL_SPACE and *PAPER_SPACE(hard owner).
			this._writer.HandleReference(DwgReferenceType.HardOwnership, this._document.ModelSpace);
			this._writer.HandleReference(DwgReferenceType.HardOwnership, this._document.PaperSpace);

			this.registerObject(this._document.BlockRecords);

			this.writeEntries(this._document.BlockRecords);
		}

		private void writeTable<T>(Table<T> table, bool register = true)
			where T : TableEntry
		{
			this.writeCommonNonEntityData(table);

			//Common:
			//Numentries BL 70
			this._writer.WriteBitLong(table.Count);

			foreach (var item in table)
			{
				//Handle refs H NULL(soft pointer)
				this._writer.HandleReference(DwgReferenceType.SoftOwnership, item);
			}

			if (!register)
				return;

			this.registerObject(table);

			this.writeEntries(table);
		}

		public void writeEntries<T>(Table<T> table)
			where T : TableEntry
		{
			foreach (var entry in table)
			{
				switch (entry)
				{
					case AppId app:
						this.writeAppId(app);
						break;
					case BlockRecord blkRecord:
						this.writeBlockRecord(blkRecord);
						break;
					case Layer layer:
						this.writeLayer(layer);
						break;
					case LineType ltype:
						this.writeLineType(ltype);
						break;
					case TextStyle tstyle:
						this.writeTextStyle(tstyle);
						break;
					case UCS ucs:
						this.writeUCS(ucs);
						break;
					case View view:
						this.writeView(view);
						break;
					default:
						this.Notify($"Table entry not implemented : {entry.GetType().FullName}", NotificationType.NotImplemented);
						break;
				}
			}
		}

		private void writeAppId(AppId app)
		{
			this.writeCommonNonEntityData(app);

			//Common:
			//Entry name TV 2
			this._writer.WriteVariableText(app.Name);

			this.writeXrefDependantBit(app);

			//Unknown RC 71 Undoc'd 71-group; doesn't even appear in DXF or an entget if it's 0.
			this._writer.WriteByte(0);
			//Handle refs H The app control(soft pointer)
			//[Reactors(soft pointer)]
			//xdicobjhandle(hard owner)
			//External reference block handle(hard pointer)
			this._writer.HandleReference(DwgReferenceType.SoftPointer, this._document.AppIds);

			this.registerObject(app);
		}

		private void writeBlockRecord(BlockRecord blkRecord)
		{
			this.writeBlock(blkRecord.BlockEntity);

			this.writeBlockHeader(blkRecord);

			this.writeBlockEnd(blkRecord.BlockEnd);

			foreach (Entity e in blkRecord.Entities)
			{
				this.writeEntity(e);
			}
		}

		private void writeBlockHeader(BlockRecord record)
		{
			this.writeCommonNonEntityData(record);

			//Common:
			//Entry name TV 2
			//Warning: names ended with a number are not readed in this method
			this._writer.WriteVariableText(record.Name);

			this.writeXrefDependantBit(record);

			//Anonymous B 1 if this is an anonymous block (1 bit)
			this._writer.WriteBit(record.Flags.HasFlag(BlockTypeFlags.Anonymous));

			//Hasatts B 1 if block contains attdefs (2 bit)
			this._writer.WriteBit(record.HasAttributes);

			//Blkisxref B 1 if block is xref (4 bit)
			this._writer.WriteBit(record.Flags.HasFlag(BlockTypeFlags.XRef));

			//Xrefoverlaid B 1 if an overlaid xref (8 bit)
			this._writer.WriteBit(record.Flags.HasFlag(BlockTypeFlags.XRefOverlay));

			//R2000+:
			if (this.R2000Plus)
			{
				//Loaded Bit B 0 indicates loaded for an xref
				this._writer.WriteBit(record.Flags.HasFlag(BlockTypeFlags.XRef));
			}

			//R2004+:
			if (this.R2004Plus
				&& !record.Flags.HasFlag(BlockTypeFlags.XRef)
				&& !record.Flags.HasFlag(BlockTypeFlags.XRefOverlay))
			{
				//Owned Object Count BL Number of objects owned by this object.
				_writer.WriteBitLong(record.Entities.Count);
			}

			//Common:
			//Base pt 3BD 10 Base point of block.
			this._writer.Write3BitDouble(record.BlockEntity.BasePoint);
			//Xref pname TV 1 Xref pathname. That's right: DXF 1 AND 3!
			//3 1 appears in a tblnext/ search elist; 3 appears in an entget.
			this._writer.WriteVariableText(record.BlockEntity.XrefPath);

			//R2000+:
			if (this.R2000Plus)
			{
				//Insert Count RC A sequence of zero or more non-zero RC’s, followed by a terminating 0 RC.The total number of these indicates how many insert handles will be present.
				foreach (var item in this._document.Entities.OfType<Insert>()
					.Where(i => i.Block.Name == record.Name))
				{
					this._writer.WriteByte(1);
				}

				this._writer.WriteByte(0);

				//Block Description TV 4 Block description.
				this._writer.WriteVariableText(record.BlockEntity.Comments);

				//Size of preview data BL Indicates number of bytes of data following.
				this._writer.WriteBitLong(0);
			}

			//R2007+:
			if (this.R2007Plus)
			{
				//Insert units BS 70
				this._writer.WriteBitShort((short)record.Units);
				//Explodable B 280
				this._writer.WriteBit(record.IsExplodable);
				//Block scaling RC 281
				this._writer.WriteByte((byte)(record.CanScale ? 1u : 0u));
			}

			//NULL(hard pointer)
			this._writer.HandleReference(DwgReferenceType.HardPointer, 0);
			//BLOCK entity. (hard owner)
			//Block begin object
			this._writer.HandleReference(DwgReferenceType.HardOwnership, record.BlockEntity);

			//R13-R2000:
			if (this._version >= ACadVersion.AC1012 && this._version <= ACadVersion.AC1015
					&& !record.Flags.HasFlag(BlockTypeFlags.XRef)
					&& !record.Flags.HasFlag(BlockTypeFlags.XRefOverlay))
			{
				if (record.Entities.Any())
				{
					//first entity in the def. (soft pointer)
					this._writer.HandleReference(DwgReferenceType.SoftPointer, record.Entities.First());
					//last entity in the def. (soft pointer)
					this._writer.HandleReference(DwgReferenceType.SoftPointer, record.Entities.Last());
				}
				else
				{
					this._writer.HandleReference(DwgReferenceType.SoftPointer, 0);
					this._writer.HandleReference(DwgReferenceType.SoftPointer, 0);
				}
			}

			//R2004+:
			if (this.R2004Plus)
			{
				foreach (var item in record.Entities)
				{
					//H[ENTITY(hard owner)] Repeats “Owned Object Count” times.
					this._writer.HandleReference(DwgReferenceType.HardOwnership, item);
				}
			}

			//Common:
			//ENDBLK entity. (hard owner)
			this._writer.HandleReference(DwgReferenceType.HardOwnership, record.BlockEnd);

			//R2000+:
			if (this.R2000Plus)
			{
				foreach (var item in this._document.Entities.OfType<Insert>()
					.Where(i => i.Block.Name == record.Name))
				{
					this._writer.HandleReference(DwgReferenceType.SoftPointer, item);
				}

				//Layout Handle H(hard pointer)
				this._writer.HandleReference(DwgReferenceType.HardOwnership, record.Layout);
			}

			this.registerObject(record);
		}

		private void writeBlock(Block block)
		{
			this.writeCommonEntityData(block);

			//Common:
			//Entry name TV 2
			this._writer.WriteVariableText(block.Name);

			this.registerObject(block);
		}

		private void writeBlockEnd(BlockEnd blkEnd)
		{
			this.writeCommonEntityData(blkEnd);

			this.registerObject(blkEnd);
		}

		private void writeLayer(Layer layer)
		{
			this.writeCommonNonEntityData(layer);

			//Common:
			//Entry name TV 2
			this._writer.WriteVariableText(layer.Name);

			this.writeXrefDependantBit(layer);

			//R13-R14 Only:
			if (this.R13_14Only)
			{
				//Frozen B 70 if frozen (1 bit)
				this._writer.WriteBit(layer.Flags.HasFlag(LayerFlags.Frozen));
				//On B if on.
				this._writer.WriteBit(layer.IsOn);
				//Frz in new B 70 if frozen by default in new viewports (2 bit)
				this._writer.WriteBit(layer.Flags.HasFlag(LayerFlags.FrozenNewViewports));
				//Locked B 70 if locked (4 bit)
				this._writer.WriteBit(layer.Flags.HasFlag(LayerFlags.Locked));
			}

			//R2000+:
			if (this.R2000Plus)
			{
				//and lineweight (mask with 0x03E0)
				short values = (short)(DwgLineWeightConverter.ToIndex(layer.LineWeight) << 5);

				//contains frozen (1 bit),
				values |= (short)LayerFlags.Frozen;

				//on (2 bit)
				if (layer.IsOn)
					values |= 0b10;

				//frozen by default in new viewports (4 bit)
				values |= (short)LayerFlags.FrozenNewViewports;

				//locked (8 bit)
				values |= (short)LayerFlags.Locked;

				//plotting flag (16 bit),
				if (layer.PlotFlag)
					values |= 0b10000;

				//Values BS 70,290,370
				this._writer.WriteBitShort(values);
			}

			//Common:
			//Color CMC 62
			this._writer.WriteCmColor(layer.Color);

			//Handle refs H Layer control (soft pointer)
			this._writer.HandleReference(DwgReferenceType.SoftPointer, this._document.Layers);
			//[Reactors(soft pointer)]
			//xdicobjhandle(hard owner)
			//External reference block handle(hard pointer)

			//R2000+:
			if (this.R2000Plus)
			{
				//H 390 Plotstyle (hard pointer), by default points to PLACEHOLDER with handle 0x0f.
				this._writer.HandleReference(DwgReferenceType.HardPointer, 0);
			}

			//R2007+:
			if (this.R2007Plus)
			{
				//H 347 Material
				this._writer.HandleReference(DwgReferenceType.HardPointer, 0);
			}

			//Common:
			//H 6 linetype (hard pointer)
			this._writer.HandleReference(DwgReferenceType.HardPointer, layer.LineType.Handle);

			if (R2013Plus)
			{
				//H Unknown handle (hard pointer). Always seems to be NULL.
				this._writer.HandleReference(DwgReferenceType.HardPointer, 0);
			}

			this.registerObject(layer);
		}

		private void writeLineType(LineType ltype)
		{
			this.writeCommonNonEntityData(ltype);

			//Common:
			//Entry name TV 2
			this._writer.WriteVariableText(ltype.Name);

			this.writeXrefDependantBit(ltype);

			//Description TV 3
			this._writer.WriteVariableText(ltype.Description);
			//Pattern Len BD 40
			this._writer.WriteBitDouble(ltype.PatternLen);
			//Alignment RC 72 Always 'A'.
			this._writer.WriteByte((byte)ltype.Alignment);

			//Numdashes RC 73 The number of repetitions of the 49...74 data.
			this._writer.WriteByte((byte)ltype.Segments.Count());
			bool isText = false;
			foreach (LineType.Segment segment in ltype.Segments)
			{
				//Dash length BD 49 Dash or dot specifier.
				this._writer.WriteBitDouble(segment.Length);
				//Complex shapecode BS 75 Shape number if shapeflag is 2, or index into the string area if shapeflag is 4.
				this._writer.WriteBitShort(segment.ShapeNumber);

				//X - offset RD 44 (0.0 for a simple dash.)
				//Y - offset RD 45(0.0 for a simple dash.)
				this._writer.WriteBitDouble(segment.Offset.X);
				this._writer.WriteBitDouble(segment.Offset.Y);

				//Scale BD 46 (1.0 for a simple dash.)
				this._writer.WriteBitDouble(segment.Scale);
				//Rotation BD 50 (0.0 for a simple dash.)
				this._writer.WriteBitDouble(segment.Rotation);
				//Shapeflag BS 74 bit coded:
				this._writer.WriteBitShort((short)segment.Shapeflag);

				if (segment.Shapeflag.HasFlag(LinetypeShapeFlags.Text))
					isText = true;
			}

			//R2004 and earlier:
			if (this._version <= ACadVersion.AC1018)
			{
				//Strings area X 9 256 bytes of text area. The complex dashes that have text use this area via the 75-group indices. It's basically a pile of 0-terminated strings.
				//First byte is always 0 for R13 and data starts at byte 1.
				//In R14 it is not a valid data start from byte 0.
				//(The 9 - group is undocumented.)
				for (int i = 0; i < 256; i++)
				{
					//TODO: Write the line type text area
					this._writer.WriteByte(0);
				}
			}

			//R2007+:
			if (this.R2007Plus && isText)
			{
				for (int i = 0; i < 512; i++)
				{
					//TODO: Write the line type text area
					this._writer.WriteByte(0);
				}
				//TODO: Read the line type text area
			}

			//Common:
			//Handle refs H Ltype control(soft pointer)
			this._writer.HandleReference(DwgReferenceType.SoftPointer, this._document.LineTypes);
			//[Reactors (soft pointer)]
			//xdicobjhandle(hard owner)
			//External reference block handle(hard pointer)

			foreach (var segment in ltype.Segments)
			{
				//340 shapefile for dash/shape (1 each) (hard pointer)
				this._writer.HandleReference(DwgReferenceType.HardPointer, 0);
			}

			this.registerObject(ltype);
		}

		private void writeTextStyle(TextStyle style)
		{
			this.writeCommonNonEntityData(style);

			//Common:
			//Entry name TV 2
			this._writer.WriteVariableText(style.Name);

			this.writeXrefDependantBit(style);

			//shape file B 1 if a shape file rather than a font (1 bit)
			this._writer.WriteBit(style.Flags.HasFlag(StyleFlags.IsShape));

			//Vertical B 1 if vertical (4 bit of flag)
			this._writer.WriteBit(style.Flags.HasFlag(StyleFlags.VerticalText));
			//Fixed height BD 40
			this._writer.WriteBitDouble(style.Height);
			//Width factor BD 41
			this._writer.WriteBitDouble(style.Width);
			//Oblique ang BD 50
			this._writer.WriteBitDouble(style.ObliqueAngle);
			//Generation RC 71 Generation flags (not bit-pair coded).
			this._writer.WriteByte((byte)style.MirrorFlag);
			//Last height BD 42
			this._writer.WriteBitDouble(style.LastHeight);
			//Font name TV 3
			this._writer.WriteVariableText(style.Filename);
			//Bigfont name TV 4
			this._writer.WriteVariableText(style.BigFontFilename);

			this._writer.HandleReference(DwgReferenceType.HardPointer, this._document.TextStyles);

			this.registerObject(style);
		}

		private void writeUCS(UCS ucs)
		{
			this.writeCommonNonEntityData(ucs);

			//Common:
			//Entry name TV 2
			this._writer.WriteVariableText(ucs.Name);

			this.writeXrefDependantBit(ucs);

			//Origin 3BD 10
			this._writer.Write3BitDouble(ucs.Origin);
			//X - direction 3BD 11
			this._writer.Write3BitDouble(ucs.XAxis);
			//Y - direction 3BD 12
			this._writer.Write3BitDouble(ucs.YAxis);

			//R2000+:
			if (this.R2000Plus)
			{
				//Elevation BD 146
				this._writer.WriteBitDouble(ucs.Elevation);
				//OrthographicViewType BS 79	//dxf docs: 79	Always 0
				this._writer.WriteBitShort((short)ucs.OrthographicViewType);
				//OrthographicType BS 71
				this._writer.WriteBitShort((short)ucs.OrthographicType);
			}

			//Common:
			//Handle refs H ucs control object (soft pointer)
			this._writer.HandleReference(DwgReferenceType.SoftPointer, this._document.UCSs);

			//R2000 +:
			if (this.R2000Plus)
			{
				//Base UCS Handle H 346 hard pointer
				this._writer.HandleReference(DwgReferenceType.HardPointer, 0);
				//Named UCS Handle H -hard pointer, not present in DXF
				this._writer.HandleReference(DwgReferenceType.HardPointer, 0);
			}

			this.registerObject(ucs);
		}

		private void writeView(View view)
		{
			this.writeCommonNonEntityData(view);

			//Common:
			//Entry name TV 2
			this._writer.WriteVariableText(view.Name);

			this.writeXrefDependantBit(view);

			//View height BD 40
			this._writer.WriteBitDouble(view.Height);
			//View width BD 41
			this._writer.WriteBitDouble(view.Width);
			//View center 2RD 10(Not bit - pair coded.)
			this._writer.Write2RawDouble(view.Center);
			//Target 3BD 12
			this._writer.Write3BitDouble(view.Target);
			//View dir 3BD 11 DXF doc suggests from target toward camera.
			this._writer.Write3BitDouble(view.Direction);
			//Twist angle BD 50 Radians
			this._writer.WriteBitDouble(view.Angle);
			//Lens length BD 42
			this._writer.WriteBitDouble(view.LensLength);
			//Front clip BD 43
			this._writer.WriteBitDouble(view.FrontClipping);
			//Back clip BD 44
			this._writer.WriteBitDouble(view.BackClipping);

			//View mode X 71 4 bits: 0123
			//Note that only bits 0, 1, 2, and 4 of the 71 can be specified -- not bit 3 (8).
			//0 : 71's bit 0 (1)
			this._writer.WriteBit(view.ViewMode.HasFlag(ViewModeType.PerspectiveView));
			//1 : 71's bit 1 (2)
			this._writer.WriteBit(view.ViewMode.HasFlag(ViewModeType.FrontClipping));
			//2 : 71's bit 2 (4)
			this._writer.WriteBit(view.ViewMode.HasFlag(ViewModeType.BackClipping));
			//3 : OPPOSITE of 71's bit 4 (16)
			this._writer.WriteBit(view.ViewMode.HasFlag(ViewModeType.FrontClippingZ));

			//R2000+:
			if (this.R2000Plus)
			{
				//Render Mode RC 281
				this._writer.WriteByte((byte)view.RenderMode);
			}

			//R2007+:
			if (this.R2007Plus)
			{
				//Use default lights B ? Default value is true
				this._writer.WriteBit(true);
				//Default lighting RC ? Default value is 1
				this._writer.WriteByte(1);
				//Brightness BD ? Default value is 0
				this._writer.WriteBitDouble(0.0);
				//Contrast BD ? Default value is 0
				this._writer.WriteBitDouble(0.0);
				//Abient color CMC? Default value is AutoCAD indexed color 250
				this._writer.WriteCmColor(new Color(250));
			}

			//Common:
			//Pspace flag B 70 Bit 0(1) of the 70 - group.
			this._writer.WriteBit(view.Flags.HasFlag((StandardFlags)0b1));

			if (this.R2000Plus)
			{
				this._writer.WriteBit(view.IsUcsAssociated);
				if (view.IsUcsAssociated)
				{
					//Origin 3BD 10 This and next 4 R2000 items are present only if 72 value is 1.
					this._writer.Write3BitDouble(view.UcsOrigin);
					//X-direction 3BD 11
					this._writer.Write3BitDouble(view.UcsXAxis);
					//Y-direction 3BD 12
					this._writer.Write3BitDouble(view.UcsYAxis);
					//Elevation BD 146
					this._writer.WriteBitDouble(view.UcsElevation);
					//OrthographicViewType BS 79
					this._writer.WriteBitShort((short)view.UcsOrthographicType);
				}
			}

			//Common:
			//Handle refs H view control object (soft pointer)
			this._writer.HandleReference(DwgReferenceType.SoftPointer, this._document.Views);

			//R2007+:
			if (this.R2007Plus)
			{
				//Camera plottable B 73
				this._writer.WriteBit(view.IsPlottable);

				//Background handle H 332 soft pointer
				this._writer.HandleReference(DwgReferenceType.SoftPointer, 0);
				//Visual style H 348 hard pointer
				this._writer.HandleReference(DwgReferenceType.HardPointer, 0);
				//Sun H 361 hard owner
				this._writer.HandleReference(DwgReferenceType.HardOwnership, 0);
			}

			if (this.R2000Plus && view.IsUcsAssociated)
			{
				//TODO: Implement ucs reference for view
				//Base UCS Handle H 346 hard pointer
				this._writer.HandleReference(DwgReferenceType.HardPointer, 0);
				//Named UCS Handle H 345 hard pointer
				this._writer.HandleReference(DwgReferenceType.HardPointer, 0);
			}

			//R2007+:
			if (this.R2007Plus)
			{
				//Live section H 334 soft pointer
				this._writer.HandleReference(DwgReferenceType.SoftPointer, 0);
			}

			this.registerObject(view);
		}

		private void writeEntity(Entity entity)
		{
			switch (entity)
			{
				default:
					this.Notify($"Entity not implemented : {entity.GetType().FullName}", NotificationType.NotImplemented);
					break;
			}
		}

		private void writeLine(Line line)
		{
			this.writeCommonEntityData(line);

			//R13-R14 Only:
			if (this.R13_14Only)
			{
				//Start pt 3BD 10
				this._writer.Write3BitDouble(line.StartPoint);
				//End pt 3BD 11
				this._writer.Write3BitDouble(line.EndPoint);
			}


			//R2000+:
			if (this.R2000Plus)
			{
				//Z’s are zero bit B
				bool flag = line.StartPoint.Z == 0.0 && line.EndPoint.Z == 0.0;
				this._writer.WriteBit(flag);

				//Start Point x RD 10
				this._writer.WriteRawDouble(line.StartPoint.X);
				//End Point x DD 11 Use 10 value for default
				this._writer.WriteBitDoubleWithDefault(line.EndPoint.X, line.StartPoint.X);
				//Start Point y RD 20
				this._writer.WriteRawDouble(line.StartPoint.Y);
				//End Point y DD 21 Use 20 value for default
				this._writer.WriteBitDoubleWithDefault(line.EndPoint.Y, line.StartPoint.Y);

				if (!flag)
				{
					//Start Point z RD 30 Present only if “Z’s are zero bit” is 0
					this._writer.WriteRawDouble(line.StartPoint.Z);
					//End Point z DD 31 Present only if “Z’s are zero bit” is 0, use 30 value for default.
					this._writer.WriteBitDoubleWithDefault(line.EndPoint.Z, line.StartPoint.Z);
				}
			}

			//Common:
			//Thickness BT 39
			this._writer.WriteBitThickness(line.Thickness);
			//Extrusion BE 210
			this._writer.WriteBitExtrusion(line.Normal);

			this.registerObject(line);
		}

		private void writePoint(Point point)
		{
			this.writeCommonEntityData(point);

			//Point 3BD 10
			this._writer.Write3BitDouble(point.Location);
			//Thickness BT 39
			this._writer.WriteBitThickness(point.Thickness);
			//Extrusion BE 210
			this._writer.WriteBitExtrusion(point.Normal);
			//X - axis ang BD 50 See DXF documentation
			this._writer.WriteBitDouble(point.Rotation);

			this.registerObject(point);
		}

		private void Notify(string message, NotificationType type)
		{
			this.OnNotification?.Invoke(this, new NotificationEventArgs(message, type));
		}
	}
}
