// ZoneDataMigrationTool.cs
// Location: Assets/Scripts/Editor/ZoneDataMigrationTool.cs
// IMPORTANT: must live inside a folder named "Editor" — Unity then excludes it from builds.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RelaxingDrive.World;

namespace RelaxingDrive.EditorTools
{
    /// <summary>
    /// One-off migration helper: converts typed zone ID strings into shared ZoneData assets.
    ///
    /// Menu: Tools > Sunday Drive > Zones >
    ///   • Report Zone Links (no changes) — read-only health check, safe to run any time
    ///   • Migrate Scene Zone IDs to ZoneData — creates assets and links them
    ///
    /// Concept — SerializedObject:
    /// Instead of adding public setters to runtime classes just for this tool, we edit their
    /// private [SerializeField] fields the same way the Inspector does. That automatically
    /// supports Undo, prefab overrides and marking the scene as changed.
    /// </summary>
    public static class ZoneDataMigrationTool
    {
        private const string DataFolder = "Assets/Data";
        private const string ZoneFolder = "Assets/Data/Zones";

        // Serialized field names on the runtime classes (must match the C# field names).
        private const string ZoneDataField = "zoneData";
        private const string ZoneLegacyField = "legacyZoneID";
        private const string BuildingZoneField = "targetZone";
        private const string BuildingLegacyField = "targetZoneID";

        [MenuItem("Tools/Sunday Drive/Zones/Report Zone Links (no changes)")]
        private static void Report() => Run(apply: false);

        [MenuItem("Tools/Sunday Drive/Zones/Migrate Scene Zone IDs to ZoneData")]
        private static void Migrate()
        {
            bool ok = EditorUtility.DisplayDialog(
                "Migrate zone IDs",
                "This creates ZoneData assets in Assets/Data/Zones (one per unique ID) and links every " +
                "VisitZone and ActivatableObject in the open scene.\n\n" +
                "Scene links can be undone with Ctrl+Z. Created assets are not removed by Undo.\n\n" +
                "Tip: commit to git first.",
                "Migrate", "Cancel");

            if (ok) Run(apply: true);
        }

        private static void Run(bool apply)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[ZoneMigration] Exit Play mode first — changes made in Play mode are lost.");
                return;
            }

            VisitZone[] zones = Object.FindObjectsByType<VisitZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            ActivatableObject[] buildings = Object.FindObjectsByType<ActivatableObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Dictionary<string, ZoneData> assetsById = LoadExistingZoneAssets();

            var zoneIds = new HashSet<string>();
            var buildingsById = new Dictionary<string, List<string>>();
            var problems = new StringBuilder();
            int created = 0, linkedZones = 0, linkedBuildings = 0;

            Undo.SetCurrentGroupName("Migrate Zone IDs to ZoneData");
            int undoGroup = Undo.GetCurrentGroup();

            // --- Visit zones ---
            foreach (VisitZone zone in zones)
            {
                var so = new SerializedObject(zone);
                SerializedProperty dataProp = so.FindProperty(ZoneDataField);
                SerializedProperty legacyProp = so.FindProperty(ZoneLegacyField);

                string id = ReadId(dataProp, legacyProp);
                if (string.IsNullOrEmpty(id))
                {
                    problems.AppendLine($"⚠ VisitZone '{zone.name}' has NO ID — create/assign a ZoneData by hand.");
                    continue;
                }

                zoneIds.Add(id);

                if (apply && dataProp.objectReferenceValue == null)
                {
                    dataProp.objectReferenceValue = GetOrCreateAsset(id, assetsById, ref created);
                    so.ApplyModifiedProperties(); // records Undo + marks dirty
                    linkedZones++;
                }
            }

