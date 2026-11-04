#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Installs an atomic 5A.3 Victory watcher and DISABLES the old 5A.2 watcher.
// No MainMenu modifications, no loot table changes, and no real save data writes.
public static class MS5A3Installer
{
    private const string Root = "Assets/MonsterSurvival/Features/Progression/Save5A3";
    private const string RegistryPath = Root + "/MS5A3ItemRegistry.asset";
    private const string BackupDir = Root + "/Backups";
    private const string BridgeName = "MS5A3_PersistentInventoryEquipment";

    [MenuItem("Tools/MonsterSurvival/5A Save System/6 - INSTALL 5A.3 Inventory + Equipment (OPEN SampleScene)")]
    public static void Install()
    {
        if (Application.isPlaying) { Debug.LogError("[5A.3] Stop Play Mode first."); return; }
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "SampleScene")
        { EditorUtility.DisplayDialog("5A.3", "Open SampleScene first.", "OK"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        GameManager gm = Find<GameManager>(scene);
        PlayerProgression progression = Find<PlayerProgression>(scene);
        MS3APlayerInventory inventory = Find<MS3APlayerInventory>(scene);
        MS3DPlayerEquipment equipment = inventory ? inventory.GetComponent<MS3DPlayerEquipment>() : null;
        MS5A2BankOnVictory legacy = Find<MS5A2BankOnVictory>(scene);
        if (!gm || !progression || !inventory || !equipment || !equipment.catalog || !legacy)
        {
            EditorUtility.DisplayDialog("5A.3: Dependencies missing",
                "Needs GameManager, PlayerProgression, Inventory 3A, Equipment 3D.1, catalog and 5A.2 Gold watcher.\n"+
                "Install 5A.1, 5A.2 (hotfix 5A.2.1), 3A and 3D.1 first.", "OK");
            return;
        }
        var allItems = new List<MS3AItemData>();
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var guid in AssetDatabase.FindAssets("t:MS3AItemData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var item = AssetDatabase.LoadAssetAtPath<MS3AItemData>(path);
            if (!item) continue;
            string id = item.itemId;
            if (string.IsNullOrWhiteSpace(id) || id == "item_unique_id" || id.Length > 96 ||
                id.Any(c => !(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.')))
            { EditorUtility.DisplayDialog("5A.3: Invalid Item ID", "Fix ID on item: " + path + " (" + id + ")", "OK"); return; }
            if (!known.Add(id))
            { EditorUtility.DisplayDialog("5A.3: DUPLICATE Item ID", "Two ItemData assets share ID: " + id + ". Fix before proceeding.", "OK"); return; }
            allItems.Add(item);
        }
        if (allItems.Count == 0)
        { EditorUtility.DisplayDialog("5A.3", "No ItemData assets found.", "OK"); return; }
        // All equipment entries must be part of registry.
        if (equipment.catalog.entries != null)
            foreach (var row in equipment.catalog.entries)
                if (row != null && row.item && !allItems.Contains(row.item))
                { EditorUtility.DisplayDialog("5A.3", "Equipment Catalog has an item missing from AssetDatabase scan.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("5A.3 — Persistent Inventory + Equipment",
            "Registry: " + allItems.Count + " unique ItemData assets.\n"+
            "Create Scene backup, add 5A.3 unified Victory save (Gold + 20 slots + 3 equipment slots), " +
            "DISABLE legacy 5A.2 watcher to avoid duplicate Gold.\n\n"+
            "Existing profile JSON files are NOT edited during installation. Continue?", "INSTALL", "CANCEL")) return;
        try
        {
            EnsureDir(BackupDir);
            string backupName = BackupDir + "/SampleScene_BEFORE_5A3.unity";
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(backupName) &&
                !AssetDatabase.CopyAsset(scene.path, backupName))
                throw new IOException("Cannot make Scene backup. Nothing installed.");
            AssetDatabase.ImportAsset(backupName);
            var registry = AssetDatabase.LoadAssetAtPath<MS5A3ItemRegistry>(RegistryPath);
            if (!registry)
            {
                registry = ScriptableObject.CreateInstance<MS5A3ItemRegistry>();
                AssetDatabase.CreateAsset(registry, RegistryPath);
            }
            // Retain IDs of now-deleted assets? Fail instead of leaving null entries.
            // Registry updates do not mutate existing JSON; deleted ItemData will fail safe on restore.
            Undo.RecordObject(registry, "5A.3 item registry");
            registry.items = allItems.OrderBy(x => x.itemId, StringComparer.Ordinal).ToList();
            EditorUtility.SetDirty(registry);
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == BridgeName);
            bool isNew = root == null;
            if (isNew)
            {
                root = new GameObject(BridgeName);
                Undo.RegisterCreatedObjectUndo(root, "5A.3 watcher");
                SceneManager.MoveGameObjectToScene(root, scene);
            }
            var bridge = root.GetComponent<MS5A3PersistentRun>();
            bool wasActive = legacy.enabled;
            if (!bridge) bridge = Undo.AddComponent<MS5A3PersistentRun>(root);
            Undo.RecordObject(bridge, "5A.3 references");
            bridge.gameManager = gm;
            bridge.progression = progression;
            bridge.inventory = inventory;
            bridge.equipment = equipment;
            bridge.itemRegistry = registry;
            bridge.legacyGoldWatcher = legacy;
            if (isNew) bridge.SetLegacyOriginalState(wasActive);
            Undo.RecordObject(legacy, "Disable old Gold watcher (unified 5A.3 save)");
            legacy.enabled = false;
            EditorUtility.SetDirty(legacy);
            EditorUtility.SetDirty(bridge);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[5A.3] INSTALL OK — registry=" + allItems.Count + " items; unified save ENABLED; legacy Gold watcher OFF. No real save files touched.");
            EditorUtility.DisplayDialog("5A.3 installed", "Saved SampleScene. Run AUDIT 5A.3; Play via MainMenu profile selection, then Victory. See README.", "OK");
        }
        catch (Exception ex)
        { Debug.LogError("[5A.3] Installer failed: " + ex); EditorUtility.DisplayDialog("5A.3 failed", ex.Message + "\nBackup: " + BackupDir, "OK"); }
    }

    [MenuItem("Tools/MonsterSurvival/5A Save System/7 - AUDIT 5A.3 (READ ONLY)")]
    public static void Audit()
    {
        Scene scene = SceneManager.GetActiveScene();
        var registry = AssetDatabase.LoadAssetAtPath<MS5A3ItemRegistry>(RegistryPath);
        Dictionary<string, MS3AItemData> lookup = null;
        string desc = "MISSING REGISTRY";
        bool valid = false;
        if (registry)
        {
            valid = registry.TryBuild(out lookup, out desc);
        }
        var bridge = Find<MS5A3PersistentRun>(scene);
        var legacy = Find<MS5A2BankOnVictory>(scene);
        var inventory = Find<MS3APlayerInventory>(scene);
        var equipment = Find<MS3DPlayerEquipment>(scene);
        var profile = MS5A2GoldBank.SelectedSlot;
        MS5A1SaveData saved;
        bool recovered;
        string loadStatus;
        bool loaded = MS5A1SaveService.TryLoad(profile, out saved, out loadStatus, out recovered);
        Debug.Log("[5A.3 AUDIT] Scene=" + scene.name +
                  " | Registry=" + (valid ? desc : "INVALID: " + desc) +
                  " | Bridge=" + (bridge ? "YES" : "MISSING") +
                  " | LegacyGoldWatcher=" + (legacy ? (legacy.enabled ? "ERROR: STILL ENABLED" : "OFF (correct)") : "MISSING") +
                  " | Inventory=" + (inventory ? inventory.Slots.Count.ToString() : "MISSING") + "/20" +
                  " | Equipment=" + (equipment ? "YES" : "MISSING") +
                  " | SelectedProfile=" + profile +
                  " | Save=" + loadStatus +
                  (loaded ? " | Snapshot=" + saved.inventorySnapshotPresent : "") +
                  " | This is a READ-ONLY check. Play Mode tests are required.");
    }

    [MenuItem("Tools/MonsterSurvival/5A Save System/8 - REMOVE 5A.3 Scene Watcher (NO SAVE DELETION)")]
    public static void Remove()
    {
        if (Application.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "SampleScene") { EditorUtility.DisplayDialog("5A.3", "Open SampleScene", "OK"); return; }
        var bridge = Find<MS5A3PersistentRun>(scene);
        if (!bridge) { Debug.Log("[5A.3] No watcher installed."); return; }
        if (!EditorUtility.DisplayDialog("REMOVE 5A.3", "Remove watcher and restore old 5A.2 Gold watcher state?\n\nSaved JSON files and ScriptableObject assets will NOT be deleted.\nExisting 5A.3 item snapshots will remain on disk.", "REMOVE", "CANCEL")) return;
        if (bridge.legacyGoldWatcher)
        {
            Undo.RecordObject(bridge.legacyGoldWatcher, "Restore 5A.2 watcher");
            bridge.legacyGoldWatcher.enabled = bridge.LegacyWatcherWasEnabled;
        }
        Undo.DestroyObjectImmediate(bridge.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[5A.3] Removed Scene bridge; 5A.2 state restored. Existing persistent JSON preserved. Code patches may be retained for old saves.");
    }

    [MenuItem("Tools/MonsterSurvival/5A Save System/9 - RUN 5A.3 ISOLATED Save QA (QA SLOT 03)")]
    public static void RunQA()
    {
        if (Application.isPlaying) { Debug.LogWarning("[5A.3 QA] Stop Play Mode first"); return; }
        const int slot = 3;
        var filename = MS5A1SaveService.GetSavePath(slot, true);
        var backup = MS5A1SaveService.GetBackupPath(slot, true);
        if (File.Exists(filename) || File.Exists(backup))
        {
            Debug.LogWarning("[5A.3 QA] SKIPPED disk QA: qa_slot_03 already exists. Will not overwrite any earlier QA data. Run Read-only audit or use fresh environment.");
            return;
        }
        bool wrote = false;
        try
        {
            MS5A1SaveData fresh = MS5A1SaveService.CreateFresh(slot, true);
            fresh.bankGold = 123;
            fresh.inventorySnapshotPresent = true;
            fresh.lastCommittedRunId = "5a3_qa_run";
            fresh.inventorySlots = new List<MS5A3SavedStack>();
            fresh.equippedItems = new List<MS5A3SavedEquipped>();
            for (int i = 0; i < 20; i++) fresh.inventorySlots.Add(new MS5A3SavedStack());
            for (int i = 0; i < 3; i++) fresh.equippedItems.Add(new MS5A3SavedEquipped { type = i });
            MS5A1SaveData result;
            string message;
            if (!MS5A1SaveService.TrySave(slot, fresh, out result, out message, true))
            { Debug.LogError("[5A.3 QA] FAIL: " + message); return; }
            wrote = true;
            MS5A1SaveData back;
            bool recovered;
            bool ok = MS5A1SaveService.TryLoad(slot, out back, out message, out recovered, true) &&
                      back.inventorySnapshotPresent && back.bankGold == 123 && back.lastCommittedRunId == "5a3_qa_run" &&
                      back.inventorySlots != null && back.inventorySlots.Count == 20 &&
                      back.equippedItems != null && back.equippedItems.Count == 3;
            Debug.Log("[5A.3 QA] " + (ok ? "PASS" : "FAIL") + " Gold + exact 20+3 slots serialized together in QA slot 03. No real profile data touched. " + message);
        }
        finally
        {
            // Delete only files created by this run; no existing files were present.
            if (wrote && File.Exists(filename)) File.Delete(filename);
            if (File.Exists(backup)) File.Delete(backup);
        }
    }

    private static T Find<T>(Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var c = root.GetComponentInChildren<T>(true);
            if (c) return c;
        }
        return null;
    }
    private static void EnsureDir(string path)
    {
        var parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
