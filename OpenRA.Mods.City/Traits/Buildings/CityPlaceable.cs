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
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[Desc("Actor can be placed by the player from the build toolbar (services, utilities, parks).")]
	public class CityPlaceableInfo : TraitInfo
	{
		[Desc("Construction cost.")]
		public readonly int Cost = 1000;

		[Desc("Toolbar category: power, water, police, fire, health, education, parks, special.")]
		public readonly string Category = "special";

		[Desc("Sort order inside the category.")]
		public readonly int DisplayOrder = 0;

		[Desc("Fluent key of a one-line description shown in the tooltip.")]
		public readonly string Description = null;

		[Desc("Footprint must be 4-adjacent to a road.")]
		public readonly bool RequiresRoad = true;

		[Desc("If non-empty, a cell of one of these terrain types must be within NearbyRange cells of the footprint (e.g. Water for pumps).")]
		public readonly HashSet<string> RequiresTerrainNearby = [];

		public readonly int NearbyRange = 2;

		[Desc("Population required before this can be built (0 = always unlocked). CityManager.IsUnlocked uses this.")]
		public readonly int UnlockPopulation = 0;

		public override object Create(ActorInitializer init) { return new CityPlaceable(this); }
	}

	public class CityPlaceable
	{
		public readonly CityPlaceableInfo Info;

		public CityPlaceable(CityPlaceableInfo info)
		{
			Info = info;
		}
	}
}
