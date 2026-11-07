// RoadData.cs
// Location: Assets/Scripts/World/RoadData.cs
//
// One asset per named road (Kangaroo Way, Emu Highway, ...).
// Kept completely separate from visit counts - a road is just a name that
// helps the player navigate, optionally linked to the animal found there.
//
// SETUP: Assets > Create > Game > Road Data (suggested folder: Assets/Data/Roads/)
//
// Why a ScriptableObject and not a string on each trigger?
//   - A long road needs several triggers (both ends, side streets). They all
//     point at ONE asset, so renaming the road is one edit, not five.
//   - Later systems (journal, hints, Level 3 rules) can ask "which animal
//     belongs to this road?" without searching the scene.

using UnityEngine;
using RelaxingDrive.Animals;

namespace RelaxingDrive.World
{
    [CreateAssetMenu(fileName = "New Road", menuName = "Game/Road Data", order = 4)]
    public class RoadData : ScriptableObject
    {
        [Tooltip("Name shown in the HUD, e.g. 'Koala Close'")]
        [SerializeField] private string displayName;

        [Tooltip("Optional: the animal this road is named after. Leave empty for roads like Brolga Boulevard.")]
        [SerializeField] private AnimalData linkedAnimal;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public AnimalData LinkedAnimal => linkedAnimal;
    }
}