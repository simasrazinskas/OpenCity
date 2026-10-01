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
using System.Collections.Generic;

namespace OpenRA.Mods.City.Traits
{
	// Road add-ons (trees, barriers, lights, parking, bus and bike lanes) and bridges.
	public partial class RoadLayer
	{
		static int BitIndex(RoadAddons flag)
		{
			var b = (int)flag;
			var i = 0;
			while ((b >>= 1) != 0)
				i++;

			return i;
		}

		void CountAddons(byte mask, int delta)
		{
			for (var i = 0; i < 6; i++)
				if ((mask & (1 << i)) != 0)
					addonCounts[i] += delta;
		}

		/// <summary>The add-on registry (yaml order). Empty when the mod defines none.</summary>
		public IReadOnlyList<RoadAddonData> AddonTypes => addonData;

		public RoadAddonData GetAddonData(RoadAddons flag)
		{
			foreach (var a in addonData)
				if (a.Flag == flag)
					return a;

			return null;
		}

		/// <summary>Add-ons present in a road cell.</summary>
		public RoadAddons GetAddons(CPos cell)
		{
			return IsRoad(cell) ? (RoadAddons)addons[cell] : RoadAddons.None;
		}

		public bool HasBusLane(CPos cell) { return (GetAddons(cell) & RoadAddons.BusLane) != 0; }

		public bool HasBikeLane(CPos cell) { return (GetAddons(cell) & RoadAddons.BikeLane) != 0; }

		public bool HasLights(CPos cell) { return (GetAddons(cell) & RoadAddons.Lights) != 0; }

		public int CountOfAddon(RoadAddons flag) { return addonCounts[BitIndex(flag)]; }

		/// <summary>Add-ons the road type of this cell may carry (highways: barrier and lights only; bridges: no trees or parking).</summary>
		public RoadAddons AllowedAddons(CPos cell)
		{
			var t = GetRoadType(cell);
			if (t == null)
				return RoadAddons.None;

			if (t.IsHighway)
				return RoadAddons.Barrier | RoadAddons.Lights;

			var allowed = RoadAddons.Barrier | RoadAddons.Lights | RoadAddons.BusLane | RoadAddons.BikeLane | RoadAddons.Trees;
			if (t.Info.ParkingSlots > 0 || t.Info.Lanes <= 2)
				allowed |= RoadAddons.Parking;

			if (IsBridge(cell))
				allowed &= ~(RoadAddons.Trees | RoadAddons.Parking);

			return allowed;
		}

		/// <summary>Turns add-ons on or off. Disallowed or unknown add-ons are ignored. Returns true if the cell changed.</summary>
		public bool SetAddon(CPos cell, RoadAddons flag, bool on)
		{
			if (!IsRoad(cell) || GetAddonData(flag) == null)
				return false;

			var cur = (RoadAddons)addons[cell];
			var next = on ? cur | (flag & AllowedAddons(cell)) : cur & ~flag;
			if (next == cur)
				return false;

			CountAddons((byte)cur, -1);
			CountAddons((byte)next, 1);
			addons[cell] = (byte)next;
			Changed(cell);
			return true;
		}

		/// <summary>Product of the add-ons' noise multipliers in percent (100 = untouched). Trees and barriers lower it.</summary>
		public int GetNoiseMultiplier(CPos cell)
		{
			var m = 100;
			var a = GetAddons(cell);
			foreach (var d in addonData)
				if ((a & d.Flag) != 0)
					m = m * d.Info.NoisePercent / 100;

			return m;
		}

		/// <summary>Noise emitted by the road cell (type noise times the add-on multiplier), 0..100.</summary>
		public int GetNoise(CPos cell)
		{
			var t = GetRoadType(cell);
			return t == null ? 0 : t.Info.Noise * GetNoiseMultiplier(cell) / 100;
		}

		public int GetAirPollution(CPos cell)
		{
			var t = GetRoadType(cell);
			if (t == null)
				return 0;

			var m = 100;
			var a = GetAddons(cell);
			foreach (var d in addonData)
				if ((a & d.Flag) != 0)
					m = m * d.Info.AirPercent / 100;

			return t.Info.AirPollution * m / 100;
		}

		/// <summary>Crime multiplier in percent from street lights around the cell (100 = none).</summary>
		public int GetCrimePercent(CPos cell)
		{
			var m = 100;
			var a = GetAddons(cell);
			foreach (var d in addonData)
				if ((a & d.Flag) != 0)
					m = m * d.Info.CrimePercent / 100;

			return m;
		}

		/// <summary>Land value points the add-ons give to the lots next to the cell (trees, lights).</summary>
		public int GetLandValueBonus(CPos cell)
		{
			var v = 0;
			var a = GetAddons(cell);
			foreach (var d in addonData)
				if ((a & d.Flag) != 0)
					v += d.Info.LandValue;

			return v;
		}

		/// <summary>Land value bonus of the road cells within `range` of a lot cell (Chebyshev), summed and capped at `cap`.</summary>
		public int GetLandValueBonusAround(CPos lot, int range = 2, int cap = 10)
		{
			var v = 0;
			for (var dy = -range; dy <= range; dy++)
				for (var dx = -range; dx <= range; dx++)
				{
					var c = new CPos(lot.X + dx, lot.Y + dy);
					if (IsRoad(c) && addons[c] != 0)
						v += GetLandValueBonus(c);
				}

			return Math.Min(cap, v);
		}

		/// <summary>Extra parking spaces from parking bays in the cell.</summary>
		int AddonParkingSlots(CPos cell)
		{
			var a = GetAddons(cell);
			var n = 0;
			foreach (var d in addonData)
				if ((a & d.Flag) != 0)
					n += d.Info.ParkingSlots;

			return n;
		}

		// ---- bridges ----

		/// <summary>True for an elevated road cell (a bridge span over water).</summary>
		public bool IsBridge(CPos cell)
		{
			return map.Contains(cell) && typeId[cell] != 0 && (bridge[cell] & BridgeBit) != 0;
		}

		/// <summary>Deck axis of a bridge cell: true = east-west, false = north-south.</summary>
		public bool BridgeIsEastWest(CPos cell)
		{
			return IsBridge(cell) && (bridge[cell] & BridgeEwBit) != 0;
		}

		/// <summary>True if the terrain of the cell can be spanned by a bridge (water, no road yet).</summary>
		public bool IsBridgeTerrain(CPos cell)
		{
			return map.Contains(cell) && typeId[cell] == 0 && Info.BridgeTerrain.Contains(map.GetTerrainInfo(cell).Type)
				&& !ConstructionUtils.HasBlockingActor(world, cell);
		}

		/// <summary>Adds a bridge cell. axisEastWest: true = deck runs east-west. Same rules as AddRoad, but needs water.</summary>
		public bool AddBridge(CPos cell, byte type, int oneWay, bool paired, bool axisEastWest)
		{
			return AddRoadInner(cell, type, oneWay, paired, false, false, axisEastWest ? 1 : 0);
		}

		public int BridgeCellCount
		{
			get
			{
				var n = 0;
				foreach (var c in roadCells)
					if ((bridge[c] & BridgeBit) != 0)
						n++;

				return n;
			}
		}
	}
}
