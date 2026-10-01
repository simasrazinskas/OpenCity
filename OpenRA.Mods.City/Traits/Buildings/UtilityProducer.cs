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
	[Desc("Produces electricity, water or sewage treatment, or transforms between the HV and LV grids. Read by the UtilityNetwork solver.")]
	public class UtilityProducerInfo : TraitInfo, Requires<CityBuildingInfo>
	{
		[Desc("Electricity produced (same units as CityBuilding.PowerUse).")]
		public readonly int Power = 0;

		[Desc("Water produced (same units as CityBuilding.WaterUse).")]
		public readonly int Water = 0;

		[Desc("Sewage the building can take in (outlet, treatment plant), in water units.")]
		public readonly int Sewage = 0;

		[Desc("Percent of the treated sewage that is returned to the water network of the same component (treatment plant).")]
		public readonly int SewageReturnPercent = 0;

		[Desc("Transformer: maximum electricity moved between the HV and the LV network through this building (0 = not a transformer).")]
		public readonly int Transformer = 0;

		[Desc("Battery: stored energy capacity (0 = not a battery). Charges from surplus, discharges into deficit.")]
		public readonly int BatteryCapacity = 0;

		[Desc("Battery: maximum charge and discharge per solve.")]
		public readonly int BatteryRate = 0;

		[Desc("Outlet: pumps next to this building within this many cells lose half of their output.")]
		public readonly int PollutedRadius = 0;

		[Desc("Outlet: percent of their output the polluted pumps lose.")]
		public readonly int PollutedLoss = 50;

		public override object Create(ActorInitializer init) { return new UtilityProducer(this); }
	}

	public class UtilityProducer
	{
		public readonly UtilityProducerInfo Info;

		/// <summary>Battery charge (solver state, ISync-like: only the solver writes it, in synced code).</summary>
		internal int Charge;

		public UtilityProducer(UtilityProducerInfo info)
		{
			Info = info;
		}
	}
}
