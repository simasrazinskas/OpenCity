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
using OpenRA.Graphics;
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
						Collection = "city-icons",
						Icon = "road",
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
				var icon = ChromeProvider.TryGetImage("city-icons", "road-" + type.Name) != null ? "road-" + type.Name : "road";
				var name = FluentProvider.TryGetMessage(type.Info.DisplayName ?? "", out var display) ? display : CityUi.Message("label-road-type-" + type.Name);
				items.Add(new ToolItem
				{
					Id = "roadtype:" + type.Name,
					Collection = "city-icons",
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
				() => toolState.Road = new RoadToolOptions { TypeId = toolState.Road.TypeId }, "road-draw"));

			items.Add(RoadModeItem("oneway", () => toolState.Road.OneWay, () => toolState.Road.OneWay = !toolState.Road.OneWay, "road-oneway", "tool:oneway"));
			items.Add(RoadModeItem("replace", () => toolState.Road.Replace, () => toolState.Road.Replace = !toolState.Road.Replace, "road-replace"));
			items.Add(RoadModeItem("paired", () => toolState.Road.Paired, () => toolState.Road.Paired = !toolState.Road.Paired, "road-boulevard"));

			items.Add(PrefabItem("roundabout", NetworkOrders.PrefabRoundabout, "road-roundabout", "tool:roundabout"));
			items.Add(PrefabItem("roundabout-large", NetworkOrders.PrefabRoundaboutLarge, "road-roundabout", "tool:roundabout"));
			items.Add(PrefabItem("ramp", NetworkOrders.PrefabRamp, "road-interchange", "tool:highway"));

			items.Add(ControlItem("control-yield", JunctionControl.Yield));
			items.Add(ControlItem("control-stop", JunctionControl.Stop));
			items.Add(ControlItem("control-signal", JunctionControl.Signal));
			items.Add(ControlItem("control-default", null));
			return items;
		}

		bool Unlocked(string key)
		{
			return key == null || ctx.Progression == null || ctx.Progression.IsUnlocked(key);
		}

		bool RoadTypeUnlocked(RoadTypeData type)
		{
			if (ctx.Progression != null)
				return ctx.Progression.IsUnlocked("road:" + type.Name);

			return manager == null || type.Info.UnlockPopulation <= manager.Population;
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
				Collection = "city-icons",
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
				Collection = "city-icons",
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
				Collection = "city-icons",
				Icon = "road-signal",
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
					Collection = "city-icons",
					Icon = ChromeProvider.TryGetImage("city-icons", icon) != null ? icon : "zoning",
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
				Zone(ZoneType.ResidentialLow, "zone-res-low", "res-low"),
				Zone(ZoneType.ResidentialHigh, "zone-res-high", "res-high"),
				Zone(ZoneType.CommercialLow, "zone-com-low", "com-low"),
				Zone(ZoneType.CommercialHigh, "zone-com-high", "com-high"),
				Zone(ZoneType.Industrial, "zone-ind", "ind"),
				Zone(ZoneType.Office, "zone-off", "off")
			};

			// The additional zone types are offered once the zoning WP ships buildings for them.
			var extra = new (ZoneType Zone, string Icon, string Suffix)[]
			{
				(ZoneType.ResidentialRow, "zone-res-row", "res-row"),
				(ZoneType.ResidentialMedium, "zone-res-med", "res-med"),
				(ZoneType.ResidentialMixed, "zone-res-mixed", "res-mixed"),
				(ZoneType.ResidentialLowRent, "zone-res-lowrent", "res-lowrent"),
				(ZoneType.OfficeHigh, "zone-off-high", "off-high"),
				(ZoneType.Warehouse, "zone-warehouse", "warehouse")
			};

			foreach (var (zone, icon, suffix) in extra)
				if (HasGrowables(zone))
					items.Add(Zone(zone, icon, suffix));

			items.Add(Zone(ZoneType.None, "dezone", "dezone"));
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

			items.Add(LineItem("powerline", "power-line", 0, false));
			items.Add(LineItem("pipe-water", "pipe", 1, false));
			items.Add(LineItem("pipe-sewage", "sewage", 2, false));
			items.Add(LineItem("pipe-both", "networks", 3, false));
			items.Add(LineItem("powerline-remove", "area-clear", 0, true));
			items.Add(LineItem("pipe-remove", "area-clear", 3, true));
			return items;
		}

		ToolItem LineItem(string id, string icon, int kind, bool remove)
		{
			return new ToolItem
			{
				Id = id,
				Collection = "city-icons",
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

			items.Add(HubAreaItem("area-paint", "area-paint", true));
			items.Add(HubAreaItem("area-clear", "area-clear", false));
			return items;
		}

		ToolItem HubAreaItem(string id, string icon, bool add)
		{
			return new ToolItem
			{
				Id = id,
				Collection = "city-icons",
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

		List<ToolItem> TransitItems()
		{
			var items = new List<ToolItem>();
			if (!TransitToolsAvailable)
				return items;

			items.Add(StopItem("busstop", "bus-stop", TransitMode.Bus));
			items.Add(StopItem("taxistand", "taxi", TransitMode.Taxi));

			items.Add(new ToolItem
			{
				Id = "busline",
				Collection = "city-icons",
				Icon = "line",
				Name = CityUi.Message("label-tool-busline"),
				Description = CityUi.Message("label-tool-busline-desc", ""),
				IsDisabled = () => ctx.TransitUi == null,
				Create = () => new UiTransitLineGenerator(world, TransitMode.Bus, ctx.TransitUi?.Lines.Count ?? 0)
			});

			items.Add(new ToolItem
			{
				Id = "stop-remove",
				Collection = "city-icons",
				Icon = "area-clear",
				Name = CityUi.Message("label-tool-stop-remove"),
				Description = CityUi.Message("label-tool-stop-remove-desc", ""),
				Create = () => new UiClickToolGenerator(world,
					(p, cell) => ctx.TransitUi != null && ctx.TransitUi.StopAt(cell) != 0 ? TransitOrders.RemoveStopOrder(p, ctx.TransitUi.StopAt(cell)) : null,
					null, cell => (ctx.TransitUi != null && ctx.TransitUi.StopAt(cell) != 0 ? CityDragOrderGenerator.RemoveColor : CityDragOrderGenerator.InvalidColor, null))
			});

			return items;
		}

		ToolItem StopItem(string id, string icon, TransitMode mode)
		{
			return new ToolItem
			{
				Id = id,
				Collection = "city-icons",
				Icon = icon,
				Name = CityUi.Message("label-tool-" + id),
				Description = CityUi.Message("label-tool-" + id + "-desc", ""),
				Create = () => new UiClickToolGenerator(world, (p, cell) => UiOrders.PlaceStop(p, cell, mode))
			};
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

			if (placeable.UnlockPopulation > 0)
				description += "\n" + FluentProvider.GetMessage(RequiresPopulation, "population", placeable.UnlockPopulation.ToString("N0", CultureInfo.CurrentCulture));

			var actorName = actor.Name;
			var hasIcon = ChromeProvider.TryGetImage("city-buildicons", actorName) != null;
			return new ToolItem
			{
				Id = "build:" + actorName,
				Collection = hasIcon ? "city-buildicons" : "city-icons",
				Icon = hasIcon ? actorName : "services",
				Name = name,
				Description = description,
				Cost = CityUtils.FormatMoney(placeable.Cost),
				ActorType = actorName,
				Create = () => new PlaceCityBuildingOrderGenerator(world, actorName)
			};
		}
	}
}
