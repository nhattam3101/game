#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Safe, idempotent installer / read-only QA for MS7E3EliteFeedback.
/// Only changes the pre-existing Elite prefab selected by EliteSpawnDirector.
/// </summary>
public sealed class MS7E3EliteFeedbackInstaller : EditorWindow
{
    [MenuItem("Tools/MonsterSurvival/7E Elite Enemies/4 - Open 7E.3 Combat VFX & QA")]
    public static void Open()
    {
        MS7E3EliteFeedbackInstaller window = GetWindow<MS7E3EliteFeedbackInstaller>("7E.3 Elite VFX");
        window.minSize = new Vector2(620f, 480f);
    }

    private readonly string[] manualNames = new string[]
    {
        "Bulwark, Swift, Regenerator visuals checked in Play Mode",
        "Hit flash visible when an Elite loses HP",
        "Enrage pulse visible near existing EliteEnemy threshold",
        "Death burst occurs and original EXP/Gold rewards work",
        "Pause/Retry/GameOver unaffected; no extra gameplay HUD",
        "Wave 5 Boss behavior and Wave 7E.2 unchanged",
        "7D set bonuses, Inventory and Save/Profile regression checked"
    };

    private readonly bool[] manual = new bool[7];
    private Vector2 scroll;
    private string status = "Ready. Audit the original Elite prefab first.";

    private void OnGUI()
    {
        EditorGUILayout.LabelField("MONSTERSURVIVAL — 7E.3 ELITE COMBAT VFX & FEEDBACK", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "VISUAL ONLY: Affix ring, hit flash, Enrage pulse and death sparkle. " +
            "7E.1 Affix/Enrage, EliteSpawnDirector, Health, EnemyController, drops, Wave and Save remain untouched.",
            MessageType.Info);
        EditorGUILayout.Space(5f);

        if (GUILayout.Button("1 — AUDIT EXISTING ELITE PREFAB (READ ONLY)", GUILayout.Height(29f)))
            status = BuildAudit();

        GUI.enabled = !EditorApplication.isPlaying;
        if (GUILayout.Button("2 — BACKUP + INSTALL ON EXISTING ELITE PREFAB", GUILayout.Height(33f)))
            Install();
        GUI.enabled = true;

        if (GUILayout.Button("3 — EXPORT 7E.3 QA REPORT TXT", GUILayout.Height(30f)))
            ExportReport();

        EditorGUILayout.Space(7f);
        EditorGUILayout.LabelField("MANUAL PLAY MODE CHECKS — check only after testing", EditorStyles.boldLabel);
        for (int i = 0; i < manualNames.Length; i++)
            manual[i] = EditorGUILayout.ToggleLeft((i + 1) + ". " + manualNames[i], manual[i]);

        EditorGUILayout.Space(7f);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(status, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private static bool TryPrefab(out GameObject prefab, out string path, out string why)
    {
        prefab = null;
        path = string.Empty;
        why = string.Empty;
        Scene active = EditorSceneManager.GetActiveScene();
        if (!active.IsValid() || !active.isLoaded)
        {
            why = "Open a valid, loaded scene with EliteSpawnDirector.";
            return false;
        }
        EliteSpawnDirector[] all = UnityEngine.Object.FindObjectsByType<EliteSpawnDirector>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        EliteSpawnDirector director = null;
        int count = 0;
        foreach (EliteSpawnDirector item in all)
        {
            if (item != null && item.gameObject.scene == active)
            {
                director = item;
                count++;
            }
        }
        if (count != 1)
        {
            why = "Expected exactly one EliteSpawnDirector in active scene; found " + count + ".";
            return false;
        }
        prefab = director.elitePrefab;
        if (prefab == null)
        {
            why = "Existing EliteSpawnDirector.elitePrefab is not assigned.";
            return false;
        }
        path = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
            !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            why = "Selected Elite object is not a writable prefab asset: " + path;
            return false;
        }
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null || asset.GetComponent<Health>() == null ||
            asset.GetComponent<EnemyController>() == null ||
            asset.GetComponent<EliteEnemy>() == null ||
            asset.GetComponent<MS7E1EliteAffix>() == null)
        {
            why = "Prefab must already contain Health, EnemyController, EliteEnemy and MS7E1EliteAffix (7E.1).";
            return false;
        }
        if (asset.GetComponentsInChildren<SpriteRenderer>(true).Length == 0)
        {
            why = "Elite prefab has no SpriteRenderer; hit flash would not be visible. Add sprite first.";
            return false;
        }
        prefab = asset;
        why = "OK: Scene=" + active.name + " | EliteSpawnDirector=1 | Prefab=" + path;
        return true;
    }

