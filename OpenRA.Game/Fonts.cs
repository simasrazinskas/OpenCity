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

using System.Collections.Frozen;
using System.Collections.Generic;

namespace OpenRA
{
	public class FontData
	{
		public readonly string Font;
		public readonly int Size;
		public readonly int Ascender;

		[Desc("Optional pixel font faces. When set, glyphs are rendered without antialiasing at their design pixel size",
			"and enlarged by a whole number of device pixels, so the text stays sharp at any UI scale.",
			"Each entry is `<file> <design em size in px> [bold]`; `bold` marks a real bold face.",
			"For every UI scale the face and multiple whose cap height is closest to Size * PixelCapRatio device pixels are used.")]
		public readonly string[] PixelFaces = [];

		[Desc("Prefer the faces marked `bold`. When a regular face has to be used at 2+ device pixels per font pixel",
			"it is emboldened by half a font pixel.")]
		public readonly bool PixelBold = false;

		[Desc("Cap height of the text relative to Size.")]
		public readonly float PixelCapRatio = 0.643f;

		[Desc("Average character advance of the text the layouts were designed for, relative to Size",
			"(measured on SpriteFont.PixelFace.ReferenceText).")]
		public readonly float PixelAdvanceRatio = 0.48f;

		[Desc("Faces whose text would be more than this much wider than PixelAdvanceRatio allows are only used as a last resort.")]
		public readonly float PixelWidthLimit = 1.25f;

		[Desc("Allowed relative difference between the drawn and the requested cap height.")]
		public readonly float PixelSizeTolerance = 0.15f;

		[Desc("Smallest cap height in device pixels, so that small fonts stay readable at small UI scales.")]
		public readonly int PixelMinCapHeight = 0;

		[Desc("Name of another pixel font that this font must always be drawn larger than (e.g. MediumBold larger than Bold).")]
		public readonly string PixelLargerThan = null;
	}

	public class Fonts : IGlobalModData
	{
		[FieldLoader.LoadUsing(nameof(LoadFonts))]
		public readonly FrozenDictionary<string, FontData> FontList;

		static object LoadFonts(MiniYaml y)
		{
			var ret = new Dictionary<string, FontData>(y.Nodes.Length);
			foreach (var node in y.Nodes)
				ret.Add(node.Key, FieldLoader.Load<FontData>(node.Value));

			return ret.ToFrozenDictionary();
		}
	}
}
