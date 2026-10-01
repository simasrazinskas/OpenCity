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

using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.City
{
	/// <summary>
	/// A translucent cell highlight drawn in the world pass (Render / RenderAboveShroud), so it scales and scrolls with the
	/// world at every zoom level. The engine's MarkerTileRenderable draws in view pixels and is only correct as an annotation.
	/// </summary>
	public class CityTileMarkerRenderable : IRenderable, IFinalizedRenderable
	{
		readonly CPos cell;
		readonly Color color;

		public CityTileMarkerRenderable(CPos cell, Color color)
		{
			this.cell = cell;
			this.color = color;
		}

		public WPos Pos => WPos.Zero;
		public int ZOffset => 0;
		public bool IsDecoration => true;

		public IRenderable WithZOffset(int newOffset) { return this; }
		public IRenderable OffsetBy(in WVec vec) { return this; }
		public IRenderable AsDecoration() { return this; }

		public IFinalizedRenderable PrepareRender(WorldRenderer wr) { return this; }

		public void Render(WorldRenderer wr)
		{
			var map = wr.World.Map;
			if (!map.Ramp.Contains(cell))
				return;

			var r = map.Grid.Ramps[map.Ramp[cell]];
			var wpos = map.CenterOfCell(cell) - new WVec(0, 0, r.CenterHeightOffset);
			Game.Renderer.WorldRgbaColorRenderer.FillRect(
				wr.Screen3DPosition(wpos + r.Corners[0]),
				wr.Screen3DPosition(wpos + r.Corners[1]),
				wr.Screen3DPosition(wpos + r.Corners[2]),
				wr.Screen3DPosition(wpos + r.Corners[3]),
				color);
		}

		public void RenderDebugGeometry(WorldRenderer wr) { }
		public Rectangle ScreenBounds(WorldRenderer wr) { return Rectangle.Empty; }
	}
}
