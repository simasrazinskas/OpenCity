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
	/// <summary>Order strings and factories of the zoning WP (resolved by ZoningTool).</summary>
	public static class ZoningOrders
	{
		/// <summary>Target = an abandoned growable building. It collapses into rubble immediately.</summary>
		public const string Condemn = "CityCondemn";

		/// <summary>Highest zone type the zoning tool accepts (the enum is append-only).</summary>
		public const ZoneType LastZone = ZoneType.Warehouse;

		public static Order CondemnOrder(Player p, Actor building) =>
			new(Condemn, p.PlayerActor, Target.FromActor(building), false);
	}
}
