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
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>
	/// Shared layout metrics of the OpenCity HUD (logical UI pixels, i.e. after UI scaling) and helpers that keep
	/// panels inside the free screen area at any window size and UI scale.
	/// Spacing grid: everything is placed on multiples of <see cref="Grid"/>.
	/// </summary>
	public static class CityLayout
	{
		public const int Grid = 4;

		/// <summary>Panel padding (inner margin between the frame and the content).</summary>
		public const int Pad = 12;

		/// <summary>Gap between panels and between a panel and the screen edge / HUD.</summary>
		public const int Gap = 8;

		/// <summary>Height of the top toolbar (RCT2 HUD).</summary>
		public const int ToolbarHeight = 31;

		/// <summary>Height of the bottom status bar.</summary>
		public const int StatusBarHeight = 38;

		static int2 lastResolution;
		static int version;

		/// <summary>The logical window size (changes with the window and the UI scale).</summary>
		public static int2 Window => new(Game.Renderer.Resolution.Width, Game.Renderer.Resolution.Height);

		/// <summary>
		/// Increments whenever the logical window size changed since the previous call (the UI scale slider or a
		/// window resize). Code-placed widgets compare it with the value they laid out for.
		/// </summary>
		public static int Version
		{
			get
			{
				var window = Window;
				if (window != lastResolution)
				{
					lastResolution = window;
					version++;
				}

				return version;
			}
		}

		/// <summary>The screen area left free by the toolbar and the status bar, in logical pixels (windows stay inside it).</summary>
		public static Rectangle WorkArea(bool belowToolbar = true)
		{
			var window = Window;

			// Outside the HUD (menus, dialogs: belowToolbar false) the whole window is free.
			var top = belowToolbar ? ToolbarHeight + Gap : Gap;
			var bottom = belowToolbar ? window.Y - StatusBarHeight - Gap : window.Y - Gap;
			return Rectangle.FromLTRB(Gap, top, Math.Max(Gap, window.X - Gap), Math.Max(top, bottom));
		}

		/// <summary>Moves (never resizes) a widget so it lies inside <paramref name="area"/>; widgets larger than the area keep their top-left corner on it.</summary>
		public static void ClampInto(Widget widget, Rectangle area)
		{
			var b = widget.Bounds;
			var x = Math.Max(area.Left, Math.Min(b.X, area.Right - b.Width));
			var y = Math.Max(area.Top, Math.Min(b.Y, area.Bottom - b.Height));
			widget.Bounds.X = x;
			widget.Bounds.Y = y;
		}

		/// <summary>Centres a widget in <paramref name="area"/> (snapped to the grid) and clamps it into the area.</summary>
		public static void CenterIn(Widget widget, Rectangle area)
		{
			widget.Bounds.X = Snap(area.X + (area.Width - widget.Bounds.Width) / 2);
			widget.Bounds.Y = Snap(area.Y + (area.Height - widget.Bounds.Height) / 2);
			ClampInto(widget, area);
		}

		/// <summary>Rounds down to the spacing grid.</summary>
		public static int Snap(int value)
		{
			return value - (value % Grid + Grid) % Grid;
		}
	}
}
