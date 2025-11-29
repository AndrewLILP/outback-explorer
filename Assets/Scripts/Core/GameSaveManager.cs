// GameSaveManager.cs
// Singleton manager for saving and loading game state
// Uses JsonUtility for serialization and works with GameSaveData.cs
// 
// BUG FIX: StartNewGame() now destroys DontDestroyOnLoad singletons before scene reload

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using RelaxingDrive.Core;
using RelaxingDrive.World;
using RelaxingDrive.Animals;

namespace RelaxingDrive.Core
{
    /// <summary>
    /// Central manager for saving and loading game state.
    /// Singleton pattern ensures only one instance exists.
    /// Handles auto-save, manual save, and debug save/load features.
    /// </summary>
    public class GameSaveManager : MonoBehaviour
    {
        #region Singleton Pattern

        private static GameSaveManager instance;
        private static bool isQuitting = false;

        public static GameSaveManager Instance
        {
            get
            {
                if (isQuitting)
                {
                    return null;
                }

                if (instance == null)
                {
                    instance = FindFirstObjectByType<GameSaveManager>();

                    if (instance == null)
                    {
                        GameObject managerObject = new GameObject("GameSaveManager");
                        instance = managerObject.AddComponent<GameSaveManager>();
                    }
                }

                return instance;
            }
        }

        #endregion

        #region Inspector Settings

        [Header("Save Settings")]
        [SerializeField] private bool enableAutoSave = true;
        [SerializeField] private float autoSaveInterval = 120f; // 2 minutes

        [Header("Debug Settings")]
        [SerializeField] private bool enableDebugKeys = true;
        [SerializeField] private bool showDebugLogs = true;

        #endregion

        #region Private Fields

        private string saveFilePath;
        private float autoSaveTimer = 0f;
        private bool hasSavedOnQuit = false;

