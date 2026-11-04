#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 7F.4.1 additive installer. Does not replace ANY existing script or edit MainMenu/Save.
public sealed class MS7F41AutoMapIdInstaller : EditorWindow
{
    private Vector2 scroll;
    private string lastReport = "Choose AUDIT to inspect existing maps and 7F.4 profiles.";

    [MenuItem("Tools/MonsterSurvival/7F Arena Events/16 - Open 7F.4.1 Auto Map ID")]
    public static void Open()
    {
        MS7F41AutoMapIdInstaller window = GetWindow<MS7F41AutoMapIdInstaller>(false, "7F.4.1 Map ID");
        window.minSize = new Vector2(610, 470);
        window.Show();
    }

    // IMGUI safety: never run modal dialogs, Scene saves or file exports while
    // a GUILayout/ScrollView pass is still being drawn. Unity 6.6 can otherwise
    // report "EndLayoutGroup: BeginLayoutGroup must be called first".
    private bool actionPending;

    private void QueueEditorAction(Action action)
    {
        if (actionPending || action == null) return;
        actionPending = true;
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            try
            {
                action();
            }
            catch (Exception ex)
            {
                lastReport = "[7F.4.1 EDITOR ACTION ERROR] " + ex;
                Debug.LogException(ex);
            }
            finally
            {
                actionPending = false;
                if (this != null) Repaint();
            }
        };
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        try
        {
            GUILayout.Label("7F.4.1 - AUTOMATIC MAP ID BINDING", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Reads ACTUAL WaveMapSwitcher.ActiveMapIndex + maps[index].mapName, as used by Menu V3.\n" +
                "Automatically creates missing real-map profiles, preserving existing 7F.4 profiles and wave settings.\n" +
                "Does not edit WaveManager, MapSelection, Menu V3, Save or hazard damage code.", MessageType.Info);

            if (GUILayout.Button("1 - AUDIT REAL MAP SOURCE (READ ONLY)", GUILayout.Height(32)))
                QueueEditorAction(() => { lastReport = Audit(); Debug.Log(lastReport); });
            if (GUILayout.Button("2 - BACKUP + INSTALL AUTO ID + REAL PROFILES", GUILayout.Height(33)))
                QueueEditorAction(Install);
            if (GUILayout.Button("3 - RESTORE PREVIOUS MANUAL OVERRIDE / DISABLE AUTO", GUILayout.Height(31)))
                QueueEditorAction(DisableAuto);
            if (GUILayout.Button("4 - EXPORT 7F.4.1 QA TXT", GUILayout.Height(31)))
                QueueEditorAction(Export);

            EditorGUILayout.HelpBox(
                "Test from MainMenu -> MAP SELECT -> START. In Play Mode, audit/export while the map is visible.\n" +
                "7F.4 must also have Enable Map Balancing ON for the profile to alter arena timing/damage.\n" +
                "7F.2 boss exclusion and 7F.4 safety caps are not changed. No Script/Prefab overwrite.", MessageType.Warning);
            GUILayout.Space(6);
            GUILayout.Label("Latest report", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(lastReport, GUILayout.MinHeight(250));
        }
        finally
        {
            EditorGUILayout.EndScrollView();
        }
    }

    private static WaveManager FindWave(out string error)
    {
        WaveManager found = null;
        int count = 0;
        Scene active = SceneManager.GetActiveScene();
        foreach (WaveManager wave in UnityEngine.Object.FindObjectsByType<WaveManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (wave == null || wave.gameObject.scene != active) continue;
            count++;
            found = wave;
        }
        error = count == 1 ? "One existing WaveManager" : "Expected one WaveManager in active Scene; found " + count;
        return count == 1 ? found : null;
    }

