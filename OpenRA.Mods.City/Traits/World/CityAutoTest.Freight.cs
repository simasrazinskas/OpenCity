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

using System.Linq;

namespace OpenRA.Mods.City.Traits
{
	public partial class CityAutoTest
	{
		void ScheduleFreightScenario()
		{
			scheduled.Add((6100, "freight-watch", WatchFreight));
			scheduled.Add((6200, "freight-farm", AddFreightFarm));
			scheduled.Add((6500, "freight-product", MatchFreightProduct));
			scheduled.Add((6600, "freight-field", AddFreightField));
			foreach (var tick in new[] { 12000, 60000, 120000, 180000, 240000, 360000 })
				scheduled.Add((tick, "freight-inventory", ReportFreightInventory));
		}

		void WatchFreight(World w, Player p)
		{
			var logistics = p.PlayerActor.TraitOrDefault<Logistics>();
			if (logistics == null)
				return;

			logistics.Accepted += e => TraceFreight(w, "accepted", e);
			logistics.Delivered += e => TraceFreight(w, "arrived", e);
			logistics.Returned += e => TraceFreight(w, "returned", e);
		}

		static void TraceFreight(World w, string stage, ShipmentEvent e)
		{
			if (e.FromProperty != 0 && e.ToProperty != 0)
				Report(w, $"freight {stage} shipment={e.ShipmentId} from={e.FromProperty} to={e.ToProperty} resource={e.Resource} units={e.Units}");
		}

		void AddFreightFarm(World w, Player p)
		{
			w.IssueOrder(CityOrders.BuildRoadOrder(p, At(48, 0), At(55, 0)));
			w.IssueOrder(CityOrders.PlaceBuildingOrder(p, "farm-hub", At(50, 1)));
			w.IssueOrder(CityOrders.PlaceBuildingOrder(p, "windturbine", At(53, 1)));
			w.IssueOrder(CityOrders.PlaceBuildingOrder(p, "watertower", At(54, 1)));
		}

		Actor FreightFarm(World w, Player p) => w.Actors.FirstOrDefault(a => a.Owner == p && a.Info.Name == "farm-hub"
			&& !a.IsDead && a.Location == At(50, 1));

		void MatchFreightProduct(World w, Player p)
		{
			var farm = FreightFarm(w, p);
			var economy = p.PlayerActor.TraitOrDefault<CityEconomy>();
			var registry = w.WorldActor.TraitsImplementing<IPropertyRegistry>().FirstOrDefault();
			if (farm == null || economy == null || registry == null)
				return;

			// Choose a product before this new farm's first company step. Existing recipes, cash,
			// jobs, yield and physical stock stay under the ordinary simulation's control.
			var hub = farm.Trait<ExtractorHub>();
			var traffic = w.WorldActor.TraitsImplementing<ITrafficService>().FirstOrDefault();
			var property = registry.GetByActor(farm);
			foreach (var target in registry.All)
			{
				var buyer = economy.CompanyOf(target.Id);
				if (buyer == null || buyer.Kind != CompanyKind.Processor || buyer.Recipe < 0 || !target.HasRoadAccess
					|| property == null || !property.HasRoadAccess)
					continue;

				var travel = traffic?.EstimateTravelTicks(property.AccessRoad, target.AccessRoad, TravelMode.Truck) ?? -1;
				if (travel < 0)
					continue;

				foreach (var resource in economy.Tables.Recipes[buyer.Recipe].InputRes)
				{
					var product = economy.ResourceName(resource);
					if (!hub.Info.Products.Contains(product))
						continue;

					w.IssueOrder(IndustryOrders.SetProductOrder(p, farm, product));
					Report(w, $"freight match farm={property.Id} processor={target.Id} product={product} travel={travel}");
					return;
				}
			}

			Report(w, "freight match unavailable: no reachable farm-input processor yet");
		}

		void AddFreightField(World w, Player p)
		{
			var farm = FreightFarm(w, p);
			if (farm != null)
				w.IssueOrder(IndustryOrders.AreaOrder(p, farm, At(45, 4), At(56, 9), true));
		}

		void ReportFreightInventory(World w, Player p)
		{
			var economy = p.PlayerActor.TraitOrDefault<CityEconomy>();
			var registry = w.WorldActor.TraitsImplementing<IPropertyRegistry>().FirstOrDefault();
			if (economy == null || registry == null)
				return;

			foreach (var property in registry.All)
			{
				var company = economy.CompanyOf(property.Id);
				if (company == null || company.Kind == CompanyKind.Office)
					continue;

				var incoming = Enumerable.Range(1, economy.ResourceCount).Sum(company.InboundMilli);
				Report(w, $"freight inventory property={property.Id} kind={company.Kind} product={economy.ResourceName(company.Output)}"
					+ $" workers={company.WorkersNow} input={string.Join('/', company.StockIn)} output={company.StockOut} incoming={incoming} powered={company.Powered}");
			}
		}
	}
}
