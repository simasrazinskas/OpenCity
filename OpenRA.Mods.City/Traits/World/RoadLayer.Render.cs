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
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	// World load (seeds, outside highways) and rendering: base road layer plus median, arrow and control overlays.
	public partial class RoadLayer
	{
		// Frame layouts of the road sequences: header of sequences/networks.yaml and EXPORT-LAYOUT.md.
		// Per-type sequences are named "<prefix>-<RoadType.Sequence>" (median-avenue, lanes-street, bridge-boulevard ...).
		const string JunctionPrefix = "junction";
		const string RingPrefix = "ring";
		const string TransitionName = "transition";
		const string EdgeName = "edge";

		const int TransitionSections = 10;
		const int WearBlock = 32;

		TerrainSpriteLayer render;
		TerrainSpriteLayer medianRender;
		TerrainSpriteLayer arrowRender;
		TerrainSpriteLayer laneRender;
		TerrainSpriteLayer stripRender;
		TerrainSpriteLayer controlRender;
		ISpriteSequence[] typeSequences;
		ISpriteSequence[] medianSequences;
		ISpriteSequence[] arrowSequences;
		ISpriteSequence[] laneSequences;
		ISpriteSequence[] stripSequences;
		ISpriteSequence[] bridgeSequences;
		ISpriteSequence[] junctionSequences;
		ISpriteSequence[] ringSequences;
		int[] classIndex;
		ISpriteSequence arrowSequence;
		ISpriteSequence controlSequence;
		ISpriteSequence transitionSequence;
		ISpriteSequence edgeSequence;
		PaletteReference palette;

		// Road classes that have transition art (RoadType.Sequence name -> class index of the transition sequence).
		static readonly string[] TransitionClasses = ["street", "gravel", "alley", "avenue", "boulevard"];

		int NeighbourMask(CPos cell)
		{
			edgeConnections.TryGetValue(cell, out var edge);
			var mask = 0;
			for (var i = 0; i < 4; i++)
			{
				var n = cell + CityUtils.Neighbours4[i];
				if (Connects(cell, n) || (edge == i + 1 && !map.Contains(n)))
					mask |= 1 << i;
			}

			return mask;
		}

		ISpriteSequence FindSequence(string name)
		{
			return map.Sequences.HasSequence(Info.Image, name) ? map.Sequences.GetSequence(Info.Image, name) : null;
		}

		ISpriteSequence FindSequence(RoadTypeData type, string prefix)
		{
			return FindSequence(prefix + "-" + type.Info.Sequence);
		}

		// Render-only hash of a cell (never SharedRandom): picks the wear variant of the road surface.
		static int CellHash(CPos c, int salt)
		{
			unchecked
			{
				var h = (uint)(c.X * 73856093) ^ (uint)(c.Y * 19349663) ^ (uint)(salt * 83492791);
				h ^= h >> 13;
				h *= 1274126177;
				h ^= h >> 16;
				return (int)(h & 0x7fffffff);
			}
		}

		// 0 used (55%), 1 new (25%), 2 worn (20%).
		static int WearVariant(CPos c)
		{
			var h = CellHash(c, 7) % 100;
			return h < 55 ? 0 : h < 80 ? 1 : 2;
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			// Highways first so that seeds on top of them are no-ops.
			foreach (var kv in w.ActorsWithTrait<OutsideConnection>())
			{
				hasOutsideConnections = true;
				LayHighway(kv.Actor, kv.Trait.Info);
			}

			var seeds = new List<Actor>();
			foreach (var a in w.Actors)
				if (a.Info.Name == Info.SeedActor && !a.IsDead)
					seeds.Add(a);

			foreach (var seed in seeds)
			{
				var cell = seed.Location;
				if (AddRoadInner(cell, DefaultTypeId, -1, false, false, true))
					ConstructionUtils.ClearAutoClearActors(w, cell);
			}

			if (seeds.Count > 0)
			{
				w.AddFrameEndTask(_ =>
				{
					foreach (var seed in seeds)
						if (!seed.Disposed)
							seed.Dispose();
				});
			}

			// Rendering.
			var n = types.Count + 1;
			typeSequences = new ISpriteSequence[n];
			medianSequences = new ISpriteSequence[n];
			arrowSequences = new ISpriteSequence[n];
			laneSequences = new ISpriteSequence[n];
			stripSequences = new ISpriteSequence[n];
			bridgeSequences = new ISpriteSequence[n];
			junctionSequences = new ISpriteSequence[n];
			ringSequences = new ISpriteSequence[n];
			classIndex = new int[n];
			foreach (var t in types)
			{
				typeSequences[t.Id] = map.Sequences.GetSequence(t.Info.Image, t.Info.Sequence);
				medianSequences[t.Id] = FindSequence(t, Info.MedianSequence);
				arrowSequences[t.Id] = FindSequence(t, Info.ArrowSequence);
				laneSequences[t.Id] = FindSequence(t, Info.LaneSequence);
				stripSequences[t.Id] = FindSequence(t, Info.StripSequence);
				bridgeSequences[t.Id] = FindSequence(t, Info.BridgeSequence);
				junctionSequences[t.Id] = FindSequence(t, JunctionPrefix);
				ringSequences[t.Id] = FindSequence(t, RingPrefix);
				classIndex[t.Id] = Array.IndexOf(TransitionClasses, t.Info.Sequence);
			}

			arrowSequence = map.Sequences.GetSequence(Info.Image, Info.ArrowSequence);
			controlSequence = map.Sequences.GetSequence(Info.Image, Info.ControlSequence);
			transitionSequence = FindSequence(TransitionName);
			edgeSequence = FindSequence(EdgeName);
			palette = wr.Palette(Info.Palette);

			var first = typeSequences[1].GetSprite(0);
			var emptySprite = new Sprite(first.Sheet, Rectangle.Empty, TextureChannel.Alpha);
			render = new TerrainSpriteLayer(w, wr, emptySprite, first.BlendMode, true);
			medianRender = new TerrainSpriteLayer(w, wr, emptySprite, first.BlendMode, true);
			arrowRender = new TerrainSpriteLayer(w, wr, emptySprite, first.BlendMode, true);
			controlRender = new TerrainSpriteLayer(w, wr, emptySprite, first.BlendMode, true);
			laneRender = new TerrainSpriteLayer(w, wr, emptySprite, first.BlendMode, true);
			stripRender = new TerrainSpriteLayer(w, wr, emptySprite, first.BlendMode, true);

			foreach (var c in roadCells)
				MarkDirty(c);

			FlushDirty();
		}

		void LayHighway(Actor actor, OutsideConnectionInfo info)
		{
			var origin = actor.Location;
			var dirVec = info.Direction;
			var last = CPos.Zero;
			for (var i = 0; i < info.Length; i++)
			{
				var cell = origin + new CVec(dirVec.X * i, dirVec.Y * i);
				if (!map.Contains(cell))
					continue;

				ConstructionUtils.ClearAutoClearActors(world, cell);
				AddRoadInner(cell, outsideTypeId, -1, false, true, false);
				last = cell;
			}

			// The inner end is a ramp so that the streets laid there by maps and players keep connecting.
			if (last != CPos.Zero && IsRoad(last) && IsHighwayClass(last))
				SetRamp(last, true);

			// The road continues off the map: draw the first cell connected towards the edge.
			var back = new CVec(-Math.Sign(dirVec.X), -Math.Sign(dirVec.Y));
			var bi = DirIndex(back);
			if (bi >= 0)
			{
				edgeConnections[origin] = bi + 1;
				MarkDirty(origin);
			}
		}

		void FlushDirty()
		{
			if (render == null || dirtyCells.Count == 0)
				return;

			foreach (var c in dirtyCells)
			{
				if (IsRoad(c))
					UpdateCell(c);
				else
				{
					render.Clear(c);
					medianRender.Clear(c);
					arrowRender.Clear(c);
					laneRender.Clear(c);
					stripRender.Clear(c);
					if (islands.Contains(c))
						controlRender.Update(c, controlSequence, palette, IslandFrame(c));
					else
						controlRender.Clear(c);
				}

				PropsChanged?.Invoke(c);
			}

			dirtyCells.Clear();
			PropsVersion++;
		}

		// Control sequence: 4 is a single-cell island, 5 + row * 3 + column a cell of a 3x3 island.
		int IslandFrame(CPos c)
		{
			var left = islands.Contains(c + new CVec(-1, 0));
			var right = islands.Contains(c + new CVec(1, 0));
			var up = islands.Contains(c + new CVec(0, -1));
			var down = islands.Contains(c + new CVec(0, 1));
			if (!left && !right && !up && !down)
				return 4;

			var gx = !left ? 0 : !right ? 2 : 1;
			var gy = !up ? 0 : !down ? 2 : 1;
			return 5 + gy * 3 + gx;
		}

		// Section of the transition art (class * 2 + one-way) of a ground road cell, or -1 (bridges, highways, no art).
		int TransitionSection(CPos c)
		{
			if (!IsRoad(c) || (bridge[c] & BridgeBit) != 0)
				return -1;

			var ci = classIndex[typeId[c]];
			return ci < 0 ? -1 : ci * 2 + ((dir[c] & OneWayMask) != 0 ? 1 : 0);
		}

		// Straight cell between two different road classes: the transition piece that matches both neighbours (-1: none).
		int TransitionFrame(CPos c, int arms)
		{
			if (transitionSequence == null || (arms != 5 && arms != 10) || (dir[c] & RampBit) != 0)
				return -1;

			var own = TransitionSection(c);
			if (own < 0)
				return -1;

			var ew = arms == 10;
			var ca = c + (ew ? new CVec(-1, 0) : new CVec(0, -1));
			var cb = c + (ew ? new CVec(1, 0) : new CVec(0, 1));
			var sa = Connects(c, ca) ? TransitionSection(ca) : -1;
			var sb = Connects(c, cb) ? TransitionSection(cb) : -1;
			if (sa < 0)
				sa = own;

			if (sb < 0)
				sb = own;

			var ownClass = own / 2;
			if (sa / 2 == ownClass && sb / 2 == ownClass)
				return -1;

			if (sa / 2 == ownClass)
				sa = own;
			else if (sb / 2 == ownClass)
				sb = own;
			else if (sa / 2 == sb / 2)
				return -1;

			return (sa * TransitionSections + sb) * 2 + (ew ? 1 : 0);
		}

		// Piece of a bridge deck: 0 end0 (land at the N / W edge), 1 span, 2 pier, 3 end1 (land at the S / E edge).
		int BridgePiece(CPos c, bool ew)
		{
			var back = c + (ew ? new CVec(-1, 0) : new CVec(0, -1));
			var front = c + (ew ? new CVec(1, 0) : new CVec(0, 1));
			var hasBack = IsBridge(back) && BridgeIsEastWest(back) == ew;
			var hasFront = IsBridge(front) && BridgeIsEastWest(front) == ew;
			if (!hasBack && hasFront)
				return 0;

			if (hasBack && !hasFront)
				return 3;

			if (hasBack && (ew ? c.X : c.Y) % 3 == 0)
				return 2;

			return 1;
		}

		bool IsRingCell(CPos c)
		{
			return IsRoad(c) && (dir[c] & OneWayMask) != 0 && control[c] == (byte)JunctionControl.Roundabout + 1;
		}

		// Arms of a roundabout ring cell that lead away from the ring (the entries and exits).
		int RingEntries(CPos c, int arms)
		{
			var entries = 0;
			for (var i = 0; i < 4; i++)
				if ((arms & (1 << i)) != 0 && !IsRingCell(c + CityUtils.Neighbours4[i]))
					entries |= 1 << i;

			return entries;
		}

		// Direction (N, E, S, W) of the street end of a highway ramp cell.
		int RampDirection(CPos c, int arms)
		{
			var first = 0;
			for (var i = 0; i < 4; i++)
			{
				if ((arms & (1 << i)) == 0)
					continue;

				first = i;
				var n = c + CityUtils.Neighbours4[i];
				if (IsRoad(n) && !types[typeId[n] - 1].IsHighway)
					return i;
			}

			return first;
		}

		// Overlay frame of the junction control paint (or the ramp / map edge marking): sequence and frame, or null.
		ISpriteSequence ControlOverlay(CPos c, byte type, int arms, bool oneWay, out int frame)
		{
			frame = 0;
			if ((dir[c] & RampBit) != 0)
			{
				frame = RampDirection(c, arms);
				return controlSequence;
			}

			if (edgeSequence != null && edgeConnections.TryGetValue(c, out var edge) && edge > 0)
			{
				frame = edge - 1;
				return edgeSequence;
			}

			var jc = GetControl(c);
			switch (jc)
			{
				case JunctionControl.Roundabout:
					if (oneWay)
					{
						frame = RingEntries(c, arms) * 16 + arms;
						return ringSequences[type];
					}

					frame = 3 * 16 + arms;
					return junctionSequences[type];
				case JunctionControl.Stop:
					frame = (oneWay ? 64 : 0) + arms;
					return junctionSequences[type];
				case JunctionControl.Signal:
					frame = (oneWay ? 64 : 0) + 16 + arms;
					return junctionSequences[type];
				case JunctionControl.Yield:
					frame = (oneWay ? 64 : 0) + 32 + arms;
					return junctionSequences[type];
				default:
					return null;
			}
		}

		void UpdateCell(CPos c)
		{
			var type = typeId[c];
			var oneWay = (dir[c] & OneWayMask) - 1;
			var ow = oneWay >= 0 ? 1 : 0;
			var arms = NeighbourMask(c);
			var onBridge = (bridge[c] & BridgeBit) != 0;
			var bridgeSequence = bridgeSequences[type];

			if (onBridge && bridgeSequence != null)
			{
				var ew = (bridge[c] & BridgeEwBit) != 0;
				render.Update(c, bridgeSequence, palette, ((ew ? 1 : 0) * 2 + ow) * 4 + BridgePiece(c, ew));
			}
			else
			{
				var transition = onBridge ? -1 : TransitionFrame(c, arms);
				if (transition >= 0)
					render.Update(c, transitionSequence, palette, transition);
				else
					render.Update(c, typeSequences[type], palette, WearVariant(c) * WearBlock + ow * 16 + arms);
			}

			// Paint that lies on the ground does not belong on a raised bridge deck.
			var ground = !onBridge;
			var lanes = (addons[c] >> 4) & 3;
			var laneSequence = laneSequences[type];
			if (ground && lanes != 0 && laneSequence != null)
				laneRender.Update(c, laneSequence, palette, (ow * 3 + lanes - 1) * 16 + arms);
			else
				laneRender.Clear(c);

			var strips = addons[c] & 15;
			var stripSequence = stripSequences[type];
			if (ground && (strips & (int)RoadAddons.Parking) != 0 && stripSequence != null)
				stripRender.Update(c, stripSequence, palette, (strips - 1) * 16 + (~arms & 15));
			else
				stripRender.Clear(c);

			var medianSequence = medianSequences[type];
			if (ground && sideBlock[c] != 0 && medianSequence != null)
				medianRender.Update(c, medianSequence, palette, sideBlock[c]);
			else
				medianRender.Clear(c);

			if (ground && oneWay >= 0)
				arrowRender.Update(c, arrowSequences[type] ?? arrowSequence, palette, oneWay);
			else
				arrowRender.Clear(c);

			var overlayFrame = 0;
			var overlay = ground ? ControlOverlay(c, type, arms, oneWay >= 0, out overlayFrame) : null;
			if (overlay != null)
				controlRender.Update(c, overlay, palette, overlayFrame);
			else
				controlRender.Clear(c);
		}

		void ITickRender.TickRender(WorldRenderer wr, Actor self)
		{
			FlushDirty();
		}

		void IRenderOverlay.Render(WorldRenderer wr)
		{
			render?.Draw(wr.Viewport);
			laneRender?.Draw(wr.Viewport);
			medianRender?.Draw(wr.Viewport);
			stripRender?.Draw(wr.Viewport);
			arrowRender?.Draw(wr.Viewport);
			controlRender?.Draw(wr.Viewport);
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (disposed)
				return;

			render?.Dispose();
			medianRender?.Dispose();
			arrowRender?.Dispose();
			laneRender?.Dispose();
			stripRender?.Dispose();
			controlRender?.Dispose();
			disposed = true;
		}
	}
}
