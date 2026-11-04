#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Additive installer: edits only the two EXISTING scenes after creating backups.
// Never modifies MMV3MenuController, WaveManager, SaveData or any Quest source.
public sealed class MS7I2ModeSelectionInstaller : EditorWindow
{
    private Vector2 scroll;
    private string lastReport = "Run 1 - AUDIT before installing.";
    private bool taskQueued;

    [MenuItem("Tools/MonsterSurvival/7I Endless Survival/2 - Open 7I.2 Mode Selection")]
    public static void Open()
    {
        MS7I2ModeSelectionInstaller w = GetWindow<MS7I2ModeSelectionInstaller>("7I.2 Mode Selection");
        w.minSize = new Vector2(670f, 450f);
    }

    private void Queue(Action action)
    {
        if (taskQueued || action == null) return;
        taskQueued = true;
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            try { action(); }
            catch (Exception ex) { lastReport = "[ERROR] " + ex; Debug.LogException(ex); }
            finally { taskQueued = false; if (this != null) Repaint(); }
        };
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        try
        {
            EditorGUILayout.LabelField("7I.2 — NORMAL / ENDLESS MODE SELECTION", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Two compact buttons appear only on MAP SELECT, directly above START ADVENTURE. The selected mode is a one-shot memory token, consumed BEFORE 7I.1.Awake in SampleScene. Requires the real installed 7I.1 component. No Save or Main Menu V3 source changes.", MessageType.Info);
            if (GUILayout.Button("1 - AUDIT REAL MAINMENU + SAMPLESCENE (READ ONLY)")) Queue(Audit);
            if (GUILayout.Button("2 - BACKUP BOTH SCENES + INSTALL 7I.2 (NORMAL DEFAULT)")) Queue(() => Modify(true));
            if (GUILayout.Button("3 - PURE MODE / HANDOFF TESTS (NO FILE WRITES)")) Queue(() => { lastReport = MS7I2ModeSession.PureRulesQA(); Debug.Log(lastReport); });
            if (GUILayout.Button("4 - EXPORT 7I.2 QA TXT")) Queue(Export);
            if (GUILayout.Button("5 - BACKUP BOTH SCENES + REMOVE ONLY 7I.2")) Queue(() => Modify(false));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Latest report", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(lastReport, GUILayout.MinHeight(260), GUILayout.ExpandHeight(true));
        }
        finally { EditorGUILayout.EndScrollView(); }
    }

    private static bool ResolveScene(string name, out string path, out string error)
    {
        path = null; error = null;
        string suffix = "/" + name + ".unity";
        string[] enabled = EditorBuildSettings.scenes
            .Where(s => s.enabled && s.path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .Select(s => s.path).Distinct().ToArray();
        if (enabled.Length == 1) { path = enabled[0]; return true; }
        if (enabled.Length > 1) { error = "More than one enabled " + name + " Scene in Build Profiles."; return false; }
        string[] candidates = AssetDatabase.FindAssets("t:Scene")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .Distinct().ToArray();
        if (candidates.Length != 1)
        {
            error = candidates.Length == 0 ? "No " + name + ".unity Scene found." :
                "Ambiguous " + name + ".unity Scene copies. Enable the correct one in Build Profiles.";
            return false;
        }
        path = candidates[0];
        return true;
    }

    private static Scene LoadForInspection(string path, out bool opened)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        if (scene.IsValid() && scene.isLoaded) { opened = false; return scene; }
        opened = true;
        return EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
    }

    private static T[] FindInScene<T>(Scene scene) where T : Component
    {
        System.Collections.Generic.List<T> list = new System.Collections.Generic.List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            list.AddRange(root.GetComponentsInChildren<T>(true));
        return list.ToArray();
    }

