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

using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[Desc("Marks a placed building as a transit depot. Vehicles of the given mode start and end their service here.")]
	public class TransitDepotInfo : TraitInfo
	{
		public readonly TransitMode Mode = TransitMode.Bus;

		[Desc("Vehicles this depot can hold (a shared pool for all lines of the mode that start here).")]
		public readonly int Capacity = 12;

		public override object Create(ActorInitializer init) { return new TransitDepot(this); }
	}

	public class TransitDepot : INotifyAddedToWorld, INotifyRemovedFromWorld
	{
		public readonly TransitDepotInfo Info;

		public TransitDepot(TransitDepotInfo info)
		{
			Info = info;
		}

		void INotifyAddedToWorld.AddedToWorld(Actor self)
		{
			self.World.WorldActor.TraitOrDefault<TransitLayer>()?.RegisterDepot(self, Info.Mode, Info.Capacity);
		}

		void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
		{
			self.World.WorldActor.TraitOrDefault<TransitLayer>()?.UnregisterDepot(self);
		}
	}
}
