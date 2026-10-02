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
	// Needs: garbage, mail, sickness, death, crime pressure and fire hazard accrue per property from occupancy.
	public partial class ServiceSimulation
	{
		readonly List<PropData> sickQueue = [];
		readonly List<PropData> bodyQueue = [];

		void AccruePulse(int pulse, int t)
		{
			var interval = Math.Max(1, Info.NeedIntervalPulses);
			var bucket = pulse % interval;
			for (var i = 0; i < propList.Count; i++)
			{
				var d = propList[i];
				if (d.Prop.Id % interval != bucket)
					continue;

				var dt = t - d.LastAccrue;
				d.LastAccrue = t;
				if (dt > 0)
					Accrue(d, dt, t);
			}
		}

		void Accrue(PropData d, int dt, int t)
		{
			// The zoning work package may replace the actor of a property (level-up); follow it.
			if (d.Prop.Actor != null && d.Prop.Actor != d.ActorRef)
			{
				d.ActorRef = d.Prop.Actor;
				d.Building = d.ActorRef.TraitOrDefault<CityBuilding>() ?? d.Building;
			}

			var cb = d.Building;
			if (!cb.IsOperational)
				return;

			var tpd = TicksPerDay;
			var residents = Residents(d);
			var workers = Workers(d);

			// Garbage.
			var rate = GarbagePerDay(d, residents, workers);
			if (rate > 0)
			{
				d.GarbageMilli += (int)(rate * 1000L * dt / tpd);
				var kg = d.GarbageMilli / 1000;
				if (kg > 0)
				{
					d.GarbageMilli -= kg * 1000;
					d.GarbageKg = Math.Min(d.GarbageKg + kg, 20000);
					cGarbageGenerated += kg;
				}
			}

			// Mail.
			var mail = residents * Info.MailPerResident + workers * Info.MailPerJob;
			if (mail > 0)
			{
				d.MailMilli += (int)(mail * 1000L * dt / tpd);
				var items = d.MailMilli / 1000;
				if (items > 0)
				{
					d.MailMilli -= items * 1000;
					d.MailItems = Math.Min(d.MailItems + items, 5000);
				}
			}

			// Sickness and death: the citizen simulation reports them itself when present.
			if (citizens == null && residents > 0)
			{
				var mult = 100 + (d.GarbageKg >= Info.PileKg ? 30 : 0) + (cb.HasWater ? 0 : 50)
					+ (pollution != null ? Math.Max(pollution.GetGround(d.Prop.Origin), pollution.GetAir(d.Prop.Origin)) / 2 : 0);
				if (d.BodySince > 0 && t - d.BodySince > Info.BodyOverdueTicks)
					mult += 50;

				d.SickMicro += (int)(residents * (long)Info.SickPpmPerDay * mult / 100 * dt / tpd);
				while (d.SickMicro >= 1000000)
				{
					d.SickMicro -= 1000000;
					RaiseSick(d, 1, t);
					cAggregate++;
				}

				d.DeathMicro += (int)(residents * (long)Info.DeathPpmPerDay * dt / tpd);
				while (d.DeathMicro >= 1000000)
				{
					d.DeathMicro -= 1000000;
					AddBody(d);
					cDeaths++;
					cAggregate++;
				}
			}

			if (d.PatrolRelief > 0)
				d.PatrolRelief--;

			d.CrimePressure = ComputeCrimePressure(d);
			RaiseProblems(d);
			d.FireHazard = ComputeFireHazard(d);

			// Random events (pure hashes of property, time and salt: independent of anything else).
			if (d.FireDamage == 0 && d.FireHazard > 0 && (residents + workers > 0 || d.Prop.Kind == PropertyKind.Service))
			{
				var chance = (long)d.FireHazard * Info.FirePpmPerHazardPerDay * dt / tpd;
				if (ServiceMath.Hash(d.Prop.Id, t, 22) % 1000000 < chance)
					StartFire(d, t);
			}

			if (!ExternalCrimeSource && d.CrimeEnd == 0 && d.CrimePressure > 0 && residents + workers > 0)
			{
				var chance = (long)d.CrimePressure * Info.CrimePpmPerPressure * dt / tpd;
				if (ServiceMath.Hash(d.Prop.Id, t, 33) % 1000000 < chance)
					StartCrime(d, t);
			}
		}

		int GarbagePerDay(PropData d, int residents, int workers)
		{
			int rate;
			switch (d.Prop.Kind)
			{
				case PropertyKind.Residential: rate = residents * Info.GarbagePerResident; break;
				case PropertyKind.Commercial: rate = Math.Max(1, workers) * Info.GarbagePerJobCommercial; break;
				case PropertyKind.Industrial:
				case PropertyKind.Warehouse:
				case PropertyKind.Extractor: rate = Math.Max(1, workers) * Info.GarbagePerJobIndustrial; break;
				case PropertyKind.Office: rate = Math.Max(1, workers) * Info.GarbagePerJobOffice; break;
				default:
					rate = Math.Max(1, workers) * Info.GarbagePerJobService;
					if (d.Service != null && d.Service.Info.Kind == ServiceKind.Health)
						rate *= 2;

					break;
			}

			var level = Math.Max(1, d.Prop.Level);
			rate = rate * Math.Max(30, 100 - Info.GarbageLevelReduction * (level - 1)) / 100;
			rate = WithPolicy(rate, "GarbagePct", d.Prop.Origin);
			if (!d.Building.NotAbandoned)
				rate = rate * 3 / 2;

			return rate;
		}

		int ComputeCrimePressure(PropData d)
		{
			int p;
			switch (d.Prop.Kind)
			{
				case PropertyKind.Residential: p = d.Prop.Zone == ZoneType.ResidentialLow || d.Prop.Zone == ZoneType.ResidentialLowRent ? 8 : 14; break;
				case PropertyKind.Commercial: p = 18; break;
				case PropertyKind.Industrial:
				case PropertyKind.Warehouse: p = 10; break;
				case PropertyKind.Office: p = 12; break;
				default: p = 4; break;
			}

			p += (100 - d.Building.Happiness) / 4;
			p += (cm?.UnemploymentRate ?? 0) / 10;
			p = p * (100 - d.Sat[(int)CatchmentGroup.Police] * 85 / 100) / 100;
			p = p * (100 - d.Sat[(int)CatchmentGroup.Admin] / 4 - (cityHallCount > 0 ? 10 : 0)) / 100;
			if (d.BodySince > 0 && Now - d.BodySince > Info.BodyOverdueTicks)
				p += 5;

			p = WithPolicy(p, "CrimePct", d.Prop.Origin);
			p = Math.Max(0, p - d.PatrolRelief);

			// Street lights (road add-ons) make the lot safer.
			if (roadLayer != null && HasAccess(d))
				p = p * roadLayer.GetCrimePercent(AccessOf(d)) / 100;

			return Math.Clamp(p, 0, 100);
		}

		static int BaseFireHazard(PropData d)
		{
			if (d.Service != null && d.Service.Info.FireHazard > 0)
				return d.Service.Info.FireHazard;

			switch (d.Prop.Kind)
			{
				case PropertyKind.Residential: return d.Prop.Zone == ZoneType.ResidentialLow || d.Prop.Zone == ZoneType.ResidentialLowRent ? 6 : 10;
				case PropertyKind.Commercial: return 12;
				case PropertyKind.Industrial:
				case PropertyKind.Warehouse: return 25;
				default: return 8;
			}
		}

		static int ComputeFireHazard(PropData d)
		{
			var h = BaseFireHazard(d);
			if (!d.Building.HasPower)
				h += 5;

			if (!d.Building.NotAbandoned)
				h += 10;

			h = h * (100 - d.Sat[(int)CatchmentGroup.Fire] * 80 / 100) / 100;
			return Math.Clamp(h, 0, 100);
		}

		// ---- sickness and death ----
		void RaiseSick(PropData d, int n, int t)
		{
			cSick += n;
			if (d.SickWaiting == 0)
				d.SickSince = t;

			d.SickWaiting += n;
			if (!sickQueue.Contains(d))
				sickQueue.Add(d);
		}

		bool RequestTreatment(int homeProperty)
		{
			var d = Data(homeProperty);
			if (d == null || !d.Alive)
				return false;

			RaiseSick(d, 1, Now);
			return d.Sat[(int)CatchmentGroup.Health] > 0 || cm == null || cm.Funds > Info.ImportCostAmbulance;
		}

		void AddBody(PropData d)
		{
			if (d == null || !d.Alive)
				return;

			if (d.Bodies == 0)
				d.BodySince = Now;

			d.Bodies++;
			if (!bodyQueue.Contains(d))
				bodyQueue.Add(d);
		}

		/// <summary>Garbage piled on a property in kg (0 if unknown).</summary>
		public int GetGarbageKg(int propertyId) { return Data(propertyId)?.GarbageKg ?? 0; }

		/// <summary>0..100 crime pressure of a property.</summary>
		public int GetCrimePressure(int propertyId) { return Data(propertyId)?.CrimePressure ?? 0; }

		/// <summary>0..100 fire hazard of a property.</summary>
		public int GetFireHazard(int propertyId) { return Data(propertyId)?.FireHazard ?? 0; }

		/// <summary>0..100 value of a service info view at a cell (coverage x capacity; crime pressure and fire hazard for the Crime view).</summary>
		public int GetOverlay(CityInfoView view, CPos cell)
		{
			switch (view)
			{
				case CityInfoView.Police: return GetSatisfaction(ServiceKind.Police, cell);
				case CityInfoView.Fire: return GetSatisfaction(ServiceKind.Fire, cell);
				case CityInfoView.Health: return GetSatisfaction(ServiceKind.Health, cell);
				case CityInfoView.Education: return GetSatisfaction(ServiceKind.Education, cell);
				case CityInfoView.Parks: return GetSatisfaction(ServiceKind.Parks, cell);
				case CityInfoView.Garbage: return GetSatisfaction(ServiceKind.Garbage, cell);
				case CityInfoView.Deathcare: return GetSatisfaction(ServiceKind.Deathcare, cell);
				case CityInfoView.Telecom: return GetSatisfaction(ServiceKind.Telecom, cell);
				case CityInfoView.Post: return GetSatisfaction(ServiceKind.Post, cell);
				case CityInfoView.Crime: return registry?.GetAt(cell) is Property p ? GetCrimePressure(p.Id) : 0;
				default: return 0;
			}
		}

		public Property GetProperty(int propertyId) { return Data(propertyId)?.Prop; }

		/// <summary>Properties with a visible garbage pile (for effects).</summary>
		public void GetPiles(List<Property> into)
		{
			for (var i = 0; i < propList.Count; i++)
				if (propList[i].GarbageKg >= Info.PileKg && propList[i].Prop.Kind != PropertyKind.None)
					into.Add(propList[i].Prop);
		}

		/// <summary>1 small, 2 medium, 3 large heap, 0 none.</summary>
		public int GetPileLevel(int propertyId)
		{
			var kg = Data(propertyId)?.GarbageKg ?? 0;
			return kg < Info.PileKg ? 0 : kg < Info.SeverePileKg ? 1 : kg < Info.SeverePileKg * 2 ? 2 : 3;
		}

		public bool IsBurning(int propertyId) { return (Data(propertyId)?.FireDamage ?? 0) > 0; }

		/// <summary>Render only: how full a storing provider is (landfill garbage, cemetery bodies), 0..100, or -1.</summary>
		public int GetFillPercent(int propertyId)
		{
			var s = Data(propertyId)?.Service;
			return s == null || s.Capacity <= 0 ? -1 : Math.Clamp(s.Stored * 100 / s.Capacity, 0, 100);
		}

		/// <summary>Fire damage (0..100) and fire engines working on a burning property, for the fire effects (read only).</summary>
		public (int Damage, int EnginesOnSite) GetFireState(int propertyId)
		{
			var d = Data(propertyId);
			return d == null ? (0, 0) : (d.FireDamage, d.EnginesOnSite);
		}

		public int GetSickWaiting(int propertyId) { return Data(propertyId)?.SickWaiting ?? 0; }

		public int GetBodiesWaiting(int propertyId) { return Data(propertyId)?.Bodies ?? 0; }

		/// <summary>0..100 service level of a property for a service (provider satisfaction x distance falloff, piles etc.).</summary>
		public int GetPropertySatisfaction(ServiceKind kind, int propertyId)
		{
			var d = Data(propertyId);
			if (d == null)
				return 0;

			var best = 0;
			for (var g = 0; g < ServiceMath.GroupCount; g++)
				if (ServiceMath.KindOf((CatchmentGroup)g) == kind)
					best = Math.Max(best, d.Sat[g]);

			return best;
		}
	}
}
