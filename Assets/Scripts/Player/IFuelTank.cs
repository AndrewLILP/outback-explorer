// IFuelTank.cs
// Location: Assets/Scripts/Player/IFuelTank.cs
//
// NEW (Nov 2026 - fuel): small interface for any vehicle with a fuel tank.
// FuelMonitor and FuelPump depend on THIS, never on RCC_CarControllerV4,
// so the fuel system would work with any future car asset that gets an adapter.
// (Same idea as IVehicleController - the "I" and "D" in SOLID.)

namespace RelaxingDrive.Player
{
    public interface IFuelTank
    {
        /// <summary>True if this vehicle actually consumes fuel (RCC: useFuelConsumption).</summary>
        bool UsesFuel { get; }

        /// <summary>Tank size in litres.</summary>
        float FuelCapacity { get; }

        /// <summary>Litres currently in the tank.</summary>
        float FuelLitres { get; }

        /// <summary>0-100. Handy for UI and warning thresholds.</summary>
        float FuelPercent { get; }

        /// <summary>Adds fuel, clamped to the tank capacity.</summary>
        void AddFuel(float litres);
    }
}