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
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// The RCT2 Settings window (design/iso/ui/panels/settings-*.png). Panel switching, saving, restart prompts and reset
	/// are the common SettingsLogic, unchanged: it still builds one (hidden) tab button per panel, and the window's icon
	/// tabs click and mirror those buttons. The Hotkeys panel shares the Input tab (Mouse / Hotkeys buttons at the top of
	/// both panels).
	/// </summary>
	[IncludeChromeLogicArgsFluentReferences(nameof(DynamicFluentReferences))]
	public class CitySettingsLogic : SettingsLogic
	{
		[FluentReference("tab")]
		const string WindowTitle = "label-settings-window-title";

		// Tab icons by panel; panels listed after a tab share it.
		static readonly (string Icon, string Panel, string Shares)[] Tabs =
		[
			("ui_monitor", "DISPLAY_PANEL", null),
			("ui_speaker", "AUDIO_PANEL", null),
			("ui_mouse", "INPUT_PANEL", "HOTKEYS_PANEL"),
			("pnl_settings", "GAMEPLAY_PANEL", null),
			("ui_list", "ADVANCED_PANEL", null)
		];

		public static new IEnumerable<(string Key, FluentReferenceAttribute Reference)> DynamicFluentReferences(Dictionary<string, MiniYaml> logicArgs)
		{
			return SettingsLogic.DynamicFluentReferences(logicArgs);
		}

		readonly Dictionary<string, ButtonWidget> buttons = [];
		readonly Dictionary<string, string> labels = [];

		[ObjectCreator.UseCtor]
		public CitySettingsLogic(Widget widget, Action onExit, WorldRenderer worldRenderer, Dictionary<string, MiniYaml> logicArgs, ModData modData)
			: base(widget, onExit, worldRenderer, logicArgs, modData)
		{
			var window = (CityPanelWidget)widget;
			var tabContainer = widget.Get("SETTINGS_TAB_CONTAINER");
			foreach (var button in tabContainer.Children.OfType<ButtonWidget>())
				buttons[button.Id] = button;

			if (logicArgs.TryGetValue("Panels", out var panels))
				foreach (var node in panels.Nodes)
					labels[node.Key] = FluentProvider.GetMessage(node.Value.Value);

			bool Active(string panel) => buttons.TryGetValue(panel, out var b) && b.IsHighlighted();

			window.SetTabs(Tabs.Where(t => buttons.ContainsKey(t.Panel)).Select(t => new CityWindowTab
			{
				Icon = t.Icon,
				GetTooltip = () => labels.GetValueOrDefault(t.Panel),
				IsActive = () => Active(t.Panel) || (t.Shares != null && Active(t.Shares)),
				OnClick = () =>
				{
					if (!Active(t.Panel) && !(t.Shares != null && Active(t.Shares)))
						buttons[t.Panel].OnClick();
				}
			}));

			window.GetTitle = () =>
			{
				var active = buttons.FirstOrDefault(b => b.Value.IsHighlighted()).Key;
				return FluentProvider.GetMessage(WindowTitle, "tab", active != null ? labels.GetValueOrDefault(active) : "");
			};

			var back = widget.Get<ButtonWidget>("BACK_BUTTON");
			window.OnClose = back.OnClick;

			var panelContainer = widget.Get("PANEL_CONTAINER");
			SetupInputSubTabs(panelContainer);
		}

		// Mouse / Hotkeys switch at the top of both input panels.
		void SetupInputSubTabs(Widget panelContainer)
		{
			if (!buttons.TryGetValue("INPUT_PANEL", out var input) || !buttons.TryGetValue("HOTKEYS_PANEL", out var hotkeys))
				return;

			foreach (var panelId in new[] { "INPUT_PANEL", "HOTKEYS_PANEL" })
			{
				var panel = panelContainer.GetOrNull(panelId);
				var mouseButton = panel?.GetOrNull<ButtonWidget>("SUBTAB_MOUSE");
				var hotkeyButton = panel?.GetOrNull<ButtonWidget>("SUBTAB_HOTKEYS");
				if (mouseButton == null || hotkeyButton == null)
					continue;

				mouseButton.IsHighlighted = () => input.IsHighlighted();
				mouseButton.OnClick = () => { if (!input.IsHighlighted()) input.OnClick(); };
				hotkeyButton.IsHighlighted = () => hotkeys.IsHighlighted();
				hotkeyButton.OnClick = () => { if (!hotkeys.IsHighlighted()) hotkeys.OnClick(); };
			}
		}
	}
}
