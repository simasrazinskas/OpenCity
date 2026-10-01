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

namespace OpenRA.Mods.City.Traits
{
	// Wave 2 capacity API of lots beyond homes and jobs: hotel rooms (tourists), storage (warehouse companies), student
	// housing (low rent flats) and corner lots. Read-only except ReportGuests (CIT).
	public partial class PropertyRegistry
	{
		/// <summary>Hotel rooms of the property (commercial lots with lodging, level scaled), 0 if none or not operational.</summary>
		public int GetLodgingRooms(int propertyId)
		{
			return byId.TryGetValue(propertyId, out var r) && r.Growable != null && r.P.Operational ? r.Growable.LodgingRooms : 0;
		}

		public int GetGuests(int propertyId)
		{
			return byId.TryGetValue(propertyId, out var r) && r.Growable != null ? r.Growable.Guests : 0;
		}

		/// <summary>CIT reports how many tourists stay in the property tonight (clamped to the rooms).</summary>
		public void ReportGuests(int propertyId, int guests)
		{
			if (byId.TryGetValue(propertyId, out var r) && r.Growable != null)
				r.Growable.Guests = Math.Clamp(guests, 0, r.Growable.LodgingRooms);
		}

		public int GetFreeRooms(int propertyId)
		{
			return Math.Max(0, GetLodgingRooms(propertyId) - GetGuests(propertyId));
		}

		/// <summary>Ids of all operational properties with hotel rooms, ordered by id (for tourism / attractiveness). Appends to `into`.</summary>
		public void GetLodgingProperties(System.Collections.Generic.List<int> into)
		{
			for (var i = 0; i < records.Count; i++)
			{
				var r = records[i];
				if (r.Growable != null && r.P.Operational && r.Growable.LodgingRooms > 0)
					into.Add(r.P.Id);
			}
		}

		/// <summary>Total rooms and guests over all operational lodging properties.</summary>
		public void LodgingTotals(out int rooms, out int guests)
		{
			rooms = 0;
			guests = 0;
			for (var i = 0; i < records.Count; i++)
			{
				var r = records[i];
				if (r.Growable == null || !r.P.Operational)
					continue;

				rooms += r.Growable.LodgingRooms;
				guests += r.Growable.Guests;
			}
		}

		/// <summary>
		/// Nearest operational property with at least `rooms` free hotel rooms, measured between access roads (Manhattan,
		/// ties by lowest id). Returns the property id or 0.
		/// </summary>
		public int FindLodging(CPos fromRoad, int rooms)
		{
			var best = 0;
			var bestDist = int.MaxValue;
			for (var i = 0; i < records.Count; i++)
			{
				var r = records[i];
				if (r.Growable == null || !r.P.Operational || r.P.AccessRoad == CPos.Zero)
					continue;

				if (r.Growable.LodgingRooms - r.Growable.Guests < rooms)
					continue;

				var d = Math.Abs(r.P.AccessRoad.X - fromRoad.X) + Math.Abs(r.P.AccessRoad.Y - fromRoad.Y);
				if (d < bestDist)
				{
					bestDist = d;
					best = r.P.Id;
				}
			}

			return best;
		}

		/// <summary>Storage capacity (units) of a warehouse or industrial lot, 0 if none or not operational.</summary>
		public int GetStorageCapacity(int propertyId)
		{
			return byId.TryGetValue(propertyId, out var r) && r.Growable != null && r.P.Operational ? r.Growable.StorageCapacity : 0;
		}

		/// <summary>Household slots of the property that are student housing (a share of HouseholdSlots, low rent flats).</summary>
		public int GetStudentHousing(int propertyId)
		{
			return byId.TryGetValue(propertyId, out var r) && r.Growable != null ? LotMath.StudentHousing(r.Growable.Info, r.P.HouseholdSlots) : 0;
		}

		/// <summary>True for lots with frontage on two connected roads (they get the corner land value bonus).</summary>
		public bool IsCorner(int propertyId)
		{
			return byId.TryGetValue(propertyId, out var r) && r.Corner;
		}
	}
}
