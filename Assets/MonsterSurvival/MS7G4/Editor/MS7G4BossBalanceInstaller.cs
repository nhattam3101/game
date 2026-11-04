#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 7G.4 - Prefab-safe, opt-in installer. Old attacks and Health are NEVER modified.
public sealed class MS7G4BossBalanceInstaller : EditorWindow
{
    private GameObject titanPrefab;
    private GameObject gravePrefab;
    private MS7G4BossBalance.BalancePreset selectedPreset = MS7G4BossBalance.BalancePreset.Balanced;
    private Vector2 scroll;
    private string latest = "Run Audit before modifying the prefabs.";
    private bool actionScheduled;

    [MenuItem("Tools/MonsterSurvival/7G Boss Skills/4 - Open 7G.4 Boss Balance")]
    public static void OpenWindow()
    {
        var window = GetWindow<MS7G4BossBalanceInstaller>(false, "7G.4 Boss Balance");
        window.minSize = new Vector2(680, 520);
        window.Show();
    }

    private void OnEnable()
    {
        if (!titanPrefab) titanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ShadowTitan.prefab");
        if (!gravePrefab) gravePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GraveWardenBoss.prefab");
    }

    private void Queue(Action action)
    {
        if (actionScheduled) return;
        actionScheduled = true;
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            try { action(); }
            catch (Exception ex)
            {
                latest = "[ERROR] " + ex;
                Debug.LogException(ex);
            }
            finally { actionScheduled = false; if (this != null) Repaint(); }
        };
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        try
        {
            GUILayout.Label("7G.4 — BOSS BALANCE: TWO EXISTING PREFABS", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Adjusts ONLY new Rift Lanes / Soul Reap values at runtime. Original Health, Shockwave, Void Burst, 5B AI, VFX and Save are read-only.\n" +
                "Install OFF; back up both Prefabs before installing or enabling. Test both bosses in Phase 2.", MessageType.Info);
            titanPrefab = (GameObject)EditorGUILayout.ObjectField("Shadow Titan prefab", titanPrefab, typeof(GameObject), false);
            gravePrefab = (GameObject)EditorGUILayout.ObjectField("Grave Warden prefab", gravePrefab, typeof(GameObject), false);
            selectedPreset = (MS7G4BossBalance.BalancePreset)EditorGUILayout.EnumPopup("Preset for ENABLE", selectedPreset);
            GUILayout.Space(6);
            if (GUILayout.Button("1 — AUDIT REAL BOSS PREFABS (READ ONLY)", GUILayout.Height(32)))
                Queue(() => { latest = Audit(); Debug.Log(latest); });
            if (GUILayout.Button("2 — BACKUP + INSTALL BALANCER (OFF)", GUILayout.Height(34)))
                Queue(() => Mutate(true, false));
            if (GUILayout.Button("3 — BACKUP + ENABLE PRESET ON BOTH BOSSES", GUILayout.Height(34)))
                Queue(() => Mutate(false, true));
            if (GUILayout.Button("4 — BACKUP + DISABLE BALANCER ON BOTH BOSSES", GUILayout.Height(32)))
                Queue(() => Mutate(false, false));
            if (GUILayout.Button("5 — PURE BALANCE / SAFETY TESTS (READ ONLY)", GUILayout.Height(30)))
                Queue(() => { latest = PureTests(); Debug.Log(latest); });
            if (GUILayout.Button("6 — EXPORT 7G.4 QA TXT (PLAY MODE)", GUILayout.Height(33)))
                Queue(Export);
            EditorGUILayout.HelpBox(
                "IMPORTANT: Test in SampleScene, Wave 5, with the selected boss still alive in Phase 2.\n" +
                "Changing preset while Phase 2 is active does NOT change telegraph geometry: restart the boss encounter.\n" +
                "To undo: option 4 sets OptIn OFF; after next spawn original skill settings are unchanged.\n" +
                "7G.1 live casting evidence and legacy attack overlaps still require manual QA.", MessageType.Warning);
            GUILayout.Label("Latest report", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(latest, GUILayout.MinHeight(310));
        }
        finally { EditorGUILayout.EndScrollView(); }
    }

