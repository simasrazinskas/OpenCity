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

using OpenRA.Mods.City.UIArt;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>The darkened city picture of the loading screens, covering the whole screen (see CityLoadBackdrop).</summary>
	public class CityBackdropWidget : Widget
	{
		CityLoadBackdrop backdrop;

		public CityBackdropWidget() { }

		protected CityBackdropWidget(CityBackdropWidget other)
			: base(other) { }

		public override CityBackdropWidget Clone() { return new CityBackdropWidget(this); }

		public override void Draw()
		{
			var res = Game.Renderer.Resolution;
			WidgetUtils.FillRectWithColor(new Rectangle(0, 0, res.Width, res.Height), Color.FromArgb(10, 12, 28));
			backdrop ??= new CityLoadBackdrop(Game.ModData.DefaultFileSystem);
			backdrop.Draw(Game.Renderer);
		}

		public override void Removed()
		{
			backdrop?.Dispose();
			backdrop = null;
			base.Removed();
		}
	}
}
