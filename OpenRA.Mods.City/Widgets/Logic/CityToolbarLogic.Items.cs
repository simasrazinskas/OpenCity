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
using System.Globalization;
using System.Linq;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;

namespace OpenRA.Mods.City.Widgets.Logic
{
	public partial class CityToolbarLogic
	{
		[FluentReference("cost")]
		const string RoadDescription = "label-city-road-description";

		[FluentReference("population")]
		const string UnlocksAt = "label-city-unlocks-at";

		[FluentReference("population")]
		const string RequiresPopulation = "label-city-requires-population";

		[FluentReference("cost", "upkeep")]
		const string CostAndUpkeep = "label-city-cost-upkeep";

		[FluentReference("cost")]
		const string CostOnly = "label-city-cost";

		[FluentReference("cost", "upkeep", "lanes", "speed")]
		const string RoadTypeDescription = "label-road-type-description";

		[FluentReference]
		const string LockedLabel = "label-zone-locked";

		[FluentReference("cost")]
		const string CostPerCell = "label-city-cost-per-cell";

		[FluentReference("cost")]
		const string CostPerStop = "label-city-cost-per-stop";

		[FluentReference]
		const string SelectHubFirst = "label-industry-select-hub";

		static readonly Color AreaAddColor = Color.FromArgb(110, 120, 220, 90);
		static readonly Color AreaRemoveColor = Color.FromArgb(120, 255, 150, 30);

		bool UtilityToolsAvailable => ctx.Utilities != null;

		bool TransitToolsAvailable => ctx.TransitUi != null;

		List<ToolItem> ItemsFor(string tab)
		{
			var items = new List<ToolItem>();
			switch (tab)
			{
				case "road":
					items.AddRange(RoadItems());
					break;
				case "zoning":
					items.AddRange(ZoneItems());
					break;
				case "networks":
					items.AddRange(NetworkItems());
					break;
				case "industry":
					items.AddRange(IndustryItems());
					break;
				case "transit":
					items.AddRange(TransitItems());
					break;
			}

			if (placeables.TryGetValue(tab, out var buildings))
				items.AddRange(buildings);

			return items;
		}

		// ---- roads ----
		RoadLayer Roads => world.WorldActor.TraitOrDefault<RoadLayer>();

		List<ToolItem> RoadItems()
		{
			var types = Roads?.Types;
			if (types == null || types.Count == 0)
			{
				// No road registry: one plain road, no modes.
				return
				[
					new ToolItem
					{
						Id = "road",
						Icon = "road_street",
						Name = FluentProvider.GetMessage(CategoryRoad),
						Description = FluentProvider.GetMessage(RoadDescription, "cost", CityUtils.FormatMoney(10)),
						Cost = CityUtils.FormatMoney(10),
						Create = () => new RoadOrderGenerator(world)
					}
				];
			}

			var items = new List<ToolItem>();
			foreach (var type in types)
			{
				var data = type;
				var icon = "road_" + type.Name;
				var name = FluentProvider.TryGetMessage(type.Info.DisplayName ?? "", out var display) ? display : CityUi.Message("label-road-type-" + type.Name);
				items.Add(new ToolItem
				{
					Id = "roadtype:" + type.Name,
					Icon = icon,
					Name = name,
					Description = FluentProvider.GetMessage(RoadTypeDescription,
						"cost", CityUtils.FormatMoney(type.Info.Cost), "upkeep", CityUtils.FormatMoney(type.Info.Upkeep),
						"lanes", type.Info.Lanes, "speed", type.Info.SpeedPercent),
					Cost = CityUtils.FormatMoney(type.Info.Cost),
					IsDisabled = () => !RoadTypeUnlocked(data),
					IsActive = () => ctx.IsToolActive("road") && toolState.Road.TypeId == data.Id,
					OnSelect = () =>
					{
						toolState.Road.TypeId = data.Id;
						StartRoadTool();
					},
					ExtraTooltip = () => RoadTypeUnlocked(data) ? "" : FluentProvider.GetMessage(LockedLabel)
				});
			}

			items.Add(RoadModeItem("draw", () => !toolState.Road.OneWay && !toolState.Road.Replace && !toolState.Road.Paired,
				() => toolState.Road = new RoadToolOptions { TypeId = toolState.Road.TypeId }, "mode_straight"));

			items.Add(RoadModeItem("oneway", () => toolState.Road.OneWay, () => toolState.Road.OneWay = !toolState.Road.OneWay, "mode_oneway", "tool:oneway"));
			items.Add(RoadModeItem("replace", () => toolState.Road.Replace, () => toolState.Road.Replace = !toolState.Road.Replace, "mode_replace"));
			items.Add(RoadModeItem("paired", () => toolState.Road.Paired, () => toolState.Road.Paired = !toolState.Road.Paired, "mode_paired"));

			items.Add(RoadModeItem("bridge", () => toolState.Road.Bridge, () => toolState.Road.Bridge = !toolState.Road.Bridge, "mode_bridge"));

			items.Add(PrefabItem("roundabout", NetworkOrders.PrefabRoundabout, "prefab_roundabout", "tool:roundabout"));
			items.Add(PrefabItem("roundabout-large", NetworkOrders.PrefabRoundaboutLarge, "prefab_roundabout_large", "tool:roundabout"));
			items.Add(PrefabItem("ramp", NetworkOrders.PrefabRamp, "prefab_ramp", "tool:highway"));

			items.Add(ControlItem("control-yield", JunctionControl.Yield));
			items.Add(ControlItem("control-stop", JunctionControl.Stop));
			items.Add(ControlItem("control-signal", JunctionControl.Signal));
			items.Add(ControlItem("control-default", null));

			foreach (var addon in Roads.AddonTypes)
				items.Add(AddonItem(addon));

			if (Roads.AddonTypes.Count > 0)
				items.Add(AddonRemoveItem());

			return items;
		}

