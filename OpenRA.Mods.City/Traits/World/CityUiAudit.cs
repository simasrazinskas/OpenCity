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

using System.Collections.Generic;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Traits
{
	/// <summary>
	/// Developer check used by the autotest ("audit=1"): walks the visible HUD widgets and reports text that does not
	/// fit its label or button, widgets that stick out of their parent and panels that leave the window. Measures with
	/// the real fonts at the current UI scale, so it catches truncation the screenshots would hide.
	/// </summary>
	public static class CityUiAudit
	{
		public static List<string> Run(Widget root)
		{
			var issues = new List<string>();
			var window = new Rectangle(0, 0, Game.Renderer.Resolution.Width, Game.Renderer.Resolution.Height);
			foreach (var child in root.Children)
				Visit(child, child.Id ?? child.GetType().Name, window, false, issues);

			return issues;
		}

		static void Visit(Widget widget, string path, Rectangle clip, bool clipped, List<string> issues)
		{
			if (!widget.IsVisible())
				return;

			var rb = widget.RenderBounds;
			if (rb.Width > 0 && rb.Height > 0 && !clipped && !Contains(clip, rb))
				issues.Add($"{path}: {Describe(rb)} outside {Describe(clip)}");

			CheckText(widget, path, rb, issues);
			CheckArt(widget, path, issues);

			// Scroll panels clip their content on purpose; only check the text inside.
			var inner = widget is ScrollPanelWidget || clipped;
			var childClip = rb.Width > 0 && rb.Height > 0 ? rb : clip;
			foreach (var child in widget.Children)
				Visit(child, path + "/" + (child.Id ?? child.GetType().Name), childClip, inner, issues);
		}

		/// <summary>Chrome art a visible widget names but the chrome does not have (it would draw nothing or a blank).</summary>
		static void CheckArt(Widget widget, string path, List<string> issues)
		{
			static bool Panel(string name) =>
				string.IsNullOrEmpty(name) || ChromeProvider.TryGetPanelImages(name) != null || ChromeProvider.TryGetPanelImages(name + "-hover") != null;

			switch (widget)
			{
				case Mods.City.Widgets.CityIconWidget icon:
				{
					var name = icon.GetIcon();
					if (string.IsNullOrEmpty(name))
						break;

					var sprite = icon.Thumbnail ? Mods.City.Widgets.CityTheme.Thumbnail(name) : Mods.City.Widgets.CityTheme.Icon(name, icon.Size);
					if (sprite == null)
						issues.Add($"{path}: missing {(icon.Thumbnail ? "thumbnail" : "icon")} `{name}` ({icon.Size})");

					break;
				}

				case Mods.City.Widgets.CityPanelWidget window:
					foreach (var tab in window.Tabs)
						if (Mods.City.Widgets.CityTheme.Icon(tab.Icon, 16) == null)
							issues.Add($"{path}: missing tab icon `{tab.Icon}`");

					break;
				case Mods.City.Widgets.CityBuildMenuWidget menu:
					foreach (var item in menu.Items)
						if (Mods.City.Widgets.CityTheme.Thumbnail(item.Thumbnail) == null && Mods.City.Widgets.CityTheme.Icon(item.Icon, 32) == null)
							issues.Add($"{path}: card `{item.Name}` has neither thumbnail `{item.Thumbnail}` nor icon `{item.Icon}`");

					break;
				case ImageWidget image:
				{
					var collection = image.GetImageCollection();
					var name = image.GetImageName();
					if (!string.IsNullOrEmpty(collection) && !string.IsNullOrEmpty(name) && ChromeProvider.TryGetImage(collection, name) == null)
						issues.Add($"{path}: missing image `{collection}/{name}`");

					break;
				}

				case ButtonWidget button:
					if (!Panel(button.Background))
						issues.Add($"{path}: missing panel `{button.Background}`");

					break;
				case BackgroundWidget background when widget is not Mods.City.Widgets.CityPanelWidget:
					if (!Panel(background.Background))
						issues.Add($"{path}: missing panel `{background.Background}`");

					break;
			}
		}

		static void CheckText(Widget widget, string path, Rectangle rb, List<string> issues)
		{
			string text;
			string fontName;
			int room;
			var wrap = false;
			switch (widget)
			{
				case LabelWidget label:
					text = label.GetText();
					fontName = label.Font;
					room = rb.Width;
					wrap = label.WordWrap;
					break;
				case ButtonWidget button:
					text = button.GetText();
					fontName = button.Font;
					room = rb.Width - button.LeftMargin - button.RightMargin;
					break;
				default:
					return;
			}

			if (string.IsNullOrEmpty(text) || !Game.Renderer.Fonts.TryGetValue(fontName, out var font))
				return;

			if (wrap)
			{
				var height = font.Measure(WidgetUtils.WrapText(text, rb.Width, font)).Y;
				if (height > rb.Height + 2)
					issues.Add($"{path}: wrapped text needs {height}px height, has {rb.Height} '{Short(text)}'");

				return;
			}

			var size = font.Measure(text);
			if (size.X > room)
				issues.Add($"{path}: text needs {size.X}px, has {room} '{Short(text)}'");
		}

		static bool Contains(Rectangle outer, Rectangle inner)
		{
			return inner.Left >= outer.Left - 1 && inner.Top >= outer.Top - 1 && inner.Right <= outer.Right + 1 && inner.Bottom <= outer.Bottom + 1;
		}

		static string Describe(Rectangle r) => $"[{r.X},{r.Y} {r.Width}x{r.Height}]";

		static string Short(string text)
		{
			text = text.Replace('\n', ' ');
			return text.Length > 40 ? text[..40] + "..." : text;
		}
	}
}
