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
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Network;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>The "New City" map picker. Starts a local game directly, there is no lobby.</summary>
	public class CityNewGameLogic : ChromeLogic
	{
		[FluentReference("width", "height")]
		const string MapSize = "label-newgame-map-size";

		[FluentReference("author")]
		const string MapAuthor = "label-newgame-map-author";

		readonly ModData modData;
		MapPreview selectedMap;
		bool sandbox;

		[ObjectCreator.UseCtor]
		public CityNewGameLogic(Widget widget, ModData modData, Action onExit)
		{
			this.modData = modData;

			var panel = (CityPanelWidget)widget;
			panel.OnClose = () =>
			{
				Ui.CloseWindow();
				onExit();
			};

			var previewWidget = widget.Get("PREVIEW_BG").Get<MapPreviewWidget>("PREVIEW");
			previewWidget.Preview = () => selectedMap;

			widget.Get<CityHeaderWidget>("MAP_HEADER").GetText = () => selectedMap?.Title ?? "";

			var climateLabel = widget.Get<LabelWidget>("MAP_CLIMATE");
			climateLabel.GetText = () => selectedMap != null && modData.DefaultTerrainInfo.TryGetValue(selectedMap.TileSet, out var terrain)
				? FluentProvider.GetMessage(terrain.Name) : "";

			var infoLabel = widget.Get<LabelWidget>("MAP_INFO");
			infoLabel.GetText = () => selectedMap == null ? "" :
				FluentProvider.GetMessage(MapSize, "width", selectedMap.Bounds.Width, "height", selectedMap.Bounds.Height);

			var authorLabel = widget.Get<LabelWidget>("MAP_AUTHOR");
			authorLabel.GetText = () => string.IsNullOrEmpty(selectedMap?.Author) ? "" :
				FluentProvider.GetMessage(MapAuthor, "author", selectedMap.Author);

			var list = widget.Get<ScrollPanelWidget>("MAP_LIST");
			var template = list.Get<ScrollItemWidget>("MAP_TEMPLATE");
			list.RemoveChild(template);

			var maps = modData.MapCache
				.Where(p => p.Status == MapStatus.Available && p.Visibility.HasFlag(MapVisibility.Lobby))
				.OrderBy(p => p.Title, StringComparer.CurrentCultureIgnoreCase)
				.ToList();

			widget.Get("NO_MAPS").IsVisible = () => maps.Count == 0;

			foreach (var map in maps)
			{
				var preview = map;
				var item = ScrollItemWidget.Setup(template,
					() => selectedMap == preview,
					() => selectedMap = preview,
					() => StartGame(onExit));

				item.Get<CityRowBackgroundWidget>("BG").IsSelected = item.IsSelected;
				item.Get<MapPreviewWidget>("ITEM_PREVIEW").Preview = () => preview;
				var title = item.Get<LabelWidget>("TITLE");
				title.GetText = () => preview.Title;
				title.GetColor = () => item.IsSelected() ? Color.White : CityTheme.Ink;
				var size = item.Get<LabelWidget>("SIZE");
				size.GetColor = () => item.IsSelected() ? Color.White : CityTheme.Ink;
				size.GetText = () =>
					FluentProvider.GetMessage(MapSize, "width", preview.Bounds.Width, "height", preview.Bounds.Height);
				list.AddChild(item);
			}

			// Prefer the last played map, otherwise the first one.
			selectedMap = maps.FirstOrDefault(m => m.Uid == Game.Settings.Server.Map) ?? maps.FirstOrDefault();

			var sandboxCheckbox = widget.Get<CheckboxWidget>("SANDBOX");
			sandboxCheckbox.IsChecked = () => sandbox;
			sandboxCheckbox.OnClick = () => sandbox = !sandbox;

			var startButton = widget.Get<ButtonWidget>("START_BUTTON");
			startButton.IsDisabled = () => selectedMap == null;
			startButton.OnClick = () => StartGame(onExit);

			widget.Get<ButtonWidget>("BACK_BUTTON").OnClick = () =>
			{
				Ui.CloseWindow();
				onExit();
			};
		}

		void StartGame(Action onExit)
		{
			if (selectedMap == null)
				return;

			// The map may have changed or vanished on disk since the list was built.
			var uid = modData.MapCache.GetUpdatedMap(selectedMap.Uid);
			if (uid == null)
			{
				Ui.CloseWindow();
				onExit();
				return;
			}

			Game.Settings.Server.Map = uid;
			Game.Settings.Save();

			var orders = new List<Order>
			{
				Order.Command("option gamespeed default"),
				Order.Command($"option {CitySandbox.Option} {sandbox}"),
				Order.Command($"state {Session.ClientState.Ready}")
			};

			CityNewGameAutoTest.PrepareGame();
			Game.CreateAndStartLocalServer(uid, orders);
		}
	}
}
