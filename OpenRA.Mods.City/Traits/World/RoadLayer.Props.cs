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

namespace OpenRA.Mods.City.Traits
{
	/// <summary>
	/// A tall road prop (signal, sign, lamp, tree, barrier, gantry) that stands on a road cell. Render-only data: the renderer
	/// draws frame <see cref="Frame"/> of sequence <see cref="Sequence"/> of image <see cref="RoadLayer.PropImage"/> at
	/// cell centre + <see cref="Offset"/> (the sprite is anchored at the foot of the prop), depth-sorted with the actors.
	/// </summary>
	public readonly struct RoadProp
	{
		/// <summary>Sequence of the roadprops image (summer / default variant).</summary>
		public readonly string Sequence;

		/// <summary>Companion sequence with the emissive pixels only (same frame, drawn at ZOffset + 1 at night, not ambient tinted), or null.</summary>
		public readonly string LitSequence;

		public readonly int Frame;

		/// <summary>World offset (1024 per cell, Z in world units = 1/32 px) from the cell centre to the foot of the prop.</summary>
		public readonly WVec Offset;

		public readonly int ZOffset;

		/// <summary>True for emissive ground decals (lamp light pools): draw only at night, after the ambient tint.</summary>
		public readonly bool NightOnly;

		/// <summary>True when the sequence has season variants ("-spring", "-autumn", "-winter" appended; summer = no suffix).</summary>
		public readonly bool Seasonal;

		/// <summary>Arm (0 N, 1 E, 2 S, 3 W) served by a traffic signal (Sequence signal-red, LitSequence signal-red-lit: swap red for amber / green), else -1.</summary>
		public readonly int SignalArm;

		public RoadProp(string sequence, int frame, WVec offset, string litSequence = null, int zOffset = 0, bool nightOnly = false,
			bool seasonal = false, int signalArm = -1)
		{
			Sequence = sequence;
			LitSequence = litSequence;
			Frame = frame;
			Offset = offset;
			ZOffset = zOffset;
			NightOnly = nightOnly;
			Seasonal = seasonal;
			SignalArm = signalArm;
		}

		/// <summary>Sequence of a traffic signal showing state "red", "amber" or "green" (lit = emissive companion).</summary>
		public static string SignalSequence(string state, bool lit)
		{
			return lit ? "signal-" + state + "-lit" : "signal-" + state;
		}

		/// <summary>Season suffix of the seasonal prop sequences (0 spring, 1 summer, 2 autumn, 3 winter).</summary>
		public static string SeasonSuffix(int season)
		{
			return season == 0 ? "-spring" : season == 2 ? "-autumn" : season == 3 ? "-winter" : "";
		}
	}

	// Road props: which tall sprites stand on a road cell (render-only, derived from the cell's state and a cell hash).
	public partial class RoadLayer
	{
		/// <summary>Image of the prop sequences (layout in the header of sequences/networks.yaml).</summary>
		public const string PropImage = "roadprops";

		// Deck height of a bridge in world units (8 px).
		const int BridgeDeckZ = 256;

		// Offsets of the foot of side props (7/16 cell from the cell centre) and corner props (3/8 cell on both axes), by N, E, S, W.
		static readonly WVec[] SideOffsets = [new(0, -448, 0), new(448, 0, 0), new(0, 448, 0), new(-448, 0, 0)];
		static readonly WVec[] CornerOffsets = [new(-384, -384, 0), new(384, -384, 0), new(384, 384, 0), new(-384, 384, 0)];

		/// <summary>Raised after a road cell (or an island) was redrawn: the props of this cell may have changed. Fires at render time.</summary>
		public event Action<CPos> PropsChanged;

		/// <summary>Incremented after every flush that redrew cells; compare to a cached value to know whether any prop changed.</summary>
		public int PropsVersion { get; private set; }

		// Free sides (no arm) of a cell that have a road running along them, so a sidewalk lines that edge.
		static int LinedSides(int arms)
		{
			var lined = 0;
			if ((arms & 10) == 10)
				lined |= (~arms & 1) | (~arms & 4);

			if ((arms & 5) == 5)
				lined |= (~arms & 2) | (~arms & 8);

			return lined;
		}

