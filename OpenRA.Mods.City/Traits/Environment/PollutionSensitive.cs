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
	[Desc("Plants that die when the ground (or air) under them stays heavily polluted. Checked sparsely and bucketed by actor id.")]
	public class PollutionSensitiveInfo : TraitInfo
	{
		[Desc("Ground pollution (0..100) above which the plant suffers.")]
		public readonly int GroundLimit = 70;

		[Desc("Air pollution (0..100) above which the plant suffers (101 = never).")]
		public readonly int AirLimit = 101;

		[Desc("Ticks between checks.")]
		public readonly int CheckTicks = 100;

		[Desc("Consecutive bad checks before the plant dies (6 checks = 600 ticks, a quarter of a game day).")]
		public readonly int DeathChecks = 6;

		public override object Create(ActorInitializer init) { return new PollutionSensitive(init.Self, this); }
	}

	public class PollutionSensitive : ITick, ISync
	{
		readonly PollutionSensitiveInfo info;
		PollutionLayer layer;

		[VerifySync]
		int stress;

		public PollutionSensitive(Actor self, PollutionSensitiveInfo info)
		{
			this.info = info;
		}

		void ITick.Tick(Actor self)
		{
			var every = Math.Max(1, info.CheckTicks);
			if ((self.World.WorldTick + (int)self.ActorID) % every != 0)
				return;

			layer ??= self.World.WorldActor.TraitOrDefault<PollutionLayer>();
			if (layer == null)
				return;

			layer.Exposure(self.Location, out var ground, out var air, out _);
			if (ground > info.GroundLimit || air > info.AirLimit)
				stress++;
			else if (stress > 0)
				stress--;

			if (stress < info.DeathChecks)
				return;

			var cell = self.Location;
			self.World.AddFrameEndTask(w =>
			{
				if (self.Disposed)
					return;

				self.Dispose();
				w.WorldActor.TraitOrDefault<CityStatistics>()?.Post("chirp-trees-dying", 0, null, 2, cell);
			});

			stress = int.MinValue / 2;
		}
	}
}
