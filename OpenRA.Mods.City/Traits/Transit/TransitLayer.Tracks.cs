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
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	// Tram track (a flag on road cells), rail (RailLayer) and the routers for tram and train lines.
	public sealed partial class TransitLayer : IRenderOverlay, INotifyActorDisposing
	{
		public const string ErrorNeedsTrack = "notification-transit-needs-track";
		public const string ErrorTrackNeedsRoad = "notification-transit-track-needs-road";

		CellLayer<byte> tram;
		TrackOverlay tramOverlay;
		RailLayer rail;
		TransitRouter tramRouter, railRouter, previewTramRouter, previewRailRouter;
		int railVersion = -1;
		bool overlayDisposed;

		/// <summary>Bumped when tram track changes.</summary>
		public int TramVersion { get; private set; }

		void InitTracks(World w, WorldRenderer wr)
		{
			tram = new CellLayer<byte>(w.Map);
			tramOverlay = new TrackOverlay(w, wr, "tram-track", "track", "terrain");
			rail = w.WorldActor.TraitOrDefault<RailLayer>();
			if (roads != null)
			{
				tramRouter = new TransitRouter(w.Map, c => IsTramTrack(c) && roads.IsRoad(c), roads.CanEnter);
				previewTramRouter = new TransitRouter(w.Map, c => IsTramTrack(c) && roads.IsRoad(c), roads.CanEnter);
			}

			if (rail != null)
			{
				railRouter = new TransitRouter(w.Map, rail.IsRail, rail.CanEnter);
				previewRailRouter = new TransitRouter(w.Map, rail.IsRail, rail.CanEnter);
			}
		}

		public bool IsTramTrack(CPos cell) => tram != null && tram.Contains(cell) && tram[cell] != 0;

		public int TramTrackCount { get; private set; }

		/// <summary>Tram and train need their progression unlock (the tramdepot / trainstation keys); the other modes are open.</summary>
		public bool ModeUnlocked(TransitMode mode)
		{
			if (!Info.RequireUnlocks || (mode != TransitMode.Tram && mode != TransitMode.Train))
				return true;

			var p = Progression();
			return p == null || p.IsUnlocked(mode == TransitMode.Tram ? "tramdepot" : "trainstation");
		}

		/// <summary>Plans a tram track drag over existing road cells. `funds` &lt; 0 means unlimited.</summary>
		public TrackPlan PlanTram(CPos from, CPos to, int funds)
		{
			var plan = new TrackPlan { Path = CityUtils.RoadPath(from, to) };
			var progress = Progression();
			foreach (var cell in plan.Path)
			{
				if (roads == null || !roads.IsRoad(cell))
				{
					plan.ErrorKey = ErrorTrackNeedsRoad;
					break;
				}

				if (IsTramTrack(cell))
					continue;

				if (progress != null && !progress.IsCellOwned(cell))
				{
					plan.ErrorKey = ErrorNotOwned;
					break;
				}

				if (funds >= 0 && plan.Cost + Info.TramTrackCostPerCell > funds)
				{
					plan.ErrorKey = ErrorMoney;
					break;
				}

				plan.Cost += Info.TramTrackCostPerCell;
				plan.Build.Add(cell);
			}

			return plan;
		}

		/// <summary>Plan a rail or tram track drag for the UI preview ("tram" or "rail").</summary>
		public TrackPlan PlanTrack(string kind, CPos from, CPos to)
		{
			var cm = Funds();
			var funds = cm == null || cm.UnlimitedMoney ? -1 : Math.Max(0, cm.Funds);
			if (kind == "rail" && rail != null)
				return rail.Plan(from, to, funds);

			return PlanTram(from, to, funds);
		}

		/// <summary>Build or remove track. Returns a fluent error key or null; a partly built drag still builds its valid prefix.</summary>
		public string BuildTrack(string kind, CPos from, CPos to, bool remove, CityManager cm)
		{
			if (!ModeUnlocked(kind == "rail" ? TransitMode.Train : TransitMode.Tram))
				return ErrorLocked;

			if (kind == "rail")
				return remove ? RemoveRail(from, to, cm) : BuildRail(from, to, cm);

			if (kind != "tram" || tram == null)
				return ErrorNotSupported;

			if (remove)
				return RemoveTram(from, to, cm);

			var plan = PlanTram(from, to, cm == null || cm.UnlimitedMoney ? -1 : Math.Max(0, cm.Funds));
			if (plan.Build.Count == 0)
				return plan.ErrorKey;

			if (cm != null && !cm.TrySpend(plan.Cost, ConstructionKey))
				return ErrorMoney;

			foreach (var c in plan.Build)
			{
				tram[c] = 1;
				TramTrackCount++;
				RefreshTram(c);
				foreach (var d in CityUtils.Neighbours4)
					RefreshTram(c + d);
			}

			TracksChanged(TransitMode.Tram);
			return plan.ErrorKey;
		}

		string BuildRail(CPos from, CPos to, CityManager cm)
		{
			if (rail == null)
				return ErrorNotSupported;

			var plan = rail.Plan(from, to, cm == null || cm.UnlimitedMoney ? -1 : Math.Max(0, cm.Funds));
			if (plan.Build.Count == 0)
				return plan.ErrorKey;

			if (cm != null && !cm.TrySpend(plan.Cost, ConstructionKey))
				return RailLayer.ErrorMoney;

			rail.Apply(plan);
			return plan.ErrorKey;
		}

		string RemoveRail(CPos from, CPos to, CityManager cm)
		{
			if (rail == null)
				return ErrorNotSupported;

			var n = rail.Remove(from, to);
			cm?.AddFunds(n * rail.Info.CostPerCell * rail.Info.RefundPercent / 100, "refund");
			return n == 0 ? ErrorNoStop : null;
		}

		string RemoveTram(CPos from, CPos to, CityManager cm)
		{
			var n = 0;
			foreach (var c in CityUtils.RoadPath(from, to))
			{
				if (!IsTramTrack(c))
					continue;

				tram[c] = 0;
				TramTrackCount--;
				n++;
				RefreshTram(c);
				foreach (var d in CityUtils.Neighbours4)
					RefreshTram(c + d);
			}

			if (n == 0)
				return ErrorNoStop;

			cm?.AddFunds(n * Info.TramTrackCostPerCell / 2, "refund");
			TracksChanged(TransitMode.Tram);
			return null;
		}

		/// <summary>Lines of a mode must be re-routed (and may break) after track changed.</summary>
		void TracksChanged(TransitMode mode)
		{
			if (mode == TransitMode.Tram)
				TramVersion++;

			for (var i = 0; i < lines.Count; i++)
				if (lines[i].Mode == mode)
					lines[i].NeedsRebuild = true;

			Version++;
		}

		/// <summary>Drops tram track from cells that are no longer roads (the road was bulldozed).</summary>
		void PurgeTram()
		{
			if (tram == null || TramTrackCount == 0)
				return;

			var removed = false;
			var b = world.Map.Bounds;
			for (var v = b.Top; v < b.Bottom; v++)
			{
				for (var u = b.Left; u < b.Right; u++)
				{
					var c = new MPos(u, v).ToCPos(world.Map);
					if (tram[c] != 0 && !roads.IsRoad(c))
					{
						tram[c] = 0;
						TramTrackCount--;
						removed = true;
						RefreshTram(c);
						foreach (var d in CityUtils.Neighbours4)
							RefreshTram(c + d);
					}
				}
			}

			if (removed)
				TracksChanged(TransitMode.Tram);
		}

		void RefreshTram(CPos cell)
		{
			if (tramOverlay == null || !tram.Contains(cell))
				return;

			if (tram[cell] == 0)
			{
				tramOverlay.Clear(cell);
				return;
			}

			var mask = 0;
			for (var i = 0; i < 4; i++)
				if (IsTramTrack(cell + CityUtils.Neighbours4[i]))
					mask |= 1 << i;

			tramOverlay.Set(cell, mask);
		}

		/// <summary>Called each tick: re-route train lines and refresh stations when the rail network changed.</summary>
		void CheckRailVersion()
		{
			if (rail == null || rail.NetworkVersion == railVersion)
				return;

			railVersion = rail.NetworkVersion;
			stationsDirty = true;
			TracksChanged(TransitMode.Train);
		}

		TransitRouter RouterFor(TransitMode mode, bool preview)
		{
			return mode switch
			{
				TransitMode.Tram => preview ? previewTramRouter : tramRouter,
				TransitMode.Train => preview ? previewRailRouter : railRouter,
				_ => preview ? previewRouter : router,
			};
		}

		/// <summary>Cells a vehicle of this mode drives from stop a to stop b, or null when unreachable.</summary>
		CPos[] Route(TransitMode mode, CPos a, CPos b, bool preview)
		{
			if (mode == TransitMode.Metro)
				return CityUtils.RoadPath(a, b).ToArray();

			return RouterFor(mode, preview)?.FindPath(a, b);
		}

		void IRenderOverlay.Render(WorldRenderer wr)
		{
			tramOverlay?.Draw(wr.Viewport);
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (overlayDisposed)
				return;

			tramOverlay?.Dispose();
			overlayDisposed = true;
		}
	}
}
