// VisitManagerSaveTest.cs
using UnityEngine;
using RelaxingDrive.Core;

namespace RelaxingDrive.Testing
{
    /// <summary>
    /// Integration test for VisitManager save/load functionality.
    /// Tests that visit counts persist correctly.
    /// 
    /// USAGE:
    /// 1. Make sure GameSaveManager GameObject exists in scene
    /// 2. Attach this script to any GameObject
    /// 3. Press Play
    /// 4. Press U to run test
    /// 
    /// DELETE THIS SCRIPT after Sprint 4 testing is complete.
    /// </summary>
    public class VisitManagerSaveTest : MonoBehaviour
    {
        private void Update()
        {
            // Press U to run test
            if (Input.GetKeyDown(KeyCode.U))
            {
                RunVisitManagerSaveTest();
            }
        }

        private void RunVisitManagerSaveTest()
        {
            Debug.Log("=== STARTING VisitManager SAVE/LOAD TEST ===");

            // Test 1: Check managers exist
            Debug.Log("\n--- TEST 1: Check Managers ---");
            if (VisitManager.Instance == null)
            {
                Debug.LogError("❌ FAILED: VisitManager not found!");
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
            VisitManager.Instance.ResetAllVisits();
            Debug.Log("✅ Save deleted, visits reset");

            // Test 3: Create some visit data
            Debug.Log("\n--- TEST 3: Create Visit Data ---");
            VisitManager.Instance.RecordVisit("zone_01");
            VisitManager.Instance.RecordVisit("zone_01");
            VisitManager.Instance.RecordVisit("zone_01"); // 3 visits

            VisitManager.Instance.RecordVisit("zone_02");
            VisitManager.Instance.RecordVisit("zone_02"); // 2 visits

            VisitManager.Instance.RecordVisit("zone_03"); // 1 visit

            Debug.Log($"zone_01: {VisitManager.Instance.GetVisitCount("zone_01")} visits");
            Debug.Log($"zone_02: {VisitManager.Instance.GetVisitCount("zone_02")} visits");
            Debug.Log($"zone_03: {VisitManager.Instance.GetVisitCount("zone_03")} visits");
            Debug.Log("✅ Visit data created");

            // Test 4: Save the data
            Debug.Log("\n--- TEST 4: Save Data ---");
            GameSaveManager.Instance.Save();
            Debug.Log("✅ Data saved (check logs above)");

            // Test 5: Clear visit data (simulate fresh start)
            Debug.Log("\n--- TEST 5: Clear Visit Data ---");
            VisitManager.Instance.ResetAllVisits();
            Debug.Log($"zone_01 after reset: {VisitManager.Instance.GetVisitCount("zone_01")} visits (should be 0)");
            Debug.Log($"zone_02 after reset: {VisitManager.Instance.GetVisitCount("zone_02")} visits (should be 0)");
            Debug.Log($"zone_03 after reset: {VisitManager.Instance.GetVisitCount("zone_03")} visits (should be 0)");
            Debug.Log("✅ All visits cleared");

            // Test 6: Load the saved data
            Debug.Log("\n--- TEST 6: Load Saved Data ---");
            GameSaveManager.Instance.Load();
            Debug.Log("✅ Data loaded (check logs above)");

            // Test 7: Verify loaded data
            Debug.Log("\n--- TEST 7: Verify Loaded Data ---");
            int zone01Count = VisitManager.Instance.GetVisitCount("zone_01");
            int zone02Count = VisitManager.Instance.GetVisitCount("zone_02");
            int zone03Count = VisitManager.Instance.GetVisitCount("zone_03");

            Debug.Log($"zone_01: {zone01Count} visits (expected: 3)");
            Debug.Log($"zone_02: {zone02Count} visits (expected: 2)");
            Debug.Log($"zone_03: {zone03Count} visits (expected: 1)");

            bool passed = true;

            if (zone01Count != 3)
            {
                Debug.LogError("❌ FAILED: zone_01 count incorrect!");
                passed = false;
            }

            if (zone02Count != 2)
            {
                Debug.LogError("❌ FAILED: zone_02 count incorrect!");
                passed = false;
            }

            if (zone03Count != 1)
            {
                Debug.LogError("❌ FAILED: zone_03 count incorrect!");
                passed = false;
            }

            if (passed)
            {
                Debug.Log("✅ All visit counts restored correctly!");
            }

            // Summary
            Debug.Log("\n=== VisitManager SAVE/LOAD TEST COMPLETE ===");
            if (passed)
            {
                Debug.Log("✅✅✅ ALL TESTS PASSED! ✅✅✅");
                Debug.Log("Visit data persists correctly across save/load!");
            }
            else
            {
                Debug.LogError("❌ SOME TESTS FAILED - Check errors above");
            }
            Debug.Log("================================================");
        }
    }
}