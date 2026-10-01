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
using System.Numerics;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>One cell of the sidebar palette.</summary>
	public sealed class PaletteItem
	{
		public string Collection;
		public string Icon;
		public string CostText = "";
		public Func<string> GetTooltip = () => "";
		public Func<bool> IsDisabled = () => false;
		public Func<bool> IsAffordable = () => true;
		public Func<bool> IsActive = () => false;
		public Action OnClick = () => { };
	}

	/// <summary>
	/// A Dune 2000 style icon palette: rows of icon cells (3 per row) on the sidebar row background.
	/// Draws the row backgrounds itself so empty rows keep the sidebar shape. The sidebar layout sets its height;
	/// when the items need more rows than fit, the palette scrolls by whole rows (mouse wheel, or clicking the
	/// scroll bar in the right frame).
	/// </summary>
	public class CityPaletteWidget : Widget
	{
		public readonly string TooltipContainer;
		public readonly string TooltipTemplate = "CITY_TOOLTIP";
		public readonly int2 IconSize = new(58, 48);
		public readonly int2 IconMargin = new(2, 0);
		public readonly int IconX = 39;
		public readonly int Columns = 3;
		public readonly int ScrollBarWidth = 4;
		public readonly string RowCollection = "sidebar";
		public readonly string RowImage = "background-iconrow";
		public readonly string Font = "Small";

		readonly ModData modData;
		readonly Lazy<TooltipContainerWidget> tooltipContainer;
		readonly List<PaletteItem> items = [];
		int hover = -1;
		int pressed = -1;
		int scrollRow;

		[ObjectCreator.UseCtor]
		public CityPaletteWidget(ModData modData)
		{
			this.modData = modData;
			tooltipContainer = Exts.Lazy(() => Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));
		}

		public void SetItems(IEnumerable<PaletteItem> newItems)
		{
			items.Clear();
			items.AddRange(newItems);
			hover = pressed = -1;
			scrollRow = 0;
			RemoveTooltip();
		}

		public IReadOnlyList<PaletteItem> Items => items;

		int TotalRows => (items.Count + Columns - 1) / Columns;

		/// <summary>Rows that fit completely in the palette's height.</summary>
		int VisibleRows => Math.Max(1, Bounds.Height / IconSize.Y);

		int MaxScroll => Math.Max(0, TotalRows - VisibleRows);

		bool CanScroll => MaxScroll > 0;

		Rectangle CellRect(int index)
		{
			var rb = RenderBounds;
			var column = index % Columns;
			var row = index / Columns - scrollRow;
			return new Rectangle(
				rb.X + IconX + column * (IconSize.X + IconMargin.X),
				rb.Y + row * IconSize.Y,
				IconSize.X, IconSize.Y);
		}

		bool IsShown(int index)
		{
			var row = index / Columns - scrollRow;
			return row >= 0 && row < VisibleRows;
		}

		int ItemAt(int2 location)
		{
			for (var i = 0; i < items.Count; i++)
				if (IsShown(i) && CellRect(i).Contains(location))
					return i;

			return -1;
		}

		/// <summary>The scroll bar sits in the sidebar's right frame, next to the last icon column.</summary>
		Rectangle ScrollTrack
		{
			get
			{
				var rb = RenderBounds;
				var x = rb.X + IconX + Columns * (IconSize.X + IconMargin.X);
				return new Rectangle(x, rb.Y + 2, ScrollBarWidth, VisibleRows * IconSize.Y - 4);
			}
		}

		void ScrollBy(int rows)
		{
			var next = Math.Clamp(scrollRow + rows, 0, MaxScroll);
			if (next == scrollRow)
				return;

			scrollRow = next;
			hover = -1;
			RemoveTooltip();
		}

		public override void Tick()
		{
			// The layout may have grown the palette: never leave empty rows below a scrolled list.
			scrollRow = Math.Clamp(scrollRow, 0, MaxScroll);
		}

		void RemoveTooltip()
		{
			if (TooltipContainer != null && tooltipContainer.IsValueCreated)
				tooltipContainer.Value.RemoveTooltip();
		}

		void SetHover(int index)
		{
			if (index == hover)
				return;

			hover = index;
			RemoveTooltip();
			if (index >= 0 && TooltipContainer != null)
			{
				var item = items[index];
				tooltipContainer.Value.SetTooltip(TooltipTemplate, new WidgetArgs { { "getText", item.GetTooltip } });
			}
		}

		public override void MouseExited()
		{
			hover = -1;
			RemoveTooltip();
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			if (mi.Event == MouseInputEvent.Scroll)
			{
				if (!CanScroll)
					return false;

				ScrollBy(mi.Delta.Y > 0 ? -1 : 1);
				return true;
			}

			var index = ItemAt(mi.Location);
			if (mi.Event == MouseInputEvent.Move)
			{
				SetHover(index);
				return false;
			}

			if (mi.Button != MouseButton.Left)
				return false;

			if (mi.Event == MouseInputEvent.Down && CanScroll && index < 0)
			{
				var track = ScrollTrack;
				var hit = new Rectangle(track.X - 2, track.Y, track.Width + 4, track.Height);
				if (hit.Contains(mi.Location))
				{
					ScrollBy(mi.Location.Y < ThumbRect().Y ? -VisibleRows : VisibleRows);
					return true;
				}
			}

			if (mi.Event == MouseInputEvent.Down)
			{
				pressed = index;
				return index >= 0;
			}

			if (mi.Event == MouseInputEvent.Up)
			{
				var wasPressed = pressed;
				pressed = -1;
				if (index >= 0 && index == wasPressed)
				{
					var item = items[index];
					var sound = item.IsDisabled() ? ChromeMetrics.Get<string>("ClickDisabledSound") : ChromeMetrics.Get<string>("ClickSound");
					Game.Sound.PlayNotification(modData.DefaultRules, null, "Sounds", sound, null);
					if (!item.IsDisabled())
						item.OnClick();

					return true;
				}
			}

			return false;
		}

		Rectangle ThumbRect()
		{
			var track = ScrollTrack;
			var total = Math.Max(1, TotalRows);
			var height = Math.Max(8, track.Height * VisibleRows / total);
			var y = track.Y + (track.Height - height) * scrollRow / Math.Max(1, MaxScroll);
			return new Rectangle(track.X, y, track.Width, height);
		}

		public override void Draw()
		{
			var rb = RenderBounds;
			var rowSprite = ChromeProvider.GetImage(RowCollection, RowImage);

			// Row backgrounds fill the whole height; a partial last row is clipped at the palette's bottom edge.
			Game.Renderer.EnableScissor(rb);
			var rows = (rb.Height + IconSize.Y - 1) / IconSize.Y;
			for (var r = 0; r < rows; r++)
				WidgetUtils.DrawSprite(rowSprite, new Vector2(rb.X, rb.Y + r * IconSize.Y));

			var font = Game.Renderer.Fonts[Font];
			for (var i = 0; i < items.Count; i++)
			{
				if (!IsShown(i))
					continue;

				var item = items[i];
				var cell = CellRect(i);
				var disabled = item.IsDisabled();
				var affordable = item.IsAffordable();

				var sprite = ChromeProvider.TryGetImage(item.Collection, item.Icon);
				if (sprite != null)
				{
					var size = sprite.Size;
					WidgetUtils.DrawSprite(sprite, new Vector2(
						cell.X + (int)(cell.Width - size.X) / 2,
						cell.Y + (int)(cell.Height - size.Y) / 2 - 2));
				}

				if (disabled)
					WidgetUtils.FillRectWithColor(cell, Color.FromArgb(160, 0, 0, 0));
				else if (!affordable)
					WidgetUtils.FillRectWithColor(cell, Color.FromArgb(90, 0, 0, 0));

				if (item.CostText.Length > 0)
				{
					var textColor = disabled ? Color.FromArgb(0xB0, 0xB0, 0xB0) : affordable ? Color.White : CityUi.Bad;
					var textSize = font.Measure(item.CostText);

					// A dark chip behind the price keeps it readable on any icon.
					var chip = new Rectangle(cell.X + (cell.Width - textSize.X) / 2 - 2, cell.Bottom - textSize.Y - 1, textSize.X + 4, textSize.Y);
					WidgetUtils.FillRectWithColor(chip, Color.FromArgb(170, 0, 0, 0));
					font.DrawText(item.CostText, new Vector2(chip.X + 2, chip.Y - 1), textColor);
				}

				if (item.IsActive())
					WidgetUtils.DrawFrame(cell, Color.FromArgb(0xFF, 0xA0, 0x20), 2);
				else if (i == hover && !disabled)
					WidgetUtils.DrawFrame(cell, Color.FromArgb(200, 0xFF, 0xE0, 0xA0), 1);
			}

			Game.Renderer.DisableScissor();

			if (CanScroll)
			{
				WidgetUtils.FillRectWithColor(ScrollTrack, Color.FromArgb(200, 0, 0, 0));
				WidgetUtils.FillRectWithColor(ThumbRect(), CityUi.Accent);
			}
		}
	}
}
