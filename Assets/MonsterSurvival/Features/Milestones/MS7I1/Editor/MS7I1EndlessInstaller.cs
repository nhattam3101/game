#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MS7I1EndlessInstaller : EditorWindow
{
    private string report = "Open SampleScene, then run AUDIT.";
    private Vector2 scroll;
    private bool busy;
    [MenuItem("Tools/MonsterSurvival/7I Endless Survival/1 - Open 7I.1 Endless Core")]
    public static void Open() { GetWindow<MS7I1EndlessInstaller>("7I.1 Endless Core").minSize = new Vector2(620, 410); }

    private void Queue(Action task)
    {
        if (busy) return;
        busy = true;
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            try { task(); } catch (Exception ex) { report = "[ERROR] " + ex; Debug.LogException(ex); }
            finally { busy = false; if (this != null) Repaint(); }
        };
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        try
        {
            EditorGUILayout.LabelField("7I.1 - ENDLESS SURVIVAL CORE", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Stage 7I.1 prototype: normal 5 waves unaffected when OFF. In Endless, defeat Wave-5 Boss, then continue to Wave 6+. Inspector option is OFF after installation. No persistent Endless rewards or MainMenu mode selector yet.", MessageType.Info);
            if (GUILayout.Button("1 - AUDIT ORIGINAL WAVEMANAGER / SCENE")) Queue(() => { report = Audit(); Debug.Log(report); });
            if (GUILayout.Button("2 - BACKUP + INSTALL ADDITIVE CORE (OFF)")) Queue(Install);
            if (GUILayout.Button("3 - BACKUP + ENABLE ENDLESS FOR TEST")) Queue(() => SetMode(true));
            if (GUILayout.Button("4 - BACKUP + RETURN TO NORMAL MODE")) Queue(() => SetMode(false));
            if (GUILayout.Button("5 - PURE WAVE RULE TESTS")) Queue(() => { report = MS7I1EndlessDirector.PureRulesQA(); Debug.Log(report); });
            if (GUILayout.Button("6 - EXPORT 7I.1 QA TXT")) Queue(Export);
            if (GUILayout.Button("7 - BACKUP + REMOVE ONLY 7I.1 COMPONENT")) Queue(RemoveComponent);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Latest report", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(report, GUILayout.MinHeight(210), GUILayout.ExpandHeight(true));
        }
        finally { EditorGUILayout.EndScrollView(); }
    }

    private static WaveManager FindManager()
    {
        if (!SceneManager.GetActiveScene().IsValid()) return null;
        var all = UnityEngine.Object.FindObjectsByType<WaveManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return all.Length == 1 && all[0].gameObject.scene == SceneManager.GetActiveScene() ? all[0] : null;
    }

    private static bool Check(out WaveManager manager, out string reason)
    {
        manager = null;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "SampleScene" || Application.isPlaying)
        { reason = "Open SampleScene in EDIT MODE (not MainMenu, not Play Mode)."; return false; }
        manager = FindManager();
        if (manager == null) { reason = "Expected exactly one existing WaveManager in SampleScene."; return false; }
        if (manager.enemySpawner == null) { reason = "WaveManager.enemySpawner is unassigned."; return false; }
        var gm = manager.gameManager ? manager.gameManager : UnityEngine.Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        if (gm == null || !gm.victoryByWaves)
        { reason = "GameManager missing or victoryByWaves=False; Wave mode must remain enabled."; return false; }
        if (manager.totalWaves != 5 || manager.bossWave != 5)
        { reason = "7I.1 expects the original five waves with bossWave=5."; return false; }
        Type t = typeof(WaveManager);
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var a = t.GetField("bossSpawned", flags);
        var b = t.GetField("bossDefeated", flags);
        var p = t.GetProperty("CurrentWave", BindingFlags.Public | BindingFlags.Instance);
        if (a == null || b == null || p == null || p.GetSetMethod(true) == null ||
            a.FieldType != typeof(bool) || b.FieldType != typeof(bool) || p.PropertyType != typeof(int))
        { reason = "WaveManager internal API differs; do not install until audited."; return false; }
        reason = "READY: real 5-wave WaveManager, Boss flags, wave setter, spawner, GameManager verified.";
        return true;
    }

    private static string Audit()
    {
        WaveManager manager; string reason;
        if (!Check(out manager, out reason)) return "[BLOCKED] " + reason;
        var addon = manager.GetComponent<MS7I1EndlessDirector>();
        return "[PASS] " + reason + "\n" +
            "Scene=" + manager.gameObject.scene.path + "\n" +
            "BossPrefab=" + (manager.bossPrefab ? manager.bossPrefab.name : "UNASSIGNED") + "\n" +
            "Existing 7I1 Component=" + (addon ? "YES" : "NO") +
            (addon ? " Mode=" + addon.enableEndlessMode + " MaxEnemies=" + addon.maxConcurrentEnemies : "") +
            "\nNote: 7I1 needs runtime Boss-wave takeover; other 7E/F scripts require in-game QA.";
    }

    private static bool Backup(Scene scene, out string path, out string reason)
    {
        path = ""; reason = "";
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string originalScenePath = Path.Combine(project, scene.path);
        if (string.IsNullOrEmpty(scene.path) || !File.Exists(originalScenePath))
        { reason = "Scene is not saved to disk"; return false; }
        string folder = Path.Combine(project, "MS_Backups", "7I1");
        try
        {
            Directory.CreateDirectory(folder);
            path = Path.Combine(folder, "SampleScene_BEFORE_7I1_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity");
            File.Copy(originalScenePath, path, false);
            if (new FileInfo(path).Length < 100) throw new IOException("Empty backup");
            return true;
        }
        catch(Exception e) { reason = e.Message; return false; }
    }

    private bool Prepare(out WaveManager manager, out string backup)
    {
        manager = null; backup = "";
        string message;
        if (!Check(out manager, out message)) { report = "[BLOCKED] " + message; return false; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { report = "[CANCELLED] User declined to save the scene."; return false; }
        if (!Backup(SceneManager.GetActiveScene(), out backup, out message))
        { report = "[BLOCKED] Unable to back up Scene: " + message; return false; }
        return true;
    }

    private void Install()
    {
        WaveManager manager; string backup;
        if (!Prepare(out manager, out backup)) return;
        var addon = manager.GetComponent<MS7I1EndlessDirector>();
        if (addon == null) addon = Undo.AddComponent<MS7I1EndlessDirector>(manager.gameObject);
        Undo.RecordObject(addon, "7I.1 install Endless OFF");
        addon.waveManager = manager;
        addon.enemySpawner = manager.enemySpawner;
        addon.enableEndlessMode = false;
        EditorUtility.SetDirty(addon);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        EditorSceneManager.SaveScene(manager.gameObject.scene);
        report = "[INSTALLED] 7I.1 on existing WaveManager; mode OFF; normal game unchanged.\nBackup=" + backup;
        Debug.Log(report);
    }

    private void SetMode(bool enable)
    {
        WaveManager manager; string backup;
        if (!Prepare(out manager, out backup)) return;
        var addon = manager.GetComponent<MS7I1EndlessDirector>();
        if (addon == null) { report = "[BLOCKED] Install the 7I.1 component first (button 2)."; return; }
        Undo.RecordObject(addon, "7I.1 toggle Endless mode");
        addon.enableEndlessMode = enable;
        EditorUtility.SetDirty(addon);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        EditorSceneManager.SaveScene(manager.gameObject.scene);
        report = "[MODE] enableEndlessMode=" + enable + "\n" +
            (enable ? "In Play, Wave 5 Boss must die, then Wave 6 begins. Normal 5-wave Victory intentionally suppressed." : "Wave5 normal Victory restored on next Play run.") +
            "\nBackup=" + backup;
        Debug.Log(report);
    }

    private void RemoveComponent()
    {
        WaveManager manager; string backup;
        if (!Prepare(out manager, out backup)) return;
        var addon = manager.GetComponent<MS7I1EndlessDirector>();
        if (addon == null) { report = "[INFO] No 7I.1 component found."; return; }
        Undo.DestroyObjectImmediate(addon);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        EditorSceneManager.SaveScene(manager.gameObject.scene);
        report = "[REMOVED] Only 7I.1 component removed. No changes to WaveManager.cs or Save.\nBackup=" + backup;
        Debug.Log(report);
    }

    private void Export()
    {
        var sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 7I.1 / ENDLESS CORE QA");
        sb.AppendLine("Time=" + DateTime.Now.ToString("O") + " Unity=" + Application.unityVersion +
            " Mode=" + (Application.isPlaying ? "PLAY" : "EDIT") + " Scene=" + SceneManager.GetActiveScene().name);
        var manager = FindManager();
        if (manager == null) sb.AppendLine("[PENDING] No unique WaveManager in active Scene.");
        else
        {
            var addon = manager.GetComponent<MS7I1EndlessDirector>();
            if (addon == null) sb.AppendLine("[PENDING] 7I1 component missing.");
            else
            {
                sb.AppendLine("ModeInspector=" + addon.enableEndlessMode + " RuntimeEnabled=" + addon.RuntimeEnabled +
                    " BossGateTakenOver=" + addon.BossGateTakenOver + " EndlessStarted=" + addon.EndlessStarted);
                sb.AppendLine("WaveManager.CurrentWave=" + manager.CurrentWave + " 7I1CurrentWave=" + addon.CurrentEndlessWave +
                    " Peak=" + addon.PeakEndlessWave + " Finished=" + addon.WavesFinished);
                sb.AppendLine("Status=" + addon.LastStatus + " Errors=" + addon.ErrorCount);
                sb.AppendLine("Spawner: enabled=" + manager.enemySpawner.enabled +
                    " maxEnemies=" + manager.enemySpawner.maxEnemies +
                    " interval=" + manager.enemySpawner.spawnInterval.ToString("F3"));
                foreach (var line in addon.Trace) sb.AppendLine("TRACE " + line);
            }
        }
        sb.AppendLine(MS7I1EndlessDirector.PureRulesQA());
        sb.AppendLine("MANUAL PENDING: boss wave 5 gate, Wave6/7 spawn, pause, GameOver, 7E/7F effects, HUD, normal mode Victory, Profile/Save unchanged.");
        string folder = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath,"..")), "MS_QAReports", "7I1");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "MS7I1_EndlessQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".txt");
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        report = "[EXPORTED] " + path + "\n\n" + sb;
        Debug.Log("[7I.1] QA saved: " + path);
    }
}
#endif
