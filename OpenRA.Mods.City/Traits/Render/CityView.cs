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
using System.Numerics;

namespace OpenRA.Mods.City.Traits
{
	/// <summary>
	/// Client-side view toggles of the isometric renderer. Render only: never synced, never an order, never read by
	/// simulation code. UI binds its toolbar button and hotkey to these.
	/// </summary>
	public static class CityView
	{
		/// <summary>Name of the hotkey definition (hotkeys.yaml) that toggles <see cref="SeeThrough"/>.</summary>
		public const string SeeThroughHotkey = "CitySeeThrough";

		static bool seeThrough;

		/// <summary>
		/// RCT2-style "see-through buildings": every building is drawn as its lot ground plus a one-storey stub so
		/// roads, vehicles and people behind tall façades are visible. Survives across games in this session.
		/// </summary>
		public static bool SeeThrough
		{
			get => seeThrough;
			set
			{
				if (seeThrough == value)
					return;

				seeThrough = value;
				SeeThroughChanged?.Invoke();
			}
		}

		/// <summary>Raised after <see cref="SeeThrough"/> changed (any thread that renders; UI thread in practice).</summary>
		public static event Action SeeThroughChanged;

		public static void ToggleSeeThrough() { SeeThrough = !SeeThrough; }

		/// <summary>Debug/perf switch: strip slicing of tall sprites (autotest "isoslice=0" measures its cost).</summary>
		public static bool Slicing { get; set; } = true;

		/// <summary>
		/// Ambient multiply for depth-sorted world sprites (buildings, props, vehicles, people, FX) at night, in
		/// winter light and under weather. Ground layers are tinted by the post-process pass instead; emissive
		/// sprites (lit windows, headlights, lamp heads) must not apply it. Returns Vector3.One when the world has no
		/// CityAtmosphere or while the tint post-process still runs after the actors (then it darkens them itself).
		/// </summary>
		public static Vector3 SpriteTint(World world)
		{
			var atmosphere = world?.WorldActor.TraitOrDefault<CityAtmosphere>();
			return atmosphere?.SpriteTint ?? Vector3.One;
		}
	}
}