		static string AddonIcon(RoadAddons flag)
		{
			switch (flag)
			{
				case RoadAddons.Trees: return "addon_trees";
				case RoadAddons.Barrier: return "addon_barrier";
				case RoadAddons.Lights: return "addon_lights";
				case RoadAddons.Parking: return "addon_parking";
				case RoadAddons.BusLane: return "addon_buslane";
				default: return "addon_bikelane";
			}
		}

		ToolItem AddonItem(RoadAddonData addon)
		{
			var flag = addon.Flag;
			var key = "road-addon:" + addon.Name;
			var name = FluentProvider.TryGetMessage(addon.Info.DisplayName ?? "", out var display) ? display : CityUi.Prettify(addon.Name);
			return new ToolItem
			{
				Id = "addon:" + addon.Name,
				Icon = AddonIcon(flag),
				Name = name,
				Description = FluentProvider.GetMessage(CostAndUpkeep, "cost", CityUtils.FormatMoney(addon.Info.Cost) + "/cell",
					"upkeep", CityUtils.FormatMoney(addon.Info.UpkeepPer10) + "/10 cells"),
				Cost = CityUtils.FormatMoney(addon.Info.Cost),
				IsDisabled = () => !Unlocked(key),
				IsActive = () => ctx.IsToolActive("addon") && toolState.Addon == flag,
				ExtraTooltip = () => Unlocked(key) ? "" : FluentProvider.GetMessage(LockedLabel),
				OnSelect = () =>
				{
					toolState.Addon = flag;
					StartAddonTool();
				}
			};
		}

		ToolItem AddonRemoveItem()
		{
			return new ToolItem
			{
				Id = "addon-remove",
				Icon = "addon_remove",
				Name = CityUi.Message("label-road-mode-addon-remove"),
				Description = CityUi.Message("label-road-mode-addon-remove-desc", ""),
				IsActive = () => toolState.AddonRemove,
				OnSelect = () =>
				{
					toolState.AddonRemove = !toolState.AddonRemove;
					if (ctx.IsToolActive("addon"))
						StartAddonTool();
				}
			};
		}

		void StartAddonTool()
		{
			ctx.ActivateTool("addon", new RoadAddonOrderGenerator(world, toolState.Addon, toolState.AddonRemove));
		}

		bool Unlocked(string key)
		{
			return key == null || ctx.Progression == null || ctx.Progression.IsUnlocked(key);
		}

		bool RoadTypeUnlocked(RoadTypeData type)
		{
			if (ctx.Progression != null)
				return ctx.Progression.IsUnlocked("road:" + type.Name);

			return manager == null || manager.UnlimitedMoney || type.Info.UnlockPopulation <= manager.Population;
		}

		/// <summary>Starts the road tool with the palette's type and modes, or pushes changed options into the running one.</summary>
		void StartRoadTool()
		{
			if (ctx.IsToolActive("road") && world.OrderGenerator is RoadOrderGenerator running)
				running.Options = toolState.Road;
			else
				ctx.ActivateTool("road", new RoadOrderGenerator(world, toolState.Road));
		}

