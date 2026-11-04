#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 5D.3.2: assign 8 visual-only sprites to existing 5D.3 tab.
// No Save reads or writes, no new UI root, no gameplay changes.
public static class MS5D32StatisticsIconsInstaller
{
    private const string Menu = "Tools/MonsterSurvival/5D Profile Overview/";
    private const string Art = "Assets/MonsterSurvival/Features/Progression/Profile5D3/Art/MS5D32_";

    [MenuItem(Menu + "11 - APPLY 5D.3.2 Statistics Icons (OPEN MainMenu)")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorUtility.DisplayDialog("5D.3.2", "Exit Play Mode before applying.", "OK"); return; }
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "MainMenu" || string.IsNullOrEmpty(scene.path))
        { EditorUtility.DisplayDialog("5D.3.2", "Open saved MainMenu scene first.", "OK"); return; }
        MS5D3StatisticsTab tab = FindTab(scene);
        if (!tab || !tab.GetComponent<MS5D2ProfilePolish>())
        { EditorUtility.DisplayDialog("5D.3.2", "5D.3 and 5D.2 are required on the Profile object.", "OK"); return; }
        string[] names = { "Victories", "VictoryGold", "ShadowTitan", "GraveWarden", "Maps", "BossBadges", "Upgrades", "Licenses" };
        Sprite[] sprites = new Sprite[names.Length];
        try
        {
            for (int i = 0; i < names.Length; i++)
            {
                string path = Art + names[i] + ".png";
                TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) throw new IOException("Missing PNG: " + path);
                if (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single)
                {
                    ti.textureType = TextureImporterType.Sprite;
                    ti.spriteImportMode = SpriteImportMode.Single;
                    ti.alphaIsTransparency = true;
                    ti.mipmapEnabled = false;
                    ti.SaveAndReimport();
                }
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (!sprites[i]) throw new IOException("Could not import sprite " + path);
            }
            Undo.RecordObject(tab, "Assign Statistics Icons 5D.3.2");
            tab.victoriesIcon = sprites[0];
            tab.victoryGoldIcon = sprites[1];
            tab.shadowTitanIcon = sprites[2];
            tab.graveWardenIcon = sprites[3];
            tab.mapsIcon = sprites[4];
            tab.bossBadgesIcon = sprites[5];
            tab.upgradesIcon = sprites[6];
            tab.licensesIcon = sprites[7];
            EditorUtility.SetDirty(tab);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[5D.3.2] 8 statistics icons assigned. PROFILE > STATISTICS. No Save/Gold writes.");
            EditorUtility.DisplayDialog("5D.3.2", "Assigned all 8 icons. Save Scene completed. Enter Play Mode and open PROFILE > STATISTICS.", "OK");
        }
        catch (Exception e) { Debug.LogError("[5D.3.2] " + e); EditorUtility.DisplayDialog("5D.3.2", e.Message, "OK"); }
    }

    [MenuItem(Menu + "12 - AUDIT 5D.3.2 Statistics Icons (READ ONLY)")]
    public static void Audit()
    {
        MS5D3StatisticsTab tab = FindTab(SceneManager.GetActiveScene());
        if (!tab) { Debug.LogWarning("[5D.3.2] No statistics tab in open Scene."); return; }
        Sprite[] sprites = { tab.victoriesIcon, tab.victoryGoldIcon, tab.shadowTitanIcon,
            tab.graveWardenIcon, tab.mapsIcon, tab.bossBadgesIcon, tab.upgradesIcon, tab.licensesIcon };
        int count=0;
        foreach (Sprite icon in sprites) if (icon) count++;
        Debug.Log("[5D.3.2 AUDIT] icon refs=" + count + "/8; read only; no Save changes.");
    }

    [MenuItem(Menu + "13 - REMOVE 5D.3.2 Icon Links ONLY (OPEN MainMenu)")]
    public static void Remove()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene=SceneManager.GetActiveScene();
        if (scene.name != "MainMenu") return;
        MS5D3StatisticsTab tab=FindTab(scene);
        if (!tab) return;
        if (!EditorUtility.DisplayDialog("Remove 5D.3.2 icons", "Remove the 8 icon links only? 5D.3 statistics and all data remain intact. 5D.2 icons can be used as fallbacks.", "REMOVE", "CANCEL")) return;
        Undo.RecordObject(tab,"Remove Statistics Icon Links");
        tab.victoriesIcon = tab.victoryGoldIcon = tab.shadowTitanIcon = tab.graveWardenIcon = null;
        tab.mapsIcon = tab.bossBadgesIcon = tab.upgradesIcon = tab.licensesIcon = null;
        EditorUtility.SetDirty(tab);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[5D.3.2] Removed icon links only. Statistics data unchanged.");
    }

    private static MS5D3StatisticsTab FindTab(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (GameObject go in scene.GetRootGameObjects())
        { MS5D3StatisticsTab tab=go.GetComponentInChildren<MS5D3StatisticsTab>(true); if (tab) return tab; }
        return null;
    }
}
#endif
