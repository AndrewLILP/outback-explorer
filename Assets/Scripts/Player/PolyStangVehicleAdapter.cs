// PolyStangVehicleAdapter.cs
using UnityEngine;
using PolyStang;

namespace RelaxingDrive.Player
{

    
    /// <summary>
    /// Adapts PolyStang's CarController to the IVehicleController interface.
    /// Attach to the same GameObject as PolyStang's CarController (Itch Demo scene).
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class PolyStangVehicleAdapter : MonoBehaviour, IVehicleController
    {
        private CarController carController;
        private Rigidbody rb;

        public bool UsesOwnCamera => false;
public void SetOwnCameraActive(bool active) { /* no-op */ }

        private void Awake()
        {
            carController = GetComponent<CarController>();
            rb = GetComponent<Rigidbody>();
        }

        public void SetCanControl(bool canControl)
        {
            carController.enabled = canControl;
        }

        public float Speed => rb != null ? rb.linearVelocity.magnitude * 3.6f : 0f; // m/s -> km/h

        public Rigidbody Rigidbody => rb;
    }
}