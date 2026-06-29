// RCCVehicleAdapter.cs
using UnityEngine;

namespace RelaxingDrive.Player
{
    /// <summary>
    /// Adapts RCC's RCC_CarControllerV4 to the IVehicleController interface.
    /// Attach to the same GameObject as the RCC car controller (Steam scenes).
    /// </summary>
    [RequireComponent(typeof(RCC_CarControllerV4))]
    public class RCCVehicleAdapter : MonoBehaviour, IVehicleController
    {
        private RCC_CarControllerV4 rccController;

        private void Awake()
        {
            rccController = GetComponent<RCC_CarControllerV4>();
        }

        public void SetCanControl(bool canControl)
        {
            rccController.SetCanControl(canControl);
        }

        public float Speed => rccController.speed; // RCC already exposes km/h

        public Rigidbody Rigidbody => rccController.Rigid;

        public bool UsesOwnCamera => true;
public void SetOwnCameraActive(bool active)
{
    if (RCC_SceneManager.Instance != null && RCC_SceneManager.Instance.activePlayerCamera != null)
        RCC_SceneManager.Instance.activePlayerCamera.gameObject.SetActive(active);
}
    }
}
