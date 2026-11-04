using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MS7A1WaveBalanceInstaller
{
    public const string AssetPath = "Assets/MonsterSurvival/Features/Combat/Balancing7A1/Data/MS7A1_BalancedWaves.asset";
    private const string Root = "Tools/MonsterSurvival/7A Enemy Wave Balance/";

    [MenuItem(Root + "1 - INSTALL 7A.1 (OPEN SampleScene)")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("7A.1", "Stop Play Mode first.", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "SampleScene" || string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog("7A.1", "Open and save SampleScene before installation.", "OK");
            return;
        }

        WaveManager manager = FindWaveManager();
        if (manager == null)
        {
            EditorUtility.DisplayDialog("7A.1", "Exactly one WaveManager must exist in SampleScene.", "OK");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!EditorUtility.DisplayDialog("Install 7A.1", "Create (or reuse) a wave-balance profile and attach a PREVIEW-ONLY component to WaveManager?\n\nA backup of SampleScene is created first.", "INSTALL", "CANCEL")) return;

        try
        {
            string backup = BackupScene(scene.path);
            if (string.IsNullOrEmpty(backup)) return;

            MS7A1WaveBalanceProfile profile = GetOrCreateProfile();
            if (profile == null) throw new InvalidOperationException("Could not create balance profile.");
            MS7A1WaveBalanceApplier applier = manager.GetComponent<MS7A1WaveBalanceApplier>();
            if (applier == null) applier = Undo.AddComponent<MS7A1WaveBalanceApplier>(manager.gameObject);

            Undo.RecordObject(applier, "Configure 7A.1 wave balance");
            applier.waveManager = manager;
            applier.profile = profile;
            // Preview-only by default. Never silently modify running gameplay.
            applier.applyAtRuntime = false;
            EditorUtility.SetDirty(applier);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeObject = applier;
            Debug.Log("[7A.1] Installer success (PREVIEW ONLY). Backup: " + backup + ". Enable 'Apply At Runtime' on the component when ready.");
        }
        catch (Exception ex)
        {
            Debug.LogError("[7A.1] Installer failed: " + ex.Message);
            EditorUtility.DisplayDialog("7A.1 Installation failed", ex.Message, "OK");
        }
    }

    [MenuItem(Root + "2 - AUDIT CURRENT SCENE (READ ONLY)")]
    public static void Audit()
    {
        Debug.Log(BuildReport());
    }

    [MenuItem(Root + "3 - EXPORT AUDIT TXT (READ ONLY)")]
    public static void Export()
    {
        string path = EditorUtility.SaveFilePanel("Save 7A.1 audit", "", "MS7A1_WaveBalance_Audit", "txt");
        if (string.IsNullOrEmpty(path)) return;
        File.WriteAllText(path, BuildReport(), Encoding.UTF8);
        Debug.Log("[7A.1] Audit exported to " + path);
    }

    [MenuItem(Root + "4 - REMOVE 7A.1 COMPONENT (OPEN SampleScene)")]
    public static void Remove()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SceneManager.GetActiveScene().name != "SampleScene") return;
        WaveManager manager = FindWaveManager();
        if (manager == null) return;
        MS7A1WaveBalanceApplier c = manager.GetComponent<MS7A1WaveBalanceApplier>();
        if (c == null) { Debug.Log("[7A.1] Nothing to remove."); return; }
        if (!EditorUtility.DisplayDialog("Remove 7A.1", "Remove the component? The profile asset and all existing gameplay scripts will remain unchanged.", "REMOVE", "CANCEL")) return;
        Undo.DestroyObjectImmediate(c);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[7A.1] Component removed. The profile asset is retained.");
    }

    public static MS7A1WaveBalanceProfile GetOrCreateProfile()
    {
        MS7A1WaveBalanceProfile existing = AssetDatabase.LoadAssetAtPath<MS7A1WaveBalanceProfile>(AssetPath);
        if (existing != null) return existing;
        string folder = Path.GetDirectoryName(AssetPath).Replace('\\', '/');
        EnsureFolders(folder);
        MS7A1WaveBalanceProfile asset = ScriptableObject.CreateInstance<MS7A1WaveBalanceProfile>();
        float[] interval = { 2.1f, 1.85f, 1.55f, 1.30f, 1.15f };
        int[] max = { 16, 20, 24, 28, 32 };
        float[] normal = { 100f, 75f, 60f, 50f, 45f };
        float[] fast = { 0f, 25f, 30f, 30f, 30f };
        float[] tank = { 0f, 0f, 10f, 20f, 25f };
        float[] ranged = { 10f, 10f, 8.5f, 7.5f, 7f };
        int[] rangedMax = { 1, 1, 2, 2, 3 };
        for (int i = 0; i < 5; i++)
        {
            asset.rows.Add(new MS7A1WaveBalanceProfile.WaveRow {
                wave = i + 1, spawnInterval = interval[i], maxEnemies = max[i],
                normalWeight = normal[i], fastWeight = fast[i], tankWeight = tank[i],
                rangedSpawnInterval = ranged[i], maxRangedEnemies = rangedMax[i]
            });
        }
        AssetDatabase.CreateAsset(asset, AssetPath);
        AssetDatabase.SaveAssets();
        return asset;
    }

    private static void EnsureFolders(string folder)
    {
        string[] parts = folder.Split('/');
        string path = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = path + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(path, parts[i]);
            path = next;
        }
    }

    private static string BackupScene(string assetScenePath)
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string source = Path.Combine(projectRoot, assetScenePath);
        if (!File.Exists(source))
        {
            EditorUtility.DisplayDialog("7A.1", "Scene file not found on disk. Save the Scene first.", "OK");
            return null;
        }
        string backupDir = Path.Combine(projectRoot, "MS_Backups", "7A1");
        Directory.CreateDirectory(backupDir);
        string target = Path.Combine(backupDir, "SampleScene_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity");
        File.Copy(source, target, false);
        return target;
    }

    private static WaveManager FindWaveManager()
    {
        WaveManager[] all = UnityEngine.Object.FindObjectsByType<WaveManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        WaveManager match = null;
        Scene scene = SceneManager.GetActiveScene();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].gameObject.scene != scene) continue;
            if (match != null) return null; // Ambiguous: refuse to patch.
            match = all[i];
        }
        return match;
    }

    private static string BuildReport()
    {
        StringBuilder s = new StringBuilder();
        s.AppendLine("MONSTERSURVIVAL 7A.1 — WAVE BALANCE AUDIT (READ ONLY)");
        s.AppendLine("Unity " + Application.unityVersion + " | Scene " + SceneManager.GetActiveScene().name + " | " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        WaveManager manager = FindWaveManager();
        if (manager == null) { s.AppendLine("ERROR: Expected exactly one WaveManager in the open scene."); return s.ToString(); }
        EnemySpawner spawner = manager.enemySpawner;
        if (spawner == null) s.AppendLine("ERROR: WaveManager.enemySpawner is missing.");
        MS7A1WaveBalanceApplier app = manager.GetComponent<MS7A1WaveBalanceApplier>();
        if (app == null) { s.AppendLine("INFO: 7A.1 component not installed."); return s.ToString(); }
        if (app.profile == null) { s.AppendLine("ERROR: Profile reference is missing."); return s.ToString(); }
        string reason;
        bool valid = app.profile.Validate(manager.totalWaves, out reason);
        s.AppendLine((valid ? "PASS: " : "ERROR: ") + reason);
        s.AppendLine("MODE: " + (app.applyAtRuntime ? "ACTIVE" : "PREVIEW ONLY"));
        s.AppendLine("ORIGINAL BossWave=" + manager.bossWave + " BossPrefab=" + (manager.bossPrefab != null ? manager.bossPrefab.name : "MISSING") + " (7A.1 does not change these)");
        s.AppendLine("RANGED: " + (manager.rangedEnemyPrefab != null ? "prefab present" : "no prefab (ranged tuning will have no effect)"));
        if (spawner != null)
            s.AppendLine("PREFABS Normal=" + (spawner.enemyPrefab != null) + " Fast=" + (spawner.fastEnemyPrefab != null) + " Tank=" + (spawner.tankEnemyPrefab != null));

        for (int wave = 1; wave <= manager.totalWaves; wave++)
        {
            MS7A1WaveBalanceProfile.WaveRow r;
            if (!app.profile.TryGetRow(wave, out r) || !MS7A1WaveBalanceProfile.ValidRow(r))
            {
                s.AppendLine("Wave " + wave + ": MISSING/INVALID"); continue;
            }
            float sum = r.normalWeight + r.fastWeight + r.tankWeight;
            float approx = manager.waveDuration / r.spawnInterval;
            s.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Wave {0}: interval={1:0.00}s cap={2} Normal={3:0}% Fast={4:0}% Tank={5:0}% RangedInterval={6:0.0}s RangedCap={7} approx spawn attempts={8:0.0}",
                wave, r.spawnInterval, r.maxEnemies, r.normalWeight / sum * 100f,
                r.fastWeight / sum * 100f, r.tankWeight / sum * 100f,
                r.rangedSpawnInterval, r.maxRangedEnemies, approx));
        }
        s.AppendLine("Approx spawn attempts ignore alive-cap, RNG, pauses and special spawners. Healer, Summoner, Elite are handled by separate directors.");
        s.AppendLine("No files or Save profiles were changed by this audit.");
        return s.ToString();
    }
}
