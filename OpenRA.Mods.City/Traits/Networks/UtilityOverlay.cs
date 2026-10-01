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

using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Draws the HV power lines (always) and the water and sewage pipes (underground view: the water, sewage and power info views).")]
	public class UtilityOverlayInfo : TraitInfo, Requires<UtilityNetworkInfo>
	{
		public override object Create(ActorInitializer init) { return new UtilityOverlay(init.Self); }
	}

	public class UtilityOverlay : IWorldLoaded, IRenderOverlay, ITickRender, INotifyActorDisposing
	{
		readonly World world;
		UtilityNetwork net;
		InfoViewLayer infoView;
		TerrainSpriteLayer lineLayer;
		TerrainSpriteLayer waterLayer;
		TerrainSpriteLayer sewerLayer;
		ISpriteSequence lineSequence;
		ISpriteSequence waterSequence;
		ISpriteSequence sewerSequence;
		PaletteReference palette;
		int builtVersion = -1;
		int builtRoadVersion = -1;
		bool pipesShown;
		bool disposed;
		RoadLayer roads;

		public UtilityOverlay(Actor self)
		{
			world = self.World;
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			net = w.WorldActor.TraitOrDefault<UtilityNetwork>();
			infoView = w.WorldActor.TraitOrDefault<InfoViewLayer>();
			roads = w.WorldActor.TraitOrDefault<RoadLayer>();
			if (net == null)
				return;

			var info = net.Info;
			lineSequence = w.Map.Sequences.GetSequence(info.Image, info.PowerLineSequence);
			waterSequence = w.Map.Sequences.GetSequence(info.Image, info.WaterPipeSequence);
			sewerSequence = w.Map.Sequences.GetSequence(info.Image, info.SewagePipeSequence);
			palette = wr.Palette(info.Palette);
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
