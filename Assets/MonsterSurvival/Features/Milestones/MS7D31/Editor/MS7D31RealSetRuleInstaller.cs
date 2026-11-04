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
/// 7D.3.1 Optional EDITOR-ONLY helper. Creates ONE explicit QA set rule using genuine
/// MS3AItemData.itemId values. Does NOT equip items, modify saves, or add runtime scripts.
/// Default reward application is disabled for safety after creating the rule.
/// </summary>
public sealed class MS7D31RealSetRuleInstaller : EditorWindow
{
    private const string MenuRoot = "Tools/MonsterSurvival/7D Equipment Sets/";
    private const string PrefKey = "MonsterSurvival.7D31.QASet.";
    private const string DefaultSetId = "7d31.qa_reaper_warden";
    private const string SuggestedWeapon = "reaper_edge_3d3";
    private const string SuggestedArmor = "soulguard_plate_3d3";
    private const string SuggestedAccessory = "warden_oath_3d4";

    private string setId = DefaultSetId;
    private string displayName = "Reaper / Soulguard / Oath (QA Set)";
    private string weaponId = SuggestedWeapon;
    private string armorId = SuggestedArmor;
    private string accessoryId = SuggestedAccessory;
    private float twoPieceDamage = 0.08f;
    private float threePieceAttack = 0.12f;
    private Vector2 scroll;
    private string lastAudit = "Not audited yet.";

    [MenuItem(MenuRoot + "17 - Open 7D.3.1 REAL Set Rule Setup")]
    public static void Open()
    {
        MS7D31RealSetRuleInstaller w = GetWindow<MS7D31RealSetRuleInstaller>("7D.3.1 Real Set Rule");
        w.minSize = new Vector2(580, 515);
        w.Show();
    }

    [MenuItem(MenuRoot + "18 - Audit 7D.3.1 Set Rules (READ ONLY)")]
    public static void MenuAudit() { Open(); GetWindow<MS7D31RealSetRuleInstaller>().Audit(true); }

    [MenuItem(MenuRoot + "19 - Export 7D.3.1 TXT")]
    public static void MenuExport() { Open(); GetWindow<MS7D31RealSetRuleInstaller>().Export(); }

    private void OnEnable() { RestoreFields(); }

    private void RestoreFields()
    {
        setId = EditorPrefs.GetString(PrefKey + "id", DefaultSetId);
        displayName = EditorPrefs.GetString(PrefKey + "name", "Reaper / Soulguard / Oath (QA Set)");
        weaponId = EditorPrefs.GetString(PrefKey + "weapon", SuggestedWeapon);
        armorId = EditorPrefs.GetString(PrefKey + "armor", SuggestedArmor);
        accessoryId = EditorPrefs.GetString(PrefKey + "accessory", SuggestedAccessory);
        twoPieceDamage = EditorPrefs.GetFloat(PrefKey + "damage", .08f);
        threePieceAttack = EditorPrefs.GetFloat(PrefKey + "aspd", .12f);
    }