		/// <summary>
		/// Appends the tall props standing on a road cell: street lamps and trees on lined sides, sound barriers and guard rails,
		/// median trees and lamps, traffic signals and stop / yield signs on junction corners, and the sign gantry of a map-edge highway.
		/// Offsets and frames are documented in the header of sequences/networks.yaml. Render-only; the list is not cleared.
		/// </summary>
		public void GetProps(CPos cell, List<RoadProp> props)
		{
			if (!IsRoad(cell))
				return;

			var type = types[typeId[cell] - 1];
			var arms = NeighbourMask(cell);
			var lined = LinedSides(arms);
			var addon = addons[cell] & 15;
			var onBridge = (bridge[cell] & BridgeBit) != 0;
			var lift = onBridge ? BridgeDeckZ : 0;
			var parity = (cell.X + cell.Y) & 1;
			var oneWay = (dir[cell] & OneWayMask) != 0;
			var boulevard = type.Info.Sequence == "boulevard";
			var highway = type.IsHighway;

			// Lamps: every second cell, alternating between the two sides of the street.
			if ((addon & (int)RoadAddons.Lights) != 0)
			{
				var heritage = type.Info.Sequence == "alley";
				var median = boulevard && !oneWay && sideBlock[cell] == 0 && (arms == 5 || arms == 10);
				if (median)
				{
					if (parity == 0)
						props.Add(new RoadProp("lamp-double", arms == 5 ? 0 : 1, new WVec(0, 0, lift), "lamp-double-lit"));
				}
				else
				{
					for (var i = 0; i < 4; i++)
					{
						if ((lined & (1 << i)) == 0 || parity != i >> 1)
							continue;

						var offset = SideOffsets[i] + new WVec(0, 0, lift);
						props.Add(new RoadProp(heritage ? "lamp-heritage" : "lamp", i, offset, heritage ? "lamp-heritage-lit" : "lamp-lit"));
						props.Add(new RoadProp("lamp-pool", i, new WVec(0, 0, lift), null, -256, true));
					}
				}
			}

			if (!onBridge)
			{
				// Street trees on every lined side (two variants chosen by a cell hash).
				if ((addon & (int)RoadAddons.Trees) != 0)
				{
					for (var i = 0; i < 4; i++)
						if ((lined & (1 << i)) != 0)
							props.Add(new RoadProp("tree", (CellHash(cell, 20 + i) & 1) * 4 + i, SideOffsets[i], seasonal: true));
				}

				if ((addon & (int)RoadAddons.Barrier) != 0)
				{
					for (var i = 0; i < 4; i++)
						if ((lined & (1 << i)) != 0)
							props.Add(new RoadProp("barrier", i, SideOffsets[i]));
				}
				else if (highway)
				{
					for (var i = 0; i < 4; i++)
						if ((lined & (1 << i)) != 0)
							props.Add(new RoadProp("guardrail", i, SideOffsets[i]));
				}

				// Boulevard medians: trees along the carriageway pair's median, or in the centre median of a two-way boulevard.
				if (boulevard)
				{
					var sb = sideBlock[cell];
					if (sb != 0)
					{
						for (var i = 0; i < 4; i++)
							if ((sb & lined & (1 << i)) != 0 && parity == i >> 1)
								props.Add(new RoadProp("median-tree", i, SideOffsets[i], seasonal: true));
					}
					else if (!oneWay && (arms == 5 || arms == 10))
						props.Add(new RoadProp("median-tree-c", arms == 5 ? 0 : 1, WVec.Zero, seasonal: true));
				}
			}

			// Junction furniture: a signal or sign at the right-hand corner of every arm that meets the junction.
			if ((arms & (arms - 1)) != 0 && !onBridge)
			{
				var control = GetControl(cell);
				if (control == JunctionControl.Signal)
				{
					for (var i = 0; i < 4; i++)
						if ((arms & (1 << i)) != 0)
							props.Add(new RoadProp("signal-red", i, CornerOffsets[i], "signal-red-lit", signalArm: i));
				}
				else if (control == JunctionControl.Stop || control == JunctionControl.Yield)
				{
					var sign = control == JunctionControl.Stop ? "sign-stop" : "sign-yield";
					for (var i = 0; i < 4; i++)
						if ((arms & (1 << i)) != 0)
							props.Add(new RoadProp(sign, i, CornerOffsets[i]));
				}
				else if (control == JunctionControl.Roundabout && oneWay)
				{
					var entries = RingEntries(cell, arms);
					for (var i = 0; i < 4; i++)
						if ((entries & (1 << i)) != 0)
							props.Add(new RoadProp("sign-yield", i, CornerOffsets[i]));
				}
			}

			// Map-edge highway: sign gantry across the carriageway.
			if (highway && edgeConnections.TryGetValue(cell, out var edge) && edge > 0)
				props.Add(new RoadProp("gantry", (edge - 1) % 2 == 0 ? 0 : 1, WVec.Zero));
		}
	}
}
