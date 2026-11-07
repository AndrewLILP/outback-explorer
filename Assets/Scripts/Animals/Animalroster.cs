// AnimalRoster.cs
// Location: Assets/Scripts/Animals/AnimalRoster.cs
//
// One asset that lists every species in the game. Award rules ("all 7 seen
// locally") and SightingManager ("is the First tier complete?") read it, so
// adding an 8th species later = drag one more AnimalData into this list.
// No code changes - that's the Open/Closed principle in practice.
//
// SETUP: Create via Assets > Create > Game > Animal Roster, name it exactly
// "AnimalRoster" and save it in Assets/Resources/ so it can be loaded by name.
// (Lazy singletons like SightingManager have no Inspector, so Resources.Load
// is how they find their data.)

using System.Collections.Generic;
using UnityEngine;

namespace RelaxingDrive.Animals
{
    [CreateAssetMenu(fileName = "AnimalRoster", menuName = "Game/Animal Roster", order = 2)]
    public class AnimalRoster : ScriptableObject
    {
        /// <summary>Asset name inside any Resources folder.</summary>
        public const string ResourcePath = "AnimalRoster";

        [Tooltip("Every species that counts toward the awards (currently 7).")]
        [SerializeField] private List<AnimalData> animals = new List<AnimalData>();

        /// <summary>Read-only view so other classes can't edit the list at runtime.</summary>
        public IReadOnlyList<AnimalData> Animals => animals;

        public int Count => animals.Count;

        /// <summary>Loads the roster from Resources. Logs an error if it's missing.</summary>
        public static AnimalRoster Load()
        {
            AnimalRoster roster = Resources.Load<AnimalRoster>(ResourcePath);
            if (roster == null)
            {
                Debug.LogError($"[AnimalRoster] No asset named '{ResourcePath}' found in a Resources folder. " +
                               "Create one via Assets > Create > Game > Animal Roster.");
            }
            return roster;
        }

        private void OnValidate()
        {
            // Catch empty slots and duplicates early, in the Editor.
            var seen = new HashSet<AnimalData>();
            foreach (AnimalData animal in animals)
            {
                if (animal == null)
                    Debug.LogWarning("[AnimalRoster] Empty slot in the roster list.", this);
                else if (!seen.Add(animal))
                    Debug.LogWarning($"[AnimalRoster] {animal.AnimalName} is listed twice.", this);
            }
        }
    }
}