		ToolItem RoadModeItem(string mode, System.Func<bool> active, System.Action toggle, string icon, string unlockKey = null)
		{
			return new ToolItem
			{
				Id = "roadmode:" + mode,
				Icon = icon,
				Name = CityUi.Message("label-road-mode-" + mode),
				Description = CityUi.Message("label-road-mode-" + mode + "-desc", ""),
				IsActive = active,
				IsDisabled = () => !Unlocked(unlockKey),
				ExtraTooltip = () => Unlocked(unlockKey) ? "" : FluentProvider.GetMessage(LockedLabel),
				OnSelect = () =>
				{
					toggle();
					StartRoadTool();
				}
			};
		}

		ToolItem PrefabItem(string id, string prefab, string icon, string unlockKey)
		{
			return new ToolItem
			{
				Id = "prefab:" + id,
				Icon = icon,
				Name = CityUi.Message("label-road-mode-" + id),
				Description = CityUi.Message("label-road-mode-" + id + "-desc", ""),
				IsDisabled = () => !Unlocked(unlockKey),
				ExtraTooltip = () => Unlocked(unlockKey) ? "" : FluentProvider.GetMessage(LockedLabel),
				Create = () => new RoadPrefabOrderGenerator(world, prefab)
			};
		}

		ToolItem ControlItem(string id, JunctionControl? value)
		{
			return new ToolItem
			{
				Id = "road-" + id,
				Icon = value switch
				{
					JunctionControl.Yield => "ctl_yield",
					JunctionControl.Stop => "ctl_stop",
					JunctionControl.Signal => "ctl_signal",
					_ => "ctl_default",
				},
				Name = CityUi.Message("label-road-mode-" + id),
				Description = CityUi.Message("label-road-mode-" + id + "-desc", ""),
				Create = () => new RoadControlOrderGenerator(world, value)
			};
		}

		// ---- zones ----
		List<ToolItem> ZoneItems()
		{
			ToolItem Zone(ZoneType zone, string icon, string suffix)
			{
				return new ToolItem
				{
					Id = "zone:" + zone,
					Icon = icon,
					Name = CityUi.Message("label-zone-" + suffix),
					Description = CityUi.Message("label-zone-" + suffix + "-desc", ""),
					IsDisabled = () => ctx.Progression != null && zone != ZoneType.None && !ctx.Progression.IsUnlocked("zone:" + zone),
					ExtraTooltip = () => ctx.Progression != null && zone != ZoneType.None && !ctx.Progression.IsUnlocked("zone:" + zone) ?
						FluentProvider.GetMessage("label-zone-locked") : "",
					Create = () => new ZoneOrderGenerator(world, zone)
				};
			}

			var items = new List<ToolItem>
			{
				Zone(ZoneType.ResidentialLow, "zone_res_low", "res-low"),
				Zone(ZoneType.ResidentialHigh, "zone_res_high", "res-high"),
				Zone(ZoneType.CommercialLow, "zone_com_low", "com-low"),
				Zone(ZoneType.CommercialHigh, "zone_com_high", "com-high"),
				Zone(ZoneType.Industrial, "zone_ind", "ind"),
				Zone(ZoneType.Office, "zone_off", "off")
			};

			// The additional zone types are offered once the zoning WP ships buildings for them.
			var extra = new (ZoneType Zone, string Icon, string Suffix)[]
			{
				(ZoneType.ResidentialRow, "zone_res_row", "res-row"),
				(ZoneType.ResidentialMedium, "zone_res_med", "res-med"),
				(ZoneType.ResidentialMixed, "zone_res_mixed", "res-mixed"),
				(ZoneType.ResidentialLowRent, "zone_res_lowrent", "res-lowrent"),
				(ZoneType.OfficeHigh, "zone_off_high", "off-high"),
				(ZoneType.Warehouse, "zone_warehouse", "warehouse")
			};

			foreach (var (zone, icon, suffix) in extra)
				if (HasGrowables(zone))
					items.Add(Zone(zone, icon, suffix));

			items.Add(Zone(ZoneType.None, "tool_dezone", "dezone"));
			return items;
		}

		bool HasGrowables(ZoneType zone)
		{
			var prefix = zone.GrowablePrefix();
			return prefix != null && world.Map.Rules.Actors.Keys.Any(name => name.StartsWith(prefix + "-", System.StringComparison.Ordinal));
		}

