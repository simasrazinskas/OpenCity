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

using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.City.Traits
{
	/// <summary>Small helpers for world-space overlays and tools under the isometric projection (render/UI only).</summary>
	public static class CityIso
	{
		/// <summary>Offset from a W x D footprint's centre to its front (bottom on screen) corner.</summary>
		public static WVec FrontCorner(int width, int depth) { return new WVec(width * 512, depth * 512, 0); }

		/// <summary>Offset from a W x D footprint's centre to its back (top on screen) corner.</summary>
		public static WVec BackCorner(int width, int depth) { return new WVec(-width * 512, -depth * 512, 0); }

		/// <summary>
		/// The city structure whose mouse bounds (footprint plus height) contain the world pixel, frontmost first; null if
		/// none. Tools use it before falling back to the cell under the cursor, so clicking a tall façade picks that building
		/// rather than the cell behind it.
		/// </summary>
		public static Actor StructureAt(World world, int2 worldPx)
		{
			Actor best = null;
			var bestKey = long.MinValue;
			foreach (var pair in world.ScreenMap.ActorsAtMouse(worldPx))
			{
				var a = pair.Actor;
				if (a.Disposed || !a.IsInWorld || !a.Info.HasTraitInfo<BuildingInfo>() || !a.Info.HasTraitInfo<CityBuildingInfo>())
					continue;

				var bi = a.Info.TraitInfo<BuildingInfo>();
				var c = a.CenterPosition;
				var key = (long)c.X + c.Y + (bi.Dimensions.X + bi.Dimensions.Y) * 512L;
				if (key > bestKey)
				{
					best = a;
					bestKey = key;
				}
			}

			return best;
		}

		/// <summary>Cell for a tool at the cursor: the hovered structure's centre cell when there is one, else the ground cell.</summary>
		public static CPos ToolCell(World world, int2 worldPx, CPos groundCell)
		{
			var a = StructureAt(world, worldPx);
			return a != null ? world.Map.CellContaining(a.CenterPosition) : groundCell;
		}
	}
}
