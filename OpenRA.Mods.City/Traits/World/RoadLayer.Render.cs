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
		TerrainSpriteLayer render;
		TerrainSpriteLayer medianRender;
		TerrainSpriteLayer arrowRender;
		TerrainSpriteLayer laneRender;
		TerrainSpriteLayer stripRender;
		TerrainSpriteLayer controlRender;
		ISpriteSequence[] typeSequences;
		ISpriteSequence medianSequence;
		ISpriteSequence arrowSequence;
		ISpriteSequence controlSequence;
		ISpriteSequence laneSequence;
		ISpriteSequence stripSequence;
		ISpriteSequence bridgeSequence;
		PaletteReference palette;

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
			typeSequences = new ISpriteSequence[types.Count + 1];
			foreach (var t in types)
				typeSequences[t.Id] = map.Sequences.GetSequence(t.Info.Image, t.Info.Sequence);

			medianSequence = map.Sequences.GetSequence(Info.Image, Info.MedianSequence);
			arrowSequence = map.Sequences.GetSequence(Info.Image, Info.ArrowSequence);
			controlSequence = map.Sequences.GetSequence(Info.Image, Info.ControlSequence);
			laneSequence = map.Sequences.GetSequence(Info.Image, Info.LaneSequence);
			stripSequence = map.Sequences.GetSequence(Info.Image, Info.StripSequence);
			bridgeSequence = map.Sequences.GetSequence(Info.Image, Info.BridgeSequence);
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
						controlRender.Update(c, controlSequence, palette, 4);
					else
						controlRender.Clear(c);
				}
			}

			dirtyCells.Clear();
		}

		void UpdateCell(CPos c)
		{
			var oneWay = (dir[c] & OneWayMask) - 1;
			var arms = NeighbourMask(c);
			var frame = arms + (oneWay >= 0 ? 16 : 0);
			if ((bridge[c] & BridgeBit) != 0)
				render.Update(c, bridgeSequence, palette, ((bridge[c] & BridgeEwBit) != 0 ? 1 : 0) + (oneWay >= 0 ? 2 : 0));
			else
				render.Update(c, typeSequences[typeId[c]], palette, frame);

			var lanes = (addons[c] >> 4) & 3;
			if (lanes != 0)
				laneRender.Update(c, laneSequence, palette, (lanes - 1) * 16 + arms);
			else
				laneRender.Clear(c);

			var strips = addons[c] & 15;
			if (strips != 0)
				stripRender.Update(c, stripSequence, palette, (strips - 1) * 16 + (~arms & 15));
			else
				stripRender.Clear(c);

			if (sideBlock[c] != 0)
				medianRender.Update(c, medianSequence, palette, sideBlock[c]);
			else
				medianRender.Clear(c);

			if (oneWay >= 0)
				arrowRender.Update(c, arrowSequence, palette, oneWay);
			else
				arrowRender.Clear(c);

			var overlay = -1;
			var jc = GetControl(c);
			if ((dir[c] & RampBit) != 0)
				overlay = 3;
			else if (jc == JunctionControl.Stop)
				overlay = 0;
			else if (jc == JunctionControl.Signal)
				overlay = 1;
			else if (jc == JunctionControl.Roundabout)
				overlay = 2;

			if (overlay >= 0)
				controlRender.Update(c, controlSequence, palette, overlay);
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
