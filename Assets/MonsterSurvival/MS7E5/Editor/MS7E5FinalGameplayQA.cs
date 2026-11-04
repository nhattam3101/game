#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 7E.5 Editor release gate. Audit/export are READ ONLY.
/// The one-time installer only adds MS7E5FinalGameplayRecorder to the current WaveManager
/// after explicit confirmation and a real .unity backup.
/// </summary>
public sealed class MS7E5FinalGameplayQA : EditorWindow
{
    private static readonly string[] ManualItems =
    {
        "7E.1: Bulwark / Swift / Regenerator each visibly behave as designed",
        "7E.3: Affix rings match types; Hit Flash / Enrage / Death Burst visible",
        "7E.4: Original EnemyController EXP + Gold still awarded exactly once",
        "7E.4: Extra rewards match preview and no duplicate payouts per Elite",
        "7E.2: Rush, Siege, Swarm pacing is visible in Waves 2, 3, 4",
        "Wave 5 boss unchanged, boss HP-follow UI works, Shadow Titan base 500 HP",
        "Pause freezes gameplay, elite regeneration and wave/surge timers",
        "Enemy cap and safe placement work; no enemies spawn into walls/breaks",
        "Healer and other enemy types still spawn and function normally",
        "Victory / Game Over / Retry / Pause menus still work",
        "Save/Gold: victory banks run gold; defeat/retry does not bank it",
        "Profiles remain independent; inventory 20 slots and Equipment 3 slots reload",
        "7D: 0/3->1/3->2/3->3/3 set; bonus adds/retracts exactly once",
        "7C synergy skills and 5B permanent upgrades do not double count stats",
        "1920x1080, 1280x720, 1024x768: SETS readable, minimal HUD unobstructed"
    };

    private Vector2 scroll;
    private string lastAudit = "Run READ-ONLY AUDIT after opening your gameplay scene.";
    private const string SessionPrefix = "MS7E5.manual.v1.";

    [MenuItem("Tools/MonsterSurvival/7E Final Gameplay QA/1 - Open 7E.5 Final QA")]
    public static void Open() { GetWindow<MS7E5FinalGameplayQA>("MS 7E.5 Final QA"); }

    [MenuItem("Tools/MonsterSurvival/7E Final Gameplay QA/2 - READ-ONLY AUDIT")]
    private static void MenuAudit() { Debug.Log(BuildAudit()); }

    [MenuItem("Tools/MonsterSurvival/7E Final Gameplay QA/3 - EXPORT TXT (PLAY MODE)")]
    private static void MenuExport() { Export(); }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("MonsterSurvival 7E.5 — Final Gameplay QA", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("READ-ONLY AUDIT and EXPORT never change gameplay. Installer adds only a recorder to WaveManager after scene backup. Export DURING Play Mode for live evidence.", MessageType.Info);
        GUILayout.Space(6);

        if (GUILayout.Button("1 — READ-ONLY SCENE + PREFAB AUDIT", GUILayout.Height(29)))
        { lastAudit = BuildAudit(); Debug.Log(lastAudit); }
        if (GUILayout.Button("2 — BACKUP SCENE + INSTALL READ-ONLY RECORDER", GUILayout.Height(29)))
        { InstallRecorder(); lastAudit = BuildAudit(); }
        if (GUILayout.Button("3 — EXPORT FINAL QA REPORT TXT", GUILayout.Height(29))) Export();
        GUILayout.Space(8);
        EditorGUILayout.LabelField("MANUAL REGRESSION CONFIRMATIONS — mark only after testing", EditorStyles.boldLabel);
        int checkedCount = 0;
        for (int i = 0; i < ManualItems.Length; i++)
        {
            bool value = SessionState.GetBool(SessionPrefix + i, false);
            bool next = EditorGUILayout.ToggleLeft((i + 1).ToString("00") + " | " + ManualItems[i], value);
            if (next != value) SessionState.SetBool(SessionPrefix + i, next);
            if (next) checkedCount++;
        }
        EditorGUILayout.LabelField("Confirmed: " + checkedCount + "/" + ManualItems.Length);
        if (GUILayout.Button("RESET ALL MANUAL CONFIRMATIONS"))
        {
            if (EditorUtility.DisplayDialog("Reset 7E.5 confirmations?", "All 15 confirmations will be reset for a new QA session.", "Reset", "Cancel"))
                for (int i = 0; i < ManualItems.Length; i++) SessionState.SetBool(SessionPrefix + i, false);
        }
        GUILayout.Space(8);
        EditorGUILayout.LabelField("Latest audit", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(lastAudit, GUILayout.MinHeight(170));
        EditorGUILayout.EndScrollView();
    }

    private static T[] SceneComponents<T>() where T : UnityEngine.Object
    { return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None); }