		// ---- networks, industry, transit ----
		List<ToolItem> NetworkItems()
		{
			var items = new List<ToolItem>();
			if (!UtilityToolsAvailable)
				return items;

			items.Add(LineItem("powerline", "net_powerline", 0, false));
			items.Add(LineItem("pipe-water", "net_pipe_water", 1, false));
			items.Add(LineItem("pipe-sewage", "net_pipe_sewage", 2, false));
			items.Add(LineItem("pipe-both", "net_pipe_both", 3, false));
			items.Add(LineItem("powerline-remove", "net_remove", 0, true));
			items.Add(LineItem("pipe-remove", "net_remove", 3, true));
			return items;
		}

		ToolItem LineItem(string id, string icon, int kind, bool remove)
		{
			return new ToolItem
			{
				Id = id,
				Icon = icon,
				Name = CityUi.Message("label-tool-" + id),
				Description = CityUi.Message("label-tool-" + id + "-desc", ""),
				Create = () => new UtilityOrderGenerator(world, kind, remove)
			};
		}

		Actor SelectedHub()
		{
			var selection = world.Selection.Actors;
			if (selection.Count != 1)
				return null;

			var actor = selection.First();
			return !actor.IsDead && actor.IsInWorld && actor.TraitOrDefault<ExtractorHub>() != null ? actor : null;
		}

		List<ToolItem> IndustryItems()
		{
			var items = new List<ToolItem>();
			if (!placeables.ContainsKey("industry"))
				return items;

			items.Add(HubAreaItem("area-paint", "tool_area_paint", true));
			items.Add(HubAreaItem("area-clear", "tool_area_clear", false));
			return items;
		}

		ToolItem HubAreaItem(string id, string icon, bool add)
		{
			return new ToolItem
			{
				Id = id,
				Icon = icon,
				Name = CityUi.Message("label-tool-" + id),
				Description = CityUi.Message("label-tool-" + id + "-desc", ""),
				IsDisabled = () => SelectedHub() == null,
				ExtraTooltip = () => SelectedHub() == null ? FluentProvider.GetMessage(SelectHubFirst) : "",
				OnSelect = () =>
				{
					var hub = SelectedHub();
					if (hub == null)
						return;

					ctx.ActivateTool(id, new UiAreaToolGenerator(world, (p, a, b) => UiOrders.HubArea(p, hub, a, b, add),
						add ? AreaAddColor : AreaRemoveColor,
						(a, b) => FluentProvider.GetMessage("label-tool-cells-count", "count", CityUtils.Rect(a, b).Count())));
				}
			};
		}

		ITransitUiSourceEx TransitEx => ctx.Get<ITransitUiSourceEx>();

		bool ModeUnlocked(string mode) => TransitEx == null || TransitEx.IsModeUnlocked(mode);

		List<ToolItem> TransitItems()
		{
			var items = new List<ToolItem>();
			if (!TransitToolsAvailable)
				return items;

			items.Add(StopItem("busstop", "tr_bus_stop", TransitMode.Bus));
			items.Add(StopItem("taxistand", "tr_taxi_stand", TransitMode.Taxi));
			items.Add(StopItem("tramstop", "tr_tram_stop", TransitMode.Tram));

			items.Add(TrackItem("tramtrack", "tr_tramtrack", "tram", false));
			items.Add(TrackItem("tramtrack-remove", "net_remove", "tram", true));
			items.Add(TrackItem("rail", "tr_rail", "rail", false));
			items.Add(TrackItem("rail-remove", "net_remove", "rail", true));

			items.Add(LineToolItem("busline", "tr_bus", TransitMode.Bus));
			items.Add(LineToolItem("tramline", "tr_tram", TransitMode.Tram));
			items.Add(LineToolItem("metroline", "tr_metro", TransitMode.Metro));
			items.Add(LineToolItem("trainline", "tr_train", TransitMode.Train));

			items.Add(new ToolItem
			{
				Id = "stop-remove",
				Icon = "tr_stop_remove",
				Name = CityUi.Message("label-tool-stop-remove"),
				Description = CityUi.Message("label-tool-stop-remove-desc", ""),
				Create = () => new UiClickToolGenerator(world,
					(p, cell) => ctx.TransitUi != null && ctx.TransitUi.StopAt(cell) != 0 ? TransitOrders.RemoveStopOrder(p, ctx.TransitUi.StopAt(cell)) : null,
					null, cell => (ctx.TransitUi != null && ctx.TransitUi.StopAt(cell) != 0 ? CityDragOrderGenerator.RemoveColor : CityDragOrderGenerator.InvalidColor, null))
			});

			return items;
		}

