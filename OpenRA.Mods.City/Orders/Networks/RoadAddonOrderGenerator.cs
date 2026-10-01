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
using OpenRA.Graphics;
using OpenRA.Mods.City.Traits;
using OpenRA.Primitives;

namespace OpenRA.Mods.City
{
	/// <summary>
	/// Drag tool for road add-ons: drag along roads to add (or remove) trees, sound barriers, street lights, parking bays,
	/// bus lanes or bike lanes. The preview uses <see cref="RoadAddonPlanner"/>, the code the order handler runs.
	/// </summary>
	public class RoadAddonOrderGenerator : CityDragOrderGenerator
	{
		readonly World world;
		readonly RoadLayer roads;
		readonly CityManager cityManager;
		readonly RoadAddons addon;
		readonly bool remove;

		RoadAddonPlan cachedPlan;
		CPos cachedFrom;
		CPos cachedTo;
		int cachedTick = -1;

		public RoadAddonOrderGenerator(World world, RoadAddons addon, bool remove = false)
			: base(world)
		{
			this.world = world;
			this.addon = addon;
			this.remove = remove;
			roads = world.WorldActor.TraitOrDefault<RoadLayer>();
			cityManager = world.LocalPlayer?.PlayerActor.TraitOrDefault<CityManager>();
		}

		public RoadAddonPlan CurrentPlan => Plan();

		RoadAddonPlan Plan()
		{
			var from = PreviewStart;
			var to = PreviewEnd;
			if (cachedPlan == null || cachedFrom != from || cachedTo != to || cachedTick != world.WorldTick)
			{
				cachedPlan = RoadAddonPlanner.Plan(world, roads, cityManager, from, to, addon, remove);
				cachedFrom = from;
				cachedTo = to;
				cachedTick = world.WorldTick;
			}

			return cachedPlan;
		}

		protected override IEnumerable<Order> OnDragComplete(World w, CPos start, CPos end)
		{
			var player = w.LocalPlayer;
			if (player != null && roads != null)
				yield return NetworkOrders.RoadAddonOrder(player, start, end, addon, remove);
		}

		protected override IEnumerable<IRenderable> RenderPreview(WorldRenderer wr, World w)
		{
			if (roads == null || !w.Map.Contains(HoverCell))
				yield break;

			var plan = Plan();
			foreach (var c in plan.Path)
			{
				if (plan.Cells.Contains(c))
					yield return Marker(c, remove ? RemoveColor : ValidColor);
				else if (roads.IsRoad(c))
					yield return Marker(c, NeutralColor);
			}
		}

		protected override IEnumerable<IRenderable> RenderPreviewAnnotations(WorldRenderer wr, World w)
		{
			if (roads == null || !w.Map.Contains(HoverCell))
				yield break;

			var plan = Plan();
			if (plan.Cells.Count == 0 && plan.ErrorKey == null)
				yield break;

			var text = CityUtils.FormatMoney(plan.Cost < 0 ? -plan.Cost : plan.Cost);
			if (plan.Cost < 0)
				text = "+" + text;

			if (plan.ErrorKey != null)
				text += " (" + FluentProvider.GetMessage(plan.ErrorKey) + ")";

			yield return Label(w, PreviewEnd, text, plan.ErrorKey != null ? Color.OrangeRed : Color.White);
		}

		protected override string GetCursorName(World w, CPos cell, int2 worldPixel, MouseInput mi)
		{
			return roads != null && roads.IsRoad(cell)
				? ConstructionUtils.Cursor(remove ? "city-bulldoze" : "city-road", "default")
				: ConstructionUtils.Cursor("city-blocked", "generic-blocked");
		}
	}
}
