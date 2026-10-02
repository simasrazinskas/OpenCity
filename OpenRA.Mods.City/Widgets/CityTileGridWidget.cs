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
using System.Numerics;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets
{
	public enum CityTileState { Owned, ForSale, Locked }

	/// <summary>
	/// The map tile overview of the Map tiles window (tools/iso_ui_panels_transit.map_tiles): a square grid of tiles in a sunken
	/// dark well. Owned tiles show their land colour with a dark outline, tiles for sale are tinted yellow with a "$" and a yellow
	/// outline, locked tiles are dark. The selected tile has a yellow frame; clicking a tile selects it.
	/// </summary>
	public class CityTileGridWidget : Widget
	{
		/// <summary>Tiles per side.</summary>
		public int Size = 9;

		/// <summary>Logical size of a tile.</summary>
		public int Cell = 20;

		public Func<int, int, CityTileState> GetState = (_, _) => CityTileState.Locked;
		public Func<int, int, Color> GetBase = (_, _) => Color.Green;
		public Func<int2> GetSelected = () => new int2(-1, -1);
		public Action<int, int> OnSelect = (_, _) => { };

		public CityTileGridWidget() { }

		protected CityTileGridWidget(CityTileGridWidget other)
			: base(other)
		{
			Size = other.Size;
			Cell = other.Cell;
			GetState = other.GetState;
			GetBase = other.GetBase;
			GetSelected = other.GetSelected;
			OnSelect = other.OnSelect;
		}

		public override CityTileGridWidget Clone() { return new CityTileGridWidget(this); }

		static Color Mix(Color a, Color b, float t)
		{
			return Color.FromArgb(255, (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
		}

		const int Inset = 2;

		public override void Draw()
		{
			var rb = RenderBounds;
			var family = CityTheme.FamilyOf(this);
			CityTheme.DrawPanel(CityTheme.Art(family, "well-dark"), rb);

			var b = CityTheme.BevelLogical;
			var yellow = CityTheme.Ramp("yellow", 5);
			var font = Game.Renderer.Fonts["TinyBold"];
			var selected = GetSelected();
			for (var ty = 0; ty < Size; ty++)
			{
				for (var tx = 0; tx < Size; tx++)
				{
					float x = rb.X + Inset + tx * Cell, y = rb.Y + Inset + ty * Cell;
					var land = GetBase(tx, ty);
					switch (GetState(tx, ty))
					{
						case CityTileState.Owned:
							CityTheme.Fill(x, y, Cell, Cell, Mix(land, Color.Black, 0.45f));
							CityTheme.Fill(x + b, y + b, Cell - 2 * b, Cell - 2 * b, land);
							CityTheme.Fill(x + b, y + b, Cell - 2 * b, b, Mix(land, Color.White, 0.35f));
							break;
						case CityTileState.ForSale:
							var tint = Mix(land, Color.FromArgb(0xF0, 0xE0, 0xA0), 0.55f);
							CityTheme.Fill(x, y, Cell, Cell, yellow);
							CityTheme.Fill(x + b, y + b, Cell - 2 * b, Cell - 2 * b, tint);
							var size = font.Measure("$");
							font.DrawText("$", new Vector2(MathF.Round(x + (Cell - size.X) / 2f), MathF.Round(y + (Cell - size.Y - font.TopOffset) / 2f)),
								CityTheme.Ramp("yellow", 1));
							break;
						default:
							var dark = Mix(land, Color.FromArgb(0x3A, 0x3A, 0x40), 0.78f);
							CityTheme.Fill(x, y, Cell, Cell, Mix(dark, Color.Black, 0.3f));
							CityTheme.Fill(x + b, y + b, Cell - 2 * b, Cell - 2 * b, dark);
							break;
					}
				}
			}

			if (selected.X >= 0 && selected.Y >= 0 && selected.X < Size && selected.Y < Size)
			{
				float x = rb.X + Inset + selected.X * Cell, y = rb.Y + Inset + selected.Y * Cell;
				var frame = CityTheme.Ramp("yellow", 6);
				var t = 2 * b;
				CityTheme.Fill(x - t, y - t, Cell + 2 * t, t, frame);
				CityTheme.Fill(x - t, y + Cell, Cell + 2 * t, t, frame);
				CityTheme.Fill(x - t, y, t, Cell, frame);
				CityTheme.Fill(x + Cell, y, t, Cell, frame);
				CityTheme.Fill(x - t - b, y - t - b, Cell + 2 * t + 2 * b, b, Color.Black);
				CityTheme.Fill(x - t - b, y + Cell + t, Cell + 2 * t + 2 * b, b, Color.Black);
			}
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			if (mi.Button != MouseButton.Left || mi.Event != MouseInputEvent.Down)
				return false;

			var rb = RenderBounds;
			var tx = (mi.Location.X - rb.X - Inset) / Cell;
			var ty = (mi.Location.Y - rb.Y - Inset) / Cell;
			if (mi.Location.X < rb.X + Inset || mi.Location.Y < rb.Y + Inset || tx >= Size || ty >= Size)
				return false;

			OnSelect(tx, ty);
			return true;
		}
	}
}
