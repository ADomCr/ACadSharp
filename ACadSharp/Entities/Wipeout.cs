﻿using ACadSharp.Attributes;
using CSMath;
using System;
using System.Collections.Generic;

namespace ACadSharp.Entities
{
	[Flags]
	public enum ImageDisplayFlags : short
	{
		/// <summary>
		/// None
		/// </summary>
		None = 0,
		/// <summary>
		/// Show image
		/// </summary>
		ShowImage = 1,
		/// <summary>
		/// Show image when not aligned with screen
		/// </summary>
		ShowNotAlignedImage = 2,
		/// <summary>
		/// Use clipping boundary
		/// </summary>
		UseClippingBoundary = 8,
		/// <summary>
		/// Transparency is on
		/// </summary>
		TransparencyIsOn = 8
	}

	/// <summary>
	/// Clipping boundary type
	/// </summary>
	public enum ClipType : short
	{
		/// <summary>
		/// Rectangular
		/// </summary>
		Rectangular = 1,
		/// <summary>
		/// Polygonal
		/// </summary>
		Polygonal = 2
	}

	/// <summary>
	/// Represents a <see cref="Wipeout"/> entity.
	/// </summary>
	/// <remarks>
	/// Object name <see cref="DxfFileToken.EntityWipeout"/> <br/>
	/// Dxf class name <see cref="DxfSubclassMarker.Wipeout"/>
	/// </remarks>
	[DxfName(DxfFileToken.EntityWipeout)]
	[DxfSubClass(DxfSubclassMarker.Wipeout)]
	public class Wipeout : Entity
	{
		/// <inheritdoc/>
		public override ObjectType ObjectType => ObjectType.UNLISTED;

		//100	Subclass marker(AcDbRasterImage)

		/// <summary>
		/// Class version
		/// </summary>
		[DxfCodeValue(90)]
		public int ClassVersion { get; set; }

		/// <summary>
		/// Insertion point(in WCS)
		/// </summary>
		[DxfCodeValue(10, 20, 30)]
		public XYZ InsertPoint { get; set; }

		/// <summary>
		/// U-vector of a single pixel(points along the visual bottom of the image, starting at the insertion point) (in WCS)
		/// </summary>
		[DxfCodeValue(11, 21, 31)]
		public XYZ UVector { get; set; }

		/// <summary>
		/// V-vector of a single pixel(points along the visual left side of the image, starting at the insertion point) (in WCS)
		/// </summary>
		[DxfCodeValue(12, 22, 32)]
		public XYZ VVector { get; set; }

		/// <summary>
		/// Image size in pixels
		/// </summary>
		/// <remarks>
		/// 2D point(U and V values)
		/// </remarks>
		[DxfCodeValue(13, 23)]
		public XY Size { get; set; }

		/// <summary>
		/// Image display properties
		/// </summary>
		[DxfCodeValue(70)]
		public ImageDisplayFlags Flags { get; set; }

		/// <summary>
		/// Clipping state
		/// </summary>
		[DxfCodeValue(280)]
		public bool ClippingState { get; set; }

		/// <summary>
		/// Brightness
		/// </summary>
		/// <remarks>
		/// 0-100; default = 50
		/// </remarks>
		[DxfCodeValue(281)]
		public byte Brightness
		{
			get { return this._brightness; }
			set
			{
				if (value < 0 || value > 100)
				{
					throw new ArgumentException($"Invalid Brightness value: {value}, must be in range 0-100");
				}

				this._brightness = value;
			}
		}

		/// <summary>
		/// Contrast
		/// </summary>
		/// <remarks>
		/// 0-100; default = 50
		/// </remarks>
		[DxfCodeValue(282)]
		public byte Contrast
		{
			get { return this._contrast; }
			set
			{
				if (value < 0 || value > 100)
				{
					throw new ArgumentException($"Invalid Brightness value: {value}, must be in range 0-100");
				}

				this._contrast = value;
			}
		}

		/// <summary>
		/// Fade
		/// </summary>
		/// <remarks>
		/// 0-100; default = 0
		/// </remarks>
		[DxfCodeValue(283)]
		public byte Fade
		{
			get { return this._fade; }
			set
			{
				if (value < 0 || value > 100)
				{
					throw new ArgumentException($"Invalid Brightness value: {value}, must be in range 0-100");
				}

				this._fade = value;
			}
		}

		/// <summary>
		/// Clipping boundary type
		/// </summary>
		[DxfCodeValue(71)]
		public ClipType ClipType { get; set; }

		/// <summary>
		/// Clip boundary vertices
		/// </summary>
		[DxfCodeValue(DxfReferenceType.Count, 91)]
		public List<XY> ClipBoundaryVertices { get; set; } = new List<XY>();
		//14	Clip boundary vertex(in OCS)
		//DXF: X value; APP: 2D point(multiple entries)
		//NOTE 1) For rectangular clip boundary type, two opposite corners must be specified.Default is (-0.5,-0.5), (size.x-0.5, size.y-0.5). 2) For polygonal clip boundary type, three or more vertices must be specified.Polygonal vertices must be listed sequentially
		//24	DXF: Y value of clip boundary vertex(in OCS) (multiple entries)

		//340	Hard reference to imagedef object

		//360	Hard reference to imagedef_reactor object

		private byte _brightness = 50;
		private byte _contrast = 50;
		private byte _fade = 0;
	}
}
