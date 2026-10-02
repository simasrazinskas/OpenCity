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
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>
	/// An icon of the RCT2 icon set (tools/iso_ui_icons_*.py; names like `cat_roads`, `pnl_budget`) drawn centred in the
	/// widget at a logical size of 12, 16, 20, 24 or 32 pixels: native pixel art at every UI scale. Inside a button it
	/// sinks with the button when pressed or toggled and shows the embossed RCT2 disabled look when the button is disabled.
	/// Thumbnail = true draws the build-menu thumbnail of the actor named by Icon instead.
	/// </summary>
	public class CityIconWidget : Widget
	{
		public string Icon;
		public int Size = 16;
		public bool Thumbnail;
		public Func<string> GetIcon;
		public Func<bool> IsDisabled;

		public CityIconWidget()
		{
			GetIcon = () => Icon;
		}

		protected CityIconWidget(CityIconWidget other)
			: base(other)
		{
			Icon = other.Icon;
			Size = other.Size;
			Thumbnail = other.Thumbnail;
			GetIcon = other.GetIcon;
			IsDisabled = other.IsDisabled;
		}

		public override CityIconWidget Clone() { return new CityIconWidget(this); }

		public override void Draw()
		{
			var name = GetIcon();
			if (string.IsNullOrEmpty(name))
				return;

			var button = Parent as ButtonWidget;
			var disabled = IsDisabled?.Invoke() ?? button?.IsDisabled() ?? false;
			var sprite = Thumbnail ? CityTheme.Thumbnail(name) : CityTheme.Icon(name, Size, disabled);
			if (sprite == null)
				return;

			var rb = RenderBounds;
			float x = rb.X, y = rb.Y;
			if (button != null && !disabled && (button.Depressed || button.IsHighlighted()))
			{
				var b = CityTheme.BevelLogical;
				x += b;
				y += b;
			}

			CityTheme.DrawCentered(sprite, x, y, rb.Width, rb.Height);
		}
	}
}
