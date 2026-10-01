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
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("Resolves the extractor area orders (CityExtractorArea, CityExtractorSetProduct, CityExtractorSetPolicy, design/06 3.2).",
		"Costs are paid through CityManager.TrySpend(amount, \"industry\"). Also exposes IExtractorSource for the player's economy.")]
	public class ExtractorToolInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new ExtractorTool(init.Self); }
	}

	public class ExtractorTool : IResolveOrder, IExtractorSource
	{
		readonly Actor self;

		public ExtractorTool(Actor self)
		{
			this.self = self;
		}

		ExtractorAreaLayer Areas => self.World.WorldActor.TraitOrDefault<ExtractorAreaLayer>();

		ExtractorHub OwnedHub(uint actorId)
		{
			var hub = Areas?.GetHubByActorId(actorId);
			return hub != null && !hub.Actor.Disposed && hub.Actor.Owner == self.Owner ? hub : null;
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			switch (order.OrderString)
			{
				case IndustryOrders.ExtractorArea:
					ResolveArea(order);
					break;
				case IndustryOrders.ExtractorSetProduct:
					ResolveProduct(order);
					break;
				case IndustryOrders.ExtractorSetPolicy:
					ResolvePolicy(order);
					break;
			}
		}

		void ResolveArea(Order order)
		{
			var layer = Areas;
			var hub = OwnedHub(order.ExtraData);
			if (layer == null || hub == null || order.Target.Type == TargetType.Invalid)
				return;

			var add = order.TargetString != "remove";
			var from = order.ExtraLocation;
			var to = self.World.Map.CellContaining(order.Target.CenterPosition);
			var plan = layer.Plan(hub, from, to, add);
			if (plan.IsEmpty)
				return;

			if (plan.Cost > 0)
			{
				var cm = self.TraitOrDefault<CityManager>();
				if (cm != null && !cm.TrySpend(plan.Cost, "industry"))
					return;
			}

			layer.Apply(plan);
		}

		void ResolveProduct(Order order)
		{
			var hub = OwnedHub(order.ExtraData);
			if (hub == null || !hub.CanProduce(order.TargetString))
				return;

			var wanted = hub.Info.Products.First(p => string.Equals(p, order.TargetString, StringComparison.OrdinalIgnoreCase));
			if (wanted == hub.Product)
				return;

			hub.Product = wanted;
			hub.RefreshCapacity();
			Areas?.NotifyChanged(hub);
		}

		void ResolvePolicy(Order order)
		{
			var hub = OwnedHub(order.ExtraData);
			if (hub == null)
				return;

			if (string.Equals(order.TargetString, "clearcut", StringComparison.OrdinalIgnoreCase) && hub.Info.Kind == NaturalResourceKind.Forest)
			{
				hub.ClearCut = order.ExtraLocation.X != 0;
				hub.RefreshCapacity();
			}
		}

		int IExtractorSource.Product(int propertyId) { return ((IExtractorSource)Areas)?.Product(propertyId) ?? 0; }

		int IExtractorSource.CapacityMilliPerDay(int propertyId) { return ((IExtractorSource)Areas)?.CapacityMilliPerDay(propertyId) ?? 0; }

		void IExtractorSource.OnProduced(int propertyId, int units) { ((IExtractorSource)Areas)?.OnProduced(propertyId, units); }
	}
}
