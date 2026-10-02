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
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// Names and RCT2 icons of progression unlock keys ("hospital", "zone:Office", "road:avenue", "tool:policies",
	/// "service:garbage"), and the unlock lists of milestones and development tree nodes (static rules data).
	/// </summary>
	public sealed class CityProgressUnlocks
	{
		readonly World world;
		readonly Dictionary<string, string[]> nodeUnlocks = [];

		public CityProgressUnlocks(World world)
		{
			this.world = world;
			var player = world.Map.Rules.Actors[SystemActors.Player];
			foreach (var node in player.TraitInfos<ProgressionNodeInfo>())
			{
				if (string.IsNullOrEmpty(node.InstanceName))
					continue;

				nodeUnlocks[node.InstanceName] = node.Unlocks;
			}
		}

		/// <summary>The unlock keys of a node (empty when unknown).</summary>
		public string[] OfNode(string id)
		{
			return nodeUnlocks.TryGetValue(id, out var keys) ? keys : [];
		}

		/// <summary>The icon of a development tree: its first unlock, or the tree category.</summary>
		public static string TreeIcon(string tree)
		{
			return tree switch
			{
				"roads" => "road_street",
				"electricity" => "cat_power",
				"transport" => "tr_metro",
				"water" => "cat_water",
				_ => Existing("cat_" + tree, "cat_signature")
			};
		}

		/// <summary>The icon of an unlock key.</summary>
		public string Icon(string key)
		{
			if (string.IsNullOrEmpty(key))
				return "cat_signature";

			var colon = key.IndexOf(':');
			if (colon > 0)
			{
				var kind = key[..colon];
				var name = key[(colon + 1)..];
				switch (kind)
				{
					case "zone":
						return Existing(ZoneIcon(name), "cat_zoning");
					case "road":
						return Existing("road_" + name, "road_street");
					case "service":
						return name == "hotels" ? "stat_hotel" : Existing("cat_" + name, "cat_signature");
					case "landmark":
						return "cat_signature";
					case "tool":
						return name switch
						{
							"roundabout" => "prefab_roundabout",
							"oneway" => "mode_oneway",
							"statistics" => "pnl_stats",
							"policies" => "pnl_policies",
							"districts" => "pnl_districts",
							"tourism" => "stat_tourist",
							_ => Existing("pnl_" + name, "pnl_game_menu")
						};
				}
			}

			if (key.EndsWith("-hub", StringComparison.Ordinal))
				return Existing("hub_" + key[..^4], "cat_industry");

			if (world.Map.Rules.Actors.TryGetValue(key, out var actor) && actor.TraitInfoOrDefault<CityPlaceableInfo>() is { } placeable)
				return Existing("cat_" + placeable.Category, "cat_signature");

			return "cat_signature";
		}

		/// <summary>The display name of an unlock key.</summary>
		public string Name(string key)
		{
			if (string.IsNullOrEmpty(key))
				return "";

			var colon = key.IndexOf(':');
			if (colon > 0)
			{
				var kind = key[..colon];
				var name = key[(colon + 1)..];
				switch (kind)
				{
					case "zone":
						return Enum.TryParse<ZoneType>(name, out var zone) ? CityUi.ZoneName(zone) : CityUi.Prettify(name);
					case "road":
						return CityUi.Message("label-road-type-" + name, CityUi.Prettify(name));
					case "service":
						return CityUi.Message("label-service-" + name, CityUi.Prettify(name));
					case "tool":
						return name switch
						{
							"statistics" => CityUi.Message("button-city-tool-stats"),
							"oneway" => CityUi.Message("label-road-mode-oneway"),
							"roundabout" => CityUi.Message("node-road-roundabout"),
							_ => CityUi.Message("button-city-tool-" + name, CityUi.Prettify(name))
						};
					default:
						return CityUi.Prettify(key.Replace(':', ' '));
				}
			}

			if (world.Map.Rules.Actors.TryGetValue(key, out var actor) && actor.TraitInfoOrDefault<TooltipInfo>() is { } tooltip)
				return FluentProvider.GetMessage(tooltip.Name);

			return CityUi.Prettify(key);
		}

		/// <summary>Icon and name of up to `max` distinct unlock keys.</summary>
		public IEnumerable<(string Icon, string Name)> List(IEnumerable<string> keys, int max)
		{
			return keys.Take(max).Select(k => (Icon(k), Name(k)));
		}

		static string ZoneIcon(string zone)
		{
			return zone switch
			{
				"ResidentialLow" => "zone_res_low",
				"ResidentialRow" => "zone_res_row",
				"ResidentialMedium" => "zone_res_med",
				"ResidentialHigh" => "zone_res_high",
				"ResidentialMixed" => "zone_res_mixed",
				"ResidentialLowRent" => "zone_res_lowrent",
				"CommercialLow" => "zone_com_low",
				"CommercialHigh" => "zone_com_high",
				"Industrial" => "zone_ind",
				"Warehouse" => "zone_warehouse",
				"Office" => "zone_off",
				"OfficeHigh" => "zone_off_high",
				_ => "cat_zoning"
			};
		}

		static string Existing(string icon, string fallback)
		{
			return CityTheme.Icon(icon) != null ? icon : fallback;
		}
	}
}
