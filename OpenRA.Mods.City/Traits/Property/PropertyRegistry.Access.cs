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

using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.City.Traits
{
	/// <summary>Finds providers of cross-WP interfaces on the world actor or any player actor (first match, deterministic order).</summary>
	public static class ZoningLookup
	{
		public static T Find<T>(World w)
			where T : class
		{
			var t = w.WorldActor.TraitsImplementing<T>().FirstOrDefault();
			if (t != null)
				return t;

			// Only playable players run a city (every player actor carries the city traits, Neutral included).
			foreach (var p in w.Players)
			{
				if (!p.Playable)
					continue;

				t = p.PlayerActor.TraitsImplementing<T>().FirstOrDefault();
				if (t != null)
					return t;
			}

			return null;
		}
	}

	/// <summary>Land value per cell (0..100), published by LandValueLayer. Request: lead moves it into CityInterfaces.cs.</summary>
	public interface ILandValueSource
	{
		/// <summary>0..100 land value of a cell (zone-neutral).</summary>
		int GetLandValue(CPos cell);

		/// <summary>Land value as a lot of this zone reads it (industry capped, commercial bonus, ...).</summary>
		int GetLandValue(CPos cell, ZoneType zone);

		/// <summary>Incremented after each recomputation.</summary>
		int Version { get; }
	}

	public partial class PropertyRegistry
	{
		readonly List<int> accessScratch = [];

		/// <summary>
		/// Lots use the middle road cell of their longest connected frontage (design 3.2); other buildings
		/// ask the road network for the nearest road that gives access.
		/// </summary>
		void RefreshAccess(Record r)
		{
			var p = r.P;
			roads ??= world.WorldActor.TraitsImplementing<IRoadNetwork>().FirstOrDefault();
			p.AccessCell = p.Origin;
			if (roads == null)
				return;

			if (r.Growable != null)
				r.Corner = CountFrontageSides(p) >= 2;

			if (r.Growable != null && TryFrontageAccess(p))
				return;

			var cells = new List<CPos>(p.Width * p.Depth);
			for (var y = 0; y < p.Depth; y++)
				for (var x = 0; x < p.Width; x++)
					cells.Add(p.Origin + new CVec(x, y));

			p.AccessRoad = roads.GetAccessRoad(cells);
			if (p.AccessRoad == CPos.Zero)
				return;

			foreach (var c in cells)
				foreach (var d in CityUtils.Neighbours4)
					if (c + d == p.AccessRoad)
						p.AccessCell = c;
		}

		// Number of footprint sides that touch a connected frontage road.
		int CountFrontageSides(Property p)
		{
			var sides = 0;
			for (var side = 0; side < 4; side++)
			{
				var len = side % 2 == 0 ? p.Width : p.Depth;
				for (var i = 0; i < len; i++)
				{
					var road = SideRoad(p, side, i);
					if (IsFrontage(road) && roads.IsConnectedToOutside(road))
					{
						sides++;
						break;
					}
				}
			}

			return sides;
		}

		bool IsFrontage(CPos road)
		{
			zoneLayer ??= world.WorldActor.TraitOrDefault<ZoneLayer>();
			return zoneLayer != null ? zoneLayer.IsFrontageRoad(road) : roads.IsRoad(road);
		}

		bool TryFrontageAccess(Property p)
		{
			var bestSide = -1;
			var bestCount = 0;
			var bestConnected = false;
			for (var side = 0; side < 4; side++)
			{
				var count = 0;
				var connected = false;
				var len = side % 2 == 0 ? p.Width : p.Depth;
				for (var i = 0; i < len; i++)
				{
					var road = SideRoad(p, side, i);
					if (!IsFrontage(road))
						continue;

					count++;
					connected |= roads.IsConnectedToOutside(road);
				}

				// Prefer a side that reaches the outside, then the longest frontage (first side on ties: N, E, S, W).
				if (count > 0 && ((connected && !bestConnected) || (connected == bestConnected && count > bestCount)))
				{
					bestSide = side;
					bestCount = count;
					bestConnected = connected;
				}
			}

			if (bestSide < 0)
			{
				p.AccessRoad = CPos.Zero;
				return true;
			}

			accessScratch.Clear();
			var length = bestSide % 2 == 0 ? p.Width : p.Depth;
			for (var i = 0; i < length; i++)
				if (IsFrontage(SideRoad(p, bestSide, i)) && (!bestConnected || roads.IsConnectedToOutside(SideRoad(p, bestSide, i))))
					accessScratch.Add(i);

			// Middle of the frontage, rounded down.
			var index = accessScratch[(accessScratch.Count - 1) / 2];
			p.AccessRoad = SideRoad(p, bestSide, index);
			p.AccessCell = SideCell(p, bestSide, index);
			return true;
		}

		// Side 0..3 = N, E, S, W (CityUtils.Neighbours4). Index runs along the side, west-east or north-south.
		static CPos SideRoad(Property p, int side, int i)
		{
			switch (side)
			{
				case 0: return new CPos(p.Origin.X + i, p.Origin.Y - 1);
				case 1: return new CPos(p.Origin.X + p.Width, p.Origin.Y + i);
				case 2: return new CPos(p.Origin.X + i, p.Origin.Y + p.Depth);
				default: return new CPos(p.Origin.X - 1, p.Origin.Y + i);
			}
		}

		static CPos SideCell(Property p, int side, int i)
		{
			switch (side)
			{
				case 0: return new CPos(p.Origin.X + i, p.Origin.Y);
				case 1: return new CPos(p.Origin.X + p.Width - 1, p.Origin.Y + i);
				case 2: return new CPos(p.Origin.X + i, p.Origin.Y + p.Depth - 1);
				default: return new CPos(p.Origin.X, p.Origin.Y + i);
			}
		}
	}
}
