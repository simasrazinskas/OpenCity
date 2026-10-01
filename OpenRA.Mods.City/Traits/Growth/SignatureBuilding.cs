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
	[Desc("Marks a landmark building (design/04 3.8): free, once per city, unlocked by rules, behaves like a level 5 lot of its zone",
		"and adds a bonus. Place it with CityPlaceable; ISignatureUnlocks says whether the player may.")]
	public class SignatureBuildingInfo : TraitInfo, Requires<GrowableBuildingInfo>
	{
		[Desc("Unlock rules, all must hold. Kinds: ZoneCells:<ZoneType>:<n> (cells of finished lots), Level5Count:<ZoneType>:<n>,",
			"Happiness:<percent>, Population:<n>, Milestone:<index>, ServiceBuilt:<actor name>.")]
		public readonly string[] Unlock = [];

		[FluentReference]
		[Desc("Fluent key of the one-line description.")]
		public readonly string Description = null;

		[Desc("Well-being (happiness points) within WellBeingRadius cells.")]
		public readonly int WellBeing = 0;

		public readonly int WellBeingRadius = 10;

		[Desc("Tourist attractiveness points (PRG tourism).")]
		public readonly int Attractiveness = 0;

		[Desc("Efficiency bonus in percent for businesses of its zone (ECO).")]
		public readonly int EfficiencyPercent = 0;

		[Desc("Pollution reduction in percent for its zone within WellBeingRadius.")]
		public readonly int PollutionReductionPercent = 0;

		public override object Create(ActorInitializer init) { return new SignatureBuilding(init.Self, this); }
	}

	public class SignatureBuilding
	{
		public readonly SignatureBuildingInfo Info;
		public readonly Actor Actor;

		public SignatureBuilding(Actor self, SignatureBuildingInfo info)
		{
			Info = info;
			Actor = self;
		}
	}
}
