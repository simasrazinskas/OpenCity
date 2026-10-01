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
using System.Runtime.CompilerServices;
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.City
{
	/// <summary>Local (unsynced) state of the tool palette: the selected road type and road modes.</summary>
	public sealed class UiToolState
	{
		static readonly ConditionalWeakTable<World, UiToolState> Cache = [];

		/// <summary>Type and modes of the road tool (NET's RoadToolOptions: type id, one-way, reverse, paired, replace).</summary>
		public RoadToolOptions Road = new() { TypeId = 1 };

		public static UiToolState For(World world)
		{
			if (!Cache.TryGetValue(world, out var state))
			{
				state = new UiToolState();
				Cache.Add(world, state);
			}

			return state;
		}
	}

	/// <summary>An order generator that wants to see key presses the toolbar forwards (Enter, Backspace, Tab, ...).</summary>
	public interface IUiKeyTool
	{
		bool HandleKey(KeyInput e);
	}

	/// <summary>Drag a rectangle (hub areas, district painting) and issue one order for it.</summary>
	public sealed class UiAreaToolGenerator : CityDragOrderGenerator
	{
		readonly Func<Player, CPos, CPos, Order> factory;
		readonly Color color;
		readonly Func<CPos, CPos, string> label;

		public UiAreaToolGenerator(World world, Func<Player, CPos, CPos, Order> factory, Color color, Func<CPos, CPos, string> label = null)
			: base(world)
		{
			this.factory = factory;
			this.color = color;
			this.label = label;
		}

		protected override IEnumerable<Order> OnDragComplete(World w, CPos start, CPos end)
		{
			var player = w.LocalPlayer;
			if (player != null)
				yield return factory(player, start, end);
		}

		protected override IEnumerable<IRenderable> RenderPreview(WorldRenderer wr, World w)
		{
			if (!w.Map.Contains(HoverCell))
				yield break;

			foreach (var cell in CityUtils.Rect(PreviewStart, PreviewEnd))
				yield return Marker(cell, color);
		}

		protected override IEnumerable<IRenderable> RenderPreviewAnnotations(WorldRenderer wr, World w)
		{
			if (!IsDragging || label == null)
				yield break;

			yield return Label(w, PreviewEnd, label(PreviewStart, PreviewEnd), Color.White);
		}

		protected override string GetCursorName(World w, CPos cell, int2 worldPixel, MouseInput mi) { return "default"; }
	}

	/// <summary>Single click tool (junction control, roundabout prefab, buy map tile): one order for the clicked cell.</summary>
	public sealed class UiClickToolGenerator : CityDragOrderGenerator
	{
		readonly Func<Player, CPos, Order> factory;
		readonly Func<CPos, IEnumerable<CPos>> footprint;
		readonly Func<CPos, (Color Color, string Label)> style;

		public UiClickToolGenerator(World world, Func<Player, CPos, Order> factory, Func<CPos, IEnumerable<CPos>> footprint = null,
			Func<CPos, (Color Color, string Label)> style = null)
			: base(world)
		{
			this.factory = factory;
			this.footprint = footprint ?? (cell => [cell]);
			this.style = style ?? (_ => (ValidColor, null));
		}

		protected override IEnumerable<Order> OnDragComplete(World w, CPos start, CPos end)
		{
			var player = w.LocalPlayer;
			var order = player != null ? factory(player, end) : null;
			if (order != null)
				yield return order;
		}

		protected override IEnumerable<IRenderable> RenderPreview(WorldRenderer wr, World w)
		{
			if (!w.Map.Contains(HoverCell))
				yield break;

			var color = style(HoverCell).Color;
			foreach (var cell in footprint(HoverCell))
				yield return Marker(cell, color);
		}

		protected override IEnumerable<IRenderable> RenderPreviewAnnotations(WorldRenderer wr, World w)
		{
			if (!w.Map.Contains(HoverCell))
				yield break;

			var text = style(HoverCell).Label;
			if (!string.IsNullOrEmpty(text))
				yield return Label(w, HoverCell, text, Color.White);
		}

		protected override string GetCursorName(World w, CPos cell, int2 worldPixel, MouseInput mi) { return "default"; }
	}
}
