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
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[Desc("Tints the actor's sprites with the foliage colour of the current season (fresh spring green, golden autumn, pale winter). Render only.")]
	public class WithSeasonTintInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new WithSeasonTint(); }
	}

	public class WithSeasonTint : IRenderModifier, INotifyCreated
	{
		CityAtmosphere atmosphere;

		void INotifyCreated.Created(Actor self)
		{
			// Images with authored seasonal frames (WithIsoSprite swaps them in) need no colour cast.
			var image = self.TraitOrDefault<RenderSprites>()?.GetImage(self);
			if (image != null && self.World.Map.Sequences.HasSequence(image, "autumn"))
				return;

			atmosphere = self.World.WorldActor.TraitOrDefault<CityAtmosphere>();
		}

		IEnumerable<IRenderable> IRenderModifier.ModifyRender(Actor self, WorldRenderer wr, IEnumerable<IRenderable> r)
		{
			if (atmosphere == null)
				return r;

			return Tint(r, atmosphere.FoliageTint);
		}

		static IEnumerable<IRenderable> Tint(IEnumerable<IRenderable> renderables, System.Numerics.Vector3 tint)
		{
			foreach (var r in renderables)
			{
				if (r is IModifyableRenderable m && (m.TintModifiers & TintModifiers.ReplaceColor) == 0)
					yield return m.WithTint(m.Tint * tint, m.TintModifiers);
				else
					yield return r;
			}
		}

		IEnumerable<Rectangle> IRenderModifier.ModifyScreenBounds(Actor self, WorldRenderer wr, IEnumerable<Rectangle> r)
		{
			return r;
		}
	}
}