        // Track activated buildings at runtime
        private HashSet<string> activatedBuildingsRuntime = new HashSet<string>();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Enforce singleton pattern
            if (instance != null && instance != this)
            {
                Debug.LogWarning("Multiple GameSaveManager instances detected! Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            Initialize();
        }

        private void Start()
        {
            if (enableAutoSave)
            {
                StartAutoSave();
            }

            // Auto-load save file on game start
            Load();
        }

        private void Update()
        {
            // Auto-save timer
            if (enableAutoSave)
            {
                autoSaveTimer += Time.deltaTime;

                if (autoSaveTimer >= autoSaveInterval)
                {
                    Save();
                    autoSaveTimer = 0f;
                }
            }

            // Debug keys for testing
            if (enableDebugKeys)
            {
                HandleDebugKeys();
            }
        }

        private void OnApplicationQuit()
        {
            isQuitting = true;

            if (!hasSavedOnQuit)
            {
                Debug.Log("[GameSaveManager] Application quitting - saving game...");
                Save();
                hasSavedOnQuit = true;
            }
        }

        private void OnDestroy()
        {
            if (!hasSavedOnQuit && !isQuitting)
            {
                Debug.Log("[GameSaveManager] OnDestroy - saving game...");
                Save();
            }
            else
            {
                Debug.Log("[GameSaveManager] OnDestroy - skipping save (already saved on quit)");
            }
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            saveFilePath = Path.Combine(Application.persistentDataPath, "outback_save.json");
            
            Debug.Log("[GameSaveManager] Initialized");
            Debug.Log("[GameSaveManager] Save file path: " + saveFilePath);
            Debug.Log($"[GameSaveManager] Auto-save: {(enableAutoSave ? "Enabled" : "Disabled")} ({autoSaveInterval}s interval)");
            Debug.Log($"[GameSaveManager] Debug keys: {(enableDebugKeys ? "Enabled" : "Disabled")} (F5=Save, F6=Load, F7=Delete, F8=NewGame)");
        }

        #endregion

        #region Save/Load Core

        /// <summary>
        /// Saves current game state to JSON file
        /// </summary>
        public void Save()
        {
            // Safety checks - prevent crashes if managers are destroyed
            if (VisitManager.Instance == null)
            {
                Debug.LogWarning("[GameSaveManager] ⚠️ VisitManager destroyed - aborting save to prevent crash");
                return;
            }

            if (AnimalDiscoveryManager.Instance == null)
            {
                Debug.LogWarning("[GameSaveManager] ⚠️ AnimalDiscoveryManager destroyed - aborting save");
                return;
            }

            try
            {
                GameSaveData saveData = new GameSaveData();

                // Collect data from all managers
                CollectSaveData(saveData);

                // Serialize to JSON
                string json = JsonUtility.ToJson(saveData, true);
                File.WriteAllText(saveFilePath, json);

                if (showDebugLogs)
                {
                    Debug.Log($"[GameSaveManager] GameSaveData [Version {saveData.saveVersion}]");
                    Debug.Log($"- Saved: {saveData.saveTimestamp}");
                    Debug.Log($"- Zones Visited: {saveData.zoneIDs.Count}");
                    Debug.Log($"- Animals Discovered: {saveData.discoveredAnimals.Count}");
                    Debug.Log($"- Buildings Activated: {saveData.activatedBuildingIDs.Count}");
                    Debug.Log("[GameSaveManager] ✅ Save successful!");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameSaveManager] ❌ Save failed: {e.Message}");
            }
        }

        /// <summary>
        /// Loads game state from JSON file
        /// </summary>
        public void Load()
        {
            if (!File.Exists(saveFilePath))
            {
                Debug.Log("[GameSaveManager] No save file found - starting fresh");
                return;
            }

            try
            {
                Debug.Log("[GameSaveManager] Loading save file...");

                string json = File.ReadAllText(saveFilePath);
                GameSaveData saveData = JsonUtility.FromJson<GameSaveData>(json);

                if (saveData == null)
                {
                    Debug.LogWarning("[GameSaveManager] ⚠️ Save file corrupted");
                    return;
                }

                // Apply loaded data to managers
                ApplySaveData(saveData);

                if (showDebugLogs)
                {
                    Debug.Log("[GameSaveManager] ✅ Load successful!");
                    Debug.Log($"[GameSaveManager] GameSaveData [Version {saveData.saveVersion}]");
                    Debug.Log($"- Saved: {saveData.saveTimestamp}");
                    Debug.Log($"- Zones Visited: {saveData.zoneIDs.Count}");
                    Debug.Log($"- Animals Discovered: {saveData.discoveredAnimals.Count}");
                    Debug.Log($"- Buildings Activated: {saveData.activatedBuildingIDs.Count}");
                    Debug.Log("[GameSaveManager] Auto-loaded save file on start");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameSaveManager] ❌ Load failed: {e.Message}");
            }
        }

        /// <summary>
        /// Deletes the save file
        /// </summary>
        public void DeleteSave()
        {
            if (File.Exists(saveFilePath))
            {
                File.Delete(saveFilePath);
                Debug.Log("[GameSaveManager] ✅ Save file deleted!");
            }
            else
            {
                Debug.Log("[GameSaveManager] No save file to delete");
            }
        }

        #endregion

        #region Data Collection

        /// <summary>
        /// Collects data from all managers and populates GameSaveData
        /// </summary>
        private void CollectSaveData(GameSaveData saveData)
        {
            // Collect visit counts from VisitManager
            if (VisitManager.Instance != null)
            {
                Dictionary<string, int> visitDict = VisitManager.Instance.GetAllVisitData();
                saveData.SetVisitDictionary(visitDict);
                Debug.Log($"[GameSaveManager] Collected {saveData.zoneIDs.Count} zone visit counts");
            }

            // Collect discovered animals from AnimalDiscoveryManager
            if (AnimalDiscoveryManager.Instance != null)
            {
                HashSet<string> discoveredAnimals = AnimalDiscoveryManager.Instance.GetDiscoveredAnimals();
                saveData.discoveredAnimals = new List<string>(discoveredAnimals);
                Debug.Log($"[GameSaveManager] Saved {saveData.discoveredAnimals.Count} discovered animals");
            }

            // Collect activated building IDs
            saveData.activatedBuildingIDs = activatedBuildingsRuntime.ToList();
            Debug.Log($"[GameSaveManager] Collected {saveData.activatedBuildingIDs.Count} activated buildings");
        }

        /// <summary>
        /// Applies loaded save data to all managers
        /// </summary>
        private void ApplySaveData(GameSaveData saveData)
        {
            // Restore visit counts to VisitManager
            if (VisitManager.Instance != null)
            {
                Dictionary<string, int> visitDict = saveData.GetVisitDictionary();
                VisitManager.Instance.LoadVisitData(visitDict);
                Debug.Log($"[GameSaveManager] Loaded visit data for {visitDict.Count} zones");
            }

            // Restore discovered animals to AnimalDiscoveryManager
            if (AnimalDiscoveryManager.Instance != null)
            {
                HashSet<string> discoveredAnimals = new HashSet<string>(saveData.discoveredAnimals);
                AnimalDiscoveryManager.Instance.LoadDiscoveryData(discoveredAnimals);
                Debug.Log($"[GameSaveManager] Loaded {saveData.discoveredAnimals.Count} discovered animals");
            }

            // Restore activated buildings
            if (saveData.activatedBuildingIDs != null && saveData.activatedBuildingIDs.Count > 0)
            {
                ActivateSavedBuildings(saveData.activatedBuildingIDs);
                Debug.Log($"[GameSaveManager] Loaded {saveData.activatedBuildingIDs.Count} activated buildings");
            }
            else
            {
                Debug.Log("[GameSaveManager] No saved buildings to activate");
            }
        }

        #endregion

        #region Building Activation

        /// <summary>
        /// Registers a building as activated (called by ActivatableObject)
        /// </summary>
        public void RegisterBuildingActivation(string buildingID)
        {
            if (!activatedBuildingsRuntime.Contains(buildingID))
            {
                activatedBuildingsRuntime.Add(buildingID);
                Debug.Log($"[GameSaveManager] Registered building activation: {buildingID}");
            }
        }

        /// <summary>
        /// Alias for RegisterBuildingActivation (for backwards compatibility)
        /// </summary>
        public void RegisterActivatedBuilding(string buildingID)
        {
            RegisterBuildingActivation(buildingID);
        }

        /// <summary>
        /// Checks if a building has been activated
        /// </summary>
        public bool IsBuildingActivated(string buildingID)
        {
            return activatedBuildingsRuntime.Contains(buildingID);
        }

        /// <summary>
        /// Activates buildings from save data
        /// </summary>
        private void ActivateSavedBuildings(List<string> buildingIDs)
        {
            if (buildingIDs == null || buildingIDs.Count == 0)
            {
                Debug.Log("[GameSaveManager] No saved buildings to activate");
                return;
            }

            // Find all ActivatableObjects in the scene
            ActivatableObject[] allBuildings = FindObjectsByType<ActivatableObject>(FindObjectsSortMode.None);

            int activatedCount = 0;

            foreach (string buildingID in buildingIDs)
            {
                // Find matching building by ID
                ActivatableObject building = System.Array.Find(allBuildings, b => b.BuildingID == buildingID);

                if (building != null && !building.IsActive)
                {
                    building.Activate();
                    activatedBuildingsRuntime.Add(buildingID);
                    activatedCount++;
                    Debug.Log($"[GameSaveManager] Activated saved building: {buildingID}");
                }
            }

            Debug.Log($"[GameSaveManager] Activated {activatedCount} buildings from save data");
        }

        #endregion

        #region Auto-Save

        private void StartAutoSave()
        {
            autoSaveTimer = 0f;
            Debug.Log("[GameSaveManager] Auto-save started (every " + autoSaveInterval + " seconds)");
        }

        #endregion

        #region New Game / Reset

        /// <summary>
        /// Starts a completely new game by deleting save file and reloading scene
        /// 🔧 BUG FIX: Now destroys DontDestroyOnLoad singletons before scene reload
        /// </summary>
        public void StartNewGame()
        {
            Debug.Log("========================================");
            Debug.Log("[GameSaveManager] STARTING NEW GAME");
            Debug.Log("========================================");

            // Step 1: Delete save file
            DeleteSave();

            // Step 2: Reset all manager states
            ResetAllManagers();

            // Step 3: Clear runtime tracking
            activatedBuildingsRuntime.Clear();

            // Step 4: 🔧 FIX - Destroy DontDestroyOnLoad singletons to prevent duplicates
            Debug.Log("[GameSaveManager] Destroying persistent singletons before reload...");

            // Destroy VisitManager
            if (VisitManager.Instance != null)
            {
                Destroy(VisitManager.Instance.gameObject);
                Debug.Log("[GameSaveManager] - Destroyed VisitManager");
            }

            // Destroy AnimalDiscoveryManager
            if (AnimalDiscoveryManager.Instance != null)
            {
                Destroy(AnimalDiscoveryManager.Instance.gameObject);
                Debug.Log("[GameSaveManager] - Destroyed AnimalDiscoveryManager");
            }

            // Destroy GameSaveManager (this instance)
            Debug.Log("[GameSaveManager] - Destroying GameSaveManager");
            Destroy(gameObject);

            // Step 5: Reload current scene
            Debug.Log("[GameSaveManager] Reloading scene for fresh start...");
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            );

            Debug.Log("[GameSaveManager] ✅ New game started successfully!");
        }