    private void SaveFields()
    {
        EditorPrefs.SetString(PrefKey + "id", setId);
        EditorPrefs.SetString(PrefKey + "name", displayName);
        EditorPrefs.SetString(PrefKey + "weapon", weaponId);
        EditorPrefs.SetString(PrefKey + "armor", armorId);
        EditorPrefs.SetString(PrefKey + "accessory", accessoryId);
        EditorPrefs.SetFloat(PrefKey + "damage", twoPieceDamage);
        EditorPrefs.SetFloat(PrefKey + "aspd", threePieceAttack);
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("7D.3.1 - REAL EQUIPMENT SET RULE SETUP", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "7D.3 UI is installed and already reads all 3 equipment IDs. The 7D.3 report showed RULES=0. " +
            "This utility appends one explicit QA rule ONLY to MS7D1EquipmentSetRegistry. It does not create gear, " +
            "modify PlayerStats, change Inventory/Save, or apply bonuses automatically.", MessageType.Info);
        EditorGUILayout.LabelField("Scene: " + SceneManager.GetActiveScene().name +
            " | Play Mode: " + EditorApplication.isPlaying);
        GUILayout.Space(7);
        EditorGUILayout.LabelField("TEST RULE - IDs must be exact MS3AItemData.itemId values", EditorStyles.boldLabel);
        setId = EditorGUILayout.TextField("Unique Set ID", setId);
        displayName = EditorGUILayout.TextField("Set display name", displayName);
        weaponId = EditorGUILayout.TextField("Weapon ID", weaponId);
        armorId = EditorGUILayout.TextField("Armor ID", armorId);
        accessoryId = EditorGUILayout.TextField("Accessory ID", accessoryId);
        twoPieceDamage = Mathf.Clamp01(EditorGUILayout.FloatField("2/3: Damage multiplier +", twoPieceDamage));
        threePieceAttack = Mathf.Clamp01(EditorGUILayout.FloatField("3/3: Attack speed +", threePieceAttack));
        GUILayout.Space(6);
        if (GUILayout.Button("A - USE THREE VERIFIED IDs FROM 7D.3 REPORT", GUILayout.Height(30)))
        {
            weaponId = SuggestedWeapon; armorId = SuggestedArmor; accessoryId = SuggestedAccessory;
            SaveFields();
            Debug.Log("[7D.3.1] Filled item IDs from user-provided 7D.3 report. No scene edits.");
        }
        GUI.enabled = EditorApplication.isPlaying;
        if (GUILayout.Button("B - CAPTURE THREE CURRENTLY EQUIPPED IDs (PLAY MODE)", GUILayout.Height(30)))
            CapturePlayMode();
        GUI.enabled = true;
        EditorGUILayout.HelpBox("If your 5A.3 equipment restores only on Play, capture the IDs while playing, " +
            "then STOP Play Mode before creating the persistent rule. Capture stores TEXT in Unity EditorPrefs only.", MessageType.None);
        GUI.enabled = !EditorApplication.isPlayingOrWillChangePlaymode;
        if (GUILayout.Button("C - CREATE QA SET RULE (SCENE BACKUP; BONUSES OFF)", GUILayout.Height(36)))
            CreateRule();
        if (GUILayout.Button("D - REMOVE ONLY THIS QA SET RULE (SCENE BACKUP)", GUILayout.Height(28)))
            RemoveRule();
        GUI.enabled = true;
        if (GUILayout.Button("E - AUDIT REAL IDs, SET RULES & BONUS GATES (READ ONLY)", GUILayout.Height(34))) Audit(true);
        if (GUILayout.Button("F - EXPORT QA REPORT TXT", GUILayout.Height(28))) Export();
        if (GUILayout.Button("RUN 7 PURE MATCHING TESTS", GUILayout.Height(25))) PureQA();
        EditorGUILayout.TextArea(lastAudit, GUILayout.MinHeight(170));
        EditorGUILayout.EndScrollView();
    }

    private static T[] InActiveScene<T>() where T : Component
    {
        Scene scene = SceneManager.GetActiveScene();
        T[] all = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<T> found = new List<T>();
        foreach (T c in all)
            if (c != null && c.gameObject.scene == scene && !EditorUtility.IsPersistent(c))
                found.Add(c);
        return found.ToArray();
    }

