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
	[Desc("Renders a growable building: frame = ((level - 1) * variants + variant) * 4 + facing (variant by a stable cell hash,",
		"facing toward the access road via WithIsoSprite). States come from sibling sequences of the same image (named",
		"'<Sequence>-<state>' or '<state>'): build1..build3 while under construction, abandoned, collapsed, burnt; missing ones",
		"fall back to the construction image and a darker tint.")]
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

		[Desc("Facing frames per variant in the sequence (isokit order 0 = +Y, 1 = +X, 2 = -Y, 3 = -X). Sequences that",
			"declare Facings themselves use those instead.")]
		public readonly int FacingFrames = 4;

		[Desc("Collapse progress (0..100) of an abandoned building from which the 'collapsed' state is shown.")]
		public readonly int CollapsedFrom = 60;

		[Desc("Brightness multiplier applied to abandoned buildings without an 'abandoned' sequence.")]
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

	public class WithGrowableSprite : IRenderModifier, IAutoMouseBounds, IIsoSpriteState, INotifyCreated
	{
		readonly WithGrowableSpriteInfo info;
		readonly GrowableBuilding growable;
		readonly Animation body;
		readonly Animation construction;
		readonly Animation boundsAnimation;
		readonly Vector3 abandonedTint;
		readonly CPos cell;
		readonly Actor self;
		readonly string image;
		readonly bool hasBuildStates;
		readonly bool hasAbandoned;
		readonly bool hasCollapsed;
		WithIsoSprite iso;

		public WithGrowableSprite(ActorInitializer init, WithGrowableSpriteInfo info)
		{
			this.info = info;
			self = init.Self;
			growable = self.Trait<GrowableBuilding>();
			abandonedTint = new Vector3(info.AbandonedBrightness, info.AbandonedBrightness * 0.9f, info.AbandonedBrightness * 0.9f);
			cell = init.Contains<LocationInit>() ? init.Get<LocationInit>().Value : CPos.Zero;

			var rs = self.Trait<RenderSprites>();
			image = rs.GetImage(self);
			var sequences = init.World.Map.Sequences;
			bool Has(string state) => sequences.HasSequence(image, info.Sequence + "-" + state) || sequences.HasSequence(image, state);
			hasBuildStates = Has("build1");
			hasAbandoned = Has("abandoned");
			hasCollapsed = Has("collapsed");

			// The facing comes from WithIsoSprite (IFacing): sequences with Facings: 4 pick it themselves.
			var facing = RenderSprites.MakeFacingFunc(self);
			body = new Animation(init.World, image, facing);
			body.PlayFetchIndex(info.Sequence, () => FrameIndex(body));

			// Construction site: the actor's own "construction" sequence (ART: (stage - 1) * 4 + facing) or the shared image.
			var ownSite = sequences.HasSequence(image, "construction");
			construction = new Animation(init.World, ownSite ? image : info.ConstructionImage);
			construction.PlayFetchIndex(ownSite ? "construction" : info.ConstructionSequence, () => ConstructionFrame(construction));

			// With authored build stages the body stays visible and WithIsoSprite swaps in the stage sprites.
			rs.Add(new AnimationWithOffset(body, null, () => growable.UnderConstruction && !hasBuildStates), info.Palette);
			rs.Add(new AnimationWithOffset(construction, null, () => !growable.UnderConstruction || hasBuildStates), info.Palette);

			// Bounds from the finished building so selection does not flicker between phases.
			boundsAnimation = new Animation(init.World, image, facing);
			boundsAnimation.PlayFetchIndex(info.Sequence, () => FrameIndex(boundsAnimation));
		}

		int ConstructionFrame(Animation anim)
		{
			var length = anim.CurrentSequence.Length;
			var facings = Math.Max(1, info.FacingFrames);
			if (length % facings != 0 || length < 2 * facings)
				return length <= 1 ? 0 : self.World.WorldTick / 8 % length;

			// Stages follow the build progress; the facing matches the finished building.
			var stages = length / facings;
			var stage = Math.Clamp(growable.ConstructionProgress * stages / 100, 0, stages - 1);
			return stage * facings + (iso?.FacingIndex ?? 0) % facings;
		}

		void INotifyCreated.Created(Actor self)
		{
			iso = self.TraitOrDefault<WithIsoSprite>();
		}

		string IIsoSpriteState.IsoState(Actor self)
		{
			if (growable.UnderConstruction && hasBuildStates)
			{
				var progress = growable.ConstructionProgress;
				return progress < 34 ? "build1" : progress < 67 ? "build2" : "build3";
			}

			if (growable.Abandoned)
				return hasCollapsed && growable.CollapseProgress >= info.CollapsedFrom ? "collapsed" : hasAbandoned ? "abandoned" : null;

			return null;
		}

		bool IIsoSpriteState.IsoLit(Actor self) { return growable.IsOperational && !growable.UnderConstruction; }

		int FrameIndex(Animation anim)
		{
			var seq = anim.CurrentSequence;

			// Facing frames inside the sequence (contract: ((level - 1) * variants + variant) * 4 + facing).
			var per = seq.Facings <= 1 && info.FacingFrames > 1 && seq.Length % info.FacingFrames == 0 ? info.FacingFrames : 1;
			var index = BlockIndex(seq.Length / per);
			return per > 1 ? index * per + (iso?.FacingIndex ?? 0) % per : index;
		}

		int BlockIndex(int count)
		{
			var levels = Math.Max(1, info.Levels);
			if (levels > 1 && count >= levels && count % levels == 0)
			{
				var variants = count / levels;

				// Themes split the variants: the first half is North American, the second half European.
				var first = 0;
				var span = variants;
				if (growable.Theme != 0 && variants >= 2)
				{
					span = variants / 2;
					first = growable.Theme == 2 ? variants - span : 0;
				}

				return (Math.Clamp(growable.Level, 1, levels) - 1) * variants + first + WithGrowableSpriteInfo.VariantFor(cell, span);
			}

			return WithGrowableSpriteInfo.VariantFor(cell, count);
		}

		IEnumerable<IRenderable> IRenderModifier.ModifyRender(Actor self, WorldRenderer wr, IEnumerable<IRenderable> r)
		{
			if (!growable.Abandoned || hasAbandoned)
				return r;

			return ModifyAbandoned(r);
		}

		IEnumerable<IRenderable> ModifyAbandoned(IEnumerable<IRenderable> r)
		{
			foreach (var renderable in r)
			{
				// Multiply (WithIsoSprite already applied the ambient tint) and leave emissive frames alone.
				if (renderable is IModifyableRenderable m && (m.TintModifiers & TintModifiers.IgnoreWorldTint) == 0)
					yield return m.WithTint(m.Tint * abandonedTint, m.TintModifiers).WithAlpha(m.Alpha * info.AbandonedAlpha);
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
