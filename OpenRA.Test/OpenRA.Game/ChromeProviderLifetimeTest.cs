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
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	[NonParallelizable]
	sealed class ChromeProviderLifetimeTest
	{
		sealed class TestTexture : ITexture
		{
			public bool Disposed;
			public Size Size => new(32, 32);
			public TextureScaleFilter ScaleFilter { get; set; }
			public void Dispose() { Disposed = true; }
			public byte[] GetData() { return []; }
			public void SetData(byte[] colors, int width, int height) { }
			public void SetSubData(byte[] colors, int xoffset, int yoffset, int width, int height) { }
			public void SetFloatData(float[] colors, int width, int height) { }
			public void SetDataFromReadBuffer(Rectangle rect) { }
		}

		[TearDown]
		public void TearDown() { ChromeProvider.Deinitialize(); }

		[MethodImpl(MethodImplOptions.NoInlining)]
		static Sprite RetireAtlas(TestTexture texture)
		{
			var builder = new SheetBuilder(SheetType.BGRA, () => texture == null ? new Sheet(SheetType.BGRA, new Size(32, 32)) : new Sheet(SheetType.BGRA, texture));
			var sprite = builder.Allocate(new Size(8, 8));

			// Seed one generated atlas without a renderer, filesystem or mod loader. Retirement itself
			// follows the public DPI-change path used by live window resizing.
			var generated = new Dictionary<(string, float), (ChromeGeneratorContext, SheetBuilder)>
			{
				{ ("Test", 1f), (null, builder) }
			};
			typeof(ChromeProvider).GetField("generated", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, generated);
			typeof(ChromeProvider).GetField("dpiScale", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, 1f);
			ChromeProvider.SetDPIScale(2f);
			return sprite;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void RetireUnreferencedAtlas(TestTexture texture) { _ = RetireAtlas(texture); }

		static void Collect()
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			ChromeProvider.CollectRetiredSheets();
		}

		[Test]
		public void AWidgetKeepingOldChromeDoesNotLoseItsTexture()
		{
			var texture = new TestTexture();
			var sprite = RetireAtlas(texture);
			Collect();
			Assert.That(texture.Disposed, Is.False);
			GC.KeepAlive(sprite);
		}

		[Test]
		public void DerivedSpritesKeepRetiredAtlasAlive()
		{
			var texture = new TestTexture();
			var derived = new Sprite(RetireAtlas(texture).Sheet, new Rectangle(1, 1, 2, 2), TextureChannel.RGBA);
			Collect();
			Assert.That(texture.Disposed, Is.False);
			GC.KeepAlive(derived);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void UploadRetiredAtlas(TestTexture texture)
		{
			var sprite = RetireAtlas(null);
			var handle = typeof(Sheet).GetField("Texture", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sprite.Sheet);
			handle.GetType().GetProperty("Value").SetValue(handle, texture);
			Collect();
			Assert.That(texture.Disposed, Is.False);
			GC.KeepAlive(sprite);
		}

		[Test]
		public void FirstUploadAfterRetirementIsAlsoReclaimed()
		{
			var texture = new TestTexture();
			UploadRetiredAtlas(texture);
			Collect();
			Assert.That(texture.Disposed, Is.True);
		}

		[Test]
		public void ContinuousScaleChangesReclaimUnreferencedAtlasTextures()
		{
			var textures = new List<TestTexture>();
			for (var resize = 0; resize < 40; resize++)
			{
				var texture = new TestTexture();
				textures.Add(texture);
				RetireUnreferencedAtlas(texture);
			}

			Collect();
			Assert.That(textures.Count(t => t.Disposed), Is.EqualTo(textures.Count));
		}
	}
}
