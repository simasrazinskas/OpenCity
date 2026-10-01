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
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	public enum PollutionScale : byte
	{
		[Desc("Emits at full strength while operational.")]
		Fixed,

		[Desc("Industrial buildings emit between 30% (empty) and 100% (all jobs filled).")]
		Workers,

		[Desc("Fixed for everything except industrial buildings, which use Workers.")]
		Auto
	}

	[Desc("Makes a city building a pollution emitter. Reads CityBuildingInfo.Pollution unless overridden here.")]
	public class PollutionSourceInfo : TraitInfo, Requires<CityBuildingInfo>
	{
		[Desc("Ground pollution at the source, 0..100. -1 = derive from CityBuildingInfo.Pollution.")]
		public readonly int Ground = -1;

		[Desc("Air pollution at the source, 0..100. -1 = derive.")]
		public readonly int Air = -1;

		[Desc("Noise at the source, 0..100. -1 = derive.")]
		public readonly int Noise = -1;

		[Desc("Water (sewage outlets etc.) pollution at the source, 0..100.")]
		public readonly int Water = 0;

		[Desc("Spread radius in cells (0 = layer defaults).")]
		public readonly int Radius = 0;

		public readonly PollutionScale Scale = PollutionScale.Auto;

		public override object Create(ActorInitializer init) { return new PollutionSource(init.Self, this); }
	}

	public class PollutionSource : IPollutionEmitter, INotifyCreated
	{
		readonly PollutionSourceInfo info;
		CityBuilding building;

		public PollutionSource(Actor self, PollutionSourceInfo info)
		{
			this.info = info;
		}

		void INotifyCreated.Created(Actor self)
		{
			building = self.TraitOrDefault<CityBuilding>();
		}

		PollutionEmission IPollutionEmitter.Emission => Emission;

		public PollutionEmission Emission
		{
			get
			{
				if (building == null || building.Cells.Length == 0 || !building.IsOperational)
					return default;

				var p = building.Info.Pollution;
				var power = building.Producer != null;
				var ground = info.Ground >= 0 ? info.Ground : power ? p * 70 / 100 : p;
				var air = info.Air >= 0 ? info.Air : power ? p : p * 60 / 100;
				var noise = info.Noise >= 0 ? info.Noise : power ? p * 30 / 100 : p * 40 / 100;
				if (ground == 0 && air == 0 && noise == 0 && info.Water == 0)
					return default;

				var scale = 100;
				var useWorkers = info.Scale == PollutionScale.Workers || (info.Scale == PollutionScale.Auto && building.Category == ZoneCategory.Industrial);
				if (useWorkers && building.Info.MaxJobs > 0)
					scale = 30 + 70 * Math.Min(building.Workers, building.Info.MaxJobs) / building.Info.MaxJobs;

				return new PollutionEmission
				{
					Ground = ground * scale / 100,
					Air = air * scale / 100,
					Noise = noise * scale / 100,
					Water = info.Water * scale / 100,
					Radius = info.Radius
				};
			}
		}
	}
}