    private static void Line(StringBuilder s, ref int pass, ref int warn, ref int error,
        bool good, string description, bool mandatory)
    {
        if (good) { pass++; s.AppendLine("[PASS] " + description); }
        else if (mandatory) { error++; s.AppendLine("[ERROR] " + description); }
        else { warn++; s.AppendLine("[WARN] " + description); }
    }

    private static string BuildAudit()
    {
        StringBuilder s = new StringBuilder();
        int pass = 0, warn = 0, error = 0;
        Scene scene = SceneManager.GetActiveScene();
        s.AppendLine("MONSTERSURVIVAL 7E.5 — READ-ONLY PRE-FLIGHT");
        s.AppendLine("Unity=" + Application.unityVersion + " Scene=" + scene.name +
            " PlayMode=" + EditorApplication.isPlaying + " Time=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        s.AppendLine("No player state, prefab, spawner or Save changes during this audit.");
        s.AppendLine("=== SCENE ===");

        WaveManager[] waves = SceneComponents<WaveManager>();
        EnemySpawner[] spawners = SceneComponents<EnemySpawner>();
        EliteSpawnDirector[] elites = SceneComponents<EliteSpawnDirector>();
        MS7E2DynamicWaveDirector[] dynamics = SceneComponents<MS7E2DynamicWaveDirector>();
        MS7E5FinalGameplayRecorder[] recorders = SceneComponents<MS7E5FinalGameplayRecorder>();
        Line(s, ref pass, ref warn, ref error, waves.Length == 1, "One existing WaveManager (found " + waves.Length + ")", true);
        Line(s, ref pass, ref warn, ref error, spawners.Length == 1, "One existing EnemySpawner (found " + spawners.Length + ")", true);
        Line(s, ref pass, ref warn, ref error, elites.Length == 1, "One existing EliteSpawnDirector (found " + elites.Length + ")", true);
        Line(s, ref pass, ref warn, ref error, dynamics.Length == 1, "One 7E.2 DynamicWaveDirector (found " + dynamics.Length + ")", true);
        Line(s, ref pass, ref warn, ref error, recorders.Length == 1, "One 7E.5 Recorder (found " + recorders.Length + ")", false);

        WaveManager wave = waves.Length == 1 ? waves[0] : null;
        EnemySpawner spawner = spawners.Length == 1 ? spawners[0] : null;
        MS7E2DynamicWaveDirector director = dynamics.Length == 1 ? dynamics[0] : null;
        EliteSpawnDirector eliteDirector = elites.Length == 1 ? elites[0] : null;
        Line(s, ref pass, ref warn, ref error,
            wave != null && spawner != null && wave.enemySpawner == spawner,
            "WaveManager.enemySpawner maps to existing EnemySpawner", true);
        Line(s, ref pass, ref warn, ref error,
            director != null && director.waveManager == wave && director.enemySpawner == spawner,
            "7E.2 references existing WaveManager and EnemySpawner", true);
        Line(s, ref pass, ref warn, ref error, director != null && director.enableDynamicEncounters,
            "7E.2 dynamic encounters opted IN", false);
        Line(s, ref pass, ref warn, ref error, director != null && director.excludeBossWave,
            "7E.2 excludes Boss Wave", true);
        Line(s, ref pass, ref warn, ref error,
            recorders.Length == 1 && recorders[0].waveManager == wave && recorders[0].enemySpawner == spawner && recorders[0].dynamicWaves == director,
            "7E.5 Recorder references existing Wave/Spawner/Director", false);

        s.AppendLine("=== ELITE PREFAB (NO MODIFICATIONS) ===");
        GameObject prefab = eliteDirector != null ? eliteDirector.elitePrefab : null;
        string path = prefab != null ? AssetDatabase.GetAssetPath(prefab) : "";
        Line(s, ref pass, ref warn, ref error,
            prefab != null && !string.IsNullOrEmpty(path),
            "Existing EliteSpawnDirector.elitePrefab asset: " + (string.IsNullOrEmpty(path) ? "MISSING" : path), true);
        if (prefab != null)
        {
            Line(s, ref pass, ref warn, ref error, prefab.GetComponent<EliteEnemy>() != null, "Existing EliteEnemy Enrage intact", true);
            Line(s, ref pass, ref warn, ref error, prefab.GetComponent<EnemyController>() != null && prefab.GetComponent<Health>() != null,
                "Existing EnemyController and Health intact", true);
            Line(s, ref pass, ref warn, ref error, prefab.GetComponent<MS7E1EliteAffix>() != null, "7E.1 Elite Affix present", true);
            Line(s, ref pass, ref warn, ref error, prefab.GetComponent<MS7E3EliteFeedback>() != null, "7E.3 Elite VFX present", true);
            MS7E4EliteRewards rewards = prefab.GetComponent<MS7E4EliteRewards>();
            Line(s, ref pass, ref warn, ref error, rewards != null, "7E.4 Extra Rewards present", true);
            Line(s, ref pass, ref warn, ref error, rewards != null && rewards.ExtraRewardsEnabled,
                "7E.4 real extra rewards enabled", false);
            if (rewards != null)
            {
                int x, g;
                rewards.PreviewBonus(MS7E1EliteAffix.AffixKind.Bulwark, out x, out g); s.AppendLine("[BALANCE] Bulwark EXTRA " + x + " EXP / " + g + " Gold");
                rewards.PreviewBonus(MS7E1EliteAffix.AffixKind.Swift, out x, out g); s.AppendLine("[BALANCE] Swift EXTRA " + x + " EXP / " + g + " Gold");
                rewards.PreviewBonus(MS7E1EliteAffix.AffixKind.Regenerator, out x, out g); s.AppendLine("[BALANCE] Regenerator EXTRA " + x + " EXP / " + g + " Gold");
            }
        }
        Line(s, ref pass, ref warn, ref error, SceneComponents<MS7D2SetBonusApplier>().Length == 1,
            "7D Equipment Set Bonus Applier remains in scene", false);
        s.AppendLine("STRUCTURE RESULT PASS=" + pass + " WARN=" + warn + " ERROR=" + error);
        return s.ToString();
    }

    private static void InstallRecorder()
    {
        if (EditorApplication.isPlaying)
        { EditorUtility.DisplayDialog("Edit Mode required", "Exit Play Mode before installing recorder.", "OK"); return; }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        { EditorUtility.DisplayDialog("Save scene first", "Save your gameplay scene (.unity) before installing.", "OK"); return; }
        if (scene.isDirty)
        { EditorUtility.DisplayDialog("Save scene first", "Save the scene (Ctrl+S) before installing. Installer requires a clean backup.", "OK"); return; }
        WaveManager[] waves = SceneComponents<WaveManager>();
        if (waves.Length != 1)
        { EditorUtility.DisplayDialog("Unsafe scene", "Expected exactly one WaveManager. No changes made.", "OK"); return; }
        WaveManager wave = waves[0];
        if (wave.enemySpawner == null)
        { EditorUtility.DisplayDialog("Missing spawner", "WaveManager.enemySpawner must reference your existing spawner.", "OK"); return; }
        MS7E2DynamicWaveDirector director = wave.GetComponent<MS7E2DynamicWaveDirector>();
        if (director == null)
        { EditorUtility.DisplayDialog("7E.2 missing", "The existing WaveManager has no 7E.2 Director.", "OK"); return; }
        MS7E5FinalGameplayRecorder[] all = SceneComponents<MS7E5FinalGameplayRecorder>();
        if (all.Length > 1 || (all.Length == 1 && all[0].gameObject != wave.gameObject))
        { EditorUtility.DisplayDialog("Duplicate recorder", "Remove duplicate/incorrect recorders manually before installing.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("7E.5 recorder installation",
            "Create a physical backup of the active .unity scene, then add/repair ONE read-only recorder on the existing WaveManager? No gameplay fields or Save files will be changed.",
            "Backup + Install", "Cancel")) return;
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string source = Path.Combine(projectRoot, scene.path);
        if (!File.Exists(source))
        { EditorUtility.DisplayDialog("Scene backup failed", "Scene file not found: " + source, "OK"); return; }
        string backupFolder = Path.Combine(projectRoot, "MS_Backups/7E5");
        Directory.CreateDirectory(backupFolder);
        string backupPath = Path.Combine(backupFolder, Path.GetFileNameWithoutExtension(source) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity");
        try { File.Copy(source, backupPath, false); }
        catch (Exception ex)
        { EditorUtility.DisplayDialog("Backup failed", ex.Message + "\nNo component installed.", "OK"); return; }

        MS7E5FinalGameplayRecorder recorder = wave.GetComponent<MS7E5FinalGameplayRecorder>();
        if (recorder == null) recorder = Undo.AddComponent<MS7E5FinalGameplayRecorder>(wave.gameObject);
        if (recorder == null) { Debug.LogError("7E.5: Cannot add recorder. Scene backup: " + backupPath); return; }
        Undo.RecordObject(recorder, "7E.5 Link read-only QA recorder");
        recorder.waveManager = wave;
        recorder.enemySpawner = wave.enemySpawner;
        recorder.dynamicWaves = director;
        recorder.recordLiveEvidence = true;
        EditorUtility.SetDirty(recorder);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[7E.5] Recorder installed on existing WaveManager. BACKUP=" + backupPath + " | Save scene (Ctrl+S), then Play Mode and export TXT.");
        EditorUtility.DisplayDialog("Installed", "7E.5 recorder attached. Backup:\n" + backupPath + "\n\nSave Scene (Ctrl+S), then Play Mode.", "OK");
    }

    private static string BuildReport(out bool releasePass)
    {
        StringBuilder s = new StringBuilder();
        s.AppendLine("MONSTERSURVIVAL 7E.5 — FINAL GAMEPLAY RELEASE GATE");
        s.AppendLine("Generated=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
            " | Unity=" + Application.unityVersion + " | Scene=" + SceneManager.GetActiveScene().name +
            " | PlayMode=" + EditorApplication.isPlaying);
        s.AppendLine("READ ONLY: This report has not modified game state, Save/Profile or scenes.");
        s.AppendLine(BuildAudit());
        MS7E5FinalGameplayRecorder[] rec = SceneComponents<MS7E5FinalGameplayRecorder>();
        MS7E5FinalGameplayRecorder r = rec.Length == 1 ? rec[0] : null;
        s.AppendLine("=== RUNTIME RELEASE EVIDENCE ===");
        bool live = EditorApplication.isPlaying && r != null && r.Recording && r.Samples > 0;
        s.AppendLine(Status(live, "Active Play Mode recorder with >0 samples"));
        if (r != null && EditorApplication.isPlaying) s.AppendLine(r.GetEvidence());
        else s.AppendLine("[PENDING] Missing LIVE recorder. Export inside Play Mode, not after stopping.");

        bool encounter = live && r.SawRush && r.SawSiege && r.SawSwarm;
        bool boss = live && r.SawBossExcluded;
        bool restore = live && r.SawSpawnerRestore;
        bool affixes = live && r.SawBulwark && r.SawSwift && r.SawRegenerator;
        bool vfx = live && MS7E3EliteFeedback.SpawnVisuals >= 3 && MS7E3EliteFeedback.HitFlashes > 0 &&
            MS7E3EliteFeedback.EnrageWarnings > 0 && MS7E3EliteFeedback.DeathBursts > 0;
        bool reward = live && MS7E4EliteRewards.GrantedDeaths >= 3 &&
            MS7E4EliteRewards.BulwarkPayouts > 0 && MS7E4EliteRewards.SwiftPayouts > 0 && MS7E4EliteRewards.RegeneratorPayouts > 0 &&
            MS7E4EliteRewards.DeathEvents >= MS7E4EliteRewards.GrantedDeaths &&
            MS7E4EliteRewards.GrantedDeaths == MS7E4EliteRewards.BulwarkPayouts +
                MS7E4EliteRewards.SwiftPayouts + MS7E4EliteRewards.RegeneratorPayouts;
        MS7E2DynamicWaveDirector[] directors = SceneComponents<MS7E2DynamicWaveDirector>();
        bool surgeNeeded = directors.Length == 1 && directors[0].enableSurge;
        bool surge = !surgeNeeded || live && r.SawSurge;
        s.AppendLine(Status(encounter, "Rush @ Wave2, Siege @ Wave3, Swarm @ Wave4 observed"));
        s.AppendLine(Status(boss, "Wave5 boss excluded from 7E.2 applying"));
        s.AppendLine(Status(restore, "EnemySpawner baseline restored at least once"));
        s.AppendLine(Status(surge, "Surge observed (or Surge disabled by design)"));
        s.AppendLine(Status(affixes, "All three live Affix types observed"));
        s.AppendLine(Status(vfx, "Live VFX initialized / hit / enrage / death events"));
        s.AppendLine(Status(reward, "All three Affix reward payouts observed with consistent event counters"));
        s.AppendLine("NOTE: No method-call counter can prove PlayerProgression wallet totals, anti-double-award or persistence by itself.");

        s.AppendLine("=== MANUAL QA CONFIRMATIONS ===");
        int manual = 0;
        for (int i = 0; i < ManualItems.Length; i++)
        {
            bool val = SessionState.GetBool(SessionPrefix + i, false);
            if (val) manual++;
            s.AppendLine((val ? "[USER CONFIRMED] " : "[NOT CONFIRMED] ") + (i + 1).ToString("00") + " | " + ManualItems[i]);
        }
        s.AppendLine("MANUAL=" + manual + "/" + ManualItems.Length + " (self-reported; no automatic gameplay proof)");
        bool structureOk = !BuildAudit().Contains("[ERROR]");
        bool allRuntime = live && encounter && boss && restore && surge && affixes && vfx && reward;
        s.AppendLine("=== RELEASE GATE ===");
        s.AppendLine("STRUCTURE=" + (structureOk ? "PASS" : "BLOCKED"));
        s.AppendLine("RUNTIME=" + (allRuntime ? "PASS" : "PENDING"));
        s.AppendLine("MANUAL=" + (manual == ManualItems.Length ? "USER_CONFIRMED" : "PENDING"));
        releasePass = structureOk && allRuntime && manual == ManualItems.Length;
        s.AppendLine("RESULT=" + (releasePass ? "READY_FOR_USER_REVIEW" : "PENDING - DO NOT MARK 7E COMPLETE"));
        s.AppendLine("RELEASE NOTE: READY_FOR_USER_REVIEW is not an automated proof of persistence, final game balance or Unity build success.");
        return s.ToString();
    }

    private static string Status(bool good, string message)
    { return (good ? "[OBSERVED] " : "[PENDING] ") + message; }

    private static void Export()
    {
        bool ready;
        string report = BuildReport(out ready);
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string fileName = "MS7E5_FinalGameplayQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
        string path = EditorUtility.SaveFilePanel("Export 7E.5 Final QA TXT", projectRoot, fileName, "txt");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            File.WriteAllText(path, report, new UTF8Encoding(false));
            Debug.Log("[7E.5] Report exported: " + path + " | " + (ready ? "READY_FOR_USER_REVIEW" : "PENDING"));
            EditorUtility.DisplayDialog("7E.5 Report", "Saved TXT:\n" + path + "\n\nRelease: " + (ready ? "READY_FOR_USER_REVIEW" : "PENDING") +
                "\n\nSend this TXT for review; do not assume a pending gate passed.", "OK");
        }
        catch (Exception ex) { Debug.LogError("[7E.5] Export failed: " + ex); }
    }
}
#endif
