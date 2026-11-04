#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 7D.1 Edit-only installer and read-only audit. Does not patch any existing C# source.
/// </summary>
public sealed class MS7D1SetInstaller : EditorWindow
{
    private PlayerStats player;
    private MS7D1EquippedItemReader reader;
    private MS7D1EquipmentSetRegistry registry;
    private Vector2 scroll;
    private string lastReport = "Run Audit to generate a report.";

    [MenuItem("Tools/MonsterSurvival/7D Equipment Sets/1 - Open 7D.1 Setup")]
    public static void Open()
    {
        MS7D1SetInstaller w = GetWindow<MS7D1SetInstaller>("7D.1 Equipment Sets");
        w.minSize = new Vector2(580, 460);
        w.FindReferences();
        w.Show();
    }

    [MenuItem("Tools/MonsterSurvival/7D Equipment Sets/2 - Audit Scene (READ ONLY)")]
    public static void MenuAudit() { Open(); GetWindow<MS7D1SetInstaller>().Audit(); }

    [MenuItem("Tools/MonsterSurvival/7D Equipment Sets/3 - Run Pure 2-3 Piece Tests")]
    public static void MenuPure() { RunPure(); }

    [MenuItem("Tools/MonsterSurvival/7D Equipment Sets/4 - Export Report TXT")]
    public static void MenuExport() { Open(); GetWindow<MS7D1SetInstaller>().Export(); }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("MONSTERSURVIVAL 7D.1 - EQUIPMENT SET FOUNDATION", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("No stat bonuses are applied yet. This stage only reads the REAL three equipped slots and previews 2/3-piece tiers. NO Save writes, no changes to gameplay scripts.", MessageType.Info);
        EditorGUILayout.LabelField("Scene: " + SceneManager.GetActiveScene().name + "  |  Play Mode: " + EditorApplication.isPlaying);
        player = (PlayerStats)EditorGUILayout.ObjectField("Scene PlayerStats", player, typeof(PlayerStats), true);
        reader = (MS7D1EquippedItemReader)EditorGUILayout.ObjectField("Equipment Reader", reader, typeof(MS7D1EquippedItemReader), true);
        registry = (MS7D1EquipmentSetRegistry)EditorGUILayout.ObjectField("Set Registry", registry, typeof(MS7D1EquipmentSetRegistry), true);
        if (GUILayout.Button("FIND SCENE REFERENCES", GUILayout.Height(26))) FindReferences();
        GUI.enabled = !EditorApplication.isPlayingOrWillChangePlaymode;
        if (GUILayout.Button("INSTALL / REPAIR IN SampleScene (creates scene BACKUP)", GUILayout.Height(36))) Install();
        GUI.enabled = true;
        if (GUILayout.Button("READ-ONLY AUDIT", GUILayout.Height(30))) Audit();
        if (GUILayout.Button("RUN PURE FORMULA TESTS (NO SCENE CHANGES)", GUILayout.Height(29))) RunPure();
        if (GUILayout.Button("EXPORT TXT REPORT", GUILayout.Height(30))) Export();
        GUI.enabled = !EditorApplication.isPlayingOrWillChangePlaymode;
        if (GUILayout.Button("REMOVE ONLY 7D.1 COMPONENTS (UNDO supported)", GUILayout.Height(30))) Remove();
        GUI.enabled = true;
        EditorGUILayout.Space();
        if (reader != null)
        {
            EditorGUILayout.LabelField("READER MAPPING", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Select Reader in Inspector. Drag EXISTING Equipment manager into Equipment Source. Fill either 3-slot collection path OR all 3 individual slot paths. Configure item ID path if necessary.", MessageType.None);
            if (GUILayout.Button("LIST FIELD / PROPERTY NAMES OF EQUIPMENT SOURCE", GUILayout.Height(29))) ShowMembers();
        }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("REPORT", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(lastReport, GUILayout.MinHeight(140));
        EditorGUILayout.EndScrollView();
        if (EditorApplication.isPlaying && Event.current.type == EventType.Repaint) Repaint();
    }

    private void FindReferences()
    {
        player = UnityEngine.Object.FindFirstObjectByType<PlayerStats>(FindObjectsInactive.Include);
        reader = player != null ? player.GetComponent<MS7D1EquippedItemReader>() :
            UnityEngine.Object.FindFirstObjectByType<MS7D1EquippedItemReader>(FindObjectsInactive.Include);
        registry = player != null ? player.GetComponent<MS7D1EquipmentSetRegistry>() :
            UnityEngine.Object.FindFirstObjectByType<MS7D1EquipmentSetRegistry>(FindObjectsInactive.Include);
        Repaint();
    }

    private static bool IsScene(Component c)
    {
        return c != null && !EditorUtility.IsPersistent(c) && c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded;
    }

    private void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[7D.1] Stop Play Mode first."); return; }
        FindReferences();
        if (!IsScene(player) || player.gameObject.scene.name != "SampleScene") {
            Debug.LogError("[7D.1] Open SampleScene and select real Scene PlayerStats (not a prefab or MainMenu)."); return;
        }
        if (UnityEngine.Object.FindObjectsByType<PlayerStats>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1) {
            Debug.LogError("[7D.1] Multiple PlayerStats found. Installer stopped; resolve duplicate before installing."); return;
        }
        foreach (var r in UnityEngine.Object.FindObjectsByType<MS7D1EquipmentSetRegistry>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (IsScene(r) && r.gameObject != player.gameObject) { Debug.LogError("[7D.1] Registry on another GameObject. Stop and resolve duplicate."); return; }
        foreach (var r in UnityEngine.Object.FindObjectsByType<MS7D1EquippedItemReader>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (IsScene(r) && r.gameObject != player.gameObject) { Debug.LogError("[7D.1] Reader on another GameObject. Stop and resolve duplicate."); return; }

        Scene scene = player.gameObject.scene;
        if (string.IsNullOrEmpty(scene.path)) { Debug.LogError("[7D.1] Save Scene first."); return; }
        EditorSceneManager.SaveOpenScenes();
        string backupDir = Path.Combine(Directory.GetCurrentDirectory(), "MS_Backups", "7D1");
        Directory.CreateDirectory(backupDir);
        string backup = Path.Combine(backupDir, Path.GetFileNameWithoutExtension(scene.path) + "_before7D1_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity");
        File.Copy(Path.GetFullPath(scene.path), backup, false);
        reader = player.GetComponent<MS7D1EquippedItemReader>();
        if (reader == null) reader = Undo.AddComponent<MS7D1EquippedItemReader>(player.gameObject);
        registry = player.GetComponent<MS7D1EquipmentSetRegistry>();
        if (registry == null) registry = Undo.AddComponent<MS7D1EquipmentSetRegistry>(player.gameObject);
        Undo.RecordObject(registry, "Wire 7D.1 Set Registry");
        registry.reader = reader;
        EditorUtility.SetDirty(registry);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[7D.1 INSTALL] Success. 2 additive components on Player, no existing scripts modified. Backup: " + backup);
        Audit();
    }

    private void ShowMembers()
    {
        if (reader == null || reader.equipmentSource == null) {
            Debug.LogWarning("[7D.1] Drag your actual Equipment manager into Reader > Equipment Source first."); return;
        }
        Type type = reader.equipmentSource.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        StringBuilder sb = new StringBuilder("[7D.1 EQUIPMENT SOURCE MEMBERS] "+ type.FullName + "\n");
        foreach (var f in type.GetFields(flags)) {
            if (f.IsStatic) continue;
            sb.AppendLine("FIELD " + f.Name + " : " + f.FieldType.Name);
        }
        foreach (var p in type.GetProperties(flags)) {
            if (p.GetIndexParameters().Length > 0 || p.GetGetMethod(true) == null) continue;
            sb.AppendLine("PROPERTY " + p.Name + " : " + p.PropertyType.Name);
        }
        sb.AppendLine("Copy member names into Reader paths. No source values are read by this listing.");
        Debug.Log(sb.ToString());
    }

    private void Audit()
    {
        FindReferences();
        int pass = 0, warn = 0, error = 0;
        var report = new StringBuilder();
        report.AppendLine("MONSTERSURVIVAL 7D.1 EQUIPMENT SET AUDIT | READ ONLY");
        report.AppendLine("Unity=" + Application.unityVersion + " | Scene=" + SceneManager.GetActiveScene().name + " | PlayMode=" + EditorApplication.isPlaying);
        Action<bool, string> check = (ok, msg) => { if (ok) pass++; else error++; report.AppendLine((ok ? "[PASS] " : "[ERROR] ") + msg); };
        check(IsScene(player), "Scene PlayerStats exists");
        check(IsScene(reader) && player != null && reader.gameObject == player.gameObject, "One reader on Player");
        check(IsScene(registry) && player != null && registry.gameObject == player.gameObject, "One registry on Player");
        check(reader != null && registry != null && registry.reader == reader, "Registry->Reader linked");
        check(UnityEngine.Object.FindObjectsByType<MS7D1EquipmentSetRegistry>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1, "Exactly one registry in open Scene");
        check(UnityEngine.Object.FindObjectsByType<MS7D1EquippedItemReader>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1, "Exactly one reader in open Scene");
        if (reader == null || !reader.Configured) { warn++; report.AppendLine("[WARN] Equipment field mapping not configured yet. This is NOT an automatic test failure."); }
        else { pass++; report.AppendLine("[PASS] Equipment source and three-slot path configured"); }
        if (registry != null) {
            int enabledRules = 0;
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in registry.sets) {
                if (rule == null || !rule.enabledRule) continue;
                enabledRules++;
                bool valid = !string.IsNullOrWhiteSpace(rule.setId) && ids.Add(rule.setId.Trim());
                int pieceCount = 0;
                var uniquePieces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (rule.pieceIds != null) foreach (var id in rule.pieceIds)
                    if (!string.IsNullOrWhiteSpace(id)) uniquePieces.Add(id.Trim());
                pieceCount = uniquePieces.Count;
                valid = valid && pieceCount >= 2 && pieceCount <= 3;
                check(valid, "Set " + rule.setId + " unique itemIDs=" + pieceCount);
            }
            if (enabledRules == 0) { warn++; report.AppendLine("[WARN] 0 enabled set rules. Create real IDs in Registry Inspector; no fictitious IDs are added."); }
        }
        if (EditorApplication.isPlaying && registry != null) {
            List<MS7D1EquipmentSetRegistry.Status> statuses;
            string diag;
            if (registry.TryPreview(out statuses, out diag)) {
                pass++;
                report.AppendLine("[PASS] Runtime 3-slot adapter: " + diag);
                foreach (var s in statuses) report.AppendLine("[LIVE] " + s.ToString());
            } else { warn++; report.AppendLine("[WARN] Runtime adapter not ready: " + diag); }
        } else report.AppendLine("[INFO] Not in Play Mode; cannot certify real gear equip/unequip events.");
        report.AppendLine("[INFO] This foundation applies ZERO real combat stat bonuses. 7D.2 will require exact equipment scripts.");
        report.AppendLine("RESULT PASS=" + pass + " WARN=" + warn + " ERROR=" + error);
        lastReport = report.ToString();
        Debug.Log(lastReport);
    }

    private static void RunPure()
    {
        int pass = 0;
        string[] required = { "blade", "armor", "ring" };
        Action<bool, string> check = (ok, label) => {
            if (ok) { pass++; Debug.Log("[7D.1 PURE PASS] " + label); }
            else Debug.LogError("[7D.1 PURE FAIL] " + label);
        };
        check(MS7D1EquipmentSetRegistry.CountMatched(required, new string[] { null, null, null }) == 0, "0/3 none");
        check(MS7D1EquipmentSetRegistry.CountMatched(required, new [] { "blade", "", "" }) == 1, "1/3 one");
        check(MS7D1EquipmentSetRegistry.CountMatched(required, new [] { "blade", "ring", "" }) == 2, "2/3 bonus ready");
        check(MS7D1EquipmentSetRegistry.CountMatched(required, new [] { "blade", "armor", "ring" }) == 3, "3/3 full ready");
        check(MS7D1EquipmentSetRegistry.CountMatched(required, new [] { "blade", "blade", "ring" }) == 2, "duplicate item does not stack");
        check(MS7D1EquipmentSetRegistry.CountMatched(required, new [] { "  BLADE ", "armor", "ring" }) == 3, "trim & case insensitive");
        check(MS7D1EquipmentSetRegistry.CountMatched(required, new [] { "helmet", "bow", "staff" }) == 0, "other set does not count");
        Debug.Log("[7D.1 PURE RESULT] " + pass + "/7. Formula only, NOT real equipment API test.");
    }

    private void Export()
    {
        Audit();
        string path = EditorUtility.SaveFilePanel("Export 7D.1 Set Audit", "", "MS7D1_SetAudit_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt", "txt");
        if (string.IsNullOrEmpty(path)) return;
        File.WriteAllText(path, lastReport, new UTF8Encoding(true));
        Debug.Log("[7D.1] Wrote report: " + path);
    }

    private void Remove()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[7D.1] Stop Play Mode first."); return; }
        FindReferences();
        if (registry != null && IsScene(registry)) Undo.DestroyObjectImmediate(registry);
        if (reader != null && IsScene(reader)) Undo.DestroyObjectImmediate(reader);
        if (IsScene(player)) { EditorSceneManager.MarkSceneDirty(player.gameObject.scene); EditorSceneManager.SaveScene(player.gameObject.scene); }
        Debug.Log("[7D.1 REMOVE] Only 7D.1 components removed. Existing equipment, Save, PlayerStats and Synergy are untouched.");
        FindReferences();
    }
}
#endif
