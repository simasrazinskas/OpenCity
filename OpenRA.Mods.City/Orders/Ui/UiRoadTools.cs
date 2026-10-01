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
		readonly TransitLayer layer;
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
			layer = CityUiContext.For(world).Get<TransitLayer>();
		}

		protected override IEnumerable<Order> OnDragComplete(World w, CPos start, CPos end)
		{
			if (source == null)
				yield break;

			var stop = ResolveStop(end);
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
				stopCells.Add(CellOf(stop, end));
			}
		}

		/// <summary>
		/// The stop a click selects. Road modes (bus, tram) use the stop of that mode on the clicked road cell; station modes
		/// (metro, train) are picked by clicking the station building (the stop sits on its access road) or the stop's cell.
		/// </summary>
		int ResolveStop(CPos cell)
		{
			if (layer == null)
				return source?.StopAt(cell) ?? 0;

			if (mode == TransitMode.Metro || mode == TransitMode.Train)
			{
				foreach (var actor in world.ActorMap.GetActorsAt(cell))
					foreach (var stop in layer.Stops)
						if (stop.Mode == mode && stop.StationActorId == actor.ActorID)
							return stop.Id;

				foreach (var stop in layer.Stops)
					if (stop.Mode == mode && stop.Cell == cell)
						return stop.Id;

				return 0;
			}

			return layer.StopAt(cell, mode)?.Id ?? 0;
		}

		/// <summary>The map cell of a stop (a metro stop's cell is its station's access road).</summary>
		CPos CellOf(int stopId, CPos clicked)
		{
			var stop = layer?.GetStop(stopId);
			return stop != null ? stop.Cell : clicked;
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

		List<CPos> preview = [];
		bool previewBroken;
		int previewCycle;
		string previewKey;

		/// <summary>Route of the stops clicked so far: the transit planner's own path when it offers one, else straight L paths.</summary>
		void RefreshPreview()
		{
			var key = string.Join(",", stops);
			if (key == previewKey)
				return;

			previewKey = key;
			previewBroken = false;
			previewCycle = 0;
			preview = [];
			if (stops.Count < 2)
				return;

			var ex = CityUiContext.For(world).Get<ITransitUiSourceEx>();
			if (ex != null)
			{
				preview = ex.PreviewLine(TransitLayer.ModeName(mode), stops, false, out previewBroken, out previewCycle) ?? [];
				if (preview.Count > 0)
					return;
			}

			for (var i = 1; i < stopCells.Count; i++)
				preview.AddRange(CityUtils.RoadPath(stopCells[i - 1], stopCells[i]));
		}

		protected override IEnumerable<IRenderable> RenderPreview(WorldRenderer wr, World w)
		{
			RefreshPreview();
			var color = previewBroken ? InvalidColor : Color.FromArgb(150, lineColor);
			foreach (var cell in preview)
				yield return Marker(cell, color);

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

			RefreshPreview();
			var text = FluentProvider.GetMessage("label-transit-line-preview", "stops", stopCells.Count, "cells", preview.Count);
			if (previewCycle > 0)
				text += "  " + FluentProvider.GetMessage("label-transit-line-cycle", "seconds", previewCycle * 40 / 1000);

			if (previewBroken)
				text += "  " + FluentProvider.GetMessage("label-transit-line-broken");

			yield return Label(w, HoverCell, text, previewBroken ? Color.OrangeRed : Color.White);
		}

		protected override string GetCursorName(World w, CPos cell, int2 worldPixel, MouseInput mi)
		{
			return ResolveStop(cell) != 0 ? "default" : "generic-blocked";
		}
	}

	/// <summary>
	/// Tram track (on existing roads) and rail track drag, previewed with the transit planner's own plan (ITransitUiSourceEx.PlanTrack):
	/// green cells get built, level crossings are amber, the cells after the error are red, the label shows cost and error.
	/// </summary>
	public sealed class UiTrackToolGenerator : CityDragOrderGenerator
	{
		static readonly Color CrossingColor = Color.FromArgb(150, 255, 190, 40);

		readonly ITransitUiSourceEx source;
		readonly string kind;
		readonly bool remove;

		public UiTrackToolGenerator(World world, string kind, bool remove)
			: base(world)
		{
			this.kind = kind;
			this.remove = remove;
			source = CityUiContext.For(world).Get<ITransitUiSourceEx>();
		}

		protected override IEnumerable<Order> OnDragComplete(World w, CPos start, CPos end)
		{
			var player = w.LocalPlayer;
			if (player != null)
				yield return TransitOrders.BuildTrackOrder(player, kind, start, end, remove);
		}

		TrackPlan Plan()
		{
			return source?.PlanTrack(kind, PreviewStart, PreviewEnd);
		}

		protected override IEnumerable<IRenderable> RenderPreview(WorldRenderer wr, World w)
		{
			if (!w.Map.Contains(HoverCell))
				yield break;

			if (remove || source == null)
			{
				foreach (var cell in CityUtils.RoadPath(PreviewStart, PreviewEnd))
					yield return Marker(cell, RemoveColor);

				yield break;
			}

			var plan = Plan();
			var build = new HashSet<CPos>(plan.Build);
			var crossings = new HashSet<CPos>(plan.Crossings);
			foreach (var cell in plan.Path)
			{
				var color = crossings.Contains(cell) ? CrossingColor : build.Contains(cell) ? ValidColor : plan.ErrorKey != null ? InvalidColor : NeutralColor;
				yield return Marker(cell, color);
			}
		}

		protected override IEnumerable<IRenderable> RenderPreviewAnnotations(WorldRenderer wr, World w)
		{
			if (remove || source == null || !w.Map.Contains(HoverCell))
				yield break;

			var plan = Plan();
			var text = CityUtils.FormatMoney(plan.Cost);
			if (plan.ErrorKey != null)
				text += " (" + CityUi.Message(plan.ErrorKey) + ")";

			yield return Label(w, PreviewEnd, text, plan.ErrorKey != null ? Color.OrangeRed : Color.White);
		}

		protected override string GetCursorName(World w, CPos cell, int2 worldPixel, MouseInput mi) { return "default"; }
	}
}
