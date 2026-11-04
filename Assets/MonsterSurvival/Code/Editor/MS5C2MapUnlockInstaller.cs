#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Non-destructive installer. Adds ONE component to existing MMV3_Root.
// Does not change the map controller, prefabs, game scene or saved profiles.
public static class MS5C2MapUnlockInstaller
{
    private const string Menu = "Tools/MonsterSurvival/5C Unlock System/";
    private const string AssetRoot = "Assets/MonsterSurvival/Features/Progression/Unlock5C2";
    private const string IconPath = AssetRoot + "/Art/MapLock.png";
    private const string BackupPath = AssetRoot + "/Backups/MainMenu_BEFORE_5C2.unity";

    [MenuItem(Menu + "3 - INSTALL 5C.2 Map Unlock UI (OPEN MainMenu)")]
    public static void Install()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("5C.2", "Exit Play Mode before installing.", "OK");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "MainMenu")
        {
            EditorUtility.DisplayDialog("5C.2", "Open the MainMenu Scene first. This installer does not modify SampleScene.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        GameObject root = Find(scene, "MMV3_Root");
        GameObject canvas = Find(scene, "MMV3_Canvas");
        MMV3MenuController menu = root ? root.GetComponent<MMV3MenuController>() : null;
        MS5A2ProfileWidget widget = FindComponent<MS5A2ProfileWidget>(scene);
        if (!root || !canvas || !menu || !widget || !canvas.GetComponent<Canvas>() ||
            !canvas.GetComponent<GraphicRaycaster>() || menu.mapCards == null || menu.mapCards.Length < 3 ||
            !menu.mapsPage || !menu.mapsStartButton)
        {
            EditorUtility.DisplayDialog("5C.2 prerequisites missing",
                "Requires MMV3_Canvas, MMV3_Root with MMV3MenuController, three map cards, Start button and 5A.2 Profile Widget.", "OK");
            return;
        }
        for (int i = 0; i < 3; i++)
            if (!menu.mapCards[i])
            {
                EditorUtility.DisplayDialog("5C.2", "Map card reference " + i + " is missing.", "OK");
                return;
            }
        if (root.GetComponent<MS5C2MapUnlockUI>())
        {
            EditorUtility.DisplayDialog("5C.2", "Already installed. No duplicate component created.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("5C.2 MAP UNLOCK UI",
            "Add LOCKED / UNLOCKED indicators and confirmed unlock purchases to Main Menu V3?\n\n" +
            "SAFE PREVIEW: Existing maps remain playable. No hard gating is enabled in 5C.2.\n" +
            "No Save files are edited by INSTALL.", "INSTALL", "CANCEL")) return;
        try
        {
            EnsureFolder(AssetRoot + "/Backups");
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(BackupPath))
            {
                if (!AssetDatabase.CopyAsset(scene.path, BackupPath))
                    throw new IOException("Could not create MainMenu Scene backup.");
                AssetDatabase.ImportAsset(BackupPath);
            }
            TextureImporter importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
            if (!importer) throw new FileNotFoundException("Missing lock icon: " + IconPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            if (!sprite) throw new InvalidOperationException("Could not import MapLock.png as Sprite.");

            MS5C2MapUnlockUI component = root.AddComponent<MS5C2MapUnlockUI>();
            component.menu = menu;
            component.profileWidget = widget;
            component.lockIcon = sprite;
            EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[5C.2] Installed Map Unlock PREVIEW on MMV3_Root. Saved Scene only; no profile JSON touched.");
            EditorUtility.DisplayDialog("5C.2 INSTALLED",
                "Enter Play Mode: MainMenu -> MAP SELECT.\n" +
                "LOCKED cards show price and UNLOCK button. Confirming unlock purchases with real Bank Gold.\n" +
                "Existing map PLAY remains unchanged (PREVIEW / SOFT GATING).", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogError("[5C.2] Install failed: " + ex);
            EditorUtility.DisplayDialog("5C.2 INSTALL ERROR", ex.Message, "OK");
        }
    }

    [MenuItem(Menu + "4 - AUDIT 5C.2 Map Unlock UI (READ ONLY)")]
    public static void Audit()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject root = Find(scene, "MMV3_Root");
        MMV3MenuController menu = root ? root.GetComponent<MMV3MenuController>() : null;
        MS5C2MapUnlockUI ui = root ? root.GetComponent<MS5C2MapUnlockUI>() : null;
        bool cards = menu && menu.mapCards != null && menu.mapCards.Length >= 3 &&
                     menu.mapCards[0] && menu.mapCards[1] && menu.mapCards[2];
        Debug.Log("[5C.2 AUDIT] Scene=" + scene.name +
            " Menu=" + (menu ? "PASS" : "MISSING") +
            " Cards=" + (cards ? "PASS" : "MISSING") +
            " OverlayComponent=" + (ui ? "PASS" : "MISSING") +
            " Profile=" + (ui && ui.profileWidget ? "PASS" : "MISSING") +
            " LockIcon=" + (ui && ui.lockIcon ? "PASS" : "MISSING"));
        for (int slot = 1; slot <= 3; slot++)
        {
            MS5C1UnlockService.Snapshot snapshot;
            string message;
            bool valid = MS5C1UnlockService.TryGetSnapshot(slot, out snapshot, out message);
            Debug.Log("[5C.2 AUDIT] PROFILE " + slot + " | " + message +
                (valid ? " | Bank Gold " + snapshot.bankGold + " | Meadow " + snapshot.IsUnlocked("map.green_meadow") +
                          " | Desert " + snapshot.IsUnlocked("map.sand_dunes") +
                          " | Ruins " + snapshot.IsUnlocked("map.ancient_ruins") : " | READ BLOCKED"));
        }
        Debug.Log("[5C.2 AUDIT] READ ONLY. MAP START IS NOT GATED. No profiles saved or migrated.");
    }

    [MenuItem(Menu + "5 - REMOVE 5C.2 Map Unlock UI ONLY (OPEN MainMenu)")]
    public static void Remove()
    {
        if (Application.isPlaying) { Debug.LogWarning("[5C.2] Stop Play Mode."); return; }
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "MainMenu") { Debug.LogWarning("[5C.2] Open MainMenu first."); return; }
        GameObject root = Find(scene, "MMV3_Root");
        MS5C2MapUnlockUI component = root ? root.GetComponent<MS5C2MapUnlockUI>() : null;
        if (!component) { Debug.Log("[5C.2] Not installed."); return; }
        if (!EditorUtility.DisplayDialog("Remove 5C.2 UI",
            "Remove only the 5C.2 runtime UI component?\n" +
            "Purchases, Gold, old map controller and Save files will be kept.", "REMOVE UI", "CANCEL")) return;
        Undo.DestroyObjectImmediate(component);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[5C.2] UI component removed; player data unchanged.");
    }

    private static GameObject Find(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root;
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                if (transform.name == name) return transform.gameObject;
        }
        return null;
    }

    private static T FindComponent<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component) return component;
        }
        return null;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
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
