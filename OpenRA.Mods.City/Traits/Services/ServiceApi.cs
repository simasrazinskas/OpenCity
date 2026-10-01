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

using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	/// <summary>Fees collected by the services (on top of ECO's power, water and garbage fees).</summary>
	public enum ServiceFee : byte { Health = 0, Education = 1 }

	/// <summary>Callbacks to the owner of a criminal (the citizen simulation) for crimes reported through <see cref="ICityCrime"/>.</summary>
	public interface ICrimeListener
	{
		/// <summary>A patrol caught the criminal in time; he sits in the jail of `jailProperty`.</summary>
		void OnArrested(int criminalId, int jailProperty);

		/// <summary>The crime was over (or the jail full) before the police could act.</summary>
		void OnEscaped(int criminalId);

		/// <summary>The sentence is served.</summary>
		void OnReleased(int criminalId, int jailProperty);
	}

	/// <summary>
	/// What the citizen simulation calls to turn its criminals into crime events. Found with
	/// <c>player.PlayerActor.TraitsImplementing&lt;ICityCrime&gt;()</c>. Synced callers only.
	/// Call <see cref="ExternalCrimeSource"/> = true once if citizens commit all crimes: the aggregate crime generation is then off.
	/// </summary>
	public interface ICityCrime
	{
		bool ExternalCrimeSource { get; set; }

		/// <summary>A criminal starts a crime at a property (call when he arrives). False if a crime is already going on there.</summary>
		bool ReportCrime(int criminalId, int targetProperty, ICrimeListener listener);

		bool IsCrimeActive(int propertyId);

		/// <summary>0..100 crime pressure of a property (to pick targets).</summary>
		int GetCrimePressure(int propertyId);

		int JailOccupancy { get; }

		int JailCapacity { get; }
	}

	/// <summary>A helicopter in the air, for renderers. The position is `From` + (`To` - `From`) * Permille / 1000 (in cells).</summary>
	public struct HelicopterFlight
	{
		public CPos From, To;
		public int Permille;
		public ServiceKind Kind;
	}

	public static class ServiceOrdersWave2
	{
		/// <summary>ExtraLocation.X = (int)ServiceFee, ExtraData = percent of the default fee (50..200).</summary>
		public const string SetServiceFee = "CitySetServiceFee";

		/// <summary>Target = the service building, ExtraData = district bit mask (0 = unrestricted).</summary>
		public const string SetDistricts = "CitySetServiceDistricts";

		public static Order SetServiceFeeOrder(Player p, ServiceFee fee, int percent) =>
			new(SetServiceFee, p.PlayerActor, false) { ExtraLocation = new CPos((int)fee, 0), ExtraData = (uint)percent };

		public static Order SetDistrictsOrder(Player p, Actor building, int districtMask) =>
			new(SetDistricts, p.PlayerActor, Target.FromActor(building), false) { ExtraData = (uint)districtMask };
	}
}
