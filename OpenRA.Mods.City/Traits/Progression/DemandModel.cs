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
	[TraitLocation(SystemActors.Player)]
	[Desc("CS2-style demand: signed integer factors per zone category and per zone type (density), eased towards the target.",
		"Implements IDemandModel on the player actor. Inputs come from ICitizenPopulation + IPropertyRegistry when present, else from the legacy CityManager counters.")]
	public class DemandModelInfo : TraitInfo
	{
		[Desc("World ticks between recalculations (one aggregate pulse).")]
		public readonly int UpdateTicks = 25;

		[Desc("Maximum change of a demand value per update when rising / falling.")]
		public readonly int RiseStep = 3;

		public readonly int FallStep = 6;

		[Desc("Residential start-appeal bonus: full below StartAppealFull residents, fades to 0 at StartAppealEnd.")]
		public readonly int StartAppeal = 30;

		public readonly int StartAppealFull = 150;

		public readonly int StartAppealEnd = 600;

		[Desc("Tax percent with a neutral tax factor, and bar points per percentage point away from it.")]
		public readonly int NeutralTax = 10;

		public readonly int TaxPointsPerPercent = 3;

		[Desc("Neutral values: share of local demand served by shops, share of shop jobs filled.")]
		public readonly int ServiceNeutral = 70;

		public readonly int StaffNeutral = 75;

		[Desc("Free-property brake: empty properties tolerated before demand turns negative (plus 10% of all properties).")]
		public readonly int FreeLowDensity = 5;

		public readonly int FreeHighDensity = 10;

		[Desc("Residents per household when deriving household units from CityBuilding.MaxResidents (legacy fallback).")]
		public readonly int ResidentsPerHousehold = 3;

		[Desc("Fallback job mix by education (percent, edu 0..4) per workplace kind, like the property registry baseline.")]
		public readonly int[] CommercialJobMix = [40, 40, 20, 0, 0];
		public readonly int[] IndustrialJobMix = [45, 40, 15, 0, 0];
		public readonly int[] OfficeJobMix = [0, 20, 50, 30, 0];
		public readonly int[] ServiceJobMix = [10, 30, 40, 20, 0];

		[Desc("Without a Progression trait the office zone opens at this population (or once education coverage exists).")]
		public readonly int OfficePopulationThreshold = 800;

		public override object Create(ActorInitializer init) { return new DemandModel(init.Self, this); }
	}

	public partial class DemandModel : IDemandModel, ITick, ISync, ICityAutoTestReporter
	{
		static readonly ZoneType[] PrimaryZones = [ZoneType.ResidentialLow, ZoneType.CommercialLow, ZoneType.Industrial, ZoneType.Office];

		public readonly DemandModelInfo Info;
		readonly Actor self;
		readonly DemandResult result = new();
		readonly int[] zoneTarget = new int[DemandInputs.ZoneCount];
		readonly int[] zoneDemand = new int[DemandInputs.ZoneCount];
		readonly bool[] zoneActive = new bool[DemandInputs.ZoneCount];
		readonly int[] catTarget = new int[5];
		readonly int[] catDemand = new int[5];
		readonly ZoneType[] catZone = new ZoneType[5];
		readonly List<DemandFactor>[] catFactors = new List<DemandFactor>[5];

		bool resolved;
		bool initialized;
		CityManager manager;
		ICitizenPopulation citizens;
		IPropertyRegistry properties;
		IProgression progression;
		Progression progressionImpl;
		ZoneLayer zones;
		IDemandInputProvider[] providers = [];

		public DemandModel(Actor self, DemandModelInfo info)
		{
			this.self = self;
			Info = info;
			for (var i = 0; i < catFactors.Length; i++)
				catFactors[i] = [];

			// Like a fresh city: residential and industrial wanted from the start (matches the legacy defaults).
			catDemand[(int)ZoneCategory.Residential] = 80;
			catDemand[(int)ZoneCategory.Industrial] = 40;
		}

		/// <summary>Snapshot of the latest inputs (read-only for the UI: tooltips and debugging).</summary>
		public DemandInputs Inputs { get; } = new();

		/// <summary>Residential demand ignoring vacancy: households move in while this is positive.</summary>
		[VerifySync]
		public int ResidentialAttraction { get; private set; } = 80;

		[VerifySync]
		public int DemandResidential => catDemand[(int)ZoneCategory.Residential];

		[VerifySync]
		public int DemandCommercial => catDemand[(int)ZoneCategory.Commercial];

		[VerifySync]
		public int DemandIndustrial => catDemand[(int)ZoneCategory.Industrial];

		[VerifySync]
		public int DemandOffice => catDemand[(int)ZoneCategory.Office];

		public int GetDemand(ZoneType zone)
		{
			return (int)zone < zoneDemand.Length ? zoneDemand[(int)zone] : 0;
		}

		public int GetDemand(ZoneCategory category)
		{
			return catDemand[(int)category];
		}

		/// <summary>Un-eased target of a category (what the factors sum to, clamped).</summary>
		public int GetTarget(ZoneCategory category) { return catTarget[(int)category]; }

		/// <summary>The zone type whose density currently drives the category bar.</summary>
		public ZoneType GetDrivingZone(ZoneCategory category) { return catZone[(int)category]; }

		/// <summary>Factors of the category bar, sorted by absolute value (largest first). Sum = target.</summary>
		public IReadOnlyList<DemandFactor> GetFactors(ZoneCategory category)
		{
			return catFactors[(int)category];
		}

		/// <summary>Factors of one zone type (category factors plus this density's free-property factor), sorted.</summary>
		public IReadOnlyList<DemandFactor> GetFactors(ZoneType zone)
		{
			var cat = zone.Category();
			if (cat == ZoneCategory.None)
				return [];

			// UI-side convenience: rebuilt on demand, never used by the sim.
			var list = new List<DemandFactor>(result.BaseFactors[(int)cat]);
			var free = result.FreeValue[(int)zone];
			if (free != 0)
				list.Add(new DemandFactor { Key = DemandMath.FreeKey(cat), Value = free });

			SortFactors(list);
			return list;
		}

		public bool IsZoneActive(ZoneType zone) { return (int)zone < zoneActive.Length && zoneActive[(int)zone]; }

		/// <summary>Combined hash of all demand values for the determinism check.</summary>
		public int StateHash
		{
			get
			{
				var h = 17;
				for (var i = 0; i < zoneDemand.Length; i++)
					h = h * 31 + zoneDemand[i];

				for (var i = 0; i < catDemand.Length; i++)
					h = h * 31 + catDemand[i];

				return h;
			}
		}

		static void SortFactors(List<DemandFactor> list)
		{
			// Insertion sort: stable, allocation free, deterministic (largest absolute value first).
			for (var i = 1; i < list.Count; i++)
			{
				var x = list[i];
				var j = i - 1;
				while (j >= 0 && Math.Abs(list[j].Value) < Math.Abs(x.Value))
				{
					list[j + 1] = list[j];
					j--;
				}

				list[j + 1] = x;
			}
		}

		void Resolve()
		{
			resolved = true;
			var world = self.World;
			manager = self.TraitOrDefault<CityManager>();
			progression = self.TraitsImplementing<IProgression>().FirstOrDefault();
			progressionImpl = progression as Progression;
			citizens = world.WorldActor.TraitsImplementing<ICitizenPopulation>().FirstOrDefault()
				?? self.TraitsImplementing<ICitizenPopulation>().FirstOrDefault();
			properties = world.WorldActor.TraitsImplementing<IPropertyRegistry>().FirstOrDefault();
			zones = world.WorldActor.TraitOrDefault<ZoneLayer>();
			providers = world.WorldActor.TraitsImplementing<IDemandInputProvider>()
				.Concat(self.TraitsImplementing<IDemandInputProvider>()).ToArray();
		}

		void ITick.Tick(Actor self)
		{
			if (self.World.WorldTick % Math.Max(1, Info.UpdateTicks) != 0)
				return;

			if (!resolved)
				Resolve();

			Update();
		}

		/// <summary>Recalculates Inputs, factors and eases the bars. Called every UpdateTicks (and from tests).</summary>
		public void Update()
		{
			GatherInputs();
			DemandMath.Compute(Inputs, Info, result);
			ResidentialAttraction = result.ResidentialAttraction;
			ApplyTargets();
		}

		bool Unlocked(string key) { return progression == null || progression.IsUnlocked(key); }

		void ApplyTargets()
		{
			for (var z = 1; z < DemandInputs.ZoneCount; z++)
			{
				var zone = (ZoneType)z;
				var cat = zone.Category();
				if (cat == ZoneCategory.None)
					continue;

				var unlocked = Unlocked("zone:" + zone) && (cat != ZoneCategory.Office || OfficeOpen());
				var primary = Array.IndexOf(PrimaryZones, zone) >= 0;
				var built = Inputs.TotalUnits[z] + Inputs.TotalBuildings[z] > 0 || (zones != null && zones.CountZoned(zone) > 0);
				zoneActive[z] = unlocked && (primary || built);
				zoneTarget[z] = unlocked ? result.ZoneTarget(zone) : 0;
			}

			for (var c = 1; c < 5; c++)
			{
				var best = ZoneType.None;
				var bestTarget = int.MinValue;
				for (var z = 1; z < DemandInputs.ZoneCount; z++)
				{
					if (!zoneActive[z] || (int)((ZoneType)z).Category() != c || zoneTarget[z] <= bestTarget)
						continue;

					best = (ZoneType)z;
					bestTarget = zoneTarget[z];
				}

				catZone[c] = best;
				catTarget[c] = best == ZoneType.None ? 0 : bestTarget;
				var list = catFactors[c];
				list.Clear();
				if (best != ZoneType.None)
				{
					list.AddRange(result.BaseFactors[c]);
					var free = result.FreeValue[(int)best];
					if (free != 0)
						list.Add(new DemandFactor { Key = DemandMath.FreeKey((ZoneCategory)c), Value = free });

					SortFactors(list);
				}
			}

			// Ease: up slowly, down fast. Category bar = best (highest eased) active zone type.
			for (var z = 1; z < DemandInputs.ZoneCount; z++)
				zoneDemand[z] = Ease(zoneDemand[z], zoneTarget[z], zoneActive[z] || zoneTarget[z] != 0);

			for (var c = 1; c < 5; c++)
				catDemand[c] = Ease(catDemand[c], catTarget[c], true);

			initialized = true;
		}

		int Ease(int current, int target, bool animate)
		{
			if (!initialized || !animate)
				return target;

			if (target > current)
				return Math.Min(target, current + Info.RiseStep);

			return Math.Max(target, current - Info.FallStep);
		}

		bool OfficeOpen()
		{
			// With a Progression trait the unlock table decides; the legacy latch only applies without one.
			if (progression != null)
				return true;

			return manager == null || manager.OfficeUnlocked || manager.Population >= Info.OfficePopulationThreshold;
		}

		string ICityAutoTestReporter.AutoTestReport()
		{
			var parts = new List<string>();
			string[] names = ["", "R", "C", "I", "O"];
			for (var c = 1; c < 5; c++)
			{
				var f = string.Join(",", catFactors[c].Select(x => $"{x.Key.Replace("demand-factor-", "")}{x.Value:+0;-0}"));
				parts.Add($"{names[c]}={catDemand[c]}(t{catTarget[c]}/{catZone[c]}) [{f}]");
			}

			var zonesText = new List<string>();
			for (var z = 1; z < DemandInputs.ZoneCount; z++)
				if (zoneActive[z])
					zonesText.Add($"{(ZoneType)z}:{zoneDemand[z]}");

			return $"demand {string.Join(" ", parts)} ra={ResidentialAttraction} zones{{{string.Join(",", zonesText)}}} hash={StateHash}";
		}
	}
}
