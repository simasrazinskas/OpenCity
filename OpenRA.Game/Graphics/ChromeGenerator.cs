#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using OpenRA.FileSystem;
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	/// <summary>
	/// Draws chrome collections at runtime instead of loading them from images.
	/// A collection opts in with `Generator: Name`, which resolves to the class `NameGenerator`.
	/// The generator is run once per device scale (window scale times UI scale) for all the collections
	/// that name it, so the art can be drawn pixel-exact at that scale.
	/// </summary>
	public interface IChromeGenerator
	{
		void Generate(ChromeGeneratorContext context);
	}

	/// <summary>An RGBA (non-premultiplied, row-major) image in device pixels.</summary>
	public sealed class ChromeBitmap
	{
		public readonly int Width;
		public readonly int Height;
		public readonly byte[] Data;

		public ChromeBitmap(int width, int height)
		{
			Width = Math.Max(0, width);
			Height = Math.Max(0, height);
			Data = new byte[4 * Width * Height];
		}

		public ChromeBitmap(int width, int height, byte[] rgba)
		{
			if (rgba.Length != 4 * width * height)
				throw new ArgumentException("The pixel data does not match the bitmap size.", nameof(rgba));

			Width = width;
			Height = height;
			Data = rgba;
		}
	}

	public sealed class ChromeGeneratorContext
	{
		/// <summary>Device pixels per logical (layout) pixel.</summary>
		public readonly float Scale;

		/// <summary>The pixel-art unit: the whole number of device pixels that one art pixel covers.</summary>
		public readonly int Unit;

		public readonly IReadOnlyFileSystem FileSystem;

		/// <summary>The (inheritance resolved) definitions of the collections that use this generator.</summary>
		public readonly IReadOnlyDictionary<string, MiniYaml> Collections;

		internal readonly Dictionary<string, Dictionary<string, Sprite>> Images = [];
		internal readonly Dictionary<string, Sprite[]> Panels = [];
		internal readonly Dictionary<string, Size> PanelMinimumSizes = [];

		readonly SheetBuilder sheetBuilder;

		internal ChromeGeneratorContext(float scale, IReadOnlyFileSystem fileSystem, IReadOnlyDictionary<string, MiniYaml> collections, SheetBuilder sheetBuilder)
		{
			Scale = scale;
			Unit = PixelUnit(scale);
			FileSystem = fileSystem;
			Collections = collections;
			this.sheetBuilder = sheetBuilder;
		}

		/// <summary>Whether anything (images or a panel) was added for a collection.</summary>
		public bool Contains(string collection)
		{
			return Images.ContainsKey(collection) || Panels.ContainsKey(collection);
		}

		/// <summary>The whole number of device pixels per art pixel for a device scale: max(1, round(scale)).</summary>
		public static int PixelUnit(float scale)
		{
			return Math.Max(1, (int)Math.Round(scale, MidpointRounding.AwayFromZero));
		}

		/// <summary>Device size of a logical length: always whole pixels, at least one.</summary>
		public int Device(float logical)
		{
			return Math.Max(1, (int)Math.Round(logical * Scale, MidpointRounding.AwayFromZero));
		}

		Sprite Upload(ChromeBitmap bitmap, float spriteScale)
		{
			var sprite = sheetBuilder.Allocate(new Size(bitmap.Width, bitmap.Height), spriteScale);
			Util.FastCopyIntoChannel(sprite, bitmap.Data, SpriteFrameType.Rgba32);
			sprite.Sheet.CommitBufferedData(sprite.Bounds);
			return sprite;
		}

		/// <summary>
		/// Adds an image. Its logical size is the device size divided by Scale, unless logicalWidth is given:
		/// then the image is drawn at that logical width (and the same factor vertically).
		/// </summary>
		public void AddImage(string collection, string image, ChromeBitmap bitmap, float logicalWidth = 0)
		{
			// Empty images (e.g. an unchecked checkmark) are valid zero-sized sprites
			if (bitmap.Width == 0 || bitmap.Height == 0)
			{
				SetImage(collection, image, sheetBuilder.Allocate(new Size(0, 0), 1f / Scale));
				return;
			}

			var spriteScale = logicalWidth > 0 ? logicalWidth / bitmap.Width : 1f / Scale;
			SetImage(collection, image, Upload(bitmap, spriteScale));
		}

		/// <summary>Makes `collection/image` an alias of an image added before.</summary>
		public bool AddImageAlias(string collection, string image, string sourceCollection, string sourceImage)
		{
			if (!Images.TryGetValue(sourceCollection, out var images) || !images.TryGetValue(sourceImage, out var sprite))
				return false;

			SetImage(collection, image, sprite);
			return true;
		}

		void SetImage(string collection, string image, Sprite sprite)
		{
			if (!Images.TryGetValue(collection, out var images))
				Images[collection] = images = [];

			images[image] = sprite;
		}

		/// <summary>
		/// Adds a nine-slice panel. The insets are device pixels; edges and centre are tiled (or stretched when
		/// they are uniform), so keep the middle of the bitmap free of details that must not repeat.
		/// </summary>
		public void AddPanel(string collection, ChromeBitmap bitmap, int left, int top, int right, int bottom, PanelSides sides = PanelSides.All)
		{
			var sprite = Upload(bitmap, 1f / Scale);
			var b = sprite.Bounds;
			var midW = b.Width - left - right;
			var midH = b.Height - top - bottom;
			var rects = new (PanelSides Side, Rectangle Rect)[]
			{
				(PanelSides.Top | PanelSides.Left, new Rectangle(b.X, b.Y, left, top)),
				(PanelSides.Top, new Rectangle(b.X + left, b.Y, midW, top)),
				(PanelSides.Top | PanelSides.Right, new Rectangle(b.X + left + midW, b.Y, right, top)),
				(PanelSides.Left, new Rectangle(b.X, b.Y + top, left, midH)),
				(PanelSides.Center, new Rectangle(b.X + left, b.Y + top, midW, midH)),
				(PanelSides.Right, new Rectangle(b.X + left + midW, b.Y + top, right, midH)),
				(PanelSides.Bottom | PanelSides.Left, new Rectangle(b.X, b.Y + top + midH, left, bottom)),
				(PanelSides.Bottom, new Rectangle(b.X + left, b.Y + top + midH, midW, bottom)),
				(PanelSides.Bottom | PanelSides.Right, new Rectangle(b.X + left + midW, b.Y + top + midH, right, bottom)),
			};

			var sprites = new Sprite[9];
			for (var i = 0; i < 9; i++)
			{
				var r = rects[i].Rect;
				if (sides.HasSide(rects[i].Side) && r.Width > 0 && r.Height > 0)
					sprites[i] = new Sprite(sprite.Sheet, r, sprite.Channel, 1f / Scale);
			}

			Panels[collection] = sprites;
			PanelMinimumSizes[collection] = new Size(
				(int)Math.Ceiling((left + right) / Scale), (int)Math.Ceiling((top + bottom) / Scale));
		}

		/// <summary>Makes a panel an alias of a panel added before.</summary>
		public bool AddPanelAlias(string collection, string sourceCollection)
		{
			if (!Panels.TryGetValue(sourceCollection, out var sprites))
				return false;

			Panels[collection] = sprites;
			PanelMinimumSizes[collection] = PanelMinimumSizes[sourceCollection];
			return true;
		}
	}
}
