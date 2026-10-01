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

		public InspectionAdapter(ServiceSimulation services)
		{
			this.services = services;
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

				if (!status.Active)
					rows.Add(Row("label-service-inactive", FluentProvider.GetMessage("label-service-inactive-value"), 3));
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

	/// <summary>Info view values from the industry and transit WPs: natural resources and the stop catchment of public transport.</summary>
	public sealed class WorldInfoViewSource : IInfoViewSource
	{
		readonly World world;
		readonly NaturalResourceLayer resources;
		readonly TransitLayer transit;
		int transitTick = -1;
		int[] transitReach = [];

		public WorldInfoViewSource(World world, NaturalResourceLayer resources, TransitLayer transit)
		{
			this.world = world;
			this.resources = resources;
			this.transit = transit;
		}

		public bool Supports(CityInfoView mode)
		{
			return (mode == CityInfoView.NaturalResources && resources != null) || (mode == CityInfoView.Transit && transit != null);
		}

		public int Version => (resources?.Version ?? 0) + (transit?.Version ?? 0);

		public int GetCell(CityInfoView mode, CPos cell)
		{
			if (mode == CityInfoView.NaturalResources)
			{
				var best = 0;
				foreach (var kind in new[] { NaturalResourceKind.Fertile, NaturalResourceKind.Forest, NaturalResourceKind.Ore, NaturalResourceKind.Oil })
					best = Math.Max(best, resources.GetRichnessPercent(kind, cell));

				return best > 0 ? best : -1;
			}

			return TransitReach(cell);
		}

		// Walking reach of every stop: 100 on the stop, falling to 0 at the stop's walk radius.
		int TransitReach(CPos cell)
		{
			var map = world.Map;
			if (!map.Contains(cell))
				return -1;

			var width = map.MapSize.Width;
			if (transitReach.Length != width * map.MapSize.Height)
				transitReach = new int[width * map.MapSize.Height];

			if (transitTick != transit.Version)
			{
				transitTick = transit.Version;
				Array.Clear(transitReach);
				var radius = Math.Max(1, transit.Info.WalkRadius);
				foreach (var stop in transit.Stops)
				{
					for (var dy = -radius; dy <= radius; dy++)
					{
						for (var dx = -radius; dx <= radius; dx++)
						{
							var distance = Math.Abs(dx) + Math.Abs(dy);
							if (distance > radius)
								continue;

							var c = stop.Cell + new CVec(dx, dy);
							if (!map.Contains(c))
								continue;

							var index = c.Y * width + c.X;
							transitReach[index] = Math.Max(transitReach[index], 100 - distance * 100 / radius);
						}
					}
				}
			}

			var value = transitReach[cell.Y * width + cell.X];
			return value > 0 ? value : -1;
		}
	}
}
