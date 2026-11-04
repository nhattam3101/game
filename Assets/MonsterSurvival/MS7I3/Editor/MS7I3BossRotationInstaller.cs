#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 7I.3 Editor-only: backs up the existing Scene + 7I.1 script, installs three
// exact integration hooks and one OFF addon. Never edits the old WaveManager.cs.
public sealed class MS7I3BossRotationInstaller : EditorWindow
{
    private GameObject titanPrefab;
    private GameObject wardenPrefab;
    private Vector2 scroll;
    private bool busy;
    private string report = "Open SampleScene in EDIT MODE. Assign the real Titan + Grave Warden prefab assets.";

    [MenuItem("Tools/MonsterSurvival/7I Endless Survival/3 - Open 7I.3 Boss Rotation")]
    public static void Open()
    {
        MS7I3BossRotationInstaller w = GetWindow<MS7I3BossRotationInstaller>("7I.3 Boss Rotation");
        w.minSize = new Vector2(650, 480);
    }

    private void Queue(Action action)
    {
        if (busy) return;
        busy = true;
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            try { action(); } catch (Exception ex) { report = "[FAIL] " + ex; Debug.LogException(ex); }
            finally { busy = false; if (this != null) Repaint(); }
        };
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        try
        {
            EditorGUILayout.LabelField("7I.3 / ENDLESS BOSS ROTATION & CAPPED SCALING", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("7I.1 + 7I.2 required. NORMAL stays unchanged. In ENDLESS, boss appears at the END of Wave 10, 15, 20... and must die before next Wave. The installer adds a small, reversible bridge to 7I.1; WaveManager.cs is untouched. Boss HP is never scaled.", MessageType.Info);
            titanPrefab = (GameObject)EditorGUILayout.ObjectField("Real Shadow Titan prefab", titanPrefab, typeof(GameObject), false);
            wardenPrefab = (GameObject)EditorGUILayout.ObjectField("Real Grave Warden prefab", wardenPrefab, typeof(GameObject), false);
            EditorGUILayout.Space();
            if (GUILayout.Button("1 - AUDIT SCENE, DEPENDENCIES AND BRIDGE")) Queue(() => { report = Audit(); Debug.Log(report); });
            if (GUILayout.Button("2 - BACKUP + INSTALL BRIDGE / ADDON (OFF)")) Queue(Install);
            if (GUILayout.Button("3 - BACKUP + ENABLE BOSS ROTATION")) Queue(() => Toggle(true));
            if (GUILayout.Button("4 - BACKUP + DISABLE BOSS ROTATION")) Queue(() => Toggle(false));
            if (GUILayout.Button("5 - PURE BOSS SCHEDULE / CAP TESTS")) Queue(() => { report = MS7I3EndlessBossRotation.PureTests(); Debug.Log(report); });
            if (GUILayout.Button("6 - EXPORT 7I.3 QA TXT")) Queue(Export);
            if (GUILayout.Button("7 - BACKUP + REMOVE ADDON / REMOVE EXACT BRIDGE")) Queue(Uninstall);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Latest report", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(report, GUILayout.MinHeight(235), GUILayout.ExpandHeight(true));
        }
        finally { EditorGUILayout.EndScrollView(); }
    }

