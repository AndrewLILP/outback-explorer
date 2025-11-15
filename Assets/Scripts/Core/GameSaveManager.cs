// GameSaveManager.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using RelaxingDrive.Animals; // For AnimalDiscoveryManager

namespace RelaxingDrive.Core
{
    /// <summary>
    /// Singleton manager that handles all save/load operations.
    /// Manages auto-save, manual save, and save-on-quit functionality.
    /// 
    /// DESIGN PATTERN: Singleton
    /// - Ensures only one save manager exists
    /// - Global access point for save/load operations
    /// - Persists across scene loads (DontDestroyOnLoad)
    /// 
    /// USAGE:
    /// - Auto-saves every 2 minutes (configurable)
    /// - Saves on application quit
    /// - Manual save: Press F5 or call GameSaveManager.Instance.Save()
    /// - Manual load: Press F6 or call GameSaveManager.Instance.Load()
    /// - Delete save: Press F7 (debug only)
    /// </summary>
    public class GameSaveManager : MonoBehaviour
    {
        #region Singleton Pattern

        private static GameSaveManager instance;

        public static GameSaveManager Instance
        {
            get
            {
                if (instance == null)
                {
                    // Try to find existing instance
                    instance = FindFirstObjectByType<GameSaveManager>();

                    // If still null, create new GameObject with manager
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

        #region Settings

        [Header("Save Settings")]
        [Tooltip("Auto-save interval in seconds (default: 120 = 2 minutes)")]
        [SerializeField] private float autoSaveInterval = 120f;

        [Tooltip("Enable auto-save (disable for testing)")]
        [SerializeField] private bool enableAutoSave = true;

        [Tooltip("Enable debug save/load keys (F5/F6/F7)")]
        [SerializeField] private bool enableDebugKeys = true;

        [Header("File Settings")]
        [Tooltip("Name of the save file")]
        [SerializeField] private string saveFileName = "outback_save.json";

        #endregion

        #region Private Fields

        private string saveFilePath;
        private Coroutine autoSaveCoroutine;
        private bool isInitialized = false;

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
            // Start auto-save coroutine
            if (enableAutoSave)
            {
                StartAutoSave();
            }

            // Auto-load on start
            Load();
        }

        private void Update()
        {
            // Debug keys (only if enabled)
            if (enableDebugKeys)
            {
                // F5 = Manual Save
                if (Input.GetKeyDown(KeyCode.F5))
                {
                    Debug.Log("[GameSaveManager] F5 pressed - Manual save triggered");
                    Save();
                }

                // F6 = Manual Load
                if (Input.GetKeyDown(KeyCode.F6))
                {
                    Debug.Log("[GameSaveManager] F6 pressed - Manual load triggered");
                    Load();
                }

                // F7 = Delete Save
                if (Input.GetKeyDown(KeyCode.F7))
                {
                    Debug.Log("[GameSaveManager] F7 pressed - Delete save triggered");
                    DeleteSave();
                }
            }
        }

        private void OnApplicationQuit()
        {
            Debug.Log("[GameSaveManager] Application quitting - saving game...");
            Save();
        }

        private void OnDestroy()
        {
            // Save on destroy (backup for quit)
            if (instance == this)
            {
                Save();
            }
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            if (isInitialized) return;

            // Build save file path
            saveFilePath = Path.Combine(Application.persistentDataPath, saveFileName);

            Debug.Log($"[GameSaveManager] Initialized");
            Debug.Log($"[GameSaveManager] Save file path: {saveFilePath}");
            Debug.Log($"[GameSaveManager] Auto-save: {(enableAutoSave ? "Enabled" : "Disabled")} ({autoSaveInterval}s interval)");
            Debug.Log($"[GameSaveManager] Debug keys: {(enableDebugKeys ? "Enabled" : "Disabled")} (F5=Save, F6=Load, F7=Delete)");

            isInitialized = true;
        }

        #endregion

        #region Save System

        /// <summary>
        /// Saves all game data to disk as JSON
        /// </summary>
        public void Save()
        {
            try
            {
                Debug.Log("[GameSaveManager] Starting save...");

                // Create new save data object
                GameSaveData saveData = new GameSaveData();

                // Gather data from all managers
                CollectSaveData(saveData);

                // Serialize to JSON (pretty print for debugging)
                string json = JsonUtility.ToJson(saveData, true);

                // Write to file
                File.WriteAllText(saveFilePath, json);

                Debug.Log($"[GameSaveManager] ✅ Save successful!");
                Debug.Log($"[GameSaveManager] {saveData.ToString()}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameSaveManager] ❌ Save failed! Error: {e.Message}");
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Loads game data from disk
        /// </summary>
        public void Load()
        {
            try
            {
                // Check if save file exists
                if (!File.Exists(saveFilePath))
                {
                    Debug.Log("[GameSaveManager] No save file found - starting fresh game");
                    return;
                }

                Debug.Log("[GameSaveManager] Loading save file...");

                // Read JSON from file
                string json = File.ReadAllText(saveFilePath);

                // Deserialize
                GameSaveData saveData = JsonUtility.FromJson<GameSaveData>(json);

                if (saveData == null)
                {
                    Debug.LogWarning("[GameSaveManager] Save data is null - starting fresh game");
                    return;
                }

                // Validate save version
                if (saveData.saveVersion != 1)
                {
                    Debug.LogWarning($"[GameSaveManager] Save version mismatch! Expected 1, got {saveData.saveVersion}");
                    Debug.LogWarning("[GameSaveManager] Attempting to load anyway...");
                }

                // Apply data to all managers
                ApplySaveData(saveData);

                Debug.Log($"[GameSaveManager] ✅ Load successful!");
                Debug.Log($"[GameSaveManager] {saveData.ToString()}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameSaveManager] ❌ Load failed! Error: {e.Message}");
                Debug.LogError("[GameSaveManager] Starting fresh game instead");
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Deletes the save file (debug only)
        /// </summary>
        public void DeleteSave()
        {
            try
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
            catch (Exception e)
            {
                Debug.LogError($"[GameSaveManager] ❌ Failed to delete save file! Error: {e.Message}");
                Debug.LogException(e);
            }
        }

        #endregion

        #region Data Collection

        /// <summary>
        /// Collects data from all game managers and populates the save data
        /// </summary>
        private void CollectSaveData(GameSaveData saveData)
        {
            // Collect VisitManager data
            if (VisitManager.Instance != null)
            {
                Dictionary<string, int> visitData = VisitManager.Instance.GetAllVisitData();
                saveData.SetVisitDictionary(visitData);
                Debug.Log($"[GameSaveManager] Saved visit data for {visitData.Count} zones");
            }
            else
            {
                Debug.LogWarning("[GameSaveManager] VisitManager not found - skipping visit data");
            }

            // Collect AnimalDiscoveryManager data
            if (AnimalDiscoveryManager.Instance != null)
            {
                HashSet<string> discoveredAnimals = AnimalDiscoveryManager.Instance.GetDiscoveredAnimals();
                saveData.discoveredAnimals = new List<string>(discoveredAnimals); // Convert HashSet to List
                Debug.Log($"[GameSaveManager] Saved {saveData.discoveredAnimals.Count} discovered animals");
            }
            else
            {
                Debug.LogWarning("[GameSaveManager] AnimalDiscoveryManager not found - skipping animal data");
            }

            // TODO: Task 6 - Collect AnimalDiscoveryManager data
            // if (AnimalDiscoveryManager.Instance != null)
            // {
            //     // Will implement in Task 6
            //     Debug.Log("[GameSaveManager] AnimalDiscoveryManager integration pending (Task 6)");
            // }

            // TODO: Task 7 - Collect ActivatableObject data
            // Will implement in Task 7
            Debug.Log("[GameSaveManager] Building activation integration pending (Task 7)");
        }

        /// <summary>
        /// Applies loaded data to all game managers
        /// </summary>
        private void ApplySaveData(GameSaveData saveData)
        {
            // Apply VisitManager data
            if (VisitManager.Instance != null)
            {
                Dictionary<string, int> visitData = saveData.GetVisitDictionary();
                VisitManager.Instance.LoadVisitData(visitData);
                Debug.Log($"[GameSaveManager] Loaded visit data for {visitData.Count} zones");
            }
            else
            {
                Debug.LogWarning("[GameSaveManager] VisitManager not found - skipping visit data");
            }

            // Apply AnimalDiscoveryManager data
            if (AnimalDiscoveryManager.Instance != null)
            {
                HashSet<string> discoveredAnimals = new HashSet<string>(saveData.discoveredAnimals); // Convert List to HashSet
                AnimalDiscoveryManager.Instance.LoadDiscoveryData(discoveredAnimals);
                Debug.Log($"[GameSaveManager] Loaded {discoveredAnimals.Count} discovered animals");
            }
            else
            {
                Debug.LogWarning("[GameSaveManager] AnimalDiscoveryManager not found - skipping animal data");
            }

            // TODO: Task 6 - Apply AnimalDiscoveryManager data
            // if (AnimalDiscoveryManager.Instance != null)
            // {
            //     // Will implement in Task 6
            //     Debug.Log("[GameSaveManager] AnimalDiscoveryManager integration pending (Task 6)");
            // }

            // TODO: Task 7 - Apply ActivatableObject data
            // Will implement in Task 7
            Debug.Log("[GameSaveManager] Building activation integration pending (Task 7)");
        }

        #endregion

        #region Auto-Save

        /// <summary>
        /// Starts the auto-save coroutine
        /// </summary>
        private void StartAutoSave()
        {
            if (autoSaveCoroutine != null)
            {
                StopCoroutine(autoSaveCoroutine);
            }

            autoSaveCoroutine = StartCoroutine(AutoSaveCoroutine());
            Debug.Log($"[GameSaveManager] Auto-save started (every {autoSaveInterval} seconds)");
        }

        /// <summary>
        /// Auto-save coroutine - saves every X seconds
        /// </summary>
        private IEnumerator AutoSaveCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(autoSaveInterval);

                Debug.Log("[GameSaveManager] Auto-save triggered");
                Save();
            }
        }

        /// <summary>
        /// Stops auto-save (useful for debugging)
        /// </summary>
        public void StopAutoSave()
        {
            if (autoSaveCoroutine != null)
            {
                StopCoroutine(autoSaveCoroutine);
                autoSaveCoroutine = null;
                Debug.Log("[GameSaveManager] Auto-save stopped");
            }
        }

        #endregion

        #region Public Utilities

        /// <summary>
        /// Checks if a save file exists
        /// </summary>
        public bool SaveFileExists()
        {
            return File.Exists(saveFilePath);
        }

        /// <summary>
        /// Gets the full path to the save file
        /// </summary>
        public string GetSaveFilePath()
        {
            return saveFilePath;
        }

        /// <summary>
        /// Gets info about the save file (for UI display)
        /// </summary>
        public string GetSaveFileInfo()
        {
            if (!File.Exists(saveFilePath))
            {
                return "No save file found";
            }

            FileInfo fileInfo = new FileInfo(saveFilePath);
            return $"Last saved: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss} ({fileInfo.Length} bytes)";
        }

        #endregion
    }
}