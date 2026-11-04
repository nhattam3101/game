#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// 7G.1 installer: edits only user-selected Shadow Titan prefab, always backs it up.
// Uses delayed Editor callbacks to avoid IMGUI EndLayoutGroup errors.
public sealed class MS7G1TitanRiftInstaller : EditorWindow
{
    private GameObject selectedBossPrefab;
    private bool busy;
    private Vector2 scroll;
    private string latest = "Choose the real Shadow Titan Boss PREFAB from Project, then AUDIT.";

    [MenuItem("Tools/MonsterSurvival/7G Boss Skills/1 - Open 7G.1 Shadow Rift Lanes")]
    public static void OpenWindow()
    {
        MS7G1TitanRiftInstaller w = GetWindow<MS7G1TitanRiftInstaller>("7G.1 Titan Skills");
        w.minSize = new Vector2(610, 500);
        w.Show();
    }

    private void OnEnable()
    {
        if (selectedBossPrefab == null) selectedBossPrefab = AutoDetect();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        try
        {
            GUILayout.Label("7G.1 - SHADOW TITAN / RIFT LANES", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Adds 3 telegraphed Rift Lanes (Phase 2 only). One hit check per cast, existing Shield/Health pipeline. " +
                "Does not replace old Shockwave, Void Burst, Boss HP, Wave 5 or Save/Profile.", MessageType.Info);
            selectedBossPrefab = (GameObject)EditorGUILayout.ObjectField("Shadow Titan PREFAB", selectedBossPrefab, typeof(GameObject), false);
            EditorGUI.BeginDisabledGroup(busy);
            if (GUILayout.Button("1 - AUDIT REAL BOSS PREFAB (READ ONLY)", GUILayout.Height(32)))
                Queue(() => { latest = Audit(); Debug.Log(latest); });
            if (GUILayout.Button("2 - BACKUP + INSTALL ADDITIVE COMPONENT (OFF)", GUILayout.Height(32)))
                Queue(() => EditPrefab(EditKind.Install));
            if (GUILayout.Button("3 - BACKUP + ENABLE SKILL ON PREFAB", GUILayout.Height(32)))
                Queue(() => EditPrefab(EditKind.Enable));
            if (GUILayout.Button("4 - BACKUP + DISABLE SKILL ON PREFAB", GUILayout.Height(32)))
                Queue(() => EditPrefab(EditKind.Disable));
            if (GUILayout.Button("5 - EXPORT 7G.1 QA TXT", GUILayout.Height(32)))
                Queue(Export);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.HelpBox("Stop Play Mode for Install / Enable / Disable. Play from MainMenu and export QA while Shadow Titan has spawned in Wave 5. Keep 500 HP unchanged.\n" +
                "A clean 7F.5 gameplay run is assumed but not automatically certified by this package.", MessageType.Warning);
            GUILayout.Label("Audit / latest report", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(latest, GUILayout.MinHeight(280));
        }
        finally { EditorGUILayout.EndScrollView(); }
    }

    private void Queue(Action action)
    {
        if (busy) return;
        busy = true;
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            try { action(); }
            catch (Exception ex) { latest = "[ERROR] " + ex; Debug.LogException(ex); }
            finally { busy = false; if (this != null) Repaint(); }
        };
    }

