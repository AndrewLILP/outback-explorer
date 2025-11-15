// GameSaveData.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RelaxingDrive.Core
{
    /// <summary>
    /// Serializable data structure for saving and loading game state.
    /// Contains all data that needs to persist between game sessions.
    /// 
    /// NOTE: Unity's JsonUtility doesn't support Dictionary serialization,
    /// so we use parallel lists (zoneIDs and visitCounts) instead.
    /// </summary>
    [System.Serializable]
    public class GameSaveData
    {
        #region Visit System Data

        /// <summary>
        /// List of zone IDs that have been visited.
        /// Parallel to visitCounts list.
        /// </summary>
        public List<string> zoneIDs = new List<string>();

        /// <summary>
        /// List of visit counts for each zone.
        /// Parallel to zoneIDs list (same index = same zone).
        /// </summary>
        public List<int> visitCounts = new List<int>();

        #endregion

        #region Animal Discovery Data

        /// <summary>
        /// List of animal names that have been discovered.
        /// Example: ["Kangaroo", "Emu", "Echidna"]
        /// </summary>
        public List<string> discoveredAnimals = new List<string>();

        #endregion

        #region Building Activation Data

        /// <summary>
        /// List of unique IDs for buildings that have been activated.
        /// Example: ["building_house1", "building_house2"]
        /// </summary>
        public List<string> activatedBuildingIDs = new List<string>();

        #endregion

        #region Metadata

        /// <summary>
        /// Timestamp when this save was created.
        /// Useful for displaying "Last played: X hours ago"
        /// </summary>
        public string saveTimestamp;

        /// <summary>
        /// Save file version for future compatibility.
        /// If we change data structure later, we can handle migration.
        /// </summary>
        public int saveVersion = 1;

        #endregion

        #region Helper Methods

        /// <summary>
        /// Constructor - initializes with current timestamp
        /// </summary>
        public GameSaveData()
        {
            saveTimestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        /// <summary>
        /// Adds a visit count entry (or updates if zone already exists)
        /// </summary>
        public void AddOrUpdateVisitCount(string zoneID, int count)
        {
            int index = zoneIDs.IndexOf(zoneID);

            if (index >= 0)
            {
                // Zone exists, update count
                visitCounts[index] = count;
            }
            else
            {
                // New zone, add to both lists
                zoneIDs.Add(zoneID);
                visitCounts.Add(count);
            }
        }

        /// <summary>
        /// Gets visit count for a specific zone (returns 0 if not found)
        /// </summary>
        public int GetVisitCount(string zoneID)
        {
            int index = zoneIDs.IndexOf(zoneID);
            return index >= 0 ? visitCounts[index] : 0;
        }

        /// <summary>
        /// Converts the parallel lists back to a Dictionary for easy use.
        /// Called by VisitManager when loading data.
        /// </summary>
        public Dictionary<string, int> GetVisitDictionary()
        {
            Dictionary<string, int> visitDict = new Dictionary<string, int>();

            for (int i = 0; i < zoneIDs.Count; i++)
            {
                if (i < visitCounts.Count) // Safety check
                {
                    visitDict[zoneIDs[i]] = visitCounts[i];
                }
            }

            return visitDict;
        }

        /// <summary>
        /// Populates the parallel lists from a Dictionary.
        /// Called by GameSaveManager when saving.
        /// </summary>
        public void SetVisitDictionary(Dictionary<string, int> visitDict)
        {
            zoneIDs.Clear();
            visitCounts.Clear();

            foreach (var kvp in visitDict)
            {
                zoneIDs.Add(kvp.Key);
                visitCounts.Add(kvp.Value);
            }
        }

        /// <summary>
        /// Checks if an animal has been discovered
        /// </summary>
        public bool IsAnimalDiscovered(string animalName)
        {
            return discoveredAnimals.Contains(animalName);
        }

        /// <summary>
        /// Adds a discovered animal (if not already in list)
        /// </summary>
        public void AddDiscoveredAnimal(string animalName)
        {
            if (!discoveredAnimals.Contains(animalName))
            {
                discoveredAnimals.Add(animalName);
            }
        }

        /// <summary>
        /// Checks if a building has been activated
        /// </summary>
        public bool IsBuildingActivated(string buildingID)
        {
            return activatedBuildingIDs.Contains(buildingID);
        }

        /// <summary>
        /// Adds an activated building ID (if not already in list)
        /// </summary>
        public void AddActivatedBuilding(string buildingID)
        {
            if (!activatedBuildingIDs.Contains(buildingID))
            {
                activatedBuildingIDs.Add(buildingID);
            }
        }

        /// <summary>
        /// Returns a summary string for debugging
        /// </summary>
        public override string ToString()
        {
            return $"GameSaveData [Version {saveVersion}]\n" +
                   $"- Saved: {saveTimestamp}\n" +
                   $"- Zones Visited: {zoneIDs.Count}\n" +
                   $"- Animals Discovered: {discoveredAnimals.Count}\n" +
                   $"- Buildings Activated: {activatedBuildingIDs.Count}";
        }

        #endregion
    }
}