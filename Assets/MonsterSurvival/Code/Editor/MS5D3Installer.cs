#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Install one read-only UI component in MainMenu, one event tracker in SampleScene.
// No edits to Save5A1, Save5A3, Unlock5C3, Profile5D1/5D2 or Boss AI.
public static class MS5D3Installer
{
    private const string Menu = "Tools/MonsterSurvival/5D Profile Overview/";
    private const string MainMenuPath = "Assets/MonsterSurvival/Content/Scenes/MainMenu.unity";
    private const string GameplayPath = "Assets/MonsterSurvival/Content/Scenes/SampleScene.unity";
    private const string BackupRoot = "Assets/MonsterSurvival/Features/Progression/Profile5D3/Backups";

    [MenuItem(Menu + "7 - INSTALL 5D.3 Profile Statistics (BOTH scenes)")]
    public static void Install()
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog("5D.3", "Stop Play Mode first.", "OK"); return; }
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuPath) ||
            !AssetDatabase.LoadAssetAtPath<SceneAsset>(GameplayPath))
        { EditorUtility.DisplayDialog("5D.3", "Expected Assets/MonsterSurvival/Content/Scenes/MainMenu.unity and SampleScene.unity. No changes made.", "OK"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!EditorUtility.DisplayDialog("Install 5D.3 Statistics",
            "Install a read-only STATISTICS tab in Profile 5D.2 and a gameplay event tracker?\n\n" +
            "The tracker stores statistics in SEPARATE, verified files per Profile. " +
            "It never alters 5A Save data, Gold, Inventory, Equipment or Boss AI.\n\n" +
            "Both Scenes will be backed up before any change.", "INSTALL", "CANCEL")) return;
        string original = SceneManager.GetActiveScene().path;
        if (string.IsNullOrEmpty(original)) original = MainMenuPath;
        try
        {
            Scene main = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
            MS5D2ProfilePolish polish = FindInScene<MS5D2ProfilePolish>(main);
            if (!polish || !polish.GetComponent<MS5D1ProfileOverview>())
            {
                EditorUtility.DisplayDialog("5D.3 prerequisites", "Install 5D.1 and 5D.2 in MainMenu first. Nothing changed.", "OK");
                return;
            }
            Scene game = EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Single);
            MS5A3PersistentRun save = FindInScene<MS5A3PersistentRun>(game);
            WaveManager wave = FindInScene<WaveManager>(game);
            PlayerProgression progress = FindInScene<PlayerProgression>(game);
            if (!save || !wave || !progress)
            {
                EditorUtility.DisplayDialog("5D.3 prerequisites", "Gameplay requires MS5A3PersistentRun, WaveManager and PlayerProgression. Nothing changed.", "OK");
                return;
            }
            EnsureFolder(BackupRoot);
            BackupScene(MainMenuPath, BackupRoot + "/MainMenu_BEFORE_5D3.unity");
            BackupScene(GameplayPath, BackupRoot + "/SampleScene_BEFORE_5D3.unity");
            // Backups completed. Install the two components only if absent.
            main = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
            polish = FindInScene<MS5D2ProfilePolish>(main);
            MS5D3StatisticsTab tab = polish.GetComponent<MS5D3StatisticsTab>();
            if (!tab)
            {
                tab = Undo.AddComponent<MS5D3StatisticsTab>(polish.gameObject);
                tab.profilePolish = polish;
                EditorUtility.SetDirty(tab);
                EditorSceneManager.MarkSceneDirty(main);
                EditorSceneManager.SaveScene(main);
            }
            game = EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Single);
            save = FindInScene<MS5A3PersistentRun>(game);
            wave = FindInScene<WaveManager>(game);
            progress = FindInScene<PlayerProgression>(game);
            MS5D3RunTracker tracker = FindInScene<MS5D3RunTracker>(game);
            if (!tracker)
            {
                tracker = Undo.AddComponent<MS5D3RunTracker>(save.gameObject);
                tracker.persistentRun = save;
                tracker.waveManager = wave;
                tracker.progression = progress;
                EditorUtility.SetDirty(tracker);
                EditorSceneManager.MarkSceneDirty(game);
                EditorSceneManager.SaveScene(game);
            }
            Debug.Log("[5D.3] INSTALLED. Two Scene components, no Save modifications. Statistics only start accumulating AFTER installation.");
            EditorUtility.DisplayDialog("5D.3 installed",
                "MainMenu: PROFILE > STATISTICS tab.\nGameplay: MS5D3RunTracker.\n\n" +
                "Gold reflects only successful 5A.3 Victory commits. Previous runs cannot be reconstructed.\n" +
                "Audit before playing; check Console for errors.", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogError("[5D.3] INSTALL FAILED: " + ex);
            EditorUtility.DisplayDialog("5D.3 install failed", ex.Message + "\nReview Scene backups before retrying.", "OK");
        }
        finally
        {
            try { if (AssetDatabase.LoadAssetAtPath<SceneAsset>(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single); }
            catch (Exception ex) { Debug.LogWarning("[5D.3] Could not restore open Scene: " + ex.Message); }
        }
    }

    [MenuItem(Menu + "8 - AUDIT 5D.3 Statistics (READ ONLY)")]
    public static void Audit()
    {
        Scene scene = SceneManager.GetActiveScene();
        int pass = 0, warn = 0;
        if (scene.name == "MainMenu")
        {
            MS5D3StatisticsTab tab = FindInScene<MS5D3StatisticsTab>(scene);
            Check(tab && tab.GetComponent<MS5D2ProfilePolish>(), "5D.3 tab attached to 5D.2 Profile", ref pass, ref warn);
        }
        else if (scene.name == "SampleScene")
        {
            MS5D3RunTracker tracker = FindInScene<MS5D3RunTracker>(scene);
            Check(tracker && tracker.persistentRun && tracker.waveManager && tracker.progression,
                "5D.3 tracker and 5A.3/Wave/Player refs", ref pass, ref warn);
        }
        else Debug.LogWarning("[5D.3 AUDIT] Open MainMenu or SampleScene for Scene check.");
        for (int slot = 1; slot <= 3; slot++)
        {
            MS5D3StatsData stats;
            bool backup;
            string info;
            bool has = MS5D3StatsStore.TryRead(slot, out stats, out info, out backup);
            if (has)
                Debug.Log("[5D.3 AUDIT] Profile " + slot + (backup ? " BACKUP ONLY" : " VERIFIED") +
                    " | wins=" + stats.victories + " | victory Gold=" + stats.totalVictoryGold +
                    " | Titan=" + stats.shadowTitanKills + " | Warden=" + stats.graveWardenKills +
                    " | rev=" + stats.revision);
            else if (info.StartsWith("NO_STATS", StringComparison.Ordinal))
                Debug.Log("[5D.3 AUDIT] Profile " + slot + " — no statistics yet (history is NOT backfilled).");
            else { warn++; Debug.LogWarning("[5D.3 AUDIT] Profile " + slot + " INVALID: " + info); }
        }
        Debug.Log("[5D.3 AUDIT] " + pass + " PASS / " + warn + " WARN | READ ONLY | no data was changed.");
    }

    [MenuItem(Menu + "9 - RUN 5D.3 ISOLATED QA (TEMP FOLDER ONLY)")]
    public static void Qa()
    {
        string report = MS5D3StatsStore.RunIsolatedQa();
        Debug.Log("[5D.3 ISOLATED QA] " + report);
        EditorUtility.DisplayDialog("5D.3 isolated QA", report, "OK");
    }

    [MenuItem(Menu + "10 - REMOVE 5D.3 COMPONENTS ONLY (BOTH scenes)")]
    public static void Remove()
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog("5D.3", "Stop Play Mode first.", "OK"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!EditorUtility.DisplayDialog("Remove 5D.3 components",
            "Remove only the 5D.3 UI tab and tracker from two Scenes?\n\n" +
            "The separate statistics files and all other systems will be left intact.", "REMOVE", "CANCEL")) return;
        string original = SceneManager.GetActiveScene().path;
        try
        {
            RemoveFrom<MS5D3StatisticsTab>(MainMenuPath);
            RemoveFrom<MS5D3RunTracker>(GameplayPath);
            Debug.Log("[5D.3] Removed Scene components only. All profile statistics data remains on disk.");
        }
        catch (Exception e) { Debug.LogError("[5D.3] REMOVE FAILED: " + e); }
        finally
        {
            if (!string.IsNullOrEmpty(original) && AssetDatabase.LoadAssetAtPath<SceneAsset>(original))
                EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
        }
    }

    private static void RemoveFrom<T>(string path) where T : Component
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        T component = FindInScene<T>(scene);
        if (!component) return;
        Undo.DestroyObjectImmediate(component);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void Check(bool ok, string what, ref int pass, ref int warn)
    {
        if (ok) { pass++; Debug.Log("[5D.3 AUDIT] PASS " + what); }
        else { warn++; Debug.LogWarning("[5D.3 AUDIT] WARN " + what); }
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found) return found;
        }
        return null;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    private static void BackupScene(string source, string destination)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(destination)) return;
        if (!AssetDatabase.CopyAsset(source, destination)) throw new IOException("Backup failed: " + source);
        AssetDatabase.ImportAsset(destination);
    }
}
#endif
