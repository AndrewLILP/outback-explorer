// AnimalDiscoveryManager.cs
// Singleton manager that tracks which animals the player has discovered
// Uses Observer Pattern to notify UI and other systems when animals are discovered

using System.Collections.Generic;
using UnityEngine;
using System;

namespace RelaxingDrive.Animals
{
    /// <summary>
    /// Central manager for tracking animal discoveries.
    /// Singleton pattern ensures only one instance exists.
    /// Fires events when new animals are discovered.
    /// </summary>
    public class AnimalDiscoveryManager : MonoBehaviour
    {
        // Singleton instance
        private static AnimalDiscoveryManager instance;
        private static bool isQuitting = false;

        public static AnimalDiscoveryManager Instance
        {
            get
            {
                if (isQuitting)
                {
                    return null;
                }

                if (instance == null)
                {
                    instance = FindFirstObjectByType<AnimalDiscoveryManager>();

                    if (instance == null)
                    {
                        GameObject managerObject = new GameObject("AnimalDiscoveryManager");
                        instance = managerObject.AddComponent<AnimalDiscoveryManager>();
                    }
                }

                return instance;
            }
        }

        [Header("Debug Settings")]
        [SerializeField] private bool showDebugMessages = true;

        // HashSet prevents duplicate entries automatically
        private HashSet<string> discoveredAnimals = new HashSet<string>();

        // Observer Pattern - Event fired when animal discovered
        public event Action<AnimalData, bool> OnAnimalDiscovered; // AnimalData, isFirstTime

        private void Awake()
        {
            // Enforce singleton pattern
            if (instance != null && instance != this)
            {
                Debug.LogWarning("AnimalDiscoveryManager: Duplicate instance detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject); // Persist between scenes

            Debug.Log("AnimalDiscoveryManager initialized successfully!");
        }

        private void OnApplicationQuit()
        {
            isQuitting = true;
        }

        /// <summary>
        /// Attempts to discover an animal. Returns true if it's a new discovery.
        /// </summary>
        /// <param name="animalData">The animal being discovered</param>
        /// <returns>True if this is the first time discovering this animal</returns>
        public bool DiscoverAnimal(AnimalData animalData)
        {
            if (animalData == null)
            {
                Debug.LogError("AnimalDiscoveryManager: Attempted to discover null AnimalData!");
                return false;
            }

            string animalName = animalData.AnimalName;
            bool isFirstDiscovery = !discoveredAnimals.Contains(animalName);

            if (isFirstDiscovery)
            {
                // Add to discovered set
                discoveredAnimals.Add(animalName);

                if (showDebugMessages)
                {
                    Debug.Log($"🆕 DISCOVERED: {animalName} ({animalData.ScientificName})");
                }
            }
            else
            {
                if (showDebugMessages)
                {
                    Debug.Log($"✓ Already discovered: {animalName}");
                }
            }

            // Notify observers (UI, achievements, etc.)
            OnAnimalDiscovered?.Invoke(animalData, isFirstDiscovery);

            return isFirstDiscovery;
        }

        /// <summary>
        /// Checks if a specific animal has been discovered.
        /// </summary>
        /// <param name="animalName">The name of the animal to check</param>
        /// <returns>True if already discovered</returns>
        public bool HasDiscovered(string animalName)
        {
            return discoveredAnimals.Contains(animalName);
        }

        /// <summary>
        /// Checks if a specific animal has been discovered using AnimalData.
        /// </summary>
        public bool HasDiscovered(AnimalData animalData)
        {
            if (animalData == null) return false;
            return discoveredAnimals.Contains(animalData.AnimalName);
        }

        /// <summary>
        /// Gets the total number of animals discovered.
        /// </summary>
        public int GetDiscoveryCount()
        {
            return discoveredAnimals.Count;
        }

        /// <summary>
        /// Gets a copy of all discovered animal names.
        /// </summary>
        public HashSet<string> GetDiscoveredAnimals()
        {
            return new HashSet<string>(discoveredAnimals);
        }

        /// <summary>
        /// Loads discovery data (for save/load system later).
        /// </summary>
        public void LoadDiscoveryData(HashSet<string> data)
        {
            if (data == null)
            {
                Debug.LogWarning("AnimalDiscoveryManager: Attempted to load null discovery data.");
                return;
            }

            discoveredAnimals = new HashSet<string>(data);

            if (showDebugMessages)
            {
                Debug.Log($"Loaded discovery data: {discoveredAnimals.Count} animals discovered.");
            }
        }

        /// <summary>
        /// Resets all discoveries (useful for testing).
        /// </summary>
        public void ResetAllDiscoveries()
        {
            discoveredAnimals.Clear();
            Debug.Log("All animal discoveries reset.");
        }

        /// <summary>
        /// Debug method to print all discovered animals.
        /// </summary>
        [ContextMenu("Print Discovered Animals")]
        public void PrintDiscoveredAnimals()
        {
            if (discoveredAnimals.Count == 0)
            {
                Debug.Log("No animals discovered yet.");
                return;
            }

            Debug.Log($"=== Discovered Animals ({discoveredAnimals.Count}) ===");
            foreach (string animalName in discoveredAnimals)
            {
                Debug.Log($"  - {animalName}");
            }
        }
    }
}