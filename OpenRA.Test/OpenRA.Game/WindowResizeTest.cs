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

using NUnit.Framework;
using OpenRA.Primitives;
using OpenRA.Support;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	[NonParallelizable]
	sealed class WindowResizeTest
	{
		[TestCase(1920, 1080, 1.5f, 1.5f)]
		[TestCase(800, 600, 1.5f, 0.78125f)]
		[TestCase(600, 1000, 1.5f, 0.5859375f)]
		[TestCase(320, 200, 1.5f, 0.5f)]
		public void WindowShrinkClampsAppliedScale(int width, int height, float requested, float expected)
		{
			var sizes = new WorldViewportSizes();
			Assert.That(sizes.ClampUIScale(requested, new Size(width, height)), Is.EqualTo(expected).Within(0.00001f));
		}

		[Test]
		public void WindowGrowthRestoresRequestedScale()
		{
			var sizes = new WorldViewportSizes();
			const float Requested = 1.5f;
			Assert.That(sizes.ClampUIScale(Requested, new Size(800, 600)), Is.LessThan(Requested));
			Assert.That(sizes.ClampUIScale(Requested, new Size(1920, 1080)), Is.EqualTo(Requested));
		}

		[Test]
		public void RelayoutUpdatesRootAndNestedParentExpressionsThroughShrinkAndGrowth()
		{
			var previousRoot = Ui.Root;
			var initial = new Size(1920, 1080);
			var root = new ContainerWidget { Bounds = new WidgetBounds(0, 0, initial.Width, initial.Height) };
			var panel = new ContainerWidget
			{
				Width = new IntegerExpression("PARENT_WIDTH - 40"),
				Height = new IntegerExpression("WINDOW_HEIGHT - 80"),
				X = new IntegerExpression("(PARENT_WIDTH - WIDTH) / 2"),
				Y = new IntegerExpression("(WINDOW_HEIGHT - HEIGHT) / 2"),
				Bounds = new WidgetBounds(20, 40, 1880, 1000)
			};
			var closeButton = new ContainerWidget
			{
				X = new IntegerExpression("PARENT_WIDTH - WIDTH - 8"),
				Y = new IntegerExpression("8"),
				Width = new IntegerExpression("24"),
				Height = new IntegerExpression("24"),
				Bounds = new WidgetBounds(1848, 8, 24, 24)
			};
			root.AddChild(panel);
			panel.AddChild(closeButton);
			Ui.Root = root;
			try
			{
				var old = initial;
				foreach (var size in new[] { new Size(800, 600), new Size(1235, 777), initial })
				{
					Ui.Relayout(old, size);
					Assert.That(root.Bounds.ToRectangle(), Is.EqualTo(new Rectangle(0, 0, size.Width, size.Height)));
					Assert.That(panel.Bounds.ToRectangle(), Is.EqualTo(new Rectangle(20, 40, size.Width - 40, size.Height - 80)));
					Assert.That(closeButton.Bounds.X, Is.EqualTo(panel.Bounds.Width - 32));
					old = size;
				}
			}
			finally
			{
				Ui.Root = previousRoot;
			}
		}

		[Test]
		public void RelayoutPreservesGeometryAssignedByLogicAndUnknownSubstitutions()
		{
			var previousRoot = Ui.Root;
			var initial = new Size(1920, 1080);
			var root = new ContainerWidget { Bounds = new WidgetBounds(0, 0, initial.Width, initial.Height) };
			var custom = new ContainerWidget
			{
				Width = new IntegerExpression("100"),
				Height = new IntegerExpression("ROW_HEIGHT"),
				X = new IntegerExpression("20"),
				Y = new IntegerExpression("ROW_INDEX * 30"),
				Bounds = new WidgetBounds(91, 123, 333, 44)
			};
			root.AddChild(custom);
			Ui.Root = root;
			try
			{
				Ui.Relayout(initial, new Size(800, 600));
				Assert.That(custom.Bounds.ToRectangle(), Is.EqualTo(new Rectangle(91, 123, 333, 44)));
			}
			finally
			{
				Ui.Root = previousRoot;
			}
		}
	}
}
