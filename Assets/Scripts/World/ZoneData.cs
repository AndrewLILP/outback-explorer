// ZoneData.cs
// Location: Assets/Scripts/World/ZoneData.cs
// Assets live in: Assets/Data/Zones/
using UnityEngine;

namespace RelaxingDrive.World
{
    /// <summary>
    /// The identity of one visitable place: its permanent ID plus the words players see.
    ///
    /// Why a ScriptableObject instead of a typed string?
    /// - Buildings and zones REFERENCE the same asset, so they can't disagree through a typo.
    /// - Renaming the asset file or changing the display name never breaks links or saves.
    /// - UI (growth hints, Photo Journal) reads the display name and description from here,
    ///   so new places are added as data, not code (Open/Closed principle).
    ///
    /// Create via: Assets > Create > Sunday Drive > Zone Data
    /// </summary>
    [CreateAssetMenu(fileName = "Zone_New", menuName = "Sunday Drive/Zone Data", order = 0)]
    public class ZoneData : ScriptableObject
    {
        [Header("Identity (permanent)")]
        [Tooltip("Save-file key. Set once and NEVER change after players have saves, " +
                 "or their visit counts for this zone are lost. Keep existing IDs during migration.")]
        [SerializeField] private string zoneID;

        [Header("Player-Facing")]
        [Tooltip("Shown in hints and the journal, e.g. \"Lake Hume\". Falls back to the Zone ID if empty.")]
        [SerializeField] private string displayName;

        [Tooltip("Broader area, e.g. \"Albury\", \"Bathurst\", \"Wine Country\". Display only.")]
        [SerializeField] private string region;

        [Tooltip("A sentence or two for the Photo Journal / map.")]
        [TextArea(2, 5)]
        [SerializeField] private string description;

        public string ZoneID => zoneID;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? zoneID : displayName;
        public string Region => region;
        public string Description => description;

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only safety net: runs whenever this asset is edited in the Inspector.
        /// Trims stray spaces and warns about missing or duplicate IDs.
        /// </summary>
        private void OnValidate()
        {
            if (zoneID != null && zoneID != zoneID.Trim())
                zoneID = zoneID.Trim(); // "Store01 " and "Store01" would silently be different keys

            if (string.IsNullOrEmpty(zoneID))
            {
                Debug.LogWarning($"[ZoneData] '{name}' has no Zone ID.", this);
                return;
            }

            // Look through every ZoneData asset in the project for the same ID.
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:ZoneData"))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                ZoneData other = UnityEditor.AssetDatabase.LoadAssetAtPath<ZoneData>(path);
                if (other != null && other != this && other.zoneID == zoneID)
                {
                    Debug.LogError($"[ZoneData] Duplicate Zone ID '{zoneID}' in '{name}' and '{other.name}'. " +
                                   "Their visits would be counted together.", this);
                }
            }
        }
#endif
    }
}
