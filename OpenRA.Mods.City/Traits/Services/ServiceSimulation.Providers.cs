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
	// Providers: efficiency (budget x staff x utilities), upgrades, facility processing, monthly finances.
	public partial class ServiceSimulation
	{
		int cityHallCount;

		void RefreshProvider(ServiceState s)
		{
			var info = s.Info;
			var cb = s.Building;
			var d = Data(s.Prop.Id);

			int fleetAdd = 0, capAdd = 0, thrAdd = 0, effAdd = 0;
			for (var i = 0; i < s.Upgrades.Length; i++)
			{
				if ((s.UpgradeMask & (1 << i)) == 0)
					continue;

				var u = s.Upgrades[i];
				fleetAdd += u.AddFleet;
				capAdd += u.AddCapacity;
				thrAdd += u.AddThroughput;
				effAdd += u.AddEfficiency;
			}

			var bud = ServiceMath.BudgetEfficiency(budget[(int)info.Kind]);
			var staff = 100;
			if (cb.Info.MaxJobs > 0 && d != null)
				staff = Math.Clamp(Workers(d) * 100 / cb.Info.MaxJobs, 40, 100);

			var util = !cb.HasPower ? 0 : info.NeedsWater && cb.Info.WaterUse > 0 && !cb.HasWater ? 50 : 100;
			var eff = bud * staff / 100 * util / 100;
			if (util > 0)
				eff += effAdd;

			s.Efficiency = Math.Clamp(eff, 0, 150);
			s.Active = cb.IsOperational && s.Efficiency > 0;

			var fleet = info.Fleet + fleetAdd;
			s.Fleet = fleet > 0 && s.Active ? Math.Max(1, (fleet * s.Efficiency + 99) / 100) : 0;
			var cap = info.Capacity + capAdd;
			var physical = info.Kind == ServiceKind.Garbage || info.Kind == ServiceKind.Deathcare;
			s.Capacity = physical ? cap : cap * s.Efficiency / 100;
			if (info.Kind == ServiceKind.Education)
				s.Capacity = WithPolicy(s.Capacity, "EduBoostPct", s.Prop.Origin);

			s.Throughput = (info.Throughput + thrAdd) * s.Efficiency / 100;
			s.Range = info.Range > 0 ? info.Range : info.Radius;

			var p = s.Prop;
			if (info.Kind == ServiceKind.Education)
			{
				p.SchoolLevel = info.SchoolLevel;
				p.StudentSeats = s.Capacity;
			}
			else if (info.Kind == ServiceKind.Health)
				p.Beds = s.Capacity;

			if (!s.JobsWritten && info.JobMix.Length == 5 && cb.Info.MaxJobs > 0)
			{
				s.JobsWritten = true;
				var mix = new int[5];
				info.JobMix.CopyTo(mix);
				PropertyRegistry.Distribute(cb.Info.MaxJobs, mix, p.JobSlots);
			}

			cityHallCount = 0;
			for (var i = 0; i < providers.Count; i++)
				if (providers[i].Active && providers[i].Info.CityWide)
					cityHallCount++;
		}

		// ---- upgrades ----
		void BuyUpgrade(Actor building, int index)
		{
			if (building == null || building.Disposed || building.Owner != self.Owner)
				return;

			var d = Data(registry.GetByActor(building)?.Id ?? 0);
			var s = d?.Service;
			if (s == null || index < 0 || index >= s.Upgrades.Length || (s.UpgradeMask & (1 << index)) != 0)
				return;

			if (cm != null && !cm.TrySpend(s.Upgrades[index].Cost, "service-upgrade"))
				return;

			s.UpgradeMask |= 1 << index;
			RefreshProvider(s);
			cUpgrades++;
			MarkAllDirty();
		}

		public bool HasUpgrade(Actor building, int index)
		{
			var s = Data(registry?.GetByActor(building)?.Id ?? 0)?.Service;
			return s != null && index >= 0 && index < 31 && (s.UpgradeMask & (1 << index)) != 0;
		}

		/// <summary>Percent of the base pollution a service building still emits after its upgrades (for the pollution layer).</summary>
		public int GetPollutionPercent(Actor building)
		{
			var s = Data(registry?.GetByActor(building)?.Id ?? 0)?.Service;
			if (s == null)
				return 100;

			var red = 0;
			for (var i = 0; i < s.Upgrades.Length; i++)
				if ((s.UpgradeMask & (1 << i)) != 0)
					red += s.Upgrades[i].PollutionReduction;

			return Math.Max(0, 100 - red);
		}

		/// <summary>Power (units) added to a building's base UtilityProducer.Power by its upgrades (for the utility network).</summary>
		public int GetExtraPower(Actor building)
		{
			var s = Data(registry?.GetByActor(building)?.Id ?? 0)?.Service;
			if (s == null)
				return 0;

			var add = 0;
			for (var i = 0; i < s.Upgrades.Length; i++)
				if ((s.UpgradeMask & (1 << i)) != 0)
					add += s.Upgrades[i].AddPower;

			return add;
		}

		public ServiceBuildingStatus GetStatus(Actor building)
		{
			var s = Data(registry?.GetByActor(building)?.Id ?? 0)?.Service;
			if (s == null)
				return default;

			var used = 0;
			switch (s.Group)
			{
				case CatchmentGroup.Health: used = s.Prop.Patients; break;
				case CatchmentGroup.Garbage:
				case CatchmentGroup.Deathcare: used = s.Stored; break;
				case CatchmentGroup.Police: used = s.Inmates; break;
				case CatchmentGroup.Edu1:
				case CatchmentGroup.Edu2:
				case CatchmentGroup.Edu3:
				case CatchmentGroup.Edu4: used = s.Enrolled; break;
			}

			return new ServiceBuildingStatus
			{
				Kind = s.Info.Kind,
				Active = s.Active,
				Efficiency = s.Efficiency,
				Satisfaction = s.Satisfaction,
				Fleet = s.Fleet,
				FleetInUse = s.FleetInUse,
				Capacity = s.Capacity,
				Used = used,
				Load = s.Load,
				UpgradeMask = s.UpgradeMask,
			};
		}

		// ---- facility processing ----
		void ProcessFacilities()
		{
			var tpd = TicksPerDay;
			for (var i = 0; i < providers.Count; i++)
			{
				var s = providers[i];
				if (!s.Active)
					continue;

				switch (s.Group)
				{
					case CatchmentGroup.Garbage:
					case CatchmentGroup.Deathcare:
						if (s.Throughput > 0 && s.Stored > 0)
						{
							s.ProcessMilli += (long)s.Throughput * 1000 * 25 / tpd;
							var take = (int)Math.Min(s.Stored, s.ProcessMilli / 1000);
							s.ProcessMilli -= take * 1000L;
							if (take >= s.Stored)
								s.ProcessMilli = Math.Min(s.ProcessMilli, 1000);

							if (take > 0)
							{
								s.Stored -= take;
								s.Processed += take;
								if (s.Group == CatchmentGroup.Garbage)
								{
									cGarbageProcessed += take;
									if (s.Info.RevenueCentsPerKg > 0 && cm != null)
									{
										s.RevCents += take * s.Info.RevenueCentsPerKg;
										if (s.RevCents >= 100)
										{
											cm.AddFunds(s.RevCents / 100, "recycling");
											s.RevCents %= 100;
										}
									}
								}
							}
						}
						else if (s.Stored == 0)
							s.ProcessMilli = 0;

						break;
				}
			}
		}

		static int FreeBeds(ServiceState s)
		{
			return s.Active ? s.Capacity - s.Prop.Patients - s.Reserved : 0;
		}

		void Admit(ServiceState s, int n)
		{
			s.Reserved = Math.Max(0, s.Reserved - n);
			for (var i = 0; i < n; i++)
			{
				s.Prop.Patients++;
				s.Discharge.Enqueue(Now + Info.StayTicks);
			}
		}

		void AdmitAndDischarge(int t)
		{
			for (var i = 0; i < providers.Count; i++)
			{
				var s = providers[i];
				if (s.Group != CatchmentGroup.Health)
					continue;

				while (s.Discharge.Count > 0 && s.Discharge.Peek() <= t)
				{
					s.Discharge.Dequeue();
					s.Prop.Patients = Math.Max(0, s.Prop.Patients - 1);
					cTreated++;
					s.Served++;
					feeCents[0] += Info.HealthFeeCents * feePercent[0] / 100;
					if (ServiceMath.Hash(s.Prop.Id, t, s.Served) % 1000 < Info.TreatedDeathPermille)
					{
						AddBody(Data(s.Prop.Id));
						cDeaths++;
					}
				}
			}
		}

		// ---- schools ----
		int TryEnroll(int schoolLevel, CPos homeRoad)
		{
			var g = CatchmentGroup.Edu1 + Math.Clamp(schoolLevel, 1, 4) - 1;
			var s = NearestWith(g, homeRoad, x => x.Capacity - x.Enrolled > 0);
			if (s == null)
				return 0;

			s.Enrolled++;
			s.Served++;
			return s.Prop.Id;
		}

		void Unenroll(int schoolProperty)
		{
			var s = Data(schoolProperty)?.Service;
			if (s != null && s.Enrolled > 0)
				s.Enrolled--;
		}

		// ---- prisons ----
		void JailToPrison()
		{
			// Inmates of jails serve JailTicks, then move to a prison with free cells (else are released).
			for (var i = 0; i < providers.Count; i++)
			{
				var s = providers[i];
				if (s.Group != CatchmentGroup.Police || s.Info.Prison || s.Inmates == 0)
					continue;

				var anonymous = s.Inmates - s.Named.Count;
				if (anonymous <= 0)
					continue;

				var move = (anonymous + 1) / 2;
				for (var k = 0; k < move; k++)
				{
					ServiceState target = null;
					for (var j = 0; j < providers.Count && target == null; j++)
						if (providers[j].Info.Prison && providers[j].Active && providers[j].Inmates < providers[j].Capacity)
							target = providers[j];

					s.Inmates--;
					if (target != null)
						target.Inmates++;
				}
			}

			for (var i = 0; i < providers.Count; i++)
			{
				var s = providers[i];
				var anon = s.Inmates - s.Named.Count;
				if (s.Info.Prison && anon > 0)
					s.Inmates = Math.Max(0, s.Inmates - (anon + 1) / 2);
			}
		}

		// ---- money ----
		void MonthlyFinance()
		{
			if (cm == null || !Info.BookUpgradeUpkeep)
				return;

			var delta = new int[ServiceMath.KindCount];
			for (var i = 0; i < providers.Count; i++)
			{
				var s = providers[i];
				if (!s.Building.IsOperational)
					continue;

				var k = (int)s.Info.Kind;
				for (var u = 0; u < s.Upgrades.Length; u++)
					if ((s.UpgradeMask & (1 << u)) != 0)
						delta[k] += s.Upgrades[u].Upkeep;
			}

			for (var k = 0; k < delta.Length; k++)
			{
				if (delta[k] > 0)
					cm.TrySpend(delta[k], "upkeep-" + ((ServiceKind)k).ToString().ToLowerInvariant());
			}
		}

		// ---- statistics ----
		int lastStatSick, lastStatDeaths, lastStatCrimes, lastStatFires;

		void RecordStatistics()
		{
			var stats = Find<ICityStatistics>();
			if (stats == null)
				return;

			stats.Record("svc-garbage-generated-t", (int)(cGarbageGenerated / 1000));
			stats.Record("svc-garbage-collected-t", (int)(cGarbageCollected / 1000));
			stats.Record("svc-sick", cSick - lastStatSick);
			stats.Record("svc-deaths", cDeaths - lastStatDeaths);
			stats.Record("svc-crimes", cCrimes - lastStatCrimes);
			stats.Record("svc-fires", cFires - lastStatFires);
			lastStatSick = cSick;
			lastStatDeaths = cDeaths;
			lastStatCrimes = cCrimes;
			lastStatFires = cFires;
		}
	}
}
