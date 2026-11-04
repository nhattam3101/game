#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Edits only two explicitly confirmed prefab assets, adding ONE agent to each.
// No WaveManager/Health/AI/SafeSpawn/Victory/Save scripts are replaced.
public sealed class MS7A3BossBalanceInstaller : EditorWindow
{
    private const string MenuRoot = "Tools/MonsterSurvival/7A Boss Balancing/";
    private const string DataDir = "Assets/MonsterSurvival/Features/Combat/Balancing7A3/Data";
    private const string TitanDataPath = DataDir + "/ShadowTitan_Balance.asset";
    private const string WardenDataPath = DataDir + "/GraveWarden_Balance.asset";
    private GameObject titanPrefab;
    private GameObject wardenPrefab;
    private Vector2 scroll;

    [MenuItem(MenuRoot + "9 - OPEN 7A.3 Boss Balance Installer")]
    public static void OpenWindow()
    {
        GetWindow<MS7A3BossBalanceInstaller>("7A.3 Boss Balance").Show();
    }

    private void OnEnable() { SuggestPrefabs(); }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        GUILayout.Label("MONSTERSURVIVAL / 7A.3 BOSS BALANCE", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Select the EXACT 2 boss PREFAB assets used by your game. Installer adds a PREVIEW-ONLY component to each prefab. No rewards are paid by default and no gameplay/save scripts are changed.", MessageType.Info);
        titanPrefab = (GameObject)EditorGUILayout.ObjectField("Shadow Titan Prefab", titanPrefab, typeof(GameObject), false);
        wardenPrefab = (GameObject)EditorGUILayout.ObjectField("Grave Warden Prefab", wardenPrefab, typeof(GameObject), false);
        if (GUILayout.Button("FIND LIKELY BOSS PREFABS (REVIEW)", GUILayout.Height(27))) SuggestPrefabs();
        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox("Required components on prefab ROOT:\nTitan: Health + BossSpecialAttack or BossVoidBurstAttack\nWarden: Health + GraveWardenBossAI.\nIf your bosses use variants, do not install on an unrelated prefab.", MessageType.Warning);
        EditorGUILayout.Space(8);
        GUILayout.Label("DEFAULT SAFETY", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Apply At Runtime: OFF (Preview Only)");
        EditorGUILayout.LabelField("Standalone Gold/EXP: OFF (never double-award by default)");
        EditorGUILayout.LabelField("Original prefab fields: NOT rewritten; only adds agent");
        EditorGUILayout.Space(8);
        if (GUILayout.Button("INSTALL / RE-LINK BOTH BOSS AGENTS", GUILayout.Height(38))) Install();
        if (GUILayout.Button("AUDIT SELECTED PREFABS (READ ONLY)", GUILayout.Height(30))) Debug.Log(BuildReport());
        if (GUILayout.Button("EXPORT 7A.3 REPORT TXT", GUILayout.Height(30))) ExportReport();
        if (GUILayout.Button("REMOVE AGENTS FROM SELECTED PREFABS", GUILayout.Height(30))) Remove();
        EditorGUILayout.HelpBox("After INSTALL, open each prefab, find MS7A3BossBalanceAgent and choose Apply At Runtime ON only for the boss you want to test. Compare by turning it OFF and restarting Play Mode.", MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    private void SuggestPrefabs()
    {
        int bestTitan = 0, bestWarden = 0;
        GameObject foundTitan = null, foundWarden = null;
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.IndexOf("/Backup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("/Demo/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("/Test/", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponent<Health>() == null) continue;
            string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            int titanScore = (name.Contains("shadow") ? 5 : 0) + (name.Contains("titan") ? 5 : 0);
            int wardenScore = (name.Contains("grave") ? 5 : 0) + (name.Contains("warden") ? 5 : 0);
            if (titanScore > bestTitan && titanScore >= 5 && IsTitan(prefab))
            { bestTitan = titanScore; foundTitan = prefab; }
            if (wardenScore > bestWarden && wardenScore >= 5 && IsWarden(prefab))
            { bestWarden = wardenScore; foundWarden = prefab; }
        }
        if (foundTitan != null) titanPrefab = foundTitan;
        if (foundWarden != null) wardenPrefab = foundWarden;
        Repaint();
    }

    private static bool IsTitan(GameObject p)
    {
        return p != null && p.GetComponent<Health>() != null &&
               (p.GetComponent<BossSpecialAttack>() != null || p.GetComponent<BossVoidBurstAttack>() != null) &&
               p.GetComponent<GraveWardenBossAI>() == null;
    }

    private static bool IsWarden(GameObject p)
    {
        return p != null && p.GetComponent<Health>() != null && p.GetComponent<GraveWardenBossAI>() != null;
    }

    private bool ValidateSelection(out string titanPath, out string wardenPath, out string issue)
    {
        titanPath = titanPrefab != null ? AssetDatabase.GetAssetPath(titanPrefab) : string.Empty;
        wardenPath = wardenPrefab != null ? AssetDatabase.GetAssetPath(wardenPrefab) : string.Empty;
        issue = null;
        if (string.IsNullOrEmpty(titanPath) || string.IsNullOrEmpty(wardenPath) ||
            !titanPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
            !wardenPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            issue = "Please select TWO real prefab assets in the Project window.";
        else if (titanPath == wardenPath)
            issue = "Both boss slots point to the SAME prefab.";
        else if (!IsTitan(titanPrefab))
            issue = "Shadow Titan root must have Health + Shockwave/VoidBurst and NOT GraveWardenBossAI.";
        else if (!IsWarden(wardenPrefab))
            issue = "Grave Warden root must have Health + GraveWardenBossAI.";
        return issue == null;
    }

    private void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Alert("Stop Play Mode before installing."); return; }
        string titanPath, wardenPath, issue;
        if (!ValidateSelection(out titanPath, out wardenPath, out issue))
        { Alert(issue); return; }
        if (!EditorUtility.DisplayDialog("7A.3: Confirm boss prefabs",
            "Add 7A.3 agents to:\n" + titanPath + "\n" + wardenPath +
            "\n\nPrefab copies will be backed up OUTSIDE Assets. New agents start in Preview Only. Existing agents keep their current Apply At Runtime setting.\n\nContinue?",
            "INSTALL", "CANCEL")) return;
        try
        {
            BackupPrefabs(new[] { titanPath, wardenPath });
            MS7A3BossBalanceProfile titan = GetOrCreateProfile(TitanDataPath, MS7A3BossBalanceProfile.BossKind.ShadowTitan);
            MS7A3BossBalanceProfile warden = GetOrCreateProfile(WardenDataPath, MS7A3BossBalanceProfile.BossKind.GraveWarden);
            InstallOnPrefab(titanPath, titan);
            InstallOnPrefab(wardenPath, warden);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[7A.3 INSTALL] Added/linked boss agents. PREVIEW ONLY on fresh installs. Existing prefabs were backed up.\n" + BuildReport());
        }
        catch (Exception e) { Debug.LogError("[7A.3 INSTALL] " + e); Alert(e.Message); }
    }

    private static void InstallOnPrefab(string path, MS7A3BossBalanceProfile profile)
    {
        GameObject instance = PrefabUtility.LoadPrefabContents(path);
        try
        {
            MS7A3BossBalanceAgent agent = instance.GetComponent<MS7A3BossBalanceAgent>();
            bool fresh = agent == null;
            if (fresh) agent = instance.AddComponent<MS7A3BossBalanceAgent>();
            agent.profile = profile;
            if (fresh)
            {
                agent.applyAtRuntime = false;
                agent.grantStandaloneIfNoEnemyController = false;
                agent.logBalance = true;
            }
            PrefabUtility.SaveAsPrefabAsset(instance, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(instance); }
    }

    private static MS7A3BossBalanceProfile GetOrCreateProfile(string path, MS7A3BossBalanceProfile.BossKind kind)
    {
        MS7A3BossBalanceProfile old = AssetDatabase.LoadAssetAtPath<MS7A3BossBalanceProfile>(path);
        if (old != null)
        {
            if (old.boss != kind) throw new InvalidOperationException("Existing asset has wrong boss kind: " + path);
            return old;
        }
        EnsureFolder(DataDir);
        MS7A3BossBalanceProfile p = ScriptableObject.CreateInstance<MS7A3BossBalanceProfile>();
        p.boss = kind;
        if (kind == MS7A3BossBalanceProfile.BossKind.ShadowTitan)
        {
            p.maxHpMultiplier = 1.10f;
            p.shockwaveDamageMultiplier = 0.95f;
            p.shockwaveCooldownMultiplier = 1.05f;
            p.shockwaveWarningMultiplier = 1.10f;
            p.voidBurstDamageMultiplier = 0.95f;
            p.voidBurstCooldownMultiplier = 1.05f;
            p.voidBurstWarningMultiplier = 1.10f;
            p.goldMultiplier = 1.15f;
            p.expMultiplier = 1.15f;
            p.standaloneGold = 30;
            p.standaloneExp = 60;
        }
        else
        {
            p.maxHpMultiplier = 1.10f;
            p.wardenCooldownMultiplier = 1.05f;
            p.wardenGapMultiplier = 1.10f;
            p.wardenMovementMultiplier = 1.0f;
            p.goldMultiplier = 1.20f;
            p.expMultiplier = 1.20f;
            p.standaloneGold = 40;
            p.standaloneExp = 80;
        }
        AssetDatabase.CreateAsset(p, path);
        return p;
    }

    private static void EnsureFolder(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static void BackupPrefabs(string[] paths)
    {
        string root = Path.GetDirectoryName(Application.dataPath);
        string folder = Path.Combine(root, "MS_Backups", "7A3", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
        Directory.CreateDirectory(folder);
        foreach (string path in paths)
        {
            string absolute = Path.Combine(root, path);
            if (!File.Exists(absolute)) throw new IOException("Cannot back up prefab: " + path);
            string dest = Path.Combine(folder, Path.GetFileNameWithoutExtension(path) + "_" +
                Guid.NewGuid().ToString("N").Substring(0, 8) + ".prefab");
            File.Copy(absolute, dest, false);
        }
        Debug.Log("[7A.3] Original prefabs backed up to " + folder);
    }

    private void Remove()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Alert("Stop Play Mode first."); return; }
        string titanPath, wardenPath, issue;
        if (!ValidateSelection(out titanPath, out wardenPath, out issue)) { Alert(issue); return; }
        if (!EditorUtility.DisplayDialog("REMOVE 7A.3", "Remove ONLY MS7A3BossBalanceAgent from the TWO selected prefabs? Original AI, Health, reward, loot, profiles, Save and 7A.1/7A.2 remain.", "REMOVE", "CANCEL")) return;
        try
        {
            BackupPrefabs(new[] { titanPath, wardenPath });
            RemoveFromPrefab(titanPath);
            RemoveFromPrefab(wardenPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[7A.3 REMOVE] Boss agents removed; data assets retained.");
        }
        catch (Exception e) { Debug.LogError(e); Alert(e.Message); }
    }

    private static void RemoveFromPrefab(string path)
    {
        GameObject instance = PrefabUtility.LoadPrefabContents(path);
        try
        {
            MS7A3BossBalanceAgent agent = instance.GetComponent<MS7A3BossBalanceAgent>();
            if (agent != null) UnityEngine.Object.DestroyImmediate(agent);
            PrefabUtility.SaveAsPrefabAsset(instance, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(instance); }
    }

    private void ExportReport()
    {
        string output = EditorUtility.SaveFilePanel("Export 7A.3 Boss Balance QA", "", "MS7A3_BossBalance_Audit", "txt");
        if (string.IsNullOrEmpty(output)) return;
        File.WriteAllText(output, BuildReport(), Encoding.UTF8);
        Debug.Log("[7A.3] Audit exported to " + output);
    }

    private string BuildReport()
    {
        StringBuilder b = new StringBuilder();
        int pass = 0, warn = 0, error = 0;
        b.AppendLine("MONSTERSURVIVAL 7A.3 - BOSS DIFFICULTY & REWARD BALANCE / READ-ONLY");
        b.AppendLine("Unity " + Application.unityVersion + " | " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        string titanPath, wardenPath, issue;
        if (!ValidateSelection(out titanPath, out wardenPath, out issue))
        { b.AppendLine("ERROR: " + issue); return b.ToString(); }
        b.AppendLine("PASS: Two different, plausible boss prefabs selected."); pass++;
        AppendBossAudit(b, titanPrefab, titanPath, MS7A3BossBalanceProfile.BossKind.ShadowTitan, ref pass, ref warn, ref error);
        AppendBossAudit(b, wardenPrefab, wardenPath, MS7A3BossBalanceProfile.BossKind.GraveWarden, ref pass, ref warn, ref error);
        b.AppendLine("NOTE: PREVIEW ONLY means ZERO gameplay changes. Re-enter Play Mode after changing ApplyAtRuntime.");
        b.AppendLine("NOTE: Grave Warden child skill damage fields are NOT guessed. Optional public float damage bindings require explicit developer verification.");
        b.AppendLine("NOTE: 7A.3 does not modify Boss HP bars, boss phases, achievements, equipment drops, WaveManager, 7A.1/7A.2, or Save.");
        b.AppendLine("NOTE: STANDALONE reward is an OPT-IN. Validate all other reward scripts before enabling it.");
        b.AppendLine("RESULT: " + pass + " PASS / " + warn + " WARN / " + error + " ERROR. Play Mode QA still required.");
        return b.ToString();
    }

    private static void AppendBossAudit(StringBuilder b, GameObject bossPrefab, string path, MS7A3BossBalanceProfile.BossKind expected, ref int pass, ref int warn, ref int error)
    {
        b.AppendLine("\n--- " + expected + " ---\nPrefab: " + path);
        MS7A3BossBalanceAgent a = bossPrefab.GetComponent<MS7A3BossBalanceAgent>();
        if (a == null) { b.AppendLine("WARN: Agent not installed yet."); warn++; return; }
        b.AppendLine("PASS: Exactly one agent on boss prefab root (DisallowMultipleComponent)."); pass++;
        if (a.profile == null || a.profile.boss != expected)
        { b.AppendLine("ERROR: Profile missing or wrong boss kind."); error++; return; }
        string reason;
        bool valid = a.profile.Validate(out reason);
        b.AppendLine((valid ? "PASS: " : "ERROR: ") + reason);
        if (valid) pass++; else error++;
        b.AppendLine("MODE: " + (a.applyAtRuntime ? "ACTIVE" : "PREVIEW ONLY"));
        if (a.applyAtRuntime) { b.AppendLine("WARN: Game will apply tuning during next Boss spawn."); warn++; }
        Health hp = bossPrefab.GetComponent<Health>();
        if (hp == null) { b.AppendLine("ERROR: No Health on boss root."); error++; return; }
        b.AppendLine("HP prefab baseline=" + hp.maxHealth.ToString("0.##") +
                     " -> expected runtime=" + (hp.maxHealth * a.profile.maxHpMultiplier).ToString("0.##"));
        if (expected == MS7A3BossBalanceProfile.BossKind.ShadowTitan)
        {
            var shock = bossPrefab.GetComponent<BossSpecialAttack>();
            var burst = bossPrefab.GetComponent<BossVoidBurstAttack>();
            b.AppendLine("Shockwave=" + (shock != null ? "YES" : "NO") + " | VoidBurst=" + (burst != null ? "YES" : "NO"));
            if (shock != null)
            {
                b.AppendLine("Shockwave damage=" + shock.shockwaveDamage + " -> " + (shock.shockwaveDamage * a.profile.shockwaveDamageMultiplier) +
                    "; cooldown=" + shock.cooldown + " -> " + (shock.cooldown * a.profile.shockwaveCooldownMultiplier));
                pass++;
            }
            if (burst != null)
            {
                b.AppendLine("VoidBurst damage/zone=" + burst.damagePerZone + " -> " + (burst.damagePerZone * a.profile.voidBurstDamageMultiplier) +
                    "; cooldown=" + burst.castInterval + " -> " + (burst.castInterval * a.profile.voidBurstCooldownMultiplier));
                pass++;
            }
            if (shock == null && burst == null) { b.AppendLine("ERROR: neither verified Shadow attack script."); error++; }
        }
        else
        {
            var w = bossPrefab.GetComponent<GraveWardenBossAI>();
            if (w == null) { b.AppendLine("ERROR: Missing GraveWardenBossAI."); error++; }
            else
            {
                b.AppendLine("Warden cooldowns (summon/tomb/orbit/reanimate)=" + w.summonCooldown + "/" + w.tombWallCooldown +
                    "/" + w.spiritOrbitCooldown + "/" + w.reanimateCooldown +
                    " | all x" + a.profile.wardenCooldownMultiplier + ". Movement x" + a.profile.wardenMovementMultiplier);
                pass++;
            }
            if (a.additionalDamageFields == null || a.additionalDamageFields.Count == 0)
            {
                b.AppendLine("INFO: Grave Warden damage not modified (no verified optional bindings). This is intentional.");
            }
        }
        if (a.additionalDamageFields != null)
        {
            HashSet<string> fieldKeys = new HashSet<string>();
            foreach (MS7A3BossBalanceAgent.OptionalDamageBinding bind in a.additionalDamageFields)
            {
                string why;
                bool ok = a.IsValidBinding(bind, out why);
                if (ok)
                {
                    string key = bind.component.GetEntityId() + ":" + bind.publicFloatFieldName;
                    if (!fieldKeys.Add(key)) { ok = false; why = "duplicate component + field (skipped at runtime)"; }
                }
                b.AppendLine((ok ? "PASS" : "WARN") + ": optional damage binding " +
                    (bind != null ? bind.publicFloatFieldName : "<null>") + ": " + why);
                if (ok) pass++; else warn++;
            }
        }
        EnemyController rewards = bossPrefab.GetComponent<EnemyController>();
        if (rewards != null)
        {
            if (!rewards.enabled)
            {
                b.AppendLine("WARN: EnemyController exists but is DISABLED; its normal OnDied reward might not be registered. Check in Play Mode.");
                warn++;
            }
            b.AppendLine("Existing EnemyController reward=" + rewards.coinReward + " Gold / " + rewards.expReward +
                " EXP -> " + Mathf.RoundToInt(rewards.coinReward * a.profile.goldMultiplier) + " / " +
                Mathf.RoundToInt(rewards.expReward * a.profile.expMultiplier) + " (no duplicate OnDied subscription)");
            pass++;
        }
        else
        {
            b.AppendLine("WARN: No EnemyController reward source. Default: NO GOLD/EXP ADDED by 7A.3. Standalone reward requires deliberate opt-in.");
            warn++;
        }
        if (a.grantStandaloneIfNoEnemyController)
        {
            b.AppendLine("WARN: Standalone Gold/EXP opt-in is ON. Verify that no other boss death listener grants Gold/EXP.");
            warn++;
        }
    }

    private static void Alert(string message) { EditorUtility.DisplayDialog("MonsterSurvival 7A.3", message, "OK"); }
}
#endif
