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

namespace OpenRA.Mods.City.Traits
{
	// Station buildings (metro): each owns one stop on its access road cell. Lines connect stops with straight tunnels.
	public sealed partial class TransitLayer
	{
		sealed class StationRecord
		{
			public Actor Actor;
			public TransitMode Mode;
		}

		readonly List<StationRecord> stations = [];
		bool stationsDirty;

		public void RegisterStation(Actor actor, TransitMode mode)
		{
			stations.Add(new StationRecord { Actor = actor, Mode = mode });
			stationsDirty = true;
		}

		public void UnregisterStation(Actor actor)
		{
			for (var i = 0; i < stations.Count; i++)
			{
				if (stations[i].Actor != actor)
					continue;

				stations.RemoveAt(i);
				for (var s = 0; s < stops.Count; s++)
				{
					if (stops[s].StationActorId == actor.ActorID)
					{
						RemoveStopInternal(stops[s]);
						break;
					}
				}

				return;
			}
		}

		/// <summary>The rail cell next to a station building (train stations stop on the track), or CPos.Zero.</summary>
		CPos RailAccess(Actor actor)
		{
			if (rail == null || actor.OccupiesSpace == null)
				return CPos.Zero;

			var occupied = actor.OccupiesSpace.OccupiedCells();
			var cells = new List<CPos>(occupied.Length);
			for (var i = 0; i < occupied.Length; i++)
				cells.Add(occupied[i].Cell);

			cells.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
			foreach (var c in cells)
			{
				foreach (var d in CityUtils.Neighbours4)
				{
					var n = c + d;
					if (!cells.Contains(n) && rail.IsRail(n))
						return n;
				}
			}

			return CPos.Zero;
		}

		CPos WalkAccess(TransitStop stop)
		{
			if (roads.IsRoad(stop.Cell))
				return stop.Cell;

			foreach (var station in stations)
				if (station.Actor.ActorID == stop.StationActorId)
					return DepotRoad(station.Actor);

			return stop.Cell;
		}

		/// <summary>Creates or moves each station's stop to its current access road cell.</summary>
		void RefreshStations()
		{
			stationsDirty = false;
			if (roads == null)
			{
				stationsDirty = stations.Count > 0;
				return;
			}

			for (var i = 0; i < stations.Count; i++)
			{
				var rec = stations[i];
				var access = rec.Mode == TransitMode.Train ? RailAccess(rec.Actor) : DepotRoad(rec.Actor);
				TransitStop existing = null;
				for (var s = 0; s < stops.Count; s++)
					if (stops[s].StationActorId == rec.Actor.ActorID)
						existing = stops[s];

				if (access == CPos.Zero)
					continue;

				if (existing == null)
				{
					stops.Add(new TransitStop { Id = nextStopId++, Mode = rec.Mode, Cell = access, Side = DefaultSide(access), StationActorId = rec.Actor.ActorID });
					RebuildStopLineIndex();
					Version++;
				}
				else if (existing.Cell != access)
				{
					existing.Cell = access;
					for (var l = 0; l < lines.Count; l++)
						if (lines[l].StopIds.Contains(existing.Id))
							lines[l].NeedsRebuild = true;

					RebuildStopLineIndex();
					Version++;
				}
			}
		}
	}
}
