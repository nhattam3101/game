#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>7F.1 opt-in installer and read-only audits. Requires no modifications to legacy scripts.</summary>
public sealed class MS7F1ArenaHazardInstaller : EditorWindow
{
    private readonly string[] manualTests =
    {
        "Warning ring visible in Wave 2; player can dodge before activation",
        "Player in danger radius loses HP OR existing shield blocks the impact",
        "Leaving circle avoids HP damage",
        "Wave 3 and Wave 4 show hazards with reasonable density",
        "Wave 5 Boss (including follow HP bar) unchanged; no arena hazards",
        "Pause freezes warning and impact; GameOver/Retry work",
        "Enemy spawning, Elite 7E, rewards, and skill synergies stay correct",
        "Save/Profile, Equipment 7D, Gold on Victory/GameOver unchanged"
    };
    private bool[] confirmed = new bool[8];
    private Vector2 scroll;
    private string lastAudit = "Run PRE-FLIGHT to check current SampleScene.";

    [MenuItem("Tools/MonsterSurvival/7F Arena Events/1 - Open 7F.1 Hazard Installer")]
    private static void Open() { GetWindow<MS7F1ArenaHazardInstaller>(false, "7F.1 Arena Hazards").minSize = new Vector2(570f, 510f); }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        GUILayout.Label("7F.1 - TELEGRAPHED ARENA HAZARDS", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Add-on for WaveManager. Starts OFF. No edits to old scripts or Save.\n7E.5 was PENDING (surge + manual QA); keep that backlog open.", MessageType.Info);

        if (GUILayout.Button("1 - PRE-FLIGHT / AUDIT (READ ONLY)", GUILayout.Height(31)))
        {
            lastAudit = Audit();
            Debug.Log(lastAudit);
        }
        if (GUILayout.Button("2 - BACKUP SCENE + INSTALL / REPAIR (DEFAULT OFF)", GUILayout.Height(31)))
            Install();
        if (GUILayout.Button("3 - BACKUP SCENE + ENABLE TEST HAZARDS (OPT-IN)", GUILayout.Height(31)))
            ChangeOptIn(true);
        if (GUILayout.Button("4 - DISABLE HAZARDS (RESTORE NORMAL GAMEPLAY)", GUILayout.Height(27)))
            ChangeOptIn(false);
        if (GUILayout.Button("5 - PURE RULE TESTS (NO GAME STATE CHANGES)", GUILayout.Height(27)))
        {
            lastAudit = PureTests();
            Debug.Log(lastAudit);
        }
        if (GUILayout.Button("6 - EXPORT 7F.1 QA REPORT TXT", GUILayout.Height(30)))
            Export();

        GUILayout.Space(8);
        GUILayout.Label("MANUAL GAMEPLAY QA - check ONLY what you observed", EditorStyles.boldLabel);
        for (int i = 0; i < confirmed.Length; i++)
            confirmed[i] = EditorGUILayout.ToggleLeft((i + 1).ToString("00") + "  " + manualTests[i], confirmed[i]);
        GUILayout.Space(5);
        EditorGUILayout.LabelField("Confirmed", CountConfirmed() + "/" + confirmed.Length);

        GUILayout.Space(8);
        GUILayout.Label("Latest audit", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(lastAudit, GUILayout.MinHeight(155));
        EditorGUILayout.EndScrollView();
    }

    private static WaveManager FindWave(out string reason)
    {
        Scene scene = SceneManager.GetActiveScene();
        WaveManager[] all = UnityEngine.Object.FindObjectsByType<WaveManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        WaveManager result = null;
        int count = 0;
        foreach (WaveManager w in all)
        {
            if (w != null && w.gameObject.scene == scene) { count++; result = w; }
        }
        if (count != 1) { reason = "Expected ONE WaveManager in active scene; found " + count; return null; }
        reason = "WaveManager=" + result.gameObject.name;
        return result;
    }

    private static MS7F1ArenaHazardDirector FindDirector(WaveManager w)
    {
        return w != null ? w.GetComponent<MS7F1ArenaHazardDirector>() : null;
    }

    private static bool CanEditScene(out WaveManager wave)
    {
        wave = null;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("7F.1", "Stop Play Mode before installing or changing Scene values.", "OK");
            return false;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog("7F.1", "Save the current SampleScene as a .unity asset first.", "OK");
            return false;
        }
        string reason;
        wave = FindWave(out reason);
        if (wave == null)
        {
            EditorUtility.DisplayDialog("7F.1", reason, "OK");
            return false;
        }
        return true;
    }

