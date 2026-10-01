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
	// Fills DemandInputs: agent path (ICitizenPopulation + IPropertyRegistry) or legacy path (CityManager counters + CityBuilding actors).
	public partial class DemandModel
	{
		// Workplace slots by category (index = ZoneCategory, 0 = services), filled by either path.
		readonly int[] slots = new int[5];
		readonly int[] filled = new int[5];

		void GatherInputs()
		{
			Inputs.Clear();
			Array.Clear(slots);
			Array.Clear(filled);

			if (citizens != null && properties != null)
				GatherAgents();
			else
				GatherLegacy();

			DeriveFlows();
			GatherCommon();

			for (var i = 0; i < providers.Length; i++)
				providers[i].FillDemandInputs(Inputs);
		}

		void GatherAgents()
		{
			var c = citizens;
			Inputs.Population = c.Population;
			Inputs.Households = c.Households;
			Inputs.Homeless = c.Homeless;
			Inputs.Unemployed = c.Unemployed;
			Inputs.AverageHappiness = c.AverageHappiness;
			for (var e = 0; e < DemandInputs.EduCount; e++)
			{
				Inputs.WorkersByEdu[e] = c.WorkersByEducation((EducationLevel)e);
				Inputs.UnemployedByEdu[e] = c.UnemployedByEducation((EducationLevel)e);
			}

			var all = properties.All;
			for (var n = 0; n < all.Count; n++)
			{
				var p = all[n];
				if (!p.Operational)
					continue;

				var z = (int)p.Zone;
				Inputs.FreeStudentSeats += Math.Max(0, p.StudentSeats - p.Students);
				if (p.Kind == PropertyKind.Residential)
				{
					if (z > 0 && z < DemandInputs.ZoneCount)
					{
						Inputs.TotalUnits[z] += p.HouseholdSlots;
						Inputs.FreeUnits[z] += Math.Max(0, p.HouseholdSlots - p.Households);
					}

					continue;
				}

				var cat = p.Zone == ZoneType.None ? 0 : (int)p.Zone.Category();
				for (var e = 0; e < DemandInputs.EduCount; e++)
				{
					Inputs.FreeJobsByEdu[e] += Math.Max(0, p.JobSlots[e] - p.JobsFilled[e]);
					slots[cat] += p.JobSlots[e];
					filled[cat] += p.JobsFilled[e];
				}

				if (z > 0 && z < DemandInputs.ZoneCount)
				{
					Inputs.TotalBuildings[z]++;
					if (p.CompanyId == 0 && p.TotalJobsFilled == 0)
						Inputs.FreeBuildings[z]++;
				}
			}
		}

		void GatherLegacy()
		{
			if (manager == null)
				return;

			var cm = manager;
			Inputs.Population = cm.Population;
			Inputs.Households = cm.Households;
			Inputs.Unemployed = cm.Unemployed;
			Inputs.AverageHappiness = cm.AverageHappiness;

			// Education is only known as a coverage ratio: split jobseekers between "uneducated" and "educated".
			var edu = Math.Clamp(cm.EducatedRatio, 0, 100);
			Inputs.UnemployedByEdu[0] = cm.Unemployed * (100 - edu) / 100;
			Inputs.UnemployedByEdu[2] = cm.Unemployed - Inputs.UnemployedByEdu[0];
			var employed = cm.Employed;
			Inputs.WorkersByEdu[0] = employed * (100 - edu) / 100;
			Inputs.WorkersByEdu[2] = employed - Inputs.WorkersByEdu[0];

			var rph = Math.Max(1, Info.ResidentsPerHousehold);
			foreach (var a in self.World.ActorsHavingTrait<CityBuilding>())
			{
				var b = a.Trait<CityBuilding>();
				if (b.Manager != cm || !b.IsOperational)
					continue;

				var info = b.Info;
				var z = (int)info.Zone;
				var cat = b.Category;
				if (cat == ZoneCategory.Residential)
				{
					if (z > 0 && z < DemandInputs.ZoneCount)
					{
						Inputs.TotalUnits[z] += info.MaxResidents / rph;
						Inputs.FreeUnits[z] += Math.Max(0, info.MaxResidents - b.Residents) / rph;
					}

					continue;
				}

				if (info.MaxJobs <= 0)
					continue;

				var free = Math.Max(0, info.MaxJobs - b.Workers);
				AddFreeJobs(cat, free);
				slots[(int)cat] += info.MaxJobs;
				filled[(int)cat] += Math.Min(info.MaxJobs, b.Workers);
				if (z > 0 && z < DemandInputs.ZoneCount)
				{
					Inputs.TotalBuildings[z]++;
					if (b.Workers == 0)
						Inputs.FreeBuildings[z]++;
				}
			}
		}

		// Spreads free job slots over education levels using the default mix of the workplace kind.
		void AddFreeJobs(ZoneCategory cat, int free)
		{
			var mix = cat switch
			{
				ZoneCategory.Commercial => Info.CommercialJobMix,
				ZoneCategory.Industrial => Info.IndustrialJobMix,
				ZoneCategory.Office => Info.OfficeJobMix,
				_ => Info.ServiceJobMix
			};

			var assigned = 0;
			var biggest = 0;
			for (var e = 0; e < DemandInputs.EduCount && e < mix.Length; e++)
			{
				var n = free * mix[e] / 100;
				Inputs.FreeJobsByEdu[e] += n;
				assigned += n;
				if (mix[e] > mix[biggest])
					biggest = e;
			}

			Inputs.FreeJobsByEdu[biggest] += free - assigned;
		}

		// Retail / goods / office flows from workplace counts: the legacy model's "wanted jobs" vs existing jobs.
		void DeriveFlows()
		{
			var workforce = Inputs.Population * 55 / 100;
			if (manager != null && citizens == null)
				workforce = manager.Workers;
			else if (citizens != null)
				workforce = citizens.Workers + citizens.Unemployed;

			var jc = slots[(int)ZoneCategory.Commercial];
			var ji = slots[(int)ZoneCategory.Industrial];
			var jo = slots[(int)ZoneCategory.Office];
			var edu = manager?.EducatedRatio ?? 0;

			Inputs.ShopNeed = Inputs.Population / 4;
			Inputs.ShopCapacity = jc;
			if (jc > 0)
				Inputs.ShopStaffFillPct = filled[(int)ZoneCategory.Commercial] * 100 / jc;

			Inputs.GoodsNeed = 8 + workforce * 25 / 100 + jc * 10 / 100;
			Inputs.GoodsProduced = ji;
			Inputs.OfficeNeed = workforce * (8 + edu * 20 / 100) / 100;
			Inputs.OfficeProduced = jo;
		}

		void GatherCommon()
		{
			var cm = manager;
			if (cm != null)
			{
				for (var c = 1; c < 5; c++)
					Inputs.TaxRates[c] = cm.GetTaxRate((ZoneCategory)c);

				Inputs.PowerShortagePct = Shortage(cm.PowerConsumed, cm.PowerProduced);
				Inputs.WaterShortagePct = Shortage(cm.WaterConsumed, cm.WaterProduced);
			}

			if (progressionImpl != null)
			{
				Inputs.TaxSensitivityPct = progressionImpl.GetCityPolicy("TaxDemandPct");
				Inputs.Attractiveness = progressionImpl.Attractiveness;
				Inputs.TouristsUnhoused = progressionImpl.TouristsUnhoused;
				Inputs.LodgingOccupancyPct = progressionImpl.LodgingOccupancyPercent;
			}
		}

		static int Shortage(int consumed, int produced)
		{
			return consumed > produced && consumed > 0 ? Math.Min(100, (consumed - produced) * 100 / consumed) : 0;
		}
	}
}
