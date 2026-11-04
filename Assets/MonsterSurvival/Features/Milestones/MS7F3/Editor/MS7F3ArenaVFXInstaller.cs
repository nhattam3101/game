#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 7F.3 installer and truthful QA. Edit-mode scene operations require explicit buttons.
/// Does not edit 7F.1/7F.2 scripts or event prefabs.
/// </summary>
public sealed class MS7F3ArenaVFXInstaller : EditorWindow
{
    private readonly string[] manualItems =
    {
        "Wave 2 Twin Strike: two clear glowing warnings, charge traces, then orange impact",
        "Wave 3 Crossfire: glow/charge outlines match the actual cross lanes",
        "Wave 4 Hollow Ring: central safe area remains visually clear",
        "The warning colors remain readable on each map and at 1280x720 / 1920x1080",
        "Standing outside the real 7F.2 geometry avoids HP damage; inside checks once",
        "Pause freezes all VFX animations and hazard timers; resume works",
        "Wave 5 Boss has no 7F.3 VFX; original boss HP bar/HUD unaffected",
        "Disabling 7F.3 removes ONLY added visuals; 7F.1 restore after disabling 7F.2 is verified",
        "Elite/Wave 7E, EXP/Gold, 7D Equipment, 7C Skills and Save/Profile unaffected"
    };
    [SerializeField] private bool[] checkedItems = new bool[9];
    private Vector2 scroll;
    private string lastAudit = "Run the read-only audit before installing.";

    [MenuItem("Tools/MonsterSurvival/7F Arena Events/10 - Open 7F.3 Environmental VFX")]
    private static void OpenWindow()
    {
        MS7F3ArenaVFXInstaller w = GetWindow<MS7F3ArenaVFXInstaller>("7F.3 Arena VFX");
        w.minSize = new Vector2(650, 540);
        w.Show();
    }

