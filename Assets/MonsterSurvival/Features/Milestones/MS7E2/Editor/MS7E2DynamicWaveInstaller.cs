#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Non-destructive installer, audit and runtime evidence export for 7E.2.</summary>
public sealed class MS7E2DynamicWaveInstaller : EditorWindow
{
    private Vector2 scroll;
    private string lastAudit = "Open SampleScene and run AUDIT. No scene modifications in audit.";

    [MenuItem("Tools/MonsterSurvival/7E Dynamic Waves/1 - Open 7E.2 Installer and QA")]
    public static void OpenWindow()
    {
        MS7E2DynamicWaveInstaller window = GetWindow<MS7E2DynamicWaveInstaller>("7E.2 Dynamic Waves");
        window.minSize = new Vector2(510, 450);
        window.Show();
    }

    [MenuItem("Tools/MonsterSurvival/7E Dynamic Waves/2 - Audit 7E.2 (READ ONLY)")]
    private static void AuditMenu()
    {
        string result = BuildAudit();
        Debug.Log(result);
        EditorUtility.DisplayDialog("7E.2 AUDIT (READ ONLY)", result, "OK");
    }

    [MenuItem("Tools/MonsterSurvival/7E Dynamic Waves/3 - Export 7E.2 TXT Report")]
    private static void ExportMenu()
    {
        ExportReport();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.Space(8);
        GUILayout.Label("MONSTERSURVIVAL — 7E.2 DYNAMIC WAVE ENCOUNTERS", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Opt-in add-on. Only changes live EnemySpawner spawn interval and three type weights during eligible waves. Boss wave excluded by default. Install does NOT enable effects.", MessageType.Info);

        if (GUILayout.Button("1 — AUDIT EXISTING WAVES / ENEMY SPAWNER (READ ONLY)", GUILayout.Height(34)))
        {
            lastAudit = BuildAudit();
            Debug.Log(lastAudit);
        }
        if (GUILayout.Button("2 — INSTALL / REPAIR ON WAVEMANAGER (BACKUP SCENE)", GUILayout.Height(34)))
            InstallOnWaveManager();
        if (GUILayout.Button("3 — EXPORT READ ONLY REPORT TXT (DURING PLAY MODE)", GUILayout.Height(34)))
            ExportReport();

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox("In Inspector: select WaveManager object > MS7E2 Dynamic Wave Director > enable 'Enable Dynamic Encounters' after the audit. Start Play Mode and test waves 2, 3, 4, 5. Export TXT while Play Mode is RUNNING to retain live trace.", MessageType.Warning);

        MS7E2DynamicWaveDirector director = FindDirector();
        if (director != null)
        {
            EditorGUILayout.LabelField("Live / Current", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(director.GetRuntimeSummary(), EditorStyles.textArea, GUILayout.MinHeight(80));
            EditorGUILayout.LabelField("Runtime Event Trace", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(director.GetTraceText(), GUILayout.MinHeight(120));
        }

        EditorGUILayout.Space(8);
        GUILayout.Label("Last audit", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(lastAudit, GUILayout.MinHeight(200));
        EditorGUILayout.EndScrollView();
    }

    private static WaveManager[] FindWaveManagers()
    {
        return UnityEngine.Object.FindObjectsByType<WaveManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    private static MS7E2DynamicWaveDirector FindDirector()
    {
        WaveManager[] waves = FindWaveManagers();
        return waves.Length == 1 ? waves[0].GetComponent<MS7E2DynamicWaveDirector>() : null;
    }

    private static string BuildAudit()
    {
        StringBuilder text = new StringBuilder();
        Scene scene = SceneManager.GetActiveScene();
        text.AppendLine("MONSTERSURVIVAL 7E.2 — DYNAMIC WAVES / READ ONLY");
        text.AppendLine("Unity=" + Application.unityVersion + " Scene=" + scene.name + " PlayMode=" + EditorApplication.isPlaying);
        WaveManager[] waves = FindWaveManagers();
        if (waves.Length != 1)
        {
            text.AppendLine("[ERROR] Need exactly one scene WaveManager; found " + waves.Length + ". No changes made.");
            return text.ToString();
        }
        WaveManager wave = waves[0];
        text.AppendLine("[PASS] Exactly one WaveManager on " + wave.gameObject.name);
        if (wave.enemySpawner == null)
        {
            text.AppendLine("[ERROR] WaveManager.enemySpawner is missing; cannot safely modify wave pacing.");
            return text.ToString();
        }
        text.AppendLine("[PASS] Reuses WaveManager.enemySpawner=" + wave.enemySpawner.gameObject.name);
        text.AppendLine("[INFO] CurrentWave=" + wave.CurrentWave + " BossWave=" + wave.bossWave + " TotalWaves=" + wave.totalWaves);
        MS7E2DynamicWaveDirector[] directors = wave.GetComponents<MS7E2DynamicWaveDirector>();
        if (directors.Length == 0)
        {
            text.AppendLine("[PENDING] Director not installed. Click Installer button 2.");
            text.AppendLine("[INFO] Existing WaveManager/Spawner unchanged.");
            return text.ToString();
        }
        if (directors.Length > 1)
        {
            text.AppendLine("[ERROR] Multiple 7E.2 components on WaveManager; do not enable until deduplicated.");
            return text.ToString();
        }
        MS7E2DynamicWaveDirector director = directors[0];
        string reason;
        bool okay = director.TryValidateSpawnerFields(out reason);
        text.AppendLine((okay ? "[PASS] " : "[ERROR] ") + reason);
        text.AppendLine("[INFO] OptIn=" + director.enableDynamicEncounters +
                        " firstAffectedWave=" + director.firstAffectedWave +
                        " excludeBossWave=" + director.excludeBossWave +
                        " surgeEnabled=" + director.enableSurge);
        if (!director.excludeBossWave)
            text.AppendLine("[WARN] Exclude Boss Wave is OFF. For 7E.2 first QA keep it ON.");
        if (director.firstAffectedWave > wave.totalWaves)
            text.AppendLine("[WARN] firstAffectedWave exceeds totalWaves; no encounter will start.");
        text.AppendLine("[INFO] Profiles (wave " + director.firstAffectedWave + ") = Rush; next=Siege; next=Swarm; rotates after that.");
        text.AppendLine("[INFO] Existing EliteSpawnDirector, HealerWaveSpawner, 7D bonuses and game rewards are untouched.");
        if (EditorApplication.isPlaying)
        {
            text.AppendLine("=== LIVE ===");
            text.AppendLine(director.GetRuntimeSummary());
            text.Append(director.GetTraceText());
            if (director.EncounterStarts == 0) text.AppendLine("[PENDING] No dynamic encounter observed in Play Mode.");
            else text.AppendLine("[PASS] At least one dynamic encounter started in this Play session.");
            if (director.SurgeStarts == 0 && director.enableSurge)
                text.AppendLine("[PENDING] Surge not yet observed in this Play session.");
            if (director.SuccessfulRestores == 0)
                text.AppendLine("[PENDING] No full spawner restoration observed yet.");
        }
        else
        {
            text.AppendLine("[PENDING] Play Mode checks not run. Export report DURING Play Mode.");
        }
        text.AppendLine("[MANUAL] Verify Wave2 Rush / Wave3 Siege / Wave4 Swarm and Wave5 boss unchanged.");
        text.AppendLine("[MANUAL] Verify pause, enemy cap/safe spawn, Elite Affixes, EXP/Gold, Save/Profile.");
        return text.ToString();
    }

    private static void InstallOnWaveManager()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("7E.2", "Exit Play Mode before installing.", "OK");
            return;
        }
        WaveManager[] waves = FindWaveManagers();
        if (waves.Length != 1 || waves[0].enemySpawner == null)
        {
            EditorUtility.DisplayDialog("7E.2", "Need exactly one WaveManager with a valid enemySpawner. Run AUDIT.", "OK");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog("7E.2", "Save SampleScene to disk first.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("7E.2 Install", "Back up the current Scene and add/repair ONLY the 7E.2 component on the existing WaveManager? Feature stays OFF by default.", "Install with backup", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string source = Path.Combine(projectRoot, scene.path);
        string backupFolder = Path.Combine(projectRoot, "MS_Backups/7E2");
        if (!File.Exists(source))
        {
            EditorUtility.DisplayDialog("7E.2", "Scene file not found on disk; install canceled.", "OK");
            return;
        }
        Directory.CreateDirectory(backupFolder);
        string backup = Path.Combine(backupFolder,
            Path.GetFileNameWithoutExtension(scene.path) + "_before_7E2_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
        File.Copy(source, backup, false);

        WaveManager wave = waves[0];
        MS7E2DynamicWaveDirector director = wave.GetComponent<MS7E2DynamicWaveDirector>();
        bool newlyAdded = director == null;
        if (newlyAdded)
        {
            director = Undo.AddComponent<MS7E2DynamicWaveDirector>(wave.gameObject);
            director.enableDynamicEncounters = false;
        }
        Undo.RecordObject(director, "7E.2 Setup References");
        if (director.waveManager == null) director.waveManager = wave;
        if (director.enemySpawner == null) director.enemySpawner = wave.enemySpawner;
        EditorUtility.SetDirty(director);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        string status = newlyAdded ? "Installed (feature OFF)." : "Repaired missing references; existing toggle preserved.";
        string message = status + "\nBackup: " + backup + "\n\n" + BuildAudit();
        Debug.Log(message);
        EditorUtility.DisplayDialog("7E.2 Installer", message, "OK");
        Selection.activeGameObject = wave.gameObject;
    }

    private static void ExportReport()
    {
        string content = BuildAudit();
        content += "\n=== RELEASE NOTES / MANUAL QA ===\n";
        content += "[ ] Confirm Rush/Siege/Swarm cycle at waves 2/3/4.\n";
        content += "[ ] Verify boss wave 5 unchanged (including Shadow Titan or Grave Warden spawn).\n";
        content += "[ ] Verify EnemySpawner maxEnemies, safe placement and no enemy spawns during breaks/pause.\n";
        content += "[ ] Verify EliteEnemy affixes, Healers, boss HP HUD, EXP/Gold.\n";
        content += "[ ] Verify Save/Profile, Victory/GameOver, retry; no changes to 7D set bonuses.\n";
        content += "[ ] Disable 7E.2 in Inspector and verify baseline EnemySpawner settings restored.\n";
        content += "RESULT=PENDING until live evidence and player manual QA are complete.\n";

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string folder = Path.Combine(projectRoot, "MS_Reports/7E2");
        Directory.CreateDirectory(folder);
        string filename = Path.Combine(folder,
            "MS7E2_DynamicWaveQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
        File.WriteAllText(filename, content, new UTF8Encoding(false));
        Debug.Log("[7E.2] Report written: " + filename);
        EditorUtility.RevealInFinder(filename);
    }
}
#endif
