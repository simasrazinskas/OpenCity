#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of the License, or (at your option)
 * any later version. For more information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// Advisor card: first-time guidance (build a road, zone, power, water, services, ...) plus the live hints of ENV's
	/// <see cref="IAdvisorSource"/>. Tutorial steps are rows of <see cref="Steps"/> with the fluent keys tutorial-{id}-title / -text /
	/// -hint and complete by themselves when the city reaches the state they ask for; advisor messages show their fluent key and
	/// "-hint", a warning or worse comes first and has a locate button.
	/// </summary>
	public class CityAdvisorLogic : ChromeLogic
	{
		[FluentReference("current", "total")]
		const string StepLabel = "label-advisor-step";

		[FluentReference]
		const string GotIt = "label-advisor-gotit";

		[FluentReference]
		const string NextLabel = "button-advisor-next";

		[FluentReference]
		const string LocateTip = "button-chirper-locate";

		const int WellTop = 38;
		const int PortraitBottom = 110;

		sealed class AdvisorStep
		{
			public string Id;
			public Func<World, CityUiContext, bool> Done;
		}

		/// <summary>One card: a tutorial step or an advisor message.</summary>
		sealed class Item
		{
			public string Id;
			public string Title;
			public string Text;
			public string Hint;
			public Color HintColor;
			public string HintIcon;
			public bool Tutorial;
			public bool Urgent;
			public int Step;
			public bool HasCell;
			public CPos Cell;
		}

		static readonly AdvisorStep[] Steps =
		[
			new() { Id = "road", Done = (w, c) => c.Roads != null && c.Roads.RoadCells.Skip(7).Any() },
			new() { Id = "zone", Done = (w, c) => ZonedCells(w) > 0 },
			new() { Id = "power", Done = (w, c) => c.Utilities != null && c.Utilities.PowerProduced > 0 },
			new() { Id = "water", Done = (w, c) => c.Utilities != null && c.Utilities.WaterProduced > 0 },
			new() { Id = "people", Done = (w, c) => (c.Citizens?.Population ?? 0) >= 20 },
			new() { Id = "services", Done = (w, c) => w.ActorsWithTrait<ServiceBuilding>().Any() },
			new() { Id = "budget", Done = (w, c) => OpenedBudget },
			new() { Id = "infoviews", Done = (w, c) => OpenedInfoViews },
			new() { Id = "grow", Done = (w, c) => (c.Citizens?.Population ?? 0) >= 250 },
		];

		/// <summary>Set by the toolbar while the budget or info view panels are shown, so the matching steps complete.</summary>
		public static bool OpenedBudget;
		public static bool OpenedInfoViews;

		/// <summary>Whether the player closed the card (session only); the order button reopens it.</summary>
		public static bool Dismissed;

		readonly World world;
		readonly CityUiContext ctx;
		readonly Widget panel;
		readonly LabelWidget text;
		readonly LabelWidget hint;
		readonly Widget hintIcon;
		readonly Widget well;
		readonly Widget progressBar;
		readonly Widget dots;
		readonly Widget[] buttons;
		readonly bool[] done = new bool[Steps.Length];
		readonly List<Item> items = [];
		readonly StringBuilder signature = new();

		string builtSignature;
		int index;
		int stampTick = -1;
		int doneTick = -1;

		static int ZonedCells(World world)
		{
			var zones = world.WorldActor.TraitOrDefault<ZoneLayer>();
			if (zones == null)
				return 1;

			var total = 0;
			for (var z = ZoneType.ResidentialLow; z <= ZoneType.Warehouse; z++)
				total += zones.CountZoned(z);

			return total;
		}

		[ObjectCreator.UseCtor]
		public CityAdvisorLogic(Widget widget, World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);
			panel = widget;
			((CityPanelWidget)widget).OnClose = () => Dismissed = true;

			var progress = widget.Get<CityBarWidget>("PROGRESS");
			progressBar = progress;
			progress.GetPercentage = () => DoneCount() * 100 / Steps.Length;
			progress.IsVisible = () => Current()?.Tutorial == true;

			dots = widget.Get("DOTS");
			dots.IsVisible = () => Current()?.Tutorial == true;
			for (var i = 0; i < Steps.Length; i++)
			{
				var step = i;
				dots.AddChild(new ColorBlockWidget(Game.ModData)
				{
					Bounds = new WidgetBounds(0, 0, 4, 4),
					GetColor = () => IsDone(step) ? CityTheme.Ramp("yellow", 5) : CityTheme.FamilyShade("people", 3)
				});
			}

			var previous = widget.Get<ButtonWidget>("PREVIOUS");
			previous.IsVisible = () => items.Count > 1;
			previous.IsDisabled = () => index <= 0;
			previous.OnClick = () => index = Math.Max(0, index - 1);

			widget.Get<ButtonWidget>("DISMISS").OnClick = () => Dismissed = true;

			var next = widget.Get<ButtonWidget>("NEXT");
			var nextText = FluentProvider.GetMessage(NextLabel);
			var gotIt = FluentProvider.GetMessage(GotIt);
			next.GetText = () => index >= items.Count - 1 ? gotIt : nextText;
			next.OnClick = () =>
			{
				if (index >= items.Count - 1)
					Dismissed = true;
				else
					index++;
			};

			var locate = widget.Get<ButtonWidget>("LOCATE");
			var locateTip = FluentProvider.GetMessage(LocateTip);
			locate.GetTooltipText = () => locateTip;
			locate.IsVisible = () => Current()?.HasCell == true;
			locate.OnClick = () =>
			{
				if (Current() is { HasCell: true } item)
					ctx.CenterOn?.Invoke(item.Cell);
			};

			widget.Get<LabelWidget>("TITLE").GetText = () => Current()?.Title ?? "";
			well = widget.Get("MESSAGE_WELL");
			text = well.Get<LabelWidget>("TEXT");
			text.GetText = () => Current()?.Text ?? "";
			hint = widget.Get<LabelWidget>("HINT");
			hint.GetText = () => Current()?.Hint ?? "";
			hint.GetColor = () => Current()?.HintColor ?? CityTheme.MoneyPositive;
			var icon = widget.Get<CityIconWidget>("HINT_ICON");
			hintIcon = icon;
			icon.GetIcon = () => Current()?.HintIcon;
			icon.IsVisible = () => !string.IsNullOrEmpty(Current()?.Hint);

			var stepLabel = widget.Get<LabelWidget>("STEP");
			stepLabel.IsVisible = () => Current() is { Tutorial: true };
			stepLabel.GetText = () => Current() is { Tutorial: true } item ? FluentProvider.GetMessage(StepLabel, "current", item.Step, "total", Steps.Length) : "";
			stepLabel.GetColor = () => CityTheme.Muted("people");
			var urgent = widget.Get("URGENT");
			urgent.IsVisible = () => Current()?.Urgent == true;
			urgent.Get<LabelWidget>("LABEL").GetColor = () => CityTheme.InkLight;
			widget.Get("NAMEPLATE").Get<LabelWidget>("NAME").GetColor = () => CityTheme.InkLight;

			buttons = [previous, widget.Get("DISMISS"), next, locate];

			widget.IsVisible = () =>
			{
				if (world.LocalPlayer == null || Dismissed)
					return false;

				Refresh();
				return items.Count > 0;
			};
		}

		bool IsDone(int step)
		{
			if (doneTick != world.WorldTick)
			{
				doneTick = world.WorldTick;
				for (var i = 0; i < Steps.Length; i++)
					done[i] = Steps[i].Done(world, ctx);
			}

			return done[step];
		}

		/// <summary>The window hugs its text: message, hint, tutorial progress, then the button row (all positions in window pixels).</summary>
		public override void Tick()
		{
			var textHeight = FitHeight(text, 11);
			var wellHeight = Math.Max(30, textHeight + 8);
			well.Bounds.Height = wellHeight;
			var y = WellTop + wellHeight + 4;

			hint.Bounds.Y = y;
			hintIcon.Bounds.Y = y;
			var hintHeight = FitHeight(hint, 16);
			if (hintHeight > 0)
				y += Math.Max(16, hintHeight) + 4;

			progressBar.Bounds.Y = y;
			dots.Bounds.Y = y + 10;
			var count = dots.Children.Count;
			for (var i = 0; i < count; i++)
				dots.Children[i].Bounds.X = dots.Bounds.Width * (2 * i + 1) / (2 * count) - 2;

			if (progressBar.IsVisible())
				y += 18;

			y = Math.Max(y + 4, PortraitBottom + 4);
			foreach (var button in buttons)
				button.Bounds.Y = y;

			panel.Bounds.Height = y + 18 + 4;
		}

		/// <summary>Sizes a word-wrapped label to its text and returns the height (0 when empty).</summary>
		static int FitHeight(LabelWidget label, int minimum)
		{
			var value = label.GetText();
			if (string.IsNullOrEmpty(value))
			{
				label.Bounds.Height = 0;
				return 0;
			}

			var font = Game.Renderer.Fonts[label.Font];
			var height = Math.Max(minimum, font.Measure(WidgetUtils.WrapText(value, label.Bounds.Width, font)).Y + 1);
			label.Bounds.Height = height;
			return height;
		}

		Item Current()
		{
			Refresh();
			return index >= 0 && index < items.Count ? items[index] : null;
		}

		int DoneCount()
		{
			IsDone(0);
			return done.Count(d => d);
		}

		/// <summary>Rebuilds the card list (at most once per tick): urgent advisor messages, the next tutorial step, the other hints.</summary>
		void Refresh()
		{
			if (stampTick == world.WorldTick)
				return;

			stampTick = world.WorldTick;
			items.Clear();
			var messages = ctx.Get<IAdvisorSource>()?.Messages ?? [];
			foreach (var m in messages)
				if (m.Severity >= ProblemTier.Warning)
					items.Add(FromMessage(m));

			var step = Array.FindIndex(Steps, s => !s.Done(world, ctx));
			if (step >= 0)
				items.Add(FromStep(step));

			foreach (var m in messages)
				if (m.Severity < ProblemTier.Warning)
					items.Add(FromMessage(m));

			// A changed card list (new message, finished step) goes back to the first card.
			signature.Clear();
			foreach (var item in items)
				signature.Append(item.Id).Append('|');

			var text = signature.ToString();
			if (text != builtSignature)
			{
				builtSignature = text;
				index = 0;
			}

			index = Math.Clamp(index, 0, Math.Max(0, items.Count - 1));
		}

		static Item FromStep(int step)
		{
			var id = Steps[step].Id;
			return new Item
			{
				Id = "tutorial:" + id,
				Tutorial = true,
				Step = step + 1,
				Title = CityUi.Message("tutorial-" + id + "-title"),
				Text = CityUi.Message("tutorial-" + id + "-text", ""),
				Hint = CityUi.Message("tutorial-" + id + "-hint", ""),
				HintColor = CityTheme.MoneyPositive,
				HintIcon = "ui_info"
			};
		}

		static Item FromMessage(AdvisorMessage m)
		{
			var text = FluentProvider.TryGetMessage(m.Key, out var message, "arg", m.Arg ?? "") ? message : CityUi.Prettify(m.Key);
			var hint = FluentProvider.TryGetMessage(m.Key + "-hint", out var h, "arg", m.Arg ?? "") ? h : "";
			var urgent = m.Severity >= ProblemTier.Warning && m.Severity != ProblemTier.Good;
			return new Item
			{
				Id = m.Id,
				Title = CityUi.Prettify(m.Id ?? m.Key),
				Text = text,
				Hint = hint,
				HintColor = urgent ? CityTheme.MoneyNegative : CityTheme.MoneyPositive,
				HintIcon = "tier_" + m.Severity.ToString().ToLowerInvariant(),
				Urgent = urgent,
				HasCell = m.HasCell,
				Cell = m.Cell
			};
		}
	}
}
