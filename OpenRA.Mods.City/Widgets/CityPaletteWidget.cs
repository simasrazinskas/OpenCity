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
	/// Draws the row backgrounds itself so empty rows keep the sidebar shape.
	/// </summary>
	public class CityPaletteWidget : Widget
	{
		public readonly string TooltipContainer;
		public readonly string TooltipTemplate = "SIMPLE_TOOLTIP";
		public readonly int2 IconSize = new(58, 48);
		public readonly int2 IconMargin = new(2, 0);
		public readonly int IconX = 39;
		public readonly int Columns = 3;
		public readonly int MinimumRows = 5;
		public readonly string RowCollection = "sidebar";
		public readonly string RowImage = "background-iconrow";
		public readonly string Font = "TinyBold";

		readonly ModData modData;
		readonly Lazy<TooltipContainerWidget> tooltipContainer;
		readonly List<PaletteItem> items = [];
		int hover = -1;
		int pressed = -1;

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
			RemoveTooltip();
		}

		public IReadOnlyList<PaletteItem> Items => items;

		int Rows => Math.Max(MinimumRows, (items.Count + Columns - 1) / Columns);

		Rectangle CellRect(int index)
		{
			var rb = RenderBounds;
			var column = index % Columns;
			var row = index / Columns;
			return new Rectangle(
				rb.X + IconX + column * (IconSize.X + IconMargin.X),
				rb.Y + row * IconSize.Y,
				IconSize.X, IconSize.Y);
		}

		int ItemAt(int2 location)
		{
			for (var i = 0; i < items.Count; i++)
				if (CellRect(i).Contains(location))
					return i;

			return -1;
		}

		public override void Tick()
		{
			Bounds.Height = Rows * IconSize.Y;
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
			var index = ItemAt(mi.Location);
			if (mi.Event == MouseInputEvent.Move)
			{
				SetHover(index);
				return false;
			}

			if (mi.Button != MouseButton.Left)
				return false;

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

		public override void Draw()
		{
			var rb = RenderBounds;
			var rowSprite = ChromeProvider.GetImage(RowCollection, RowImage);
			for (var r = 0; r < Rows; r++)
				WidgetUtils.DrawSprite(rowSprite, new Vector2(rb.X, rb.Y + r * IconSize.Y));

			var font = Game.Renderer.Fonts[Font];
			for (var i = 0; i < items.Count; i++)
			{
				var item = items[i];
				var cell = CellRect(i);
				var disabled = item.IsDisabled();
				var affordable = item.IsAffordable();

				var sprite = ChromeProvider.TryGetImage(item.Collection, item.Icon);
				if (sprite != null)
				{
					var size = sprite.Size;
					WidgetUtils.DrawSprite(sprite, new Vector2(
						cell.X + (cell.Width - size.X) / 2f,
						cell.Y + (cell.Height - size.Y) / 2f - 2));
				}

				if (disabled)
					WidgetUtils.FillRectWithColor(cell, Color.FromArgb(160, 0, 0, 0));
				else if (!affordable)
					WidgetUtils.FillRectWithColor(cell, Color.FromArgb(90, 0, 0, 0));

				if (item.CostText.Length > 0)
				{
					var textColor = disabled ? Color.FromArgb(0xB0, 0xB0, 0xB0) : affordable ? Color.White : CityUi.Bad;
					var textSize = font.Measure(item.CostText);
					font.DrawTextWithShadow(item.CostText,
						new Vector2(cell.X + (cell.Width - textSize.X) / 2f, cell.Bottom - textSize.Y - 1),
						textColor, Color.Black, Color.FromArgb(0, 0, 0, 0), 1);
				}

				if (item.IsActive())
					DrawFrame(cell, Color.FromArgb(0xFF, 0xA0, 0x20), 2);
				else if (i == hover && !disabled)
					DrawFrame(cell, Color.FromArgb(200, 0xFF, 0xE0, 0xA0), 1);
			}
		}

		static void DrawFrame(Rectangle r, Color c, int t)
		{
			WidgetUtils.FillRectWithColor(new Rectangle(r.X, r.Y, r.Width, t), c);
			WidgetUtils.FillRectWithColor(new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
			WidgetUtils.FillRectWithColor(new Rectangle(r.X, r.Y, t, r.Height), c);
			WidgetUtils.FillRectWithColor(new Rectangle(r.Right - t, r.Y, t, r.Height), c);
		}
	}
}
