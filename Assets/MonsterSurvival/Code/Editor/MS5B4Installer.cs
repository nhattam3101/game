#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 5B.4 add-on. Never rebuild 5B.2, never touches Gold, profiles, or gameplay scenes.
public static class MS5B4Installer
{
    private const string Menu = "Tools/MonsterSurvival/5B Permanent Upgrades/";
    private const string BackupDir = "Assets/MonsterSurvival/Features/Progression/Upgrade5B4/Backups";
    private const string AudioDir = "Assets/MonsterSurvival/Features/Progression/Upgrade5B4/Audio/";

    [MenuItem(Menu + "10 - INSTALL 5B.4 Purchase VFX + Audio (MainMenu)")]
    public static void Install()
    {
        if (Application.isPlaying) { Debug.LogWarning("[5B.4] Stop Play Mode first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string path = ScenePath();
        if (string.IsNullOrEmpty(path))
        { EditorUtility.DisplayDialog("5B.4", "MainMenu.unity not found.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("5B.4 — Upgrade Purchase Effects",
            "Add success glow + original UI sounds to the EXISTING 5B.2 shop?\n\n" +
            "Makes no changes to Save data, purchase logic, gameplay, or item stats.", "INSTALL", "CANCEL")) return;
        try
        {
            AudioClip yes = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "UpgradeSuccess_Original.wav");
            AudioClip no = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "UpgradeBlocked_Original.wav");
            if (!yes || !no) throw new InvalidOperationException("Missing bundled WAV files. Reimport all 5B.4 Assets.");

            Scene s = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            GameObject canvas = Find(s, "MMV3_Canvas");
            GameObject overlay = Find(s, "MS5B2_UpgradeShop");
            if (!canvas || !overlay || !overlay.transform.IsChildOf(canvas.transform) ||
                !overlay.GetComponent<MS5B2ShopUI>())
                throw new InvalidOperationException("Existing Main Menu V3 / 5B.2 Shop not found. Install 5B.2 first.");
            if (overlay.GetComponent<MS5B4PurchaseFeedback>())
            {
                Debug.Log("[5B.4] Already installed; no duplicate components.");
                EditorUtility.DisplayDialog("5B.4", "Already installed. No changes made.", "OK");
                return;
            }
            EnsureFolder(BackupDir);
            string backup = BackupDir + "/MainMenu_BEFORE_5B4.unity";
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(backup))
            {
                if (!AssetDatabase.CopyAsset(path, backup)) throw new IOException("Failed to back up MainMenu.");
                AssetDatabase.ImportAsset(backup);
            }
            MS5B4PurchaseFeedback effect = overlay.AddComponent<MS5B4PurchaseFeedback>();
            effect.successClip = yes;
            effect.blockedClip = no;
            effect.localSfxVolume = .55f;
            effect.followExistingSfxVolume = true;
            EditorUtility.SetDirty(effect);
            EditorSceneManager.MarkSceneDirty(s);
            EditorSceneManager.SaveScene(s);
            Debug.Log("[5B.4] Installed on 5B.2 Shop only. Save/GameManager unchanged.");
            EditorUtility.DisplayDialog("5B.4 INSTALLED", "Open MainMenu, Play Mode, UPGRADES.\n"+
                "Buy an affordable upgrade to see glow + hear success.\n"+
                "Preview menu can demo the VFX without spending Gold.","OK");
        }
        catch (Exception ex)
        {
            Debug.LogError("[5B.4] Installation failed: " + ex);
            EditorUtility.DisplayDialog("5B.4 ERROR", ex.Message,"OK");
        }
    }

    [MenuItem(Menu + "11 - AUDIT 5B.4 (READ ONLY)")]
    public static void Audit()
    {
        Scene s = SceneManager.GetActiveScene();
        GameObject overlay = Find(s,"MS5B2_UpgradeShop");
        MS5B4PurchaseFeedback fx = overlay ? overlay.GetComponent<MS5B4PurchaseFeedback>() : null;
        Debug.Log("[5B.4 AUDIT] Scene=" + s.name +
            " Shop=" + (overlay && overlay.GetComponent<MS5B2ShopUI>() ? "PASS" : "MISSING") +
            " Effect=" + (fx ? "PASS" : "MISSING") +
            " SuccessWAV=" + (fx && fx.successClip ? "PASS" : "MISSING") +
            " BlockedWAV=" + (fx && fx.blockedClip ? "PASS" : "MISSING") +
            " SFXMode=" + (fx && fx.followExistingSfxVolume ? "AudioManager" : "Manual") +
            " (read only; no saves touched)");
    }

    [MenuItem(Menu + "12 - PREVIEW 5B.4 VFX + SFX (PLAY MODE ONLY)")]
    public static void Preview()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[5B.4] Preview requires Play Mode."); return; }
        Scene scene = SceneManager.GetActiveScene();
        GameObject overlay = Find(scene,"MS5B2_UpgradeShop");
        MS5B4PurchaseFeedback fx = overlay ? overlay.GetComponent<MS5B4PurchaseFeedback>() : null;
        if (!fx) { Debug.LogWarning("[5B.4] 5B.4 component missing."); return; }
        fx.PreviewSuccess(0);
        Debug.Log("[5B.4] Preview requested: NO purchase, Gold deduction or save write.");
    }

    [MenuItem(Menu + "13 - REMOVE 5B.4 Effects ONLY (MainMenu)")]
    public static void Remove()
    {
        if (Application.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string path = ScenePath();
        if (string.IsNullOrEmpty(path)) return;
        if (!EditorUtility.DisplayDialog("REMOVE 5B.4",
            "Remove ONLY the 5B.4 presentation component from MainMenu?\n" +
            "5B.1/5B.2/5B.3, purchased upgrades and profile saves stay intact.","REMOVE","CANCEL"))return;
        Scene scene = EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
        GameObject overlay = Find(scene,"MS5B2_UpgradeShop");
        MS5B4PurchaseFeedback fx = overlay ? overlay.GetComponent<MS5B4PurchaseFeedback>() : null;
        if (fx) UnityEngine.Object.DestroyImmediate(fx);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[5B.4] Effects removed; Gold/Shop data not modified.");
    }

    private static string ScenePath()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Scene MainMenu"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path).Equals("MainMenu",StringComparison.OrdinalIgnoreCase)
                && !path.Contains("/Backups/"))return path;
        }
        return null;
    }
    private static GameObject Find(Scene scene,string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        }
        return null;
    }
    private static void EnsureFolder(string path)
    {
        string[] parts=path.Split('/');string current=parts[0];
        for (int i=1;i<parts.Length;i++)
        {
            string next=current+"/"+parts[i];
            if (!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);
            current=next;
        }
    }
}
#endif
