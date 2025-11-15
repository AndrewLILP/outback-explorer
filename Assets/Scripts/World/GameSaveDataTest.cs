// GameSaveDataTest.cs
using UnityEngine;
using RelaxingDrive.Core;

namespace RelaxingDrive.Testing
{
    /// <summary>
    /// Simple test script to verify GameSaveData serializes correctly.
    /// Attach to any GameObject and press F9 in Play mode to run test.
    /// 
    /// USAGE: Attach to empty GameObject, press Play, press F9 in Game view.
    /// DELETE THIS SCRIPT after Sprint 4 testing is complete.
    /// </summary>
    public class GameSaveDataTest : MonoBehaviour
    {
        private void Update()
        {
            // Press T to run test
            if (Input.GetKeyDown(KeyCode.T))
            {
                RunSerializationTest();
            }
        }

        private void RunSerializationTest()
        {
            Debug.Log("=== STARTING GameSaveData SERIALIZATION TEST ===");

            // 1. Create test data
            GameSaveData testData = new GameSaveData();

            // Add some visit counts
            testData.AddOrUpdateVisitCount("zone_01", 3);
            testData.AddOrUpdateVisitCount("zone_02", 5);
            testData.AddOrUpdateVisitCount("zone_03", 1);

            // Add discovered animals
            testData.AddDiscoveredAnimal("Kangaroo");
            testData.AddDiscoveredAnimal("Emu");

            // Add activated buildings
            testData.AddActivatedBuilding("building_house1");
            testData.AddActivatedBuilding("building_house2");

            Debug.Log("Test data created:");
            Debug.Log(testData.ToString());

            // 2. Serialize to JSON
            string json = JsonUtility.ToJson(testData, true); // true = pretty print
            Debug.Log("Serialized JSON:");
            Debug.Log(json);

            // 3. Deserialize back to object
            GameSaveData loadedData = JsonUtility.FromJson<GameSaveData>(json);
            Debug.Log("\nDeserialized data:");
            Debug.Log(loadedData.ToString());

            // 4. Verify data integrity
            bool passed = true;

            // Check visit counts
            if (loadedData.GetVisitCount("zone_01") != 3)
            {
                Debug.LogError("FAILED: zone_01 visit count incorrect!");
                passed = false;
            }

            // Check discovered animals
            if (!loadedData.IsAnimalDiscovered("Kangaroo"))
            {
                Debug.LogError("FAILED: Kangaroo not discovered!");
                passed = false;
            }

            // Check activated buildings
            if (!loadedData.IsBuildingActivated("building_house1"))
            {
                Debug.LogError("FAILED: building_house1 not activated!");
                passed = false;
            }

            // 5. Test Dictionary conversion
            var visitDict = loadedData.GetVisitDictionary();
            Debug.Log($"\nConverted to Dictionary: {visitDict.Count} zones");
            foreach (var kvp in visitDict)
            {
                Debug.Log($"  - {kvp.Key}: {kvp.Value} visits");
            }

            // Final result
            if (passed)
            {
                Debug.Log("\n✅ SERIALIZATION TEST PASSED! GameSaveData works correctly.");
            }
            else
            {
                Debug.LogError("\n❌ SERIALIZATION TEST FAILED! Check errors above.");
            }

            Debug.Log("=== TEST COMPLETE ===");
        }
    }
}