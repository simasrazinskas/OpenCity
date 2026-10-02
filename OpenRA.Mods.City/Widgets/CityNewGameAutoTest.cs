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

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>Carry the developer harness from the real map picker into its newly started game.</summary>
	public static class CityNewGameAutoTest
	{
		public static void PrepareGame()
		{
			var gameSpec = Environment.GetEnvironmentVariable("OPENCITY_NEWGAME_AUTOTEST");
			if (!string.IsNullOrWhiteSpace(gameSpec) && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENCITY_AUTOTEST")))
				Environment.SetEnvironmentVariable("OPENCITY_AUTOTEST", gameSpec);
		}
	}
}
