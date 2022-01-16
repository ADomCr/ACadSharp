﻿using ACadSharp.Attributes;
using ACadSharp.Entities;
using ACadSharp.IO.Templates;
using System.Collections.Generic;
using System.Text;

namespace ACadSharp.Tables
{
	public class TextStyle : TableEntry
	{
		public override ObjectType ObjectType => ObjectType.STYLE;

		public override string ObjectName => DxfFileToken.TableStyle;

		/// <summary>
		/// Default text style.
		/// </summary>
		public static TextStyle Default { get { return new TextStyle("STANDARD"); } }

		//100	Subclass marker(AcDbTextStyleTableRecord)

		/// <summary>
		/// Style state flags.
		/// </summary>
		public new StyleFlags Flags { get; set; }

		/// <summary>
		/// Fixed text height; 0 if not fixed
		/// </summary>
		[DxfCodeValue(40)]
		public double Height { get; set; }

		/// <summary>
		/// Width factor
		/// </summary>
		[DxfCodeValue(41)]
		public double Width { get; set; }

		/// <summary>
		/// Specifies the oblique angle of the object.
		/// </summary>
		/// <value>
		/// The angle in radians within the range of -85 to +85 degrees. A positive angle denotes a lean to the right; a negative value will have 2*PI added to it to convert it to its positive equivalent.
		/// </value>
		[DxfCodeValue(50)]
		public double ObliqueAngle { get; set; } = 0.0;

		/// <summary>
		/// Mirror flags.
		/// </summary>
		[DxfCodeValue(71)]
		public TextMirrorFlag MirrorFlag { get; set; } = TextMirrorFlag.None;

		/// <summary>
		/// Last height used.
		/// </summary>
		[DxfCodeValue(42)]
		public double LastHeight { get; set; }

		/// <summary>
		/// Primary font file name.
		/// </summary>
		[DxfCodeValue(3)]
		public string Filename { get; set; }

		/// <summary>
		/// Bigfont file name; blank if none.
		/// </summary>
		[DxfCodeValue(4)]
		public string BigFontFilename { get; set; }

		/// <summary>
		/// A long value which contains a truetype font’s pitch and family, character set, and italic and bold flags
		/// </summary>
		[DxfCodeValue(1071)]
		public FontFlags TrueType { get; set; }

		internal TextStyle() : base() { }

		public TextStyle(string name) : base(name) { }
	}
}
