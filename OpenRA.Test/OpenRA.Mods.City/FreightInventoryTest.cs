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
using OpenRA.Mods.City.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	sealed class FreightInventoryTest
	{
		[Test]
		public void CropSwitchWaitsForOldStockAndForCargoThatCouldReturn()
		{
			var farm = new Company { Kind = CompanyKind.Extractor, Output = 1, StockOut = 24000 };
			Assert.That(farm.TrySwitchCrop(2, false), Is.False);
			farm.StockOut = 0;
			Assert.That(farm.TrySwitchCrop(2, true), Is.False);
			Assert.That(farm.Output, Is.EqualTo(1));

			// A failed old-crop load returns before the switch and is sold as the old resource.
			farm.StockOut += 24000;
			Assert.That(farm.TrySwitchCrop(2, false), Is.False);
			farm.StockOut -= 24000;
			Assert.That(farm.TrySwitchCrop(2, false), Is.True);
			Assert.That(farm.Output, Is.EqualTo(2));
		}

		[Test]
		public void CropSwitchPreservesUnsellableFractionsWithoutRelabelingThem()
		{
			var farm = new Company { Kind = CompanyKind.Extractor, Output = 1, StockOut = 750, ProducedCarry = 750 };
			Assert.That(farm.TrySwitchCrop(2, false), Is.True);
			Assert.That(farm.StockOut, Is.Zero);
			Assert.That(farm.CropRemainderMilli(1), Is.EqualTo(750));
			farm.StockOut = 250;
			farm.ProducedCarry = 250;
			Assert.That(farm.TrySwitchCrop(1, false), Is.True);
			Assert.That(farm.StockOut, Is.EqualTo(750));
			Assert.That(farm.ProducedCarry, Is.EqualTo(750));
			Assert.That(farm.CropRemainderMilli(2), Is.EqualTo(250));
			Assert.That(farm.CropRemainderMilli(1), Is.Zero);
		}

		[Test]
		public void GoodsInTransitCannotFeedProduction()
		{
			var processor = new Company { Recipe = 0 };
			processor.ReserveInbound(1, 24000);
			processor.ReserveInbound(2, 12000);
			Assert.That(processor.StockIn, Is.EqualTo(new[] { 0, 0, 0 }));
			Assert.That(processor.InboundMilli(1), Is.EqualTo(24000));
			Assert.That(processor.DeliverInbound(2, 12000, [1, 2]), Is.True);
			Assert.That(processor.StockIn, Is.EqualTo(new[] { 0, 12000, 0 }));
			Assert.That(processor.InboundMilli(1), Is.EqualTo(24000));
			Assert.That(processor.InboundMilli(2), Is.Zero);
		}

		[Test]
		public void PartialLoadsPreserveTheRemainingPurchaseReservation()
		{
			var processor = new Company { Recipe = 0 };
			processor.ReserveInbound(1, 48000);
			Assert.That(processor.DeliverInbound(1, 24000, [1]), Is.True);
			Assert.That(processor.StockIn[0], Is.EqualTo(24000));
			Assert.That(processor.InboundMilli(1), Is.EqualTo(24000));
			Assert.That(processor.DeliverInbound(1, 24000, [1]), Is.True);
			Assert.That(processor.StockIn[0], Is.EqualTo(48000));
			Assert.That(processor.InboundMilli(1), Is.Zero);
		}

		[Test]
		public void ShopStockAppearsOnlyOnArrivalAndCannotBeDeliveredTwice()
		{
			var shop = new Company { Resale = true, Output = 3, StockCap = 30000 };
			shop.ReserveInbound(3, 12000);
			Assert.That(shop.StockOut, Is.Zero);
			Assert.That(shop.DeliverInbound(3, 12000, []), Is.True);
			Assert.That(shop.StockOut, Is.EqualTo(12000));
			Assert.That(shop.DeliverInbound(3, 12000, []), Is.False);
			Assert.That(shop.StockOut, Is.EqualTo(12000));
		}

		[Test]
		public void CancelledLoadCannotArriveAfterItsReservationWasReleased()
		{
			var processor = new Company { Recipe = 0 };
			processor.ReserveInbound(1, 24000);
			processor.ReserveInbound(1, -24000);
			Assert.That(processor.DeliverInbound(1, 24000, [1]), Is.False);
			Assert.That(processor.StockIn[0], Is.Zero);
			Assert.That(processor.InboundMilli(1), Is.Zero);
		}

		[Test]
		public void ChangedRecipeRejectsCargoWithoutLosingTheRefundReservation()
		{
			var processor = new Company { Recipe = 0 };
			processor.ReserveInbound(1, 24000);
			Assert.That(processor.DeliverInbound(1, 24000, [2]), Is.False);
			Assert.That(processor.StockIn, Is.EqualTo(new[] { 0, 0, 0 }));
			Assert.That(processor.InboundMilli(1), Is.EqualTo(24000));
		}
	}
}
