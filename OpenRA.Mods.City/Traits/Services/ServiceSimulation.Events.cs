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
	// Events: fires (start, burn, spread, rubble) and crime (alarm, patrol response, arrest).
	public partial class ServiceSimulation
	{
		readonly List<int> fires = [];
		readonly List<PropData> crimeList = [];
		int lastLandfillNotice = -100000;
		int lastFireNotice = -100000;

		void NotifyOnce(ref int last, int t, string fluentKey)
		{
			if (t - last < TicksPerDay)
				return;

			last = t;
			TextNotificationsManager.AddTransientLine(self.Owner, fluentKey);
		}

		/// <summary>Burning properties (ids), for renderers and info views.</summary>
		public IReadOnlyList<int> BurningProperties => fires;

		void TickEvents(int t)
		{
			TickFires(t);
			TickCrimes(t);
		}

		// ---- fire ----
		void StartFire(PropData d, int t)
		{
			if (d.FireDamage > 0 || !d.Alive || !d.Building.IsOperational)
				return;

			d.FireDamage = Math.Max(1, Info.FireStartDamage);
			d.FireStart = t;
			d.EnginesOnSite = d.EnginesEnRoute = d.EnginesWanted = 0;
			d.UnservedSince = -1;
			fires.Add(d.Prop.Id);
			cFires++;
			NotifyOnce(ref lastFireNotice, t, "notification-service-fire");
			Chirp("chirp-service-fire", 2, d);
		}

		void TickFires(int t)
		{
			var burnEvery = Math.Max(1, Info.BurnTicksPerDamage / 5);
			for (var i = fires.Count - 1; i >= 0; i--)
			{
				var d = Data(fires[i]);
				if (d == null || !d.Alive || d.FireDamage <= 0)
				{
					fires.RemoveAt(i);
					continue;
				}

				if (t / 5 % burnEvery == 0)
					d.FireDamage++;

				d.FireDamage -= Info.ExtinguishPerEngine * d.EnginesOnSite;
				if (d.FireDamage <= 0)
				{
					d.FireDamage = 0;
					d.EnginesWanted = 0;
					cFiresOut++;
					fires.RemoveAt(i);
				}
				else if (d.FireDamage >= 100)
				{
					fires.RemoveAt(i);
					Destroy(d);
				}
				else if (t % Math.Max(1, Info.FireSpreadInterval) == 0)
					SpreadFire(d, t);
			}
		}

		readonly List<int> spreadSeen = [];

		void SpreadFire(PropData d, int t)
		{
			spreadSeen.Clear();
			var p = d.Prop;
			for (var y = p.Origin.Y - 1; y <= p.Origin.Y + p.Depth; y++)
			{
				for (var x = p.Origin.X - 1; x <= p.Origin.X + p.Width; x++)
				{
					var np = registry.GetAt(new CPos(x, y));
					if (np == null || np == p || spreadSeen.Contains(np.Id))
						continue;

					spreadSeen.Add(np.Id);
					var nd = Data(np.Id);
					if (nd == null || !nd.Alive || nd.FireDamage > 0)
						continue;

					if (ServiceMath.Hash(np.Id, t, 44) % 100 < nd.FireHazard / 2)
						StartFire(nd, t);
				}
			}
		}

		void Destroy(PropData d)
		{
			cFiresLost++;
			if (d.Prop.Actor != null && d.Prop.Actor.Info.TraitInfoOrDefault<UtilityProducerInfo>()?.Power > 0)
				Chirp("chirp-service-blackout", 2, d);

			Chirp("chirp-service-fire-lost", 2, d);
			cDeaths += Residents(d) * 5 / 100;
			d.FireDamage = 0;
			d.EnginesWanted = 0;
			var actor = d.Prop.Actor;
			var origin = d.Prop.Origin;
			var width = d.Prop.Width;
			var depth = d.Prop.Depth;
			var owner = self.Owner;
			var rubble = Info.RubbleActor;
			world.AddFrameEndTask(w =>
			{
				if (actor == null || actor.Disposed)
					return;

				actor.Dispose();

				// Disposal is deferred to the end of the frame, so the rubble follows in a task queued after it.
				if (string.IsNullOrEmpty(rubble) || !w.Map.Rules.Actors.ContainsKey(rubble))
					return;

				w.AddFrameEndTask(w2 =>
				{
					// Prefer the zoning package's footprint-sized rubble (same look as collapsed buildings).
					var sized = $"rubble-{width}x{depth}";
					if (w2.Map.Rules.Actors.ContainsKey(sized))
					{
						w2.CreateActor(sized, [new LocationInit(origin), new OwnerInit(owner)]);
						return;
					}

					for (var y = 0; y < depth; y++)
					{
						for (var x = 0; x < width; x++)
						{
							var cell = origin + new CVec(x, y);
							if (w2.Map.Contains(cell))
								w2.CreateActor(rubble, [new LocationInit(cell), new OwnerInit(owner)]);
						}
					}
				});
			});
		}

		void DispatchFire(int t)
		{
			for (var i = 0; i < fires.Count; i++)
			{
				var d = Data(fires[i]);
				if (d == null || !d.Alive || d.FireDamage <= 0 || t < d.FireStart + Info.FireAlarmDelay || !HasAccess(d))
					continue;

				var wanted = Math.Min(Info.MaxEngines, 1 + BaseFireHazard(d) / Math.Max(1, Info.EngineHazardDivisor));
				if (d.EnginesWanted == 0)
					cRequests++;

				d.EnginesWanted = wanted;
				var have = d.EnginesEnRoute + d.EnginesOnSite;
				if (have >= wanted)
					continue;

				ProvidersAt(CatchmentGroup.Fire, AccessOf(d), out var a, out _, out var b, out _, d.Prop.Origin);
				var sent = 0;
				var s = a != null && a.Active && a.Fleet - a.FleetInUse > 0 ? a : b != null && b.Active && b.Fleet - b.FleetInUse > 0 ? b : null;
				while (s != null && have + sent < wanted && sent < 2 && s.Fleet - s.FleetInUse > 0)
				{
					var v = NewVehicle(ServiceKind.Fire, s, true);
					v.Target = d.Prop.Id;
					v.MaxLoad = 0;
					AddStop(v, d.Prop.Id, 1);
					d.EnginesEnRoute++;
					sent++;
					StartLeg(v, t);
				}

				if (sent == 0 && have < wanted && TryHeliFire(d, t))
					sent++;

				if (sent == 0 && have == 0)
				{
					// Nobody can come: after a short while an engine is imported from the outside connection.
					if (d.UnservedSince < 0)
						d.UnservedSince = t;
					else if (d.UnservedSince != int.MaxValue && t - d.UnservedSince > 100)
					{
						ImportVehicle(ServiceKind.Fire, d, 1, Info.ImportCostFire, t);
						d.UnservedSince = int.MaxValue;
					}
				}
			}
		}

		// ---- crime ----
		void StartCrime(PropData d, int t)
		{
			if (d.CrimeEnd != 0)
				return;

			d.CrimeAlarm = t + Info.CrimeAlarmDelay;
			d.CrimeEnd = t + Info.CrimeDuration;
			d.CrimeCar = 0;
			crimeList.Add(d);
			cCrimes++;
		}

		void TickCrimes(int t)
		{
			for (var i = crimeList.Count - 1; i >= 0; i--)
			{
				var d = crimeList[i];
				if (!d.Alive || d.CrimeEnd == 0)
				{
					crimeList.RemoveAt(i);
					continue;
				}

				if (t >= d.CrimeEnd)
				{
					// The criminal got away: the victim loses something.
					cEscapes++;
					CrimeEscaped(d);
					Chirp("chirp-service-crime", 2, d);
					if (d.Prop.Kind == PropertyKind.Commercial)
						cm?.TrySpend(50, "crime");

					d.CrimeEnd = 0;
					d.CrimeCar = 0;
					crimeList.RemoveAt(i);
				}
			}
		}

		void DispatchPolice(int t)
		{
			for (var i = 0; i < crimeList.Count; i++)
			{
				var d = crimeList[i];
				if (!d.Alive || d.CrimeEnd == 0 || d.CrimeCar != 0 || t < d.CrimeAlarm || !HasAccess(d))
					continue;

				ProvidersAt(CatchmentGroup.Police, AccessOf(d), out var a, out _, out var b, out _, d.Prop.Origin);
				var s = a != null && a.Active && a.Fleet - a.FleetInUse > 0 ? a : b != null && b.Active && b.Fleet - b.FleetInUse > 0 ? b : null;
				if (s == null)
					continue;

				var v = NewVehicle(ServiceKind.Police, s, true);
				v.Target = d.Prop.Id;
				AddStop(v, d.Prop.Id, 1);
				d.CrimeCar = v.Slot + 1;
				cRequests++;
				StartLeg(v, t);
			}
		}

		void ResolveCrimeArrival(ServiceVehicle v, PropData d, int t)
		{
			if (d.CrimeEnd > 0 && t < d.CrimeEnd)
			{
				// In time: arrest. A full jail releases the criminal again.
				d.CrimeEnd = 0;
				Arrest(d, v.Home);
			}

			if (!v.Patrol)
				d.CrimeCar = 0;
		}
	}
}
