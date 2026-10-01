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

using System.Collections.Generic;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[Desc("Simulation data for any city building (growables and services): capacity, utilities, status.")]
	public class CityBuildingInfo : TraitInfo
	{
		[Desc("Zone of growables (None for services).")]
		public readonly ZoneType Zone = ZoneType.None;

		public readonly int Level = 1;

		[Desc("Max residents (residential).")]
		public readonly int MaxResidents = 0;

		[Desc("Max jobs (commercial/industrial/office/services).")]
		public readonly int MaxJobs = 0;

		[Desc("Electricity consumed (MW-ish units).")]
		public readonly int PowerUse = 1;

		[Desc("Water consumed (units).")]
		public readonly int WaterUse = 1;

		[Desc("Monthly upkeep cost (services).")]
		public readonly int Upkeep = 0;

		[Desc("Ground pollution emitted into the surrounding cells (0..100 at the source, linear falloff).")]
		public readonly int Pollution = 0;

		public override object Create(ActorInitializer init) { return new CityBuilding(init.Self, this); }
	}

	/// <summary>
	/// Per-building copy of the capacity numbers of <see cref="CityBuildingInfo"/>. Growables change their numbers at run time
	/// (levels are state, not actor swaps), so everything that reads `building.Info.MaxResidents` etc. gets the current values.
	/// </summary>
	public sealed class CityBuildingStats
	{
		public ZoneType Zone { get; internal set; }
		public int Level { get; internal set; }
		public int MaxResidents { get; internal set; }
		public int MaxJobs { get; internal set; }
		public int PowerUse { get; internal set; }
		public int WaterUse { get; internal set; }
		public int Upkeep { get; internal set; }
		public int Pollution { get; internal set; }

		internal CityBuildingStats(CityBuildingInfo info)
		{
			Zone = info.Zone;
			Level = info.Level;
			MaxResidents = info.MaxResidents;
			MaxJobs = info.MaxJobs;
			PowerUse = info.PowerUse;
			WaterUse = info.WaterUse;
			Upkeep = info.Upkeep;
			Pollution = info.Pollution;
		}
	}

	public class CityBuilding : INotifyCreated, INotifyAddedToWorld, INotifyRemovedFromWorld, INotifyOwnerChanged, ISync
	{
		/// <summary>Current (possibly level-dependent) numbers of this building. Use TraitInfo for the yaml definition.</summary>
		public readonly CityBuildingStats Info;
		public readonly CityBuildingInfo TraitInfo;
		readonly Actor self;
		ICityBuildingState[] states = [];
		int landValue = 30;

		// Cached sibling traits, used by the simulation.
		internal GrowableBuilding Growable;
		internal UtilityProducer Producer;
		internal ServiceBuilding Service;

		// Set by the simulation each day.
		internal bool RoadAccessDirty = true;
		internal bool OperationalToday;
		internal uint SortId;

		public CityBuilding(Actor self, CityBuildingInfo info)
		{
			this.self = self;
			TraitInfo = info;
			Info = new CityBuildingStats(info);
		}

		/// <summary>Footprint cells (valid once the actor is in the world).</summary>
		public CPos[] Cells { get; private set; } = [];

		public ZoneCategory Category => Info.Zone.Category();

		/// <summary>Zone type of a growable (None for services).</summary>
		public ZoneType Zone => Info.Zone;

		/// <summary>Current level (1..5) of growables, 1 otherwise.</summary>
		public int Level => Info.Level;

		/// <summary>Current residents (residential buildings). Carried over by D when a building levels up.</summary>
		[VerifySync]
		public int Residents { get; set; }

		/// <summary>Residential: working-age residents (about 55%). Workplaces: filled jobs.</summary>
		[VerifySync]
		public int Workers { get; set; }

		[VerifySync]
		public bool HasPower { get; set; } = true;

		[VerifySync]
		public bool HasWater { get; set; } = true;

		[VerifySync]
		public bool HasRoadAccess { get; set; } = true;

		/// <summary>0..100.</summary>
		[VerifySync]
		public int Happiness { get; set; } = 50;

		/// <summary>0..100.</summary>
		[VerifySync]
		public int LandValue
		{
			get => landValue;
			set
			{
				// Once the zoning land value layer drives this building, legacy writers (CityManager) are ignored.
				if (!LandValueFromZoning)
					landValue = value;
			}
		}

		/// <summary>True once LandValueLayer owns the value of this building.</summary>
		public bool LandValueFromZoning { get; private set; }

		internal void SetZoningLandValue(int value)
		{
			LandValueFromZoning = true;
			landValue = value;
		}

		/// <summary>False while under construction / abandoned (any ICityBuildingState says so).</summary>
		public bool IsOperational
		{
			get
			{
				for (var i = 0; i < states.Length; i++)
					if (!states[i].IsOperational)
						return false;

				return true;
			}
		}

		/// <summary>False once abandoned (under-construction buildings still count as part of the city's pipeline).</summary>
		internal bool NotAbandoned => Growable == null || !Growable.Abandoned;

		void INotifyCreated.Created(Actor self)
		{
			states = self.TraitsImplementing<ICityBuildingState>().ToArray();
			Growable = self.TraitOrDefault<GrowableBuilding>();
			Producer = self.TraitOrDefault<UtilityProducer>();
			Service = self.TraitOrDefault<ServiceBuilding>();
		}

		void INotifyAddedToWorld.AddedToWorld(Actor self)
		{
			var occupied = self.OccupiesSpace?.OccupiedCells();
			if (occupied != null && occupied.Length > 0)
				Cells = occupied.Select(c => c.Cell).OrderBy(c => c.Y).ThenBy(c => c.X).ToArray();
			else
				Cells = [self.Location];

			Register(self.Owner);
		}

		void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
		{
			Manager?.Unregister(this);
			Manager = null;
		}

		void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
		{
			if (!self.IsInWorld)
				return;

			Manager?.Unregister(this);
			Manager = null;
			Register(newOwner);
		}

		void Register(Player owner)
		{
			// Every player actor has a CityManager, but only playable players run a city.
			// Actors of other players (e.g. the shellmap town owned by Neutral) are static decoration.
			Manager = owner.Playable ? owner.PlayerActor.TraitOrDefault<CityManager>() : null;
			RoadAccessDirty = true;
			Manager?.Register(this, self.ActorID);
		}

		/// <summary>The player's CityManager, or null for decoration owned by a player without one.</summary>
		public CityManager Manager { get; private set; }

		/// <summary>Cells as an enumerable, for APIs that need IEnumerable.</summary>
		public IEnumerable<CPos> FootprintCells => Cells;
	}
}
