// RoadNameTrigger.cs
// Location: Assets/Scripts/World/RoadNameTrigger.cs
//
// Put on an empty GameObject with a trigger collider stretched across a road.
// When the player (driving or on foot) passes through, it announces the road.
//
// Placement: one at each place the player can ENTER the road (each end, and
// where side streets join). Several triggers can share the same RoadData.
//
// Observer pattern with a STATIC event: any number of triggers in the scene,
// one listener (RoadNameUI). The UI doesn't need references to every trigger,
// and triggers don't know the UI exists.
//
// Single responsibility: detect the player and announce the road. No UI here.

using System;
using UnityEngine;
using RelaxingDrive.Player;

namespace RelaxingDrive.World
{
    [RequireComponent(typeof(Collider))]
    public class RoadNameTrigger : MonoBehaviour
    {
        /// <summary>Fired when the player enters a named road. Listeners: RoadNameUI (and later, hints/journal).</summary>
        public static event Action<RoadData> OnRoadEntered;

        /// <summary>The road the player most recently entered (null until the first trigger).</summary>
        public static RoadData CurrentRoad { get; private set; }

        [Tooltip("The road this trigger announces")]
        [SerializeField] private RoadData road;

        [Tooltip("Ignore repeat hits for this long. RCC cars have many child colliders, " +
                 "so one drive-through can call OnTriggerEnter several times.")]
        [SerializeField] private float repeatCooldownSeconds = 1f;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private float lastFiredTime = -999f;

        private void Reset()
        {
            // Runs when the component is first added: make the collider a trigger.
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (road == null || !IsActivePlayer(other)) return;
            if (Time.time - lastFiredTime < repeatCooldownSeconds) return;

            lastFiredTime = Time.time;
            CurrentRoad = road;

            if (showDebugLogs) Debug.Log($"[RoadNameTrigger] '{name}': entered {road.DisplayName}");
            OnRoadEntered?.Invoke(road);
        }

        /// <summary>
        /// Same check as SpeedLimitGate: is this collider part of whatever the
        /// player controls right now (car while driving, character on foot)?
        /// </summary>
        private bool IsActivePlayer(Collider other)
        {
            PlayerStateManager psm = PlayerStateManager.Instance;
            if (psm == null) return false;

            Transform active = psm.ActivePlayerTransform;
            return active != null && other.transform.IsChildOf(active);
        }

        // Scene view: a blue box with the road's name, so triggers are easy to find.
        private void OnDrawGizmos()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null) return;

            Gizmos.color = new Color(0.3f, 0.55f, 0.95f, 0.3f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);

#if UNITY_EDITOR
            if (road != null)
                UnityEditor.Handles.Label(transform.position + Vector3.up * 3f, road.DisplayName);
#endif
        }
    }
}