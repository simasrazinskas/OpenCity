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
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>One card of a build menu.</summary>
	public sealed class PaletteItem
	{
		/// <summary>RCT2 icon (tools/iso_ui_icons_*.py) shown when there is no thumbnail.</summary>
		public string Icon;

		/// <summary>Actor whose build-menu thumbnail is shown (bits/chrome/iso/thumbs-*.png), or null.</summary>
		public string Thumbnail;

		public string Name = "";
		public string CostText = "";
		public Func<string> GetTooltip = () => "";
		public Func<bool> IsDisabled = () => false;
		public Func<bool> IsAffordable = () => true;
		public Func<bool> IsActive = () => false;
		public Action OnClick = () => { };
	}

	/// <summary>
	/// RCT2 build menu (design/iso/ui/panels/build-menu-health.png, system/ds-cards.png): a grid of cards, each a sunken
	/// well with the building thumbnail (or the tool icon) above its name and price. States: normal, hover, selected
	/// (the active tool), locked (silhouette, muted text); unaffordable prices turn red. Scrolls by rows (mouse wheel or
	/// the scrollbar on the right) when the cards need more rows than fit.
	/// </summary>
	public class CityBuildMenuWidget : Widget
	{
		public const int CardWidth = 68;
		public const int CardHeight = 90;
		public const int Gap = 3;
		public const int ScrollBarWidth = 10;

		public readonly string TooltipContainer;
		public readonly string TooltipTemplate = "CITY_TOOLTIP";
		public readonly string Font = "Tiny";

		readonly ModData modData;
		readonly Lazy<TooltipContainerWidget> tooltipContainer;
		readonly List<PaletteItem> items = [];
		int pressed = -1;
		int scrollRow;

		/// <summary>The card under the mouse, or -1.</summary>
		public int Hovered { get; private set; } = -1;

		[ObjectCreator.UseCtor]
		public CityBuildMenuWidget(ModData modData)
		{
			this.modData = modData;
			tooltipContainer = Exts.Lazy(() => Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));
		}

		public void SetItems(IEnumerable<PaletteItem> newItems)
		{
			items.Clear();
			items.AddRange(newItems);
			Hovered = pressed = -1;
			scrollRow = 0;
			RemoveTooltip();
		}

		public IReadOnlyList<PaletteItem> Items => items;

		/// <summary>Cards per row for a given widget width (the scrollbar column is kept free).</summary>
		public static int ColumnsFor(int width) { return Math.Max(1, (width - ScrollBarWidth - 2 + Gap) / (CardWidth + Gap)); }

		/// <summary>The widget width that fits a number of columns (plus the scrollbar column).</summary>
		public static int WidthFor(int columns) { return columns * (CardWidth + Gap) - Gap + ScrollBarWidth + 2; }

		public static int HeightFor(int rows) { return rows * (CardHeight + Gap) - Gap; }

		int Columns => ColumnsFor(Bounds.Width);

		int TotalRows => (items.Count + Columns - 1) / Columns;

		int VisibleRows => Math.Max(1, (Bounds.Height + Gap) / (CardHeight + Gap));

		int MaxScroll => Math.Max(0, TotalRows - VisibleRows);

		bool CanScroll => MaxScroll > 0;

		Rectangle CellRect(int index)
		{
			var rb = RenderBounds;
			var column = index % Columns;
			var row = index / Columns - scrollRow;
			return new Rectangle(rb.X + column * (CardWidth + Gap), rb.Y + row * (CardHeight + Gap), CardWidth, CardHeight);
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

		Rectangle ScrollTrack
		{
			get
			{
				var rb = RenderBounds;
				return new Rectangle(rb.Right - ScrollBarWidth, rb.Y, ScrollBarWidth, Math.Min(rb.Height, HeightFor(VisibleRows)));
			}
		}

		void ScrollBy(int rows)
		{
			var next = Math.Clamp(scrollRow + rows, 0, MaxScroll);
			if (next == scrollRow)
				return;

			scrollRow = next;
			Hovered = -1;
			RemoveTooltip();
		}

		/// <summary>Scrolls so that the card of an item is visible.</summary>
		public void ScrollTo(int index)
		{
			if (index < 0 || index >= items.Count)
				return;

			var row = index / Columns;
			if (row < scrollRow)
				scrollRow = row;
			else if (row >= scrollRow + VisibleRows)
				scrollRow = row - VisibleRows + 1;

			scrollRow = Math.Clamp(scrollRow, 0, MaxScroll);
		}

		public override void Tick()
		{
			scrollRow = Math.Clamp(scrollRow, 0, MaxScroll);
		}

		void RemoveTooltip()
		{
			if (TooltipContainer != null && tooltipContainer.IsValueCreated)
				tooltipContainer.Value.RemoveTooltip();
		}

		void SetHover(int index)
		{
			if (index == Hovered)
				return;

			Hovered = index;
			RemoveTooltip();
			if (index >= 0 && TooltipContainer != null)
			{
				var item = items[index];
				tooltipContainer.Value.SetTooltip(TooltipTemplate, new WidgetArgs { { "getText", item.GetTooltip } });
			}
		}

		public override void MouseExited()
		{
			Hovered = -1;
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

			if (mi.Event == MouseInputEvent.Down && CanScroll && index < 0 && ScrollTrack.Contains(mi.Location))
			{
				ScrollBy(mi.Location.Y < ThumbRect().Y ? -VisibleRows : VisibleRows);
				return true;
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
			var height = Math.Max(10, track.Height * VisibleRows / total);
			var y = track.Y + (track.Height - height) * scrollRow / Math.Max(1, MaxScroll);
			return new Rectangle(track.X, y, track.Width, height);
		}

		public override void Draw()
		{
			var family = CityTheme.FamilyOf(this);
			var font = Game.Renderer.Fonts[Font];
			var b = CityTheme.BevelLogical;
			var ink = CityTheme.Ink;
			var muted = CityTheme.Muted(family);

			for (var i = 0; i < items.Count; i++)
			{
				if (!IsShown(i))
					continue;

				var item = items[i];
				var cell = CellRect(i);
				var disabled = item.IsDisabled();
				var active = item.IsActive();
				var part = disabled ? "card-locked" : active ? "card-selected" : i == Hovered ? "card-hover" : "card";
				CityTheme.DrawPanel(CityTheme.Art(family, part), cell);

				// The sunken thumbnail well: square, inside the card bevel
				var well = new CityRect(cell.X + b, cell.Y + b, cell.Width - 2 * b, cell.Width - 2 * b);
				CityTheme.DrawPanel(CityTheme.Art(family, "card-well"), well.X, well.Y, well.Width, well.Height);
				var sink = active ? b : 0;
				var thumb = CityTheme.Thumbnail(item.Thumbnail);
				var sprite = thumb ?? CityTheme.Icon(item.Icon, 32, disabled);
				if (sprite != null)
				{
					if (disabled && thumb != null)
						Game.Renderer.RgbaSpriteRenderer.DrawSprite(sprite, new Vector3(
							(float)Math.Round(well.X + (well.Width - sprite.Size.X) / 2 + sink), (float)Math.Round(well.Y + (well.Height - sprite.Size.Y) / 2 + sink), 0),
							1f, new Vector3(0.22f, 0.17f, 0.19f), 1f);
					else
						CityTheme.DrawCentered(sprite, well.X + sink, well.Y + sink, well.Width, well.Height);
				}

				var textY = cell.Y + cell.Width + 1;
				var name = WidgetUtils.TruncateText(item.Name ?? "", cell.Width - 4, font);
				var nameSize = font.Measure(name);
				font.DrawText(name, new Vector2(cell.X + (cell.Width - nameSize.X) / 2, textY), disabled ? muted : ink);

				if (item.CostText.Length > 0)
				{
					var color = disabled ? muted : item.IsAffordable() ? CityTheme.MoneyPositive : CityTheme.MoneyNegative;
					var cost = WidgetUtils.TruncateText(item.CostText, cell.Width - 4, font);
					var costSize = font.Measure(cost);
					font.DrawText(cost, new Vector2(cell.X + (cell.Width - costSize.X) / 2, textY + 10), color);
				}
			}

			if (CanScroll)
			{
				var track = ScrollTrack;
				CityTheme.DrawPanel(CityTheme.Art(family, "scroll-trough"), track);
				CityTheme.DrawPanel(CityTheme.Art(family, "slider-thumb"), ThumbRect());
			}
		}
	}
}
