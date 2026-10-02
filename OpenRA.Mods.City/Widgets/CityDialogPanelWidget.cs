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

using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>
	/// A modal RCT2 message window (confirmation, error, info, text input prompts): a CityPanel that dims everything
	/// behind it. Windows opened with Ui.OpenWindow hide the window below them, so the dimmed screen is the game or menu
	/// background.
	/// </summary>
	public class CityDialogPanelWidget : CityPanelWidget
	{
		/// <summary>Opacity (0-255) of the black veil over the screen.</summary>
		public int Dim = 110;

		public CityDialogPanelWidget() { }

		protected CityDialogPanelWidget(CityDialogPanelWidget other)
			: base(other)
		{
			Dim = other.Dim;
		}

		public override CityDialogPanelWidget Clone() { return new CityDialogPanelWidget(this); }

		public override void Draw()
		{
			var resolution = Game.Renderer.Resolution;
			WidgetUtils.FillRectWithColor(new Rectangle(0, 0, resolution.Width, resolution.Height), Color.FromArgb(Dim, 0, 0, 0));
			base.Draw();
		}
	}
}
