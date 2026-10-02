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
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets
{
	public enum CityPanelPlacement { Free, Center, TopLeft, TopCenter, TopRight, BottomLeft, BottomCenter, BottomRight }

	/// <summary>An icon tab of an RCT window.</summary>
	public sealed class CityWindowTab
	{
		public string Icon;
		public Func<string> GetTooltip = () => null;
		public Func<bool> IsActive = () => false;
		public Func<bool> IsDisabled = () => false;
		public Action OnClick = () => { };
	}

	/// <summary>
	/// An RCT2-style floating window (design/iso/ui/system/ds-window-anatomy.png): bevelled frame in the colours of its
	/// family, title bar with the title and a close box, optional row of icon tabs, resize grip. Windows open anchored
	/// (Placement, in the screen area left free by the toolbar and the status bar), can be dragged by the title bar,
	/// come to the front when clicked and are always kept on screen, also across window resizes and UI scale changes.
	/// All shared widgets inside take the art of the window family (CityTheme.ApplyFamily).
	/// </summary>
	public class CityPanelWidget : BackgroundWidget, ICityFamilyWidget
	{
		/// <summary>Logical height of the title bar.</summary>
		public const int TitleBarHeight = 15;

		/// <summary>Logical size of a tab.</summary>
		public const int TabWidth = 31, TabHeight = 26;

		/// <summary>Y of the content area: below the title bar, and below the tabs when the window has tabs.</summary>
		public const int ContentTop = 21, ContentTopWithTabs = 49;

		/// <summary>Inner padding of the content area.</summary>
		public const int Padding = 4;

		public CityPanelPlacement Placement = CityPanelPlacement.Center;

		/// <summary>Keep the top band free for the toolbar.</summary>
		public bool BelowAlerts = true;

		/// <summary>Optional id of a sibling panel: while it is visible, this panel docks to its left (top-aligned).</summary>
		public string LeftOf;

		/// <summary>Optional ids of sibling widgets this panel moves away from (sideways, then upwards) when it overlaps them and there is room.</summary>
		public string[] Avoid = [];

		/// <summary>Colour scheme: a window family of uistyle.yaml (city, finance, services, transit, info, people, zoning, system).</summary>
		public string Family { get; set; } = CityTheme.DefaultFamily;

		/// <summary>Fluent key of the title (or set GetTitle).</summary>
		[FluentReference]
		public string Title;

		public bool TitleBar = true;
		public bool CloseBox = true;
		public bool Draggable = true;
		public bool Grip = true;
		public string TooltipContainer = "TOOLTIP_CONTAINER";
		public string TooltipTemplate = "CITY_TOOLTIP";

		public Func<string> GetTitle;
		public Action OnClose;

		readonly List<CityWindowTab> tabs = [];
		Widget leftOf;
		Widget[] avoid;
		readonly Lazy<TooltipContainerWidget> tooltipContainer;

		/// <summary>Where the player dragged the window to (relative to the work area's top-left), or null while anchored.</summary>
		int2? dragged;
		int2 dragOffset;
		bool dragging;
		bool closePressed;
		int hoverTab = -1;
		bool raise;

		public CityPanelWidget()
		{
			Background = null;
			tooltipContainer = Exts.Lazy(() => Ui.Root.GetOrNull<TooltipContainerWidget>(TooltipContainer));
		}

		protected CityPanelWidget(CityPanelWidget other)
			: base(other)
		{
			Placement = other.Placement;
			BelowAlerts = other.BelowAlerts;
			LeftOf = other.LeftOf;
			Avoid = other.Avoid;
			Family = other.Family;
			Title = other.Title;
			TitleBar = other.TitleBar;
			CloseBox = other.CloseBox;
			Draggable = other.Draggable;
			Grip = other.Grip;
			TooltipContainer = other.TooltipContainer;
			TooltipTemplate = other.TooltipTemplate;
			GetTitle = other.GetTitle;
			OnClose = other.OnClose;
			tooltipContainer = Exts.Lazy(() => Ui.Root.GetOrNull<TooltipContainerWidget>(TooltipContainer));
		}

		public override CityPanelWidget Clone() { return new CityPanelWidget(this); }

		public override void Initialize(WidgetArgs args)
		{
			base.Initialize(args);
			if (GetTitle == null && Title != null)
			{
				var text = FluentProvider.GetMessage(Title);
				GetTitle = () => text;
			}
		}

		/// <summary>Replaces the icon tabs (empty = no tab row).</summary>
		public void SetTabs(IEnumerable<CityWindowTab> newTabs)
		{
			tabs.Clear();
			tabs.AddRange(newTabs);
			hoverTab = -1;
		}

		public IReadOnlyList<CityWindowTab> Tabs => tabs;

		/// <summary>Y where the content starts (below title bar and tabs).</summary>
		public int ContentY => !TitleBar ? Padding : tabs.Count > 0 ? ContentTopWithTabs : ContentTop;

		public void Close()
		{
			if (OnClose != null)
				OnClose();
			else
				Visible = false;
		}

		public override void Tick()
		{
			Place();
			if (raise)
			{
				// Not while the parent is iterating its children (this runs inside its tick).
				raise = false;
				Game.RunAfterTick(BringToFront);
			}
		}

		/// <summary>Draws this window above its siblings.</summary>
		public void BringToFront()
		{
			if (Parent == null)
				return;

			var siblings = Parent.Children;
			var index = siblings.IndexOf(this);
			if (index < 0 || index == siblings.Count - 1)
				return;

			// Keep non-window siblings (tooltips, overlays) that come after the windows on top.
			var last = siblings.FindLastIndex(w => w is CityPanelWidget);
			if (last <= index)
				return;

			siblings.RemoveAt(index);
			siblings.Insert(last, this);
		}

		int lastDrawFrame = -10;
		string appliedFamily;

		public override void Draw()
		{
			// A window that was not drawn in the previous frame was just opened: bring it to the front.
			if (Game.RenderFrame > lastDrawFrame + 1)
				raise = true;

			lastDrawFrame = Game.RenderFrame;

			// A panel opened after this frame's tick is placed before its first draw.
			Place();

			// Logic may switch the colour scheme (e.g. the building inspector per building kind).
			if (appliedFamily != null && appliedFamily != Family)
				CityTheme.Refamily(this, appliedFamily, Family);

			appliedFamily = Family;
			CityTheme.ApplyFamily(this, Family);

			var rb = RenderBounds;
			float x = rb.X, y = rb.Y, w = rb.Width, h = rb.Height;
			var background = string.IsNullOrEmpty(Background) || Background == "dialog" ? CityTheme.Art(Family, "window") : Background;
			CityTheme.DrawPanel(background, x, y, w, h);
			if (!TitleBar)
				return;

			var inset = 2 * CityTheme.BevelLogical;
			var tb = TitleBarRect();
			CityTheme.DrawPanel(CityTheme.Art(Family, "titlebar"), tb.X, tb.Y, tb.Width, tb.Height);

			var close = CloseRect();
			var titleWidth = tb.Width - (CloseBox ? close.Width + 2 : 0);
			var title = GetTitle?.Invoke();
			if (!string.IsNullOrEmpty(title))
			{
				var font = Game.Renderer.Fonts["TinyBold"];
				title = WidgetUtils.TruncateText(title, (int)titleWidth - 8, font);
				var size = font.Measure(title);
				var px = tb.X + (titleWidth - size.X) / 2;
				var py = tb.Y + (tb.Height - size.Y - font.TopOffset) / 2;
				font.DrawTextWithContrast(title, new System.Numerics.Vector2(MathF.Round(px), MathF.Round(py)),
					Color.White, CityTheme.FamilyShade(Family, 0), 1);
			}

			if (CloseBox)
			{
				var state = closePressed && close.Contains(Viewport.LastMousePos) ? "pressed"
					: Ui.MouseOverWidget == this && close.Contains(Viewport.LastMousePos) ? "hover" : "normal";
				var sprite = ChromeProvider.GetImage("rct-close", state);
				OpenRA.Mods.Common.Widgets.WidgetUtils.DrawSprite(sprite, new System.Numerics.Vector2(close.X, close.Y));
			}

			if (tabs.Count > 0)
				DrawTabs(x, y, w, inset);

			if (Grip)
			{
				var grip = ChromeProvider.TryGetImage(CityTheme.Art(Family, "rct-bits"), "grip");
				if (grip != null)
					WidgetUtils.DrawSprite(grip, new System.Numerics.Vector2(x + w - inset - grip.Size.X, y + h - inset - grip.Size.Y));
			}
		}

		void DrawTabs(float x, float y, float w, float inset)
		{
			var b = CityTheme.BevelLogical;
			var rowY = y + inset + TitleBarHeight + 2;
			var rowBottom = rowY + TabHeight;
			var activeIndex = tabs.FindIndex(t => t.IsActive());

			// The body edge under the tabs: a light line, broken under the active tab
			var light = CityTheme.FamilyShade(Family, 7);
			WidgetUtils.FillRectWithColor(Rect(x + inset / 2, rowBottom - b, w - inset, b), light);

			for (var i = 0; i < tabs.Count; i++)
			{
				var tab = tabs[i];
				var r = TabRect(i);
				var active = i == activeIndex;
				var part = active ? "tab-active" : i == hoverTab && !tab.IsDisabled() ? "tab-hover" : "tab";
				var top = active ? r.Y : r.Y + 2;
				var height = active ? TabHeight : TabHeight - 2;
				CityTheme.DrawPanel(CityTheme.Art(Family, part), r.X, top, r.Width, height);
				if (active)
					WidgetUtils.FillRectWithColor(Rect(r.X + b, rowBottom - b, r.Width - 2 * b, b), CityTheme.Body(Family));

				var icon = CityTheme.Icon(tab.Icon, 16, tab.IsDisabled());
				CityTheme.DrawCentered(icon, r.X, top, r.Width, height);
			}
		}

		static Rectangle Rect(float x, float y, float w, float h)
		{
			return new Rectangle((int)Math.Round(x), (int)Math.Round(y), Math.Max(1, (int)Math.Round(w)), Math.Max(1, (int)Math.Round(h)));
		}

		CityRect TitleBarRect()
		{
			var rb = RenderBounds;
			var inset = 2 * CityTheme.BevelLogical;
			return new CityRect(rb.X + inset, rb.Y + inset, rb.Width - 2 * inset, TitleBarHeight);
		}

		CityRect CloseRect()
		{
			var tb = TitleBarRect();
			var sprite = ChromeProvider.TryGetImage("rct-close", "normal");
			var s = sprite?.Size.X ?? 11;
			var b = CityTheme.BevelLogical;
			return new CityRect(tb.Right - s - b - 1, tb.Y + (tb.Height - s) / 2, s, s);
		}

		CityRect TabRect(int i)
		{
			var rb = RenderBounds;
			var inset = 2 * CityTheme.BevelLogical;
			return new CityRect(rb.X + inset + 2 + i * (TabWidth + 1), rb.Y + inset + TitleBarHeight + 2, TabWidth, TabHeight);
		}

		int TabAt(int2 pos)
		{
			for (var i = 0; i < tabs.Count; i++)
				if (TabRect(i).Contains(pos))
					return i;

			return -1;
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			if (mi.Event == MouseInputEvent.Move)
				UpdateHoverTab(mi.Location);

			if (dragging)
			{
				if (mi.Event == MouseInputEvent.Move)
				{
					var area = Area;
					var pos = mi.Location - dragOffset;
					Bounds.X = pos.X - (Parent?.ChildOrigin.X ?? 0);
					Bounds.Y = pos.Y - (Parent?.ChildOrigin.Y ?? 0);
					CityLayout.ClampInto(this, area);
					dragged = new int2(Bounds.X - area.X, Bounds.Y - area.Y);
				}
				else if (mi.Event == MouseInputEvent.Up)
				{
					dragging = false;
					YieldMouseFocus(mi);
				}

				return true;
			}

			if (closePressed)
			{
				if (mi.Event == MouseInputEvent.Up)
				{
					closePressed = false;
					YieldMouseFocus(mi);
					if (CloseRect().Contains(mi.Location))
					{
						Game.Sound.PlayNotification(Game.ModData.DefaultRules, null, "Sounds", ChromeMetrics.Get<string>("ClickSound"), null);
						Close();
					}
				}

				return true;
			}

			if (mi.Event == MouseInputEvent.Down && mi.Button == MouseButton.Left && TitleBar)
			{
				raise = true;
				if (CloseBox && CloseRect().Contains(mi.Location))
				{
					closePressed = TakeMouseFocus(mi);
					return true;
				}

				var tab = TabAt(mi.Location);
				if (tab >= 0)
				{
					if (!tabs[tab].IsDisabled())
					{
						Game.Sound.PlayNotification(Game.ModData.DefaultRules, null, "Sounds", ChromeMetrics.Get<string>("ClickSound"), null);
						tabs[tab].OnClick();
					}

					return true;
				}

				if (Draggable && TitleBarRect().Contains(mi.Location))
				{
					dragging = TakeMouseFocus(mi);
					dragOffset = mi.Location - RenderOrigin;
					return true;
				}
			}
			else if (mi.Event == MouseInputEvent.Down)
				raise = true;

			return !ClickThrough && EventBounds.Contains(mi.Location);
		}

		void UpdateHoverTab(int2 pos)
		{
			var tab = Ui.MouseOverWidget == this ? TabAt(pos) : -1;
			if (tab == hoverTab)
				return;

			hoverTab = tab;
			var container = tooltipContainer.Value;
			if (container == null)
				return;

			container.RemoveTooltip();
			if (tab >= 0 && tabs[tab].GetTooltip() != null)
			{
				var t = tabs[tab];
				container.SetTooltip(TooltipTemplate, new WidgetArgs { { "getText", t.GetTooltip } });
			}
		}

		public override void MouseExited()
		{
			if (hoverTab >= 0)
			{
				hoverTab = -1;
				tooltipContainer.Value?.RemoveTooltip();
			}
		}

		public override void Hidden()
		{
			base.Hidden();
			if (hoverTab >= 0)
				tooltipContainer.Value?.RemoveTooltip();

			hoverTab = -1;
			dragging = closePressed = false;
		}

		/// <summary>Applies the placement rule now (also used by logic right after it resized the panel).</summary>
		public void Place()
		{
			var area = CityLayout.WorkArea(BelowAlerts);
			if (dragged is int2 d)
			{
				Bounds.X = area.X + d.X;
				Bounds.Y = area.Y + d.Y;
				CityLayout.ClampInto(this, area);
				return;
			}

			if (LeftOf != null)
			{
				leftOf ??= Parent?.GetOrNull(LeftOf);
				if (leftOf != null && leftOf.IsVisible())
				{
					Bounds.X = leftOf.Bounds.X - CityLayout.Gap - Bounds.Width;
					Bounds.Y = leftOf.Bounds.Y;
					CityLayout.ClampInto(this, area);
					return;
				}
			}

			var w = Bounds.Width;
			var h = Bounds.Height;
			var centerX = CityLayout.Snap(area.X + (area.Width - w) / 2);
			var centerY = CityLayout.Snap(area.Y + (area.Height - h) / 2);
			switch (Placement)
			{
				case CityPanelPlacement.Center: Bounds.X = centerX; Bounds.Y = centerY; break;
				case CityPanelPlacement.TopLeft: Bounds.X = area.Left; Bounds.Y = area.Top; break;
				case CityPanelPlacement.TopCenter: Bounds.X = centerX; Bounds.Y = area.Top; break;
				case CityPanelPlacement.TopRight: Bounds.X = area.Right - w; Bounds.Y = area.Top; break;
				case CityPanelPlacement.BottomLeft: Bounds.X = area.Left; Bounds.Y = area.Bottom - h; break;
				case CityPanelPlacement.BottomCenter: Bounds.X = centerX; Bounds.Y = area.Bottom - h; break;
				case CityPanelPlacement.BottomRight: Bounds.X = area.Right - w; Bounds.Y = area.Bottom - h; break;
			}

			CityLayout.ClampInto(this, area);
			AvoidOthers(area);
		}

		/// <summary>Forgets where the player dragged the window, so it opens at its anchor again.</summary>
		public void ResetPosition() { dragged = null; }

		void AvoidOthers(Rectangle area)
		{
			if (Avoid.Length == 0 || Parent == null)
				return;

			avoid ??= Array.ConvertAll(Avoid, Parent.GetOrNull);
			foreach (var other in avoid)
			{
				if (other == null || !other.IsVisible())
					continue;

				var o = other.Bounds;
				var mine = new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);
				if (!mine.IntersectsWith(new Rectangle(o.X, o.Y, o.Width, o.Height)))
					continue;

				if (o.Right + CityLayout.Gap + Bounds.Width <= area.Right)
					Bounds.X = Math.Max(Bounds.X, o.Right + CityLayout.Gap);
				else if (o.Left - CityLayout.Gap - Bounds.Width >= area.Left)
					Bounds.X = Math.Min(Bounds.X, o.Left - CityLayout.Gap - Bounds.Width);
				else if (o.Top - CityLayout.Gap - Bounds.Height >= area.Top)
					Bounds.Y = o.Top - CityLayout.Gap - Bounds.Height;
			}
		}

		/// <summary>The largest height this panel can have on screen right now.</summary>
		public int MaxHeight => CityLayout.WorkArea(BelowAlerts).Height;

		/// <summary>The screen area this panel is kept in.</summary>
		public Rectangle Area => CityLayout.WorkArea(BelowAlerts);
	}
}
