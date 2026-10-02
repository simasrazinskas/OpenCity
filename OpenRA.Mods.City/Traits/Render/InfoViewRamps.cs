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
using OpenRA.Mods.City.Widgets;
using OpenRA.Primitives;

namespace OpenRA.Mods.City.Traits
{
	/// <summary>
	/// The info view colour ramps of the iso design (NET, design/iso/net/infoviews; ART sequences/overlays.yaml header):
	/// 11 steps per ramp (0, 10 .. 100), 16 category colours and 6 resource hues x richness. Alpha is the dither density
	/// of ART's ramp tiles. The world
	/// overlay, the building tint and the UI legends all use these so they match exactly.
	/// </summary>
	public static class InfoViewRamps
	{
		static Color C(int a, int r, int g, int b) { return Color.FromArgb(a, r, g, b); }

		// good-000 .. good-100 (Bad is the same ramp reversed). Coverage 640/1024.
		static readonly Color[] Good =
		[
			C(158, 229, 75, 60), C(158, 232, 102, 63), C(158, 234, 128, 66), C(158, 237, 155, 70), C(158, 239, 181, 73),
			C(158, 242, 208, 76), C(158, 206, 207, 79), C(158, 170, 205, 82), C(158, 135, 204, 84), C(158, 99, 202, 87),
			C(158, 63, 201, 90)
		];

		static readonly Color[] Pollution =
		[
			C(15, 150, 220, 130), C(33, 168, 210, 112), C(48, 186, 200, 94), C(66, 204, 190, 76), C(82, 222, 180, 58),
			C(99, 240, 170, 40), C(117, 213, 145, 36), C(133, 186, 120, 32), C(150, 159, 95, 28), C(166, 132, 70, 24),
			C(184, 105, 45, 20)
		];

		static readonly Color[] Blue =
		[
			C(102, 205, 230, 255), C(110, 188, 216, 251), C(117, 172, 201, 247), C(125, 156, 186, 243), C(133, 139, 172, 239),
			C(140, 122, 158, 235), C(148, 106, 143, 231), C(156, 90, 128, 227), C(163, 73, 114, 223), C(171, 57, 100, 219),
			C(178, 40, 85, 215)
		];

		static readonly Color[] Green =
		[
			C(76, 25, 35, 25), C(87, 30, 53, 32), C(97, 34, 71, 39), C(107, 38, 89, 46), C(117, 43, 107, 53),
			C(128, 48, 125, 60), C(138, 52, 143, 67), C(148, 56, 161, 74), C(158, 61, 179, 81), C(168, 66, 197, 88),
			C(178, 70, 215, 95)
		];

		static readonly Color[] Category =
		[
			C(153, 229, 75, 60), C(153, 63, 169, 245), C(153, 126, 217, 87), C(153, 242, 201, 76),
			C(153, 176, 124, 232), C(153, 255, 140, 58), C(153, 79, 214, 198), C(153, 232, 107, 180),
			C(153, 154, 201, 60), C(153, 90, 111, 224), C(153, 214, 158, 107), C(153, 107, 201, 155),
			C(153, 201, 90, 123), C(153, 143, 168, 184), C(153, 224, 224, 90), C(153, 90, 160, 138)
		];

		// Kinds in NaturalResource order: yellow (fertile), green (forest), blue (water), coal (oil), stone (ore), cyan (fish).
		static readonly Color[] ResourceHues =
		[
			C(255, 235, 205, 60), C(255, 60, 185, 75), C(255, 70, 130, 235),
			C(255, 70, 70, 90), C(255, 170, 160, 150), C(255, 60, 200, 210)
		];

		/// <summary>Number of steps of the continuous ramps (0, 10 .. 100).</summary>
		public const int Steps = 11;

		/// <summary>Number of distinct category colours (districts, road classes); indices wrap.</summary>
		public static int CategoryCount => Category.Length;

		/// <summary>
		/// Colour of a 0..100 value (category index for <see cref="InfoRamp.Category"/>, kind * 16 + richness 0..10 for
		/// <see cref="InfoRamp.Resource"/>). Continuous ramps snap to NET's 11 steps (RCT2-style bands).
		/// </summary>
		public static Color ColorOf(InfoRamp ramp, int value)
		{
			switch (ramp)
			{
				case InfoRamp.Category:
					return Category[(value % Category.Length + Category.Length) % Category.Length];
				case InfoRamp.Resource:
				{
					var hue = ResourceHues[Math.Clamp(value / 16, 0, ResourceHues.Length - 1)];
					var richness = Math.Clamp(value % 16, 0, 10);

					// Dither density 0.30 .. 0.80 over the richness steps (ART overlays.resource).
					var alpha = (int)MathF.Round((0.30f + richness * 0.05f) * 255);
					return C(alpha, hue.R, hue.G, hue.B);
				}

				default:
					return Step(ramp, (Math.Clamp(value, 0, 100) + 5) / 10);
			}
		}

		/// <summary>Step 0..10 of a continuous ramp (for legends).</summary>
		public static Color Step(InfoRamp ramp, int step)
		{
			step = Math.Clamp(step, 0, Steps - 1);
			switch (ramp)
			{
				case InfoRamp.Good: return Good[step];
				case InfoRamp.Bad: return Good[Steps - 1 - step];
				case InfoRamp.Pollution: return Pollution[step];
				case InfoRamp.Blue: return Blue[step];
				case InfoRamp.Green: return Green[step];
				case InfoRamp.Category: return Category[step % Category.Length];
				default: return Good[step];
			}
		}

		/// <summary>Opaque version of a ramp colour (building tint, legend swatches).</summary>
		public static Color Opaque(Color c) { return C(255, c.R, c.G, c.B); }
	}
}
