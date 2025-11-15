// AnimalDiscoverySaveTest.cs
using UnityEngine;
using RelaxingDrive.Core;
using RelaxingDrive.Animals;

namespace RelaxingDrive.Testing
{
    /// <summary>
    /// Integration test for AnimalDiscoveryManager save/load functionality.
    /// Tests that discovered animals persist correctly.
    /// 
    /// USAGE:
    /// 1. Make sure GameSaveManager GameObject exists in scene
    /// 2. Make sure you have AnimalData ScriptableObjects created for testing
    /// 3. Attach this script to any GameObject
    /// 4. Press Play
    /// 5. Press I to run test
    /// 
    /// NOTE: This test uses manual animal names (doesn't require actual AnimalData assets)
    /// DELETE THIS SCRIPT after Sprint 4 testing is complete.
    /// </summary>
    public class AnimalDiscoverySaveTest : MonoBehaviour
    {
        private void Update()
        {
            // Press I to run test
            if (Input.GetKeyDown(KeyCode.I))
            {
                RunAnimalDiscoverySaveTest();
            }
        }

        private void RunAnimalDiscoverySaveTest()
        {
            Debug.Log("=== STARTING AnimalDiscovery SAVE/LOAD TEST ===");

            // Test 1: Check managers exist
            Debug.Log("\n--- TEST 1: Check Managers ---");
            if (AnimalDiscoveryManager.Instance == null)
            {
                Debug.LogError("❌ FAILED: AnimalDiscoveryManager not found!");
                return;
            }
            if (GameSaveManager.Instance == null)
            {
                Debug.LogError("❌ FAILED: GameSaveManager not found!");
                return;
            }
            Debug.Log("✅ Both managers found");

            // Test 2: Clear any existing save
            Debug.Log("\n--- TEST 2: Clear Existing Save ---");
            GameSaveManager.Instance.DeleteSave();
            AnimalDiscoveryManager.Instance.ResetAllDiscoveries();
            Debug.Log("✅ Save deleted, discoveries reset");

            // Test 3: Simulate discovering animals (without AnimalData assets)
            Debug.Log("\n--- TEST 3: Discover Animals (Simulated) ---");

            // We'll manually add animals to the discovery manager
            // This simulates what would happen if player discovered them in-game
            TestDiscoverAnimal("Kangaroo");
            TestDiscoverAnimal("Emu");
            TestDiscoverAnimal("Echidna");

            int discoveryCount = AnimalDiscoveryManager.Instance.GetDiscoveryCount();
            Debug.Log($"Total animals discovered: {discoveryCount}");
            Debug.Log("✅ Animal discovery data created");

            // Test 4: Save the data
            Debug.Log("\n--- TEST 4: Save Data ---");
            GameSaveManager.Instance.Save();
            Debug.Log("✅ Data saved (check logs above)");

            // Test 5: Clear discovery data (simulate fresh start)
            Debug.Log("\n--- TEST 5: Clear Discovery Data ---");
            AnimalDiscoveryManager.Instance.ResetAllDiscoveries();
            Debug.Log($"Animals discovered after reset: {AnimalDiscoveryManager.Instance.GetDiscoveryCount()} (should be 0)");
            Debug.Log($"Kangaroo discovered: {AnimalDiscoveryManager.Instance.HasDiscovered("Kangaroo")} (should be False)");
            Debug.Log($"Emu discovered: {AnimalDiscoveryManager.Instance.HasDiscovered("Emu")} (should be False)");
            Debug.Log($"Echidna discovered: {AnimalDiscoveryManager.Instance.HasDiscovered("Echidna")} (should be False)");
            Debug.Log("✅ All discoveries cleared");

            // Test 6: Load the saved data
            Debug.Log("\n--- TEST 6: Load Saved Data ---");
            GameSaveManager.Instance.Load();
            Debug.Log("✅ Data loaded (check logs above)");

            // Test 7: Verify loaded data
            Debug.Log("\n--- TEST 7: Verify Loaded Data ---");
            int loadedCount = AnimalDiscoveryManager.Instance.GetDiscoveryCount();
            bool hasKangaroo = AnimalDiscoveryManager.Instance.HasDiscovered("Kangaroo");
            bool hasEmu = AnimalDiscoveryManager.Instance.HasDiscovered("Emu");
            bool hasEchidna = AnimalDiscoveryManager.Instance.HasDiscovered("Echidna");
            bool hasDevil = AnimalDiscoveryManager.Instance.HasDiscovered("Tasmanian Devil"); // Should be false

            Debug.Log($"Animals discovered: {loadedCount} (expected: 3)");
            Debug.Log($"Kangaroo discovered: {hasKangaroo} (expected: True)");
            Debug.Log($"Emu discovered: {hasEmu} (expected: True)");
            Debug.Log($"Echidna discovered: {hasEchidna} (expected: True)");
            Debug.Log($"Tasmanian Devil discovered: {hasDevil} (expected: False)");

            bool passed = true;

            if (loadedCount != 3)
            {
                Debug.LogError("❌ FAILED: Discovery count incorrect!");
                passed = false;
            }

            if (!hasKangaroo)
            {
                Debug.LogError("❌ FAILED: Kangaroo not discovered!");
                passed = false;
            }

            if (!hasEmu)
            {
                Debug.LogError("❌ FAILED: Emu not discovered!");
                passed = false;
            }

            if (!hasEchidna)
            {
                Debug.LogError("❌ FAILED: Echidna not discovered!");
                passed = false;
            }

            if (hasDevil)
            {
                Debug.LogError("❌ FAILED: Tasmanian Devil should NOT be discovered!");
                passed = false;
            }

            if (passed)
            {
                Debug.Log("✅ All discovered animals restored correctly!");
            }

            // Summary
            Debug.Log("\n=== AnimalDiscovery SAVE/LOAD TEST COMPLETE ===");
            if (passed)
            {
                Debug.Log("✅✅✅ ALL TESTS PASSED! ✅✅✅");
                Debug.Log("Animal discovery data persists correctly across save/load!");
            }
            else
            {
                Debug.LogError("❌ SOME TESTS FAILED - Check errors above");
            }
            Debug.Log("====================================================");
        }

        /// <summary>
        /// Helper method to simulate discovering an animal
        /// (bypasses the need for actual AnimalData ScriptableObjects)
        /// </summary>
        private void TestDiscoverAnimal(string animalName)
        {
            // Directly add to the discovered set
            // This simulates what would happen if the player discovered the animal in-game
            var discoveredAnimals = AnimalDiscoveryManager.Instance.GetDiscoveredAnimals();
            discoveredAnimals.Add(animalName);
            AnimalDiscoveryManager.Instance.LoadDiscoveryData(discoveredAnimals);

            Debug.Log($"🆕 Simulated discovery: {animalName}");
        }
    }
}