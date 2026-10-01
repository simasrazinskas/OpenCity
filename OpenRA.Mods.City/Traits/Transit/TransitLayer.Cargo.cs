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

namespace OpenRA.Mods.City.Traits
{
	// Cargo terminals register here so the logistics simulation (and the UI) can find them.
	public sealed partial class TransitLayer
	{
		readonly List<CargoTerminal> cargo = [];

		public IReadOnlyList<CargoTerminal> CargoTerminals => cargo;

		public int CargoVersion { get; private set; }

		public void RegisterCargo(CargoTerminal terminal)
		{
			var at = cargo.Count;
			while (at > 0 && cargo[at - 1].Actor.ActorID > terminal.Actor.ActorID)
				at--;

			cargo.Insert(at, terminal);
			CargoVersion++;
		}

		public void UnregisterCargo(CargoTerminal terminal)
		{
			if (cargo.Remove(terminal))
				CargoVersion++;
		}

		/// <summary>True when a building touches rail that reaches the map border.</summary>
		public bool IsRailConnected(Actor building)
		{
			var access = RailAccess(building);
			return access != CPos.Zero && rail != null && rail.IsConnectedToEdge(access);
		}
	}
}
