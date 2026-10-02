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
using System.Globalization;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Developer harness: when the OPENCITY_AUTOTEST environment variable is set, builds a test city through",
		"real player orders, logs city stats, takes screenshots and exits. Does nothing otherwise.",
		"OPENCITY_AUTOTEST format: semicolon-separated key=value pairs, e.g.",
		"'ticks=3000;shots=100,1500,3000;timestep=5;log=250;scenario=basic'.",
		"shotui=a,b,... opens UI per shot (n-th entry before the n-th shot; join several actions with '+'): a panel name",
		"('budget' = CITY_BUDGET_PANEL), a widget id ('CITY_TILES_PANEL'), 'select:<actor type>', 'citizen', 'vehicle',",
		"'click:<BUTTON_ID>' (presses a button anywhere in the UI), 'key:<HotkeyName>', 'hover:<ID>[@x/y]', 'close' (closes the top window) or 'none'. 'audit=1' reports",
		"clipped text and widgets outside their parent after each shot.")]
	public class CityAutoTestInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new CityAutoTest(); }
	}

	public partial class CityAutoTest : IWorldLoaded, ITick
	{
		bool enabled;
		int endTick = 3000;
		int logInterval = 250;
		int timestep = 0;
		string scenario = "basic";
		float shotZoom;
		string probe;
		CityInfoView shotView;
		(int U, int V) shotCenter = (14, 0);

		// edge=Up|Down|Left|Right[+...]: after centering, jump the view to these map edges (map-edge hotkeys) before the shot
		readonly List<ScrollDirection> shotEdges = [];
		readonly HashSet<int> shotTicks = [];
		readonly List<string> shotUi = [];
		readonly HashSet<string> audited = [];
		int2? hoverAt;
		bool audit;
		int shotIndex;
		WorldRenderer worldRenderer;
		CPos anchor;
		bool issued;
		bool menuOnly;
		readonly List<(int Tick, string Name, Action<World, Player> Run)> scheduled = [];

		// click=<tick>:<WIDGET_ID>,...: presses buttons at the given ticks (see also ui=)
		readonly List<(int Tick, string Widget)> clicks = [];

		// mouse=<x>,<y>: where the pointer is parked for screenshots (logical pixels, negative = from the right/bottom)
		int2? mousePos;
		bool reportedGameOver;
		readonly List<string> uiClicks = [];
		float liveUIScale;
		bool logFonts;

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			var env = Environment.GetEnvironmentVariable("OPENCITY_AUTOTEST");
			if (string.IsNullOrEmpty(env) || w.Type == WorldType.Editor)
				return;

			// Shellmap (main menu) worlds only take screenshots: autotest.sh passes "menu=1" when run without a map.
			menuOnly = w.Type == WorldType.Shellmap;
			if (menuOnly != env.Contains("menu=1", StringComparison.Ordinal))
				return;

			enabled = true;
			worldRenderer = wr;
			foreach (var kv in env.Split(';', StringSplitOptions.RemoveEmptyEntries))
			{
				var parts = kv.Split('=', 2);
				if (parts.Length != 2)
					continue;

				switch (parts[0].Trim())
				{
					case "ticks": endTick = int.Parse(parts[1], CultureInfo.InvariantCulture); break;
					case "log": logInterval = Math.Max(1, int.Parse(parts[1], CultureInfo.InvariantCulture)); break;
					case "timestep": timestep = int.Parse(parts[1], CultureInfo.InvariantCulture); break;
					case "scenario": scenario = parts[1].Trim(); break;
					case "zoom": shotZoom = float.Parse(parts[1], CultureInfo.InvariantCulture); break;
					case "probe": probe = parts[1].Trim(); break;
					case "edge":
						foreach (var e in parts[1].Split('+', StringSplitOptions.RemoveEmptyEntries))
							shotEdges.Add(Enum.Parse<ScrollDirection>(e.Trim(), true));
						break;
					case "center":
						var uv = parts[1].Split(',');
						shotCenter = (int.Parse(uv[0], CultureInfo.InvariantCulture), int.Parse(uv[1], CultureInfo.InvariantCulture));
						break;
					case "view": shotView = Enum.Parse<CityInfoView>(parts[1].Trim(), true); break;
					case "shotui": shotUi.AddRange(parts[1].Split(',')); break;
					case "audit": audit = parts[1].Trim() == "1"; break;
					case "click":
						foreach (var c in parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries))
						{
							var tw = c.Split(':', 2);
							clicks.Add((int.Parse(tw[0], CultureInfo.InvariantCulture), tw[1].Trim()));
						}

						break;
					case "mouse":
						var xy = parts[1].Split(',');
						mousePos = new int2(int.Parse(xy[0], CultureInfo.InvariantCulture), int.Parse(xy[1], CultureInfo.InvariantCulture));
						break;
					case "ui": uiClicks.AddRange(parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries)); break;
					case "fonts": logFonts = parts[1].Trim() == "1"; break;
					case "uiscale": liveUIScale = float.Parse(parts[1], CultureInfo.InvariantCulture); break;
					case "movers": TrafficSim.Showcase = parts[1].Trim() == "demo"; break;
					case "weather":
						var atmosphere = w.WorldActor.TraitOrDefault<CityAtmosphere>();
						if (atmosphere != null)
							atmosphere.TestWeather = Array.IndexOf(["none", "rain", "snow", "storm", "fog"], parts[1].Trim());

						break;
					case "seethrough": CityView.SeeThrough = parts[1].Trim() == "1"; break;
					case "isoslice": CityView.Slicing = parts[1].Trim() != "0"; break;
					case "hour": w.WorldActor.TraitOrDefault<CityAtmosphere>()?.ForceTime(float.Parse(parts[1], CultureInfo.InvariantCulture), null); break;
					case "month": w.WorldActor.TraitOrDefault<CityAtmosphere>()?.ForceTime(null, float.Parse(parts[1], CultureInfo.InvariantCulture)); break;
					case "shots":
						foreach (var s in parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries))
							shotTicks.Add(int.Parse(s, CultureInfo.InvariantCulture));
						break;
				}
			}

			// Info view from the start, so the ground fill, building tints and lighting have settled by the first shot.
			var startView = InfoViewLayer.Get(w);
			if (shotView != CityInfoView.None && startView != null)
				startView.Mode = shotView;

			var connection = w.ActorsWithTrait<OutsideConnection>().FirstOrDefault();
			if (connection.Actor != null)
				anchor = connection.Actor.Location + connection.Trait.Info.Direction * (connection.Trait.Info.Length - 1);
			else
				anchor = new MPos(w.Map.Bounds.Left + w.Map.Bounds.Width / 2, w.Map.Bounds.Top + w.Map.Bounds.Height / 2).ToCPos(w.Map);

			Report(w, $"autotest enabled: scenario={scenario} ticks={endTick} anchor={anchor}");
		}

		/// <summary>UI-only test steps (button clicks); they run outside the tick, like player input.</summary>
		void RunUiSteps(World w, int tick)
		{
			foreach (var (_, id) in clicks.Where(c => c.Tick == tick))
			{
				Game.RunAfterTick(() =>
				{
					var button = FindButton(Ui.Root, id);
					if (button == null)
						Report(w, $"click: no button `{id}`");
					else
					{
						button.OnClick();
						Report(w, $"click: {id}");
					}
				});
			}
		}

		int2 MousePosition(int2 fallback)
		{
			if (mousePos is not int2 p)
				return fallback;

			var r = Game.Renderer.Resolution;
			return new int2(p.X < 0 ? r.Width + p.X : p.X, p.Y < 0 ? r.Height + p.Y : p.Y);
		}

		void ITick.Tick(Actor self)
		{
			if (!enabled)
				return;

			var w = self.World;
			var tick = w.WorldTick;

			if (timestep > 0 && w.Timestep != timestep)
				w.Timestep = timestep;

			// Replay playback: only log stats (compare them with the original run to verify determinism).
			if (w.IsReplay)
			{
				if (timestep > 0)
					w.ReplayTimestep = timestep;

				if (w.IsGameOver && !reportedGameOver)
				{
					reportedGameOver = true;
					Report(w, "REPLAY ENDED EARLY OR WENT OUT OF SYNC");
				}

				// The mayor re-derives its decisions from synced state without issuing orders (they come from the replay),
				// so its 'report mayor' lines can be compared with the recording run.
				if (scenario == "automayor")
					MayorTick(w, w.Players.FirstOrDefault(pl => pl.Playable));

				if (tick % logInterval == 0)
					LogStats(w);

				if (tick >= endTick)
				{
					enabled = false;
					Report(w, "autotest (replay) finished");
					Game.RunAfterDelay(500, Game.Exit);
				}

				return;
			}

			RunUiSteps(w, tick);

			// Hover the parked pointer like a real mouse move, so hover states and tooltips show up in the shots
			if (mousePos != null)
			{
				Game.RunAfterTick(() =>
				{
					var p = MousePosition(Viewport.LastMousePos);
					Viewport.LastMousePos = p;
					Ui.HandleInput(new MouseInput(MouseInputEvent.Move, MouseButton.None, p, int2.Zero, Modifiers.None, 0));
				});
			}

			// "ui=ID1,ID2": click the visible buttons with these widget ids, one every 10 ticks from tick 20 (e.g. to open dialogs).
			if (tick >= 20 && tick % 10 == 0 && (tick - 20) / 10 < uiClicks.Count)
			{
				var id = uiClicks[(tick - 20) / 10].Trim();
				Game.RunAfterTick(() => ClickButton(w, id));
			}

			// "fonts=1": log the pixel face and multiple that each font uses at the current UI scale.
			if (tick == 25 && logFonts)
				foreach (var f in Game.Renderer.Fonts.OrderBy(f => f.Key, StringComparer.Ordinal))
					if (f.Value.IsPixelFont)
						Report(w, $"font {f.Key}: grid {f.Value.CurrentPixelFace.Grid}{(f.Value.CurrentPixelFace.IsBold ? " bold" : "")}"
							+ $" x{f.Value.PixelScale} cap {f.Value.CurrentPixelFace.CapHeight * f.Value.PixelScale}px at scale {Game.Renderer.WindowScale}");

			// "uiscale=1.5": change the UI scale live at tick 45 (after any ui= clicks), like releasing the settings slider does.
			if (tick == 45 && liveUIScale > 0)
			{
				Mods.Common.Widgets.Logic.DisplaySettingsLogic.ApplyUIScale(Game.Settings.Graphics, liveUIScale);
				Report(w, $"ui: scale set to {liveUIScale}");
			}

			if (menuOnly)
			{
				if (shotTicks.Contains(tick))
				{
					var ui = shotIndex < shotUi.Count ? shotUi[shotIndex] : null;
					shotIndex++;
					Game.RunAfterTick(() => ApplyUi(w, ui));
					Game.RunAfterDelay(200, Game.TakeScreenshot);
					Game.RunAfterDelay(250, () => Audit(w, ui));
				}

				if (tick >= endTick)
				{
					enabled = false;
					Report(w, "autotest (menu) finished");
					Game.RunAfterDelay(500, Game.Exit);
				}

				return;
			}

			if (!issued && tick >= 5 && w.LocalPlayer != null)
			{
				issued = true;
				IssueScenario(w, w.LocalPlayer);
			}

			// Scheduled steps of the "full" scenario (issued once their tick is reached).
			for (var i = 0; i < scheduled.Count; i++)
			{
				if (scheduled[i].Tick > tick || w.LocalPlayer == null)
					continue;

				var (_, name, run) = scheduled[i];
				scheduled.RemoveAt(i--);
				run(w, w.LocalPlayer);
				Report(w, $"scenario step '{name}' issued");
			}

			if (scenario == "automayor" && w.LocalPlayer != null)
				MayorTick(w, w.LocalPlayer);

			if (tick % logInterval == 0)
				LogStats(w);

			if (shotTicks.Contains(tick))
			{
				var viewport = worldRenderer.Viewport;
				if (shotZoom > 0)
					viewport.SetZoom(shotZoom);

				viewport.Center(w.Map.CenterOfCell(At(shotCenter.U, shotCenter.V)));
				if (shotEdges.Count > 0)
				{
					foreach (var e in shotEdges)
						viewport.JumpToMapEdge(e);

					var edgeCell = w.Map.CellContaining(viewport.CenterPosition);
					Report(w, $"view at map edge {string.Join("+", shotEdges)}: center={edgeCell} blocked={viewport.GetBlockedDirections()}");
				}

				var ui = shotIndex < shotUi.Count ? shotUi[shotIndex] : null;
				shotIndex++;
				Game.RunAfterTick(() => Sync.RunUnsynced(w, () => ApplyUi(w, ui)));

				// Opened panels fill their lists on their first UI tick: give them a few frames before the shot.
				Game.RunAfterDelay(string.IsNullOrWhiteSpace(ui) ? 0 : 120, () =>
				{
					// Order generators may only be swapped outside synced code.
					if (probe != null)
						Sync.RunUnsynced(w, () => ActivateProbe(w));
					else
					{
						// Park the pointer on the HUD's bottom strip so no world tooltip shows up in the shot (unless "hover:" or "mouse=" put it somewhere).
						Viewport.LastMousePos = hoverAt ?? MousePosition(new int2(380, Game.Renderer.Resolution.Height - 20));
						hoverAt = null;
					}

					var infoView = w.WorldActor.TraitOrDefault<InfoViewLayer>();
					if (infoView != null)
						infoView.Mode = shotView;

					Game.TakeScreenshot();
					Game.RunAfterDelay(100, () => Audit(w, ui));
				});
				Report(w, $"screenshot requested at tick {tick}");
			}

			if (tick >= endTick)
			{
				enabled = false;
				LogStats(w);
				Report(w, "autotest finished");
				Game.RunAfterDelay(500, Game.Exit);
			}
		}

		/// <summary>Opens the UI named by the "shotui=" entry of the current shot (panels close first; UI state only, nothing synced).</summary>
		void ApplyUi(World w, string spec)
		{
			if (string.IsNullOrWhiteSpace(spec))
				return;

			var ctx = Widgets.CityUiContext.For(w);
			foreach (var panel in w.Type == WorldType.Shellmap ? [] : PanelIds)
				if (Ui.Root.GetOrNull(panel) is { } open)
					open.Visible = false;

			foreach (var raw in spec.Split('+', StringSplitOptions.RemoveEmptyEntries))
			{
				var action = raw.Trim();
				if (action == "none")
					continue;

				if (action.StartsWith("select:", StringComparison.Ordinal))
				{
					var type = action["select:".Length..];
					var actor = w.Actors.Where(a => a.Info.Name == type && a.IsInWorld).OrderBy(a => a.ActorID).FirstOrDefault();
					if (actor != null)
						w.Selection.Combine(w, [actor], false, true);

					Report(w, $"ui select {type}: {(actor != null ? actor.ActorID.ToString(CultureInfo.InvariantCulture) : "none")}");
				}
				else if (action.StartsWith("click:", StringComparison.Ordinal))
					ClickButton(w, action["click:".Length..]);
				else if (action.StartsWith("tab:", StringComparison.Ordinal))
					Widgets.CityUiTour.ClickTab(action["tab:".Length..], m => Report(w, m));
				else if (action.StartsWith("hover:", StringComparison.Ordinal))
				{
					// Moves the pointer onto a widget (its centre, or "ID@x/y" for an offset inside it) so its tooltip shows.
					var target = action["hover:".Length..].Split('@');
					var widget = Ui.Root.GetOrNull(target[0]);
					if (widget != null)
					{
						var rb = widget.RenderBounds;
						var at = new int2(rb.X + rb.Width / 2, rb.Y + rb.Height / 2);
						if (target.Length > 1 && target[1].Split('/') is [var hx, var hy])
							at = new int2(rb.X + int.Parse(hx, CultureInfo.InvariantCulture), rb.Y + int.Parse(hy, CultureInfo.InvariantCulture));

						hoverAt = at;
						Viewport.LastMousePos = at;
						Ui.HandleInput(new MouseInput(MouseInputEvent.Move, MouseButton.None, at, int2.Zero, Modifiers.None, 0));
					}

					Report(w, $"ui hover {target[0]}: {(widget != null ? "ok" : "not found")}");
				}
				else if (action.StartsWith("key:", StringComparison.Ordinal))
				{
					// Presses a hotkey by its name in the hotkey definitions (e.g. key:CityTiles).
					var name = action["key:".Length..];
					var hotkey = Game.ModData.Hotkeys[name].GetValue();
					var handled = Ui.HandleKeyPress(new KeyInput { Event = KeyInputEvent.Down, Key = hotkey.Key, Modifiers = hotkey.Modifiers });
					Ui.HandleKeyPress(new KeyInput { Event = KeyInputEvent.Up, Key = hotkey.Key, Modifiers = hotkey.Modifiers });
					Report(w, $"ui key {name} ({hotkey}): {(handled ? "handled" : "not handled")}");
				}
				else if (Widgets.CityMenuAutoTest.TryApply(action, m => Report(w, m), w))
				{
				}
				else if (action == "close")
				{
					Ui.CloseWindow();
					Report(w, "ui close window");
				}
				else if (action == "citizen")
				{
					var citizens = ctx.Citizens;
					var properties = ctx.Properties;
					ctx.SelectedCitizen = citizens == null || properties == null ? 0 :
						properties.All.SelectMany(p => citizens.ResidentsOf(p.Id)).FirstOrDefault();
					Report(w, $"ui citizen {ctx.SelectedCitizen}");
				}
				else if (action == "vehicle")
				{
					var inspector = ctx.Get<IVehicleInspector>();
					var center = worldRenderer.Viewport.CenterPosition;
					if (inspector != null && inspector.TryGetVehicleAt(center, 1024 * 256, out var view))
						ctx.SelectedVehicle = view.TripId;

					Report(w, $"ui vehicle {ctx.SelectedVehicle}");
				}
				else
				{
					var id = action.Any(char.IsLower) ? "CITY_" + action.ToUpperInvariant() + "_PANEL" : action;
					var widget = Ui.Root.GetOrNull(id);
					if (widget != null)
						widget.Visible = true;

					Report(w, $"ui open {id}: {(widget != null ? "ok" : "not found")}");
				}
			}
		}

		/// <summary>"audit=1": reports clipped text and widgets outside their parent after each shot (each issue once per run).</summary>
		void Audit(World w, string ui)
		{
			if (!audit)
				return;

			var issues = CityUiAudit.Run(Ui.Root);
			var fresh = 0;
			foreach (var issue in issues)
				if (audited.Add(issue) && fresh++ < 80)
					Report(w, $"audit [{ui ?? "none"}] {issue}");

			Report(w, $"audit [{ui ?? "none"}] {issues.Count} issues ({fresh} new) at {Game.Renderer.Resolution.Width}x{Game.Renderer.Resolution.Height}");
		}

		static string[] PanelIds => Widgets.Logic.CityToolbarLogic.PanelIds;

		static void ClickButton(World w, string id)
		{
			var button = FindButton(Ui.Root, id);
			if (button == null)
			{
				Report(w, $"ui: no visible button '{id}'");
				return;
			}

			button.OnClick();
			Report(w, $"ui: clicked '{id}'");
		}

		static ButtonWidget FindButton(Widget parent, string id)
		{
			foreach (var c in parent.Children)
			{
				if (!c.IsVisible())
					continue;

				if (c.Id == id && c is ButtonWidget b && !b.IsDisabled())
					return b;

				var found = FindButton(c, id);
				if (found != null)
					return found;
			}

			return null;
		}

		/// <summary>Activates a placement tool with the pointer over a fixed cell, so screenshots show whether its preview lines up at any zoom.</summary>
		void ActivateProbe(World w)
		{
			w.OrderGenerator = probe == "bulldoze" ? new BulldozeOrderGenerator(w) : new PlaceCityBuildingOrderGenerator(w, probe);
			var target = w.Map.CenterOfCell(At(18, 3));
			Viewport.LastMousePos = worldRenderer.Viewport.WorldToViewPx(worldRenderer.ScreenPxPosition(target));
			var picked = worldRenderer.Viewport.ViewToWorld(Viewport.LastMousePos);
			Report(w, $"probe '{probe}' at {At(18, 3)} zoom={worldRenderer.Viewport.Zoom:0.00} mouse={Viewport.LastMousePos} picked={picked}" +
				(picked == At(18, 3) ? " pick=OK" : " pick=WRONG"));
		}

		// Scenario layout assumes the outside connection road runs east (Direction 1,0); (u, v) = (east, south) offsets from its end.
		CPos At(int u, int v) => anchor + new CVec(u, v);

		void IssueScenario(World w, Player p)
		{
			var orders = new List<Order>
			{
				// Main avenue and a street grid.
				CityOrders.BuildRoadOrder(p, At(0, 0), At(34, 0)),
				CityOrders.BuildRoadOrder(p, At(6, -9), At(6, 9)),
				CityOrders.BuildRoadOrder(p, At(13, -9), At(13, 9)),
				CityOrders.BuildRoadOrder(p, At(20, -9), At(20, 9)),
				CityOrders.BuildRoadOrder(p, At(27, -9), At(27, 9)),
				CityOrders.BuildRoadOrder(p, At(6, -9), At(27, -9)),
				CityOrders.BuildRoadOrder(p, At(6, 9), At(27, 9)),
				CityOrders.BuildRoadOrder(p, At(6, 5), At(27, 5)),

				// Zones.
				CityOrders.ZoneOrder(p, At(7, -8), At(12, -1), ZoneType.ResidentialLow),
				CityOrders.ZoneOrder(p, At(14, -8), At(19, -1), ZoneType.ResidentialHigh),
				CityOrders.ZoneOrder(p, At(21, -8), At(26, -1), ZoneType.Office),
				CityOrders.ZoneOrder(p, At(7, 1), At(12, 4), ZoneType.CommercialLow),
				CityOrders.ZoneOrder(p, At(14, 1), At(19, 4), ZoneType.CommercialHigh),
				CityOrders.ZoneOrder(p, At(21, 1), At(26, 8), ZoneType.Industrial),
				CityOrders.ZoneOrder(p, At(7, 6), At(19, 8), ZoneType.ResidentialLow),

				// Utilities and services along the avenue (north side: v = -2..-1, south side: v = 1..2).
				CityOrders.PlaceBuildingOrder(p, "police", At(1, -2)),
				CityOrders.PlaceBuildingOrder(p, "firestation", At(3, -2)),
				CityOrders.PlaceBuildingOrder(p, "clinic", At(29, -2)),
				CityOrders.PlaceBuildingOrder(p, "school", At(31, -2)),
				CityOrders.PlaceBuildingOrder(p, "park-large", At(33, -2)),
				CityOrders.PlaceBuildingOrder(p, "powerplant-coal", At(29, 1)),
				CityOrders.PlaceBuildingOrder(p, "watertower", At(31, 1)),
				CityOrders.PlaceBuildingOrder(p, "watertower", At(32, 1)),
				CityOrders.PlaceBuildingOrder(p, "windturbine", At(33, 1)),
				CityOrders.PlaceBuildingOrder(p, "watertower", At(34, 1)),
				CityOrders.PlaceBuildingOrder(p, "watertower", At(33, -1)),
				CityOrders.PlaceBuildingOrder(p, "park-small", At(1, 1)),
				CityOrders.PlaceBuildingOrder(p, "plaza", At(2, 1)),
				CityOrders.SetSpeedOrder(p, 3),
			};

			if (scenario == "full")
				ScheduleFullScenario();

			if (scenario == "isoshow")
				ScheduleIsoShow();

			if (scenario == "automayor")
			{
				// The AutoMayor plays through real orders from its own tick hook (CityAutoTest.AutoMayor.cs).
				w.IssueOrder(CityOrders.SetSpeedOrder(p, 3));
				Report(w, "issued automayor start");
				return;
			}

			if (scenario == "stress")
			{
				// Test-only grant (runs in synced tick code, so it is still deterministic).
				p.PlayerActor.TraitOrDefault<CityManager>()?.AddFunds(2000000, "test");
				AddStressOrders(w, p, orders);
			}

			if (scenario == "bulldoze")
			{
				orders.Add(CityOrders.BulldozeOrder(p, At(13, -9), At(13, -9)));
				orders.Add(CityOrders.ZoneOrder(p, At(7, 6), At(9, 8), ZoneType.None));
			}

			foreach (var o in orders)
				w.IssueOrder(o);

			Report(w, $"issued {orders.Count} scenario orders");
		}

		// Exercises every system on top of "basic": extra utilities, an extended avenue, a farm with its field area, a landfill,
		// a bus depot and a bus line, then schools/hospital once progression unlocks them (locked orders are simply refused).
		void ScheduleFullScenario()
		{
			scheduled.Add((5, "utilities", FullUtilities));
			scheduled.Add((300, "industry-and-depots", FullIndustryAndDepots));
			scheduled.Add((600, "farm-area", FullFarmArea));
			scheduled.Add((900, "bus-stops", FullBusStops));
			scheduled.Add((950, "bus-line", FullBusLine));
			foreach (var t in new[] { 4800, 9600 })
			{
				scheduled.Add((t, "farm-retry", FullFarmRetry));
				scheduled.Add((t + 300, "farm-area-retry", FullFarmArea));
			}

			scheduled.Add((4800, "education-health", FullEducationHealth));
			scheduled.Add((9600, "hospital", FullHospital));
		}

		void FullUtilities(World w, Player p)
		{
			w.IssueOrder(CityOrders.PlaceBuildingOrder(p, "watertower", At(34, 1)));
			w.IssueOrder(CityOrders.PlaceBuildingOrder(p, "windturbine", At(5, 1)));
			w.IssueOrder(CityOrders.PlaceBuildingOrder(p, "windturbine", At(5, -1)));
		}

		void FullIndustryAndDepots(World w, Player p)
		{
			w.IssueOrder(CityOrders.BuildRoadOrder(p, At(34, 0), At(48, 0)));
			w.IssueOrder(CityOrders.PlaceBuildingOrder(p, "farm-hub", At(38, 1)));
			w.IssueOrder(CityOrders.PlaceBuildingOrder(p, "landfill", At(42, -3)));
			w.IssueOrder(CityOrders.PlaceBuildingOrder(p, "busdepot", At(44, 1)));
		}

		void FullFarmRetry(World w, Player p)
		{
			w.IssueOrder(CityOrders.PlaceBuildingOrder(p, "farm-hub", At(38, 1)));
		}

		void FullFarmArea(World w, Player p)
		{
			var hub = w.Actors.FirstOrDefault(a => a.Owner == p && a.Info.Name == "farm-hub" && !a.IsDead);
			if (hub != null)
				w.IssueOrder(IndustryOrders.AreaOrder(p, hub, At(36, 4), At(44, 10), true));
		}

		void FullBusStops(World w, Player p)
		{
			foreach (var u in new[] { 3, 10, 17, 24, 31 })
				w.IssueOrder(TransitOrders.PlaceStopOrder(p, At(u, 0), TransitMode.Bus));
		}

		void FullBusLine(World w, Player p)
		{
			// Use whichever stops were actually placed (ids are assigned by the transit layer).
			var transit = w.WorldActor.TraitOrDefault<TransitLayer>();
			var ids = transit?.Stops.Select(s => s.Id).Order().ToList() ?? [];
			if (ids.Count >= 2)
				w.IssueOrder(TransitOrders.CreateLineOrder(p, TransitMode.Bus, false, ids, 0xE04040, "", 2));
		}

		void FullEducationHealth(World w, Player p)
		{
			w.IssueOrder(CityOrders.PlaceBuildingOrder(p, "school", At(31, -2)));
			w.IssueOrder(CityOrders.PlaceBuildingOrder(p, "park-large", At(33, -2)));
			w.IssueOrder(CityOrders.PlaceBuildingOrder(p, "highschool", At(46, -3)));
		}

		void FullHospital(World w, Player p)
		{
			w.IssueOrder(CityOrders.PlaceBuildingOrder(p, "hospital", At(38, -3)));
		}

		// Covers most of the map with a street grid and zones every block, to measure simulation cost at scale.
		static void AddStressOrders(World w, Player p, List<Order> orders)
		{
			var b = w.Map.Bounds;
			var tl = new MPos(b.Left + 2, b.Top + 2).ToCPos(w.Map);
			var br = new MPos(b.Right - 3, b.Bottom - 3).ToCPos(w.Map);
			const int Block = 7;
			for (var y = tl.Y; y <= br.Y; y += Block)
				orders.Add(CityOrders.BuildRoadOrder(p, new CPos(tl.X, y), new CPos(br.X, y)));

			for (var x = tl.X; x <= br.X; x += Block)
				orders.Add(CityOrders.BuildRoadOrder(p, new CPos(x, tl.Y), new CPos(x, br.Y)));

			ZoneType[] zones =
			[
				ZoneType.ResidentialLow, ZoneType.ResidentialHigh, ZoneType.CommercialLow, ZoneType.Industrial,
				ZoneType.ResidentialLow, ZoneType.CommercialHigh, ZoneType.Office, ZoneType.ResidentialHigh
			];
			var i = 0;
			for (var y = tl.Y; y + Block <= br.Y; y += Block)
				for (var x = tl.X; x + Block <= br.X; x += Block)
					orders.Add(CityOrders.ZoneOrder(p, new CPos(x + 1, y + 1), new CPos(x + Block - 1, y + Block - 1), zones[i++ % zones.Length]));

			// Utilities: a power plant and water towers along the first street.
			for (var x = tl.X + 1; x + 1 < br.X; x += 12)
			{
				orders.Add(CityOrders.PlaceBuildingOrder(p, "powerplant-coal", new CPos(x, tl.Y + 1)));
				orders.Add(CityOrders.PlaceBuildingOrder(p, "watertower", new CPos(x + 3, tl.Y + 1)));
				orders.Add(CityOrders.PlaceBuildingOrder(p, "watertower", new CPos(x + 4, tl.Y + 1)));
			}
		}

		long lastLogRunTime = -1;
		int lastLogTick;
		int lastLogFrame;

		void LogStats(World w)
		{
			// Real-time simulation speed since the previous log line (not part of the deterministic output).
			var runTime = Game.RunTime;
			var tps = lastLogRunTime >= 0 && runTime > lastLogRunTime ? (w.WorldTick - lastLogTick) * 1000L / (runTime - lastLogRunTime) : 0;
			var fps = lastLogRunTime >= 0 && runTime > lastLogRunTime ? (Game.RenderFrame - lastLogFrame) * 1000L / (runTime - lastLogRunTime) : 0;
			lastLogRunTime = runTime;
			lastLogTick = w.WorldTick;
			lastLogFrame = Game.RenderFrame;
			Console.WriteLine($"[autotest-perf t={w.WorldTick}] ticks/s={tps} fps={fps} zoom={worldRenderer?.Viewport.Zoom:0.##}");

			// Replays have no local player: report the first playable player's city.
			var p = w.LocalPlayer ?? w.Players.FirstOrDefault(pl => pl.Playable);
			var cm = p?.PlayerActor.TraitOrDefault<CityManager>();
			if (cm == null)
			{
				Report(w, "no CityManager on local player");
				return;
			}

			var growables = w.ActorsHavingTrait<GrowableBuilding>().ToList();
			var byZone = growables
				.GroupBy(a => a.Trait<GrowableBuilding>().Zone)
				.Select(g => $"{g.Key}:{g.Count()}");
			var roads = w.WorldActor.TraitOrDefault<RoadLayer>()?.RoadCellCount ?? -1;
			var traffic = w.WorldActor.TraitsImplementing<ITrafficService>().FirstOrDefault()?.ActiveVehicles ?? -1;

			Report(w, $"date={cm.Date} funds={cm.Funds} balance/mo={cm.MonthlyBalance} pop={cm.Population} workers={cm.Workers} jobs={cm.Jobs} " +
				$"unemployed={cm.Unemployed} demand R/C/I/O={cm.GetDemand(ZoneCategory.Residential)}/{cm.GetDemand(ZoneCategory.Commercial)}/" +
				$"{cm.GetDemand(ZoneCategory.Industrial)}/{cm.GetDemand(ZoneCategory.Office)} power={cm.PowerConsumed}/{cm.PowerProduced} " +
				$"water={cm.WaterConsumed}/{cm.WaterProduced} happy={cm.AverageHappiness} milestone={cm.MilestoneName} roads={roads} " +
				$"growables={growables.Count} [{string.Join(",", byZone)}] vehicles={traffic}");

			// Work-package reporters (world and player traits), in trait order: deterministic content only.
			foreach (var r in w.WorldActor.TraitsImplementing<ICityAutoTestReporter>())
				Report(w, "report " + r.AutoTestReport());

			foreach (var r in p.PlayerActor.TraitsImplementing<ICityAutoTestReporter>())
				Report(w, "report " + r.AutoTestReport());
		}

		static void Report(World w, string message)
		{
			var line = $"[autotest t={w.WorldTick}] {message}";
			Console.WriteLine(line);
			Log.Write("debug", line);
		}
	}
}
