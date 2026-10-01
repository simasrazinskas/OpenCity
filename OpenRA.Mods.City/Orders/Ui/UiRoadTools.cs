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
using OpenRA.Mods.City.Widgets;
using OpenRA.Primitives;

namespace OpenRA.Mods.City
{
	/// <summary>
	/// Transit line tool (design 08 section 6): click the stops of the line in order, click the first stop again or press
	/// Enter to finish (a loop when the first stop was clicked again), Backspace removes the last stop, Escape or a right
	/// click cancels. The preview draws the stop-to-stop road paths in the line colour; the planner re-routes in the sim.
	/// </summary>
	public sealed class UiTransitLineGenerator : CityDragOrderGenerator, IUiKeyTool
	{
		readonly World world;
		readonly ITransitUiSource source;
		readonly TransitMode mode;
		readonly int colorIndex;
		readonly Color lineColor;
		readonly List<int> stops = [];
		readonly List<CPos> stopCells = [];

		public UiTransitLineGenerator(World world, TransitMode mode, int colorIndex)
			: base(world)
		{
			this.world = world;
			this.mode = mode;
			this.colorIndex = colorIndex;
			lineColor = TransitLayer.LineColor(colorIndex);
			source = CityUiContext.For(world).TransitUi;
		}

		protected override IEnumerable<Order> OnDragComplete(World w, CPos start, CPos end)
		{
			if (source == null)
				yield break;

			var stop = source.StopAt(end);
			if (stop == 0)
				yield break;

			if (stops.Count >= 2 && stop == stops[0])
			{
				Finish(true);
				yield break;
			}

			if (stops.Count == 0 || stops[^1] != stop)
			{
				stops.Add(stop);
				stopCells.Add(end);
			}
		}

		void Finish(bool loop)
		{
			var player = world.LocalPlayer;
			if (player != null && stops.Count >= 2)
				world.IssueOrder(UiOrders.CreateLine(player, mode, loop, stops, colorIndex));

			stops.Clear();
			stopCells.Clear();
		}

		bool IUiKeyTool.HandleKey(KeyInput e)
		{
			if (e.Event != KeyInputEvent.Down)
				return false;

			if (e.Key == Keycode.RETURN)
			{
				Finish(false);
				return true;
			}

			if (e.Key == Keycode.BACKSPACE && stops.Count > 0)
			{
				stops.RemoveAt(stops.Count - 1);
				stopCells.RemoveAt(stopCells.Count - 1);
				return true;
			}

			return false;
		}

		protected override IEnumerable<IRenderable> RenderPreview(WorldRenderer wr, World w)
		{
			for (var i = 1; i < stopCells.Count; i++)
				foreach (var cell in CityUtils.RoadPath(stopCells[i - 1], stopCells[i]))
					yield return Marker(cell, Color.FromArgb(150, lineColor));

			foreach (var cell in stopCells)
				yield return Marker(cell, Color.FromArgb(220, lineColor));

			if (stopCells.Count > 0 && w.Map.Contains(HoverCell))
				foreach (var cell in CityUtils.RoadPath(stopCells[^1], HoverCell))
					yield return Marker(cell, NeutralColor);
		}

		protected override IEnumerable<IRenderable> RenderPreviewAnnotations(WorldRenderer wr, World w)
		{
			if (stopCells.Count == 0 || !w.Map.Contains(HoverCell))
				yield break;

			var length = 0;
			for (var i = 1; i < stopCells.Count; i++)
				length += CityUtils.RoadPath(stopCells[i - 1], stopCells[i]).Count;

			yield return Label(w, HoverCell, FluentProvider.GetMessage("label-transit-line-preview", "stops", stopCells.Count, "cells", length), Color.White);
		}

		protected override string GetCursorName(World w, CPos cell, int2 worldPixel, MouseInput mi)
		{
			return source != null && source.StopAt(cell) != 0 ? "default" : "generic-blocked";
		}
	}
}
