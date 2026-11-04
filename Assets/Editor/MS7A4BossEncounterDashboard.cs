#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only QA dashboard: NEVER changes SaveData, Boss rewards or gameplay numbers.
[InitializeOnLoad]
public sealed class MS7A4EditorPlayHistory
{
    internal const string SessionKey = "MS7A4_BossQA_History_LastRun";

    static MS7A4EditorPlayHistory()
    {
        EditorApplication.playModeStateChanged += StateChanged;
    }

    private static void StateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            MS7A4EncounterHistory history = new MS7A4EncounterHistory();
            history.records.AddRange(MS7A4BossEncounterTracker.Sessions);
            var liveTracker = UnityEngine.Object.FindFirstObjectByType<MS7A4BossEncounterTracker>(FindObjectsInactive.Include);
            if (liveTracker != null && liveTracker.LiveRecord != null)
            {
                MS7A4EncounterRecord incomplete = JsonUtility.FromJson<MS7A4EncounterRecord>(JsonUtility.ToJson(liveTracker.LiveRecord));
                incomplete.result = "Stopped Play Mode (incomplete)";
                incomplete.fightSeconds = liveTracker.LiveFightSeconds;
                history.records.Add(incomplete);
            }
            SessionState.SetString(SessionKey, JsonUtility.ToJson(history));
        }
    }
}

public sealed class MS7A4BossEncounterDashboard : EditorWindow
{
    private const string Menu = "Tools/MonsterSurvival/7A Boss Balancing/";
    private Vector2 scroll;
    private bool showDetails = true;
    private bool showComparison = true;

    [MenuItem(Menu + "13 - OPEN 7A.4 Boss Encounter Dashboard")]
    public static void OpenWindow()
    {
        GetWindow<MS7A4BossEncounterDashboard>("7A.4 Encounter QA").Show();
    }

