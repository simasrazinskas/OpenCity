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
using OpenRA.Mods.City.Traits;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>Presents CityEconomy's fees, tax rates, loan and trade statistics to the budget and production panels.</summary>
	public sealed class EconomyUiAdapter : IEconomyUiSource
	{
		readonly CityEconomy economy;
		readonly CityManager manager;

		public EconomyUiAdapter(CityEconomy economy, CityManager manager)
		{
			this.economy = economy;
			this.manager = manager;
		}

		/// <summary>Only the fees the economy has (electricity, water, garbage) report a value; others return -1.</summary>
		public int GetFee(string key)
		{
			switch (key)
			{
				case "power": return economy.FeePercent(FeeKind.Power);
				case "water": return economy.FeePercent(FeeKind.Water);
				case "garbage": return economy.FeePercent(FeeKind.Garbage);
				default: return -1;
			}
		}

		public int GetTaxDetail(int kind, int index)
		{
			switch ((TaxDetailKind)kind)
			{
				case TaxDetailKind.Category: return manager?.GetTaxRate((ZoneCategory)index) ?? 0;
				case TaxDetailKind.Education: return economy.IncomeTaxRate((EducationLevel)index);
				default: return economy.ProfitTaxRate(ZoneCategory.Industrial, index);
			}
		}

		public int LoanPrincipal => economy.LoanPrincipal;
		public int LoanLimit => economy.LoanLimit;
		public int LoanInterestTenthsPercent => economy.LoanRateTenths;

		public ResourceStat GetResourceStat(int resourceId)
		{
			return new ResourceStat
			{
				Produced = Clamp(economy.TradeProducedLast(resourceId)),
				Consumed = Clamp(economy.TradeConsumedLast(resourceId)),
				Imported = Clamp(economy.TradeImportedLast(resourceId)),
				Exported = Clamp(economy.TradeExportedLast(resourceId))
			};
		}

		static int Clamp(long value) { return (int)Math.Clamp(value, int.MinValue, int.MaxValue); }
	}

	/// <summary>Presents the public transport lines and stops to the transit panel and the line tool.</summary>
	public sealed class TransitUiAdapter : ITransitUiSource
	{
		readonly TransitLayer layer;
		readonly World world;
		readonly List<TransitLineEntry> lines = [];
		int builtTick = -1;

		public TransitUiAdapter(World world, TransitLayer layer)
		{
			this.world = world;
			this.layer = layer;
		}

		public IReadOnlyList<TransitLineEntry> Lines
		{
			get
			{
				if (builtTick == world.WorldTick)
					return lines;

				builtTick = world.WorldTick;
				lines.Clear();
				foreach (var line in layer.Lines)
				{
					var stats = line.ThisMonth;
					var waiting = 0;
					foreach (var id in line.StopIds)
						waiting += layer.GetStop(id)?.WaitingCount ?? 0;

					var color = TransitLayer.LineColor(line.Color);
					lines.Add(new TransitLineEntry
					{
						Id = line.Id,
						Name = string.IsNullOrEmpty(line.Name) ? FluentProvider.GetMessage("label-transit-line-name", "id", line.Id) : line.Name,
						Mode = TransitLayer.ModeName(line.Mode),
						ArgbColor = (color.R << 16) | (color.G << 8) | color.B,
						Stops = line.StopCount,
						Vehicles = line.Vehicles.Count,
						Usage = line.LastMonth.LoadSamples > 0 ? line.LastMonth.UsagePercent : stats.UsagePercent,
						PassengersThisMonth = stats.Passengers,
						RevenueMonth = stats.FareCents / 100,
						CostMonth = stats.CostCents / 100,
						WaitingNow = waiting,
						TicketCents = line.TicketCents,
						MaxTicketCents = layer.Info.MaxTicketCents,
						VehicleTarget = line.TargetVehicles
					});
				}

				return lines;
			}
		}

		public int StopAt(CPos cell) { return layer.StopAtAny(cell)?.Id ?? 0; }
	}
}
