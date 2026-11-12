// SpeedZoneGate.cs
// Trigger volume that sets the governor's limit when the car passes through.
// Place one at each town exit/entry, e.g. 60 at the town end, 110 at the highway end of the bridge.
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SpeedZoneGate : MonoBehaviour
{
    [SerializeField] private float limitKmh = 110f;

    private void Reset() => GetComponent<Collider>().isTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        // RCC cars have many colliders (body and wheels), so go through the Rigidbody to find the car root.
        if (other.attachedRigidbody == null) return;
        if (other.attachedRigidbody.TryGetComponent(out SpeedGovernor governor))
            governor.SetLimit(limitKmh);
    }
}