    [MenuItem(Menu + "14 - INSTALL 7A.4 Tracker (OPEN SampleScene)")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorUtility.DisplayDialog("7A.4", "Stop Play Mode before installing.", "OK"); return; }
        WaveManager[] managers = UnityEngine.Object.FindObjectsByType<WaveManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (managers.Length != 1)
        {
            EditorUtility.DisplayDialog("7A.4", "Open the gameplay scene containing exactly one WaveManager. Found: " + managers.Length, "OK");
            return;
        }
        WaveManager wm = managers[0];
        if (!wm.gameObject.scene.IsValid() || !wm.gameObject.scene.isLoaded)
        { EditorUtility.DisplayDialog("7A.4", "WaveManager must belong to an open scene, not a prefab asset.", "OK"); return; }
        MS7A4BossEncounterTracker tracker = wm.GetComponent<MS7A4BossEncounterTracker>();
        if (tracker == null)
        {
            tracker = Undo.AddComponent<MS7A4BossEncounterTracker>(wm.gameObject);
            tracker.waveManager = wm;
            tracker.mapSwitcher = wm.GetComponent<WaveMapSwitcher>();
            EditorSceneManager.MarkSceneDirty(wm.gameObject.scene);
            Debug.Log("[7A.4] Installed one READ-ONLY Boss Encounter Tracker on " + wm.name + ". Save scene with Ctrl+S.");
        }
        else Debug.Log("[7A.4] Tracker already present; no duplicate created.");
        Selection.activeGameObject = wm.gameObject;
        OpenWindow();
    }

    [MenuItem(Menu + "15 - AUDIT 7A.4 (READ ONLY)")]
    public static void Audit()
    {
        string report = BuildAudit();
        Debug.Log(report);
        EditorUtility.DisplayDialog("7A.4 Audit", report.Length > 1500 ? report.Substring(0, 1500) + "\nSee Console for full audit." : report, "OK");
    }

    [MenuItem(Menu + "16 - REMOVE 7A.4 Tracker ONLY")]
    public static void Remove()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var trackers = UnityEngine.Object.FindObjectsByType<MS7A4BossEncounterTracker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (trackers.Length == 0) { Debug.Log("[7A.4] No tracker found."); return; }
        if (!EditorUtility.DisplayDialog("Remove 7A.4", "Remove " + trackers.Length + " 7A.4 tracker(s) from open scene(s)? No Save/AI/reward data is touched.", "REMOVE", "CANCEL")) return;
        foreach (var t in trackers)
        {
            if (t != null && t.gameObject.scene.IsValid())
            {
                Scene scene = t.gameObject.scene;
                Undo.DestroyObjectImmediate(t);
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }
        Debug.Log("[7A.4] Removed tracker component(s) only. Save the scene.");
    }

    [MenuItem(Menu + "17 - RUN 7A.4 Isolated Economy Math QA")]
    public static void MathQA()
    {
        int pass = 0;
        var log = new StringBuilder("[7A.4 PURE MATH QA] No Save files or scenes modified.\n");
        Test("no level-up EXP +4", MS7A4BossEncounterTracker.CalculateExpEarned(3, 1, 10, 7, 1) == 4, ref pass, log);
        Test("one level-up EXP +5", MS7A4BossEncounterTracker.CalculateExpEarned(8, 1, 10, 3, 2) == 5, ref pass, log);
        Test("two level-ups EXP +20", MS7A4BossEncounterTracker.CalculateExpEarned(8, 1, 10, 5, 3) == 20, ref pass, log);
        Test("unchanged progression EXP +0", MS7A4BossEncounterTracker.CalculateExpEarned(2, 2, 13, 2, 2) == 0, ref pass, log);
        // Gold earned tracks only positive changes; net gold may be negative.
        int[] coinBalances = { 80, 90, 75, 85 };
        int gross = 0;
        for (int i = 1; i < coinBalances.Length; i++)
            gross += Math.Max(0, coinBalances[i] - coinBalances[i - 1]);
        Test("Gold gross +20 vs net +5", gross == 20 && coinBalances[3] - coinBalances[0] == 5, ref pass, log);
        log.AppendLine("RESULT " + pass + "/5 PASS");
        Debug.Log(log.ToString());
        EditorUtility.DisplayDialog("7A.4 Math QA", pass + "/5 PASS. Full details in Console.", "OK");
    }

    private static void Test(string label, bool ok, ref int pass, StringBuilder log)
    {
        if (ok) pass++;
        log.AppendLine((ok ? "[PASS] " : "[ERROR] ") + label);
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("MONSTERSURVIVAL 7A.4", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Boss Encounter QA & Reward Economy", EditorStyles.largeLabel);
        EditorGUILayout.HelpBox("READ ONLY. Measures WaveManager-spawned boss encounters, Player HP loss events, run Gold and EXP earned during encounter. HP loss may come from any enemy. Gold/EXP include all sources, NOT exclusive boss rewards. Does not change Save, Boss AI, HP, rewards, achievements or bank Gold.", MessageType.Info);
        EditorGUILayout.LabelField("Mode", EditorApplication.isPlaying ? "PLAY - live data" : "EDIT - cached results from last Play session");
        EditorGUILayout.LabelField("Scene", SceneManager.GetActiveScene().name);
        EditorGUILayout.Space(5);
        if (GUILayout.Button("INSTALL TRACKER ON OPEN GAMEPLAY SCENE", GUILayout.Height(28))) Install();
        if (GUILayout.Button("AUDIT CURRENT SCENE (READ ONLY)", GUILayout.Height(26))) Audit();
        if (GUILayout.Button("RUN 5 PURE MATH TESTS", GUILayout.Height(26))) MathQA();
        EditorGUILayout.Space(8);
        List<MS7A4EncounterRecord> records = ReadHistory();
        EditorGUILayout.LabelField("COMPLETED / INTERRUPTED ENCOUNTERS", records.Count.ToString());
        MS7A4BossEncounterTracker live = UnityEngine.Object.FindFirstObjectByType<MS7A4BossEncounterTracker>(FindObjectsInactive.Include);
        if (EditorApplication.isPlaying && live != null)
            EditorGUILayout.HelpBox("Live: " + (live.LiveRecord == null ? live.Diagnostic :
                live.LiveRecord.bossName + " / " + live.LiveRecord.mapName +
                " / HP lost " + live.LiveRecord.playerHPLost.ToString("F1") +
                " / Gold+ " + live.LiveRecord.goldEarned + " / EXP+ " + live.LiveRecord.expEarned), MessageType.None);
        if (records.Count == 0)
            EditorGUILayout.HelpBox("No completed encounters recorded yet. In Play Mode, allow WaveManager to spawn its boss and defeat it or finish the run. Export before recompiling scripts.", MessageType.Warning);
        showComparison = EditorGUILayout.Foldout(showComparison, "COMPARE BY BOSS + MAP (EXPERIMENTAL)", true);
        if (showComparison) DrawComparison(records);
        showDetails = EditorGUILayout.Foldout(showDetails, "INDIVIDUAL ENCOUNTERS", true);
        if (showDetails) DrawRecords(records);
        EditorGUILayout.Space(10);
        using (new EditorGUI.DisabledScope(records.Count == 0))
        {
            if (GUILayout.Button("EXPORT CSV (SAVE FILE DIALOG)", GUILayout.Height(32))) ExportCsv(records);
            if (GUILayout.Button("EXPORT TXT (SAVE FILE DIALOG)", GUILayout.Height(32))) ExportTxt(records);
        }
        EditorGUILayout.HelpBox("Suggested first pass: 3-5 complete fights per Boss per map, repeat with Apply At Runtime OFF/ON (7A.3), then compare. Target times are starter hypotheses, not proven balance thresholds. You must independently test boss drops, achievements and bank Gold after Victory.", MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    private static void DrawComparison(List<MS7A4EncounterRecord> records)
    {
        var groups = records.Where(r => r.result == "Boss defeated")
            .GroupBy(r => (r.bossName ?? "?") + " @ " + (r.mapName ?? "?"));
        foreach (var group in groups)
        {
            var arr = group.ToArray();
            EditorGUILayout.LabelField(group.Key + "   |   " + arr.Length + " fight(s)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("  Avg time / HP lost / HP-loss events",
                Average(arr, x => x.fightSeconds).ToString("F1") + "s / " +
                Average(arr, x => x.playerHPLost).ToString("F1") + " / " +
                Average(arr, x => x.playerDamageEvents).ToString("F1"));
            EditorGUILayout.LabelField("  Avg run Gold earned / EXP earned",
                Average(arr, x => x.goldEarned).ToString("F1") + " / " +
                Average(arr, x => x.expEarned).ToString("F1"));
            if (arr.Length < 3) EditorGUILayout.HelpBox("Small sample: need at least 3 fights before judging this map/boss.", MessageType.Info);
        }
    }

    private static double Average(MS7A4EncounterRecord[] list, Func<MS7A4EncounterRecord, double> selector)
    {
        return list.Length == 0 ? 0 : list.Average(selector);
    }

    private static void DrawRecords(List<MS7A4EncounterRecord> records)
    {
        for (int i = 0; i < records.Count; i++)
        {
            MS7A4EncounterRecord r = records[i];
            EditorGUILayout.LabelField("#" + (i + 1) + " " + r.bossName + " / " + r.mapName + " / " + r.result);
            EditorGUILayout.LabelField("  " + r.fightSeconds.ToString("F1") + "s | HP lost " + r.playerHPLost.ToString("F1") +
                " (events " + r.playerDamageEvents + ") | Gold+ " + r.goldEarned +
                " (net " + r.goldNetChange + ") | EXP+ " + r.expEarned);
        }
    }

    private static List<MS7A4EncounterRecord> ReadHistory()
    {
        if (EditorApplication.isPlaying) return new List<MS7A4EncounterRecord>(MS7A4BossEncounterTracker.Sessions);
        string json = SessionState.GetString(MS7A4EditorPlayHistory.SessionKey, "");
        if (string.IsNullOrEmpty(json)) return new List<MS7A4EncounterRecord>();
        try
        {
            var saved = JsonUtility.FromJson<MS7A4EncounterHistory>(json);
            return saved != null && saved.records != null ? saved.records : new List<MS7A4EncounterRecord>();
        }
        catch { return new List<MS7A4EncounterRecord>(); }
    }

    private static string BuildAudit()
    {
        var sb = new StringBuilder("[7A.4 Boss Encounter QA AUDIT / READ ONLY]\n");
        WaveManager[] wm = UnityEngine.Object.FindObjectsByType<WaveManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        MS7A4BossEncounterTracker[] tr = UnityEngine.Object.FindObjectsByType<MS7A4BossEncounterTracker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine("Scene: " + SceneManager.GetActiveScene().name + " | WaveManager=" + wm.Length + " | 7A4 Tracker=" + tr.Length);
        sb.AppendLine(wm.Length == 1 ? "[PASS] Exactly one WaveManager" : "[WARN] Expected exactly one WaveManager");
        sb.AppendLine(tr.Length == 1 ? "[PASS] Exactly one tracker" : "[WARN] Expected exactly one tracker (Install if missing)");
        FieldInfo boss = typeof(WaveManager).GetField("bossHealth", BindingFlags.NonPublic | BindingFlags.Instance);
        sb.AppendLine(boss != null ? "[PASS] WaveManager private bossHealth field available for observation" : "[ERROR] WaveManager.bossHealth field not found. This tracker needs an update.");
        if (wm.Length == 1)
        {
            WaveManager w = wm[0];
            sb.AppendLine(w.bossPrefab != null ? "[PASS] Boss Prefab=" + w.bossPrefab.name : "[ERROR] Boss Prefab missing");
            sb.AppendLine("[INFO] bossWave=" + w.bossWave + ", bossName=" + w.bossName);
            var map = w.GetComponent<WaveMapSwitcher>();
            sb.AppendLine(map != null ? "[PASS] WaveMapSwitcher available (" + map.maps.Count + " maps)" : "[WARN] Map switcher missing; map label will be unknown");
            if (w.GetComponent<MS7A4BossEncounterTracker>() != null)
                sb.AppendLine("[PASS] Tracker installed on the WaveManager object");
        }
        sb.AppendLine("[INFO] 7A.3 compile hotfix must be applied separately if your project still has CS0619.");
        sb.AppendLine("[INFO] No Editor audit mutates Boss/Save/Gold/EXP.");
        return sb.ToString();
    }

    private static void ExportCsv(List<MS7A4EncounterRecord> list)
    {
        string path = EditorUtility.SaveFilePanel("7A.4 Export CSV", Application.dataPath, "MS7A4_EncounterResults", "csv");
        if (string.IsNullOrEmpty(path)) return;
        var sb = new StringBuilder();
        sb.AppendLine("UTC,Boss,Map,Scene,Wave,Result,Seconds,BossStartHP,BossDamageObserved,BossHitEvents,PlayerStartHP,PlayerHPLost,PlayerDamageEvents,GrossRunGold,NetRunGold,ExpEarned,Note");
        foreach (var r in list)
        {
            string[] fields = {
                r.utcDate,r.bossName,r.mapName,r.sceneName,r.wave.ToString(),r.result,
                F(r.fightSeconds),F(r.bossInitialHP),F(r.bossDamageObserved),r.bossHitEvents.ToString(),
                F(r.playerHPAtStart),F(r.playerHPLost),r.playerDamageEvents.ToString(),
                r.goldEarned.ToString(),r.goldNetChange.ToString(),r.expEarned.ToString(),r.note
            };
            sb.AppendLine(string.Join(",", fields.Select(Csv)));
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        Debug.Log("[7A.4] CSV exported: " + path);
    }

    private static string F(float x) { return x.ToString("0.###", CultureInfo.InvariantCulture); }
    private static string Csv(string s) { return "\"" + (s ?? "").Replace("\"", "\"\"") + "\""; }

    private static void ExportTxt(List<MS7A4EncounterRecord> list)
    {
        string path = EditorUtility.SaveFilePanel("7A.4 Export TXT", Application.dataPath, "MS7A4_EncounterQA", "txt");
        if (string.IsNullOrEmpty(path)) return;
        var sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 7A.4 — BOSS ENCOUNTER QA / READ ONLY");
        sb.AppendLine("Exported UTC " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("Unity " + Application.unityVersion + "; records=" + list.Count);
        sb.AppendLine("LIMITATIONS: HP loss events may be from any attacker. Run Gold/EXP earned may be from any source during encounter. Bank Gold is NOT measured. Results are not exclusive boss kill rewards. Manual tests required for Drops/Achievements/Victory.");
        sb.AppendLine();
        foreach (var r in list)
        {
            sb.AppendLine("[ENCOUNTER] " + r.utcDate + " | " + r.bossName + " | " + r.mapName + " | " + r.result);
            sb.AppendLine("wave=" + r.wave + " seconds=" + F(r.fightSeconds) + " bossInitialHP=" + F(r.bossInitialHP) +
                " bossDamageObserved=" + F(r.bossDamageObserved) + " bossHitEvents=" + r.bossHitEvents);
            sb.AppendLine("playerHPStart=" + F(r.playerHPAtStart) + " HP-lost=" + F(r.playerHPLost) +
                " playerDamageEvents=" + r.playerDamageEvents + " grossRunGold=" + r.goldEarned +
                " netRunGold=" + r.goldNetChange + " expEarned=" + r.expEarned);
            sb.AppendLine(r.note);
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        Debug.Log("[7A.4] TXT exported: " + path);
    }
}
#endif
