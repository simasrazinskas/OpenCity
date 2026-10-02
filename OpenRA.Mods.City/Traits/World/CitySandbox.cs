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
using OpenRA.Network;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Optional sandbox game mode: all development and land unlocked, with unlimited city money.")]
	public class CitySandboxInfo : TraitInfo, ILobbyOptions
	{
		[FluentReference]
		public const string Label = "checkbox-city-sandbox";

		[FluentReference]
		public const string Description = "checkbox-city-sandbox-description";

		IEnumerable<LobbyOption> ILobbyOptions.LobbyOptions(MapPreview map)
		{
			yield return new LobbyBooleanOption(map, CitySandbox.Option, Label, Description, true, 20, false, false);
		}

		public override object Create(ActorInitializer init) => new CitySandbox(init.Self.World);
	}

	public sealed class CitySandbox : ISync
	{
		public const string Option = "city-sandbox";

		[VerifySync]
		public bool Enabled { get; }

		public CitySandbox(World world)
		{
			Enabled = EnabledFor(world);
		}

		// Read the serialized lobby option directly: player traits may be constructed before world traits.
		public static bool EnabledFor(World world) => EnabledFor(world.LobbyInfo.GlobalSettings);

		public static bool EnabledFor(Session.Global settings) => settings.OptionOrDefault(Option, false);
	}
}
