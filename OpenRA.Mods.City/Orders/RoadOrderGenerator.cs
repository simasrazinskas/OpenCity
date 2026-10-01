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
using System.Numerics;
using OpenRA.Graphics;
using OpenRA.Mods.City.Traits;
using OpenRA.Primitives;

namespace OpenRA.Mods.City
{
	/// <summary>
	/// Road tool: drag to draw an L-shaped road of the selected type and modes (<see cref="RoadToolOptions"/>: one-way, reverse, paired, replace). The preview uses the same planning code as the order handler
	/// (<see cref="RoadPlanner.Plan"/>): green cells will be built, grey cells are existing roads, red cells will
	/// not be built (blocked or unaffordable). Cursor: "city-road" (falls back to "default"), "city-blocked" when the cursor cell is bad.
	/// </summary>
	public class RoadOrderGenerator : CityDragOrderGenerator
	{
		public static readonly Color ReplaceColor = Color.FromArgb(120, 70, 150, 255);

		readonly World world;
		readonly RoadLayer roads;
		readonly CityManager cityManager;

		RoadPlan cachedPlan;
		CPos cachedFrom;
		CPos cachedTo;
		int cachedNetworkVersion = -1;
		int cachedFunds = -1;
		int cachedTick = -1;
		RoadToolOptions cachedOptions;

		/// <summary>Selected type and modes. The UI may change it while the tool is active (Tab flips <c>Reverse</c> of a one-way drag).</summary>
		public RoadToolOptions Options;

		/// <summary>Legacy constructor: plain two-way streets, existing roads are kept.</summary>
		public RoadOrderGenerator(World world)
			: this(world, RoadToolOptions.Default) { }

		public RoadOrderGenerator(World world, RoadToolOptions options)
			: base(world)
		{
			this.world = world;
			Options = options;
			roads = world.WorldActor.TraitOrDefault<RoadLayer>();
			cityManager = world.LocalPlayer?.PlayerActor.TraitOrDefault<CityManager>();
		}

		/// <summary>The plan the preview shows (cached per tick). UI uses it for cost and type read-outs.</summary>
		public RoadPlan CurrentPlan => Plan();

		RoadPlan Plan()
		{
			var from = PreviewStart;
			var to = PreviewEnd;
			var funds = cityManager?.Funds ?? 0;
			if (cachedPlan == null || cachedFrom != from || cachedTo != to || cachedNetworkVersion != roads.NetworkVersion
				|| cachedFunds != funds || cachedTick != world.WorldTick || !cachedOptions.Equals(Options))
			{
				cachedPlan = ConstructionUtils.PlanRoad(world, roads, cityManager, from, to, Options);
				cachedFrom = from;
				cachedTo = to;
				cachedNetworkVersion = roads.NetworkVersion;
				cachedFunds = funds;
				cachedTick = world.WorldTick;
				cachedOptions = Options;
			}

			return cachedPlan;
		}

		protected override IEnumerable<Order> OnDragComplete(World w, CPos start, CPos end)
		{
			var player = w.LocalPlayer;
			if (player == null || roads == null)
				yield break;

			yield return NetworkOrders.BuildRoad(player, start, end, Options);
		}

		protected override IEnumerable<IRenderable> RenderPreview(WorldRenderer wr, World w)
		{
			if (roads == null || !w.Map.Contains(HoverCell))
				yield break;

			var plan = Plan();
			var arrows = w.Map.Sequences.GetSequence(roads.Info.Image, roads.Info.ArrowSequence);
			var palette = wr.Palette(roads.Info.Palette);
			foreach (var e in plan.Entries)
			{
				switch (e.Action)
				{
					case RoadPlanAction.New:
						yield return Marker(e.Cell, ValidColor);
						break;
					case RoadPlanAction.Replace:
						yield return Marker(e.Cell, ReplaceColor);
						break;
					case RoadPlanAction.Keep:
						yield return Marker(e.Cell, NeutralColor);
						break;
					default:
						yield return Marker(e.Cell, InvalidColor);
						break;
				}

				if (e.OneWay >= 0 && e.Action != RoadPlanAction.Blocked && e.Action != RoadPlanAction.Keep)
				{
					var pos = w.Map.CenterOfCell(e.Cell);
					yield return new SpriteRenderable(arrows.GetSprite(e.OneWay), pos, WVec.Zero, 1, palette, arrows.Scale, 0.85f, Vector3.One, TintModifiers.None, false);
				}
			}
		}

		protected override IEnumerable<IRenderable> RenderPreviewAnnotations(WorldRenderer wr, World w)
		{
			if (roads == null || !w.Map.Contains(HoverCell))
				yield break;

			var plan = Plan();
			if (plan.Entries.Count == 0 || (plan.Cost == 0 && plan.ErrorKey == null))
				yield break;

			var text = CityUtils.FormatMoney(plan.Cost);
			if (plan.ErrorKey != null)
				text += " (" + FluentProvider.GetMessage(plan.ErrorKey) + ")";

			yield return Label(w, PreviewEnd, text, plan.ErrorKey != null ? Color.OrangeRed : Color.White);
		}

		protected override string GetCursorName(World w, CPos cell, int2 worldPixel, MouseInput mi)
		{
			if (roads == null || !w.Map.Contains(cell))
				return ConstructionUtils.Cursor("city-blocked", "generic-blocked");

			return roads.IsRoad(cell) || roads.CanBuildRoadAt(cell) || (Options.Bridge && roads.IsBridgeTerrain(cell))
				? ConstructionUtils.Cursor("city-road", "default")
				: ConstructionUtils.Cursor("city-blocked", "generic-blocked");
		}
	}
}
