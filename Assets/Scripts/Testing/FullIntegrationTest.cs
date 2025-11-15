// FullIntegrationTest.cs
using UnityEngine;
using RelaxingDrive.Core;
using RelaxingDrive.Animals;
using RelaxingDrive.World;

namespace RelaxingDrive.Testing
{
    /// <summary>
    /// Comprehensive integration test for the complete save/load system.
    /// Tests that ALL systems persist correctly:
    /// - Visit counts
    /// - Animal discoveries  
    /// - Building activations
    /// 
    /// USAGE:
    /// 1. Make sure GameSaveManager exists in scene
    /// 2. Make sure you have VisitZones, Animals, and Buildings set up
    /// 3. Attach this script to any GameObject
    /// 4. Press Play
    /// 5. Press L to run full integration test
    /// 
    /// DELETE THIS SCRIPT after Sprint 4 testing is complete.
    /// </summary>
    public class FullIntegrationTest : MonoBehaviour
    {
        private void Update()
        {
            // Press L to run full integration test
            if (Input.GetKeyDown(KeyCode.L))
            {
                RunFullIntegrationTest();
            }
        }

        private void RunFullIntegrationTest()
        {
            Debug.Log("════════════════════════════════════════════════");
            Debug.Log("=== FULL INTEGRATION TEST - SAVE/LOAD SYSTEM ===");
            Debug.Log("════════════════════════════════════════════════");

            // ========================================
            // PHASE 1: VERIFY ALL MANAGERS EXIST
            // ========================================
            Debug.Log("\n【PHASE 1】 VERIFY MANAGERS");
            Debug.Log("─────────────────────────────");

            bool hasGameSaveManager = GameSaveManager.Instance != null;
            bool hasVisitManager = VisitManager.Instance != null;
            bool hasAnimalManager = AnimalDiscoveryManager.Instance != null;

            Debug.Log($"GameSaveManager: {(hasGameSaveManager ? "✓" : "✗")}");
            Debug.Log($"VisitManager: {(hasVisitManager ? "✓" : "✗")}");
            Debug.Log($"AnimalDiscoveryManager: {(hasAnimalManager ? "✓" : "✗")}");

            if (!hasGameSaveManager || !hasVisitManager || !hasAnimalManager)
            {
                Debug.LogError("❌ CRITICAL: Missing required managers!");
                Debug.LogError("Test cannot continue without all managers present.");
                return;
            }

            Debug.Log("\n✅ All managers present");

            // ========================================
            // PHASE 2: VERIFY SCENE SETUP
            // ========================================
            Debug.Log("\n【PHASE 2】 VERIFY SCENE SETUP");
            Debug.Log("─────────────────────────────");

            VisitZone[] zones = FindObjectsByType<VisitZone>(FindObjectsSortMode.None);
            AnimalController[] animals = FindObjectsByType<AnimalController>(FindObjectsSortMode.None);
            ActivatableObject[] buildings = FindObjectsByType<ActivatableObject>(FindObjectsSortMode.None);

            Debug.Log($"VisitZones found: {zones.Length}");
            Debug.Log($"Animals found: {animals.Length}");
            Debug.Log($"Buildings found: {buildings.Length}");

            int buildingsWithIDs = 0;
            foreach (var building in buildings)
            {
                if (!string.IsNullOrEmpty(building.BuildingID))
                    buildingsWithIDs++;
            }

            Debug.Log($"Buildings with IDs: {buildingsWithIDs}");

            if (zones.Length == 0)
            {
                Debug.LogWarning("⚠️ No VisitZones in scene - visit tracking won't be tested");
            }

            if (animals.Length == 0)
            {
                Debug.LogWarning("⚠️ No Animals in scene - animal discovery won't be tested");
            }

            if (buildingsWithIDs == 0)
            {
                Debug.LogWarning("⚠️ No buildings with IDs - building persistence won't be tested");
            }

            Debug.Log("\n✅ Scene setup verified");

            // ========================================
            // PHASE 3: DELETE OLD SAVE & RESET STATE
            // ========================================
            Debug.Log("\n【PHASE 3】 RESET STATE");
            Debug.Log("─────────────────────────────");

            GameSaveManager.Instance.DeleteSave();
            VisitManager.Instance.ResetAllVisits();
            AnimalDiscoveryManager.Instance.ResetAllDiscoveries();

            Debug.Log("✅ Old save deleted, all data reset");

            // ========================================
            // PHASE 4: SIMULATE GAMEPLAY
            // ========================================
            Debug.Log("\n【PHASE 4】 SIMULATE GAMEPLAY");
            Debug.Log("─────────────────────────────");

            // Simulate visiting zones
            int visitedZones = 0;
            if (zones.Length > 0)
            {
                for (int i = 0; i < Mathf.Min(3, zones.Length); i++)
                {
                    string zoneID = zones[i].ZoneID;
                    VisitManager.Instance.RecordVisit(zoneID);
                    VisitManager.Instance.RecordVisit(zoneID); // Visit twice
                    visitedZones++;
                }
                Debug.Log($"✓ Simulated {visitedZones} zone visits");
            }

            // Simulate discovering animals
            int discoveredAnimals = 0;
            if (AnimalDiscoveryManager.Instance != null)
            {
                // Simulate discovering animals by name
                SimulateAnimalDiscovery("Kangaroo");
                SimulateAnimalDiscovery("Emu");
                discoveredAnimals = 2;
                Debug.Log($"✓ Simulated {discoveredAnimals} animal discoveries");
            }

            // Activate some buildings
            int activatedBuildings = 0;
            foreach (var building in buildings)
            {
                if (!string.IsNullOrEmpty(building.BuildingID) && !building.IsActive)
                {
                    building.Activate();
                    activatedBuildings++;

                    if (activatedBuildings >= 2) // Activate 2 buildings
                        break;
                }
            }

            if (activatedBuildings > 0)
            {
                Debug.Log($"✓ Activated {activatedBuildings} buildings");
            }

            Debug.Log("\n✅ Gameplay simulation complete");

            // ========================================
            // PHASE 5: SAVE CURRENT STATE
            // ========================================
            Debug.Log("\n【PHASE 5】 SAVE STATE");
            Debug.Log("─────────────────────────────");

            GameSaveManager.Instance.Save();

            Debug.Log("✅ Game state saved");

            // ========================================
            // PHASE 6: VERIFY SAVE FILE
            // ========================================
            Debug.Log("\n【PHASE 6】 VERIFY SAVE FILE");
            Debug.Log("─────────────────────────────");

            if (GameSaveManager.Instance.SaveFileExists())
            {
                Debug.Log($"✓ Save file exists");
                Debug.Log($"  {GameSaveManager.Instance.GetSaveFileInfo()}");
            }
            else
            {
                Debug.LogError("❌ Save file was not created!");
            }

            // ========================================
            // PHASE 7: RESET STATE (SIMULATE RESTART)
            // ========================================
            Debug.Log("\n【PHASE 7】 RESET STATE (SIMULATE RESTART)");
            Debug.Log("─────────────────────────────");

            VisitManager.Instance.ResetAllVisits();
            AnimalDiscoveryManager.Instance.ResetAllDiscoveries();

            // Deactivate all buildings
            foreach (var building in buildings)
            {
                if (building.IsActive)
                {
                    building.Deactivate();
                }
            }

            Debug.Log("✅ All state reset (simulating fresh game start)");

            // ========================================
            // PHASE 8: LOAD SAVED STATE
            // ========================================
            Debug.Log("\n【PHASE 8】 LOAD SAVED STATE");
            Debug.Log("─────────────────────────────");

            GameSaveManager.Instance.Load();

            Debug.Log("✅ Save file loaded");

            // ========================================
            // PHASE 9: VERIFY LOADED DATA
            // ========================================
            Debug.Log("\n【PHASE 9】 VERIFY LOADED DATA");
            Debug.Log("─────────────────────────────");

            bool allPassed = true;

            // Check visit counts
            if (visitedZones > 0)
            {
                bool visitsPassed = true;
                for (int i = 0; i < visitedZones; i++)
                {
                    string zoneID = zones[i].ZoneID;
                    int count = VisitManager.Instance.GetVisitCount(zoneID);
                    if (count != 2) // We visited each zone twice
                    {
                        Debug.LogError($"❌ Zone '{zoneID}' has {count} visits, expected 2");
                        visitsPassed = false;
                        allPassed = false;
                    }
                }

                if (visitsPassed)
                {
                    Debug.Log($"✓ Visit counts restored correctly ({visitedZones} zones)");
                }
            }

            // Check animal discoveries
            if (discoveredAnimals > 0)
            {
                bool animalsPassed = true;
                if (!AnimalDiscoveryManager.Instance.HasDiscovered("Kangaroo"))
                {
                    Debug.LogError("❌ Kangaroo not discovered after load");
                    animalsPassed = false;
                    allPassed = false;
                }
                if (!AnimalDiscoveryManager.Instance.HasDiscovered("Emu"))
                {
                    Debug.LogError("❌ Emu not discovered after load");
                    animalsPassed = false;
                    allPassed = false;
                }

                if (animalsPassed)
                {
                    Debug.Log($"✓ Animal discoveries restored correctly ({discoveredAnimals} animals)");
                }
            }

            // Check building activations
            if (activatedBuildings > 0)
            {
                int activeCount = 0;
                foreach (var building in buildings)
                {
                    if (!string.IsNullOrEmpty(building.BuildingID) && building.IsActive)
                    {
                        activeCount++;
                    }
                }

                if (activeCount == activatedBuildings)
                {
                    Debug.Log($"✓ Building activations restored correctly ({activeCount} buildings)");
                }
                else
                {
                    Debug.LogError($"❌ Expected {activatedBuildings} active buildings, found {activeCount}");
                    allPassed = false;
                }
            }

            // ========================================
            // FINAL RESULT
            // ========================================
            Debug.Log("\n════════════════════════════════════════════════");
            Debug.Log("【FINAL RESULT】");
            Debug.Log("════════════════════════════════════════════════");

            if (allPassed)
            {
                Debug.Log("✅✅✅ ALL SYSTEMS PASSED! ✅✅✅");
                Debug.Log("\n🎉 SAVE/LOAD SYSTEM FULLY FUNCTIONAL! 🎉");
                Debug.Log("\nAll three systems persist correctly:");
                Debug.Log("  • Visit Tracking System ✓");
                Debug.Log("  • Animal Discovery System ✓");
                Debug.Log("  • Building Activation System ✓");
                Debug.Log("\nSprint 4 is COMPLETE! 🚀");
            }
            else
            {
                Debug.LogError("❌ SOME TESTS FAILED");
                Debug.LogError("Review errors above to identify issues");
            }

            Debug.Log("\n════════════════════════════════════════════════");

            Debug.Log("\n💡 MANUAL TEST RECOMMENDED:");
            Debug.Log("   1. Play the game naturally");
            Debug.Log("   2. Drive around, discover animals, unlock buildings");
            Debug.Log("   3. Press F5 to save");
            Debug.Log("   4. Stop and restart Play mode");
            Debug.Log("   5. Press F6 to load");
            Debug.Log("   6. Everything should persist! ✨");
            Debug.Log("════════════════════════════════════════════════");
        }

        /// <summary>
        /// Helper to simulate discovering an animal without AnimalData asset
        /// </summary>
        private void SimulateAnimalDiscovery(string animalName)
        {
            var discoveredAnimals = AnimalDiscoveryManager.Instance.GetDiscoveredAnimals();
            discoveredAnimals.Add(animalName);
            AnimalDiscoveryManager.Instance.LoadDiscoveryData(discoveredAnimals);
        }
    }
}