    private static bool SceneReady(out WaveManager manager, out MS7I1EndlessDirector director, out string reason)
    {
        manager = null; director = null;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "SampleScene")
        { reason = "Open the REAL SampleScene first."; return false; }
        WaveManager[] all = UnityEngine.Object.FindObjectsByType<WaveManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int count = 0;
        foreach (WaveManager candidate in all)
            if (candidate.gameObject.scene == scene) { manager = candidate; count++; }
        if (count != 1 || !manager) { reason = "Expected one WaveManager in SampleScene; found " + count; return false; }
        director = manager.GetComponent<MS7I1EndlessDirector>();
        if (!director || !director.enabled) { reason = "Enabled MS7I1EndlessDirector missing on WaveManager"; return false; }
        if (!manager.GetComponent<MS7I2GameplayModeBootstrap>())
        { reason = "7I.2 GameplayModeBootstrap missing"; return false; }
        GameManager existingGameManager = manager.gameManager ? manager.gameManager :
            UnityEngine.Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        if (!manager.enemySpawner || !existingGameManager || !existingGameManager.victoryByWaves ||
            manager.totalWaves != 5 || manager.bossWave != 5)
        { reason = "Original 5-wave / GameManager / EnemySpawner setup invalid"; return false; }
        if (!manager.safeSpawn && !manager.GetComponent<MapSafeSpawn2D>())
        { reason = "MapSafeSpawn2D missing. Boss rotation requires verified safe spawn positions."; return false; }
        reason = "REAL SampleScene / WaveManager / 7I.1 / 7I.2 / safe spawn verified";
        return true;
    }

    private static bool ValidPrefab(GameObject prefab, bool grave, out string reason)
    {
        reason = "";
        if (!prefab || !PrefabUtility.IsPartOfPrefabAsset(prefab))
        { reason = "Please drag REAL Prefab ASSET from Project, not Hierarchy"; return false; }
        if (!prefab.GetComponent<Health>()) { reason = prefab.name + " missing Health"; return false; }
        bool wardenAI = prefab.GetComponentInChildren<GraveWardenBossAI>(true) != null;
        if (grave && !wardenAI) { reason = "Grave Warden prefab missing GraveWardenBossAI"; return false; }
        if (!grave && wardenAI) { reason = "Shadow Titan slot points at Grave Warden"; return false; }
        reason = "VALID " + prefab.name;
        return true;
    }

    private static string SourcePath(MS7I1EndlessDirector director)
    {
        MonoScript mono = MonoScript.FromMonoBehaviour(director);
        return mono ? AssetDatabase.GetAssetPath(mono) : "";
    }

    private const string HookStart = "            SetSafeSpawnerBalance(wave);";
    private const string HookStartPatch = HookStart + "\n" +
        "            // 7I3_BRIDGE_ONCE: Optional capped scaling (no HP / Save changes).\n" +
        "            MS7I3EndlessBossRotation ms7i3 = GetComponent<MS7I3EndlessBossRotation>();\n" +
        "            if (ms7i3 != null && ms7i3.Ready) ms7i3.AdjustWaveSpawner(wave);";
    private const string HookTime = "            float remaining = Mathf.Max(10f, endlessWaveSeconds);";
    private const string HookTimePatch = HookTime + "\n" +
        "            // 7I3_BRIDGE_DURATION: Optional bounded Wave length.\n" +
        "            if (ms7i3 != null && ms7i3.Ready) remaining = ms7i3.AdjustWaveDuration(wave, remaining);";
    private const string HookGate = "            WavesFinished++;";
    private const string HookGatePatch =
        "            // 7I3_BRIDGE_GATE: Boss is required to die before proceeding.\n" +
        "            if (ms7i3 != null && ms7i3.Ready)\n" +
        "            {\n" +
        "                yield return ms7i3.RunBossGate(wave);\n" +
        "                if (waveManager == null || waveManager.gameManager == null ||\n" +
        "                    waveManager.gameManager.IsGameOver || !ms7i3.CanAdvance(wave))\n" +
        "                {\n" +
        "                    LastStatus = \"ENDLESS STOPPED: 7I.3 boss gate incomplete\";\n" +
        "                    if (enemySpawner != null) enemySpawner.enabled = false;\n" +
        "                    Record(LastStatus);\n" +
        "                    yield break;\n" +
        "                }\n" +
        "            }\n" + HookGate;

    private static string ReadSource(string assetPath)
    {
        string full = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), assetPath);
        return File.ReadAllText(full);
    }
    private static bool HasAllHooks(string source)
    {
        return source.Contains("7I3_BRIDGE_ONCE") && source.Contains("7I3_BRIDGE_DURATION") &&
               source.Contains("7I3_BRIDGE_GATE") && source.Contains(HookGatePatch);
    }
    private static string ReplaceExactlyOnce(string source, string find, string replace)
    {
        int pos = source.IndexOf(find, StringComparison.Ordinal);
        if (pos < 0 || source.IndexOf(find, pos + find.Length, StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException("Expected exactly one integration anchor: " + find);
        return source.Substring(0, pos) + replace + source.Substring(pos + find.Length);
    }
    private static string BuildPatched(string source)
    {
        if (HasAllHooks(source)) return source;
        if (source.Contains("7I3_BRIDGE_"))
            throw new InvalidOperationException("Partial 7I.3 bridge detected. Do not modify existing code automatically.");
        // All or nothing; don't mutate files until all three anchors validate.
        source = ReplaceExactlyOnce(source, HookStart, HookStartPatch);
        source = ReplaceExactlyOnce(source, HookTime, HookTimePatch);
        source = ReplaceExactlyOnce(source, HookGate, HookGatePatch);
        if (!HasAllHooks(source)) throw new InvalidOperationException("Patch verification failed");
        return source;
    }
    private static string RemovePatch(string source)
    {
        if (!source.Contains("7I3_BRIDGE_")) return source;
        if (!HasAllHooks(source)) throw new InvalidOperationException("Partial 7I.3 bridge; restore script from backup manually");
        source = ReplaceExactlyOnce(source, HookGatePatch, HookGate);
        source = ReplaceExactlyOnce(source, HookTimePatch, HookTime);
        source = ReplaceExactlyOnce(source, HookStartPatch, HookStart);
        if (source.Contains("7I3_BRIDGE_")) throw new InvalidOperationException("Incomplete bridge removal");
        return source;
    }

    private static string BackupSceneAndSource(Scene scene, string sourcePath)
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string sceneFile = Path.Combine(project, scene.path);
        string scriptFile = Path.Combine(project, sourcePath);
        if (string.IsNullOrEmpty(scene.path) || !File.Exists(sceneFile) || !File.Exists(scriptFile))
            throw new IOException("Missing Scene or 7I.1 script on disk. No edits performed.");
        string folder = Path.Combine(project, "MS_Backups", "7I3", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
        Directory.CreateDirectory(folder);
        File.Copy(sceneFile, Path.Combine(folder, "SampleScene.unity"));
        File.Copy(scriptFile, Path.Combine(folder, "MS7I1EndlessDirector.cs"));
        if (new FileInfo(Path.Combine(folder, "SampleScene.unity")).Length < 100 ||
            new FileInfo(Path.Combine(folder, "MS7I1EndlessDirector.cs")).Length < 100)
            throw new IOException("Backup validation failed. No edits performed.");
        return folder;
    }

    private static string Audit()
    {
        WaveManager wm; MS7I1EndlessDirector director; string reason;
        if (!SceneReady(out wm, out director, out reason)) return "[BLOCKED] " + reason;
        string path = SourcePath(director);
        if (string.IsNullOrEmpty(path)) return "[BLOCKED] 7I.1 source asset not found";
        string source = ReadSource(path);
        string patchState = HasAllHooks(source) ? "INSTALLED" : source.Contains("7I3_BRIDGE_") ? "PARTIAL / BLOCKED" : "NOT INSTALLED";
        MS7I3EndlessBossRotation addon = wm.GetComponent<MS7I3EndlessBossRotation>();
        StringBuilder b = new StringBuilder();
        b.AppendLine("[PASS] " + reason);
        b.AppendLine("7I.1 Script: " + path + " | BRIDGE=" + patchState);
        b.AppendLine("Addon=" + (addon ? "PRESENT" : "MISSING") +
            (addon ? " enabled=" + addon.enableBossRotation : ""));
        if (addon)
        {
            string valid;
            b.AppendLine("Prefabs=" + addon.shadowTitanPrefab + " / " + addon.graveWardenPrefab);
            b.AppendLine("Runtime dependencies=" + addon.DependenciesValid(out valid) + " : " + valid);
        }
        b.AppendLine("Runtime and HP / Save QA still require Play Mode.");
        return b.ToString();
    }

    private bool BeginEdit(out WaveManager wm, out MS7I1EndlessDirector director, out string sourcePath, out string backup)
    {
        wm = null; director = null; sourcePath = ""; backup = "";
        if (Application.isPlaying) { report = "[BLOCKED] Stop Play Mode and open SampleScene in Edit Mode"; return false; }
        string reason;
        if (!SceneReady(out wm, out director, out reason)) { report = "[BLOCKED] " + reason; return false; }
        sourcePath = SourcePath(director);
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath,"..")),sourcePath)))
        { report = "[BLOCKED] 7I.1 script asset missing"; return false; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { report = "[CANCELLED] Scene not saved"; return false; }
        try { backup = BackupSceneAndSource(SceneManager.GetActiveScene(), sourcePath); }
        catch (Exception ex) { report = "[BLOCKED] Backup failed: " + ex.Message; return false; }
        return true;
    }

    private void Install()
    {
        WaveManager wm; MS7I1EndlessDirector director; string script, backup;
        if (!BeginEdit(out wm, out director, out script, out backup)) return;
        string reasonA, reasonB;
        bool validA = ValidPrefab(titanPrefab, false, out reasonA);
        bool validB = ValidPrefab(wardenPrefab, true, out reasonB);
        if (!validA || !validB || titanPrefab == wardenPrefab)
        { report = "[BLOCKED] Prefabs: " + reasonA + " | " + reasonB + ". Select two different assets."; return; }
        string project = Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        string full = Path.Combine(project, script);
        string source = File.ReadAllText(full);
        string patched;
        try { patched = BuildPatched(source); }
        catch (Exception ex) { report = "[BLOCKED] No files modified. " + ex.Message; return; }

        MS7I3EndlessBossRotation addon = wm.GetComponent<MS7I3EndlessBossRotation>();
        if (!addon) addon = Undo.AddComponent<MS7I3EndlessBossRotation>(wm.gameObject);
        Undo.RecordObject(addon, "Install 7I.3 Boss Rotation OFF");
        addon.endless = director; addon.waveManager = wm; addon.spawner = wm.enemySpawner;
        addon.shadowTitanPrefab = titanPrefab; addon.graveWardenPrefab = wardenPrefab;
        addon.enableBossRotation = false;
        EditorUtility.SetDirty(addon);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
        { report = "[BLOCKED] Scene save failed; script was not patched. Backup=" + backup; return; }
        if (patched != source)
        {
            File.WriteAllText(full, patched, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(script, ImportAssetOptions.ForceUpdate);
        }
        report = "[INSTALLED OFF] 7I.3 addon on WaveManager; 7I.1 bridge installed. Prefabs assigned.\n" +
            "Next, wait for Unity compilation; reopen window and click ENABLE.\nBackup=" + backup;
        Debug.Log(report);
    }

    private void Toggle(bool enabled)
    {
        WaveManager wm; MS7I1EndlessDirector director; string script, backup;
        if (!BeginEdit(out wm, out director, out script, out backup)) return;
        MS7I3EndlessBossRotation addon = wm.GetComponent<MS7I3EndlessBossRotation>();
        if (!addon) { report = "[BLOCKED] Install button 2 first"; return; }
        if (enabled)
        {
            if (!HasAllHooks(ReadSource(script))) { report = "[BLOCKED] Bridge not fully installed"; return; }
            string reason;
            if (!addon.DependenciesValid(out reason)) { report = "[BLOCKED] " + reason; return; }
        }
        Undo.RecordObject(addon, "7I.3 toggle boss rotation");
        addon.enableBossRotation = enabled;
        EditorUtility.SetDirty(addon);
        EditorSceneManager.MarkSceneDirty(wm.gameObject.scene);
        EditorSceneManager.SaveScene(wm.gameObject.scene);
        report = "[7I.3] Boss Rotation " + (enabled ? "ENABLED (Endless only)" : "DISABLED") + ". NORMAL always unchanged.\nBackup=" + backup;
        Debug.Log(report);
    }

    private void Uninstall()
    {
        WaveManager wm; MS7I1EndlessDirector director; string script, backup;
        if (!BeginEdit(out wm, out director, out script, out backup)) return;
        string project = Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        string full = Path.Combine(project, script);
        string source = File.ReadAllText(full);
        string clean;
        try { clean = RemovePatch(source); }
        catch (Exception ex) { report = "[BLOCKED] No changes. " + ex.Message; return; }
        MS7I3EndlessBossRotation addon = wm.GetComponent<MS7I3EndlessBossRotation>();
        if (addon) Undo.DestroyObjectImmediate(addon);
        EditorSceneManager.MarkSceneDirty(wm.gameObject.scene);
        EditorSceneManager.SaveScene(wm.gameObject.scene);
        if (clean != source)
        {
            File.WriteAllText(full, clean, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(script, ImportAssetOptions.ForceUpdate);
        }
        report = "[UNINSTALLED] Only 7I.3 addon + exact 7I.1 bridge removed. Other systems retained.\nBackup=" + backup;
        Debug.Log(report);
    }

    private void Export()
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        string folder = Path.Combine(project, "MS_QAReports", "7I3");
        Directory.CreateDirectory(folder);
        var sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 7I.3 / BOSS ROTATION + DIFFICULTY QA");
        sb.AppendLine("Generated=" + DateTime.Now.ToString("o") + " Unity=" + Application.unityVersion +
            " Scene=" + SceneManager.GetActiveScene().name + " Play=" + Application.isPlaying);
        if (!Application.isPlaying) sb.AppendLine(Audit());
        else
        {
            WaveManager wm = UnityEngine.Object.FindFirstObjectByType<WaveManager>();
            MS7I1EndlessDirector director = wm ? wm.GetComponent<MS7I1EndlessDirector>() : null;
            MS7I3EndlessBossRotation addon = wm ? wm.GetComponent<MS7I3EndlessBossRotation>() : null;
            MS7I2GameplayModeBootstrap bootstrap = wm ? wm.GetComponent<MS7I2GameplayModeBootstrap>() : null;
            sb.AppendLine("7I2=" + (bootstrap ? bootstrap.status : "MISSING"));
            sb.AppendLine("7I1=" + (director ? "Enabled=" + director.RuntimeEnabled + " EndlessStarted=" + director.EndlessStarted +
                " Wave=" + director.CurrentEndlessWave + " Peak=" + director.PeakEndlessWave + " Error=" + director.ErrorCount : "MISSING"));
            if (addon)
            {
                sb.AppendLine("7I3 Enabled=" + addon.enableBossRotation + " Status=" + addon.Status +
                    " Spawns=" + addon.BossesSpawned + " Defeats=" + addon.BossesDefeated +
                    " SafeRetries=" + addon.SafeSpawnRetries + " Errors=" + addon.Errors +
                    " ActiveBossWave=" + addon.ActiveBossWave + " GateDefeated=" + addon.ActiveBossDefeated +
                    " MaxObservedWave=" + addon.MaxObservedWave);
                foreach (string t in addon.Trace) sb.AppendLine(t);
            }
            else sb.AppendLine("7I3 MISSING");
            if (wm)
            {
                sb.AppendLine("Spawner enabled=" + (wm.enemySpawner ? wm.enemySpawner.enabled.ToString() : "MISSING") +
                    " interval=" + (wm.enemySpawner ? wm.enemySpawner.spawnInterval.ToString("F2") : "-") +
                    " maxEnemies=" + (wm.enemySpawner ? wm.enemySpawner.maxEnemies.ToString() : "-") +
                    " WaveManagerCurrentWave=" + wm.CurrentWave);
            }
        }
        sb.AppendLine(MS7I3EndlessBossRotation.PureTests());
        sb.AppendLine("MANUAL PENDING: NORMAL Victory Wave5, ENDLESS Wave10 Boss spawn+death then Wave11, Wave15 Grave Warden, 7F/7E interaction, Boss HP follow, Pause, GameOver, Save/Quest isolation.");
        string output = Path.Combine(folder, "MS7I3_BossRotationQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".txt");
        File.WriteAllText(output, sb.ToString(), Encoding.UTF8);
        report = "[EXPORTED] " + output + "\n" + sb;
        Debug.Log("[7I.3] Exported: " + output);
    }
}
#endif
