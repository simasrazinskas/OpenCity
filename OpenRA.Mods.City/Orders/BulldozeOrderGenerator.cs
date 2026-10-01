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
	/// Bulldoze tool: drag a rectangle. Everything that will be removed (roads, buildings touching the rectangle) is highlighted
	/// orange, the rest of the rectangle is shaded white. Shows the net money change. Cursor: "city-bulldoze" (falls back to "default").
	/// </summary>
	public class BulldozeOrderGenerator : CityDragOrderGenerator
	{
		readonly World world;
		readonly RoadLayer roads;

		BulldozePlan cachedPlan;
		CPos cachedFrom;
		CPos cachedTo;
		int cachedTick = -1;

		public BulldozeOrderGenerator(World world)
			: base(world)
		{
			this.world = world;
			roads = world.WorldActor.TraitOrDefault<RoadLayer>();
		}

		BulldozePlan Plan()
		{
			var from = PreviewStart;
			var to = PreviewEnd;
			if (cachedPlan == null || cachedFrom != from || cachedTo != to || cachedTick != world.WorldTick)
			{
				cachedPlan = ConstructionUtils.PlanBulldoze(world, roads, from, to);
				cachedFrom = from;
				cachedTo = to;
				cachedTick = world.WorldTick;
			}

			return cachedPlan;
		}

		protected override IEnumerable<Order> OnDragComplete(World w, CPos start, CPos end)
		{
			var player = w.LocalPlayer;
			if (player == null)
				yield break;

			yield return CityOrders.BulldozeOrder(player, start, end);
		}

		protected override IEnumerable<IRenderable> RenderPreview(WorldRenderer wr, World w)
		{
			if (!w.Map.Contains(HoverCell))
				yield break;

			var plan = Plan();
			var removed = new HashSet<CPos>(plan.RoadCells);
			foreach (var a in plan.Actors)
				foreach (var (cell, _) in a.OccupiesSpace.OccupiedCells())
					removed.Add(cell);

			foreach (var c in CityUtils.Rect(PreviewStart, PreviewEnd))
				if (w.Map.Contains(c) && !removed.Contains(c))
					yield return Marker(c, NeutralColor);

			foreach (var c in removed)
				yield return Marker(c, RemoveColor);
		}

		protected override IEnumerable<IRenderable> RenderPreviewAnnotations(WorldRenderer wr, World w)
		{
			if (!w.Map.Contains(HoverCell))
				yield break;

			var plan = Plan();
			var net = plan.Refund - plan.Cost;
			if (net == 0)
				yield break;

			var text = (net > 0 ? "+" : string.Empty) + CityUtils.FormatMoney(net);
			yield return Label(w, PreviewEnd, text, net > 0 ? Color.LightGreen : Color.White);
		}

		protected override string GetCursorName(World w, CPos cell, int2 worldPixel, MouseInput mi)
		{
			return ConstructionUtils.Cursor("city-bulldoze", "default");
		}
	}
}
