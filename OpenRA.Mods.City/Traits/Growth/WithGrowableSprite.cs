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
using System.Numerics;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[Desc("Renders a growable building: construction scaffolding while it is being built, then a variant of its own sprite",
		"(chosen by a stable hash of the cell). Abandoned buildings are drawn darker.")]
	public class WithGrowableSpriteInfo : TraitInfo, Requires<RenderSpritesInfo>, Requires<GrowableBuildingInfo>, IRenderActorPreviewSpritesInfo
	{
		[Desc("Image shown while under construction.")]
		public readonly string ConstructionImage = "construction";

		[SequenceReference(nameof(ConstructionImage))]
		public readonly string ConstructionSequence = "small";

		[SequenceReference]
		[Desc("Sequence of the actor's own image. Its frames are the variants.")]
		public readonly string Sequence = "idle";

		[Desc("Sequences with a multiple of this many frames hold one block of variants per level: frame = (level - 1) * variants + variant.",
			"Older single-level sequences (4 frames) just use the variant.")]
		public readonly int Levels = 5;

		[Desc("Brightness multiplier applied to abandoned buildings.")]
		public readonly float AbandonedBrightness = 0.5f;

		[Desc("Opacity of abandoned buildings.")]
		public readonly float AbandonedAlpha = 0.9f;

		[PaletteReference]
		[Desc("Custom palette name.")]
		public readonly string Palette = null;

		public override object Create(ActorInitializer init) { return new WithGrowableSprite(init, this); }

		/// <summary>Stable variant index for a cell (same result on every client and every run).</summary>
		public static int VariantFor(CPos cell, int count)
		{
			if (count <= 1)
				return 0;

			unchecked
			{
				var h = (uint)(cell.X * 73856093) ^ (uint)(cell.Y * 19349663);
				h ^= h >> 13;
				h *= 0x5bd1e995;
				h ^= h >> 15;
				return (int)(h % (uint)count);
			}
		}

		public IEnumerable<IActorPreview> RenderPreviewSprites(ActorPreviewInitializer init, string image, int facings, PaletteReference p)
		{
			if (Palette != null)
				p = init.WorldRenderer.Palette(Palette);

			var anim = new Animation(init.World, image);
			anim.PlayRepeating(Sequence);

			yield return new SpriteActorPreview(anim, () => WVec.Zero, () => 0, p);
		}
	}

	public class WithGrowableSprite : IRenderModifier, IAutoMouseBounds
	{
		readonly WithGrowableSpriteInfo info;
		readonly GrowableBuilding growable;
		readonly Animation body;
		readonly Animation construction;
		readonly Animation boundsAnimation;
		readonly Vector3 abandonedTint;
		readonly CPos cell;

		public WithGrowableSprite(ActorInitializer init, WithGrowableSpriteInfo info)
		{
			this.info = info;
			var self = init.Self;
			growable = self.Trait<GrowableBuilding>();
			abandonedTint = new Vector3(info.AbandonedBrightness, info.AbandonedBrightness * 0.9f, info.AbandonedBrightness * 0.9f);
			cell = init.Contains<LocationInit>() ? init.Get<LocationInit>().Value : CPos.Zero;

			var rs = self.Trait<RenderSprites>();
			var image = rs.GetImage(self);

			body = new Animation(init.World, image);
			body.PlayFetchIndex(info.Sequence, () => FrameIndex(body));

			construction = new Animation(init.World, info.ConstructionImage);
			construction.PlayRepeating(info.ConstructionSequence);

			rs.Add(new AnimationWithOffset(body, null, () => growable.UnderConstruction), info.Palette);
			rs.Add(new AnimationWithOffset(construction, null, () => !growable.UnderConstruction), info.Palette);

			// Bounds from the finished building so selection does not flicker between phases.
			boundsAnimation = new Animation(init.World, image);
			boundsAnimation.PlayFetchIndex(info.Sequence, () => FrameIndex(boundsAnimation));
		}

		int FrameIndex(Animation anim)
		{
			var count = anim.CurrentSequence.Length;
			var levels = Math.Max(1, info.Levels);
			if (levels > 1 && count >= levels && count % levels == 0)
			{
				var variants = count / levels;
				return (Math.Clamp(growable.Level, 1, levels) - 1) * variants + WithGrowableSpriteInfo.VariantFor(cell, variants);
			}

			return WithGrowableSpriteInfo.VariantFor(cell, count);
		}

		IEnumerable<IRenderable> IRenderModifier.ModifyRender(Actor self, WorldRenderer wr, IEnumerable<IRenderable> r)
		{
			if (!growable.Abandoned)
				return r;

			return ModifyAbandoned(r);
		}

		IEnumerable<IRenderable> ModifyAbandoned(IEnumerable<IRenderable> r)
		{
			foreach (var renderable in r)
			{
				if (renderable is IModifyableRenderable m)
					yield return m.WithTint(abandonedTint, TintModifiers.None).WithAlpha(info.AbandonedAlpha);
				else
					yield return renderable;
			}
		}

		IEnumerable<Rectangle> IRenderModifier.ModifyScreenBounds(Actor self, WorldRenderer wr, IEnumerable<Rectangle> r)
		{
			return r;
		}

		Rectangle IAutoMouseBounds.AutoMouseoverBounds(Actor self, WorldRenderer wr)
		{
			return boundsAnimation.ScreenBounds(wr, self.CenterPosition, WVec.Zero);
		}
	}
}