    private string BuildAudit()
    {
        StringBuilder log = new StringBuilder();
        log.AppendLine("MONSTERSURVIVAL 7E.3 - ELITE COMBAT VFX QA (READ ONLY)");
        log.AppendLine("Unity=" + Application.unityVersion + " | Scene=" + EditorSceneManager.GetActiveScene().name +
                       " | PlayMode=" + EditorApplication.isPlaying + " | Time=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        GameObject prefab;
        string path;
        string why;
        bool structural = TryPrefab(out prefab, out path, out why);
        if (!structural)
        {
            log.AppendLine("[WARN] " + why);
        }
        else
        {
            log.AppendLine("[PASS] " + why);
            int attached = prefab.GetComponents<MS7E3EliteFeedback>().Length;
            log.AppendLine(attached == 1
                ? "[PASS] Exactly one MS7E3EliteFeedback on EXISTING Elite prefab"
                : "[WARN] MS7E3EliteFeedback count=" + attached + " (run Installer if 0)");
            log.AppendLine("[PASS] Existing 7E.1 Affix, EliteEnemy Enrage, Health and EnemyController intact");
            log.AppendLine("[PASS] Elite prefab SpriteRenderer(s) exist");
        }
        int seen = 0, ready = 0, liveEnrage = 0, localHits = 0;
        MS7E3EliteFeedback[] active = UnityEngine.Object.FindObjectsByType<MS7E3EliteFeedback>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (MS7E3EliteFeedback item in active)
        {
            if (item == null || !item.gameObject.scene.IsValid()) continue;
            seen++;
            if (item.Ready) ready++;
            if (item.EnrageVisualActive) liveEnrage++;
            localHits += item.LocalHitFlashes;
        }

        log.AppendLine("=== LIVE EVENTS (counts reset each Play session) ===");
        log.AppendLine("Active VFX components=" + seen + " | initialized=" + ready +
                       " | currently enrage=" + liveEnrage + " | live components hit count=" + localHits);
        log.AppendLine("Total visuals initialized=" + MS7E3EliteFeedback.SpawnVisuals +
                       " | hit flashes=" + MS7E3EliteFeedback.HitFlashes +
                       " | enrage warnings=" + MS7E3EliteFeedback.EnrageWarnings +
                       " | death bursts=" + MS7E3EliteFeedback.DeathBursts);
        log.AppendLine("=== RECENT EVENTS ===");
        log.AppendLine(EditorApplication.isPlaying ? MS7E3EliteFeedback.GetRecentTrace() :
                       "(Edit Mode has no runtime evidence. Export during Play Mode for counts.)");

        int confirmed = 0;
        log.AppendLine("=== MANUAL CONFIRMATIONS ===");
        for (int i = 0; i < manual.Length; i++)
        {
            if (manual[i]) confirmed++;
            log.AppendLine((manual[i] ? "[CONFIRMED] " : "[NOT CONFIRMED] ") +
                           (i + 1).ToString("00") + " | " + manualNames[i]);
        }
        log.AppendLine("MANUAL=" + confirmed + "/" + manual.Length);

        bool eventsSeen = EditorApplication.isPlaying &&
                          MS7E3EliteFeedback.SpawnVisuals > 0 &&
                          MS7E3EliteFeedback.HitFlashes > 0 &&
                          MS7E3EliteFeedback.EnrageWarnings > 0 &&
                          MS7E3EliteFeedback.DeathBursts > 0;
        log.AppendLine("LIVE_FEEDBACK_EVIDENCE=" + (eventsSeen ? "OBSERVED" : "PENDING"));
        log.AppendLine("RESULT=" + (structural && prefab.GetComponent<MS7E3EliteFeedback>() != null &&
                    eventsSeen && confirmed == manual.Length ? "READY_FOR_USER_REVIEW" : "PENDING") +
                    " (do not claim gameplay PASS from Editor audit alone)");
        return log.ToString();
    }

    private void Install()
    {
        GameObject prefab;
        string path;
        string reason;
        if (!TryPrefab(out prefab, out path, out reason))
        {
            status = "[STOP] " + reason;
            return;
        }
        if (prefab.GetComponent<MS7E3EliteFeedback>() != null)
        {
            status = "[PASS] Already installed; no duplicate created.\n" + BuildAudit();
            return;
        }
        if (!EditorUtility.DisplayDialog("7E.3 SAFE INSTALL",
            "Add one visual-only component to existing Elite prefab?\n\n" + path +
            "\n\nOriginal prefab is backed up to MS_Backups/7E3/. " +
            "No Wave, Boss, Save, Player, 7E.1, EXP/Gold or HUD files will be changed.",
            "BACKUP + INSTALL", "CANCEL"))
            return;

        GameObject editable = null;
        string backup = string.Empty;
        try
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string original = Path.GetFullPath(Path.Combine(projectRoot, path));
            string backupFolder = Path.Combine(projectRoot, "MS_Backups", "7E3");
            Directory.CreateDirectory(backupFolder);
            backup = Path.Combine(backupFolder, Path.GetFileNameWithoutExtension(path) +
                     "_before_7E3_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".prefab");
            File.Copy(original, backup, false);
            editable = PrefabUtility.LoadPrefabContents(path);
            if (editable.GetComponent<MS7E3EliteFeedback>() == null)
                editable.AddComponent<MS7E3EliteFeedback>();
            GameObject result = PrefabUtility.SaveAsPrefabAsset(editable, path);
            if (result == null) throw new InvalidOperationException("Prefab SaveAsPrefabAsset returned null.");
            AssetDatabase.SaveAssets();
            status = "[PASS] Added MS7E3EliteFeedback to the existing Elite prefab.\n" +
                     "BACKUP=" + backup + "\n" + BuildAudit();
        }
        catch (Exception ex)
        {
            status = "[ERROR] Install failed; backup path (if made): " + backup + "\n" + ex;
            Debug.LogException(ex);
        }
        finally
        {
            if (editable != null) PrefabUtility.UnloadPrefabContents(editable);
        }
    }

    private void ExportReport()
    {
        status = BuildAudit();
        string fileName = "MS7E3_EliteCombatVFXQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string path = EditorUtility.SaveFilePanel("7E.3 Export read-only QA TXT", root, fileName, "txt");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            File.WriteAllText(path, status, new UTF8Encoding(false));
            EditorUtility.RevealInFinder(path);
        }
        catch (Exception ex)
        {
            status += "\n[ERROR] QA export failed: " + ex.Message;
            Debug.LogException(ex);
        }
    }
}
#endif