    private void OnGUI()
    {
        if (checkedItems == null || checkedItems.Length != manualItems.Length)
            checkedItems = new bool[manualItems.Length];
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("7F.3 — ENVIRONMENTAL VFX & FEEDBACK", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "7F.3 adds visual halo + progressive warning outline + impact flash to EXISTING 7F.2 events.\n" +
            "No changes to hit geometry, HP, Shield, Enemy, Wave, Boss or Save. Install OFF by default.\n" +
            "Keep 7F.2 Multiple Events enabled in the WaveManager Inspector for testing.",
            MessageType.Info);
        if (GUILayout.Button("1 - AUDIT SCENE + 7F.2 (READ ONLY)", GUILayout.Height(30)))
            { lastAudit = Audit(); Debug.Log(lastAudit); }
        if (GUILayout.Button("2 - BACKUP SCENE + INSTALL / REPAIR (OFF)", GUILayout.Height(30))) Install();
        if (GUILayout.Button("3 - BACKUP SCENE + ENABLE VFX", GUILayout.Height(28))) Toggle(true);
        if (GUILayout.Button("4 - BACKUP SCENE + DISABLE VFX", GUILayout.Height(28))) Toggle(false);
        if (GUILayout.Button("5 - PURE VISUAL MATH TESTS", GUILayout.Height(28)))
            { lastAudit = PureTests(); Debug.Log(lastAudit); }
        if (GUILayout.Button("6 - EXPORT 7F.3 QA REPORT (PLAY MODE)", GUILayout.Height(30))) Export();

        GUILayout.Space(9);
        EditorGUILayout.LabelField("MANUAL QA — ONLY TICK ITEMS YOU ACTUALLY TESTED", EditorStyles.boldLabel);
        for (int i = 0; i < manualItems.Length; i++)
            checkedItems[i] = EditorGUILayout.ToggleLeft((i + 1).ToString("00") + " — " + manualItems[i], checkedItems[i]);
        GUILayout.Space(8);
        EditorGUILayout.LabelField("Most recent audit", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(lastAudit, GUILayout.MinHeight(100));
        EditorGUILayout.EndScrollView();
    }

    private static WaveManager FindWave(out string issue)
    {
        WaveManager[] all = UnityEngine.Object.FindObjectsByType<WaveManager>(FindObjectsSortMode.None);
        if (all.Length != 1)
        {
            issue = "Expected exactly one active WaveManager in the loaded scene; found " + all.Length;
            return null;
        }
        issue = "WaveManager=" + all[0].name;
        return all[0];
    }

    private static bool TryReferences(out WaveManager wave, out MS7F2ArenaEventDirector source,
        out MS7F3ArenaVFXDirector vfx, out string issue)
    {
        wave = FindWave(out issue);
        source = null;
        vfx = null;
        if (wave == null) return false;
        source = wave.GetComponent<MS7F2ArenaEventDirector>();
        if (source == null)
        {
            issue = "7F.2 MS7F2ArenaEventDirector missing on WaveManager";
            return false;
        }
        MS7F3ArenaVFXDirector[] directors = wave.GetComponents<MS7F3ArenaVFXDirector>();
        if (directors.Length > 1)
        {
            issue = "Duplicate 7F.3 VFX directors on WaveManager";
            return false;
        }
        vfx = directors.Length == 1 ? directors[0] : null;
        if (source.waveManager != wave || source.enemySpawner == null ||
            source.enemySpawner != wave.enemySpawner)
        {
            issue = "7F.2 WaveManager/EnemySpawner references do not match";
            return false;
        }
        issue = "Exactly one WaveManager, verified 7F.2 and original EnemySpawner";
        return true;
    }

    private static string Audit()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("MS7F3 - READ ONLY SCENE AUDIT");
        sb.AppendLine("Unity=" + Application.unityVersion + " | Scene=" + SceneManager.GetActiveScene().name +
            " | PlayMode=" + EditorApplication.isPlaying);
        WaveManager wave;
        MS7F2ArenaEventDirector source;
        MS7F3ArenaVFXDirector vfx;
        string issue;
        if (!TryReferences(out wave, out source, out vfx, out issue))
        { sb.AppendLine("[ERROR] " + issue); return sb.ToString(); }
        sb.AppendLine("[PASS] " + issue);
        sb.AppendLine("[PASS] 7F.2 Twin/Crossfire/Hollow Ring source is present");
        sb.AppendLine("[INFO] 7F.2 enabled=" + source.enableMultipleEvents +
            " bossWave=" + wave.bossWave + " 7F.2 excludeBossWave=" + source.excludeBossWave);
        MS7F1ArenaHazardDirector legacy = wave.GetComponent<MS7F1ArenaHazardDirector>();
        sb.AppendLine("[INFO] 7F.1 present=" + (legacy != null) +
            " old optIn=" + (legacy != null ? legacy.enableArenaHazards.ToString() : "n/a"));
        if (vfx == null) sb.AppendLine("[PENDING] 7F.3 not installed; use button 2");
        else
        {
            sb.AppendLine(vfx.arenaEvents == source ? "[PASS] 7F.3 -> exact existing 7F.2 director" :
                "[WARN] 7F.3 reference mismatched: use INSTALL / REPAIR in Edit Mode");
            sb.AppendLine("[INFO] 7F.3 enableArenaVFX=" + vfx.enableArenaVFX);
            if (EditorApplication.isPlaying) sb.AppendLine(vfx.GetRuntimeReport());
            else sb.AppendLine("[PENDING] Play Mode needed to verify visuals");
        }
        sb.AppendLine("[INFO] 7F.2 Legacy7F1Restores requires separate manual toggle test.");
        return sb.ToString();
    }

    private static bool BackupScene(out string backupPath)
    {
        backupPath = "";
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorUtility.DisplayDialog("7F.3", "Exit Play Mode before modifying the Scene.", "OK"); return false; }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        { EditorUtility.DisplayDialog("7F.3", "Save the Scene as an asset before installing.", "OK"); return false; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        string root = Path.GetDirectoryName(Application.dataPath);
        string original = Path.Combine(root, scene.path);
        if (!File.Exists(original))
        { EditorUtility.DisplayDialog("7F.3", "Cannot find the source scene on disk: " + original, "OK"); return false; }
        string backupDir = Path.Combine(root, "MS_Backups", "7F3");
        Directory.CreateDirectory(backupDir);
        string filename = Path.GetFileNameWithoutExtension(scene.path) + "_7F3_" +
            DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + Path.GetExtension(scene.path);
        backupPath = Path.Combine(backupDir, filename);
        File.Copy(original, backupPath, false);
        Debug.Log("[7F.3] Backup created outside Assets: " + backupPath);
        return true;
    }