    private static bool TryReferences(out MS3DPlayerEquipment gear,
        out MS7D1EquippedItemReader reader, out MS7D1EquipmentSetRegistry registry,
        out MS7D2SetBonusApplier applier, out string error)
    {
        gear = null; reader = null; registry = null; applier = null; error = "";
        if (SceneManager.GetActiveScene().name != "SampleScene")
        { error = "Open gameplay SampleScene first."; return false; }
        MS3DPlayerEquipment[] gs = InActiveScene<MS3DPlayerEquipment>();
        MS7D1EquippedItemReader[] rs = InActiveScene<MS7D1EquippedItemReader>();
        MS7D1EquipmentSetRegistry[] ss = InActiveScene<MS7D1EquipmentSetRegistry>();
        MS7D2SetBonusApplier[] aa = InActiveScene<MS7D2SetBonusApplier>();
        if (gs.Length != 1 || rs.Length != 1 || ss.Length != 1 || aa.Length != 1)
        { error = "Need exactly one 3D.1 Equipment, 7D.1 Reader/Registry, and 7D.2 Applier in SampleScene."; return false; }
        gear = gs[0]; reader = rs[0]; registry = ss[0]; applier = aa[0];
        if (reader.gameObject != gear.gameObject || registry.gameObject != gear.gameObject ||
            applier.gameObject != gear.gameObject || reader.equipmentSource != gear ||
            registry.reader != reader || applier.equipmentReader != reader || applier.setRegistry != registry)
        { error = "Player components are not consistently linked. Audit/fix 7D.2.1 before creating rules."; return false; }
        return true;
    }

    private static string ActualId(MS3DPlayerEquipment gear, MS3DEquipmentType slot)
    {
        MS3AItemData item = gear != null ? gear.GetEquipped(slot) : null;
        return item != null && !string.IsNullOrWhiteSpace(item.itemId) ? item.itemId.Trim() : "";
    }

    private void CapturePlayMode()
    {
        if (!EditorApplication.isPlaying)
        { Debug.LogWarning("[7D.3.1] Capture requires Play Mode in SampleScene."); return; }
        MS3DPlayerEquipment gear; MS7D1EquippedItemReader reader;
        MS7D1EquipmentSetRegistry registry; MS7D2SetBonusApplier applier; string error;
        if (!TryReferences(out gear, out reader, out registry, out applier, out error))
        { Debug.LogWarning("[7D.3.1] Cannot capture equipped IDs: " + error); return; }
        string w = ActualId(gear, MS3DEquipmentType.Weapon);
        string a = ActualId(gear, MS3DEquipmentType.Armor);
        string x = ActualId(gear, MS3DEquipmentType.Accessory);
        if (!UniqueNonEmpty(w, a, x))
        { Debug.LogWarning("[7D.3.1] Equip 3 distinct pieces first; found IDs: " + w + " / " + a + " / " + x); return; }
        weaponId = w; armorId = a; accessoryId = x; SaveFields();
        Debug.Log("[7D.3.1] CAPTURED real equipped IDs: " + w + " | " + a + " | " + x +
            ". STOP Play Mode, then press CREATE. No scene files changed.");
        Repaint();
    }