    private static bool BackupCurrentScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!EditorSceneManager.SaveScene(scene))
        {
            Debug.LogError("[7F.1] Failed to save Scene before backup. Aborting.");
            return false;
        }
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string folder = Path.Combine(projectRoot, "MS_Backups", "7F1");
        Directory.CreateDirectory(folder);
        string fileName = Path.GetFileNameWithoutExtension(scene.path) + "_7F1_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity";
        string backup = Path.Combine(folder, fileName);
        try
        {
            File.Copy(Path.Combine(projectRoot, scene.path), backup, false);
        }
        catch (Exception e)
        {
            Debug.LogError("[7F.1] Scene backup failed: " + backup + " / " + e.Message);
            return false;
        }
        Debug.Log("[7F.1] Scene backup: " + backup);
        return true;
    }

    private void Install()
    {
        WaveManager w;
        if (!CanEditScene(out w)) return;
        if (w.enemySpawner == null)
        {
            EditorUtility.DisplayDialog("7F.1", "WaveManager.enemySpawner is missing. Fix the existing reference first.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("7F.1 Installer", "Back up the Scene and add/repair the 7F.1 component on the EXISTING WaveManager? New installs start OFF.", "Install", "Cancel")) return;
        if (!BackupCurrentScene()) return;
        MS7F1ArenaHazardDirector d = FindDirector(w);
        bool isNew = d == null;
        if (isNew) d = Undo.AddComponent<MS7F1ArenaHazardDirector>(w.gameObject);
        Undo.RecordObject(d, "7F1 Reference Binding");
        d.waveManager = w;
        d.enemySpawner = w.enemySpawner;
        if (isNew) d.enableArenaHazards = false;
        EditorUtility.SetDirty(d);
        EditorSceneManager.MarkSceneDirty(w.gameObject.scene);
        EditorSceneManager.SaveScene(w.gameObject.scene);
        Selection.activeObject = w.gameObject;
        lastAudit = Audit();
        Debug.Log("[7F.1] " + (isNew ? "Installed" : "Repaired") + " component. OptIn=" + d.enableArenaHazards);
    }

    private void ChangeOptIn(bool value)
    {
        WaveManager w;
        if (!CanEditScene(out w)) return;
        MS7F1ArenaHazardDirector d = FindDirector(w);
        if (d == null)
        {
            EditorUtility.DisplayDialog("7F.1", "Install 7F.1 first.", "OK");
            return;
        }
        if (value)
        {
            string reason;
            if (!d.TryValidate(out reason))
            {
                EditorUtility.DisplayDialog("7F.1 Preflight failed", reason + "\nNo changes made.", "OK");
                return;
            }
        }
        string message = value ? "This will ENABLE telegraphed player hazards in non-boss waves. Damage is 8 by default. Back up first?" :
            "This will DISABLE 7F.1 hazards (other systems unchanged). Back up first?";
        if (!EditorUtility.DisplayDialog("7F.1 Explicit Opt-in", message, value ? "Enable" : "Disable", "Cancel")) return;
        if (!BackupCurrentScene()) return;
        Undo.RecordObject(d, "7F1 Change Opt-in");
        d.enableArenaHazards = value;
        EditorUtility.SetDirty(d);
        EditorSceneManager.MarkSceneDirty(w.gameObject.scene);
        EditorSceneManager.SaveScene(w.gameObject.scene);
        lastAudit = Audit();
        Debug.Log("[7F.1] enableArenaHazards=" + value);
    }

    private string Audit()
    {
        StringBuilder s = new StringBuilder();
        Scene scene = SceneManager.GetActiveScene();
        s.AppendLine("MS 7F.1 SCENE AUDIT (READ ONLY)");
        s.AppendLine("Unity=" + Application.unityVersion + " | Scene=" + scene.name + " | PlayMode=" + EditorApplication.isPlaying);
        string why;
        WaveManager wave = FindWave(out why);
        if (wave == null) { s.AppendLine("[ERROR] " + why); return s.ToString(); }
        s.AppendLine("[PASS] " + why);
        s.AppendLine(wave.enemySpawner != null ? "[PASS] Existing EnemySpawner assigned" : "[ERROR] Missing EnemySpawner");
        MS7F1ArenaHazardDirector[] all = UnityEngine.Object.FindObjectsByType<MS7F1ArenaHazardDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int count = 0;
        foreach (MS7F1ArenaHazardDirector d0 in all)
            if (d0 != null && d0.gameObject.scene == scene) count++;
        s.AppendLine(count == 1 ? "[PASS] Exactly one 7F.1 Director" : "[WARN] Found 7F.1 Directors=" + count);
        MS7F1ArenaHazardDirector d = FindDirector(wave);
        if (d != null)
        {
            s.AppendLine(d.waveManager == wave && d.enemySpawner == wave.enemySpawner ?
                "[PASS] 7F.1 references match WaveManager" : "[ERROR] 7F.1 references inconsistent");
            string reason;
            s.AppendLine(d.TryValidate(out reason) ? "[PASS] " + reason : "[WARN] " + reason);
            s.AppendLine("[INFO] OptIn=" + d.enableArenaHazards + " | FirstWave=" + d.firstAffectedWave +
                " | ExcludeBoss=" + d.excludeBossWave + " | Warn=" + d.warningSeconds.ToString("F2") +
                " | Damage=" + d.damage.ToString("F1"));
            if (EditorApplication.isPlaying)
                s.AppendLine(d.GetRuntimeReport());
            else s.AppendLine("[PENDING] Play Mode evidence required. Runtime counters reset each Play session.");
        }
        else s.AppendLine("[PENDING] Install the 7F.1 director on WaveManager");
        s.AppendLine("[INFO] 7E.5 prior release gate still PENDING; this add-on does not close it.");
        return s.ToString();
    }

    private static string PureTests()
    {
        int pass = 0, fail = 0;
        StringBuilder sb = new StringBuilder("MS7F1 PURE RULE QA (no Scene modifications)\n");
        Check(!MS7F1ArenaHazardDirector.IsEligible(true, 1, 2, 5, 5, true, true, false, false), "Wave1 excluded", sb, ref pass, ref fail);
        Check(MS7F1ArenaHazardDirector.IsEligible(true, 2, 2, 5, 5, true, true, false, false), "Wave2 eligible", sb, ref pass, ref fail);
        Check(!MS7F1ArenaHazardDirector.IsEligible(true, 5, 2, 5, 5, true, true, false, false), "Boss wave excluded", sb, ref pass, ref fail);
        Check(!MS7F1ArenaHazardDirector.IsEligible(false, 2, 2, 5, 5, true, true, false, false), "Opt-out no hazards", sb, ref pass, ref fail);
        Check(!MS7F1ArenaHazardDirector.IsEligible(true, 2, 2, 5, 5, true, false, false, false), "Break no hazards", sb, ref pass, ref fail);
        Check(!MS7F1ArenaHazardDirector.IsEligible(true, 2, 2, 5, 5, true, true, true, false), "Game over no hazards", sb, ref pass, ref fail);
        Check(!MS7F1ArenaHazardDirector.IsEligible(true, 2, 2, 5, 5, true, true, false, true), "Pause no hazards", sb, ref pass, ref fail);
        Check(MS7F1ArenaHazardDirector.WithinCircle(Vector2.zero, Vector2.right, 1f), "Circle edge included", sb, ref pass, ref fail);
        Check(!MS7F1ArenaHazardDirector.WithinCircle(Vector2.zero, Vector2.right * 2f, 1f), "Outside safe", sb, ref pass, ref fail);
        sb.AppendLine("PURE RESULT PASS=" + pass + " FAIL=" + fail);
        return sb.ToString();
    }

    private static void Check(bool condition, string name, StringBuilder sb, ref int pass, ref int fail)
    {
        if (condition) { pass++; sb.AppendLine("[PASS] " + name); }
        else { fail++; sb.AppendLine("[FAIL] " + name); }
    }

    private int CountConfirmed()
    {
        int c = 0; for (int i = 0; i < confirmed.Length; i++) if (confirmed[i]) c++;
        return c;
    }

    private void Export()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 7F.1 - ARENA HAZARDS QA");
        sb.AppendLine("Generated=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine(Audit());
        sb.AppendLine(PureTests());
        sb.AppendLine("=== MANUAL TESTS ===");
        for (int i = 0; i < confirmed.Length; i++)
            sb.AppendLine((confirmed[i] ? "[CONFIRMED] " : "[NOT CONFIRMED] ") + (i + 1).ToString("00") + " " + manualTests[i]);
        sb.AppendLine("MANUAL=" + CountConfirmed() + "/" + confirmed.Length);
        string reason;
        WaveManager wave = FindWave(out reason);
        MS7F1ArenaHazardDirector d = FindDirector(wave);
        bool observed = d != null && EditorApplication.isPlaying && d.SpawnedTotal > 0 &&
                        d.ActivationsTotal > 0 && d.BossWaveSkips > 0 &&
                        d.SawWave2 && d.SawWave3 && d.SawWave4;
        sb.AppendLine("RUNTIME_EVIDENCE=" + (observed ? "OBSERVED" : "PENDING"));
        sb.AppendLine("MANUAL_EVIDENCE=" + (CountConfirmed() == confirmed.Length ? "CONFIRMED" : "PENDING"));
        sb.AppendLine("RESULT=" + (observed && CountConfirmed() == confirmed.Length ? "READY_FOR_USER_REVIEW" : "PENDING"));
        sb.AppendLine("NOTE: This QA cannot prove physical visibility, player actions, balance or persistence without manual tests.");

        string path = EditorUtility.SaveFilePanel("Save 7F.1 QA TXT", Application.dataPath,
            "MS7F1_ArenaHazardQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"), "txt");
        if (string.IsNullOrEmpty(path)) return;
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        Debug.Log("[7F.1] QA exported: " + path);
        lastAudit = sb.ToString();
    }
}
#endif
