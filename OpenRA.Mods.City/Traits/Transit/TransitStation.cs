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
	[Desc("Marks a placed building as a station: it provides one stop of the given mode on its access road cell.")]
	public class TransitStationInfo : TraitInfo
	{
		public readonly TransitMode Mode = TransitMode.Metro;

		public override object Create(ActorInitializer init) { return new TransitStation(this); }
	}

	public class TransitStation : INotifyAddedToWorld, INotifyRemovedFromWorld
	{
		public readonly TransitStationInfo Info;

		public TransitStation(TransitStationInfo info)
		{
			Info = info;
		}

		void INotifyAddedToWorld.AddedToWorld(Actor self)
		{
			self.World.WorldActor.TraitOrDefault<TransitLayer>()?.RegisterStation(self, Info.Mode);
		}

		void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
		{
			self.World.WorldActor.TraitOrDefault<TransitLayer>()?.UnregisterStation(self);
		}
	}
}
