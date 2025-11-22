// BuildingIDHelper.cs
// Place this file in: Assets/Scripts/Editor/BuildingIDHelper.cs
// This Editor script helps you manage Building IDs for the save system

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using RelaxingDrive.World;

namespace RelaxingDrive.EditorTools
{
    /// <summary>
    /// Editor window to help manage Building IDs for save/load system.
    /// Access via: Tools > Building ID Helper
    /// </summary>
    public class BuildingIDHelper : EditorWindow
    {
        private Vector2 scrollPosition;
        private bool showOnlyMissingIDs = false;
        private bool showOnlyDuplicates = false;

        [MenuItem("Tools/Building ID Helper")]
        public static void ShowWindow()
        {
            GetWindow<BuildingIDHelper>("Building ID Helper");
        }

        private void OnGUI()
        {
            GUILayout.Label("Building ID Management", EditorStyles.boldLabel);
            GUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Building IDs are required for the save/load system to work correctly.\n" +
                "Each building must have a UNIQUE ID.\n\n" +
                "Use this tool to:\n" +
                "• Find buildings without IDs\n" +
                "• Detect duplicate IDs\n" +
                "• Auto-generate unique IDs",
                MessageType.Info
            );

            GUILayout.Space(10);

            // Filter options
            EditorGUILayout.BeginHorizontal();
            showOnlyMissingIDs = GUILayout.Toggle(showOnlyMissingIDs, "Show Only Missing IDs");
            showOnlyDuplicates = GUILayout.Toggle(showOnlyDuplicates, "Show Only Duplicates");
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Action buttons
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Scan Scene for Buildings", GUILayout.Height(30)))
            {
                ScanAndDisplayBuildings();
            }

            if (GUILayout.Button("Auto-Generate All Missing IDs", GUILayout.Height(30)))
            {
                AutoGenerateAllMissingIDs();
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Display buildings
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DisplayBuildingList();
            EditorGUILayout.EndScrollView();
        }

        private void ScanAndDisplayBuildings()
        {
            Repaint();
        }

        private void DisplayBuildingList()
        {
            // Find all ActivatableObjects in scene
            ActivatableObject[] buildings = FindObjectsByType<ActivatableObject>(FindObjectsSortMode.None);

            if (buildings.Length == 0)
            {
                EditorGUILayout.HelpBox("No ActivatableObjects found in scene.", MessageType.Warning);
                return;
            }

            // Check for duplicates
            Dictionary<string, List<ActivatableObject>> idGroups = new Dictionary<string, List<ActivatableObject>>();
            List<ActivatableObject> missingIDBuildings = new List<ActivatableObject>();

            foreach (var building in buildings)
            {
                string id = building.BuildingID;

                if (string.IsNullOrEmpty(id))
                {
                    missingIDBuildings.Add(building);
                }
                else
                {
                    if (!idGroups.ContainsKey(id))
                    {
                        idGroups[id] = new List<ActivatableObject>();
                    }
                    idGroups[id].Add(building);
                }
            }

            // Find duplicates
            var duplicates = idGroups.Where(kvp => kvp.Value.Count > 1).ToList();

            // Display summary
            EditorGUILayout.LabelField($"Total Buildings: {buildings.Length}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Missing IDs: {missingIDBuildings.Count}",
                missingIDBuildings.Count > 0 ? GetErrorStyle() : EditorStyles.label);
            EditorGUILayout.LabelField($"Duplicate IDs: {duplicates.Count}",
                duplicates.Count > 0 ? GetErrorStyle() : EditorStyles.label);

            GUILayout.Space(10);

            // Display missing IDs
            if (missingIDBuildings.Count > 0 && (!showOnlyDuplicates || showOnlyMissingIDs))
            {
                EditorGUILayout.LabelField("⚠️ Buildings Missing IDs:", EditorStyles.boldLabel);

                foreach (var building in missingIDBuildings)
                {
                    EditorGUILayout.BeginHorizontal("box");

                    EditorGUILayout.ObjectField(building, typeof(ActivatableObject), true);

                    if (GUILayout.Button("Generate ID", GUILayout.Width(100)))
                    {
                        GenerateIDForBuilding(building);
                    }

                    EditorGUILayout.EndHorizontal();
                }

                GUILayout.Space(10);
            }

            // Display duplicates
            if (duplicates.Count > 0 && (!showOnlyMissingIDs || showOnlyDuplicates))
            {
                EditorGUILayout.LabelField("❌ Duplicate IDs Found:", EditorStyles.boldLabel);

                foreach (var duplicate in duplicates)
                {
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField($"ID: '{duplicate.Key}' (used {duplicate.Value.Count} times)", GetErrorStyle());

                    foreach (var building in duplicate.Value)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.ObjectField(building, typeof(ActivatableObject), true);

                        if (GUILayout.Button("Fix Duplicate", GUILayout.Width(100)))
                        {
                            GenerateIDForBuilding(building);
                        }

                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.EndVertical();
                    GUILayout.Space(5);
                }
            }

            // Display valid buildings
            if (!showOnlyMissingIDs && !showOnlyDuplicates)
            {
                var validBuildings = idGroups.Where(kvp => kvp.Value.Count == 1).ToList();

                if (validBuildings.Count > 0)
                {
                    GUILayout.Space(10);
                    EditorGUILayout.LabelField($"✅ Valid Buildings ({validBuildings.Count}):", EditorStyles.boldLabel);

                    foreach (var valid in validBuildings)
                    {
                        EditorGUILayout.BeginHorizontal("box");
                        EditorGUILayout.LabelField(valid.Key, GUILayout.Width(200));
                        EditorGUILayout.ObjectField(valid.Value[0], typeof(ActivatableObject), true);
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
        }

        private void AutoGenerateAllMissingIDs()
        {
            ActivatableObject[] buildings = FindObjectsByType<ActivatableObject>(FindObjectsSortMode.None);
            int count = 0;

            foreach (var building in buildings)
            {
                if (string.IsNullOrEmpty(building.BuildingID))
                {
                    GenerateIDForBuilding(building);
                    count++;
                }
            }

            Debug.Log($"[BuildingIDHelper] Generated IDs for {count} buildings");
            Repaint();
        }

        private void GenerateIDForBuilding(ActivatableObject building)
        {
            // Generate a unique ID based on GameObject name and instance ID
            string baseName = building.gameObject.name.Replace(" ", "_").ToLower();
            string uniqueID = $"{baseName}_{building.GetInstanceID().ToString().Replace("-", "")}";

            // Use SerializedObject to modify the private field
            SerializedObject so = new SerializedObject(building);
            SerializedProperty prop = so.FindProperty("buildingID");

            if (prop != null)
            {
                prop.stringValue = uniqueID;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(building);

                Debug.Log($"[BuildingIDHelper] Generated ID for '{building.gameObject.name}': {uniqueID}");
            }
            else
            {
                Debug.LogError($"[BuildingIDHelper] Could not find 'buildingID' field on {building.gameObject.name}");
            }
        }

        private GUIStyle GetErrorStyle()
        {
            GUIStyle style = new GUIStyle(EditorStyles.label);
            style.normal.textColor = Color.red;
            style.fontStyle = FontStyle.Bold;
            return style;
        }
    }
}
#endif