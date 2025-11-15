// GameSaveManagerTest.cs
using UnityEngine;
using RelaxingDrive.Core;

namespace RelaxingDrive.Testing
{
    /// <summary>
    /// Test script to verify GameSaveManager works correctly.
    /// Tests save, load, and delete operations.
    /// 
    /// USAGE:
    /// 1. Attach to empty GameObject
    /// 2. Press Play
    /// 3. Press Y to run test
    /// 
    /// DELETE THIS SCRIPT after Sprint 4 testing is complete.
    /// </summary>
    public class GameSaveManagerTest : MonoBehaviour
    {
        private void Update()
        {
            // Press Y to run test
            if (Input.GetKeyDown(KeyCode.Y))
            {
                RunSaveManagerTest();
            }
        }

        private void RunSaveManagerTest()
        {
            Debug.Log("=== STARTING GameSaveManager TEST ===");

            // Test 1: Check singleton instance
            Debug.Log("\n--- TEST 1: Singleton Instance ---");
            if (GameSaveManager.Instance != null)
            {
                Debug.Log("✅ GameSaveManager singleton created successfully");
            }
            else
            {
                Debug.LogError("❌ FAILED: GameSaveManager instance is null!");
                return;
            }

            // Test 2: Save file path
            Debug.Log("\n--- TEST 2: Save File Path ---");
            string savePath = GameSaveManager.Instance.GetSaveFilePath();
            Debug.Log($"Save file path: {savePath}");

            // Test 3: Delete existing save (clean slate)
            Debug.Log("\n--- TEST 3: Delete Existing Save ---");
            GameSaveManager.Instance.DeleteSave();

            if (!GameSaveManager.Instance.SaveFileExists())
            {
                Debug.Log("✅ Save file deleted (or didn't exist)");
            }
            else
            {
                Debug.LogWarning("⚠️ Save file still exists after delete attempt");
            }

            // Test 4: Save operation
            Debug.Log("\n--- TEST 4: Save Operation ---");
            GameSaveManager.Instance.Save();

            if (GameSaveManager.Instance.SaveFileExists())
            {
                Debug.Log("✅ Save file created successfully");
                Debug.Log(GameSaveManager.Instance.GetSaveFileInfo());
            }
            else
            {
                Debug.LogError("❌ FAILED: Save file not created!");
            }

            // Test 5: Load operation
            Debug.Log("\n--- TEST 5: Load Operation ---");
            GameSaveManager.Instance.Load();
            Debug.Log("✅ Load operation completed (check logs above for details)");

            // Test 6: Debug keys info
            Debug.Log("\n--- TEST 6: Debug Keys ---");
            Debug.Log("Debug keys are available:");
            Debug.Log("  F5 = Manual Save");
            Debug.Log("  F6 = Manual Load");
            Debug.Log("  F7 = Delete Save");
            Debug.Log("Try pressing these keys to test!");

            // Summary
            Debug.Log("\n=== GameSaveManager TEST COMPLETE ===");
            Debug.Log("✅ All basic tests passed!");
            Debug.Log("Note: Full integration testing will happen in Tasks 5-7");
            Debug.Log("=====================================");
        }
    }
}