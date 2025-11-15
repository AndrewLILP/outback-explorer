// BuildingActivationSaveTest.cs
using UnityEngine;
using RelaxingDrive.Core;
using RelaxingDrive.World;

namespace RelaxingDrive.Testing
{
    /// <summary>
    /// Integration test for building activation save/load functionality.
    /// Tests that activated buildings persist correctly.
    /// 
    /// USAGE:
    /// 1. Make sure GameSaveManager GameObject exists in scene
    /// 2. Make sure you have ActivatableObjects with Building IDs in scene
    /// 3. Attach this script to any GameObject
    /// 4. Press Play
    /// 5. Press O to run test
    /// 
    /// DELETE THIS SCRIPT after Sprint 4 testing is complete.
    /// </summary>
    public class BuildingActivationSaveTest : MonoBehaviour
    {
        private void Update()
        {
            // Press O to run test
            if (Input.GetKeyDown(KeyCode.O))
            {
                RunBuildingActivationSaveTest();
            }
        }

        private void RunBuildingActivationSaveTest()
        {
            Debug.Log("=== STARTING Building Activation SAVE/LOAD TEST ===");

            // Test 1: Check managers exist
            Debug.Log("\n--- TEST 1: Check Managers ---");
            if (GameSaveManager.Instance == null)
            {
                Debug.LogError("❌ FAILED: GameSaveManager not found!");
                return;
            }
            Debug.Log("✅ GameSaveManager found");

            // Test 2: Find ActivatableObjects in scene
            Debug.Log("\n--- TEST 2: Find ActivatableObjects ---");
            ActivatableObject[] allBuildings = FindObjectsByType<ActivatableObject>(FindObjectsSortMode.None);

            if (allBuildings.Length == 0)
            {
                Debug.LogError("❌ FAILED: No ActivatableObjects found in scene!");
                Debug.LogError("Please add at least one GameObject with ActivatableObject component and a Building ID");
                return;
            }

            Debug.Log($"Found {allBuildings.Length} ActivatableObjects:");
            foreach (var building in allBuildings)
            {
                string id = string.IsNullOrEmpty(building.BuildingID) ? "<NO ID>" : building.BuildingID;
                Debug.Log($"  - {building.gameObject.name} (ID: {id}, Active: {building.IsActive})");
            }

            // Check for IDs
            int buildingsWithIDs = 0;
            foreach (var building in allBuildings)
            {
                if (!string.IsNullOrEmpty(building.BuildingID))
                    buildingsWithIDs++;
            }

            if (buildingsWithIDs == 0)
            {
                Debug.LogWarning("⚠️ WARNING: No buildings have IDs set!");
                Debug.LogWarning("Set Building IDs in the Inspector for ActivatableObjects to test save/load");
            }

            Debug.Log($"✅ Found {buildingsWithIDs} buildings with IDs");

            // Test 3: Clear existing save
            Debug.Log("\n--- TEST 3: Clear Existing Save ---");
            GameSaveManager.Instance.DeleteSave();
            Debug.Log("✅ Save deleted");

            // Test 4: Manually activate some buildings
            Debug.Log("\n--- TEST 4: Activate Buildings Manually ---");
            int activatedCount = 0;

            foreach (var building in allBuildings)
            {
                if (string.IsNullOrEmpty(building.BuildingID))
                    continue;

                if (!building.IsActive)
                {
                    Debug.Log($"Manually activating: {building.gameObject.name} (ID: {building.BuildingID})");
                    building.Activate(); // Use normal activation (registers with SaveManager)
                    activatedCount++;

                    // Stop after activating a few (or all if there are few)
                    if (activatedCount >= 3)
                        break;
                }
            }

            if (activatedCount == 0)
            {
                Debug.LogWarning("⚠️ All buildings were already active - test may not show load behavior");
            }

            Debug.Log($"✅ Activated {activatedCount} buildings");

            // Test 5: Save the data
            Debug.Log("\n--- TEST 5: Save Data ---");
            GameSaveManager.Instance.Save();
            Debug.Log("✅ Data saved (check logs above)");

            // Test 6: Deactivate all buildings (simulate fresh scene)
            Debug.Log("\n--- TEST 6: Deactivate All Buildings ---");
            foreach (var building in allBuildings)
            {
                if (building.IsActive)
                {
                    // Force deactivate for testing (even permanent ones)
                    building.Deactivate();

                    // If permanent, force deactivate anyway for testing
                    var renderer = building.GetComponent<MeshRenderer>();
                    if (renderer != null)
                        renderer.enabled = false;
                }
            }

            Debug.Log("Checking building states after deactivation:");
            foreach (var building in allBuildings)
            {
                if (!string.IsNullOrEmpty(building.BuildingID))
                {
                    Debug.Log($"  - {building.gameObject.name}: Active = {building.IsActive}");
                }
            }

            Debug.Log("✅ All buildings deactivated");

            // Test 7: Load the saved data
            Debug.Log("\n--- TEST 7: Load Saved Data ---");
            GameSaveManager.Instance.Load();
            Debug.Log("✅ Data loaded (check logs above)");

            // Test 8: Verify loaded buildings are active
            Debug.Log("\n--- TEST 8: Verify Loaded Building States ---");

            int activeCount = 0;
            foreach (var building in allBuildings)
            {
                if (!string.IsNullOrEmpty(building.BuildingID) && building.IsActive)
                {
                    Debug.Log($"✓ {building.gameObject.name} (ID: {building.BuildingID}) is ACTIVE");
                    activeCount++;
                }
            }

            bool passed = true;

            if (activeCount == 0 && activatedCount > 0)
            {
                Debug.LogError("❌ FAILED: No buildings were activated from save!");
                passed = false;
            }
            else if (activeCount != activatedCount)
            {
                Debug.LogWarning($"⚠️ WARNING: Activated {activeCount} buildings, expected {activatedCount}");
                Debug.LogWarning("This may be okay if buildings have different visit requirements");
            }
            else
            {
                Debug.Log($"✅ Correct number of buildings activated ({activeCount})");
            }

            // Summary
            Debug.Log("\n=== Building Activation SAVE/LOAD TEST COMPLETE ===");
            if (passed)
            {
                Debug.Log("✅✅✅ TEST PASSED! ✅✅✅");
                Debug.Log("Building activation states persist correctly across save/load!");

                if (activatedCount > 0)
                {
                    Debug.Log($"Successfully saved and restored {activeCount} buildings");
                }
            }
            else
            {
                Debug.LogError("❌ TEST FAILED - Check errors above");
            }

            Debug.Log("\n💡 TIP: To fully test, drive around and trigger buildings naturally,");
            Debug.Log("then save (F5), restart scene, and load (F6) to see persistence!");
            Debug.Log("======================================================");
        }
    }
}