    private static bool ValidateMainMenu(Scene scene, out MMV3MenuController menu, out string reason)
    {
        menu = null;
        MMV3MenuController[] all = FindInScene<MMV3MenuController>(scene);
        if (all.Length != 1) { reason = "MainMenu: expected one MMV3MenuController, found " + all.Length; return false; }
        menu = all[0];
        if (menu.mapsPage == null || menu.mapsStartButton == null || menu.homePage == null)
        { reason = "MainMenu: MMV3 references mapsPage/mapsStartButton/homePage missing"; return false; }
        if (menu.gameplaySceneName != "SampleScene")
        { reason = "MainMenu: gameplaySceneName is '" + menu.gameplaySceneName + "', expected SampleScene"; return false; }
        reason = "MainMenu V3 exact references READY (Start Adventure / MAP SELECT)";
        return true;
    }

    private static bool ValidateGameplay(Scene scene, out WaveManager manager,
        out MS7I1EndlessDirector director, out string reason)
    {
        manager = null; director = null;
        WaveManager[] all = FindInScene<WaveManager>(scene);
        if (all.Length != 1) { reason = "SampleScene: expected one WaveManager, found " + all.Length; return false; }
        manager = all[0];
        director = manager.GetComponent<MS7I1EndlessDirector>();
        if (!director) { reason = "SampleScene: 7I.1 component missing on EXISTING WaveManager"; return false; }
        if (!director.enabled) { reason = "SampleScene: 7I.1 component is disabled"; return false; }
        GameManager gm = manager.gameManager;
        if (!gm)
        {
            GameManager[] managers = FindInScene<GameManager>(scene);
            if (managers.Length == 1) gm = managers[0];
        }
        if (!manager.enemySpawner || !gm || !gm.victoryByWaves ||
            manager.totalWaves != 5 || manager.bossWave != 5)
        { reason = "SampleScene: WaveManager/EnemySpawner/GameManager/5-wave Boss references invalid"; return false; }
        if (FindInScene<MMV3GameplayBootstrap>(scene).Length != 1)
        { reason = "SampleScene: expected exactly one MMV3GameplayBootstrap"; return false; }
        reason = "SampleScene WaveManager + 7I.1 + MMV3GameplayBootstrap READY";
        return true;
    }

    private void Audit()
    {
        string mainPath, playPath, error;
        if (!ResolveScene("MainMenu", out mainPath, out error) ||
            !ResolveScene("SampleScene", out playPath, out error))
        { lastReport = "[BLOCKED] " + error; return; }
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 7I.2 / READ-ONLY AUDIT");
        sb.AppendLine("Unity=" + Application.unityVersion + " Mode=" + (Application.isPlaying ? "PLAY" : "EDIT"));
        if (Application.isPlaying)
        {
            sb.AppendLine("[INFO] Scene audit must be run in Edit Mode. Use EXPORT for live Play data.");
            lastReport = sb.ToString(); return;
        }
        bool openM = false, openS = false;
        Scene m = default(Scene), s = default(Scene);
        try
        {
            m = LoadForInspection(mainPath, out openM);
            s = LoadForInspection(playPath, out openS);
            MMV3MenuController menu; WaveManager wm; MS7I1EndlessDirector d;
            string rM, rS;
            bool okM = ValidateMainMenu(m, out menu, out rM);
            bool okS = ValidateGameplay(s, out wm, out d, out rS);
            sb.AppendLine((okM ? "[PASS] " : "[FAIL] ") + mainPath + " / " + rM);
            sb.AppendLine((okS ? "[PASS] " : "[FAIL] ") + playPath + " / " + rS);
            if (okM) sb.AppendLine("7I2 menu component=" + (menu.GetComponent<MS7I2ModeMenu>() ? "INSTALLED" : "ABSENT"));
            if (okS) sb.AppendLine("7I2 gameplay bootstrap=" + (wm.GetComponent<MS7I2GameplayModeBootstrap>() ? "INSTALLED" : "ABSENT") +
                " / 7I1 Inspector Mode=" + d.enableEndlessMode);
            sb.AppendLine(okM && okS ? "[READY] Can install 7I.2" : "[BLOCKED] Fix the actual missing reference first");
        }
        finally
        {
            if (openS && s.IsValid()) EditorSceneManager.CloseScene(s, true);
            if (openM && m.IsValid()) EditorSceneManager.CloseScene(m, true);
        }
        lastReport = sb.ToString();
        Debug.Log(lastReport);
    }