    private static bool TrySource(out WaveManager wave, out WaveMapSwitcher switcher,
        out MS7F4MapBalanceDirector balance, out string error)
    {
        wave = FindWave(out error);
        switcher = wave != null ? wave.GetComponent<WaveMapSwitcher>() : null;
        balance = wave != null ? wave.GetComponent<MS7F4MapBalanceDirector>() : null;
        if (wave == null) return false;
        if (switcher == null) { error = "WaveMapSwitcher missing on existing WaveManager"; return false; }
        if (balance == null) { error = "7F.4 Map Balance Director missing: install 7F.4 first"; return false; }
        if (switcher.maps == null || switcher.maps.Count < 1) { error = "WaveMapSwitcher map list is empty"; return false; }
        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < switcher.maps.Count; i++)
        {
            if (!MS7F41AutoMapIdBinder.IsIndexValid(switcher, i))
            {
                error = "Map entry " + i + " must have a non-empty mapName and mapRoot";
                return false;
            }
            string id = MS7F41AutoMapIdBinder.GetIdForIndex(switcher, i);
            if (!ids.Add(id)) { error = "Duplicate mapName: " + id; return false; }
        }
        error = "REAL WaveMapSwitcher maps verified (" + switcher.maps.Count + ")";
        return true;
    }

    private static MS7F4MapBalanceDirector.MapProfile FindProfile(MS7F4MapBalanceDirector balance, string id)
    {
        if (balance.mapProfiles == null) return null;
        foreach (MS7F4MapBalanceDirector.MapProfile p in balance.mapProfiles)
            if (p != null && MS7F4MapBalanceDirector.SameMap(p.mapId, id)) return p;
        return null;
    }

    // Deep-copy template values: never replace or mutate an existing user profile.
    private static MS7F4MapBalanceDirector.MapProfile CopyFrom(
        MS7F4MapBalanceDirector.MapProfile src, string newId)
    {
        var target = new MS7F4MapBalanceDirector.MapProfile
        {
            mapId = newId,
            enabled = true,
            waves = new List<MS7F4MapBalanceDirector.WaveRule>()
        };
        if (src == null || src.waves == null) return target;
        foreach (MS7F4MapBalanceDirector.WaveRule r in src.waves)
        {
            if (r == null) continue;
            target.waves.Add(new MS7F4MapBalanceDirector.WaveRule
            {
                wave = r.wave,
                damageScale = r.damageScale,
                warningScale = r.warningScale,
                shapeScale = r.shapeScale,
                firstDelayScale = r.firstDelayScale,
                intervalScale = r.intervalScale,
                maxEventsPerWave = r.maxEventsPerWave
            });
        }
        return target;
    }

    // Defaults are taken from 7F.4's existing demonstration profiles by difficulty:
    // map slot 0=Meadow/easier, 1=Desert/balanced, 2=Ruins/harder.
    // If templates were deleted, use simple safe presets; users may refine later.
    private static MS7F4MapBalanceDirector.MapProfile CreateProfile(
        MS7F4MapBalanceDirector balance, string actualId, int mapIndex)
    {
        string templateId = mapIndex == 0 ? "Map_Forest_DEMO" :
                            mapIndex == 2 ? "Map_Ruins_DEMO" : "SampleScene";
        MS7F4MapBalanceDirector.MapProfile template = FindProfile(balance, templateId);
        MS7F4MapBalanceDirector.MapProfile target = CopyFrom(template, actualId);
        if (target.waves.Count > 0) return target;
        for (int wave = 2; wave <= 4; wave++)
        {
            var rule = new MS7F4MapBalanceDirector.WaveRule { wave = wave, maxEventsPerWave = 1 };
            if (mapIndex == 0) { rule.damageScale = 0.8f; rule.warningScale = 1.25f; rule.shapeScale = 0.9f; }
            else if (mapIndex >= 2) { rule.damageScale = 1.1f; rule.warningScale = 1.05f; rule.shapeScale = 1.05f; }
            target.waves.Add(rule);
        }
        return target;
    }

    private static bool BackupScene(Scene scene, out string location)
    {
        location = "";
        if (string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog("7F.4.1", "Please save the Scene as a .unity file first.", "OK");
            return false;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        if (!EditorSceneManager.SaveScene(scene)) return false;
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string source = Path.GetFullPath(Path.Combine(projectRoot, scene.path));
        string folder = Path.Combine(projectRoot, "MS_Backups", "7F41");
        Directory.CreateDirectory(folder);
        string baseName = Path.GetFileNameWithoutExtension(scene.path);
        location = Path.Combine(folder, baseName + "_BEFORE_7F41_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity");
        try { File.Copy(source, location, false); return true; }
        catch (Exception ex)
        {
            Debug.LogError("[7F.4.1] Cannot back up Scene: " + ex.Message);
            EditorUtility.DisplayDialog("7F.4.1 Backup failed", ex.Message, "OK");
            return false;
        }
    }

    private void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorUtility.DisplayDialog("7F.4.1", "Stop Play Mode to install. The runtime audit works in Play Mode.", "OK"); return; }
        WaveManager wave;
        WaveMapSwitcher switcher;
        MS7F4MapBalanceDirector balance;
        string issue;
        if (!TrySource(out wave, out switcher, out balance, out issue))
        { EditorUtility.DisplayDialog("7F.4.1 Missing source", issue, "OK"); return; }
        if (!EditorUtility.DisplayDialog("7F.4.1 - Auto ID", "Back up the current Scene, attach auto Map ID reader to existing WaveManager, and ADD any missing map profiles from real WaveMapSwitcher names?\n\nExisting map profiles, gameplay scripts, Save/Profile and Boss will stay unchanged.", "Install", "Cancel")) return;
        string backup;
        if (!BackupScene(wave.gameObject.scene, out backup)) return;

        MS7F41AutoMapIdBinder binder = wave.GetComponent<MS7F41AutoMapIdBinder>();
        bool newBinder = binder == null;
        if (newBinder) binder = Undo.AddComponent<MS7F41AutoMapIdBinder>(wave.gameObject);
        Undo.RecordObject(binder, "7F.4.1 Auto Map ID");
        Undo.RecordObject(balance, "7F.4.1 Add real Map Profiles");
        if (newBinder) binder.previousOverrideBeforeInstall = balance.mapIdOverride;
        binder.mapSwitcher = switcher;
        binder.mapBalance = balance;
        binder.enableAutoBinding = true;
        balance.mapIdOverride = "";  // scene fallback until a REAL map root becomes active.
        if (balance.mapProfiles == null) balance.mapProfiles = new List<MS7F4MapBalanceDirector.MapProfile>();
        int added = 0;
        for (int i = 0; i < switcher.maps.Count; i++)
        {
            string id = MS7F41AutoMapIdBinder.GetIdForIndex(switcher, i);
            if (FindProfile(balance, id) != null) continue; // Preserve any customized profile.
            balance.mapProfiles.Add(CreateProfile(balance, id, i));
            added++;
        }
        EditorUtility.SetDirty(binder);
        EditorUtility.SetDirty(balance);
        EditorSceneManager.MarkSceneDirty(wave.gameObject.scene);
        EditorSceneManager.SaveScene(wave.gameObject.scene);
        Selection.activeObject = wave.gameObject;
        lastReport = "[INSTALL] Auto Map ID ENABLED; new real profiles=" + added +
            "; previous profiles untouched.\nBackup=" + backup + "\n" + Audit();
        Debug.Log(lastReport);
        EditorUtility.DisplayDialog("7F.4.1 Installed", "Auto Map ID active. New real profiles: " + added + ".\nTest MainMenu -> select map -> START; run Audit in Play Mode.", "OK");
    }

    private void DisableAuto()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorUtility.DisplayDialog("7F.4.1", "Stop Play Mode before restoring Scene settings.", "OK"); return; }
        WaveManager wave;
        WaveMapSwitcher switcher;
        MS7F4MapBalanceDirector balance;
        string issue;
        if (!TrySource(out wave, out switcher, out balance, out issue))
        { EditorUtility.DisplayDialog("7F.4.1", issue, "OK"); return; }
        MS7F41AutoMapIdBinder binder = wave.GetComponent<MS7F41AutoMapIdBinder>();
        if (binder == null) { EditorUtility.DisplayDialog("7F.4.1", "No 7F.4.1 Binder was installed.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("7F.4.1", "Disable auto Map ID and restore the Map ID Override that was present before first installation?\nExisting map profiles remain.", "Restore", "Cancel")) return;
        string backup;
        if (!BackupScene(wave.gameObject.scene, out backup)) return;
        Undo.RecordObject(binder, "7F.4.1 Disable Auto ID");
        Undo.RecordObject(balance, "7F.4.1 Restore Map Override");
        binder.enableAutoBinding = false;
        balance.mapIdOverride = binder.previousOverrideBeforeInstall ?? "";
        EditorUtility.SetDirty(binder);
        EditorUtility.SetDirty(balance);
        EditorSceneManager.MarkSceneDirty(wave.gameObject.scene);
        EditorSceneManager.SaveScene(wave.gameObject.scene);
        lastReport = "[RESTORED] Auto disabled, former override='" + balance.mapIdOverride + "'.\n" + Audit();
        Debug.Log(lastReport);
    }

    private static string Audit()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 7F.4.1 — AUTO MAP ID AUDIT (READ ONLY)");
        sb.AppendLine("Unity=" + Application.unityVersion + " Scene=" + SceneManager.GetActiveScene().name + " PlayMode=" + EditorApplication.isPlaying);
        WaveManager wave;
        WaveMapSwitcher switcher;
        MS7F4MapBalanceDirector balance;
        string issue;
        if (!TrySource(out wave, out switcher, out balance, out issue))
        { sb.AppendLine("[ERROR] " + issue); return sb.ToString(); }
        sb.AppendLine("[PASS] " + issue);
        sb.AppendLine("[INFO] Menu V3: MMV3GameplayBootstrap -> WaveMapSwitcher.SelectMapForMatch(index) -> mapName");
        MS7F41AutoMapIdBinder binder = wave.GetComponent<MS7F41AutoMapIdBinder>();
        sb.AppendLine(binder != null ? "[PASS] One auto binding component on existing WaveManager" : "[PENDING] Install 7F.4.1 Binder");
        if (binder != null)
            sb.AppendLine(binder.mapSwitcher == switcher && binder.mapBalance == balance ?
                "[PASS] Exact existing references" : "[ERROR] Binder references mismatch");
        for (int i = 0; i < switcher.maps.Count; i++)
        {
            string id = MS7F41AutoMapIdBinder.GetIdForIndex(switcher, i);
            var profile = FindProfile(balance, id);
            sb.AppendLine("[MAP] index=" + i + " id='" + id + "' root='" + switcher.maps[i].mapRoot.name +
                "' profile=" + (profile == null ? "MISSING" : profile.enabled && profile.waves != null && profile.waves.Count > 0 ? "READY" : "DISABLED/EMPTY"));
        }
        sb.AppendLine("[INFO] ActiveIndex=" + switcher.ActiveMapIndex + " SelectedIndex=" + switcher.SelectedMapIndex +
            " UseSelectedMapForAllWaves=" + switcher.useSelectedMapForAllWaves);
        sb.AppendLine("[INFO] 7F.4 EnableMapBalancing=" + balance.enableMapBalancing +
            " EffectiveMapId='" + balance.ActiveMapId + "' CurrentOverride='" + balance.mapIdOverride + "'");
        if (!EditorApplication.isPlaying) sb.AppendLine("[PENDING] Run MainMenu -> Map Select -> START and export in Play Mode to confirm actual ID.");
        else if (binder != null) sb.AppendLine(binder.GetReport());
        sb.AppendLine("[INFO] Existing 7F.2 event types, Boss exclusion, and 7F.4 caps are unchanged.");
        return sb.ToString();
    }

    private void Export()
    {
        string output = Audit();
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string folder = Path.Combine(root, "MS_QAReports", "7F41");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "MS7F41_AutoMapIdQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
        File.WriteAllText(path, output, new UTF8Encoding(false));
        lastReport = output;
        Debug.Log("[7F.4.1] Exported: " + path);
        EditorUtility.RevealInFinder(path);
    }
}
#endif
