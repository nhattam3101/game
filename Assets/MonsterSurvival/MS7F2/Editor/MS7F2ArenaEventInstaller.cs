#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>7F.2 scene-safe installer + read-only audit. No overwrites to older gameplay scripts.</summary>
public sealed class MS7F2ArenaEventInstaller : EditorWindow
{
    private readonly string[] manualTests =
    {
        "Wave2: Twin Strike shows two warning circles, then one combined damage check",
        "Wave3: Crossfire shows two warning bands; crossing out dodges",
        "Wave4: Hollow Ring shows inner safe area and outer boundary",
        "All three telegraphs visible before activation, reasonable visibility",
        "Player outside hazard has no HP damage; inside checks Health/Shield once",
        "Pause holds active warnings/countdowns; Retry / GameOver remain correct",
        "7F.1 does not cause overlapping damage while 7F.2 is enabled",
        "Turning 7F.2 off restores the previous 7F.1 opt-in setting",
        "Wave breaks cancel pending hazards; Wave5 Boss never receives arena events",
        "Elite 7E / Dynamic Waves / EXP / Gold remain correct",
        "Save/Profile, Equipment 7D, Skill 7C and minimal HUD are unchanged"
    };

    [SerializeField] private bool[] confirmed = new bool[11];
    private Vector2 scroll;
    private string lastAudit = "Run 1 - AUDIT before installing.";

    [MenuItem("Tools/MonsterSurvival/7F Arena Events/7 - Open 7F.2 Multiple Events")]
    private static void Open()
    {
        GetWindow<MS7F2ArenaEventInstaller>(false, "7F.2 Multiple Events").minSize = new Vector2(600f, 530f);
    }

    private void OnGUI()
    {
        if (confirmed == null || confirmed.Length != manualTests.Length)
            confirmed = new bool[manualTests.Length];
        scroll = EditorGUILayout.BeginScrollView(scroll);
        GUILayout.Label("7F.2 - MULTIPLE ARENA EVENTS", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Twin Strike (Wave2), Crossfire (Wave3), Hollow Ring (Wave4).\n" +
            "7F.1 is temporarily suspended in Play Mode to prevent overlapping hazards.\n" +
            "New installation is OFF. Wave5 Boss is ALWAYS excluded.", MessageType.Info);
        if (GUILayout.Button("1 - AUDIT REFERENCES AND 7F.1 (READ ONLY)", GUILayout.Height(30)))
        { lastAudit = Audit(); Debug.Log(lastAudit); }
        if (GUILayout.Button("2 - BACKUP SCENE + INSTALL / REPAIR (DEFAULT OFF)", GUILayout.Height(31))) Install();
        if (GUILayout.Button("3 - BACKUP SCENE + ENABLE 7F.2 EVENTS", GUILayout.Height(31))) ChangeEnabled(true);
        if (GUILayout.Button("4 - BACKUP SCENE + DISABLE 7F.2 EVENTS", GUILayout.Height(28))) ChangeEnabled(false);
        if (GUILayout.Button("5 - PURE GEOMETRY / WAVE TESTS", GUILayout.Height(28)))
        { lastAudit = PureTests(); Debug.Log(lastAudit); }
        if (GUILayout.Button("6 - EXPORT 7F.2 QA TXT (WHILE PLAYING)", GUILayout.Height(30))) Export();
        GUILayout.Space(9);
        GUILayout.Label("MANUAL QA - tick only after actually testing", EditorStyles.boldLabel);
        for (int i = 0; i < manualTests.Length; i++)
            confirmed[i] = EditorGUILayout.ToggleLeft((i + 1).ToString("00") + "  " + manualTests[i], confirmed[i]);
        EditorGUILayout.LabelField("Manual confirmed", CountConfirmed() + " / " + manualTests.Length);
        GUILayout.Space(9);
        GUILayout.Label("Latest audit / tests", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(lastAudit, GUILayout.MinHeight(170));
        EditorGUILayout.EndScrollView();
    }

    private static WaveManager FindWave(out string reason)
    {
        Scene active = SceneManager.GetActiveScene();
        WaveManager[] found = UnityEngine.Object.FindObjectsByType<WaveManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        WaveManager wave = null;
        int count = 0;
        foreach (WaveManager w in found)
        {
            if (w != null && w.gameObject.scene == active) { wave = w; count++; }
        }
        if (count != 1) { reason = "Expected ONE WaveManager in active Scene, found " + count; return null; }
        reason = "Found WaveManager=" + wave.name;
        return wave;
    }

    private static bool CanChangeScene(out WaveManager wave)
    {
        wave = null;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("7F.2", "Exit Play Mode before editing the Scene. Use Export while playing.", "OK");
            return false;
        }
        Scene s = SceneManager.GetActiveScene();
        if (!s.IsValid() || string.IsNullOrEmpty(s.path))
        {
            EditorUtility.DisplayDialog("7F.2", "Save SampleScene first (File > Save).", "OK");
            return false;
        }
        string why;
        wave = FindWave(out why);
        if (wave == null)
        {
            EditorUtility.DisplayDialog("7F.2", why, "OK");
            return false;
        }
        if (wave.enemySpawner == null)
        {
            EditorUtility.DisplayDialog("7F.2", "WaveManager.enemySpawner is NULL. Fix it first.", "OK");
            return false;
        }
        return true;
    }

