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
	[Desc("Actor can be removed with the bulldoze tool (growables, services, trees).")]
	public class BulldozableInfo : TraitInfo
	{
		[Desc("Percentage of CityPlaceable.Cost refunded when bulldozed.")]
		public readonly int RefundPercent = 0;

		[Desc("Flat cost charged for bulldozing (e.g. clearing trees).")]
		public readonly int Cost = 0;

		[Desc("Silently removed (paying Cost) when a road, zone growth or placed building needs the cell (trees).",
			"Actors without AutoClear block construction.")]
		public readonly bool AutoClear = false;

		public override object Create(ActorInitializer init) { return new Bulldozable(this); }
	}

	public class Bulldozable
	{
		public readonly BulldozableInfo Info;

		public Bulldozable(BulldozableInfo info)
		{
			Info = info;
		}
	}
}
