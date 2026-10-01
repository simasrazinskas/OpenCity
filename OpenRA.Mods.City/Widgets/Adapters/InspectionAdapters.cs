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
using System.Globalization;
using OpenRA.Mods.City.Traits;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>Extra building panel rows from the services WP (efficiency, satisfaction, capacity, fleet) and the industry WP (hubs).</summary>
	public sealed class InspectionAdapter : IInspectionContributor
	{
		readonly ServiceSimulation services;
		readonly Logistics logistics;
		readonly CityEconomy economy;
		readonly PropertyRegistry registry;

		public InspectionAdapter(ServiceSimulation services, Logistics logistics, CityEconomy economy, PropertyRegistry registry)
		{
			this.registry = registry;
			this.services = services;
			this.logistics = logistics;
			this.economy = economy;
		}

		static InspectionRow Row(string labelKey, string value, int tone, int bar = -1)
		{
			return new InspectionRow { Label = CityUi.Message(labelKey), Value = value, Tone = tone, BarPercent = bar };
		}

		static string Percent(int value) { return FluentProvider.GetMessage("label-city-percent", "value", value); }

		static string OfMax(int current, int max)
		{
			return FluentProvider.GetMessage("label-building-current-of-max", "current", current.ToString("N0", CultureInfo.CurrentCulture),
				"max", max.ToString("N0", CultureInfo.CurrentCulture));
		}

		static int Tone(int percent) { return percent >= 60 ? 1 : percent >= 35 ? 2 : 3; }

		public void Contribute(Actor building, Property property, List<InspectionRow> rows)
		{
			if (services != null && building.Info.TraitInfoOrDefault<ServiceBuildingInfo>() != null)
			{
				var status = services.GetStatus(building);
				rows.Add(Row("label-service-efficiency", Percent(status.Efficiency), Tone(Math.Min(100, status.Efficiency)), Math.Min(100, status.Efficiency)));
				rows.Add(Row("label-service-satisfaction", Percent(status.Satisfaction), Tone(status.Satisfaction), status.Satisfaction));
				if (status.Capacity > 0)
					rows.Add(Row("label-service-capacity", OfMax(status.Used, status.Capacity), Tone(100 - status.Used * 100 / Math.Max(1, status.Capacity))));

				if (status.Fleet > 0)
					rows.Add(Row("label-service-fleet", OfMax(status.FleetInUse, status.Fleet), 0));

				if (status.Kind == ServiceKind.Parks)
				{
					var condition = services.GetCondition(building);
					rows.Add(Row("label-service-condition", Percent(condition), Tone(condition), condition));
				}

				if (!status.Active)
					rows.Add(Row("label-service-inactive", FluentProvider.GetMessage("label-service-inactive-value"), 3));
			}

			// Warehouses and terminals: stock against capacity of the goods they hold.
			if (logistics != null && property != null && logistics.IsWarehouse(property.Id))
			{
				var shown = 0;
				foreach (var resource in logistics.WarehouseResources(property.Id))
				{
					if (shown++ >= 6)
						break;

					var name = economy != null ? economy.ResourceName(resource) : "#" + resource;
					var capacity = logistics.WarehouseCapacity(property.Id, resource);
					var stock = logistics.WarehouseStock(property.Id, resource);
					rows.Add(new InspectionRow
					{
						Label = CityUi.Message("label-resource-" + name.ToLowerInvariant(), name),
						Value = OfMax(stock, capacity),
						Tone = capacity > 0 && stock * 100 / capacity > 90 ? 2 : 0,
						BarPercent = capacity > 0 ? Math.Min(100, stock * 100 / capacity) : -1
					});
				}
			}

			// Lot facts of the zoning WP: corner lots, hotel rooms and storage space.
			if (registry != null && property != null)
			{
				if (registry.IsCorner(property.Id))
					rows.Add(Row("label-lot-corner", FluentProvider.GetMessage("label-lot-corner-value"), 1));

				var rooms = registry.GetLodgingRooms(property.Id);
				if (rooms > 0)
				{
					var guests = registry.GetGuests(property.Id);
					rows.Add(Row("label-lot-rooms", OfMax(guests, rooms), Tone(100 - guests * 100 / rooms), Math.Min(100, guests * 100 / rooms)));
				}

				var storage = registry.GetStorageCapacity(property.Id);
				if (storage > 0)
					rows.Add(Row("label-lot-storage", storage.ToString("N0", CultureInfo.CurrentCulture), 0));
			}

			var hub = building.TraitOrDefault<ExtractorHub>();
			if (hub != null)
			{
				var product = string.IsNullOrEmpty(hub.Product) ? "-" : CityUi.Message("label-resource-" + hub.Product.ToLowerInvariant(), hub.Product);
				rows.Add(Row("label-hub-product", product, 0));
				rows.Add(Row("label-hub-area", hub.Cells.Count.ToString(CultureInfo.CurrentCulture), hub.Cells.Count > 0 ? 0 : 2));
				rows.Add(Row("label-hub-output", Percent(hub.OutputPercent), Tone(hub.OutputPercent), hub.OutputPercent));
				rows.Add(Row("label-hub-capacity", FluentProvider.GetMessage("label-hub-capacity-value", "units", hub.CapMilliPerDay / 1000), 0));
				if (hub.MonthsLeft > 0 && hub.MonthsLeft < 1000)
					rows.Add(Row("label-hub-months-left", hub.MonthsLeft.ToString(CultureInfo.CurrentCulture), hub.MonthsLeft < 6 ? 3 : 0));
			}
		}
	}

	/// <summary>The in-place upgrades of service buildings (declared as ServiceUpgrade traits, bought with CityBuyUpgrade).</summary>
	public sealed class UpgradeAdapter : IUpgradeSource
	{
		readonly ServiceSimulation services;
		readonly List<UpgradeEntry> entries = [];

		public UpgradeAdapter(ServiceSimulation services)
		{
			this.services = services;
		}

		public IReadOnlyList<UpgradeEntry> UpgradesOf(Actor building)
		{
			entries.Clear();
			var index = 0;
			foreach (var upgrade in building.Info.TraitInfos<ServiceUpgradeInfo>())
			{
				entries.Add(new UpgradeEntry
				{
					NameKey = upgrade.Name,
					Cost = upgrade.Cost,
					UpkeepPerMonth = upgrade.Upkeep,
					Owned = services.HasUpgrade(building, index)
				});

				index++;
			}

			return entries;
		}
	}
}
