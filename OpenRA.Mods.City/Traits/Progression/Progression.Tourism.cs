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
	/// <summary>
	/// Tourism basics for the citizen sim (tourists are citizens with a flag) and the demand model (hotel factor).
	/// Implemented by the Progression player trait.
	/// </summary>
	public interface ITourism
	{
		/// <summary>0..100 city attractiveness (diminishing returns over the attractions' points).</summary>
		int Attractiveness { get; }

		/// <summary>Tourist groups that should arrive per month: attractiveness^2 / divisor + a bonus per owned outside connection.</summary>
		int VisitorGroupsPerMonth { get; }

		/// <summary>Road cells where tourists enter the city (owned highway entries), in actor id order.</summary>
		IReadOnlyList<CPos> ArrivalCells { get; }

		/// <summary>The citizen sim reports lodging each pulse or day: tourists without a bed and bed occupancy 0..100 (-1 = no hotels).</summary>
		void ReportLodging(int touristsUnhoused, int occupancyPercent);

		/// <summary>The citizen sim reports money tourists spent (cents): shops, hotels, transit. Feeds the tourism statistics and the economy sink.</summary>
		void ReportTouristSpending(int cents);
	}

	/// <summary>Optional: implemented by the economy (any world or player trait) to learn about tourist spending.</summary>
	public interface ITouristSpendingSink
	{
		void OnTouristSpending(int cents);
	}

	public partial class Progression : ITourism
	{
		readonly List<CPos> arrivalCells = [];
		IPollutionMap pollution;

		[VerifySync]
		public int Attractiveness { get; private set; }

		[VerifySync]
		public int VisitorGroupsPerMonth { get; private set; }

		/// <summary>Raw attractiveness points before the diminishing-returns curve.</summary>
		public int AttractionPoints { get; private set; }

		ITouristSpendingSink[] spendingSinks;

		/// <summary>Cumulative tourist visits (sum of tourists present each month).</summary>
		public int TouristVisits { get; private set; }

		/// <summary>Cents tourists spent this month / last month.</summary>
		public int TouristSpendingThisMonth { get; private set; }

		public int TouristSpendingLastMonth { get; private set; }

		public void ReportTouristSpending(int cents)
		{
			if (cents <= 0)
				return;

			TouristSpendingThisMonth += cents;
			if (spendingSinks == null)
			{
				var list = new List<ITouristSpendingSink>();
				list.AddRange(world.WorldActor.TraitsImplementing<ITouristSpendingSink>());
				list.AddRange(self.TraitsImplementing<ITouristSpendingSink>());

				spendingSinks = list.ToArray();
			}

			foreach (var sink in spendingSinks)
				sink.OnTouristSpending(cents);
		}

		int TourismHash()
		{
			return unchecked(TouristVisits * 31 + TouristSpendingThisMonth * 17 + TouristSpendingLastMonth + TouristsUnhoused * 7 + LodgingOccupancyPercent);
		}

		PropertyRegistry lodging;
		int lodgingReportTick = -1000;

		/// <summary>Hotel rooms and guests over all operational lodging properties (zoning registry).</summary>
		public int LodgingRooms { get; private set; }

		public int LodgingGuests { get; private set; }

		public int TouristsUnhoused { get; private set; }

		public int LodgingOccupancyPercent { get; private set; } = -1;

		public IReadOnlyList<CPos> ArrivalCells => arrivalCells;

		public void ReportLodging(int touristsUnhoused, int occupancyPercent)
		{
			lodgingReportTick = world.WorldTick;
			TouristsUnhoused = Math.Max(0, touristsUnhoused);
			LodgingOccupancyPercent = occupancyPercent;
		}

		void UpdateTourism()
		{
			var raw = 0;
			foreach (var a in world.ActorsHavingTrait<ProgressionValue>())
			{
				if (a.Owner != self.Owner || !a.IsInWorld)
					continue;

				var value = a.Trait<ProgressionValue>().Info.Attractiveness;
				if (value <= 0)
					continue;

				var cb = a.TraitOrDefault<CityBuilding>();
				if (cb != null && !cb.IsOperational)
					continue;

				if (pollution != null)
				{
					var cell = a.Location;
					var pen = Math.Max(pollution.GetAir(cell), pollution.GetGround(cell));
					value = value * (100 - pen / 2) / 100;
				}

				raw += value;
			}

			if (signatures != null)
				raw += signatures.Attractiveness;

			// Hotels: rooms add attractiveness, and the registry's room/guest totals give occupancy and unhoused tourists
			// unless the citizen sim reported lodging itself during the last few pulses.
			if (lodging != null)
			{
				lodging.LodgingTotals(out var rooms, out var guests);
				LodgingRooms = rooms;
				LodgingGuests = guests;
				raw += rooms * Info.HotelPointsPer10Rooms / 10;
				if (world.WorldTick - lodgingReportTick > Info.UpdateTicks * 8)
				{
					LodgingOccupancyPercent = rooms > 0 ? Math.Min(100, guests * 100 / rooms) : -1;
					TouristsUnhoused = citizens != null && rooms > 0 ? Math.Max(0, citizens.Tourists - rooms) : 0;
				}
			}

			AttractionPoints = raw;
			var x = (int)Math.Min(int.MaxValue / 2, (long)raw * (100 + GetCityPolicy("AttractivenessPct")) / 100);
			Attractiveness = x <= 0 ? 0 : (int)(100L * x / (x + Math.Max(1, Info.AttractionHalf)));

			arrivalCells.Clear();
			foreach (var a in world.ActorsHavingTrait<OutsideConnection>())
				if (IsCellOwned(a.Location))
					arrivalCells.Add(a.Location);

			var groups = Attractiveness * Attractiveness / Math.Max(1, Info.VisitorDivisor) + arrivalCells.Count * Info.ConnectionVisitors;

			// Tourists without a bed turn others away: each unhoused tourist costs 5% of the arrivals (at most half).
			VisitorGroupsPerMonth = groups * (100 - Math.Min(50, TouristsUnhoused * Info.UnhousedPenaltyPercent)) / 100;
		}
	}
}
