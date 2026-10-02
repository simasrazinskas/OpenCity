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
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Draws the HV power lines (always) and the water and sewage pipes (underground view: the water, sewage and power info views).")]
	public class UtilityOverlayInfo : TraitInfo, Requires<UtilityNetworkInfo>, NotBefore<InfoViewLayerInfo>
	{
		[Desc("Sprite image of the tall line props (pylons, distribution poles), see UtilityOverlay.GetProps.")]
		public readonly string PropImage = "utilprops";

		[SequenceReference(nameof(PropImage))]
		[Desc("16 frames (neighbour mask): lattice pylon with wires for a high-voltage line cell.")]
		public readonly string PylonSequence = "hv";

		[SequenceReference(nameof(PropImage))]
		[Desc("16 frames (neighbour mask): wooden pole with wires for a road cell that carries the low-voltage cable.")]
		public readonly string PoleSequence = "lv";

		[PaletteReference]
		[Desc("Palette the prop sprites are drawn with.")]
		public readonly string PropPalette = "city";

		public override object Create(ActorInitializer init) { return new UtilityOverlay(init.Self, this); }
	}

	/// <summary>A tall utility prop (render only): draw it as a sorted sprite at the cell centre plus Offset.</summary>
	public readonly struct UtilityProp
	{
		public readonly ISpriteSequence Sequence;
		public readonly int Frame;
		public readonly WVec Offset;
		public readonly int ZOffset;
		public readonly bool Cable;

		public UtilityProp(ISpriteSequence sequence, int frame, WVec offset, int zOffset, bool cable)
		{
			Sequence = sequence;
			Frame = frame;
			Offset = offset;
			ZOffset = zOffset;
			Cable = cable;
		}
	}

	public class UtilityOverlay : IWorldLoaded, IRenderOverlay, ITickRender, INotifyActorDisposing
	{
		readonly World world;
		readonly UtilityOverlayInfo info;
		UtilityNetwork net;
		InfoViewLayer infoView;
		TerrainSpriteLayer lineLayer;
		TerrainSpriteLayer waterLayer;
		TerrainSpriteLayer sewerLayer;
		ISpriteSequence lineSequence;
		ISpriteSequence waterSequence;
		ISpriteSequence sewerSequence;
		ISpriteSequence pylonSequence;
		ISpriteSequence poleSequence;
		PaletteReference palette;
		int builtVersion = -1;
		int builtRoadVersion = -1;
		bool pipesShown;
		bool disposed;
		RoadLayer roads;

		public UtilityOverlay(Actor self, UtilityOverlayInfo info)
		{
			this.info = info;
			world = self.World;
		}

		/// <summary>Palette name of the prop sprites (UtilityOverlayInfo.PropPalette).</summary>
		public string PropPalette => info.PropPalette;

		/// <summary>
		/// Adds the tall props that stand on a cell: the lattice pylon of a high-voltage line and, with includeCable,
		/// the wooden pole of a road cell that carries the low-voltage cable. The ground footprint stays in the
		/// terrain overlay layers. Render only. Draw each prop as a sorted sprite at CenterOfCell(cell) + Offset
		/// (frame anchored on the cell centre, ZOffset 0).
		/// </summary>
		public void GetProps(CPos cell, List<UtilityProp> props, bool includeCable = false)
		{
			if (net == null)
				return;

			if (pylonSequence != null && net.HasPowerLine(cell))
				props.Add(new UtilityProp(pylonSequence, Mask(net.HasPowerLine, cell), WVec.Zero, 0, false));

			if (includeCable && poleSequence != null && net.HasCable(cell))
				props.Add(new UtilityProp(poleSequence, Mask(net.HasCable, cell), WVec.Zero, 0, true));
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			net = w.WorldActor.TraitOrDefault<UtilityNetwork>();
			infoView = w.WorldActor.TraitOrDefault<InfoViewLayer>();
			roads = w.WorldActor.TraitOrDefault<RoadLayer>();
			if (net == null)
				return;

			var netInfo = net.Info;
			var sequences = w.Map.Sequences;
			lineSequence = sequences.GetSequence(netInfo.Image, netInfo.PowerLineSequence);
			waterSequence = sequences.GetSequence(netInfo.Image, netInfo.WaterPipeSequence);
			sewerSequence = sequences.GetSequence(netInfo.Image, netInfo.SewagePipeSequence);
			palette = wr.Palette(netInfo.Palette);
			if (sequences.HasSequence(info.PropImage, info.PylonSequence))
				pylonSequence = sequences.GetSequence(info.PropImage, info.PylonSequence);

			if (sequences.HasSequence(info.PropImage, info.PoleSequence))
				poleSequence = sequences.GetSequence(info.PropImage, info.PoleSequence);

			var first = lineSequence.GetSprite(0);
			var empty = new Sprite(first.Sheet, Rectangle.Empty, TextureChannel.Alpha);
			lineLayer = new TerrainSpriteLayer(w, wr, empty, first.BlendMode, true);
			waterLayer = new TerrainSpriteLayer(w, wr, empty, first.BlendMode, true);
			sewerLayer = new TerrainSpriteLayer(w, wr, empty, first.BlendMode, true);
		}

		static bool ShowsPipes(CityInfoView v)
		{
			return v is CityInfoView.Water or CityInfoView.WaterGrid or CityInfoView.Sewage or CityInfoView.Power or CityInfoView.PowerGrid;
		}

		static int Mask(System.Func<CPos, bool> has, CPos c)
		{
			var m = 0;
			for (var i = 0; i < 4; i++)
				if (has(c + CityUtils.Neighbours4[i]))
					m |= 1 << i;

			return m;
		}

		void Rebuild(bool pipes)
		{
			var map = world.Map;
			foreach (var c in map.AllCells)
			{
				if (!map.Contains(c))
					continue;

				if (net.HasPowerLine(c))
					lineLayer.Update(c, lineSequence, palette, Mask(net.HasPowerLine, c));
				else
					lineLayer.Clear(c);

				if (pipes && net.HasWaterPipe(c))
					waterLayer.Update(c, waterSequence, palette, Mask(net.HasWaterPipe, c));
				else
					waterLayer.Clear(c);

				if (pipes && net.HasSewagePipe(c))
					sewerLayer.Update(c, sewerSequence, palette, Mask(net.HasSewagePipe, c));
				else
					sewerLayer.Clear(c);
			}
		}

		void ITickRender.TickRender(WorldRenderer wr, Actor self)
		{
			if (net == null || lineLayer == null)
				return;

			var pipes = infoView != null && ShowsPipes(infoView.Mode);
			var roadVersion = roads?.NetworkVersion ?? 0;
			if (net.LayoutVersion != builtVersion || pipes != pipesShown || (pipes && roadVersion != builtRoadVersion))
			{
				builtVersion = net.LayoutVersion;
				builtRoadVersion = roadVersion;
				pipesShown = pipes;
				Rebuild(pipes);
			}
		}

		void IRenderOverlay.Render(WorldRenderer wr)
		{
			if (net == null)
				return;

			if (pipesShown)
			{
				waterLayer?.Draw(wr.Viewport);
				sewerLayer?.Draw(wr.Viewport);
			}

			lineLayer?.Draw(wr.Viewport);
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (disposed)
				return;

			lineLayer?.Dispose();
			waterLayer?.Dispose();
			sewerLayer?.Dispose();
			disposed = true;
		}
	}
}