            // --- Buildings ---
            foreach (ActivatableObject building in buildings)
            {
                var so = new SerializedObject(building);
                SerializedProperty dataProp = so.FindProperty(BuildingZoneField);
                SerializedProperty legacyProp = so.FindProperty(BuildingLegacyField);

                if (dataProp == null)
                {
                    Debug.LogError("[ZoneMigration] ActivatableObject has no 'targetZone' field yet — " +
                                   "apply the ActivatableObject edits first, then run again.");
                    return;
                }

                string id = ReadId(dataProp, legacyProp);
                if (string.IsNullOrEmpty(id))
                {
                    problems.AppendLine($"⚠ Building '{building.name}' targets no zone.");
                    continue;
                }

                if (!buildingsById.TryGetValue(id, out List<string> names))
                    buildingsById[id] = names = new List<string>();
                names.Add(building.name);

                if (apply && dataProp.objectReferenceValue == null)
                {
                    dataProp.objectReferenceValue = GetOrCreateAsset(id, assetsById, ref created);
                    so.ApplyModifiedProperties();
                    linkedBuildings++;
                }
            }

            // --- Cross-check: which links can never fire? ---
            foreach (var pair in buildingsById.Where(p => !zoneIds.Contains(p.Key)))
            {
                problems.AppendLine($"✖ No VisitZone has ID '{pair.Key}' — these can NEVER activate: " +
                                    string.Join(", ", pair.Value));
            }

            foreach (string id in zoneIds.Where(id => !buildingsById.ContainsKey(id)))
            {
                problems.AppendLine($"• Zone '{id}' unlocks nothing (fine if it's for future content or hints).");
            }

            if (apply)
            {
                Undo.CollapseUndoOperations(undoGroup);
                EditorSceneManager.MarkAllScenesDirty();
                AssetDatabase.SaveAssets();
            }

            // --- Report ---
            var report = new StringBuilder();
            report.AppendLine(apply ? "[ZoneMigration] ✅ Migration complete" : "[ZoneMigration] Report (no changes made)");
            report.AppendLine($"Zones: {zones.Length}  |  Buildings: {buildings.Length}  |  Unique zone IDs: {zoneIds.Count}");
            if (apply)
                report.AppendLine($"Assets created: {created}  |  Zones linked: {linkedZones}  |  Buildings linked: {linkedBuildings}");
            report.AppendLine(problems.Length > 0 ? "\nIssues:\n" + problems : "\nNo issues found.");
            if (apply) report.AppendLine("\nRemember to SAVE THE SCENE (Ctrl+S).");

            Debug.Log(report.ToString());
        }

        /// <summary>Asset reference wins; otherwise the trimmed legacy string.</summary>
        private static string ReadId(SerializedProperty dataProp, SerializedProperty legacyProp)
        {
            if (dataProp != null && dataProp.objectReferenceValue is ZoneData data)
                return data.ZoneID;

            return legacyProp != null ? legacyProp.stringValue?.Trim() : null;
        }

        private static Dictionary<string, ZoneData> LoadExistingZoneAssets()
        {
            var result = new Dictionary<string, ZoneData>();
            foreach (string guid in AssetDatabase.FindAssets("t:ZoneData"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ZoneData>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && !string.IsNullOrEmpty(asset.ZoneID) && !result.ContainsKey(asset.ZoneID))
                    result[asset.ZoneID] = asset;
            }
            return result;
        }

        private static ZoneData GetOrCreateAsset(string id, Dictionary<string, ZoneData> assetsById, ref int created)
        {
            if (assetsById.TryGetValue(id, out ZoneData existing))
                return existing; // reuse: two zones/buildings with the same ID share ONE asset

            if (!AssetDatabase.IsValidFolder(DataFolder)) AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder(ZoneFolder)) AssetDatabase.CreateFolder(DataFolder, "Zones");

            ZoneData asset = ScriptableObject.CreateInstance<ZoneData>();
            var so = new SerializedObject(asset);
            so.FindProperty("zoneID").stringValue = id; // keep the exact old ID so saves still match
            so.ApplyModifiedPropertiesWithoutUndo();
            // displayName is left empty on purpose: it falls back to the ID until you name it.

            string path = AssetDatabase.GenerateUniqueAssetPath($"{ZoneFolder}/Zone_{SafeFileName(id)}.asset");
            AssetDatabase.CreateAsset(asset, path);

            assetsById[id] = asset;
            created++;
            return asset;
        }

        private static string SafeFileName(string id)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                id = id.Replace(c, '_');
            return id.Replace(' ', '_');
        }
    }
}
