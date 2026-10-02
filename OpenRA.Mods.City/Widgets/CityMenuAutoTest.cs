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
using System.Globalization;
using System.Linq;
using System.Reflection;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>Extra "shotui=" actions of CityAutoTest for the menus (UI state only, nothing synced).</summary>
	public static class CityMenuAutoTest
	{
		/// <summary>
		/// Handles `pick:N` (clicks the N-th visible item of the first visible scroll panel) and `pick:LIST_ID:N`
		/// (of the scroll panel with that id). Returns false for actions it does not know.
		/// </summary>
		public static bool TryApply(string action, Action<string> report, World world)
		{
			// Exercise real SDL geometry changes without changing the configured graphics mode. This also models
			// a compositor forcing a fullscreen game into a smaller window. Used only by the developer harness.
			if (action.StartsWith("resize:", StringComparison.Ordinal) || action == "windowed")
			{
				const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
				var platformWindow = typeof(Renderer).GetProperty("Window", Flags)?.GetValue(Game.Renderer);
				var handle = platformWindow?.GetType().GetProperty("Window", Flags)?.GetValue(platformWindow);
				var sdl = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("SDL2.SDL")).FirstOrDefault(t => t != null);
				if (handle == null || sdl == null)
					throw new InvalidOperationException("Window resize testing requires the SDL platform.");

				if (action == "windowed")
				{
					var result = sdl.GetMethod("SDL_SetWindowFullscreen")?.Invoke(null, [handle, 0u]);
					if (result is not int code || code != 0)
						throw new InvalidOperationException("Could not leave fullscreen for the resize test.");
				}
				else
				{
					var size = action["resize:".Length..].Split('x');
					if (size.Length != 2 || !int.TryParse(size[0], out var width) || !int.TryParse(size[1], out var height)
						|| width <= 0 || height <= 0)
						throw new ArgumentException("Expected resize:<width>x<height> with positive dimensions.", nameof(action));

					sdl.GetMethod("SDL_SetWindowSize")?.Invoke(null, [handle, width, height]);
				}

				report($"ui {action}; configured mode remains {Game.Settings.Graphics.Mode}");
				Game.RunAfterDelay(100, () => report($"window: native={Game.Renderer.NativeResolution.Width}x{Game.Renderer.NativeResolution.Height}, "
					+ $"logical={Game.Renderer.Resolution.Width}x{Game.Renderer.Resolution.Height}, "
					+ $"uiScale={Game.Renderer.UIScale}, root={Ui.Root.Bounds.Width}x{Ui.Root.Bounds.Height}"));
				return true;
			}

			// `show:WIDGET_ID` loads a widget tree into the UI root (for screens that only open during a game start, e.g. GAMESAVE_LOADING_SCREEN).
			if (action.StartsWith("show:", StringComparison.Ordinal))
			{
				var id = action["show:".Length..];
				Game.LoadWidget(world, id, Ui.Root, []);
				report($"ui show {id}");
				return true;
			}

			// `transient:TEXT` posts a transient world message (for checking its look).
			if (action.StartsWith("transient:", StringComparison.Ordinal))
			{
				TextNotificationsManager.AddTransientLine(null, action["transient:".Length..]);
				report($"ui {action}");
				return true;
			}

			if (!action.StartsWith("pick:", StringComparison.Ordinal))
				return false;

			var parts = action["pick:".Length..].Split(':');
			if (!int.TryParse(parts[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
			{
				report($"ui {action}: bad index");
				return true;
			}

			var list = parts.Length > 1 ? Find(Ui.Root, w => w.Id == parts[0] && w is ScrollPanelWidget) : Find(Ui.Root, w => w is ScrollPanelWidget);
			var items = list?.Children.OfType<ScrollItemWidget>().Where(i => i.IsVisible()).ToList();
			if (items == null || index < 0 || index >= items.Count)
			{
				report($"ui {action}: not found");
				return true;
			}

			items[index].OnClick();
			report($"ui {action}: clicked item {index} of {list.Id}");
			return true;
		}

		static Widget Find(Widget parent, Func<Widget, bool> match)
		{
			foreach (var c in parent.Children)
			{
				if (!c.IsVisible())
					continue;

				if (match(c))
					return c;

				var found = Find(c, match);
				if (found != null)
					return found;
			}

			return null;
		}
	}
}