    private static void Install()
    {
        WaveManager wave;
        MS7F2ArenaEventDirector source;
        MS7F3ArenaVFXDirector vfx;
        string issue;
        if (!TryReferences(out wave, out source, out vfx, out issue))
        { EditorUtility.DisplayDialog("7F.3 - Audit failed", issue, "OK"); return; }
        if (!EditorUtility.DisplayDialog("7F.3 - Install", "Back up the current Scene, then attach one VFX director to the EXISTING WaveManager? This will not enable VFX automatically.", "Install", "Cancel")) return;
        string backup;
        if (!BackupScene(out backup)) return;
        if (vfx == null)
        {
            vfx = Undo.AddComponent<MS7F3ArenaVFXDirector>(wave.gameObject);
            vfx.enableArenaVFX = false;
        }
        Undo.RecordObject(vfx, "Bind 7F.3 VFX Director");
        vfx.arenaEvents = source;
        EditorUtility.SetDirty(vfx);
        EditorSceneManager.MarkSceneDirty(wave.gameObject.scene);
        EditorSceneManager.SaveScene(wave.gameObject.scene);
        Debug.Log("[7F.3] Installed / repaired. VFX enabled=" + vfx.enableArenaVFX + " | Backup=" + backup);
    }

    private static void Toggle(bool nextValue)
    {
        WaveManager wave;
        MS7F2ArenaEventDirector source;
        MS7F3ArenaVFXDirector vfx;
        string issue;
        if (!TryReferences(out wave, out source, out vfx, out issue))
        { EditorUtility.DisplayDialog("7F.3", issue, "OK"); return; }
        if (vfx == null)
        { EditorUtility.DisplayDialog("7F.3", "Install 7F.3 first (button 2).", "OK"); return; }
        if (nextValue && !source.enableMultipleEvents)
        { EditorUtility.DisplayDialog("7F.3", "7F.2 Multiple Events is OFF. Enable 7F.2 first, then enable 7F.3 VFX.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("7F.3 - Confirm", (nextValue ? "ENABLE" : "DISABLE") +
            " VFX on the current Scene? This only changes one 7F.3 bool field.", "Confirm", "Cancel")) return;
        string backup;
        if (!BackupScene(out backup)) return;
        Undo.RecordObject(vfx, "7F.3 VFX Opt-in");
        vfx.arenaEvents = source;
        vfx.enableArenaVFX = nextValue;
        EditorUtility.SetDirty(vfx);
        EditorSceneManager.MarkSceneDirty(wave.gameObject.scene);
        EditorSceneManager.SaveScene(wave.gameObject.scene);
        Debug.Log("[7F.3] enableArenaVFX=" + nextValue + " | Backup=" + backup);
    }

    private static void Check(bool value, string description, StringBuilder sb, ref int pass, ref int fail)
    { if (value) { pass++; sb.AppendLine("[PASS] " + description); }
      else { fail++; sb.AppendLine("[FAIL] " + description); } }

