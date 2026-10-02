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

using System.Linq;
using NUnit.Framework;
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	sealed class SheetBuilderTest
	{
		[TestCase(SheetType.BGRA, TextureChannel.RGBA)]
		[TestCase(SheetType.Indexed, TextureChannel.Red)]
		public void ResetReclaimsAtlasesAndRestartsAllocation(SheetType type, TextureChannel channel)
		{
			using var builder = new SheetBuilder(type, 32);
			for (var resize = 0; resize < 20; resize++)
			{
				var first = builder.Allocate(new Size(20, 20));
				Assert.That(first.Bounds, Is.EqualTo(new Rectangle(1, 1, 20, 20)));
				Assert.That(first.Channel, Is.EqualTo(channel));
				for (var glyph = 0; glyph < 10; glyph++)
					builder.Allocate(new Size(20, 20));

				Assert.That(builder.AllSheets.Count(), Is.GreaterThan(1));
				builder.Reset();
				Assert.That(builder.AllSheets, Is.Empty);
				Assert.That(builder.Current, Is.Null);
			}
		}
	}
}
