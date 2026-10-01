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

using System.Numerics;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Render-only overlay: darkens map tiles the player does not own yet and draws a border along the edge of the owned area.")]
	public class TileBorderOverlayInfo : TraitInfo
	{
		[Desc("Colour (with alpha) drawn over locked tiles.")]
		public readonly Color LockedColor = Color.FromArgb(120, 10, 12, 20);

		[Desc("Colour of the line between owned and locked tiles.")]
		public readonly Color BorderColor = Color.FromArgb(230, 232, 196, 96);

		[Desc("Border line width in screen pixels.")]
		public readonly float BorderWidth = 3;

		public override object Create(ActorInitializer init) { return new TileBorderOverlay(init.Self, this); }
	}

	public class TileBorderOverlay : IRenderOverlay
	{
		readonly TileBorderOverlayInfo info;
		readonly World world;
		Progression progression;

		public TileBorderOverlay(Actor self, TileBorderOverlayInfo info)
		{
			this.info = info;
			world = self.World;
		}

		Progression Find()
		{
			if (progression != null)
				return progression;

			var local = world.LocalPlayer;
			if (local != null)
				progression = local.PlayerActor.TraitOrDefault<Progression>();

			if (progression == null)
			{
				foreach (var p in world.Players)
				{
					if (!p.Playable)
						continue;

					progression = p.PlayerActor.TraitOrDefault<Progression>();
					if (progression != null)
						break;
				}
			}

			return progression;
		}

		// Screen position of the top-left corner of a cell.
		Vector3 Corner(WorldRenderer wr, int x, int y)
		{
			var p = world.Map.CenterOfCell(new CPos(x, y)) - new WVec(512, 512, 0);
			return wr.Screen3DPosition(p);
		}

		void IRenderOverlay.Render(WorldRenderer wr)
		{
			var pr = Find();
			if (pr == null || pr.OwnedTileCount == 0)
				return;

			var renderer = Game.Renderer.WorldRgbaColorRenderer;
			var g = pr.TileGrid;
			for (var ty = 0; ty < g; ty++)
			{
				for (var tx = 0; tx < g; tx++)
				{
					var r = pr.GetTileRect(tx, ty);
					var tl = Corner(wr, r.Left, r.Top);
					var br = Corner(wr, r.Right, r.Bottom);
					var owned = pr.IsTileOwned(tx, ty);
					if (!owned)
						renderer.FillRect(tl, br, info.LockedColor);

					// Border on edges where ownership changes (each shared edge is drawn once, by the owned side).
					if (!owned)
						continue;

					var tr = Corner(wr, r.Right, r.Top);
					var bl = Corner(wr, r.Left, r.Bottom);
					if (tx == 0 || !pr.IsTileOwned(tx - 1, ty))
						renderer.DrawLine(tl, bl, info.BorderWidth, info.BorderColor);

					if (tx == g - 1 || !pr.IsTileOwned(tx + 1, ty))
						renderer.DrawLine(tr, br, info.BorderWidth, info.BorderColor);

					if (ty == 0 || !pr.IsTileOwned(tx, ty - 1))
						renderer.DrawLine(tl, tr, info.BorderWidth, info.BorderColor);

					if (ty == g - 1 || !pr.IsTileOwned(tx, ty + 1))
						renderer.DrawLine(bl, br, info.BorderWidth, info.BorderColor);
				}
			}
		}
	}
}