    private static bool BackupBoth(string mainPath, string playPath, out string folder, out string reason)
    {
        folder = ""; reason = "";
        try
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string sourceM = Path.Combine(projectRoot, mainPath);
            string sourceS = Path.Combine(projectRoot, playPath);
            if (!File.Exists(sourceM) || !File.Exists(sourceS))
            { reason = "Both scenes must be saved as .unity files before installation."; return false; }
            folder = Path.Combine(projectRoot, "MS_Backups", "7I2", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            Directory.CreateDirectory(folder);
            string targetM = Path.Combine(folder, "MainMenu_BEFORE_7I2.unity");
            string targetS = Path.Combine(folder, "SampleScene_BEFORE_7I2.unity");
            File.Copy(sourceM, targetM, false);
            File.Copy(sourceS, targetS, false);
            if (new FileInfo(targetM).Length < 100 || new FileInfo(targetS).Length < 100)
            { reason = "Backup files appear empty."; return false; }
            return true;
        }
        catch (Exception e) { reason = e.Message; return false; }
    }

    private void Modify(bool install)
    {
        if (Application.isPlaying) { lastReport = "[BLOCKED] Exit Play Mode before modifying Scenes."; return; }
        string mainPath, playPath, reason;
        if (!ResolveScene("MainMenu", out mainPath, out reason) ||
            !ResolveScene("SampleScene", out playPath, out reason))
        { lastReport = "[BLOCKED] " + reason; return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        { lastReport = "[CANCELLED] User chose not to save current modified scenes."; return; }
        // Validate first WITHOUT any scene mutation, before creating backups.
        bool openM = false, openS = false;
        Scene m = default(Scene), s = default(Scene);
        string backup = "";
        try
        {
            m = LoadForInspection(mainPath, out openM);
            s = LoadForInspection(playPath, out openS);
            MMV3MenuController menu; WaveManager manager; MS7I1EndlessDirector director;
            string rM, rS;
            bool menuOK = ValidateMainMenu(m, out menu, out rM);
            bool gameOK = ValidateGameplay(s, out manager, out director, out rS);
            if (!menuOK || !gameOK)
            { lastReport = "[BLOCKED] " + rM + "\n" + rS + "\nNo Scene modified."; return; }
            if (!BackupBoth(mainPath, playPath, out backup, out reason))
            { lastReport = "[BLOCKED] Backup failed: " + reason + "\nNo Scene modified."; return; }
            if (install)
            {
                MS7I2ModeMenu chooser = menu.GetComponent<MS7I2ModeMenu>();
                if (!chooser) chooser = Undo.AddComponent<MS7I2ModeMenu>(menu.gameObject);
                Undo.RecordObject(chooser, "7I2 exact MainMenu binding");
                chooser.menu = menu;
                chooser.selectedMode = MS7I2GameMode.Normal;
                EditorUtility.SetDirty(chooser);

                MS7I2GameplayModeBootstrap bridge = manager.GetComponent<MS7I2GameplayModeBootstrap>();
                if (!bridge) bridge = Undo.AddComponent<MS7I2GameplayModeBootstrap>(manager.gameObject);
                Undo.RecordObject(bridge, "7I2 exact 7I1 binding");
                bridge.endlessDirector = director;
                EditorUtility.SetDirty(bridge);

                // In Edit Mode set Normal as the safe default. Player chooses mode at each launch.
                Undo.RecordObject(director, "7I2 Normal default");
                director.enableEndlessMode = false;
                EditorUtility.SetDirty(director);
                lastReport = "[INSTALLED] NORMAL / ENDLESS selector installed on existing Menu V3 and WaveManager.";
            }
            else
            {
                MS7I2ModeMenu chooser = menu.GetComponent<MS7I2ModeMenu>();
                MS7I2GameplayModeBootstrap bridge = manager.GetComponent<MS7I2GameplayModeBootstrap>();
                if (chooser) Undo.DestroyObjectImmediate(chooser);
                if (bridge) Undo.DestroyObjectImmediate(bridge);
                lastReport = "[REMOVED] Only 7I.2 components removed; 7I.1 remains installed.";
            }
            EditorSceneManager.MarkSceneDirty(m);
            EditorSceneManager.MarkSceneDirty(s);
            bool ok1 = EditorSceneManager.SaveScene(m);
            bool ok2 = EditorSceneManager.SaveScene(s);
            if (!ok1 || !ok2)
                lastReport = "[WARNING] Scene Save did not report success for both scenes; use backups! ";
            lastReport += "\nMainMenu=" + mainPath + "\nSampleScene=" + playPath +
                "\nBackup=" + backup + "\nNo PlayerPrefs, SaveData, Quest, WaveManager source or Prefab edits.";
            Debug.Log(lastReport);
        }
        finally
        {
            if (openS && s.IsValid()) EditorSceneManager.CloseScene(s, true);
            if (openM && m.IsValid()) EditorSceneManager.CloseScene(m, true);
        }
    }

    private void Export()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 7I.2 - MODE SELECTION QA");
        sb.AppendLine("Time=" + DateTime.Now.ToString("O") + " Unity=" + Application.unityVersion +
            " Mode=" + (Application.isPlaying ? "PLAY" : "EDIT") + " Scene=" + SceneManager.GetActiveScene().name);
        sb.AppendLine("SessionPending=" + MS7I2ModeSession.HasPending +
            " PendingMode=" + MS7I2ModeSession.PendingMode +
            " LastApplied=" + MS7I2ModeSession.LastAppliedMode +
            " FromMainMenu=" + MS7I2ModeSession.LastLaunchWasFromMenu +
            " StartCommits=" + MS7I2ModeSession.CommittedStarts +
            " Consumes=" + MS7I2ModeSession.ConsumedStarts);
        if (Application.isPlaying && SceneManager.GetActiveScene().name == "MainMenu")
        {
            MS7I2ModeMenu[] menus = UnityEngine.Object.FindObjectsByType<MS7I2ModeMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (MS7I2ModeMenu item in menus)
                sb.AppendLine("MENU selected=" + item.selectedMode + " built=" + item.UiBuilt + " status=" + item.Status);
        }
        if (Application.isPlaying && SceneManager.GetActiveScene().name == "SampleScene")
        {
            MS7I2GameplayModeBootstrap[] bridges = UnityEngine.Object.FindObjectsByType<MS7I2GameplayModeBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (MS7I2GameplayModeBootstrap bridge in bridges)
            {
                sb.AppendLine("GAMEPLAY mode=" + bridge.appliedMode + " fromMenu=" + bridge.cameFromMainMenu +
                    " applied=" + bridge.applied + " status=" + bridge.status);
                MS7I1EndlessDirector d = bridge.endlessDirector;
                if (d) sb.AppendLine("7I1 InspectorFlag=" + d.enableEndlessMode + " RuntimeEnabled=" + d.RuntimeEnabled +
                    " BossGate=" + d.BossGateTakenOver + " Started=" + d.EndlessStarted +
                    " CurrentEndlessWave=" + d.CurrentEndlessWave + " Peak=" + d.PeakEndlessWave +
                    " ErrorCount=" + d.ErrorCount + " LastStatus=" + d.LastStatus);
            }
        }
        sb.AppendLine(MS7I2ModeSession.PureRulesQA());
        sb.AppendLine("MANUAL PENDING: NORMAL Wave5 Victory, ENDLESS Wave6+, real map/profile mapping, Pause/GameOver, save/Quest isolation, buttons at 1920x1080 / 1280x720 / 1024x768.");
        string folder = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "MS_QAReports", "7I2");
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, "MS7I2_ModeQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".txt");
        File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
        lastReport = "[EXPORTED] " + file + "\n\n" + sb;
        Debug.Log("[7I.2] QA saved: " + file);
    }
}
#endif