    private static GameObject AutoDetect()
    {
        GameObject candidate = null;
        int matches = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = Path.GetFileNameWithoutExtension(path).Replace(" ", "").Replace("_", "");
            if (file.IndexOf("ShadowTitan", StringComparison.OrdinalIgnoreCase) < 0) continue;
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponentInChildren<BossPhase2Controller>(true) == null ||
                asset.GetComponentInChildren<Health>(true) == null) continue;
            candidate = asset;
            matches++;
            if (matches > 1) return null; // Require user's choice if multiple variants.
        }
        return matches == 1 ? candidate : null;
    }

    private bool Validate(out string assetPath, out string message)
    {
        assetPath = selectedBossPrefab == null ? "" : AssetDatabase.GetAssetPath(selectedBossPrefab);
        if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            message = "Select a Shadow Titan .prefab ASSET (not a Hierarchy instance).";
            return false;
        }
        Health hp = selectedBossPrefab.GetComponentInChildren<Health>(true);
        BossPhase2Controller phase = selectedBossPrefab.GetComponentInChildren<BossPhase2Controller>(true);
        BossVoidBurstAttack burst = selectedBossPrefab.GetComponentInChildren<BossVoidBurstAttack>(true);
        if (hp == null || phase == null || burst == null)
        {
            message = "Selected Prefab needs Health + BossPhase2Controller + BossVoidBurstAttack on Shadow Titan.";
            return false;
        }
        if (hp.gameObject != phase.gameObject || hp.gameObject != burst.gameObject)
        {
            message = "Health, phase controller and Void Burst must belong to the same Boss GameObject.";
            return false;
        }
        message = "Valid Shadow Titan Boss source at " + assetPath;
        return true;
    }

    private enum EditKind { Install, Enable, Disable }

    private void EditPrefab(EditKind kind)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("7G.1", "Stop Play Mode first. Changes to prefab assets in Play Mode are unsafe.", "OK");
            return;
        }
        string path, message;
        if (!Validate(out path, out message))
        {
            EditorUtility.DisplayDialog("7G.1 Pre-flight", message, "OK");
            latest = "[FAIL] " + message;
            return;
        }
        string actionText = kind == EditKind.Install ? "INSTALL skill (OFF)" :
                            kind == EditKind.Enable ? "ENABLE skill" : "DISABLE skill";
        if (!EditorUtility.DisplayDialog("7G.1 Shadow Titan", "A timestamped backup will be saved OUTSIDE Assets before editing:\n" +
            path + "\n\nAction: " + actionText + "\n\nExisting Health and Boss abilities are not edited. Continue?", "Confirm", "Cancel")) return;

        string backup = BackupAsset(path);
        GameObject root = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents(path);
            Health hp = root.GetComponentInChildren<Health>(true);
            BossPhase2Controller phase = root.GetComponentInChildren<BossPhase2Controller>(true);
            BossVoidBurstAttack voidBurst = root.GetComponentInChildren<BossVoidBurstAttack>(true);
            if (hp == null || phase == null || voidBurst == null || hp.gameObject != phase.gameObject || hp.gameObject != voidBurst.gameObject)
                throw new InvalidOperationException("Prefab contents changed; aborting before writing.");
            MS7G1TitanRiftLanes ability = hp.GetComponent<MS7G1TitanRiftLanes>();
            if (ability == null)
            {
                if (kind != EditKind.Install) throw new InvalidOperationException("Skill missing. First press INSTALL OFF.");
                ability = hp.gameObject.AddComponent<MS7G1TitanRiftLanes>();
                ability.enableRiftLanes = false;
            }
            ability.phaseController = phase;
            if (kind == EditKind.Enable) ability.enableRiftLanes = true;
            if (kind == EditKind.Disable) ability.enableRiftLanes = false;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            AssetDatabase.SaveAssets();
            latest = "[SUCCESS] " + actionText + "\nTarget=" + path + "\nBackup=" + backup +
                     "\nBoss HP max (not modified)=" + hp.maxHealth +
                     "\n" + Audit();
            Debug.Log("[7G.1] " + actionText + " on " + path + ". Backup=" + backup);
        }
        finally
        {
            if (root != null) PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static string BackupAsset(string path)
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string source = Path.Combine(root, path);
        string folder = Path.Combine(root, "MS_Backups", "7G1");
        Directory.CreateDirectory(folder);
        string fileName = Path.GetFileNameWithoutExtension(path);
        string destination = Path.Combine(folder, fileName + "_BEFORE_7G1_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".prefab");
        File.Copy(source, destination, false);
        if (File.Exists(source + ".meta")) File.Copy(source + ".meta", destination + ".meta", false);
        return destination;
    }

    private string Audit()
    {
        var sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 7G.1 - SHADOW TITAN RIFT LANES / READ ONLY");
        sb.AppendLine("Unity=" + Application.unityVersion + " Scene=" + SceneManager.GetActiveScene().name +
                      " PlayMode=" + EditorApplication.isPlaying);
        string path, message;
        if (!Validate(out path, out message))
        {
            sb.AppendLine("[FAIL] " + message);
            return sb.ToString();
        }
        sb.AppendLine("[PASS] " + message);
        var hp = selectedBossPrefab.GetComponentInChildren<Health>(true);
        var boss = hp.GetComponent<MS7G1TitanRiftLanes>();
        sb.AppendLine("[INFO] Shadow Titan prefab Health.maxHealth=" + hp.maxHealth + " (must stay 500 in your balanced build)");
        if (Mathf.Abs(hp.maxHealth - 500f) > 0.01f)
            sb.AppendLine("[WARN] Health.maxHealth is not 500 in this prefab; manually verify intended actual boss variant. No automatic changes.");
        sb.AppendLine(boss != null ? "[PASS] Additive 7G.1 component present" : "[PENDING] Install 7G.1 component");
        if (boss != null)
        {
            sb.AppendLine("[INFO] enableRiftLanes=" + boss.enableRiftLanes + " phaseTwoOnly=" + boss.phaseTwoOnly +
                          " avoidVoidBurstOverlap=" + boss.avoidVoidBurstOverlap);
            sb.AppendLine(boss.phaseController != null ? "[PASS] References existing Phase2 controller" : "[WARN] Missing explicit phase reference");
            sb.AppendLine("[INFO] Damage=" + boss.damage + " Warning=" + boss.warningSeconds +
                          " Cooldown=" + boss.cooldownSeconds + " LaneLength=" + boss.laneLength);
        }
        MS7G1TitanRiftLanes[] live = UnityEngine.Object.FindObjectsByType<MS7G1TitanRiftLanes>(FindObjectsSortMode.None);
        sb.AppendLine("[INFO] Live abilities in Scene=" + live.Length);
        for (int i = 0; i < live.Length; i++) sb.AppendLine(live[i].RuntimeReport());
        if (live.Length == 0) sb.AppendLine("[PENDING] Enter Wave 5 and wait for actual Shadow Titan spawn; export while Play Mode is running.");
        sb.AppendLine("=== MANUAL QA ===");
        sb.AppendLine("[ ] Phase 2: three telegraphed lanes visible and dodgeable; no wall traps");
        sb.AppendLine("[ ] Avoid Void Burst overlap; Shockwave/Void Burst still work normally");
        sb.AppendLine("[ ] Standing inside => one shield/Health check only; outside => no Rift damage");
        sb.AppendLine("[ ] Pause freezes Rift warning/impact/cooldown; Retry/Game Over/Victory normal");
        sb.AppendLine("[ ] Actual Shadow Titan remains 500 HP; red follow-head Boss bar unchanged");
        sb.AppendLine("[ ] Boss Wave 5, 7F Arena disabled there, 7E elites unaffected");
        sb.AppendLine("[ ] Save/Profile/Gold/7C skills/7D equipment remain unchanged");
        sb.AppendLine("MANUAL=PENDING (unless explicitly tested by user)");
        sb.AppendLine("RELEASE=PENDING UNTIL LIVE + MANUAL QA");
        return sb.ToString();
    }

    private void Export()
    {
        latest = Audit();
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string folder = Path.Combine(root, "MS_QAReports", "7G1");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "MS7G1_TitanRiftQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
        File.WriteAllText(path, latest, new UTF8Encoding(false));
        Debug.Log("[7G.1] QA report: " + path);
        EditorUtility.RevealInFinder(path);
    }
}
#endif
