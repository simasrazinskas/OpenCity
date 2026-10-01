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
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets
{
	public enum CityPanelPlacement { Free, Center, TopLeft, TopCenter, TopRight, BottomLeft, BottomCenter, BottomRight }

	/// <summary>
	/// A HUD panel that positions itself every frame from its current size: anchored in the screen area left free
	/// by the sidebar, the bottom bar and the alert strip, and always clamped into it. Logic may resize the panel at
	/// any time (the position follows) and the panel follows window resizes and UI scale changes on its own.
	/// </summary>
	public class CityPanelWidget : BackgroundWidget
	{
		public CityPanelPlacement Placement = CityPanelPlacement.Center;

		/// <summary>Keep the top band free for the alert strip.</summary>
		public bool BelowAlerts = true;

		/// <summary>Optional id of a sibling panel: while it is visible, this panel docks to its left (top-aligned).</summary>
		public string LeftOf;

		/// <summary>Optional ids of sibling widgets this panel moves away from (sideways, then upwards) when it overlaps them and there is room.</summary>
		public string[] Avoid = [];

		Widget leftOf;
		Widget[] avoid;

		public CityPanelWidget() { }

		protected CityPanelWidget(CityPanelWidget other)
			: base(other)
		{
			Placement = other.Placement;
			BelowAlerts = other.BelowAlerts;
			LeftOf = other.LeftOf;
			Avoid = other.Avoid;
		}

		public override CityPanelWidget Clone() { return new CityPanelWidget(this); }

		public override void Tick()
		{
			Place();
		}

		public override void Draw()
		{
			// A panel opened after this frame's tick is placed before its first draw.
			Place();
			base.Draw();
		}

		/// <summary>Applies the placement rule now (also used by logic right after it resized the panel).</summary>
		public void Place()
		{
			var area = CityLayout.WorkArea(BelowAlerts);
			if (LeftOf != null)
			{
				leftOf ??= Parent?.GetOrNull(LeftOf);
				if (leftOf != null && leftOf.IsVisible())
				{
					Bounds.X = leftOf.Bounds.X - CityLayout.Gap - Bounds.Width;
					Bounds.Y = leftOf.Bounds.Y;
					CityLayout.ClampInto(this, area);
					return;
				}
			}

			var w = Bounds.Width;
			var h = Bounds.Height;
			var centerX = CityLayout.Snap(area.X + (area.Width - w) / 2);
			var centerY = CityLayout.Snap(area.Y + (area.Height - h) / 2);
			switch (Placement)
			{
				case CityPanelPlacement.Center: Bounds.X = centerX; Bounds.Y = centerY; break;
				case CityPanelPlacement.TopLeft: Bounds.X = area.Left; Bounds.Y = area.Top; break;
				case CityPanelPlacement.TopCenter: Bounds.X = centerX; Bounds.Y = area.Top; break;
				case CityPanelPlacement.TopRight: Bounds.X = area.Right - w; Bounds.Y = area.Top; break;
				case CityPanelPlacement.BottomLeft: Bounds.X = area.Left; Bounds.Y = area.Bottom - h; break;
				case CityPanelPlacement.BottomCenter: Bounds.X = centerX; Bounds.Y = area.Bottom - h; break;
				case CityPanelPlacement.BottomRight: Bounds.X = area.Right - w; Bounds.Y = area.Bottom - h; break;
			}

			CityLayout.ClampInto(this, area);
			AvoidOthers(area);
		}

		void AvoidOthers(Rectangle area)
		{
			if (Avoid.Length == 0 || Parent == null)
				return;

			avoid ??= Array.ConvertAll(Avoid, Parent.GetOrNull);
			foreach (var other in avoid)
			{
				if (other == null || !other.IsVisible())
					continue;

				var o = other.Bounds;
				var mine = new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);
				if (!mine.IntersectsWith(new Rectangle(o.X, o.Y, o.Width, o.Height)))
					continue;

				if (o.Right + CityLayout.Gap + Bounds.Width <= area.Right)
					Bounds.X = Math.Max(Bounds.X, o.Right + CityLayout.Gap);
				else if (o.Left - CityLayout.Gap - Bounds.Width >= area.Left)
					Bounds.X = Math.Min(Bounds.X, o.Left - CityLayout.Gap - Bounds.Width);
				else if (o.Top - CityLayout.Gap - Bounds.Height >= area.Top)
					Bounds.Y = o.Top - CityLayout.Gap - Bounds.Height;
			}
		}

		/// <summary>The largest height this panel can have on screen right now.</summary>
		public int MaxHeight => CityLayout.WorkArea(BelowAlerts).Height;

		/// <summary>The screen area this panel is kept in.</summary>
		public Rectangle Area => CityLayout.WorkArea(BelowAlerts);
	}
}
