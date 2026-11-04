#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MS7A2AdvancedBalanceInstaller
{
    private const string Root = "Tools/MonsterSurvival/7A Enemy Wave Balance/";
    private const string AssetPath = "Assets/MonsterSurvival/Balancing7A2/Data/MS7A2_AdvancedEnemyBalance.asset";

    [MenuItem(Root + "5 - INSTALL 7A.2 (OPEN SampleScene)")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorUtility.DisplayDialog("7A.2", "Stop Play Mode before install.", "OK"); return; }
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "SampleScene" || string.IsNullOrEmpty(scene.path))
        { EditorUtility.DisplayDialog("7A.2", "Open and save SampleScene first.", "OK"); return; }
        WaveManager wave = FindWave();
        if (wave == null) { EditorUtility.DisplayDialog("7A.2", "Exactly ONE WaveManager must exist in SampleScene.", "OK"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!EditorUtility.DisplayDialog("Install 7A.2", "Add PREVIEW-ONLY advanced enemy balance to WaveManager? No old components will be removed. A Scene backup is created.", "INSTALL", "CANCEL")) return;
        try
        {
            string backup = BackupScene(scene.path);
            MS7A2AdvancedBalanceProfile data = GetOrCreateProfile();
            MS7A2AdvancedBalanceApplier component = wave.GetComponent<MS7A2AdvancedBalanceApplier>();
            if (component == null) component = Undo.AddComponent<MS7A2AdvancedBalanceApplier>(wave.gameObject);
            Undo.RecordObject(component, "Configure 7A.2 balancing");
            component.waveManager = wave;
            component.healerSpawner = wave.GetComponent<HealerWaveSpawner>();
            component.summonerSpawner = wave.GetComponent<SummonerWaveSpawner>();
            component.profile = data;
            component.applyAtRuntime = false; // Always safe default, including reinstall
            EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = wave.gameObject;
            Debug.Log("[7A.2 INSTALL] PREVIEW ONLY. SampleScene backup: " + backup + ". Enable Apply At Runtime in Inspector for live tuning.");
        }
        catch (Exception e) { Debug.LogError("[7A.2 INSTALL] " + e); EditorUtility.DisplayDialog("7A.2 failed", e.Message, "OK"); }
    }

    [MenuItem(Root + "6 - AUDIT 7A.2 (READ ONLY)")]
    public static void Audit() { Debug.Log(BuildReport()); }

    [MenuItem(Root + "7 - EXPORT 7A.2 AUDIT TXT (READ ONLY)")]
    public static void Export()
    {
        string path = EditorUtility.SaveFilePanel("7A.2 Audit", "", "MS7A2_AdvancedBalance_Audit", "txt");
        if (string.IsNullOrEmpty(path)) return;
        File.WriteAllText(path, BuildReport(), Encoding.UTF8);
        Debug.Log("[7A.2] Exported: " + path);
    }

    [MenuItem(Root + "8 - REMOVE 7A.2 COMPONENT (OPEN SampleScene)")]
    public static void Remove()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "SampleScene") return;
        WaveManager wave = FindWave();
        if (wave == null) return;
        MS7A2AdvancedBalanceApplier component = wave.GetComponent<MS7A2AdvancedBalanceApplier>();
        if (component == null) { Debug.Log("[7A.2] No component to remove."); return; }
        if (!EditorUtility.DisplayDialog("Remove 7A.2", "Remove ONLY the 7A.2 applier? Data asset and original AI/7A.1 components remain.", "REMOVE", "CANCEL")) return;
        Undo.DestroyObjectImmediate(component);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[7A.2] Component removed. Existing 7A.1 balance and AI unchanged.");
    }

    private static string BuildReport()
    {
        StringBuilder s = new StringBuilder();
        s.AppendLine("MONSTERSURVIVAL 7A.2 - ADVANCED ENEMY BALANCE AUDIT (READ ONLY)");
        s.AppendLine("Unity=" + Application.unityVersion + " scene=" + SceneManager.GetActiveScene().name + " time=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        WaveManager wave = FindWave();
        if (wave == null) { s.AppendLine("ERROR: Exactly one WaveManager required."); return s.ToString(); }
        MS7A2AdvancedBalanceApplier a = wave.GetComponent<MS7A2AdvancedBalanceApplier>();
        if (a == null) { s.AppendLine("WARN: 7A.2 component not yet installed."); return s.ToString(); }
        if (a.profile == null) { s.AppendLine("ERROR: Profile is missing."); return s.ToString(); }
        string reason;
        bool valid = a.profile.Validate(wave.totalWaves, out reason);
        s.AppendLine((valid ? "PASS: " : "ERROR: ") + reason);
        s.AppendLine("MODE: " + (a.applyAtRuntime ? "ACTIVE (live changes)" : "PREVIEW ONLY (no live changes)"));
        s.AppendLine("7A.1: " + (wave.GetComponent<MS7A1WaveBalanceApplier>() != null ? "present; separately controlled" : "not installed (7A.2 still supported)"));
        s.AppendLine("HEALER director: " + (a.healerSpawner != null ? "attached" : "missing; healer wave count tuning skipped"));
        s.AppendLine("SUMMONER director: " + (a.summonerSpawner != null ? "attached" : "missing; summoner wave count tuning skipped"));
        s.AppendLine("RANGED prefab: " + (wave.rangedEnemyPrefab != null ? "present" : "missing; ranged tuning skipped"));
        s.AppendLine("BOSS Wave: " + wave.bossWave + "; prefab=" + (wave.bossPrefab != null ? wave.bossPrefab.name : "none") + "; never touched by 7A.2.");
        int marked = 0;
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        for (int i = 0; i < guids.Length; i++)
        {
            GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (p != null && p.GetComponent<MS7A2EliteMarker>() != null) marked++;
        }
        s.AppendLine("Elite prefabs with MS7A2EliteMarker: " + marked + " (optional; none spawned by 7A.2)");
        for (int w = 1; w <= wave.totalWaves; w++)
        {
            MS7A2AdvancedBalanceProfile.WaveRow r;
            if (!a.profile.TryGet(w, out r) || !MS7A2AdvancedBalanceProfile.Valid(r))
            { s.AppendLine("ERROR Wave " + w + ": missing/invalid"); continue; }
            s.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Wave {0}: Healer interval={1:0.0}s active={2} total={3} heal={4:0.0}/{5:0.0}s | Summoner interval={6:0.0}s active={7} total={8} summons={9}/{10:0.0}s | Ranged attack={11:0.0}s dmg={12:0.0} | Gold/EXP x{13:0.00}/x{14:0.00}",
                w, r.healerSpawnInterval, r.maxActiveHealers, r.healerSpawnsPerWave,
                r.healerHealAmount, r.healerHealInterval, r.summonerSpawnInterval,
                r.maxActiveSummoners, r.summonerSpawnsPerWave, r.minionsPerCast,
                r.summonInterval, r.rangedAttackInterval, r.rangedProjectileDamage,
                r.specialGoldMultiplier, r.specialExpMultiplier));
        }
        s.AppendLine("INFO: Healer/Summoner firstWave/lastWave stay unchanged. Elite requires a marked existing prefab.");
        s.AppendLine("INFO: Bonuses scale EXISTING rewards only on recognized specials. Boss/minions/Normal/Fast/Tank not changed.");
        s.AppendLine("INFO: Predicted Gold is NOT actual match income; use Play Mode comparison.");
        return s.ToString();
    }

    private static WaveManager FindWave()
    {
        WaveManager[] all = UnityEngine.Object.FindObjectsByType<WaveManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        WaveManager wave = null;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].gameObject.scene != SceneManager.GetActiveScene()) continue;
            if (wave != null) return null;
            wave = all[i];
        }
        return wave;
    }

    private static MS7A2AdvancedBalanceProfile GetOrCreateProfile()
    {
        MS7A2AdvancedBalanceProfile existing = AssetDatabase.LoadAssetAtPath<MS7A2AdvancedBalanceProfile>(AssetPath);
        if (existing != null) return existing;
        EnsureFolders("Assets/MonsterSurvival/Balancing7A2/Data");
        MS7A2AdvancedBalanceProfile asset = ScriptableObject.CreateInstance<MS7A2AdvancedBalanceProfile>();
        for (int i = 1; i <= 5; i++)
        {
            MS7A2AdvancedBalanceProfile.WaveRow r = new MS7A2AdvancedBalanceProfile.WaveRow();
            r.wave = i;
            r.healerSpawnInterval = new float[] { 18f, 16f, 14f, 13f, 13f }[i - 1];
            r.maxActiveHealers = 1;
            r.healerSpawnsPerWave = new int[] { 1, 1, 2, 2, 2 }[i - 1];
            r.healerHealAmount = new float[] { 3f, 3f, 4f, 5f, 5f }[i - 1];
            r.healerHealInterval = new float[] { 3.4f, 3.2f, 3f, 2.8f, 2.8f }[i - 1];
            r.summonerSpawnInterval = new float[] { 20f, 20f, 20f, 18f, 18f }[i - 1];
            r.maxActiveSummoners = 1;
            r.summonerSpawnsPerWave = 1;
            r.summonInterval = new float[] { 14f, 14f, 13f, 12f, 12f }[i - 1];
            r.minionsPerCast = 1;
            r.maxActiveMinions = 2;
            r.rangedAttackInterval = new float[] { 3f, 2.8f, 2.6f, 2.4f, 2.2f }[i - 1];
            r.rangedProjectileDamage = new float[] { 6f, 6f, 7f, 8f, 9f }[i - 1];
            r.specialGoldMultiplier = new float[] { 1f, 1f, 1.05f, 1.15f, 1.2f }[i - 1];
            r.specialExpMultiplier = new float[] { 1f, 1f, 1.10f, 1.15f, 1.25f }[i - 1];
            r.eliteDamageMultiplier = new float[] { 1f, 1.05f, 1.10f, 1.15f, 1.2f }[i - 1];
            asset.waves.Add(r);
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

    private static string BackupScene(string scenePath)
    {
        string root = Path.GetDirectoryName(Application.dataPath);
        string source = Path.Combine(root, scenePath);
        if (!File.Exists(source)) throw new IOException("Scene must be saved before install.");
        string folder = Path.Combine(root, "MS_Backups", "7A2");
        Directory.CreateDirectory(folder);
        string dest = Path.Combine(folder, "SampleScene_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity");
        File.Copy(source, dest, false);
        return dest;
    }
}
#endif
