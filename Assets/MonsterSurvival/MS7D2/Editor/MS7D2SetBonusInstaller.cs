#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Unity 6.6 Edit/Play QA for equipment set actual bonuses.
/// Adds or removes ONLY the 7D.2 component. Does not patch other C# source.
/// </summary>
public sealed class MS7D2SetBonusInstaller : EditorWindow
{
    private PlayerStats player;
    private MS7D1EquippedItemReader reader;
    private MS7D1EquipmentSetRegistry registry;
    private MS7D2SetBonusApplier applier;
    private Vector2 scroll;
    private string report = "Chon Audit hoac Export de kiem tra.";

    [MenuItem("Tools/MonsterSurvival/7D Equipment Sets/5 - Open 7D.2 Actual Bonuses")]
    public static void Open()
    {
        MS7D2SetBonusInstaller w = GetWindow<MS7D2SetBonusInstaller>("7D.2 Actual Bonuses");
        w.minSize = new Vector2(610, 480);
        w.FindReferences();
        w.Show();
    }

    [MenuItem("Tools/MonsterSurvival/7D Equipment Sets/6 - Audit 7D.2 (READ ONLY)")]
    public static void MenuAudit() { Open(); GetWindow<MS7D2SetBonusInstaller>().Audit(); }

    [MenuItem("Tools/MonsterSurvival/7D Equipment Sets/7 - Run 7D.2 Pure Math Tests")]
    public static void MenuPure() { PureTests(); }

