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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Orders;
using OpenRA.Primitives;

namespace OpenRA.Mods.City
{
	/// <summary>
	/// Shared base for the "press, drag, release" city tools (road, bulldoze, zone).
	/// <para>Input behaviour:</para>
	///  - Action button (left) down on a map cell starts a drag at that cell.
	///  - Move with the action button held updates <see cref="DragCurrent"/>.
	///  - Move without the button held while dragging aborts the drag (button was released over the chrome).
	///  - Action button up calls <see cref="OnDragComplete"/>; the returned orders are issued. The tool stays active
	///    (<see cref="StayActiveAfterDrag"/>) so the player can keep dragging.
	///  - Cancel button (right) up aborts a running drag, or leaves the tool if nothing is being dragged. Escape leaves the tool.
	/// <para>Subclasses implement <see cref="OnDragComplete"/>, <see cref="RenderPreview"/> / <see cref="RenderPreviewAnnotations"/>
	/// for the preview and <see cref="GetCursorName"/>. <see cref="PreviewStart"/> / <see cref="PreviewEnd"/> give the
	/// current drag corners, or the hovered cell for both while no drag is running.</para>
	/// </summary>
	public abstract class CityDragOrderGenerator : IOrderGenerator
	{
		public static readonly Color ValidColor = Color.FromArgb(110, 70, 220, 90);
		public static readonly Color InvalidColor = Color.FromArgb(120, 230, 60, 50);
		public static readonly Color NeutralColor = Color.FromArgb(70, 255, 255, 255);
		public static readonly Color RemoveColor = Color.FromArgb(120, 255, 150, 30);

		readonly GameSettings gameSettings = Game.Settings.Game;
		readonly World world;

		protected CityDragOrderGenerator(World world)
		{
			this.world = world;
			if (gameSettings.MouseControlStyle == MouseControlStyle.Classic)
				world.Selection.Clear();
		}

		public MouseButton ActionButton => gameSettings.ResolveActionButton(MouseActionType.PlaceBuilding);
		public MouseButton CancelButton => gameSettings.ResolveCancelButton(MouseActionType.PlaceBuilding);

		/// <summary>True between drag start and drag end / abort.</summary>
		protected bool IsDragging { get; private set; }

		/// <summary>Cell where the current drag started. Only meaningful while <see cref="IsDragging"/>.</summary>
		protected CPos DragStart { get; private set; }

		/// <summary>Cell the pointer is on during the current drag. Only meaningful while <see cref="IsDragging"/>.</summary>
		protected CPos DragCurrent { get; private set; }

		/// <summary>Cell under the pointer (clamped to the map), updated on every mouse move and every render.</summary>
		protected CPos HoverCell { get; private set; }

		/// <summary>First corner of the area to preview: the drag start, or the hovered cell when not dragging.</summary>
		protected CPos PreviewStart => IsDragging ? DragStart : HoverCell;

		/// <summary>Second corner of the area to preview: the drag position, or the hovered cell when not dragging.</summary>
		protected CPos PreviewEnd => IsDragging ? DragCurrent : HoverCell;

		/// <summary>Keep the tool active after a completed drag (default true).</summary>
		protected virtual bool StayActiveAfterDrag => true;

		/// <summary>Called when the player releases the button after a drag (a plain click gives start == end). Return the orders to issue.</summary>
		protected abstract IEnumerable<Order> OnDragComplete(World w, CPos start, CPos end);

		/// <summary>Cell highlights drawn above the terrain (use <see cref="Marker"/>).</summary>
		protected virtual IEnumerable<IRenderable> RenderPreview(WorldRenderer wr, World w) { yield break; }

		/// <summary>Text or other annotations (use <see cref="Label"/>).</summary>
		protected virtual IEnumerable<IRenderable> RenderPreviewAnnotations(WorldRenderer wr, World w) { yield break; }

		/// <summary>Cursor name for the current pointer position.</summary>
		protected abstract string GetCursorName(World w, CPos cell, int2 worldPixel, MouseInput mi);

		/// <summary>Called when the drag is aborted without completing (right click, button released elsewhere).</summary>
		protected virtual void OnDragAborted() { }

		protected virtual void Tick(World w) { }

