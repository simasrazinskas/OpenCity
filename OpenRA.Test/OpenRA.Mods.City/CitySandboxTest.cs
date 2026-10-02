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

using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using OpenRA.Mods.City.Traits;
using OpenRA.Network;
using OpenRA.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	sealed class CitySandboxTest
	{
		const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		static void Set(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);

		static Actor Player(bool sandbox, params TraitInfo[] traits)
		{
			var world = (World)RuntimeHelpers.GetUninitializedObject(typeof(World));
			var orders = (OrderManager)RuntimeHelpers.GetUninitializedObject(typeof(OrderManager));
			orders.LobbyInfo = new Session();
			orders.LobbyInfo.GlobalSettings.LobbyOptions[CitySandbox.Option] = new Session.LobbyOptionState { Value = sandbox.ToString() };
			Set(world, "OrderManager", orders);
			var actor = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor));
			Set(actor, "World", world);
			Set(actor, "Info", new ActorInfo("player", traits));
			return actor;
		}

		[Test]
		public void MissingSandboxOptionKeepsExistingGamesNormal()
		{
			Assert.That(CitySandbox.EnabledFor(new Session.Global()), Is.False);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void SandboxOptionSurvivesSerializedGameSettings(bool enabled)
		{
			var settings = new Session.Global();
			settings.LobbyOptions[CitySandbox.Option] = new Session.LobbyOptionState
			{
				Value = enabled.ToString(),
				PreferredValue = enabled.ToString()
			};
			var restored = Session.Global.Deserialize(settings.Serialize().Value);
			Assert.That(CitySandbox.EnabledFor(restored), Is.EqualTo(enabled));
		}

		[Test]
		public void UnlimitedMoneyCoversConstructionChargesIncomeAndDirectUpkeep()
		{
			var manager = new CityManager(Player(true), new CityManagerInfo());
			var initial = manager.Funds;
			Assert.That(manager.UnlimitedMoney, Is.True);
			Assert.That(manager.CanAfford(int.MaxValue), Is.True);
			Assert.That(manager.TrySpend(100000, "construction"), Is.True);
			typeof(CityManager).GetMethod("Charge", Fields).Invoke(manager, [200000, "upkeep"]);
			manager.AddFunds(300000, "tax");
			manager.AddFunds(-500000, "refund");

			// Legacy upkeep/income writes the Funds property directly; its setter must obey the same invariant.
			typeof(CityManager).GetProperty("Funds").SetValue(manager, -900000);
			Assert.That(manager.Funds, Is.EqualTo(initial));
			Assert.That(manager.ThisMonthExpenses["construction"], Is.EqualTo(100000));
			Assert.That(manager.ThisMonthExpenses["upkeep"], Is.EqualTo(200000));
			Assert.That(manager.ThisMonthIncome["tax"], Is.EqualTo(300000));
		}

		[Test]
		public void NormalMoneyStillRejectsUnaffordableConstructionAndPaysCharges()
		{
			var manager = new CityManager(Player(false), new CityManagerInfo());
			Assert.That(manager.UnlimitedMoney, Is.False);
			Assert.That(manager.CanAfford(70001), Is.False);
			Assert.That(manager.TrySpend(70001, "construction"), Is.False);
			Assert.That(manager.TrySpend(20000, "construction"), Is.True);
			typeof(CityManager).GetMethod("Charge", Fields).Invoke(manager, [60000, "upkeep"]);
			Assert.That(manager.Funds, Is.EqualTo(-10000));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void AllSandboxUnlocksAreAvailableWithoutInventingPopulationOrXp(bool sandbox)
		{
			var milestone = new ProgressionMilestoneInfo();
			Set(milestone, "Xp", 1000);
			Set(milestone, "DevPoints", 10);
			Set(milestone, "Unlocks", new[] { "airport", "road:highway" });
			var node = new ProgressionNodeInfo();
			Set(node, "InstanceName", "health");
			Set(node, "Unlocks", new[] { "hospital" });
			var policy = new ProgressionPolicyInfo();
			Set(policy, "InstanceName", "recycling");
			var progression = new Progression(Player(sandbox, milestone, node, policy), new ProgressionInfo());
			Assert.That(progression.IsUnlocked("airport"), Is.EqualTo(sandbox));
			Assert.That(progression.IsUnlocked("road:highway"), Is.EqualTo(sandbox));
			Assert.That(progression.IsUnlocked("hospital"), Is.EqualTo(sandbox));
			Assert.That(progression.IsPolicyAvailable(0), Is.EqualTo(sandbox));
			Assert.That(progression.GetNodeState(0), Is.EqualTo(sandbox ? NodeState.Owned : NodeState.Locked));
			Assert.That(progression.Xp, Is.Zero);
			Assert.That(progression.PeakPopulation, Is.Zero);
			Assert.That(progression.MilestoneIndex, Is.Zero);
		}
	}
}
