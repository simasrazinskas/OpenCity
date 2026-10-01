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
	/// Click tool for road prefabs: roundabouts (ring of one-way cells around an island) and highway ramps. The preview uses
	/// <see cref="RoadPrefabs.Plan"/>, the same code as the order handler. Cursor "city-road".
	/// </summary>
	public class RoadPrefabOrderGenerator : CityDragOrderGenerator
	{
		readonly World world;
		readonly RoadLayer roads;
		readonly CityManager cityManager;
		readonly string prefab;

		public RoadPrefabOrderGenerator(World world, string prefab)
			: base(world)
		{
			this.world = world;
			this.prefab = prefab;
			roads = world.WorldActor.TraitOrDefault<RoadLayer>();
			cityManager = world.LocalPlayer?.PlayerActor.TraitOrDefault<CityManager>();
		}

		public RoadPrefabPlan CurrentPlan => RoadPrefabs.Plan(world, roads, cityManager, HoverCell, prefab);

		protected override IEnumerable<Order> OnDragComplete(World w, CPos start, CPos end)
		{
			var player = w.LocalPlayer;
			if (player != null && roads != null)
				yield return NetworkOrders.PlaceRoadPrefabOrder(player, end, prefab);
		}

		protected override IEnumerable<IRenderable> RenderPreview(WorldRenderer wr, World w)
		{
			if (roads == null || !w.Map.Contains(HoverCell))
				yield break;

			var plan = CurrentPlan;
			var color = plan.Valid ? ValidColor : InvalidColor;
			foreach (var e in plan.Entries)
				yield return Marker(e.Cell, color);

			foreach (var c in plan.Island)
				yield return Marker(c, plan.Valid ? RemoveColor : InvalidColor);
		}

		protected override IEnumerable<IRenderable> RenderPreviewAnnotations(WorldRenderer wr, World w)
		{
			if (roads == null || !w.Map.Contains(HoverCell))
				yield break;

			var plan = CurrentPlan;
			var text = CityUtils.FormatMoney(plan.Cost - plan.Refund);
			if (plan.ErrorKey != null)
				text += " (" + FluentProvider.GetMessage(plan.ErrorKey) + ")";

			yield return Label(w, HoverCell, text, plan.ErrorKey != null ? Color.OrangeRed : Color.White);
		}

		protected override string GetCursorName(World w, CPos cell, int2 worldPixel, MouseInput mi)
		{
			return ConstructionUtils.Cursor("city-road", "default");
		}
	}

	/// <summary>Click tool that sets the junction control of the clicked road cell (null = back to the default).</summary>
	public class RoadControlOrderGenerator : CityDragOrderGenerator
	{
		readonly RoadLayer roads;
		readonly JunctionControl? value;

		public RoadControlOrderGenerator(World world, JunctionControl? value)
			: base(world)
		{
			this.value = value;
			roads = world.WorldActor.TraitOrDefault<RoadLayer>();
		}

		protected override IEnumerable<Order> OnDragComplete(World w, CPos start, CPos end)
		{
			var player = w.LocalPlayer;
			if (player != null && roads != null && roads.IsRoad(end))
				yield return NetworkOrders.SetControl(player, end, value);
		}

		protected override IEnumerable<IRenderable> RenderPreview(WorldRenderer wr, World w)
		{
			if (roads == null || !w.Map.Contains(HoverCell))
				yield break;

			yield return Marker(HoverCell, roads.IsJunction(HoverCell) ? ValidColor : InvalidColor);
		}

		protected override string GetCursorName(World w, CPos cell, int2 worldPixel, MouseInput mi)
		{
			return roads != null && roads.IsJunction(cell)
				? ConstructionUtils.Cursor("city-road", "default")
				: ConstructionUtils.Cursor("city-blocked", "generic-blocked");
		}
	}
}
