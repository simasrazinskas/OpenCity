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
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	public partial class ServiceSimulation : ICityServices, ICityAutoTestReporter, ITick, IResolveOrder, ITripListener, IFireStarter
	{
		public readonly ServiceSimulationInfo Info;
		readonly Actor self;
		readonly World world;

		CityClock clock;
		IPropertyRegistry registry;
		IRoadNetwork roads;
		ITrafficService traffic;
		ICitizenPopulation citizens;
		IPollutionMap pollution;
		CityCoverageLayer coverage;
		CityManager cm;
		bool ready;
		bool disabled;
		int mapWidth, mapHeight;

		PropData[] props = new PropData[512];
		readonly List<PropData> propList = [];
		readonly List<ServiceState> providers = [];
		readonly int[] budget = new int[ServiceMath.KindCount];

		// Counters for reports.
		int cRequests, cDispatched, cFires, cFiresOut, cFiresLost, cCrimes, cArrests, cEscapes, cSick, cTreated, cDeaths, cBodiesCollected;
		long cGarbageGenerated, cGarbageCollected, cGarbageProcessed;
		int cImported, cImportCost, cRealTrips, cVirtualTrips, cTripFailures, cUpgrades, cAggregate;

		public ServiceSimulation(Actor self, ServiceSimulationInfo info)
		{
			Info = info;
			this.self = self;
			world = self.World;
			for (var i = 0; i < budget.Length; i++)
				budget[i] = 100;
		}

		public int StateHash { get; private set; }

		bool Initialize()
		{
			// Only a playable player runs a city (the other player actors, e.g. Neutral, would overwrite the shared coverage maps).
			if (!self.Owner.Playable)
			{
				disabled = true;
				return true;
			}

			var wa = world.WorldActor;
			registry = wa.TraitsImplementing<IPropertyRegistry>().FirstOrDefault();
			if (registry == null)
				return false;

			clock = wa.TraitOrDefault<CityClock>();
			roads = wa.TraitsImplementing<IRoadNetwork>().FirstOrDefault();
			coverage = wa.TraitOrDefault<CityCoverageLayer>();
			cm = self.TraitOrDefault<CityManager>();
			mapWidth = world.Map.MapSize.Width;
			mapHeight = world.Map.MapSize.Height;
			ResolveProviders();
			InitCatchments();

			foreach (var p in registry.All.ToList())
				OnPropertyAdded(p);

			registry.Added += OnPropertyAdded;
			registry.Removed += OnPropertyRemoved;
			ready = true;
			return true;
		}

		T Find<T>() where T : class
		{
			return world.WorldActor.TraitsImplementing<T>().FirstOrDefault() ?? self.TraitsImplementing<T>().FirstOrDefault();
		}

		// Other work packages' providers may be registered in any order, so they are looked up again from time to time.
		void ResolveProviders()
		{
			traffic = Info.VirtualOnly ? null : Find<ITrafficService>();
			citizens = Find<ICitizenPopulation>();
			pollution = Find<IPollutionMap>();
			ResolveIntegration();
		}

		PropData Data(int propertyId)
		{
			return propertyId > 0 && propertyId < props.Length ? props[propertyId] : null;
		}

		void OnPropertyAdded(Property p)
		{
			if (p.Actor == null || p.Actor.Disposed || p.Actor.Owner != self.Owner)
				return;

			var cb = p.Actor.TraitOrDefault<CityBuilding>();
			if (cb == null)
				return;

			if (p.Id >= props.Length)
				Array.Resize(ref props, Math.Max(p.Id + 1, props.Length * 2));

			if (props[p.Id] != null)
				return;

			// The first sickness and death of identical buildings are spread over time.
			var d = new PropData
			{
				Prop = p,
				Building = cb,
				ActorRef = p.Actor,
				LastAccrue = world.WorldTick,
				SickMicro = ServiceMath.Hash(p.Id, 1, 77) % 1000000,
				DeathMicro = ServiceMath.Hash(p.Id, 2, 78) % 1000000,
			};
			props[p.Id] = d;
			propList.Add(d);

			var svc = p.Actor.TraitOrDefault<ServiceBuilding>();
			if (svc != null)
			{
				var s = new ServiceState
				{
					Prop = p,
					Need = d,
					Actor = p.Actor,
					Building = cb,
					Svc = svc,
					Info = svc.Info,
					Group = ServiceMath.GroupOf(svc.Info),
					Upgrades = p.Actor.Info.TraitInfos<ServiceUpgradeInfo>().ToArray(),
					DistrictMask = svc.Info.DistrictMask,
				};

				svc.State = s;
				d.Service = s;
				providers.Add(s);
				RefreshProvider(s);
				MarkAllDirty();
			}

			d.FireHazard = BaseFireHazard(d);
		}

		void OnPropertyRemoved(Property p)
		{
			var d = Data(p.Id);
			if (d == null || !d.Alive)
				return;

			d.Alive = false;
			propList.Remove(d);
			if (d.Service != null)
			{
				d.Service.Alive = false;
				if (d.Service.Svc != null)
					d.Service.Svc.State = null;

				providers.Remove(d.Service);
				MarkAllDirty();
			}

			if (d.FireDamage > 0)
				fires.Remove(d.Prop.Id);
		}

		void ITick.Tick(Actor self)
		{
			if (disabled)
				return;

			if (!ready && !Initialize())
				return;

			if (disabled)
				return;

			var t = world.WorldTick;
			if (t % 100 == 0)
				ResolveProviders();

			TickVehicles(t);
			if (t % 5 == 0)
			{
				TickEvents(t);
				TickWildfires(t);
			}

			if (t % 25 == 0)
				Pulse(t / 25, t);

			if (clock != null && clock.IsNewDay)
				NewDay();
		}

		void Pulse(int pulse, int t)
		{
			if (pulse % 4 == 0)
				for (var i = 0; i < providers.Count; i++)
					RefreshProvider(providers[i]);

			AccruePulse(pulse, t);
			RaiseEventProblems();
			ReleaseNamed(t);
			DecayParks();
			TickStorm(t);
			CatchmentPulse(pulse);
			ProcessFacilities();
			DispatchPulse(t);
			AdmitAndDischarge(t);
		}

		void NewDay()
		{
			RecordStatistics();
			BookFees();
			NewDayDisasters();
			MonthlyFinance();
			JailToPrison();
			StateHash = ComputeHash();
		}

		// ---- orders ----
		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			switch (order.OrderString)
			{
				case ServiceOrders.SetBudget:
				{
					var kind = order.ExtraLocation.X;
					if (kind < 0 || kind >= budget.Length)
						return;

					var pct = (int)Math.Clamp(order.ExtraData, 50u, 150u);
					budget[kind] = (pct + 2) / 5 * 5;
					for (var i = 0; i < providers.Count; i++)
						RefreshProvider(providers[i]);

					break;
				}

				case ServiceOrders.BuyUpgrade:
					BuyUpgrade(order.Target.Actor, (int)order.ExtraData);
					break;

				case ServiceOrdersWave2.SetServiceFee:
					if (order.ExtraLocation.X >= 0 && order.ExtraLocation.X < feePercent.Length)
						feePercent[order.ExtraLocation.X] = (int)Math.Clamp(order.ExtraData, 50u, 200u);

					break;

				case ServiceOrdersWave2.SetDistricts:
					SetDistricts(order.Target.Actor, (int)order.ExtraData);
					break;
			}
		}

		// ---- ICityServices ----
		int ICityServices.GetBudget(ServiceKind kind) { return GetBudget(kind); }

		public int GetBudget(ServiceKind kind) { return budget[(int)kind]; }

		public int GetSatisfaction(ServiceKind kind, CPos cell)
		{
			switch (kind)
			{
				case ServiceKind.Power:
				case ServiceKind.Water:
				case ServiceKind.Sewage:
				{
					var cb = registry?.GetAt(cell)?.Actor?.TraitOrDefault<CityBuilding>();
					return cb == null ? 100 : kind == ServiceKind.Power ? (cb.HasPower ? 100 : 0) : (cb.HasWater ? 100 : 0);
				}
			}

			return coverage != null ? coverage.GetServiceCoverage(kind, cell) : 0;
		}

		int ICityServices.TryEnroll(int citizenId, int schoolLevel, CPos homeRoad) { return TryEnroll(schoolLevel, homeRoad); }

		void ICityServices.Unenroll(int citizenId, int schoolProperty) { Unenroll(schoolProperty); }

		bool ICityServices.RequestTreatment(int citizenId, int homeProperty) { return RequestTreatment(homeProperty); }

		void ICityServices.ReportDeath(int citizenId, int property) { AddBody(Data(property)); cDeaths++; }

		// ---- helpers shared by the partial files ----
		int Population()
		{
			if (citizens != null)
				return citizens.Population;

			return cm?.Population ?? 0;
		}

		int Residents(PropData d)
		{
			return citizens != null ? d.Prop.Residents : d.Building.Residents;
		}

		int Workers(PropData d)
		{
			return citizens != null ? d.Prop.TotalJobsFilled : d.Building.Workers;
		}

		int Now => world.WorldTick;

		int TicksPerDay => clock?.TicksPerDay ?? 2400;

		static int Manhattan(CPos a, CPos b) { return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y); }

		bool InMap(CPos c) { return c.X >= 0 && c.Y >= 0 && c.X < mapWidth && c.Y < mapHeight; }

		int CellIndex(CPos c) { return c.Y * mapWidth + c.X; }

		// ---- reporter ----
		string ICityAutoTestReporter.AutoTestReport()
		{
			StateHash = ComputeHash();
			var piled = 0;
			var kg = 0L;
			var burning = fires.Count;
			var sat = new int[ServiceMath.GroupCount];
			var satN = new int[ServiceMath.GroupCount];
			var covN = new int[ServiceMath.GroupCount];
			var bodies = 0;
			var sickWaiting = 0;
			foreach (var d in propList)
			{
				kg += d.GarbageKg;
				if (d.GarbageKg >= Info.PileKg)
					piled++;

				bodies += d.Bodies;
				sickWaiting += d.SickWaiting;
				if (d.Prop.Kind != PropertyKind.Residential)
					continue;

				for (var g = 0; g < sat.Length; g++)
				{
					sat[g] += d.Sat[g];
					satN[g]++;
					if (d.Sat[g] > 0)
						covN[g]++;
				}
			}

			string Avg(CatchmentGroup g) { var i = (int)g; return satN[i] > 0 ? $"{sat[i] / satN[i]}({covN[i] * 100 / satN[i]}%)" : "0"; }
			var stored = 0L;
			var inmates = 0;
			var beds = 0;
			var patients = 0;
			var fleet = 0;
			var fleetUse = 0;
			var heliBases = 0;
			var heliActive = 0;
			foreach (var s in providers)
			{
				if (s.Group == CatchmentGroup.Garbage)
					stored += s.Stored;

				inmates += s.Inmates;
				beds += s.Group == CatchmentGroup.Health ? s.Capacity : 0;
				patients += s.Group == CatchmentGroup.Health ? s.Prop.Patients : 0;
				fleet += s.Fleet;
				if (s.Info.Helicopter)
				{
					heliBases++;
					if (s.Active)
						heliActive++;
				}
				fleetUse += s.FleetInUse;
			}

			string[] parts =
			[
				$"services providers={providers.Count} requests={cRequests} dispatched={cDispatched} imported={cImported}(${cImportCost})",
				$"fires={cFires}/out={cFiresOut}/lost={cFiresLost}/burning={burning}",
				$"crime={cCrimes}/arrests={cArrests}/escaped={cEscapes}/inmates={inmates}",
				$"sick={cSick}(agg={cAggregate})/treated={cTreated}/waiting={sickWaiting}/beds={patients}of{beds}",
				$"deaths={cDeaths}/bodies={bodies}/collected={cBodiesCollected}",
				$"garbage gen={cGarbageGenerated / 1000}t/col={cGarbageCollected / 1000}t/proc={cGarbageProcessed / 1000}t/stored={stored / 1000}t/piled={piled}/{propList.Count}",
				$"sat G={Avg(CatchmentGroup.Garbage)} H={Avg(CatchmentGroup.Health)} D={Avg(CatchmentGroup.Deathcare)} E1={Avg(CatchmentGroup.Edu1)} E2={Avg(CatchmentGroup.Edu2)}",
				$"P={Avg(CatchmentGroup.Police)} F={Avg(CatchmentGroup.Fire)} Pk={Avg(CatchmentGroup.Parks)} Po={Avg(CatchmentGroup.Post)}",
				$"patrol={cPatrols} maint={cMaint} heli={cHeli}({heliActive}/{heliBases}) wild={cWild}/out={cWildOut}/lost={cWildLost} storm={cStorm}",
				$"veh={fleetUse}/{fleet} real={cRealTrips} virt={cVirtualTrips} failed={cTripFailures} upg={cUpgrades} hash={StateHash:X8}",
			];

			return string.Join(' ', parts);
		}

		int ComputeHash()
		{
			unchecked
			{
				var h = 17;
				for (var i = 0; i < budget.Length; i++)
					h = h * 31 + budget[i];

				h = h * 31 + cRequests; h = h * 31 + cDispatched; h = h * 31 + cFires; h = h * 31 + cCrimes; h = h * 31 + cSick; h = h * 31 + cDeaths;
				h = h * 31 + (int)(cGarbageGenerated ^ (cGarbageGenerated >> 32)); h = h * 31 + (int)(cGarbageCollected ^ (cGarbageCollected >> 32));
				for (var i = 0; i < propList.Count; i++)
				{
					var d = propList[i];
					h = h * 31 + d.Prop.Id; h = h * 31 + d.GarbageKg; h = h * 31 + d.MailItems; h = h * 31 + d.SickWaiting; h = h * 31 + d.Bodies;
					h = h * 31 + d.FireDamage; h = h * 31 + d.CrimePressure;
					for (var g = 0; g < d.Sat.Length; g++)
						h = h * 31 + d.Sat[g];
				}

				for (var i = 0; i < providers.Count; i++)
				{
					var s = providers[i];
					h = h * 31 + s.Prop.Id; h = h * 31 + s.Stored; h = h * 31 + s.Enrolled; h = h * 31 + s.Inmates; h = h * 31 + s.FleetInUse;
					h = h * 31 + s.Efficiency; h = h * 31 + s.Satisfaction; h = h * 31 + s.Prop.Patients; h = h * 31 + s.UpgradeMask;
				}

				for (var i = 0; i < vehicles.Count; i++)
				{
					var v = vehicles[i];
					if (v.Active)
						h = h * 31 + (int)v.Kind * 7 + v.Load + v.StopIdx * 3 + (int)v.Phase;
				}

				return h;
			}
		}
	}
}
