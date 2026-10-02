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

using System.Linq;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Network;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// Runs after GameSaveBrowserLogic / LoadGameBrowserLogic (design/iso/ui/panels/save-dialog.png, load-dialog.png) and
	/// dresses their widgets as an RCT2 window: the close box acts as Cancel, the rows get the zebra / selected look and a
	/// second line (map and play time of the save), the common map preview shrinks to the right column and the player
	/// list (a city has no players to list) stays hidden.
	/// </summary>
	public class CitySaveBrowserLogic : ChromeLogic
	{
		[FluentReference]
		const string IncompatibleTitle = "label-load-game-browser-panel-incompatible-title";

		const int PreviewHeight = 112;

		[ObjectCreator.UseCtor]
		public CitySaveBrowserLogic(Widget widget, ModData modData)
		{
			var panel = (CityPanelWidget)widget;
			var cancel = widget.Get<ButtonWidget>("CANCEL_BUTTON");
			panel.OnClose = () => cancel.OnClick();

			ShrinkPreview(widget.Get("MAP_PREVIEW_ROOT"));
			widget.Get("SAVE_INFO").Get("PLAYER_LIST").IsVisible = () => false;

			var list = widget.Get<ScrollPanelWidget>("GAME_LIST");
			foreach (var item in list.Children.OfType<ScrollItemWidget>().Where(i => i.ItemKey != null).ToList())
				DecorateRow(item, modData);
		}

		/// <summary>The common map preview is laid out for 174x174: make it a thumbnail with title, type and author below.</summary>
		static void ShrinkPreview(Widget root)
		{
			var preview = root.Children.FirstOrDefault();
			if (preview == null)
				return;

			foreach (var id in new[] { "MAP_LARGE", "MAP_SMALL" })
			{
				var map = preview.GetOrNull(id);
				var background = map?.GetOrNull("MAP_BG");
				if (background == null)
					continue;

				background.Bounds.Height = PreviewHeight;
				foreach (var child in background.Children)
					child.Bounds = new WidgetBounds(1, 1, background.Bounds.Width - 2, PreviewHeight - 2);

				if (map.GetOrNull<LabelWidget>("MAP_TITLE") is { } title)
				{
					title.Font = "TinyBold";
					title.Bounds = new WidgetBounds(0, PreviewHeight + 1, title.Bounds.Width, 11);
				}
			}

			if (preview.GetOrNull("MAP_AVAILABLE") is { } available)
			{
				if (available.GetOrNull<LabelWidget>("MAP_TYPE") is { } type)
				{
					type.Font = "Tiny";
					type.Bounds = new WidgetBounds(0, PreviewHeight + 12, type.Bounds.Width, 11);
				}

				if (available.GetOrNull<LabelWidget>("MAP_AUTHOR") is { } author)
				{
					author.Font = "Tiny";
					author.Bounds = new WidgetBounds(0, PreviewHeight + 23, author.Bounds.Width, 11);
				}
			}
		}

		static void DecorateRow(ScrollItemWidget item, ModData modData)
		{
			bool Selected() => item.IsSelected();
			if (item.GetOrNull<CityRowBackgroundWidget>("BG") is { } background)
				background.IsSelected = Selected;

			var family = CityTheme.FamilyOf(item);
			var title = item.Get<LabelWithTooltipWidget>("TITLE");
			title.GetColor = () => Selected() ? Color.White : CityTheme.Ink;

			if (item.GetOrNull<LabelWidget>("CREATION_TIME") is { } time)
			{
				time.IsVisible = () => true;
				time.GetColor = () => Selected() ? Color.White : CityTheme.Muted(family);
			}

			if (item.GetOrNull<LabelWidget>("INFO") is { } info)
			{
				var text = SaveInfo(item.ItemKey, modData);
				info.GetText = () => text;
				info.GetColor = () => Selected() ? Color.White : CityTheme.Muted(family);
			}
		}

		/// <summary>"Map  -  play time" of a save, or the incompatible notice when its header cannot be read.</summary>
		static string SaveInfo(string path, ModData modData)
		{
			try
			{
				var save = new GameSave(path);
				var map = modData.MapCache[save.GlobalSettings.Map].Title;
				var duration = GameSaveUtils.FormatGameDuration(GameSaveUtils.GetGameDuration(save));
				return $"{map}  -  {duration}";
			}
			catch
			{
				return FluentProvider.GetMessage(IncompatibleTitle);
			}
		}
	}
}
