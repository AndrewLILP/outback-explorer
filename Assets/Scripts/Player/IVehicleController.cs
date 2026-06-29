// IVehicleController.cs
using UnityEngine;

namespace RelaxingDrive.Player
{
    /// <summary>
    /// Common contract for any drivable vehicle (PolyStang, RCC, future assets).
    /// PlayerStateManager and DrivingState depend on this interface only,
    /// never on a concrete car controller type. Each car asset gets a thin
    /// adapter component implementing this, attached alongside its own controller.
    /// </summary>
    public interface IVehicleController
    {
        /// <summary>Enable/disable player control of the vehicle.</summary>
        void SetCanControl(bool canControl);

        /// <summary>Current speed in km/h (for future HUD speedometer binding).</summary>
        float Speed { get; }

        /// <summary>The vehicle's Rigidbody, so states can freeze/unfreeze physics.</summary>
        Rigidbody Rigidbody { get; }

        /// <summary>
        /// True if this vehicle asset owns/drives its own dedicated camera rig
        /// (e.g. RCC). False if it has no camera of its own and depends on
        /// FollowCamera instead (e.g. PolyStang).
        /// </summary>
        bool UsesOwnCamera { get; }

        /// <summary>
        /// Activates/deactivates this vehicle's own camera rig. No-op for
        /// adapters where UsesOwnCamera is false.
        /// </summary>
        void SetOwnCameraActive(bool active);
    }
}