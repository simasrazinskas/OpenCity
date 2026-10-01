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
	public struct TransitStopEntry
	{
		public int Id;

		/// <summary>"bus", "taxi", "tram", "metro" or "train".</summary>
		public string Mode;
		public CPos Cell;

		/// <summary>0..3 = side of the road (N, E, S, W) the sign is drawn on.</summary>
		public int Side;
		public int Waiting, Lines;

		/// <summary>Actor id of the station building for metro and train stops, 0 for road stops and taxi stands.</summary>
		public uint StationActorId;
	}

	public struct TransitDepotEntry
	{
		public uint ActorId;
		public string Mode;
		public int Used, Capacity;

		/// <summary>Road cell vehicles leave from, CPos.Zero when the depot has no road access.</summary>
		public CPos Road;
	}

	/// <summary>
	/// Wave 2 read side for the transit UI (stops and taxi stands, depots, track, placement and drag checks). Implemented by TransitLayer;
	/// fetch with <c>world.WorldActor.TraitsImplementing&lt;ITransitUiSourceEx&gt;()</c>. Everything is read only.
	/// </summary>
	public interface ITransitUiSourceEx
	{
		/// <summary>Bumped whenever stops, lines, depots or track change (cache key).</summary>
		int Version { get; }

		IReadOnlyList<TransitStopEntry> StopEntries { get; }

		IReadOnlyList<TransitDepotEntry> DepotEntries { get; }

		/// <summary>Fluent error key why a stop or taxi stand of this mode cannot be placed on this cell now ("bus", "taxi", "tram"), or null when it can. For tool cursors.</summary>
		string CheckStop(CPos cell, string mode);

		/// <summary>Plan of a tram or rail track drag ("tram" or "rail"): the cells that get built, level crossings, cost and the error that stops the drag.</summary>
		TrackPlan PlanTrack(string kind, CPos from, CPos to);

		bool HasTramTrack(CPos cell);

		bool HasRail(CPos cell);

		bool IsLevelCrossing(CPos cell);

		/// <summary>False while the progression has not unlocked the mode ("tram", "train", "metro", "bus", "taxi").</summary>
		bool IsModeUnlocked(string mode);

		int StopCost { get; }

		int TramTrackCostPerCell { get; }

		int RailCostPerCell { get; }

		/// <summary>Cost of the tunnel of a metro line through these stops.</summary>
		int MetroTunnelCost(IList<int> stopIds, bool loop);

		/// <summary>Route preview for the line tool of any mode (the stop-to-stop path in cells, whether a leg is cut, the cycle time in ticks).</summary>
		List<CPos> PreviewLine(string mode, IList<int> stopIds, bool loop, out bool broken, out int cycleTicks);
	}
}
