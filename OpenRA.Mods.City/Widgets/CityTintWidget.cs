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
	/// A translucent colour wash over what is drawn below it (locked cards dim their icon with it). Takes no mouse input, so it
	/// never gets in the way of the widgets around it.
	/// </summary>
	public class CityTintWidget : Widget
	{
		public Color Color = Color.Transparent;
		public Func<Color> GetColor;

		public CityTintWidget()
		{
			GetColor = () => Color;
		}

		protected CityTintWidget(CityTintWidget other)
			: base(other)
		{
			Color = other.Color;
			GetColor = other.GetColor;
		}

		public override CityTintWidget Clone() { return new CityTintWidget(this); }

		public override void Draw()
		{
			var rb = RenderBounds;
			CityTheme.Fill(rb.X, rb.Y, rb.Width, rb.Height, GetColor());
		}
	}
}