        /// <summary>
        /// Resets all manager states without reloading scene
        /// </summary>
        private void ResetAllManagers()
        {
            Debug.Log("[GameSaveManager] Resetting all managers...");

            // Reset VisitManager
            if (VisitManager.Instance != null)
            {
                VisitManager.Instance.ResetAllVisits();
                Debug.Log("[GameSaveManager] - VisitManager reset");
            }

            // Reset AnimalDiscoveryManager
            if (AnimalDiscoveryManager.Instance != null)
            {
                AnimalDiscoveryManager.Instance.ResetAllDiscoveries();
                Debug.Log("[GameSaveManager] - AnimalDiscoveryManager reset");
            }

            Debug.Log("[GameSaveManager] - All managers reset complete");
        }

        #endregion

        #region Debug Keys

        private void HandleDebugKeys()
        {
            if (Input.GetKeyDown(KeyCode.F5))
            {
                Debug.Log("[GameSaveManager] F5 pressed - Manual save");
                Save();
            }

            if (Input.GetKeyDown(KeyCode.F6))
            {
                Debug.Log("[GameSaveManager] F6 pressed - Manual load");
                Load();
            }

            if (Input.GetKeyDown(KeyCode.F7))
            {
                Debug.Log("[GameSaveManager] F7 pressed - Delete save");
                DeleteSave();
            }

            if (Input.GetKeyDown(KeyCode.F8))
            {
                Debug.Log("[GameSaveManager] F8 pressed - Start new game");
                StartNewGame();
            }
        }

        #endregion
    }
}
