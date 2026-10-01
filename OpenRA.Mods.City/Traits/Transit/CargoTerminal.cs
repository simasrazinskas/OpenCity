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

using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	public enum CargoMode : byte { Rail = 0, Harbor, Air }

	// Implements IND's ICargoTerminal (Traits/Industry/IndustryTypes.cs): Logistics treats the building as a warehouse with an outside trade link.
	[Desc("Marks a building as a cargo terminal. See ICargoTerminal.")]
	public class CargoTerminalInfo : TraitInfo
	{
		public readonly CargoMode Mode = CargoMode.Rail;

		[Desc("Units of each resource the terminal stores.")]
		public readonly int StoragePerResource = 600;

		[Desc("Units per clock day the outside link trades when connected (design: rail 400, harbor 900, air 80).")]
		public readonly int TradeUnitsPerDay = 400;

		[Desc("Outside freight cost in percent of the truck haul cost.")]
		public readonly int FreightCostPercent = 35;

		public override object Create(ActorInitializer init) { return new CargoTerminal(init.Self, this); }
	}

	public class CargoTerminal : ICargoTerminal, INotifyAddedToWorld, INotifyRemovedFromWorld
	{
		public readonly CargoTerminalInfo Info;

		public CargoTerminal(Actor self, CargoTerminalInfo info)
		{
			Actor = self;
			Info = info;
		}

		public Actor Actor { get; }

		/// <summary>The kind of terminal (rail yard, harbor, airport).</summary>
		public CargoMode CargoKind => Info.Mode;

		/// <summary>The freight mode of the outside link, as IND's Logistics sees it.</summary>
		public FreightMode Mode => Info.Mode switch { CargoMode.Rail => FreightMode.Rail, CargoMode.Harbor => FreightMode.Ship, _ => FreightMode.Air };

		public int PropertyId => Actor.World.WorldActor.TraitsImplementing<IPropertyRegistry>().FirstOrDefault()?.GetByActor(Actor)?.Id ?? 0;

		public CPos AccessRoad => Actor.World.WorldActor.TraitsImplementing<IPropertyRegistry>().FirstOrDefault()?.GetByActor(Actor)?.AccessRoad ?? CPos.Zero;

		public int StorageUnitsPerResource => Info.StoragePerResource;

		public int TradeUnitsPerDay => OutsideConnected ? Info.TradeUnitsPerDay : 0;

		public int FreightCostPercent => Info.FreightCostPercent;

		public bool OutsideConnected
		{
			get
			{
				if (Info.Mode != CargoMode.Rail)
					return true;

				var layer = Actor.World.WorldActor.TraitOrDefault<TransitLayer>();
				return layer != null && layer.IsRailConnected(Actor);
			}
		}

		void INotifyAddedToWorld.AddedToWorld(Actor self)
		{
			Actor.World.WorldActor.TraitOrDefault<TransitLayer>()?.RegisterCargo(this);
		}

		void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
		{
			Actor.World.WorldActor.TraitOrDefault<TransitLayer>()?.UnregisterCargo(this);
		}
	}
}