    private static string PureTests()
    {
        int pass = 0, fail = 0;
        StringBuilder sb = new StringBuilder("MS7F3 - PURE DISPLAY MATH QA (NO SCENE CHANGES)\n");
        Color warn = new Color(0.6f, 0.35f, 1f, 0.8f);
        Color active = new Color(1f, 0.5f, 0.2f, 0.9f);
        Check(MS7F3ArenaEventFX.LooksLikeImpact(active, warn, active),
            "Detect active tint", sb, ref pass, ref fail);
        Check(!MS7F3ArenaEventFX.LooksLikeImpact(warn, warn, active),
            "Warning tint is not impact", sb, ref pass, ref fail);
        Check(!MS7F3ArenaEventFX.LooksLikeImpact(warn, warn, warn),
            "Same-color case is not misclassified", sb, ref pass, ref fail);
        Vector3[] rect = { new Vector3(0f, 0f, 0f), new Vector3(2f, 0f, 0f),
                           new Vector3(2f, 2f, 0f), new Vector3(0f, 2f, 0f) };
        Check(Vector3.Distance(MS7F3ArenaEventFX.SampleClosedPolyline(rect, 0f), rect[0]) < .001f,
            "Closed perimeter start", sb, ref pass, ref fail);
        Check(Vector3.Distance(MS7F3ArenaEventFX.SampleClosedPolyline(rect, 0.25f), rect[1]) < .001f,
            "Closed perimeter first quarter", sb, ref pass, ref fail);
        Check(Vector3.Distance(MS7F3ArenaEventFX.SampleClosedPolyline(rect, 0.5f), rect[2]) < .001f,
            "Closed perimeter middle", sb, ref pass, ref fail);
        Check(Vector3.Distance(MS7F3ArenaEventFX.SampleClosedPolyline(rect, .75f), rect[3]) < .001f,
            "Closed perimeter third quarter", sb, ref pass, ref fail);
        Check(MS7F2ArenaEventDirector.ForWave(2, 2) == MS7F2ArenaEvent.EventKind.TwinStrike,
            "7F.2 wave 2 unchanged", sb, ref pass, ref fail);
        Check(MS7F2ArenaEventDirector.ForWave(3, 2) == MS7F2ArenaEvent.EventKind.Crossfire,
            "7F.2 wave 3 unchanged", sb, ref pass, ref fail);
        Check(MS7F2ArenaEventDirector.ForWave(4, 2) == MS7F2ArenaEvent.EventKind.HollowRing,
            "7F.2 wave 4 unchanged", sb, ref pass, ref fail);
        Check(!MS7F2ArenaEventDirector.Eligible(true, 5, 2, 5, 5, true, false, false),
            "7F.2 still excludes Boss Wave", sb, ref pass, ref fail);
        sb.AppendLine("PURE RESULT PASS=" + pass + " FAIL=" + fail);
        sb.AppendLine("NOTE: Pure tests cannot prove Game View visuals or rendering API compatibility.");
        return sb.ToString();
    }

    private int ManualCount()
    {
        int n = 0;
        for (int i = 0; i < checkedItems.Length; i++) if (checkedItems[i]) n++;
        return n;
    }

    private void Export()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 7F.3 — ENVIRONMENTAL VFX FINAL QA (READ ONLY)");
        sb.AppendLine("Generated=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine(Audit());
        sb.AppendLine(PureTests());
        WaveManager wave;
        MS7F2ArenaEventDirector source;
        MS7F3ArenaVFXDirector vfx;
        string issue;
        bool valid = TryReferences(out wave, out source, out vfx, out issue);
        bool runtime = EditorApplication.isPlaying && valid && vfx != null &&
            vfx.TwinVisuals > 0 && vfx.CrossfireVisuals > 0 && vfx.RingVisuals > 0 &&
            vfx.ImpactVisuals >= 3 && source.BossSkips > 0;
        sb.AppendLine("=== MANUAL GAMEPLAY QA ===");
        for (int i = 0; i < manualItems.Length; i++)
            sb.AppendLine((checkedItems[i] ? "[CONFIRMED] " : "[NOT CONFIRMED] ") +
                (i + 1).ToString("00") + " " + manualItems[i]);
        sb.AppendLine("MANUAL=" + ManualCount() + "/" + manualItems.Length);
        sb.AppendLine("RUNTIME=" + (runtime ? "OBSERVED" : "PENDING"));
        sb.AppendLine("MANUAL_STATUS=" + (ManualCount() == manualItems.Length ? "CONFIRMED" : "PENDING"));
        sb.AppendLine("RESULT=" + (runtime && ManualCount() == manualItems.Length ?
            "READY_FOR_USER_REVIEW" : "PENDING"));
        sb.AppendLine("NOTE: VFX instrumentation does not establish game build success, actual visual quality or Save compatibility.");
        string file = EditorUtility.SaveFilePanel("Export 7F.3 QA", Application.dataPath,
            "MS7F3_EnvironmentalVFXQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"), "txt");
        if (string.IsNullOrEmpty(file)) return;
        File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
        lastAudit = sb.ToString();
        Debug.Log("[7F.3] QA saved: " + file);
    }
}
#endif
