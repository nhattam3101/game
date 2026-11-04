#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 5D.2 installs only one addon component on the 5D.1 overlay root.
// No changes to earlier source scripts, Save JSON, Bank Gold, or gameplay scenes.
public static class MS5D2Installer
{
    private const string Menu = "Tools/MonsterSurvival/5D Profile Overview/";
    private const string Root = "Assets/MonsterSurvival/Profile5D2";
    private const string Backup = Root + "/Backups/MainMenu_BEFORE_5D2.unity";

    [MenuItem(Menu + "4 - INSTALL 5D.2 Responsive Polish (OPEN MainMenu)")]
    public static void Install()
    {
        if (Application.isPlaying)
        { EditorUtility.DisplayDialog("5D.2", "Stop Play Mode first.", "OK"); return; }
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "MainMenu" || string.IsNullOrEmpty(scene.path))
        { EditorUtility.DisplayDialog("5D.2", "Open saved MainMenu scene first.", "OK"); return; }
        MS5D1ProfileOverview original = FindInScene<MS5D1ProfileOverview>(scene);
        if (!original || !original.launcher || !original.existingProfileWidget ||
            !original.GetComponentInParent<Canvas>())
        {
            EditorUtility.DisplayDialog("5D.2 prerequisites missing",
                "Install/audit 5D.1 first. Missing MS5D1ProfileOverview, launcher, Profile Widget or Canvas. No changes made.", "OK");
            return;
        }
        if (original.GetComponent<MS5D2ProfilePolish>())
        { EditorUtility.DisplayDialog("5D.2", "Already installed. Use 5D.2 AUDIT.", "OK"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!EditorUtility.DisplayDialog("Install 5D.2 / Read-only",
            "Add responsive tabs, scrolling profile cards, original item icons when found, and UI transitions?\n\n" +
            "No Save writes, no changes to existing 5D.1 code, purchases, menu navigation or gameplay.",
            "INSTALL", "CANCEL")) return;
        try
        {
            EnsureFolder(Root + "/Backups");
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(Backup))
            {
                if (!AssetDatabase.CopyAsset(scene.path, Backup)) throw new IOException("Failed to create MainMenu backup.");
                AssetDatabase.ImportAsset(Backup);
            }
            MS5D2ProfilePolish polish = Undo.AddComponent<MS5D2ProfilePolish>(original.gameObject);
            polish.originalOverview = original;
            polish.itemRegistry = FindUniqueAsset<MS5A3ItemRegistry>();
            polish.equipmentCatalog = FindUniqueAsset<MS3DEquipmentCatalog>();
            polish.genericItemIcon = LoadSprite("Bag");
            polish.weaponIcon = LoadSprite("Weapon");
            polish.armorIcon = LoadSprite("Armor");
            polish.accessoryIcon = LoadSprite("Accessory");
            polish.mapIcon = LoadSprite("Map");
            polish.medalIcon = LoadSprite("Medal");
            EditorUtility.SetDirty(polish);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[5D.2] INSTALLED on " + original.name +
                ". Registry=" + (polish.itemRegistry ? polish.itemRegistry.name : "not auto-found; optional manual assignment") +
                "; EquipmentCatalog=" + (polish.equipmentCatalog ? polish.equipmentCatalog.name : "not auto-found; optional manual assignment") +
                ". Saved Scene backup: " + Backup);
            EditorUtility.DisplayDialog("5D.2 Installed",
                "MainMenu has a read-only polished PROFILE view. Open PROFILE in Play Mode.\n\n" +
                "Actual item icons appear when Item Registry is assigned and items have sprites.\n" +
                "Backups: " + Backup, "OK");
        }
        catch (Exception e)
        {
            Debug.LogError("[5D.2] INSTALL ERROR: " + e);
            EditorUtility.DisplayDialog("5D.2", "Install failed: " + e.Message, "OK");
        }
    }

    [MenuItem(Menu + "5 - AUDIT 5D.2 Responsive Polish (READ ONLY)")]
    public static void Audit()
    {
        Scene scene = SceneManager.GetActiveScene();
        MS5D1ProfileOverview original = FindInScene<MS5D1ProfileOverview>(scene);
        MS5D2ProfilePolish polish = FindInScene<MS5D2ProfilePolish>(scene);
        int pass = 0, warn = 0;
        Check(scene.name == "MainMenu", "MainMenu scene is open", ref pass, ref warn);
        Check(original && original.launcher && original.existingProfileWidget, "5D.1 launcher/widget connected", ref pass, ref warn);
        Check(polish && polish.originalOverview == original, "5D.2 overlay attached to same 5D.1 object", ref pass, ref warn);
        if (polish)
        {
            Check(polish.genericItemIcon && polish.weaponIcon && polish.armorIcon &&
                  polish.accessoryIcon && polish.mapIcon && polish.medalIcon,
                  "All 6 category icon sprites linked", ref pass, ref warn);
            if (!polish.itemRegistry) Debug.LogWarning("[5D.2 AUDIT] Item Registry not assigned: generic icons and IDs will be used (read-only fallback).");
            if (!polish.equipmentCatalog) Debug.LogWarning("[5D.2 AUDIT] Equipment Catalog not assigned: rarity tint defaults to purple.");
            if (polish.itemRegistry && polish.itemRegistry.items != null)
                Debug.Log("[5D.2 AUDIT] Registry entries=" + polish.itemRegistry.items.Count + " (saved item IDs resolved only in Play Mode).");
        }
        Debug.Log("[5D.2 AUDIT] Scene=" + scene.name + " | " + pass + " PASS / " + warn + " WARN. " +
                  "READ ONLY. Play Mode/resolution tests must be confirmed manually.");
    }

    [MenuItem(Menu + "6 - REMOVE 5D.2 ONLY (OPEN MainMenu)")]
    public static void Remove()
    {
        if (Application.isPlaying) { Debug.LogWarning("[5D.2] Stop Play Mode before remove."); return; }
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "MainMenu") { Debug.LogWarning("[5D.2] Open MainMenu first."); return; }
        MS5D2ProfilePolish polish = FindInScene<MS5D2ProfilePolish>(scene);
        if (!polish) { Debug.Log("[5D.2] Nothing to remove."); return; }
        if (!EditorUtility.DisplayDialog("Remove 5D.2",
            "Remove ONLY the responsive polish component?\n5D.1 PROFILE UI, Save data and all menus remain unchanged.",
            "REMOVE", "CANCEL")) return;
        Undo.DestroyObjectImmediate(polish);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[5D.2] Removed addon component. 5D.1 original UI remains available.");
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component) return component;
        }
        return null;
    }
    private static T FindUniqueAsset<T>() where T : UnityEngine.Object
    {
        string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
        if (guids.Length != 1)
        {
            Debug.LogWarning("[5D.2] Found " + guids.Length + " " + typeof(T).Name +
                " asset(s). Assign exact intended asset in MS5D2ProfilePolish Inspector if needed.");
            return null;
        }
        return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }
    private static Sprite LoadSprite(string fileName)
    {
        string path = Root + "/Art/MS5D2_" + fileName + ".png";
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new IOException("Missing icon PNG: " + path);
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (!sprite) throw new IOException("Could not import Sprite: " + path);
        return sprite;
    }
    private static void EnsureFolder(string path)
    {
        string[] folders = path.Split('/');
        string parent = folders[0];
        for (int i = 1; i < folders.Length; i++)
        {
            string next = parent + "/" + folders[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, folders[i]);
            parent = next;
        }
    }
    private static void Check(bool result, string label, ref int pass, ref int warn)
    {
        if (result) { pass++; Debug.Log("[5D.2 AUDIT] PASS  " + label); }
        else { warn++; Debug.LogWarning("[5D.2 AUDIT] WARN  " + label); }
    }
}
#endif