		/// <summary>A line tool of one transit mode, disabled until the progression unlocks the mode.</summary>
		ToolItem LineToolItem(string id, string icon, TransitMode mode)
		{
			var name = TransitLayer.ModeName(mode);
			return new ToolItem
			{
				Id = id,
				Icon = icon,
				Name = CityUi.Message("label-tool-" + id),
				Description = CityUi.Message("label-tool-" + id + "-desc", ""),
				IsDisabled = () => ctx.TransitUi == null || !ModeUnlocked(name),
				ExtraTooltip = () => ModeUnlocked(name) ? "" : FluentProvider.GetMessage(LockedLabel),
				Create = () => new UiTransitLineGenerator(world, mode, ctx.TransitUi?.Lines.Count ?? 0)
			};
		}

		/// <summary>Tram track (laid on roads) or rail track drag with the planner's preview, or its removal.</summary>
		ToolItem TrackItem(string id, string icon, string kind, bool remove)
		{
			var mode = kind == "rail" ? "train" : "tram";
			return new ToolItem
			{
				Id = id,
				Icon = icon,
				Name = CityUi.Message("label-tool-" + id),
				Description = CityUi.Message("label-tool-" + id + "-desc", "") + TrackCost(kind),
				IsDisabled = () => TransitEx == null || !ModeUnlocked(mode),
				ExtraTooltip = () => ModeUnlocked(mode) ? "" : FluentProvider.GetMessage(LockedLabel),
				Create = () => new UiTrackToolGenerator(world, kind, remove)
			};
		}

		string TrackCost(string kind)
		{
			var ex = TransitEx;
			if (ex == null)
				return "";

			var cost = kind == "rail" ? ex.RailCostPerCell : ex.TramTrackCostPerCell;
			return "\n" + FluentProvider.GetMessage(CostPerCell, "cost", CityUtils.FormatMoney(cost));
		}

		/// <summary>A stop or stand of one mode; the preview colour and label come from the planner's own placement check.</summary>
		ToolItem StopItem(string id, string icon, TransitMode mode)
		{
			var name = TransitLayer.ModeName(mode);
			return new ToolItem
			{
				Id = id,
				Icon = icon,
				Name = CityUi.Message("label-tool-" + id),
				Description = CityUi.Message("label-tool-" + id + "-desc", "") + StopCost(),
				IsDisabled = () => !ModeUnlocked(name),
				ExtraTooltip = () => ModeUnlocked(name) ? "" : FluentProvider.GetMessage(LockedLabel),
				Create = () => new UiClickToolGenerator(world, (p, cell) => TransitEx?.CheckStop(cell, name) == null ? UiOrders.PlaceStop(p, cell, mode) : null,
					null, cell =>
					{
						var error = TransitEx?.CheckStop(cell, name);
						return error == null ? (CityDragOrderGenerator.ValidColor, null) : (CityDragOrderGenerator.InvalidColor, CityUi.Message(error));
					})
			};
		}

		string StopCost()
		{
			var ex = TransitEx;
			return ex == null ? "" : "\n" + FluentProvider.GetMessage(CostPerStop, "cost", CityUtils.FormatMoney(ex.StopCost));
		}

		// ---- building placeables (services, utilities, hubs, depots) ----
		ToolItem MakePlaceableItem(ActorInfo actor, CityPlaceableInfo placeable)
		{
			var tooltip = actor.TraitInfoOrDefault<TooltipInfo>();
			var name = tooltip != null ? FluentProvider.GetMessage(tooltip.Name) : actor.Name;
			var upkeep = actor.TraitInfoOrDefault<CityBuildingInfo>()?.Upkeep ?? 0;

			var description = FluentProvider.GetMessage(upkeep > 0 ? CostAndUpkeep : CostOnly,
				"cost", CityUtils.FormatMoney(placeable.Cost),
				"upkeep", CityUtils.FormatMoney(upkeep));

			if (!string.IsNullOrEmpty(placeable.Description))
				description += "\n" + FluentProvider.GetMessage(placeable.Description);

			var signature = IsSignature(actor.Name);
			if (placeable.UnlockPopulation > 0 && !signature)
				description += "\n" + FluentProvider.GetMessage(RequiresPopulation, "population", placeable.UnlockPopulation.ToString("N0", CultureInfo.CurrentCulture));

			var actorName = actor.Name;
			return new ToolItem
			{
				Id = "build:" + actorName,
				Icon = CategoryIcon(placeable.Category),
				Name = name,
				Description = description,
				Cost = CityUtils.FormatMoney(placeable.Cost),
				ActorType = actorName,
				ExtraTooltip = signature ? () => SignatureTooltip(actorName) : null,
				Create = () => new PlaceCityBuildingOrderGenerator(world, actorName)
			};
		}
	}
}
