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
using OpenRA.Primitives;

namespace OpenRA
{
	public class WorldViewportSizes : IGlobalModData
	{
		public readonly int2 CloseWindowHeights = new(480, 600);
		public readonly int2 MediumWindowHeights = new(600, 900);
		public readonly int2 FarWindowHeights = new(900, 1300);

		public readonly float DefaultScale = 1.0f;
		public readonly float MaxZoomScale = 2.0f;
		public readonly int MaxZoomWindowHeight = 240;
		public readonly bool AllowNativeZoom = true;

		public readonly Size MinEffectiveResolution = new(1024, 720);

		[Desc("If set, mouse wheel and hotkey zooming steps between these absolute zoom levels (native window pixels",
			"per world pixel) instead of zooming continuously, and the camera distance setting picks the closest default level.",
			"Whole numbers (and halves) keep pixel art pixel-perfect.")]
		public readonly float[] ZoomLevels = [];

		[Desc("Duration in milliseconds of the animated transition between ZoomLevels. 0 disables the animation.")]
		public readonly int ZoomAnimationDuration = 0;

		[Desc("Keep the whole viewport inside the map bounds (instead of only its center), so no void shows beyond the map edges.")]
		public readonly bool KeepViewInsideMap = false;

		[Desc("Smallest UI scale offered by the UI scale setting.")]
		public readonly float MinUIScale = 0.5f;

		[Desc("Largest UI scale offered by the UI scale setting (it is further limited by MinEffectiveResolution).")]
		public readonly float MaxUIScale = 3f;

		/// <summary>Largest UI scale that keeps the effective resolution at or above <see cref="MinEffectiveResolution"/> (at least MinUIScale).</summary>
		public float MaxUIScaleFor(Size nativeResolution)
		{
			var max = Math.Min(
				(float)nativeResolution.Width / MinEffectiveResolution.Width,
				(float)nativeResolution.Height / MinEffectiveResolution.Height);

			return Math.Max(Math.Min(MinUIScale, 1f), Math.Min(MaxUIScale, max));
		}

		/// <summary>
		/// The UI scale to apply for a requested setting: values that would push the effective resolution below the minimum
		/// are clamped to the largest scale that fits (the setting itself is kept, so it applies again on a larger window).
		/// </summary>
		public float ClampUIScale(float scale, Size nativeResolution)
		{
			return Math.Clamp(scale, Math.Min(MinUIScale, 1f), MaxUIScaleFor(nativeResolution));
		}

		public int2 GetSizeRange(WorldViewport distance)
		{
			return distance == WorldViewport.Close ? CloseWindowHeights
				: distance == WorldViewport.Medium ? MediumWindowHeights
				: FarWindowHeights;
		}
	}
}
