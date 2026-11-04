#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 7D.2.1: exact 3D.1/5A.3 binding. Editor-only, no old gameplay source edits.
// Requires the already installed MS3DPlayerEquipment, 7D.1 and 7D.2 scripts.
public sealed class MS7D21EquipmentAutoBinder : EditorWindow
{
    private const string Prefix = "Tools/MonsterSurvival/7D Equipment Sets/";
    private const string DemoWeapon = "dusk_blade_3d1";
    private const string DemoArmor = "violet_armor_3d1";
    private const string DemoAccessory = "warden_ring_3d1";

    private Vector2 scroll;
    private string lastReport = "Click AUDIT to inspect the three real Equipment slots.";

    [MenuItem(Prefix + "9 - Open 7D.2.1 Auto Binder")]
    public static void Open()
    {
        var w = GetWindow<MS7D21EquipmentAutoBinder>("MS 7D.2.1 Auto Bind");
        w.minSize = new Vector2(590, 470);
        w.Show();
    }

    [MenuItem(Prefix + "10 - Audit Real Equipment (READ ONLY)")]
    public static void MenuAudit()
    {
        Open();
        GetWindow<MS7D21EquipmentAutoBinder>().Audit();
    }

    [MenuItem(Prefix + "11 - Export 7D.2.1 TXT")]
    public static void MenuExport()
    {
        Open();
        GetWindow<MS7D21EquipmentAutoBinder>().Export();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("7D.2.1 - AUTO BIND REAL 3D.1 EQUIPMENT", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Reads MS3DPlayerEquipment.weapon / armor / accessory and MS3AItemData.itemId. " +
            "No Save, Inventory, stat, boss, prefab or existing C# source modifications.", MessageType.Info);
        EditorGUILayout.LabelField("Scene", SceneManager.GetActiveScene().name);
        EditorGUILayout.LabelField("Play Mode", EditorApplication.isPlaying ? "Yes" : "No");

        GUI.enabled = !EditorApplication.isPlayingOrWillChangePlaymode;
        if (GUILayout.Button("1 - AUTO BIND EXACT 3D.1 SLOTS (BACKUP SCENE)", GUILayout.Height(38))) AutoBind();
        if (GUILayout.Button("2 - OPTIONAL: CREATE REAL 3D.1 DEMO SET (BACKUP SCENE)", GUILayout.Height(32))) SeedDemo();
        GUI.enabled = true;
        if (GUILayout.Button("3 - AUDIT READER + REAL EQUIPPED IDs (READ ONLY)", GUILayout.Height(33))) Audit();
        if (GUILayout.Button("4 - EXPORT TXT REPORT", GUILayout.Height(32))) Export();

        EditorGUILayout.HelpBox(
            "IMPORTANT: This tool switches BOTH 7D.2 bonus gates OFF while installing. " +
            "Only turn them ON after an Edit/Play Audit and an equip/unequip test. " +
            "Optional demo rule uses actual catalog references; it never grants equipment.", MessageType.Warning);
        EditorGUILayout.LabelField("REPORT", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(lastReport, GUILayout.MinHeight(215));
        EditorGUILayout.EndScrollView();
    }

    private static bool SceneComponent(Component c)
    {
        return c != null && !EditorUtility.IsPersistent(c) && c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded;
    }

    private static bool TryFind(out PlayerStats player, out MS3DPlayerEquipment gear,
        out MS7D1EquippedItemReader reader, out MS7D1EquipmentSetRegistry registry,
        out MS7D2SetBonusApplier applier, out string error)
    {
        player = null; gear = null; reader = null; registry = null; applier = null;
        error = "";
        var players = UnityEngine.Object.FindObjectsByType<PlayerStats>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (players.Length != 1 || !SceneComponent(players[0]))
        {
            error = "Need exactly ONE Scene PlayerStats in SampleScene; found " + players.Length;
            return false;
        }
        player = players[0];
        gear = player.GetComponent<MS3DPlayerEquipment>();
        reader = player.GetComponent<MS7D1EquippedItemReader>();
        registry = player.GetComponent<MS7D1EquipmentSetRegistry>();
        applier = player.GetComponent<MS7D2SetBonusApplier>();
        if (gear == null || reader == null || registry == null || applier == null)
        {
            error = "Missing component on the same Player: MS3DPlayerEquipment, MS7D1EquippedItemReader, MS7D1EquipmentSetRegistry or MS7D2SetBonusApplier";
            return false;
        }
        if (player.gameObject.scene.name != "SampleScene")
        {
            error = "Open SampleScene (gameplay Scene), not MainMenu";
            return false;
        }
        if (UnityEngine.Object.FindObjectsByType<MS7D1EquippedItemReader>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1 ||
            UnityEngine.Object.FindObjectsByType<MS7D1EquipmentSetRegistry>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1 ||
            UnityEngine.Object.FindObjectsByType<MS7D2SetBonusApplier>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1 ||
            UnityEngine.Object.FindObjectsByType<MS3DPlayerEquipment>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1)
        {
            error = "Duplicate Equipment, Reader, Registry or Applier in loaded Scenes. Stop and resolve duplication.";
            return false;
        }
        return true;
    }

    private static bool BeginEdit(out PlayerStats player, out MS3DPlayerEquipment gear,
        out MS7D1EquippedItemReader reader, out MS7D1EquipmentSetRegistry registry,
        out MS7D2SetBonusApplier applier, out string error)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            player = null; gear = null; reader = null; registry = null; applier = null;
            error = "Exit Play Mode before modifying Scene.";
            return false;
        }
        return TryFind(out player, out gear, out reader, out registry, out applier, out error);
    }

    private static string BackupScene(Scene scene)
    {
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            throw new InvalidOperationException("Save SampleScene first (Ctrl+S).");
        EditorSceneManager.SaveOpenScenes();
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "MS_Backups", "7D21");
        Directory.CreateDirectory(dir);
        string destination = Path.Combine(dir, Path.GetFileNameWithoutExtension(scene.path) +
            "_before7D21_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity");
        File.Copy(Path.GetFullPath(scene.path), destination, false);
        return destination;
    }

    private void AutoBind()
    {
        PlayerStats player; MS3DPlayerEquipment gear; MS7D1EquippedItemReader reader;
        MS7D1EquipmentSetRegistry registry; MS7D2SetBonusApplier applier; string error;
        if (!BeginEdit(out player, out gear, out reader, out registry, out applier, out error))
        { Debug.LogError("[7D.2.1] " + error); return; }
        if (reader.equipmentSource != null && reader.equipmentSource != gear)
        {
            Debug.LogError("[7D.2.1] Reader currently points to a different source (" +
                reader.equipmentSource.GetType().Name + "). No overwrite. Inspect manually.");
            return;
        }
        if (!EditorUtility.DisplayDialog("7D.2.1 - Exact binding",
            "Configure Reader: Source=MS3DPlayerEquipment, slots=weapon/armor/accessory, itemId=itemId.\n" +
            "Wire Registry/Applier and switch BOTH actual bonus gates OFF until Play Mode verification.\n" +
            "Scene backup will be created outside Assets. Continue?", "Bind safely", "Cancel")) return;
        try
        {
            string backup = BackupScene(player.gameObject.scene);
            Undo.RecordObject(reader, "7D.2.1 Bind Real Equipment Slots");
            Undo.RecordObject(registry, "7D.2.1 Wire Registry");
            Undo.RecordObject(applier, "7D.2.1 Safe Bonus Wiring");
            reader.equipmentSource = gear;
            reader.threeSlotCollectionPath = "";
            reader.slot1Path = "weapon";
            reader.slot2Path = "armor";
            reader.slot3Path = "accessory";
            reader.itemIdPath = "itemId";
            reader.allowObjectNameFallback = false;
            registry.reader = reader;
            applier.playerStats = player;
            applier.equipmentReader = reader;
            applier.setRegistry = registry;
            applier.mappingVerifiedByUser = false;
            applier.enableActualBonuses = false;
            EditorUtility.SetDirty(reader);
            EditorUtility.SetDirty(registry);
            EditorUtility.SetDirty(applier);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            if (!EditorSceneManager.SaveScene(player.gameObject.scene))
                throw new IOException("Could not save Scene; backup: " + backup);
            Debug.Log("[7D.2.1] Exact Equipment auto-binding installed. Gates OFF. Backup: " + backup);
            Audit();
        }
        catch (Exception ex)
        {
            Debug.LogError("[7D.2.1] Binding failed: " + ex.Message);
        }
    }

    private static MS3AItemData CatalogItem(MS3DEquipmentCatalog catalog, string id, MS3DEquipmentType type)
    {
        if (catalog == null || catalog.entries == null) return null;
        foreach (MS3DEquipmentDefinition row in catalog.entries)
        {
            if (row != null && row.item != null && row.type == type &&
                String.Equals(row.item.itemId, id, StringComparison.Ordinal))
                return row.item;
        }
        return null;
    }

    private void SeedDemo()
    {
        PlayerStats player; MS3DPlayerEquipment gear; MS7D1EquippedItemReader reader;
        MS7D1EquipmentSetRegistry registry; MS7D2SetBonusApplier applier; string error;
        if (!BeginEdit(out player, out gear, out reader, out registry, out applier, out error))
        { Debug.LogError("[7D.2.1] " + error); return; }
        if (gear.catalog == null ||
            CatalogItem(gear.catalog, DemoWeapon, MS3DEquipmentType.Weapon) == null ||
            CatalogItem(gear.catalog, DemoArmor, MS3DEquipmentType.Armor) == null ||
            CatalogItem(gear.catalog, DemoAccessory, MS3DEquipmentType.Accessory) == null)
        {
            Debug.LogWarning("[7D.2.1] 3D.1 demo items not found in CURRENT Player catalog. No fictional IDs inserted. Configure own real set manually.");
            return;
        }
        if (registry.sets == null)
        { Debug.LogError("[7D.2.1] Registry.sets is null. Repair 7D.1 before seeding."); return; }
        foreach (var rule in registry.sets)
            if (rule != null && rule.enabledRule)
            {
                Debug.LogWarning("[7D.2.1] Existing enabled rules found. Demo seeding aborted to preserve user set definitions.");
                return;
            }
        if (!EditorUtility.DisplayDialog("7D.2.1 - OPTIONAL DEMO SET",
            "All three 3D.1 demo items exist in the CURRENT Equipment Catalog.\n" +
            "Append ONE demo set: dusk_blade_3d1 + violet_armor_3d1 + warden_ring_3d1.\n" +
            "Rules remain editable and bonus gates are switched OFF. Does NOT grant items.\nContinue?", "Add demo set", "Cancel")) return;
        try
        {
            string backup = BackupScene(player.gameObject.scene);
            Undo.RecordObject(registry, "7D.2.1 Add Demo Set");
            Undo.RecordObject(applier, "7D.2.1 Keep Bonuses Off");
            registry.sets.Add(new MS7D1EquipmentSetRegistry.Rule
            {
                enabledRule = true,
                setId = "7d21.legacy_3d1_demo",
                displayName = "Dusk / Violet / Warden (Demo Set)",
                pieceIds = new[] { DemoWeapon, DemoArmor, DemoAccessory },
                damageBonusAtTwo = .08f,
                attackSpeedBonusAtThree = .12f
            });
            applier.mappingVerifiedByUser = false;
            applier.enableActualBonuses = false;
            EditorUtility.SetDirty(registry);
            EditorUtility.SetDirty(applier);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            EditorSceneManager.SaveScene(player.gameObject.scene);
            Debug.Log("[7D.2.1] Created real-asset 3D.1 DEMO set; no gear granted. Backup: " + backup);
            Audit();
        }
        catch (Exception ex) { Debug.LogError("[7D.2.1] Demo set aborted: " + ex.Message); }
    }

    private string Report()
    {
        int pass = 0, warn = 0, errorCount = 0;
        var sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 7D.2.1 - REAL 3D.1 EQUIPMENT CONNECTION / READ ONLY");
        sb.AppendLine("Unity=" + Application.unityVersion + " | Scene=" + SceneManager.GetActiveScene().name +
            " | PlayMode=" + EditorApplication.isPlaying + " | Time=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        PlayerStats player; MS3DPlayerEquipment gear; MS7D1EquippedItemReader reader;
        MS7D1EquipmentSetRegistry registry; MS7D2SetBonusApplier applier; string message;
        if (!TryFind(out player, out gear, out reader, out registry, out applier, out message))
        {
            sb.AppendLine("[ERROR] " + message);
            sb.AppendLine("RESULT PASS=0 WARN=0 ERROR=1");
            return sb.ToString();
        }
        Action<bool, string> check = (ok, info) =>
        {
            if (ok) { pass++; sb.AppendLine("[PASS] " + info); }
            else { errorCount++; sb.AppendLine("[ERROR] " + info); }
        };
        check(reader.equipmentSource == gear, "Equipment Source is SAME Player MS3DPlayerEquipment");
        check(String.IsNullOrEmpty(reader.threeSlotCollectionPath) &&
            reader.slot1Path == "weapon" && reader.slot2Path == "armor" && reader.slot3Path == "accessory",
            "Reader three exact 3D.1 private member paths");
        check(reader.itemIdPath == "itemId" && !reader.allowObjectNameFallback,
            "Stable MS3AItemData.itemId (never displayName or Unity name)");
        check(registry.reader == reader, "Registry -> Reader");
        check(applier.playerStats == player && applier.equipmentReader == reader && applier.setRegistry == registry,
            "Applier -> PlayerStats / Reader / Registry");
        check(gear.catalog != null, "3D.1 EquipmentCatalog present");
        string[] ids; string readerDiagnostic;
        bool read = reader.TryRead(out ids, out readerDiagnostic);
        check(read, "Reader TryRead " + readerDiagnostic);
        int equippedCount = 0;
        if (read)
        {
            for (int i = 0; i < 3; i++)
            {
                MS3AItemData equipped = gear.GetEquipped((MS3DEquipmentType)i);
                string expected = equipped != null ? equipped.itemId : "";
                string actual = ids[i] ?? "";
                bool same = String.Equals(expected, actual, StringComparison.Ordinal);
                check(same, ((MS3DEquipmentType)i) + " real API itemId: " +
                    (String.IsNullOrEmpty(expected) ? "[EMPTY]" : expected) +
                    " | reader: " + (String.IsNullOrEmpty(actual) ? "[EMPTY]" : actual));
                if (equipped != null)
                {
                    equippedCount++;
                    MS3DEquipmentDefinition definition;
                    check(gear.catalog != null && gear.catalog.TryGet(equipped, out definition) &&
                        definition != null && (int)definition.type == i,
                        "Catalog recognizes worn " + (MS3DEquipmentType)i);
                }
            }
        }
        if (equippedCount == 0)
        {
            warn++; sb.AppendLine("[WARN] 0 equipped. Empty state can be checked now, but equip/unequip mapping is NOT runtime-verified.");
        }
        sb.AppendLine("[INFO] 7D.2 bonus gates: MappingVerified=" + applier.mappingVerifiedByUser +
            " | ActualEnabled=" + applier.enableActualBonuses);
        sb.AppendLine("[INFO] Set rules=" + (registry.sets != null ? registry.sets.Count.ToString() : "NULL"));
        List<MS7D1EquipmentSetRegistry.Status> statuses; string setDiagnostic;
        bool validPreview = registry.TryPreview(out statuses, out setDiagnostic);
        if (validPreview)
        {
            pass++;
            sb.AppendLine("[PASS] Registry preview: " + setDiagnostic);
            foreach (var status in statuses) sb.AppendLine("[SET] " + status.ToString());
            if (statuses.Count == 0)
            { warn++; sb.AppendLine("[WARN] No enabled set rules. Mapping OK but bonuses cannot activate without real piece IDs."); }
        }
        else
        {
            errorCount++;
            sb.AppendLine("[ERROR] Registry TryPreview: " + setDiagnostic);
        }
        if (EditorApplication.isPlaying)
        {
            sb.AppendLine("[LIVE] Own applied: Damage+" + applier.AppliedDamageBonus.ToString("0.###") +
                " AttackSpeed+" + applier.AppliedAttackSpeedBonus.ToString("0.###"));
            sb.AppendLine("[LIVE] Applier diagnostic: " + applier.LastDiagnostic);
            sb.AppendLine("[LIVE] Check actual PlayerStats multiplier values in Player Inspector; report tracks the bonus owned by 7D.2 only.");
        }
        else
        {
            sb.AppendLine("[INFO] Edit Mode audit does NOT confirm stat bonuses or inventory restore. Test in Play Mode.");
        }
        sb.AppendLine("[MANUAL] Equip weapon + armor + accessory; confirm 1/3 -> 2/3 -> 3/3, then unequip -> 2/3 -> 1/3.");
        sb.AppendLine("[MANUAL] Enable 7D.2 gates only after verifying real IDs and rules; ensure bonuses revert once.");
        sb.AppendLine("[MANUAL] Check Save/Profile switching, Victory/Defeat, and 3D.2 stat bonuses remain intact.");
        sb.AppendLine("RESULT PASS=" + pass + " WARN=" + warn + " ERROR=" + errorCount);
        return sb.ToString();
    }

    private void Audit()
    {
        lastReport = Report();
        Debug.Log("[7D.2.1 AUDIT]\n" + lastReport);
        Repaint();
    }

    private void Export()
    {
        lastReport = Report();
        string path = EditorUtility.SaveFilePanel("Export 7D.2.1 Equipment Mapping QA", "",
            "MS7D21_EquipmentAutoBind_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt", "txt");
        if (String.IsNullOrEmpty(path)) return;
        File.WriteAllText(path, lastReport, new UTF8Encoding(false));
        Debug.Log("[7D.2.1] Saved read-only report: " + path);
    }
}
#endif
