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
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using OpenRA.FileSystem;
using OpenRA.Graphics;
using OpenRA.Mods.Common.LoadScreens;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;

namespace OpenRA.Mods.City.UIArt
{
	/// <summary>
	/// The OpenCity load screen: the logo on a stripe band, both drawn procedurally at the device scale
	/// (like the rest of the chrome) instead of loaded from 1x/2x/3x images.
	/// </summary>
	public sealed class CityLoadScreen : BlankLoadScreen
	{
		[FluentReference]
		const string Loading = "loadscreen-loading";

		Dictionary<string, string> info;
		Stopwatch lastUpdate;
		float scale;
		Sheet sheet;
		Sprite logo, stripe;
		string[] messages = [];

		public override void Init(Manifest manifest, IReadOnlyFileSystem fileSystem)
		{
			base.Init(manifest, fileSystem);
			info = manifest.LoadScreen.ToDictionary(my => my.Value);
			messages = FluentProvider.GetMessage(Loading).Split(',').Select(x => x.Trim()).ToArray();
		}

		public override void Display()
		{
			// Limit load screens to at most 5 FPS
			if (Game.Renderer == null || (lastUpdate != null && lastUpdate.Elapsed.TotalSeconds < 0.2))
				return;

			lastUpdate ??= Stopwatch.StartNew();
			var r = Game.Renderer;
			if (sheet == null || scale != r.WindowScale)
				CreateSprites(r.WindowScale);

			r.BeginUI();
			var res = r.Resolution;
			WidgetUtils.FillRectWithSprite(0, res.Height / 2 - 128, res.Width, stripe.Size.Y, stripe);
			r.RgbaSpriteRenderer.DrawSprite(logo, new Vector3(res.Width / 2 - 128, res.Height / 2 - 128, 0));

			if (r.Fonts != null && messages.Length > 0)
			{
				var text = messages.Random(Game.CosmeticRandom);
				var textSize = r.Fonts["Bold"].Measure(text);
				r.Fonts["Bold"].DrawText(text, new Vector2(res.Width - textSize.X - 20, res.Height - textSize.Y - 20), Color.White);
			}

			r.EndFrame(new NullInputHandler());
			lastUpdate.Restart();
		}

		void CreateSprites(float windowScale)
		{
			scale = windowScale;
			sheet?.Dispose();
			var (logoArt, stripeArt) = CityChromeGenerator.RenderLoadScreen(fileSystem, scale,
				info.GetValueOrDefault("Style", "city|uistyle.yaml"), info.GetValueOrDefault("Icons", "city|bits/chrome/cityicons.vec"));

			var size = new Size(Exts.NextPowerOf2(logoArt.Width + stripeArt.Width + 2), Exts.NextPowerOf2(System.Math.Max(logoArt.Height, stripeArt.Height)));
			sheet = new Sheet(SheetType.BGRA, size);
			sheet.CreateBuffer();
			logo = new Sprite(sheet, new Rectangle(0, 0, logoArt.Width, logoArt.Height), TextureChannel.RGBA, 256f / logoArt.Width);
			stripe = new Sprite(sheet, new Rectangle(logoArt.Width + 2, 0, stripeArt.Width, stripeArt.Height), TextureChannel.RGBA, 1f / scale);
			Util.FastCopyIntoChannel(logo, logoArt.Data, SpriteFrameType.Rgba32);
			Util.FastCopyIntoChannel(stripe, stripeArt.Data, SpriteFrameType.Rgba32);
			sheet.CommitBufferedData();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
				sheet?.Dispose();

			base.Dispose(disposing);
		}
	}
}
