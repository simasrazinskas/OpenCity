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
	[Desc("Rubble left by a collapsed building. It blocks the lot for a while, can be bulldozed for free, then vanishes so the zone regrows.")]
	public class RubbleInfo : TraitInfo
	{
		[Desc("Ticks until the rubble vanishes (two months by default).")]
		public readonly int Ticks = 4800;

		public override object Create(ActorInitializer init) { return new Rubble(this); }
	}

	public class Rubble : ITick, ISync
	{
		readonly RubbleInfo info;

		[VerifySync]
		int left;

		public Rubble(RubbleInfo info)
		{
			this.info = info;
			left = info.Ticks;
		}

		/// <summary>0..100 progress until the rubble disappears.</summary>
		public int Progress => info.Ticks <= 0 ? 100 : 100 - left * 100 / info.Ticks;

		void ITick.Tick(Actor self)
		{
			if (--left > 0)
				return;

			self.World.AddFrameEndTask(w =>
			{
				if (!self.Disposed)
					self.Dispose();
			});
		}
	}
}