		/// <summary>Starts a drag programmatically (tests, subclasses with different start semantics).</summary>
		protected void BeginDrag(CPos cell)
		{
			IsDragging = true;
			DragStart = cell;
			DragCurrent = cell;
		}

		protected void AbortDrag()
		{
			if (!IsDragging)
				return;

			IsDragging = false;
			OnDragAborted();
		}

		IEnumerable<Order> IOrderGenerator.Order(World w, CPos cell, int2 worldPixel, MouseInput mi)
		{
			HoverCell = w.Map.Contains(cell) ? cell : w.Map.Clamp(cell);

			if (mi.Button.HasFlag(CancelButton) && mi.Event == MouseInputEvent.Up)
			{
				if (IsDragging)
					AbortDrag();
				else
					w.CancelInputMode();

				return [];
			}

			if (mi.Event == MouseInputEvent.Down && mi.Button == ActionButton)
			{
				if (!IsDragging && w.Map.Contains(cell))
					BeginDrag(cell);

				return [];
			}

			if (!IsDragging)
				return [];

			if (mi.Event == MouseInputEvent.Move)
			{
				if (mi.Button.HasFlag(ActionButton))
					DragCurrent = HoverCell;
				else
					AbortDrag();

				return [];
			}

			if (mi.Event == MouseInputEvent.Up && mi.Button == ActionButton)
			{
				DragCurrent = HoverCell;
				var start = DragStart;
				var end = DragCurrent;
				IsDragging = false;

				var orders = new List<Order>(OnDragComplete(w, start, end) ?? []);
				if (!StayActiveAfterDrag)
					w.CancelInputMode();

				return orders;
			}

			return [];
		}

		bool IOrderGenerator.HandleKeyPress(KeyInput e)
		{
			if (e.Event == KeyInputEvent.Down && e.Key == Keycode.ESCAPE)
			{
				world.CancelInputMode();
				return true;
			}

			return false;
		}

		void IOrderGenerator.Tick(World w) { Tick(w); }

		IEnumerable<IRenderable> IOrderGenerator.Render(WorldRenderer wr, World w) { return []; }

		IEnumerable<IRenderable> IOrderGenerator.RenderAboveShroud(WorldRenderer wr, World w)
		{
			UpdateHover(wr, w);
			return RenderPreview(wr, w);
		}

		IEnumerable<IRenderable> IOrderGenerator.RenderAnnotations(WorldRenderer wr, World w)
		{
			UpdateHover(wr, w);
			return RenderPreviewAnnotations(wr, w);
		}

		string IOrderGenerator.GetCursor(World w, CPos cell, int2 worldPixel, MouseInput mi) { return GetCursorName(w, cell, worldPixel, mi); }

		void IOrderGenerator.Deactivate() { }

		void IOrderGenerator.SelectionChanged(World w, IEnumerable<Actor> selected) { }

		void UpdateHover(WorldRenderer wr, World w)
		{
			var cell = wr.Viewport.ViewToWorld(Viewport.LastMousePos);
			HoverCell = w.Map.Contains(cell) ? cell : w.Map.Clamp(cell);
			if (IsDragging)
				DragCurrent = HoverCell;
		}

		/// <summary>A translucent cell highlight for RenderPreview (world pass). Annotations must use MarkerTileRenderable instead.</summary>
		protected static IRenderable Marker(CPos cell, Color color)
		{
			return new CityTileMarkerRenderable(cell, color);
		}

		/// <summary>Highlights every cell of the rectangle spanned by two corners.</summary>
		protected static IEnumerable<IRenderable> MarkRect(CPos a, CPos b, Color color)
		{
			foreach (var c in CityUtils.Rect(a, b))
				yield return Marker(c, color);
		}

		/// <summary>A text label centred a little below the given cell (e.g. a cost readout).</summary>
		protected static IRenderable Label(World w, CPos cell, string text, Color color)
		{
			var font = Game.Renderer.Fonts["Bold"];
			var pos = w.Map.CenterOfCell(cell) + new WVec(0, 1024, 0);
			return new TextAnnotationRenderable(font, pos, 0, color, text);
		}

		/// <summary>Number of cells in the rectangle spanned by two corners.</summary>
		protected static int RectCellCount(CPos a, CPos b)
		{
			return (Math.Abs(a.X - b.X) + 1) * (Math.Abs(a.Y - b.Y) + 1);
		}
	}
}
