#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MS7I4RewardsInstaller : EditorWindow
{
    private Vector2 scroll;
    private string report = "7I.4: Audit first. Installation defaults to OFF.";
    private bool queued;

    [MenuItem("Tools/MonsterSurvival/7I Endless Survival/4 - Open 7I.4 Rewards & Records")]
    public static void Open()
    {
        MS7I4RewardsInstaller w = GetWindow<MS7I4RewardsInstaller>("7I.4 Rewards");
        w.minSize = new Vector2(730f, 520f);
    }

    private void Queue(Action fn)
    {
        if (queued) return;
        queued = true;
        EditorApplication.delayCall += () =>
        {
            try { if (this != null) fn(); }
            catch (Exception ex) { report = "[ERROR] " + ex; Debug.LogException(ex); }
            finally { queued = false; if (this != null) Repaint(); }
        };
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        try
        {
            EditorGUILayout.LabelField("7I.4 - ENDLESS REWARDS & PROFILE RECORDS", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Endless defeat writes ONLY its own verified profile record. MainMenu CLAIM uses one 5A Save transaction (Gold + claim marker), never Victory/7H Quests. Requires 7I.1, 7I.2, and 7I.3. Default OFF. Backup BOTH scenes before changes.", MessageType.Info);
            if (GUILayout.Button("1 - AUDIT EXISTING SCENES / DEPENDENCIES (READ ONLY)")) Queue(Audit);
            if (GUILayout.Button("2 - BACKUP + INSTALL 7I.4 (RECORDING OFF)")) Queue(() => Modify(1));
            if (GUILayout.Button("3 - BACKUP + ENABLE ENDLESS RECORDS / REWARDS")) Queue(() => Modify(2));
            if (GUILayout.Button("4 - BACKUP + DISABLE ENDLESS RECORDS / REWARDS")) Queue(() => Modify(3));
            if (GUILayout.Button("5 - PURE REWARD / MARKER RULE TESTS")) Queue(() => { report = MS7I4RecordService.PureTests(); Debug.Log(report); });
            if (GUILayout.Button("6 - EXPORT 7I.4 QA TXT")) Queue(Export);
            if (GUILayout.Button("7 - BACKUP + REMOVE 7I.4 COMPONENTS ONLY")) Queue(() => Modify(4));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Latest report", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(report, GUILayout.MinHeight(330), GUILayout.ExpandHeight(true));
        }
        finally { EditorGUILayout.EndScrollView(); }
    }

    private static bool ScenePath(string sceneName, out string path, out string reason)
    {
        path = ""; reason = "";
        string suffix = "/" + sceneName + ".unity";
        string[] enabled = EditorBuildSettings.scenes.Where(s => s.enabled &&
            s.path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .Select(s => s.path).Distinct().ToArray();
        if (enabled.Length == 1) { path = enabled[0]; return true; }
        if (enabled.Length > 1) { reason = "Duplicate enabled " + sceneName + " scenes"; return false; }
        string[] found = AssetDatabase.FindAssets("t:Scene")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .Distinct().ToArray();
        if (found.Length == 1) { path = found[0]; return true; }
        reason = "Expected one real " + sceneName + " Scene; found " + found.Length;
        return false;
    }

    private static Scene LoadScene(string path, out bool opened)
    {
        Scene loaded = SceneManager.GetSceneByPath(path);
        opened = !loaded.IsValid() || !loaded.isLoaded;
        return opened ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive) : loaded;
    }

    private static T[] FindScene<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<T>(true)).ToArray();
    }

    private static bool Validate(Scene main, Scene gameplay,
        out MMV3MenuController menu, out WaveManager wave, out string reason)
    {
        menu = null; wave = null; reason = "BLOCKED";
        MMV3MenuController[] menus = FindScene<MMV3MenuController>(main);
        WaveManager[] waves = FindScene<WaveManager>(gameplay);
        if (menus.Length != 1 || waves.Length != 1)
        { reason = "Require one MMV3MenuController MainMenu and one WaveManager SampleScene"; return false; }
        menu = menus[0]; wave = waves[0];
        if (menu == null || !menu.mapsPage || menu.mapsStartButton == null ||
            menu.gameplaySceneName != "SampleScene" || !menu.GetComponent<MS7I2ModeMenu>())
        { reason = "MainMenu V3 / 7I.2 Mode Selection not installed correctly"; return false; }
        MS7I1EndlessDirector d = wave.GetComponent<MS7I1EndlessDirector>();
        MS7I2GameplayModeBootstrap b = wave.GetComponent<MS7I2GameplayModeBootstrap>();
        MS7I3EndlessBossRotation r = wave.GetComponent<MS7I3EndlessBossRotation>();
        GameManager manager = wave.gameManager;
        if (!manager)
        {
            GameManager[] found = FindScene<GameManager>(gameplay);
            if (found.Length == 1) manager = found[0];
        }
        if (!d || !b || !r || !d.enabled || b.endlessDirector != d ||
            !wave.enemySpawner || !manager || !manager.victoryByWaves ||
            wave.totalWaves != 5 || wave.bossWave != 5)
        { reason = "SampleScene requires valid 7I.1 / 7I.2 / 7I.3 on existing 5-wave WaveManager"; return false; }
        reason = "READY: exact MainMenu V3, 7I.2 and WaveManager 7I.1/2/3 found";
        return true;
    }

    private void Audit()
    {
        string mainPath, playPath, why;
        if (Application.isPlaying) { report = "READ-ONLY SCENE AUDIT: exit Play Mode first. Export is available during gameplay."; return; }
        if (!ScenePath("MainMenu", out mainPath, out why) || !ScenePath("SampleScene", out playPath, out why))
        { report = "[BLOCKED] " + why; return; }
        bool openedM = false, openedS = false;
        Scene m = default(Scene), s = default(Scene);
        try
        {
            m = LoadScene(mainPath, out openedM);
            s = LoadScene(playPath, out openedS);
            MMV3MenuController menu; WaveManager wave;
            bool valid = Validate(m, s, out menu, out wave, out why);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("7I.4 SCENE AUDIT / Unity=" + Application.unityVersion);
            sb.AppendLine((valid ? "[PASS] " : "[BLOCKED] ") + why);
            if (valid)
            {
                MS7I4RunRecorder rec = wave.GetComponent<MS7I4RunRecorder>();
                MS7I4RecordsMenu board = menu.GetComponent<MS7I4RecordsMenu>();
                sb.AppendLine("Record component=" + (rec ? "INSTALLED" : "MISSING") +
                    " Enabled=" + (rec && rec.enableEndlessRecords));
                sb.AppendLine("Map Select Records panel=" + (board ? "INSTALLED" : "MISSING"));
                sb.AppendLine("No reads/writes to Save during audit.");
            }
            report = sb.ToString(); Debug.Log(report);
        }
        finally
        {
            if (openedS && s.IsValid()) EditorSceneManager.CloseScene(s, true);
            if (openedM && m.IsValid()) EditorSceneManager.CloseScene(m, true);
        }
    }

    private static bool Backup(string mainPath, string playPath, out string location, out string reason)
    {
        location = ""; reason = "";
        try
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string a = Path.Combine(root, mainPath);
            string b = Path.Combine(root, playPath);
            if (!File.Exists(a) || !File.Exists(b)) { reason = "Original Scene files missing"; return false; }
            location = Path.Combine(root, "MS_Backups", "7I4", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            Directory.CreateDirectory(location);
            File.Copy(a, Path.Combine(location, "MainMenu_BEFORE_7I4.unity"));
            File.Copy(b, Path.Combine(location, "SampleScene_BEFORE_7I4.unity"));
            if (new FileInfo(a).Length < 100 || new FileInfo(b).Length < 100)
            { reason = "Scene files appear empty"; return false; }
            return true;
        }
        catch (Exception e) { reason = e.Message; return false; }
    }

    // mode 1=install OFF, 2=enable, 3=disable, 4=remove.
    private void Modify(int mode)
    {
        if (Application.isPlaying) { report = "[BLOCKED] Exit Play Mode before any installation/change."; return; }
        string mainPath, playPath, reason;
        if (!ScenePath("MainMenu", out mainPath, out reason) ||
            !ScenePath("SampleScene", out playPath, out reason))
        { report = "[BLOCKED] " + reason; return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        { report = "[CANCELLED] Save modified scenes first"; return; }
        bool openedM = false, openedS = false;
        Scene m = default(Scene), s = default(Scene);
        try
        {
            m = LoadScene(mainPath, out openedM);
            s = LoadScene(playPath, out openedS);
            MMV3MenuController menu; WaveManager wave;
            if (!Validate(m, s, out menu, out wave, out reason))
            { report = "[BLOCKED] " + reason + "; scenes untouched"; return; }
            MS7I4RunRecorder current = wave.GetComponent<MS7I4RunRecorder>();
            MS7I4RecordsMenu panel = menu.GetComponent<MS7I4RecordsMenu>();
            if ((mode == 2 || mode == 3) && (!current || !panel))
            { report = "[BLOCKED] Install button 2 first"; return; }
            string location;
            if (!Backup(mainPath, playPath, out location, out reason))
            { report = "[BLOCKED] Scene backup failed: " + reason; return; }
            if (mode == 1)
            {
                if (!current) current = Undo.AddComponent<MS7I4RunRecorder>(wave.gameObject);
                Undo.RecordObject(current, "7I4 safe recorder binding");
                current.waveManager = wave;
                current.endless = wave.GetComponent<MS7I1EndlessDirector>();
                current.modeBridge = wave.GetComponent<MS7I2GameplayModeBootstrap>();
                current.rotation = wave.GetComponent<MS7I3EndlessBossRotation>();
                current.enableEndlessRecords = false;
                EditorUtility.SetDirty(current);
                if (!panel) panel = Undo.AddComponent<MS7I4RecordsMenu>(menu.gameObject);
                Undo.RecordObject(panel, "7I4 menu binding");
                panel.menu = menu;
                EditorUtility.SetDirty(panel);
                report = "[INSTALLED OFF] Records panel on MAP SELECT; recorder stays OFF.";
            }
            else if (mode == 2 || mode == 3)
            {
                Undo.RecordObject(current, "7I4 toggle records");
                current.enableEndlessRecords = (mode == 2);
                EditorUtility.SetDirty(current);
                report = mode == 2 ? "[ENABLED] Endless records on defeat only." : "[DISABLED] No further Endless results recorded.";
            }
            else
            {
                if (current) Undo.DestroyObjectImmediate(current);
                if (panel) Undo.DestroyObjectImmediate(panel);
                report = "[REMOVED] Only the two 7I4 scene components; existing files and 5A Save untouched.";
            }
            EditorSceneManager.MarkSceneDirty(m);
            EditorSceneManager.MarkSceneDirty(s);
            bool okM = EditorSceneManager.SaveScene(m);
            bool okS = EditorSceneManager.SaveScene(s);
            if (!okM || !okS) report += " [WARNING: Scene Save may have failed!]";
            report += "\nMain=" + mainPath + "\nGameplay=" + playPath +
                "\nBackup=" + location + "\nNo WaveManager.cs, 7I1/2/3, 5A, 7H or Boss prefab modifications.";
            Debug.Log(report);
        }
        finally
        {
            if (openedS && s.IsValid()) EditorSceneManager.CloseScene(s, true);
            if (openedM && m.IsValid()) EditorSceneManager.CloseScene(m, true);
        }
    }

    private void Export()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 7I.4 / ENDLESS RECORDS & REWARDS QA");
        sb.AppendLine("Generated=" + DateTime.Now.ToString("O") + " Unity=" + Application.unityVersion +
            " Play=" + Application.isPlaying + " Scene=" + SceneManager.GetActiveScene().name);
        sb.AppendLine(MS7I4RecordService.PureTests());
        if (Application.isPlaying)
        {
            MS7I4RunRecorder recorder = UnityEngine.Object.FindFirstObjectByType<MS7I4RunRecorder>(FindObjectsInactive.Include);
            MS7I4RecordsMenu board = UnityEngine.Object.FindFirstObjectByType<MS7I4RecordsMenu>(FindObjectsInactive.Include);
            if (recorder)
            {
                sb.AppendLine("RECORDER enabled=" + recorder.enableEndlessRecords +
                    " slot=" + recorder.CapturedSlot + " bound=" + !string.IsNullOrEmpty(recorder.BoundProfileId) +
                    " captured=" + recorder.Captured + " cleared=" + recorder.LastRecordedClearedWave +
                    " reward=" + recorder.LastRecordedReward + " errors=" + recorder.Errors);
                sb.AppendLine("RECORDER STATUS=" + recorder.Status);
            }
            else sb.AppendLine("RECORDER component not in active scene (MainMenu is normal)");
            if (board) sb.AppendLine("RECORDS UI built=" + board.Built + " status=" + board.Status);
            sb.AppendLine("7I2 last mode=" + MS7I2ModeSession.LastAppliedMode +
                " fromMenu=" + MS7I2ModeSession.LastLaunchWasFromMenu);
            int selected = MS5A2GoldBank.SelectedSlot;
            MS7I4ProfileRecord record;
            int count, gold; string result;
            bool ok = MS7I4RecordService.TrySnapshot(selected, out record, out count, out gold, out result);
            sb.AppendLine("PROFILE " + selected + " valid=" + ok + " status=" + result);
            if (ok)
                sb.AppendLine("BEST REACHED=" + record.bestReachedWave + " BEST CLEARED=" +
                    record.bestClearedWave + " RUNS=" + record.gamesCompleted + " BOSSES=" +
                    record.totalEndlessBossesDefeated + " PENDING=" + count + " GOLD=" + gold);
        }
        sb.AppendLine("MANUAL PENDING: real Endless defeat record, restart/quit no ticket, MainMenu claim one-time, Gold/5A marker/revision, 7H not increment, two Profiles isolation, Normal Victory, UI, backup/rollback.");
        try
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string folder = Path.Combine(root, "MS_QAReports", "7I4");
            Directory.CreateDirectory(folder);
            string dest = Path.Combine(folder, "MS7I4_RewardsQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".txt");
            File.WriteAllText(dest, sb.ToString(), Encoding.UTF8);
            report = "[EXPORTED] " + dest + "\n" + sb;
            Debug.Log("[7I.4] QA saved: " + dest);
        }
        catch (Exception e) { report = "[ERROR] QA export: " + e.Message + "\n" + sb; }
    }
}
#endif
