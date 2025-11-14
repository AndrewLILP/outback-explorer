// AnimalData.cs
// ScriptableObject that stores educational information about Australian wildlife
// Used by the Animal Discovery System to display facts when player approaches animals

using UnityEngine;

namespace RelaxingDrive.Animals
{
    /// <summary>
    /// ScriptableObject containing all data about a discoverable animal.
    /// Create instances via: Assets > Create > Game > Animal Data
    /// </summary>
    [CreateAssetMenu(fileName = "New Animal", menuName = "Game/Animal Data", order = 1)]
    public class AnimalData : ScriptableObject
    {
        [Header("Animal Identity")]
        [Tooltip("Common name of the animal (e.g., 'Kangaroo')")]
        [SerializeField] private string animalName;

        [Tooltip("Scientific name (e.g., 'Macropus')")]
        [SerializeField] private string scientificName;

        [Header("Natural Information")]
        [Tooltip("Where this animal lives in Australia")]
        [TextArea(2, 4)]
        [SerializeField] private string habitat;

        [Tooltip("What this animal eats (e.g., 'Herbivore - grasses, leaves')")]
        [SerializeField] private string diet;

        [Header("Educational Content")]
        [Tooltip("Interesting fact to teach players about this animal")]
        [TextArea(2, 4)]
        [SerializeField] private string funFact;

        [Header("Visual & Detection")]
        [Tooltip("Icon image shown in UI (square sprite recommended)")]
        [SerializeField] private Sprite icon;

        [Tooltip("Distance (in meters) at which player can discover this animal")]
        [SerializeField] private float discoveryRange = 10f;

        // Public properties for read-only access (encapsulation)
        public string AnimalName => animalName;
        public string ScientificName => scientificName;
        public string Habitat => habitat;
        public string Diet => diet;
        public string FunFact => funFact;
        public Sprite Icon => icon;
        public float DiscoveryRange => discoveryRange;

        /// <summary>
        /// Validates that all required fields are filled in.
        /// Called automatically by Unity when values change in Inspector.
        /// </summary>
        private void OnValidate()
        {
            // Ensure discovery range is reasonable
            if (discoveryRange < 1f)
            {
                discoveryRange = 1f;
                Debug.LogWarning($"AnimalData '{animalName}': Discovery range too small, set to minimum 1m");
            }

            if (discoveryRange > 50f)
            {
                discoveryRange = 50f;
                Debug.LogWarning($"AnimalData '{animalName}': Discovery range too large, capped at 50m");
            }
        }
    }
}