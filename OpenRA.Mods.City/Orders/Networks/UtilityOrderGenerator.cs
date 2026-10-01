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
	/// Drag tool for HV power lines (kind 0) and water / sewage pipes (kind 1 water, 2 sewage, 3 both). With remove = true the
	/// drag removes them (and shows the refund). The preview uses <see cref="UtilityPlanner"/>, the code the order handler runs.
	/// </summary>
	public class UtilityOrderGenerator : CityDragOrderGenerator
	{
		readonly World world;
		readonly UtilityNetwork net;
		readonly CityManager cityManager;
		readonly int kind;
		readonly bool remove;

		UtilityPlan cachedPlan;
		CPos cachedFrom;
		CPos cachedTo;
		int cachedTick = -1;

		public UtilityOrderGenerator(World world, int kind, bool remove = false)
			: base(world)
		{
			this.world = world;
			this.kind = kind;
			this.remove = remove;
			net = world.WorldActor.TraitOrDefault<UtilityNetwork>();
			cityManager = world.LocalPlayer?.PlayerActor.TraitOrDefault<CityManager>();
		}

		public UtilityPlan CurrentPlan => Plan();

		UtilityPlan Plan()
		{
			var from = PreviewStart;
			var to = PreviewEnd;
			if (cachedPlan == null || cachedFrom != from || cachedTo != to || cachedTick != world.WorldTick)
			{
				cachedPlan = UtilityPlanner.Plan(world, net, cityManager, from, to, kind, remove);
				cachedFrom = from;
				cachedTo = to;
				cachedTick = world.WorldTick;
			}

			return cachedPlan;
		}

		protected override IEnumerable<Order> OnDragComplete(World w, CPos start, CPos end)
		{
			var player = w.LocalPlayer;
			if (player == null || net == null)
				yield break;

			yield return kind == 0 ? NetworkOrders.PowerLine(player, start, end, remove) : NetworkOrders.Pipe(player, start, end, kind, remove);
		}

		protected override IEnumerable<IRenderable> RenderPreview(WorldRenderer wr, World w)
		{
			if (net == null || !w.Map.Contains(HoverCell))
				yield break;

			var plan = Plan();
			for (var i = 0; i < plan.Path.Count; i++)
			{
				var c = plan.Path[i];
				if (i >= plan.StopIndex)
					yield return Marker(c, InvalidColor);
				else if (plan.Cells.Contains(c))
					yield return Marker(c, remove ? RemoveColor : ValidColor);
				else
					yield return Marker(c, NeutralColor);
			}
		}

		protected override IEnumerable<IRenderable> RenderPreviewAnnotations(WorldRenderer wr, World w)
		{
			if (net == null || !w.Map.Contains(HoverCell))
				yield break;

			var plan = Plan();
			if (plan.Cells.Count == 0 && plan.ErrorKey == null)
				yield break;

			string text;
			text = CityUtils.FormatMoney(plan.Cost < 0 ? -plan.Cost : plan.Cost);
			if (plan.Cost < 0)
				text = "+" + text;

			if (plan.ErrorKey != null)
				text += " (" + FluentProvider.GetMessage(plan.ErrorKey) + ")";

			yield return Label(w, PreviewEnd, text, plan.ErrorKey != null ? Color.OrangeRed : Color.White);
		}

		protected override string GetCursorName(World w, CPos cell, int2 worldPixel, MouseInput mi)
		{
			return ConstructionUtils.Cursor(remove ? "city-bulldoze" : "city-road", "default");
		}
	}
}
