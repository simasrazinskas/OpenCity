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
	// Tourists: visitor groups (from PRG's ITourism) arrive at an outside connection, take a bed in a hotel or in spare commercial
	// lodging (or stay as day-trippers without one), spend money in shops and leave after a few days.
	public partial class CitizenSim
	{
		struct TouristGroup
		{
			public int Id;
			public int Size;
			public int Lodging;     // property id, 0 = no bed (day-tripper)
			public int DaysLeft;
		}

		TouristGroup[] groups = new TouristGroup[64];
		int groupCount;
		int nextGroupId = 1;
		int touristAccumMilli;
		int railSeen;
		int railPending;
		int railTourists;
		int fallbackGroupsPerDay;
		long tourismCents;
		long tourismPool;
		int touristTripsRequested;
		readonly List<Property> lodgingCands = [];
		readonly Dictionary<int, int> lodgingUsed = [];

		public int Tourists { get; private set; }

		/// <summary>Tourists with a bed, and beds available (hotels and spare commercial lodging).</summary>
		public int TouristsLodged { get; private set; }
		public int LodgingBeds { get; private set; }

		int BedsOf(Property p)
		{
			var jobs = p.TotalJobSlots;
			var hotel = p.Actor != null && p.Actor.Info.Name.Contains("hotel", StringComparison.OrdinalIgnoreCase);
			return hotel ? jobs * info.HotelBedsPerJob : jobs / Math.Max(1, info.SpareLodgingJobsDivisor);
		}

		/// <summary>Daily: without ITourism the arrival rate is derived from park coverage around homes.</summary>
		void UpdateTourists()
		{
			if (tourism != null || coverage == null || Population < 50)
			{
				fallbackGroupsPerDay = 0;
				return;
			}

			var all = registry.All;
			var sum = 0;
			var n = 0;
			for (var s = 0; s < 8 && all.Count > 0; s++)
			{
				var p = all[NextRandom(all.Count)];
				if (p.Kind != PropertyKind.Residential)
					continue;

				sum += Satisfaction(ServiceKind.Parks, CityService.Parks, p.AccessCell);
				n++;
			}

			var attraction = n > 0 ? sum / n : 0;
			fallbackGroupsPerDay = info.TouristsPerDayAtFullAttraction * attraction * (1 + Population / 200) / 200;
		}

		void TickTourists()
		{
			var pulsesPerDay = Math.Max(1, TicksPerDay / 25);
			var pulseNo = world.WorldTick / 25;

			// Arrivals: continuous through the day.
			var perDay = tourism != null ? Math.Min(tourism.VisitorGroupsPerMonth, 200) : fallbackGroupsPerDay;
			if (perDay > 0 && Population > 0)
				touristAccumMilli += perDay * 1000 / pulsesPerDay;
			else
				touristAccumMilli = 0;

			var cap = Population / 10 + 20;
			while (touristAccumMilli >= 1000)
			{
				touristAccumMilli -= 1000;
				if (Tourists >= cap)
					continue;

				SpawnGroup(false, 0);
			}

			// Visitors brought by intercity trains (PT) come on top of the road arrivals and appear at the connected stations.
			if (rail != null)
			{
				railPending += Math.Max(0, rail.VisitorsTotal - railSeen);
				railSeen = rail.VisitorsTotal;
				while (railPending > 0 && rail.ConnectedStationCells.Count > 0)
				{
					var size = Math.Min(railPending, 1 + NextRandom(3));
					railPending -= size;
					if (Tourists < cap)
						SpawnGroup(true, size);
				}

				if (rail.ConnectedStationCells.Count == 0)
					railPending = 0;
			}

			// Each group has one daily visit tick (its own pulse of the day).
			for (var i = groupCount - 1; i >= 0; i--)
			{
				if ((pulseNo + groups[i].Id) % pulsesPerDay != 0)
					continue;

				SpendTourist(i);
				groups[i].DaysLeft--;
				if (groups[i].DaysLeft <= 0)
					RemoveGroup(i);
			}

			var unhoused = 0;
			var lodged = 0;
			for (var i = 0; i < groupCount; i++)
			{
				if (groups[i].Lodging == 0)
					unhoused += groups[i].Size;
				else
					lodged += groups[i].Size;
			}

			TouristsLodged = lodged;
			Tourists = unhoused + lodged;
			tourism?.ReportLodging(unhoused, LodgingBeds > 0 ? Math.Min(100, lodged * 100 / LodgingBeds) : -1);
		}

		void SpawnGroup(bool byRail, int railSize)
		{
			if (groupCount == groups.Length)
				Array.Resize(ref groups, groups.Length * 2);

			var g = new TouristGroup
			{
				Id = nextGroupId++,
				Size = byRail ? Math.Max(1, railSize) : 1 + NextRandom(3),
				DaysLeft = Math.Max(1, info.TouristStayDays),
			};

			var n = lodgingCands.Count;
			Property best = null;
			for (var s = 0; s < 4 && n > 0; s++)
			{
				var p = lodgingCands[NextRandom(n)];
				lodgingUsed.TryGetValue(p.Id, out var used);
				if (!Alive(p) || used + g.Size > BedsOf(p))
					continue;

				// Prefer hotels.
				if (best == null || (p.Actor != null && p.Actor.Info.Name.Contains("hotel", StringComparison.OrdinalIgnoreCase)))
					best = p;
			}

			if (best != null)
			{
				g.Lodging = best.Id;
				lodgingUsed.TryGetValue(best.Id, out var u);
				lodgingUsed[best.Id] = u + g.Size;
			}

			groups[groupCount++] = g;
			Tourists += g.Size;
			if (byRail)
				railTourists += g.Size;

			RequestTouristTrip(g, best, byRail);
		}

		/// <summary>A visible trip from the nearest outside connection to the lodging or a shop (traffic load only).</summary>
		void RequestTouristTrip(in TouristGroup g, Property lodging, bool byRail)
		{
			var arrival = byRail ? rail.ConnectedStationCells : tourism?.ArrivalCells;
			if (!UseTrips || arrival == null || arrival.Count == 0)
				return;

			var dest = lodging ?? (shopCands.Count > 0 ? shopCands[NextRandom(shopCands.Count)] : null);
			if (dest == null)
				return;

			var req = new TripRequest
			{
				Purpose = TripPurpose.Tourism,
				AllowedModes = TravelModes.Car | TravelModes.Transit,
				AgeGroup = AgeGroup.Adult,
				OwnerId = -g.Id,
				OriginRoad = arrival[Hash(g.Id, Today, 130) % arrival.Count],
				DestinationRoad = dest.AccessRoad,
				DestinationProperty = dest.Id,
				DepartTick = world.WorldTick,
			};

			touristTripsRequested++;
			traffic.RequestTrip(req, this);
		}

		void SpendTourist(int i)
		{
			var spend = info.TouristSpendCents * groups[i].Size;
			if (spend <= 0)
				return;

			var charged = spend;
			if (economy != null && economy.ConsumerResources.Count > 0)
			{
				var lodging = registry.Get(groups[i].Lodging);
				var road = lodging != null ? lodging.AccessRoad : (shopCands.Count > 0 ? shopCands[0].AccessRoad : CPos.Zero);
				var res = economy.ConsumerResources[Hash(groups[i].Id, Today, 131) % economy.ConsumerResources.Count];
				var shop = economy.FindShop(res, road);
				charged = 0;
				if (shop != 0)
					economy.SellToHousehold(shop, res, 1500 * groups[i].Size, spend, out charged);
			}
			else
				tourismPool += charged;

			tourismCents += charged;
			if (charged > 0)
				tourism?.ReportTouristSpending(charged);
		}

		void RemoveGroup(int i)
		{
			if (groups[i].Lodging != 0 && lodgingUsed.TryGetValue(groups[i].Lodging, out var used))
				lodgingUsed[groups[i].Lodging] = Math.Max(0, used - groups[i].Size);

			groups[i] = groups[--groupCount];
		}

		void FlushTourism()
		{
			if (tourismPool >= 100 && cm != null)
			{
				var dollars = (int)(tourismPool / 100);
				cm.AddFunds(dollars, "tourism");
				tourismPool -= dollars * 100L;
			}
		}
	}
}
