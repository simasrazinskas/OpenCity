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
	// Connection rule, directed moves, connectivity, access and the IRoadNetwork implementation.
	public partial class RoadLayer
	{
		List<CPos> zoningCells = [];
		int zoningVersion = -1;

		static int Opposite(int i) { return (i + 2) & 3; }

		static int DirIndex(CVec v)
		{
			for (var i = 0; i < 4; i++)
				if (CityUtils.Neighbours4[i] == v)
					return i;

			return -1;
		}

		/// <summary>
		/// The one connection rule (symmetric): may these two cells touch as network neighbours?
		/// Both must be roads, neither may block the shared side, and highway-class cells only meet non-highway
		/// cells at a ramp. Drawing, connectivity, zoning access, utilities and traffic all use it.
		/// </summary>
		public bool Connects(CPos a, CPos b)
		{
			if (!IsRoad(a) || !IsRoad(b))
				return false;

			var v = DirIndex(b - a);
			if (v < 0)
				return false;

			if ((sideBlock[a] & (1 << v)) != 0 || (sideBlock[b] & (1 << Opposite(v))) != 0)
				return false;

			// A bridge deck only connects along its own axis.
			var ev = v == 1 || v == 3;
			if ((bridge[a] & BridgeBit) != 0 && ev != ((bridge[a] & BridgeEwBit) != 0))
				return false;

			if ((bridge[b] & BridgeBit) != 0 && ev != ((bridge[b] & BridgeEwBit) != 0))
				return false;

			var ha = types[typeId[a] - 1].IsHighway;
			var hb = types[typeId[b] - 1].IsHighway;
			if (ha != hb)
			{
				// Only a ramp (a highway cell flagged as ramp) joins a highway with a ground road.
				var highway = ha ? a : b;
				return (dir[highway] & RampBit) != 0;
			}

			return true;
		}

		/// <summary>
		/// Directed move: may a vehicle step from `from` towards CityUtils.Neighbours4[dir]? Rejects reversing out of or into
		/// a one-way cell; side entry and exit are legal.
		/// </summary>
		public bool CanEnter(CPos from, int direction)
		{
			if ((uint)direction > 3)
				return false;

			var n = from + CityUtils.Neighbours4[direction];
			if (!Connects(from, n))
				return false;

			var ow = (dir[from] & OneWayMask) - 1;
			if (ow >= 0 && direction == Opposite(ow))
				return false;

			var nw = (dir[n] & OneWayMask) - 1;
			if (nw >= 0 && direction == Opposite(nw))
				return false;

			return true;
		}

		IProgression progressionCache;
		bool progressionSearched;

		/// <summary>The progression provider (world trait, else the first playable player's), or null.</summary>
		public IProgression Progression
		{
			get
			{
				if (!progressionSearched)
				{
					progressionSearched = true;
					foreach (var p in world.WorldActor.TraitsImplementing<IProgression>())
					{
						progressionCache = p;
						break;
					}

					if (progressionCache == null)
					{
						foreach (var pl in world.Players)
						{
							if (!pl.Playable)
								continue;

							foreach (var p in pl.PlayerActor.TraitsImplementing<IProgression>())
							{
								progressionCache = p;
								break;
							}

							if (progressionCache != null)
								break;
						}
					}
				}

				return progressionCache;
			}
		}

		/// <summary>Buildable-area gate (purchased map tiles). Always true without a progression provider.</summary>
		public bool IsCellOwned(CPos cell)
		{
			return Progression == null || Progression.IsCellOwned(cell);
		}

		/// <summary>Money returned when this road cell is bulldozed (RefundPercent of its type cost).</summary>
		public int RefundFor(CPos cell)
		{
			var t = GetRoadType(cell);
			return t == null ? 0 : t.Info.Cost * (IsBridge(cell) ? Info.BridgeCostMultiplier : 1) * Info.RefundPercent / 100;
		}

		/// <summary>
		/// Road unlocks: IProgression.IsUnlocked("road:&lt;name&gt;") if a progression provider exists (world or player trait),
		/// otherwise the player's population against the type's UnlockPopulation.
		/// </summary>
		public bool IsTypeUnlocked(Player player, RoadTypeData type)
		{
			if (type == null)
				return false;

			var progression = Progression;
			if (progression == null && player != null)
				foreach (var p in player.PlayerActor.TraitsImplementing<IProgression>())
				{
					progression = p;
					break;
				}

			if (progression != null)
				return progression.IsUnlocked("road:" + type.Name);

			if (type.Info.UnlockPopulation <= 0)
				return true;

			var cm = player?.PlayerActor.TraitOrDefault<CityManager>();
			return cm != null && cm.Population >= type.Info.UnlockPopulation;
		}

		/// <summary>Number of Connects neighbours.</summary>
		public int ArmCount(CPos cell)
		{
			var n = 0;
			for (var i = 0; i < 4; i++)
				if (Connects(cell, cell + CityUtils.Neighbours4[i]))
					n++;

			return n;
		}

		/// <summary>Effective junction control (explicit or default by the types that meet); None for non-junctions.</summary>
		public JunctionControl GetControl(CPos cell)
		{
			if (!IsRoad(cell))
				return JunctionControl.None;

			var explicitValue = control[cell];
			var arms = 0;
			var best = JunctionControl.None;
			for (var i = 0; i < 4; i++)
			{
				var n = cell + CityUtils.Neighbours4[i];
				if (!Connects(cell, n))
					continue;

				arms++;
				var c = types[typeId[n] - 1].Info.DefaultControl;
				if (c > best)
					best = c;
			}

			if (arms < 3)
				return explicitValue != 0 && explicitValue - 1 == (int)JunctionControl.Roundabout ? JunctionControl.Roundabout : JunctionControl.None;

			if (explicitValue != 0)
				return (JunctionControl)(explicitValue - 1);

			if ((dir[cell] & RampBit) != 0)
				return JunctionControl.None;

			var own = types[typeId[cell] - 1].Info.DefaultControl;
			return own > best ? own : best;
		}

		/// <summary>True if this road is a junction (3 or more connected arms).</summary>
		public bool IsJunction(CPos cell) { return IsRoad(cell) && ArmCount(cell) >= 3; }

		public bool GivesAccess(CPos cell)
		{
			var t = GetRoadType(cell);
			return t != null && t.Info.GivesAccess && (bridge[cell] & BridgeBit) == 0;
		}

		public bool IsZoneable(CPos cell)
		{
			var t = GetRoadType(cell);
			return t != null && t.Info.Zoneable && (bridge[cell] & BridgeBit) == 0;
		}

		public bool CarriesCable(CPos cell)
		{
			var t = GetRoadType(cell);
			return t != null && t.Info.CarriesCable;
		}

		public bool CarriesPipes(CPos cell)
		{
			var t = GetRoadType(cell);
			return t != null && t.Info.CarriesPipes && (bridge[cell] & BridgeBit) == 0;
		}

		/// <summary>Road cells whose type allows zoning next to them (never highways), row-major order. Cached per NetworkVersion.</summary>
		public IReadOnlyList<CPos> ZoningCells
		{
			get
			{
				if (zoningVersion != NetworkVersion)
				{
					zoningVersion = NetworkVersion;
					zoningCells = [];
					foreach (var c in roadCells)
						if (types[typeId[c] - 1].Info.Zoneable && (bridge[c] & BridgeBit) == 0)
							zoningCells.Add(c);

					zoningCells.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
				}

				return zoningCells;
			}
		}

		/// <summary>
		/// True if this road cell belongs to a road network that reaches an OutsideConnection.
		/// If the world has no OutsideConnection at all (e.g. test maps), every road counts as connected.
		/// </summary>
		public bool IsConnectedToOutside(CPos roadCell)
		{
			if (!IsRoad(roadCell))
				return false;

			if (!hasOutsideConnections)
				return true;

			EnsureConnectivity();
			return connected[roadCell];
		}

		/// <summary>
		/// True if a street that gives access and is connected to the outside lies within `range` cells
		/// (Chebyshev distance). Highways and ramps never give access.
		/// </summary>
		public bool HasRoadAccessWithin(CPos cell, int range)
		{
			for (var dy = -range; dy <= range; dy++)
				for (var dx = -range; dx <= range; dx++)
				{
					var c = new CPos(cell.X + dx, cell.Y + dy);
					if (map.Contains(c) && GivesAccess(c) && IsConnectedToOutside(c))
						return true;
				}

			return false;
		}

		/// <summary>True if any cell 4-adjacent to the given cells is an access road connected to the outside.</summary>
		public bool HasRoadAccess(IEnumerable<CPos> cells)
		{
			foreach (var c in cells)
				foreach (var d in CityUtils.Neighbours4)
				{
					var n = c + d;
					if (GivesAccess(n) && IsConnectedToOutside(n))
						return true;
				}

			return false;
		}

		/// <summary>
		/// Nearest road that gives access to a lot cell: the closest cell (Chebyshev rings up to `range`, ties by row-major order)
		/// that gives access and is connected to the outside. CPos.Zero if there is none.
		/// </summary>
		public CPos GetAccessCell(CPos lot, int range = 4)
		{
			for (var r = 1; r <= range; r++)
			{
				for (var dy = -r; dy <= r; dy++)
					for (var dx = -r; dx <= r; dx++)
					{
						if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r)
							continue;

						var c = new CPos(lot.X + dx, lot.Y + dy);
						if (GivesAccess(c) && IsConnectedToOutside(c))
							return c;
					}
			}

			return CPos.Zero;
		}

		void EnsureConnectivity()
		{
			if (connectivityVersion == NetworkVersion)
				return;

			connectivityVersion = NetworkVersion;
			connected.Clear(false);
			floodQueue.Clear();

			foreach (var c in roadCells)
			{
				if ((dir[c] & LockedBit) != 0)
				{
					connected[c] = true;
					floodQueue.Enqueue(c);
				}
			}

			while (floodQueue.Count > 0)
			{
				var c = floodQueue.Dequeue();
				for (var i = 0; i < 4; i++)
				{
					var n = c + CityUtils.Neighbours4[i];
					if (map.Contains(n) && typeId[n] != 0 && !connected[n] && Connects(c, n))
					{
						connected[n] = true;
						floodQueue.Enqueue(n);
					}
				}
			}
		}

		int hashVersion = -1;
		int stateHash;

		/// <summary>Hash of every road cell (type, direction, side blocks, control) in row-major order, cached per NetworkVersion.</summary>
		[VerifySync]
		public int StateHash
		{
			get
			{
				if (hashVersion != NetworkVersion)
				{
					hashVersion = NetworkVersion;
					unchecked
					{
						var h = 17;
						foreach (var c in map.AllCells)
						{
							var t = typeId[c];
							if (t == 0)
								continue;

							h = h * 31 + c.X;
							h = h * 31 + c.Y;
							h = h * 31 + (t | (dir[c] << 8) | (sideBlock[c] << 16) | (control[c] << 20));
							h = h * 31 + (addons[c] | (bridge[c] << 8));
						}

						stateHash = h;
					}
				}

				return stateHash;
			}
		}

		string ICityAutoTestReporter.AutoTestReport()
		{
			var parts = new List<string>();
			for (var i = 0; i < types.Count; i++)
				parts.Add(types[i].Name + ":" + typeCounts[i + 1]);

			var oneWay = 0;
			var ramps = 0;
			var blocked = 0;
			foreach (var c in roadCells)
			{
				if ((dir[c] & OneWayMask) != 0)
					oneWay++;

				if ((dir[c] & RampBit) != 0)
					ramps++;

				if (sideBlock[c] != 0)
					blocked++;
			}

			var bridges = 0;
			foreach (var c in roadCells)
				if ((bridge[c] & BridgeBit) != 0)
					bridges++;

			return $"roads cells={roadCells.Count} types=[{string.Join(",", parts)}] oneway={oneWay} ramps={ramps} medians={blocked} islands={islands.Count} " +
				$"bridges={bridges} " +
				$"addons=[trees:{addonCounts[0]},barrier:{addonCounts[1]},lights:{addonCounts[2]},parking:{addonCounts[3]},bus:{addonCounts[4]},bike:{addonCounts[5]}] " +
				$"upkeep={MonthlyUpkeep} hash={StateHash}";
		}

		// ---- IRoadNetwork ----
		RoadClass IRoadNetwork.GetClass(CPos cell) { var t = GetRoadType(cell); return t == null ? RoadClass.None : t.Class; }
		int IRoadNetwork.GetLanes(CPos cell) { var t = GetRoadType(cell); return t == null ? 1 : Math.Clamp(t.Info.Lanes, 1, 4); }
		int IRoadNetwork.GetSpeedPercent(CPos cell) { var t = GetRoadType(cell); return t == null ? 100 : t.Info.SpeedPercent; }
		int IRoadNetwork.GetParkingSlots(CPos cell) { var t = GetRoadType(cell); return t == null ? 0 : t.Info.ParkingSlots + AddonParkingSlots(cell); }

		CPos IRoadNetwork.GetAccessRoad(IEnumerable<CPos> footprint)
		{
			var fallback = CPos.Zero;
			foreach (var c in footprint)
			{
				foreach (var d in CityUtils.Neighbours4)
				{
					var n = c + d;
					if (!GivesAccess(n))
						continue;

					if (IsConnectedToOutside(n))
						return n;

					if (fallback == CPos.Zero)
						fallback = n;
				}
			}

			return fallback;
		}
	}
}