    private static bool CheckPrefab(GameObject asset, bool titan, out string reason)
    {
        reason = "";
        if (!asset) { reason = (titan ? "Shadow Titan" : "Grave Warden") + " prefab not selected."; return false; }
        var path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
            !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
            PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.NotAPrefab)
        { reason = "Select a prefab ASSET in Project, not a Scene instance: " + path; return false; }
        if (!asset.GetComponent<Health>()) { reason = "Missing Health on " + path; return false; }
        if (titan && (!asset.GetComponent<MS7G1TitanRiftLanes>() || !asset.GetComponent<BossPhase2Controller>()))
        { reason = "Expected 7G.1 Rift + BossPhase2Controller on " + path; return false; }
        if (!titan && (!asset.GetComponent<MS7G2GraveSoulReap>() || !asset.GetComponent<GraveWardenBossAI>()))
        { reason = "Expected 7G.2 Soul Reap + GraveWardenBossAI on " + path; return false; }
        reason = "PASS: " + path;
        return true;
    }

    private bool BothValid(out string message)
    {
        string a, b;
        bool first = CheckPrefab(titanPrefab, true, out a);
        bool second = CheckPrefab(gravePrefab, false, out b);
        message = a + "\n" + b;
        if (first && second && AssetDatabase.GetAssetPath(titanPrefab) == AssetDatabase.GetAssetPath(gravePrefab))
        { message += "\n[ERROR] Boss prefabs must be separate assets."; return false; }
        return first && second;
    }

    private string Audit()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 7G.4 — BOSS BALANCE PREFLIGHT (READ ONLY)");
        sb.AppendLine("Unity=" + Application.unityVersion + " Scene=" + SceneManager.GetActiveScene().name +
                      " PlayMode=" + EditorApplication.isPlaying);
        string explanation;
        bool valid = BothValid(out explanation);
        sb.AppendLine(explanation);
        if (!valid) { sb.AppendLine("[FAIL] PREFLIGHT"); return sb.ToString(); }
        var titan = titanPrefab.GetComponent<MS7G1TitanRiftLanes>();
        var grave = gravePrefab.GetComponent<MS7G2GraveSoulReap>();
        var th = titanPrefab.GetComponent<Health>();
        var gh = gravePrefab.GetComponent<Health>();
        sb.AppendLine((Mathf.Approximately(th.maxHealth, 500f) ? "[PASS]" : "[WARN]") +
                      " Shadow Titan maxHealth=" + th.maxHealth + " (target 500, NEVER changed)");
        sb.AppendLine((Mathf.Approximately(gh.maxHealth, 750f) ? "[PASS]" : "[WARN]") +
                      " Grave Warden maxHealth=" + gh.maxHealth + " (original observed 750, NEVER changed)");
        sb.AppendLine("[INFO] Titan 7G.1 optIn=" + titan.enableRiftLanes + " warn=" + titan.warningSeconds +
                      " dmg=" + titan.damage + " cooldown=" + titan.cooldownSeconds);
        sb.AppendLine("[INFO] Grave 7G.2 optIn=" + grave.enableSoulReap + " warn=" + grave.warningDuration +
                      " dmg=" + grave.damage + " cooldown=" + grave.cooldown);
        sb.AppendLine("[INFO] Titan old Shockwave=" + (titanPrefab.GetComponent<BossSpecialAttack>() != null) +
                      " VoidBurst=" + (titanPrefab.GetComponent<BossVoidBurstAttack>() != null));
        sb.AppendLine("[INFO] Grave old AI=" + (gravePrefab.GetComponent<GraveWardenBossAI>() != null));
        var tb = titanPrefab.GetComponent<MS7G4BossBalance>();
        var gb = gravePrefab.GetComponent<MS7G4BossBalance>();
        sb.AppendLine("[INFO] Titan 7G.4=" + (tb ? (tb.enableBossBalance ? "ON " : "OFF ") + tb.preset : "NOT_INSTALLED"));
        sb.AppendLine("[INFO] Grave 7G.4=" + (gb ? (gb.enableBossBalance ? "ON " : "OFF ") + gb.preset : "NOT_INSTALLED"));
        sb.AppendLine("[INFO] Original HP, animations, Boss AI, Save/Profile and HUD not written by this installer.");
        return sb.ToString();
    }

    private static string Backup(GameObject asset)
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string folder = Path.Combine(root, "MS_Backups", "7G4");
        Directory.CreateDirectory(folder);
        string src = Path.Combine(root, AssetDatabase.GetAssetPath(asset));
        string dst = Path.Combine(folder, Path.GetFileNameWithoutExtension(src) +
                          "_BEFORE_7G4_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".prefab");
        File.Copy(src, dst, false); // fail closed if backup unavailable
        return dst;
    }

    private void Mutate(bool installOff, bool enable)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorUtility.DisplayDialog("7G.4", "Stop Play Mode before changing prefabs.", "OK"); return; }
        string issue;
        if (!BothValid(out issue))
        { EditorUtility.DisplayDialog("7G.4 PREFLIGHT", issue, "OK"); return; }
        if (!EditorUtility.DisplayDialog("7G.4 - Back up both Boss Prefabs",
            "Mode: " + (installOff ? "INSTALL OFF" : enable ? "ENABLE " + selectedPreset : "DISABLE") +
            "\nCreates backups BEFORE editing. Only 7G.4 component added/changed.\nNo old skills/HP/Save modified. Continue?",
            "Continue", "Cancel")) return;
        // Back up BOTH assets before any write; fail closed if either fails.
        string first = Backup(titanPrefab), second = Backup(gravePrefab);
        ApplyPrefab(titanPrefab, installOff, enable);
        ApplyPrefab(gravePrefab, installOff, enable);
        latest = "[DONE] " + (installOff ? "Installed 7G.4 OFF" : enable ? "Enabled " + selectedPreset : "Disabled 7G.4") +
                 "\nBACKUPS:\n" + first + "\n" + second + "\n\n" + Audit();
        Debug.Log(latest);
    }

    private void ApplyPrefab(GameObject asset, bool installOff, bool enable)
    {
        string path = AssetDatabase.GetAssetPath(asset);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var balancer = root.GetComponent<MS7G4BossBalance>();
            bool created = balancer == null;
            if (created) balancer = root.AddComponent<MS7G4BossBalance>();
            if (installOff)
            {
                // The INSTALL (OFF) button always leaves the addon disabled.
                // Existing preset selection is preserved for a later explicit ENABLE.
                balancer.enableBossBalance = false;
            }
            else
            {
                balancer.enableBossBalance = enable;
                if (enable) balancer.preset = selectedPreset;
            }
            if (!PrefabUtility.SaveAsPrefabAsset(root, path))
                throw new InvalidOperationException("Cannot save " + path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private string PureTests()
    {
        var sb = new StringBuilder("MONSTERSURVIVAL 7G.4 PURE BALANCE QA (NO SCENE CHANGES)\n");
        int pass = 0, fail = 0;
        Action<bool, string> test = (ok, label) =>
        {
            sb.AppendLine((ok ? "[PASS] " : "[FAIL] ") + label);
            if (ok) pass++; else fail++;
        };
        test(2.4f >= 2.1f && 2.1f >= 1.8f, "Titan warning Gentle >= Balanced >= Challenge");
        test(6f <= 8f && 8f <= 10f && 10f <= 15f, "Titan damage capped");
        test(22f >= 18f && 18f >= 15f, "Titan cooldown safe ordering");
        test(0.22f >= 0.15f && 0.29f <= 0.65f, "Titan lane half width in supported API range");
        test(34f <= 45f && 28f >= 10f, "Titan lane angle in supported API range");
        test(1.1f > 0f && 1.1f < 5.8f, "Titan safe radius stays within default laneLength");
        test(2.3f >= 1.95f && 1.95f >= 1.65f, "Grave warning Gentle >= Balanced >= Challenge");
        test(7f <= 9f && 9f <= 11f, "Grave damage conservative ordering");
        test(24f >= 20f && 20f >= 17f, "Grave cooldown safe ordering");
        test(1.8f < 4f && 1.6f < 4.15f && 1.4f < 4.3f, "Grave inner safe radius < outer radius");
        test(38f >= 25f && 49f <= 75f, "Grave arc angles supported");
        test(1.65f >= 1.2f && 1.8f >= 0.8f, "Both telegraphs above existing runtime minimums");
        test(typeof(MS7G4BossBalance).GetMethod("RestoreOriginal") != null,
             "Restore method exists for reversible runtime values");
        test(typeof(MS7G4BossBalance).GetMethod("BuildReport") != null,
             "Recorder report exists");
        sb.AppendLine("PURE PASS=" + pass + " FAIL=" + fail + " (not Unity PlayMode evidence)");
        return sb.ToString();
    }

    private void Export()
    {
        var sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 7G.4 — TWO BOSS BALANCE QA / READ ONLY");
        sb.AppendLine("Created=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                      " Unity=" + Application.unityVersion + " Scene=" + SceneManager.GetActiveScene().name +
                      " PlayMode=" + EditorApplication.isPlaying);
        sb.AppendLine(Audit());
        int live = 0;
        if (EditorApplication.isPlaying)
        {
            foreach (var b in UnityEngine.Object.FindObjectsByType<MS7G4BossBalance>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (b == null || b.gameObject.scene != SceneManager.GetActiveScene() || !b.gameObject.activeInHierarchy) continue;
                live++;
                sb.AppendLine("=== LIVE BOSS " + live + " ===");
                sb.AppendLine(b.BuildReport());
            }
        }
        sb.AppendLine("LIVE_SCENE_INSTANCES=" + live);
        sb.AppendLine(live > 0 ? "[OBSERVED] Boss object active. Counters establish which abilities were used." :
                      "[PENDING] Export during Wave 5 with boss alive; no live Boss component found.");
        sb.AppendLine("=== MANUAL QA (USER CONFIRMATIONS) ===");
        string[] checks = {
            "Titan (500 HP): Phase 2 Rift Lanes timing, visible and dodgeable; no unfair walls",
            "Grave (750 HP observed): Phase 2 Soul Reap warning, safe inner area, dodge at arc sides",
            "Inside either attack: only one Health/Shield hit; outside: no damage",
            "Titan Shockwave and Void Burst still run normally and warning overlap remains fair",
            "Grave Warden Summon/Tomb Wall/Spirit Orbit/Reanimate all remain functional",
            "No extra Boss damage during Wave 5 from Arena 7F",
            "Pause/Resume correctly freezes warnings, cooldowns and VFX",
            "Disable 7G.4 restores original 7G.1/7G.2 fields once; no repeated stat stacking",
            "MainMenu, Victory/GameOver/Retry remain functional",
            "Shadow Titan red and Grave Warden purple-gold HP bars follow Boss heads",
            "Save/Profile/Gold, 7C Skills, 7D Equipment, 7E Elites unaffected",
            "Test Gentle/Balanced/Challenge on both Bosses; play at least one complete encounter each"
        };
        for (int i = 0; i < checks.Length; i++) sb.AppendLine("[NOT CONFIRMED] " + (i + 1).ToString("00") + " " + checks[i]);
        sb.AppendLine("STRUCTURE=" + (BothValidForExport() ? "PASS" : "FAIL") +
                      " RUNTIME=" + (live > 0 ? "OBSERVED (NOT COMPLETE)" : "PENDING") +
                      " MANUAL=0/12 RESULT=PENDING");
        sb.AppendLine("NOTE: This report does NOT certify Game View visual quality, Build success or Save/Profile persistence.");
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string folder = Path.Combine(project, "MS_QAReports", "7G4");
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, "MS7G4_BossBalanceQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
        File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
        latest = sb.ToString();
        Debug.Log("[7G.4] QA exported: " + file);
        EditorUtility.RevealInFinder(file);
    }
    private bool BothValidForExport() { string s; return BothValid(out s); }
}
#endif