    [MenuItem("Tools/MonsterSurvival/7D Equipment Sets/8 - Export 7D.2 TXT Report")]
    public static void MenuExport() { Open(); GetWindow<MS7D2SetBonusInstaller>().Export(); }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("MONSTERSURVIVAL 7D.2 - ACTUAL EQUIPMENT SET BONUSES", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Safe opt-in. No old scripts patched, no Save writes. DO NOT enable real bonuses until 7D.1 correctly reads your equipment slots in Play Mode.", MessageType.Warning);
        EditorGUILayout.LabelField("Scene: " + SceneManager.GetActiveScene().name + " | Play Mode: " + EditorApplication.isPlaying);
        player = (PlayerStats)EditorGUILayout.ObjectField("Scene PlayerStats", player, typeof(PlayerStats), true);
        reader = (MS7D1EquippedItemReader)EditorGUILayout.ObjectField("7D.1 Equipped Reader", reader, typeof(MS7D1EquippedItemReader), true);
        registry = (MS7D1EquipmentSetRegistry)EditorGUILayout.ObjectField("7D.1 Set Registry", registry, typeof(MS7D1EquipmentSetRegistry), true);
        applier = (MS7D2SetBonusApplier)EditorGUILayout.ObjectField("7D.2 Applier", applier, typeof(MS7D2SetBonusApplier), true);
        if (GUILayout.Button("FIND CURRENT SCENE REFERENCES", GUILayout.Height(27))) FindReferences();
        GUI.enabled = !EditorApplication.isPlayingOrWillChangePlaymode;
        if (GUILayout.Button("1 - INSTALL / REPAIR 7D.2 (BACKUP SCENE)", GUILayout.Height(36))) Install();
        GUI.enabled = true;
        if (GUILayout.Button("2 - AUDIT + CURRENT BONUSES (READ ONLY)", GUILayout.Height(30))) Audit();
        if (GUILayout.Button("3 - RUN PURE MATH TESTS", GUILayout.Height(29))) PureTests();
        if (GUILayout.Button("4 - EXPORT TXT REPORT", GUILayout.Height(29))) Export();
        GUI.enabled = !EditorApplication.isPlayingOrWillChangePlaymode;
        if (GUILayout.Button("5 - REMOVE ONLY 7D.2 COMPONENT", GUILayout.Height(29))) Remove();
        GUI.enabled = true;
        EditorGUILayout.Space();
        if (applier != null)
        {
            EditorGUILayout.LabelField("INSPECTOR SAFETY GATES", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Player > MS7D2SetBonusApplier: First verify 7D.1 reader receives equipped IDs (not bag items); then tick Mapping Verified By User AND Enable Actual Bonuses. Defaults are both OFF.", MessageType.None);
            EditorGUILayout.LabelField("Mapping confirmed: " + applier.mappingVerifiedByUser);
            EditorGUILayout.LabelField("Apply enabled: " + applier.enableActualBonuses);
            EditorGUILayout.LabelField("Current own damage delta: " + applier.AppliedDamageBonus.ToString("0.###"));
            EditorGUILayout.LabelField("Current own attack speed delta: " + applier.AppliedAttackSpeedBonus.ToString("0.###"));
        }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("LATEST REPORT", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(report, GUILayout.MinHeight(170));
        EditorGUILayout.EndScrollView();
        if (EditorApplication.isPlaying && Event.current.type == EventType.Repaint) Repaint();
    }

    private void FindReferences()
    {
        player = UnityEngine.Object.FindFirstObjectByType<PlayerStats>(FindObjectsInactive.Include);
        reader = player != null ? player.GetComponent<MS7D1EquippedItemReader>() : null;
        registry = player != null ? player.GetComponent<MS7D1EquipmentSetRegistry>() : null;
        applier = player != null ? player.GetComponent<MS7D2SetBonusApplier>() : null;
        Repaint();
    }

    private static bool IsScene(Component c)
    {
        return c != null && !EditorUtility.IsPersistent(c) &&
            c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded;
    }

    private void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[7D.2] Exit Play Mode before installing."); return; }
        FindReferences();
        if (!IsScene(player) || player.gameObject.scene.name != "SampleScene" ||
            !IsScene(reader) || !IsScene(registry) || registry.reader != reader ||
            reader.gameObject != player.gameObject || registry.gameObject != player.gameObject)
        {
            Debug.LogError("[7D.2] Open SampleScene and finish installing 7D.1 on the SAME PlayerStats GameObject first. No writes performed.");
            return;
        }
        if (UnityEngine.Object.FindObjectsByType<PlayerStats>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1 ||
            UnityEngine.Object.FindObjectsByType<MS7D1EquippedItemReader>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1 ||
            UnityEngine.Object.FindObjectsByType<MS7D1EquipmentSetRegistry>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1)
        {
            Debug.LogError("[7D.2] Duplicate 7D.1 or PlayerStats components found. Installation aborted.");
            return;
        }
        foreach (MS7D2SetBonusApplier other in UnityEngine.Object.FindObjectsByType<MS7D2SetBonusApplier>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (IsScene(other) && other.gameObject != player.gameObject)
            {
                Debug.LogError("[7D.2] Applier already exists on ANOTHER GameObject: " + other.name);
                return;
            }
        Scene scene = player.gameObject.scene;
        if (string.IsNullOrEmpty(scene.path)) { Debug.LogError("[7D.2] Save SampleScene first."); return; }
        EditorSceneManager.SaveOpenScenes();
        string folder = Path.Combine(Directory.GetCurrentDirectory(), "MS_Backups", "7D2");
        Directory.CreateDirectory(folder);
        string destination = Path.Combine(folder, "SampleScene_before7D2_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity");
        File.Copy(Path.GetFullPath(scene.path), destination, false);
        applier = player.GetComponent<MS7D2SetBonusApplier>();
        bool newlyCreated = applier == null;
        if (newlyCreated) applier = Undo.AddComponent<MS7D2SetBonusApplier>(player.gameObject);
        Undo.RecordObject(applier, "Wire 7D.2 equipment set references");
        applier.playerStats = player;
        applier.equipmentReader = reader;
        applier.setRegistry = registry;
        // NEVER automatically tick the 2 opt-in gates (even on repeat install).
        if (newlyCreated) { applier.mappingVerifiedByUser = false; applier.enableActualBonuses = false; }
        EditorUtility.SetDirty(applier);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[7D.2 INSTALL] Added/repaired only 7D.2 component; 2 safety toggles unchanged/OFF by default. Scene backup: " + destination);
        Audit();
    }

    private void Audit()
    {
        FindReferences();
        int pass = 0, warning = 0, error = 0;
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 7D.2 ACTUAL EQUIPMENT SET QA - READ ONLY");
        sb.AppendLine("UTC=" + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " | Unity=" + Application.unityVersion +
                      " | Scene=" + SceneManager.GetActiveScene().name + " | PlayMode=" + EditorApplication.isPlaying);
        Action<bool, string> check = (condition, label) => {
            if (condition) pass++; else error++;
            sb.AppendLine((condition ? "[PASS] " : "[ERROR] ") + label);
        };
        check(IsScene(player), "PlayerStats exists in active Scene");
        check(IsScene(reader) && player != null && reader.gameObject == player.gameObject, "7D.1 Reader on Player");
        check(IsScene(registry) && player != null && registry.gameObject == player.gameObject, "7D.1 Registry on Player");
        check(IsScene(applier) && player != null && applier.gameObject == player.gameObject, "7D.2 Applier on Player");
        check(registry != null && registry.reader == reader, "Registry->Reader connected");
        check(applier != null && applier.playerStats == player && applier.equipmentReader == reader && applier.setRegistry == registry,
              "Applier points to current PlayerStats / Reader / Registry");
        check(UnityEngine.Object.FindObjectsByType<MS7D2SetBonusApplier>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
              "Only one Set Bonus Applier in loaded Scenes");
        if (reader == null || !reader.Configured) { warning++; sb.AppendLine("[WARN] 7D.1 reader not configured; actual stat changes must stay OFF"); }
        else { pass++; sb.AppendLine("[PASS] 7D.1 Equipment source is configured (requires runtime equip/unequip verification)"); }
        if (applier != null)
        {
            float d, a; string reason;
            bool ready = applier.TryEvaluate(out d, out a, out reason);
            if (ready) { pass++; sb.AppendLine("[PASS] Read-only candidate bonuses: Damage +" + d.ToString("0.###") + ", ASPD +" + a.ToString("0.###")); }
            else { warning++; sb.AppendLine("[WARN] Not ready to apply bonuses: " + reason); }
            sb.AppendLine("[INFO] MappingVerified=" + applier.mappingVerifiedByUser + " | ActualBonusesEnabled=" + applier.enableActualBonuses);
            sb.AppendLine("[INFO] OwnCurrentlyApplied: Damage +" + applier.AppliedDamageBonus + " | ASPD +" + applier.AppliedAttackSpeedBonus);
            sb.AppendLine("[INFO] Diagnostic: " + applier.LastDiagnostic);
        }
        else sb.AppendLine("[INFO] Install 7D.2 first; no stats have been edited by audit.");
        if (registry != null && reader != null && EditorApplication.isPlaying)
        {
            string[] ids; string readDiag;
            bool ok = reader.TryRead(out ids, out readDiag);
            sb.AppendLine("[LIVE] Equipped 3 slots reader result: " + (ok ? "OK " : "FAIL ") + readDiag);
            if (!ok) warning++;
            List<MS7D1EquipmentSetRegistry.Status> statuses; string d;
            if (registry.TryPreview(out statuses, out d))
                foreach (var status in statuses) sb.AppendLine("[LIVE] " + status.ToString());
        }
        else sb.AppendLine("[INFO] Runtime stats and unequip reversal require a Play Mode test; Editor Audit is not a gameplay PASS.");
        sb.AppendLine("[MANUAL] Verify: 0/3 => 0/0, 1/3 => 0/0, 2/3 => +damage, 3/3 => +damage +attack, unequip => reverse exactly once.");
        sb.AppendLine("[MANUAL] Verify: LevelUp/permanent upgrades do not get lost; Return to Menu and Profile Save unchanged.");
        sb.AppendLine("[INFO] +0.08 = +0.08 additive multiplier points (NOT guaranteed +8% of final damage after other bonuses).");
        sb.AppendLine("RESULT PASS=" + pass + " WARN=" + warning + " ERROR=" + error);
        report = sb.ToString();
        Debug.Log(report);
    }

    private static void PureTests()
    {
        int passed = 0, tested = 0;
        Action<bool, string> test = (ok, label) => {
            tested++;
            if (ok) { passed++; Debug.Log("[7D.2 PURE PASS] " + label); }
            else Debug.LogError("[7D.2 PURE FAIL] " + label);
        };
        Func<float,float,bool> eq = (a,b) => Mathf.Abs(a - b) < .00001f;
        float baseDamage = 1f;
        float two = MS7D2SetBonusApplier.ReplaceContribution(baseDamage, 0f, .08f);
        test(eq(two, 1.08f), "2/3 adds +0.08 multiplier points");
        test(eq(MS7D2SetBonusApplier.ReplaceContribution(two, .08f, .08f), two), "repeated poll does NOT stack");
        test(eq(MS7D2SetBonusApplier.ReplaceContribution(two, .08f, 0f), 1f), "unequip reverts 2/3 bonus");
        float speed = MS7D2SetBonusApplier.ReplaceContribution(1f, 0f, .12f);
        test(eq(speed, 1.12f), "3/3 grants +0.12 ASPD multiplier points");
        test(eq(MS7D2SetBonusApplier.ReplaceContribution(speed, .12f, 0f), 1f), "remove 3/3 reverses ASPD once");
        test(eq(MS7D2SetBonusApplier.ReplaceContribution(1.28f, .08f, .08f), 1.28f), "unrelated skill +0.20 stays intact");
        test(eq(MS7D2SetBonusApplier.ReplaceContribution(1.28f, .08f, 0f), 1.20f), "remove set keeps unrelated skill +0.20");
        test(MS7D1EquipmentSetRegistry.CountMatched(new[] { "blade", "armor", "ring" },
             new[] { "blade", "blade", "ring" }) == 2, "one item cannot fill two different slots");
        Debug.Log("[7D.2 PURE RESULT] " + passed + "/" + tested +
                  " formula-only. Does not certify Unity Play Mode or actual equipment source.");
    }

    private void Export()
    {
        Audit();
        string path = EditorUtility.SaveFilePanel("Export 7D.2 equipment QA", "",
            "MS7D2_SetBonusQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt", "txt");
        if (string.IsNullOrEmpty(path)) return;
        File.WriteAllText(path, report, new UTF8Encoding(true));
        Debug.Log("[7D.2] Report saved: " + path);
    }

    private void Remove()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[7D.2] Exit Play Mode before uninstalling."); return; }
        FindReferences();
        if (applier != null && IsScene(applier))
        {
            Undo.DestroyObjectImmediate(applier);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            EditorSceneManager.SaveScene(player.gameObject.scene);
        }
        Debug.Log("[7D.2 REMOVE] Removed ONLY 7D.2 Applier. 7D.1 Reader/Registry, gear and Save remain unchanged.");
        FindReferences();
    }
}
#endif