    private static bool UniqueNonEmpty(params string[] ids)
    {
        var s = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string id in ids)
            if (string.IsNullOrWhiteSpace(id) || !s.Add(id.Trim())) return false;
        return true;
    }

    private static string BackupScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
            throw new InvalidOperationException("Save SampleScene as .unity first.");
        if (!EditorSceneManager.SaveScene(scene))
            throw new IOException("Cannot save SampleScene before backup.");
        string root = Directory.GetParent(Application.dataPath).FullName;
        string dst = Path.Combine(root, "MS_Backups", "7D31");
        Directory.CreateDirectory(dst);
        string backup = Path.Combine(dst, "SampleScene_before_7D31_" +
            DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity");
        File.Copy(Path.Combine(root, scene.path), backup, false);
        return backup;
    }

    private void CreateRule()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogError("[7D.3.1] STOP Play Mode before creating a persistent rule."); return; }
        MS3DPlayerEquipment gear; MS7D1EquippedItemReader reader;
        MS7D1EquipmentSetRegistry registry; MS7D2SetBonusApplier applier; string error;
        if (!TryReferences(out gear, out reader, out registry, out applier, out error))
        { Debug.LogError("[7D.3.1] " + error); return; }
        string id = setId == null ? "" : setId.Trim();
        string w = weaponId == null ? "" : weaponId.Trim();
        string a = armorId == null ? "" : armorId.Trim();
        string x = accessoryId == null ? "" : accessoryId.Trim();
        if (string.IsNullOrWhiteSpace(id) || !UniqueNonEmpty(w, a, x))
        { Debug.LogError("[7D.3.1] Need a unique Set ID and 3 distinct non-empty equipment IDs."); return; }
        if (registry.sets != null)
        {
            foreach (MS7D1EquipmentSetRegistry.Rule rule in registry.sets)
            {
                if (rule == null) continue;
                if (string.Equals(rule.setId == null ? "" : rule.setId.Trim(), id, StringComparison.OrdinalIgnoreCase))
                { Debug.LogWarning("[7D.3.1] Same Set ID already exists. No duplicate created. Remove/edit it in Inspector if needed."); return; }
                if (rule.enabledRule && applier.prohibitPiecesSharedBetweenSets && rule.pieceIds != null)
                    foreach (string piece in rule.pieceIds)
                        if (!string.IsNullOrWhiteSpace(piece) &&
                            (string.Equals(piece.Trim(), w, StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(piece.Trim(), a, StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(piece.Trim(), x, StringComparison.OrdinalIgnoreCase)))
                        { Debug.LogWarning("[7D.3.1] Piece overlaps an enabled set rule while overlap protection is ON. Nothing changed: " + piece); return; }
            }
        }
        string catalogInfo = CatalogCheck(gear, w, a, x);
        if (!EditorUtility.DisplayDialog("7D.3.1: create explicit QA set",
            "Append ONE enabled QA set to 7D.1 Registry?\n\n" + id + "\n" +
            "Weapon: " + w + "\nArmor: " + a + "\nAccessory: " + x + "\n\n" +
            "2/3 damage +" + twoPieceDamage.ToString("0.###") +
            " | 3/3 attack speed +" + threePieceAttack.ToString("0.###") +
            "\n\nCatalog cross-check: " + catalogInfo +
            "\n\nThis is a QA/test set, not official game balance. Bonuses are SWITCHED OFF." +
            "\nScene will be backed up. Existing item, equipment, and Save data will not be changed.",
            "Create QA set", "Cancel")) return;
        try
        {
            Scene scene = SceneManager.GetActiveScene();
            string backup = BackupScene(scene);
            Undo.RecordObject(registry, "7D.3.1 Add QA Equipment Set Rule");
            Undo.RecordObject(applier, "7D.3.1 Disable Real Set Bonuses Until QA");
            if (registry.sets == null) registry.sets = new List<MS7D1EquipmentSetRegistry.Rule>();
            registry.sets.Add(new MS7D1EquipmentSetRegistry.Rule
            {
                enabledRule = true,
                setId = id,
                displayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName.Trim(),
                pieceIds = new[] { w, a, x },
                damageBonusAtTwo = Mathf.Clamp01(twoPieceDamage),
                attackSpeedBonusAtThree = Mathf.Clamp01(threePieceAttack)
            });
            applier.mappingVerifiedByUser = false;
            applier.enableActualBonuses = false;
            EditorUtility.SetDirty(registry);
            EditorUtility.SetDirty(applier);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Cannot save changed scene. Inspect Undo. Backup: " + backup);
            SaveFields();
            Debug.Log("[7D.3.1] QA rule added to 7D.1 Registry; both actual bonus gates OFF. Backup: " + backup);
            Audit(true);
        }
        catch (Exception e) { Debug.LogError("[7D.3.1] Create failed: " + e.Message); }
    }

    private static string CatalogCheck(MS3DPlayerEquipment gear, string weapon, string armor, string accessory)
    {
        if (gear == null || gear.catalog == null || gear.catalog.entries == null) return "Catalog unavailable (not an error by itself)";
        bool w = false, a = false, x = false;
        foreach (MS3DEquipmentDefinition row in gear.catalog.entries)
        {
            if (row == null || row.item == null) continue;
            if (row.type == MS3DEquipmentType.Weapon && row.item.itemId == weapon) w = true;
            if (row.type == MS3DEquipmentType.Armor && row.item.itemId == armor) a = true;
            if (row.type == MS3DEquipmentType.Accessory && row.item.itemId == accessory) x = true;
        }
        return "Weapon=" + w + ", Armor=" + a + ", Accessory=" + x;
    }

    private void RemoveRule()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogError("[7D.3.1] STOP Play Mode first."); return; }
        MS3DPlayerEquipment gear; MS7D1EquippedItemReader reader;
        MS7D1EquipmentSetRegistry registry; MS7D2SetBonusApplier applier; string error;
        if (!TryReferences(out gear, out reader, out registry, out applier, out error))
        { Debug.LogError("[7D.3.1] " + error); return; }
        string id = setId == null ? "" : setId.Trim();
        if (id != DefaultSetId)
        { Debug.LogWarning("[7D.3.1] Removal only permitted for the default QA Set ID to protect real sets. " + DefaultSetId); return; }
        int index = registry.sets == null ? -1 : registry.sets.FindIndex(rule =>
            rule != null && rule.setId == DefaultSetId);
        if (index < 0) { Debug.Log("[7D.3.1] Default QA Set ID not found. Nothing removed."); return; }
        if (!EditorUtility.DisplayDialog("Remove QA set only?",
            "Remove ONLY " + DefaultSetId + " from Registry? Equipment, PlayerStats and Save are untouched. " +
            "Both bonus gates will be disabled. Scene backup is created first.", "Remove QA set", "Cancel")) return;
        try
        {
            Scene scene = SceneManager.GetActiveScene(); string backup = BackupScene(scene);
            Undo.RecordObject(registry, "7D.3.1 Remove QA Set Rule");
            Undo.RecordObject(applier, "7D.3.1 Disable Set Bonuses Before QA Rule Removal");
            applier.mappingVerifiedByUser = false;
            applier.enableActualBonuses = false;
            registry.sets.RemoveAt(index);
            EditorUtility.SetDirty(registry); EditorUtility.SetDirty(applier);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[7D.3.1] Removed only QA rule, no equipment or Save changes. Backup: " + backup);
            Audit(true);
        }
        catch (Exception e) { Debug.LogError("[7D.3.1] Remove failed: " + e.Message); }
    }

    private string BuildAudit()
    {
        var sb = new StringBuilder();
        int pass = 0, warn = 0, errorCount = 0;
        sb.AppendLine("MONSTERSURVIVAL 7D.3.1 - REAL EQUIPMENT SET RULE AUDIT (READ ONLY)");
        sb.AppendLine("Unity=" + Application.unityVersion + " | Scene=" + SceneManager.GetActiveScene().name +
            " | PlayMode=" + EditorApplication.isPlaying + " | Time=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        MS3DPlayerEquipment gear; MS7D1EquippedItemReader reader;
        MS7D1EquipmentSetRegistry registry; MS7D2SetBonusApplier applier; string reason;
        if (!TryReferences(out gear, out reader, out registry, out applier, out reason))
        {
            sb.AppendLine("[ERROR] " + reason + "\nRESULT PASS=0 WARN=0 ERROR=1"); return sb.ToString();
        }
        sb.AppendLine("[PASS] 3D.1 Equipment and 7D.1 Reader/Registry and 7D.2 Applier linked on same Player"); pass++;
        string[] ids; string diag;
        if (reader.TryRead(out ids, out diag))
        { sb.AppendLine("[PASS] Reader: " + diag); pass++; }
        else { sb.AppendLine("[ERROR] Reader: " + diag); errorCount++; }
        string w = ActualId(gear, MS3DEquipmentType.Weapon);
        string a = ActualId(gear, MS3DEquipmentType.Armor);
        string x = ActualId(gear, MS3DEquipmentType.Accessory);
        sb.AppendLine("[INFO] PlayerEquipment API IDs: W=" + w + " A=" + a + " X=" + x);
        sb.AppendLine("[INFO] Catalog cross-check for proposed QA pieces: " + CatalogCheck(gear, weaponId, armorId, accessoryId));
        int count = registry.sets == null ? 0 : registry.sets.Count;
        sb.AppendLine("[INFO] Registry raw rules count=" + count);
        List<MS7D1EquipmentSetRegistry.Status> previews;
        if (registry.TryPreview(out previews, out diag))
        {
            sb.AppendLine("[PASS] Registry preview reads live equipment: " + diag); pass++;
            if (previews.Count > 0)
            {
                pass++;
                sb.AppendLine("[PASS] " + previews.Count + " enabled and valid set rule(s) show in 7D.3 UI");
                foreach (var s in previews) sb.AppendLine("[SET] " + s.ToString());
            }
            else { warn++; sb.AppendLine("[WARN] No enabled VALID set rule. 7D.3 shows NO ENABLED EQUIPMENT SETS."); }
        }
        else { errorCount++; sb.AppendLine("[ERROR] Registry Preview failed: " + diag); }
        sb.AppendLine("[INFO] Bonus gates mappingVerified=" + applier.mappingVerifiedByUser +
            " enabled=" + applier.enableActualBonuses + " | appliedDamage=" + applier.AppliedDamageBonus +
            " appliedASPD=" + applier.AppliedAttackSpeedBonus);
        if (!applier.mappingVerifiedByUser || !applier.enableActualBonuses)
        { sb.AppendLine("[INFO] Bonuses OFF: preview only. Correct until equip/unequip QA is confirmed."); }
        else { sb.AppendLine("[INFO] Bonus gates ON: test actual stats and exact unequip reversal in Play Mode."); }
        if (!EditorApplication.isPlaying)
            sb.AppendLine("[INFO] Edit Mode may have EMPTY equipment slots; profile equipment can restore only in Play Mode.");
        else sb.AppendLine("[INFO] Play Mode audit shows actual live itemIDs; manual stat tests still required.");
        sb.AppendLine("RESULT PASS=" + pass + " WARN=" + warn + " ERROR=" + errorCount);
        return sb.ToString();
    }

    private void Audit(bool log)
    {
        lastAudit = BuildAudit();
        if (log) Debug.Log(lastAudit);
        Repaint();
    }

    private void Export()
    {
        lastAudit = BuildAudit();
        string path = EditorUtility.SaveFilePanel("Export 7D.3.1 report", "",
            "MS7D31_RealSetRule_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt", "txt");
        if (string.IsNullOrEmpty(path)) return;
        File.WriteAllText(path, lastAudit, new UTF8Encoding(false));
        Debug.Log("[7D.3.1] QA TXT exported: " + path);
    }

    private void PureQA()
    {
        int pass = 0;
        Action<bool, string> test = (ok, name) =>
        {
            if (ok) { pass++; Debug.Log("[7D.3.1 PURE] PASS " + name); }
            else Debug.LogError("[7D.3.1 PURE] FAIL " + name);
        };
        string[] required = { "weapon", "armor", "accessory" };
        test(MS7D1EquipmentSetRegistry.CountMatched(required, new string[] { null, null, null }) == 0, "0/3");
        test(MS7D1EquipmentSetRegistry.CountMatched(required, new[] { "weapon", "", "" }) == 1, "1/3");
        test(MS7D1EquipmentSetRegistry.CountMatched(required, new[] { "weapon", "armor", "" }) == 2, "2/3");
        test(MS7D1EquipmentSetRegistry.CountMatched(required, new[] { "weapon", "armor", "accessory" }) == 3, "3/3");
        test(MS7D1EquipmentSetRegistry.CountMatched(required, new[] { "weapon", "weapon", "accessory" }) == 2, "duplicate item not counted");
        test(MS7D1EquipmentSetRegistry.CountMatched(required, new[] { "WEAPON", "ARMOR", "ACCESSORY" }) == 3, "case insensitive IDs");
        test(MS7D1EquipmentSetRegistry.CountMatched(required, new[] { "weapon", "", "accessory" }) == 2, "unequip 3->2");
        Debug.Log("[7D.3.1 PURE RESULT] " + pass + "/7 matching checks (does not test actual damage or Save)");
    }
}
#endif
