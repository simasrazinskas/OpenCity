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
	[Desc("Marks a highway entry point at the map edge. On world load RoadLayer lays a road from here inward.",
		"Growth only happens on cells with road access to a network that reaches an outside connection.")]
	public class OutsideConnectionInfo : TraitInfo
	{
		[Desc("Direction the initial road runs, as a cell vector (e.g. 1,0 runs east).")]
		public readonly CVec Direction = new(1, 0);

		[Desc("Number of road cells laid on world load.")]
		public readonly int Length = 12;

		public override object Create(ActorInitializer init) { return new OutsideConnection(this); }
	}

	public class OutsideConnection
	{
		public readonly OutsideConnectionInfo Info;

		public OutsideConnection(OutsideConnectionInfo info)
		{
			Info = info;
		}
	}
}
