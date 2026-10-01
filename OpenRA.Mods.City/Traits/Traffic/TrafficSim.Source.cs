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
using System.Linq;

namespace OpenRA.Mods.City.Traits
{
	// Aggregate trip source: stands in for the citizen simulation until it requests trips itself. Mimics the legacy
	// mix (home to work, highway commuters, freight, residents leaving) scaled by the city population.
	public sealed partial class TrafficSim
	{
		static readonly int[] HourFactor =
		[
			30, 30, 30, 30, 30, 40, 70, 140, 140, 100, 90, 90,
			100, 90, 90, 90, 120, 135, 120, 85, 75, 65, 50, 40
		];

		readonly List<int> homeCells = [];
		readonly List<int> jobCells = [];
		readonly List<int> industryCells = [];
		int lastEndpointRefresh = -100000;
		bool aggregateActive;
		bool citizensSearched;
		bool citizensExist;
		bool transitSearched;
		bool transitExists;

		void TickSource()
		{
			aggregateActive = SourceEnabled();
			if (!aggregateActive)
				return;

			if (tick - lastEndpointRefresh >= 50 || graphVersion != endpointVersion)
				RefreshEndpoints();

			if (roadListCount < 2)
				return;

			var target = TargetVehicles();
			var deficit = target - ActiveVehicles - future.Count - ready.Count;
			if (deficit <= 0)
				return;

			var n = Math.Min(Info.MaxSpawnPerTick, (deficit + 3) / 4);
			for (var i = 0; i < n; i++)
				CreateAggregateTrip();
		}

		bool SourceEnabled()
		{
			if (!citizensSearched)
			{
				citizensSearched = true;
				citizensExist = world.WorldActor.TraitsImplementing<ICitizenPopulation>().Any();
				if (!citizensExist)
				{
					foreach (var p in world.Players)
						if (p.Playable && p.PlayerActor.TraitsImplementing<ICitizenPopulation>().Any())
							citizensExist = true;
				}
			}

			// Switched off as soon as a citizen simulation requests trips (checked with a hysteresis of 500 ticks).
			return !(citizensExist && tick - lastExternalCitizenTick < 500);
		}

		// With a public transport simulation, buses are real transit trips: the aggregate source stops making random buses.
		bool TransitExists()
		{
			if (!transitSearched)
			{
				transitSearched = true;
				transitExists = world.WorldActor.TraitsImplementing<ITransitPlanner>().Any();
			}

			return transitExists;
		}

		int TargetVehicles()
		{
			var cm = FindCity();
			var target = cm == null ? Info.FallbackVehicles : Math.Min(Info.MaxVehicles, cm.Population / Math.Max(1, Info.PopulationPerVehicle));
			if (clock != null && cm != null)
				target = target * HourFactor[clock.Hour % 24] / 100;

			return target;
		}

		int endpointVersion = -1;

		void RefreshEndpoints()
		{
			lastEndpointRefresh = tick;
			endpointVersion = graphVersion;
			FindCity();
			homeCells.Clear();
			jobCells.Clear();
			industryCells.Clear();

			var buildings = new List<(uint Id, int Cell, bool Home, bool Industrial)>();
			foreach (var tp in world.ActorsWithTrait<CityBuilding>())
			{
				var a = tp.Actor;
				var b = tp.Trait;
				if (!a.IsInWorld || a.IsDead || !b.IsOperational)
					continue;

				var category = b.Info.Zone.Category();
				var isHome = category == ZoneCategory.Residential && (b.Residents > 0 || (cityManager == null && b.Info.MaxResidents > 0));
				var isJob = category != ZoneCategory.Residential && b.Info.MaxJobs > 0;
				if (!isHome && !isJob)
					continue;

				var road = net.GetAccessRoad(b.Cells);
				if (road == CPos.Zero || !InMap(road) || roadFlag[Cell(road)] == 0)
					continue;

				buildings.Add((a.ActorID, Cell(road), isHome, category == ZoneCategory.Industrial));
			}

			buildings.Sort((x, y) => x.Id.CompareTo(y.Id));
			foreach (var (_, cell, home, industrial) in buildings)
			{
				if (home)
					homeCells.Add(cell);
				else
				{
					jobCells.Add(cell);
					if (industrial)
						industryCells.Add(cell);
				}
			}
		}

		// 0 = home, 1 = job, 2 = gate (highway), 3 = industry, 4 = any road
		int PickCell(int kind, int fallback)
		{
			var c = PickKind(kind);
			if (c < 0)
				c = PickKind(fallback);

			return c < 0 ? PickKind(4) : c;
		}

		int PickKind(int kind)
		{
			List<int> list;
			switch (kind)
			{
				case 0: list = homeCells; break;
				case 1: list = jobCells; break;
				case 2: list = gates; break;
				case 3: list = industryCells.Count > 0 ? industryCells : jobCells; break;
				default:
					return roadListCount == 0 ? -1 : roadList[NextRandom(roadListCount)];
			}

			return list.Count == 0 ? -1 : list[NextRandom(list.Count)];
		}

		void CreateAggregateTrip()
		{
			int originKind, originFallback, destKind, destFallback;
			var purpose = TripPurpose.Work;
			var freight = false;
			var roll = NextRandom(100);
			if (roll < 55)
			{
				originKind = 0; originFallback = 2; destKind = 1; destFallback = 2;
			}
			else if (roll < 75)
			{
				originKind = 2; originFallback = 4; destKind = 1; destFallback = 0;
				purpose = TripPurpose.Commute;
			}
			else if (roll < 90)
			{
				originKind = 3; originFallback = 4; destKind = 2; destFallback = 4;
				purpose = TripPurpose.Freight;
				freight = true;
			}
			else
			{
				originKind = 0; originFallback = 4; destKind = 2; destFallback = 4;
				purpose = TripPurpose.Leisure;
			}

			var origin = PickCell(originKind, originFallback);
			var destination = PickCell(destKind, destFallback);
			if (origin < 0 || destination < 0 || origin == destination)
				return;

			string type = null;
			if (freight && NextRandom(100) < Info.TruckChance)
				type = "truck";
			else if (!TransitExists() && NextRandom(100) < Info.BusChance)
				type = "bus";

			var t = NewTrip(origin, destination, 0, purpose, tick, type);
			t.Internal = true;
			t.Parks = t.Kind == KindCar && IsCitizenPurpose(purpose);
			Enqueue(t);
		}
	}
}