    private static bool BackupScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!EditorSceneManager.SaveScene(scene))
        {
            Debug.LogError("[7F.2] Unable to save the Scene; aborting.");
            return false;
        }
        if (!AssetDatabase.IsValidFolder("Assets/MS_Backups")) AssetDatabase.CreateFolder("Assets", "MS_Backups");
        if (!AssetDatabase.IsValidFolder("Assets/MS_Backups/7F2"))
            AssetDatabase.CreateFolder("Assets/MS_Backups", "7F2");
        string backup = AssetDatabase.GenerateUniqueAssetPath("Assets/MS_Backups/7F2/" +
            Path.GetFileNameWithoutExtension(scene.path) + "_7F2_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
        if (!AssetDatabase.CopyAsset(scene.path, backup))
        {
            Debug.LogError("[7F.2] Scene backup FAILED. Nothing changed.");
            return false;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[7F.2] Scene backup: " + backup);
        return true;
    }

    private void Install()
    {
        WaveManager wave;
        if (!CanChangeScene(out wave)) return;
        if (!EditorUtility.DisplayDialog("7F.2 INSTALL", "Create a Scene backup and attach 7F.2 to EXISTING WaveManager?\nNew installation is OFF. Existing gameplay scripts are untouched.", "Install", "Cancel")) return;
        if (!BackupScene()) return;
        MS7F2ArenaEventDirector d = wave.GetComponent<MS7F2ArenaEventDirector>();
        bool newlyAdded = d == null;
        if (newlyAdded) d = Undo.AddComponent<MS7F2ArenaEventDirector>(wave.gameObject);
        Undo.RecordObject(d, "7F.2 references");
        d.waveManager = wave;
        d.enemySpawner = wave.enemySpawner;
        d.legacy7F1 = wave.GetComponent<MS7F1ArenaHazardDirector>();
        if (newlyAdded) { d.enableMultipleEvents = false; d.suspend7F1WhileActive = true; }
        EditorUtility.SetDirty(d);
        EditorSceneManager.MarkSceneDirty(wave.gameObject.scene);
        EditorSceneManager.SaveScene(wave.gameObject.scene);
        Selection.activeObject = wave.gameObject;
        lastAudit = Audit();
        Debug.Log("[7F.2] " + (newlyAdded ? "Installed" : "Repaired") + " on existing WaveManager.");
    }

    private void ChangeEnabled(bool target)
    {
        WaveManager wave;
        if (!CanChangeScene(out wave)) return;
        MS7F2ArenaEventDirector d = wave.GetComponent<MS7F2ArenaEventDirector>();
        if (d == null)
        { EditorUtility.DisplayDialog("7F.2", "Install 7F.2 first.", "OK"); return; }
        if (target)
        {
            string issue;
            if (!d.TryValidate(out issue))
            { EditorUtility.DisplayDialog("7F.2 Preflight failed", issue, "OK"); return; }
            if (d.legacy7F1 != null && !d.suspend7F1WhileActive)
            { EditorUtility.DisplayDialog("7F.2", "Enable Suspend 7F1 While Active in Inspector before activating.", "OK"); return; }
        }
        string msg = target
            ? "ENABLE real player-impact events (default 6 damage).\n7F.1 will be suspended only during 7F.2 Play Mode and restored afterward.\nWave Boss 5 stays protected. Continue?"
            : "DISABLE 7F.2 and restore 7F.1 original behavior. Continue?";
        if (!EditorUtility.DisplayDialog("7F.2 EXPLICIT OPT-IN", msg, target ? "Enable" : "Disable", "Cancel")) return;
        if (!BackupScene()) return;
        Undo.RecordObject(d, "7F.2 Enable Multiple Events");
        d.enableMultipleEvents = target;
        EditorUtility.SetDirty(d);
        EditorSceneManager.MarkSceneDirty(wave.gameObject.scene);
        EditorSceneManager.SaveScene(wave.gameObject.scene);
        lastAudit = Audit();
    }

    private static string Audit()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("MS7F2 - SCENE STRUCTURAL AUDIT (READ ONLY)");
        sb.AppendLine("Unity=" + Application.unityVersion + " Scene=" + SceneManager.GetActiveScene().name + " PlayMode=" + EditorApplication.isPlaying);
        string reason;
        WaveManager wave = FindWave(out reason);
        if (wave == null) { sb.AppendLine("[ERROR] " + reason); return sb.ToString(); }
        sb.AppendLine("[PASS] " + reason);
        sb.AppendLine(wave.enemySpawner == null ? "[ERROR] EnemySpawner not assigned" : "[PASS] Existing EnemySpawner assigned");
        MS7F2ArenaEventDirector d = wave.GetComponent<MS7F2ArenaEventDirector>();
        if (d == null) { sb.AppendLine("[PENDING] Install 7F.2 director on WaveManager"); return sb.ToString(); }
        int c = 0;
        foreach (MS7F2ArenaEventDirector a in UnityEngine.Object.FindObjectsByType<MS7F2ArenaEventDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (a != null && a.gameObject.scene == wave.gameObject.scene) c++;
        sb.AppendLine(c == 1 ? "[PASS] Exactly one 7F.2 Director" : "[ERROR] 7F.2 Director count=" + c);
        sb.AppendLine(d.waveManager == wave && d.enemySpawner == wave.enemySpawner ?
            "[PASS] Original Wave/Spawner linked" : "[ERROR] Wrong references");
        MS7F1ArenaHazardDirector old = wave.GetComponent<MS7F1ArenaHazardDirector>();
        sb.AppendLine(old != null ? "[PASS] Existing 7F.1 Director present" : "[WARN] 7F.1 not installed - 7F.2 will still work independently");
        sb.AppendLine(d.legacy7F1 == old ? "[PASS] 7F.1 reference matches" : "[WARN] 7F.1 reference needs rebind (button 2)");
        sb.AppendLine("[INFO] OptIn=" + d.enableMultipleEvents + " SuspendLegacy=" + d.suspend7F1WhileActive +
            " ExcludeBoss=" + d.excludeBossWave + " (boss exclusion hard enforced)");
        sb.AppendLine("[INFO] Wave=" + wave.CurrentWave + " Boss=" + wave.bossWave + " Total=" + wave.totalWaves);
        sb.AppendLine("[INFO] 7F.1 serialized opt-in=" + (old != null ? old.enableArenaHazards.ToString() : "n/a"));
        sb.AppendLine(d.TryValidate(out reason) ? "[PASS] " + reason : "[WARN] " + reason);
        if (EditorApplication.isPlaying) sb.AppendLine(d.GetRuntimeReport());
        else sb.AppendLine("[PENDING] Run Waves 2,3,4,5 in Play Mode for real evidence");
        return sb.ToString();
    }

    private static void Check(bool yes, string desc, StringBuilder sb, ref int pass, ref int fail)
    { if (yes) { pass++; sb.AppendLine("[PASS] " + desc); } else { fail++; sb.AppendLine("[FAIL] " + desc); } }

    private static string PureTests()
    {
        int pass = 0, fail = 0;
        StringBuilder sb = new StringBuilder("MS7F2 - PURE RULE + GEOMETRY QA (NO GAME CHANGES)\n");
        Check(MS7F2ArenaEventDirector.Eligible(true, 2, 2, 5, 5, true, false, false), "Wave2 eligible", sb, ref pass, ref fail);
        Check(!MS7F2ArenaEventDirector.Eligible(true, 1, 2, 5, 5, true, false, false), "Wave1 excluded", sb, ref pass, ref fail);
        Check(!MS7F2ArenaEventDirector.Eligible(true, 5, 2, 5, 5, true, false, false), "Boss excluded", sb, ref pass, ref fail);
        Check(!MS7F2ArenaEventDirector.Eligible(false, 2, 2, 5, 5, true, false, false), "Opt-out excluded", sb, ref pass, ref fail);
        Check(!MS7F2ArenaEventDirector.Eligible(true, 2, 2, 5, 5, false, false, false), "Break excluded", sb, ref pass, ref fail);
        Check(!MS7F2ArenaEventDirector.Eligible(true, 2, 2, 5, 5, true, false, true), "Pause excluded", sb, ref pass, ref fail);
        Check(!MS7F2ArenaEventDirector.Eligible(true, 2, 2, 5, 5, true, true, false), "GameOver excluded", sb, ref pass, ref fail);
        Check(MS7F2ArenaEventDirector.ForWave(2, 2) == MS7F2ArenaEvent.EventKind.TwinStrike, "Wave2 TwinStrike", sb, ref pass, ref fail);
        Check(MS7F2ArenaEventDirector.ForWave(3, 2) == MS7F2ArenaEvent.EventKind.Crossfire, "Wave3 Crossfire", sb, ref pass, ref fail);
        Check(MS7F2ArenaEventDirector.ForWave(4, 2) == MS7F2ArenaEvent.EventKind.HollowRing, "Wave4 HollowRing", sb, ref pass, ref fail);
        Check(MS7F2ArenaEvent.IsDangerAt(MS7F2ArenaEvent.EventKind.TwinStrike, Vector2.zero, Vector2.zero, 0f, 1.08f, .84f, .72f, 2.8f, .7f, 2.05f), "Twin center threatened", sb, ref pass, ref fail);
        Check(!MS7F2ArenaEvent.IsDangerAt(MS7F2ArenaEvent.EventKind.TwinStrike, Vector2.right * 4f, Vector2.zero, 0f, 1.08f, .84f, .72f, 2.8f, .7f, 2.05f), "Twin outside safe", sb, ref pass, ref fail);
        Check(MS7F2ArenaEvent.IsDangerAt(MS7F2ArenaEvent.EventKind.Crossfire, Vector2.right * 2f, Vector2.zero, 0f, 1.08f, .84f, .72f, 2.8f, .7f, 2.05f), "Cross lane dangerous", sb, ref pass, ref fail);
        Check(!MS7F2ArenaEvent.IsDangerAt(MS7F2ArenaEvent.EventKind.Crossfire, new Vector2(2f, 2f), Vector2.zero, 0f, 1.08f, .84f, .72f, 2.8f, .7f, 2.05f), "Cross diagonal safe", sb, ref pass, ref fail);
        Check(!MS7F2ArenaEvent.IsDangerAt(MS7F2ArenaEvent.EventKind.HollowRing, Vector2.zero, Vector2.zero, 0f, 1.08f, .84f, .72f, 2.8f, .7f, 2.05f), "Ring center safe", sb, ref pass, ref fail);
        Check(MS7F2ArenaEvent.IsDangerAt(MS7F2ArenaEvent.EventKind.HollowRing, Vector2.right * 1.2f, Vector2.zero, 0f, 1.08f, .84f, .72f, 2.8f, .7f, 2.05f), "Ring danger zone", sb, ref pass, ref fail);
        Check(!MS7F2ArenaEvent.IsDangerAt(MS7F2ArenaEvent.EventKind.HollowRing, Vector2.right * 3f, Vector2.zero, 0f, 1.08f, .84f, .72f, 2.8f, .7f, 2.05f), "Ring outer safe", sb, ref pass, ref fail);
        sb.AppendLine("PURE RESULT PASS=" + pass + " FAIL=" + fail);
        return sb.ToString();
    }

    private int CountConfirmed()
    {
        int count = 0;
        for (int i = 0; i < confirmed.Length; i++) if (confirmed[i]) count++;
        return count;
    }

    private void Export()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 7F.2 - MULTIPLE ARENA EVENTS / FINAL QA");
        sb.AppendLine("Generated=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine(Audit());
        sb.AppendLine(PureTests());
        sb.AppendLine("=== MANUAL GAMEPLAY CONFIRMATIONS ===");
        for (int i = 0; i < manualTests.Length; i++)
            sb.AppendLine((confirmed[i] ? "[CONFIRMED] " : "[NOT CONFIRMED] ") + (i + 1).ToString("00") + " " + manualTests[i]);
        sb.AppendLine("MANUAL=" + CountConfirmed() + "/" + manualTests.Length);
        string why;
        WaveManager wave = FindWave(out why);
        MS7F2ArenaEventDirector d = wave != null ? wave.GetComponent<MS7F2ArenaEventDirector>() : null;
        bool playing = EditorApplication.isPlaying;
        bool completeRuntime = playing && d != null && d.TwinStarts > 0 &&
            d.CrossfireStarts > 0 && d.RingStarts > 0 && d.TotalImpacts >= 3 && d.BossSkips > 0 &&
            (d.legacy7F1 == null || d.LegacySuspensions > 0);
        sb.AppendLine("RUNTIME=" + (completeRuntime ? "OBSERVED" : "PENDING"));
        sb.AppendLine("MANUAL=" + (CountConfirmed() == manualTests.Length ? "CONFIRMED" : "PENDING"));
        sb.AppendLine("RESULT=" + (completeRuntime && CountConfirmed() == manualTests.Length ? "READY_FOR_USER_REVIEW" : "PENDING"));
        sb.AppendLine("NOTE: Logs cannot prove physical visuals, PlayerShield, wallet persistence or final game build.");
        string file = EditorUtility.SaveFilePanel("Export 7F.2 QA", Application.dataPath,
            "MS7F2_MultipleArenaQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"), "txt");
        if (string.IsNullOrEmpty(file)) return;
        File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
        lastAudit = sb.ToString();
        Debug.Log("[7F.2] Report saved: " + file);
    }
}
#endif
