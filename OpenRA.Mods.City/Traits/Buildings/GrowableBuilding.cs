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
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	// State of a lot building. Level (1..5), condition and abandonment are plain state on the actor; a level change
	// never replaces the actor, so the Property id, tenants and selection stay valid. See GrowableBuilding.Condition.cs for
	// the rent/condition loop.
	public partial class GrowableBuilding : ICityBuildingState, IPollutionEmitter, ITick, ISync
	{
		public readonly GrowableBuildingInfo Info;

		readonly Actor self;
		readonly CityBuilding city;
		readonly CityManager manager;
		readonly int ticksPerDay;

		[VerifySync]
		int constructionLeft;

		[VerifySync]
		public int DaysWithoutService { get; private set; }

		[VerifySync]
		public int DaysUnhappy { get; private set; }

		[VerifySync]
		int abandonedDays;

		[VerifySync]
		public bool Abandoned { get; private set; }

		[VerifySync]
		public int Level { get; private set; }

		bool replacing;

		public GrowableBuilding(ActorInitializer init, GrowableBuildingInfo info)
		{
			Info = info;
			self = init.Self;
			city = self.Trait<CityBuilding>();

			var dims = self.Info.TraitInfoOrDefault<BuildingInfo>()?.Dimensions ?? new CVec(1, 1);
			Width = Math.Max(1, dims.X);
			Depth = Math.Max(1, dims.Y);
			Cells = Width * Depth;

			Level = Math.Clamp(init.GetValue<GrowableLevelInit, int>(info.Level), 1, Math.Max(1, info.MaxLevel));
			Theme = init.GetValue<GrowableThemeInit, int>(0);
			Condition = 0;
			balanceMilli = LotMath.Upkeep(info, Cells, Level) * 1000;

			// Players that never play (the Neutral player of a shellmap) have no real economy:
			// their growables are static decoration that is always operational.
			var owner = self.Owner;
			if (owner != null && owner.Playable)
				manager = owner.PlayerActor.TraitOrDefault<CityManager>();

			if (manager != null)
			{
				ticksPerDay = Math.Max(1, manager.Info.TicksPerDay);
				var baseTicks = Info.ConstructionTicks + Info.ConstructionTicksPerCell * (Cells - 1);
				var variance = baseTicks * Info.ConstructionVariance / 100;
				constructionLeft = Math.Max(1, baseTicks + (variance > 0 ? init.World.SharedRandom.Next(-variance, variance + 1) : 0));
				ConstructionTotal = constructionLeft;
			}

			ApplyCapacity(false);
		}

		public ZoneType Zone => Info.Zone;

		/// <summary>Footprint size in Cells.</summary>
		public int Width { get; }
		public int Depth { get; }

		/// <summary>Number of footprint Cells.</summary>
		public int Cells { get; }

		/// <summary>True for city-managed buildings; false for static decoration (e.g. Neutral-owned shellmap buildings).</summary>
		public bool IsManaged => manager != null;

		public bool UnderConstruction => constructionLeft > 0;

		/// <summary>True while the construction phase is the result of a level-up.</summary>
		public bool IsUpgrading { get; private set; }

		// Upgrading buildings keep their residents and jobs while the new level is built.
		public bool IsOperational => (!UnderConstruction || IsUpgrading) && !Abandoned;

		public int ConstructionTotal { get; private set; }

		/// <summary>0..100 while under construction, 100 afterwards.</summary>
		public int ConstructionProgress => constructionLeft <= 0 || ConstructionTotal <= 0 ? 100 : 100 - constructionLeft * 100 / ConstructionTotal;

		/// <summary>0..100 progress of the abandonment timers (the worse of the services and happiness timers).</summary>
		public int AbandonProgress
		{
			get
			{
				if (Abandoned)
					return 100;

				var services = DaysWithoutService * 100 / Math.Max(1, Info.AbandonDaysWithoutService);
				var happiness = DaysUnhappy * 100 / Math.Max(1, Info.AbandonDaysUnhappy);
				return Math.Min(100, Math.Max(services, happiness));
			}
		}

		/// <summary>0..100 progress of an abandoned building towards collapse.</summary>
		public int CollapseProgress => Abandoned ? Math.Min(100, abandonedDays * 100 / Math.Max(1, Info.CollapseDays)) : 0;

		/// <summary>Raised after level or capacity changed (PropertyRegistry republishes the property).</summary>
		internal event Action<GrowableBuilding> CapacityChanged;

		/// <summary>Stamps the capacity of the current level into the CityBuilding (legacy readers see current values).</summary>
		void ApplyCapacity(bool notify)
		{
			var stats = city.Info;
			stats.Level = Level;
			stats.Zone = Info.Zone;
			stats.MaxResidents = LotMath.HouseholdSlots(Info, Cells, Level) * Math.Max(1, Info.ResidentsPerHousehold);
			stats.MaxJobs = LotMath.Jobs(Info, Cells, Level);
			stats.PowerUse = LotMath.Utility(Info.PowerPerCellMilli, Info.UtilityDiscountPercent, Cells, Level);
			stats.WaterUse = LotMath.Utility(Info.WaterPerCellMilli, Info.UtilityDiscountPercent, Cells, Level);
			stats.Upkeep = LotMath.Upkeep(Info, Cells, Level);
			stats.Pollution = city.TraitInfo.Pollution * Math.Max(30, 100 - 12 * (Level - 1)) / 100;

			if (notify)
				CapacityChanged?.Invoke(this);
		}

		/// <summary>Industrial lots pollute while operational; cleaner at higher levels (CityBuilding.Pollution scaled by level).</summary>
		PollutionEmission IPollutionEmitter.Emission
		{
			get
			{
				var p = city.Info.Pollution;
				if (p <= 0 || !IsOperational)
					return default;

				return new PollutionEmission { Ground = p, Air = p * 3 / 4, Noise = p / 2, Radius = 0 };
			}
		}

		/// <summary>Visual theme: 0 any, 1 North American, 2 European (chosen at spawn, never changes).</summary>
		public int Theme { get; }

		/// <summary>Hotel rooms of this building at its current level.</summary>
		public int LodgingRooms => LotMath.Lodging(Info, Cells, Level);

		/// <summary>Storage capacity (units) of this building at its current level.</summary>
		public int StorageCapacity => LotMath.Storage(Info, Cells, Level);

		/// <summary>Hotel guests currently staying (written by CIT through PropertyRegistry.ReportGuests).</summary>
		[VerifySync]
		public int Guests { get; internal set; }

		/// <summary>Default capacity numbers of the current level for the registry.</summary>
		internal int HouseholdSlots => LotMath.HouseholdSlots(Info, Cells, Level);

		internal void FillJobSlots(int[] slots) { LotMath.JobSlots(Info, Cells, Level, slots); }

		void ITick.Tick(Actor self)
		{
			if (manager == null || replacing)
				return;

			if (constructionLeft > 0)
			{
				constructionLeft--;
				if (constructionLeft == 0)
					IsUpgrading = false;

				return;
			}

			var stagger = self.World.WorldTick + (int)self.ActorID;

			// Slow checks happen once per legacy day (CityManager pulse), staggered per actor.
			if (stagger % ticksPerDay == 0)
				DayTick();

			if (!Abandoned && stagger % Math.Max(1, Info.ConditionInterval) == 0)
				ConditionTick();
		}

		bool LacksServices()
		{
			// CityManager keeps HasRoadAccess current (zone-depth rule for growables).
			return !city.HasPower || !city.HasWater || !city.HasRoadAccess;
		}

		void DayTick()
		{
			var noService = LacksServices();
			var unhappy = city.Happiness < Info.AbandonHappiness;

			if (Abandoned)
			{
				if (Info.RecoverHappiness >= 0 && !noService && city.Happiness >= Info.RecoverHappiness)
				{
					Abandoned = false;
					abandonedDays = 0;
					DaysWithoutService = 0;
					DaysUnhappy = 0;
					Condition = 0;
					ResetBalance();
					CapacityChanged?.Invoke(this);
					return;
				}

				abandonedDays++;
				if (abandonedDays >= Info.CollapseDays)
					Collapse();

				return;
			}

			DaysWithoutService = noService ? DaysWithoutService + 1 : 0;
			DaysUnhappy = unhappy ? DaysUnhappy + 1 : 0;

			if (DaysWithoutService >= Info.AbandonDaysWithoutService || DaysUnhappy >= Info.AbandonDaysUnhappy)
				Abandon();
		}

		void Abandon()
		{
			Abandoned = true;
			abandonedDays = 0;
			Condition = 0;
			CapacityChanged?.Invoke(this);
		}

		/// <summary>Condemn order: an abandoned building collapses right away. Synced callers only.</summary>
		public bool Condemn()
		{
			if (!Abandoned || replacing)
				return false;

			Collapse();
			return true;
		}

		void Collapse()
		{
			if (replacing)
				return;

			replacing = true;
			var cell = self.Location;
			var owner = self.Owner;
			var rubble = "rubble-" + Width + "x" + Depth;

			// The zone stays; rubble blocks the lot for a while, then ZoneGrowth builds something new here.
			self.World.AddFrameEndTask(w =>
			{
				if (self.Disposed)
					return;

				self.Dispose();
				if (w.Map.Rules.Actors.ContainsKey(rubble))
					w.CreateActor(rubble, [new LocationInit(cell), new OwnerInit(owner)]);
			});
		}
	}
}
