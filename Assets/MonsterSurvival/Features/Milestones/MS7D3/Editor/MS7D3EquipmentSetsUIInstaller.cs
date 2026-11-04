#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>7D.3: only installs a UI component onto the existing 3D.1 Inventory Canvas.</summary>
public sealed class MS7D3EquipmentSetsUIInstaller : EditorWindow
{
    private Vector2 scroll;
    private string lastReport = "Not audited yet.";
    private const string Root = "Tools/MonsterSurvival/7D Equipment Sets/";

    [MenuItem(Root + "13 - Open 7D.3 Equipment Sets UI")]
    public static void Open()
    {
        MS7D3EquipmentSetsUIInstaller win = GetWindow<MS7D3EquipmentSetsUIInstaller>("7D.3 Set UI");
        win.minSize = new Vector2(570f, 440f);
        win.Show();
    }

    [MenuItem(Root + "14 - Audit 7D.3 (READ ONLY)")]
    public static void MenuAudit()
    {
        Open();
        GetWindow<MS7D3EquipmentSetsUIInstaller>().Audit(true);
    }

    [MenuItem(Root + "15 - Export 7D.3 Report TXT")]
    public static void MenuExport()
    {
        Open();
        GetWindow<MS7D3EquipmentSetsUIInstaller>().Export();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        GUILayout.Label("MONSTERSURVIVAL 7D.3 — EQUIPMENT SET UI", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Shows set progress INSIDE existing Inventory/Equipment screen. No new battle HUD. " +
            "No PlayerStats, Inventory, Save, Equipment, Skill or Boss code is changed.", MessageType.Info);
        GUILayout.Label("Scene: " + SceneManager.GetActiveScene().name +
                      "  |  Play Mode: " + EditorApplication.isPlaying);
        GUILayout.Space(7);
        GUI.enabled = !EditorApplication.isPlayingOrWillChangePlaymode;
        if (GUILayout.Button("1 - INSTALL / REPAIR 7D.3 (BACKUP SCENE)", GUILayout.Height(39f))) Install();
        GUI.enabled = true;
        if (GUILayout.Button("2 - AUDIT REFERENCES / SET RULES (READ ONLY)", GUILayout.Height(34f))) Audit(true);
        if (GUILayout.Button("3 - EXPORT QA REPORT AS TXT", GUILayout.Height(32f))) Export();
        GUI.enabled = !EditorApplication.isPlayingOrWillChangePlaymode;
        if (GUILayout.Button("4 - REMOVE 7D.3 COMPONENT ONLY", GUILayout.Height(30f))) Remove();
        GUI.enabled = true;
        GUILayout.Space(9);
        EditorGUILayout.HelpBox("Rules=0 is not a compile error. Configure real piece IDs in the 7D.1 Registry, or use the OPTIONAL 7D.2.1 demo-set button. UI still shows a setup message without rules.", MessageType.Warning);
        GUILayout.Label("Last audit (no scene writes):", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(lastReport, GUILayout.MinHeight(220f));
        EditorGUILayout.EndScrollView();
    }

    private static bool SceneObject(Component c)
    {
        return c != null && !EditorUtility.IsPersistent(c) &&
               c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded;
    }

    private static T[] InScene<T>(Scene scene) where T : Component
    {
        var all = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var list = new System.Collections.Generic.List<T>();
        foreach (T x in all)
            if (SceneObject(x) && x.gameObject.scene.Equals(scene)) list.Add(x);
        return list.ToArray();
    }

    private static string BackupScene(Scene scene)
    {
        if (string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("Save SampleScene first.");
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Cannot save Scene before backup.");
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "MS_Backups", "7D3");
        Directory.CreateDirectory(dir);
        string destination = Path.Combine(dir,
            Path.GetFileNameWithoutExtension(scene.path) + "_before_7D3_" +
            DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity");
        File.Copy(Path.GetFullPath(scene.path), destination, false);
        return destination;
    }

    private void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[7D.3] Exit Play Mode before installing."); return;
        }
        try
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.name != "SampleScene")
                throw new InvalidOperationException("Open gameplay SampleScene before installing.");
            MS3DEquipmentUI[] equipmentUIs = InScene<MS3DEquipmentUI>(scene);
            MS3DPlayerEquipment[] equipments = InScene<MS3DPlayerEquipment>(scene);
            MS7D1EquipmentSetRegistry[] registries = InScene<MS7D1EquipmentSetRegistry>(scene);
            MS7D1EquippedItemReader[] readers = InScene<MS7D1EquippedItemReader>(scene);
            MS7D2SetBonusApplier[] appliers = InScene<MS7D2SetBonusApplier>(scene);
            MS7D3EquipmentSetsUI[] already = InScene<MS7D3EquipmentSetsUI>(scene);
            if (equipmentUIs.Length != 1 || equipments.Length != 1 ||
                registries.Length != 1 || readers.Length != 1 || appliers.Length != 1)
                throw new InvalidOperationException(
                    "Need exactly one existing 3D.1 Equipment UI/PlayerEquipment and one 7D.1 Reader/Registry and 7D.2 Applier in the scene. Run Audit.");
            if (already.Length > 1 || (already.Length == 1 && already[0].gameObject != equipmentUIs[0].gameObject))
                throw new InvalidOperationException("7D.3 exists on another object. Resolve duplicate before installing.");
            MS3DEquipmentUI equipUI = equipmentUIs[0];
            MS3DPlayerEquipment equipment = equipments[0];
            if (equipUI.equipmentWindow == null || equipUI.inventoryUI == null ||
                equipUI.inventoryUI.panel == null || equipUI.equipment != equipment)
                throw new InvalidOperationException("3D.1 UI references incomplete. Repair 3D.1 before 7D.3.");
            if (equipUI.inventoryUI.inventory == null ||
                equipUI.inventoryUI.inventory.gameObject != equipment.gameObject)
                throw new InvalidOperationException("3D.1 UI inventory and Equipment must belong to the SAME Player.");
            if (readers[0].gameObject != equipment.gameObject ||
                registries[0].gameObject != equipment.gameObject ||
                appliers[0].gameObject != equipment.gameObject)
                throw new InvalidOperationException("7D.1/7D.2 must be on SAME Player as 3D.1 Equipment.");
            string backup = BackupScene(scene);
            MS7D3EquipmentSetsUI display = equipUI.GetComponent<MS7D3EquipmentSetsUI>();
            if (display == null) display = Undo.AddComponent<MS7D3EquipmentSetsUI>(equipUI.gameObject);
            Undo.RecordObject(display, "Link 7D.3 Equipment Set UI");
            display.equipmentUI = equipUI;
            display.playerEquipment = equipment;
            display.equippedReader = readers[0];
            display.setRegistry = registries[0];
            display.bonusApplier = appliers[0];
            EditorUtility.SetDirty(display);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Save failed; backup: " + backup);
            Debug.Log("[7D.3 INSTALL] SUCCESS. Added/repaired a single UI component on existing Equipment Canvas. " +
                "Runtime-only SETS button / panel. No Inventory/Stats/Save changes. Backup: " + backup);
            Audit(true);
        }
        catch (Exception ex) { Debug.LogError("[7D.3 INSTALL ABORT] " + ex.Message); }
    }

    private string BuildAudit()
    {
        Scene scene = SceneManager.GetActiveScene();
        var sb = new StringBuilder();
        int pass = 0, warn = 0, error = 0;
        sb.AppendLine("MONSTERSURVIVAL 7D.3 — EQUIPMENT SETS UI / READ ONLY");
        sb.AppendLine("Generated=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
            " | Unity=" + Application.unityVersion + " | Scene=" + scene.name +
            " | PlayMode=" + EditorApplication.isPlaying);
        var uis = InScene<MS3DEquipmentUI>(scene);
        var displays = InScene<MS7D3EquipmentSetsUI>(scene);
        var equipments = InScene<MS3DPlayerEquipment>(scene);
        var readers = InScene<MS7D1EquippedItemReader>(scene);
        var registries = InScene<MS7D1EquipmentSetRegistry>(scene);
        var appliers = InScene<MS7D2SetBonusApplier>(scene);
        Check(sb, ref pass, ref warn, ref error, uis.Length == 1, "One existing 3D.1 Equipment UI (" + uis.Length + ")");
        Check(sb, ref pass, ref warn, ref error, displays.Length == 1, "One 7D.3 UI component (" + displays.Length + ")");
        Check(sb, ref pass, ref warn, ref error, equipments.Length == 1, "One PlayerEquipment (" + equipments.Length + ")");
        Check(sb, ref pass, ref warn, ref error, readers.Length == 1 && registries.Length == 1 && appliers.Length == 1,
            "One 7D.1 reader + registry + 7D.2 applier");
        if (uis.Length == 1 && displays.Length == 1 && equipments.Length == 1 &&
            readers.Length == 1 && registries.Length == 1 && appliers.Length == 1)
        {
            MS3DEquipmentUI eqUI = uis[0];
            MS7D3EquipmentSetsUI ui = displays[0];
            MS3DPlayerEquipment equip = equipments[0];
            Check(sb, ref pass, ref warn, ref error, ui.gameObject == eqUI.gameObject &&
                ui.equipmentUI == eqUI && eqUI.equipmentWindow != null &&
                eqUI.inventoryUI != null && eqUI.inventoryUI.panel != null,
                "7D.3 mounted on existing Inventory canvas; 3D.1 window intact");
            Check(sb, ref pass, ref warn, ref error, ui.playerEquipment == equip &&
                ui.equippedReader == readers[0] && ui.setRegistry == registries[0] &&
                ui.bonusApplier == appliers[0], "UI references match real Player and 7D.1/7D.2");
            Check(sb, ref pass, ref warn, ref error,
                readers[0].equipmentSource == equip && registries[0].reader == readers[0],
                "Reader source = 3D.1 player Equipment; registry -> Reader");
            string[] ids;
            string diagnostic;
            bool readOk = readers[0].TryRead(out ids, out diagnostic);
            Check(sb, ref pass, ref warn, ref error, readOk,
                "Equipment reader: " + diagnostic + (readOk ? " | IDs=" + string.Join(", ", ids) : ""));
            System.Collections.Generic.List<MS7D1EquipmentSetRegistry.Status> preview;
            bool previewOk = registries[0].TryPreview(out preview, out diagnostic);
            Check(sb, ref pass, ref warn, ref error, previewOk, "Registry preview readable: " + diagnostic);
            if (previewOk)
            {
                if (preview.Count == 0) { warn++; sb.AppendLine("[WARN] No enabled valid set rules. UI will show its setup message; configure 7D.1 Registry."); }
                else
                {
                    pass++; sb.AppendLine("[PASS] " + preview.Count + " enabled/valid set rules available to render");
                    foreach (var status in preview) sb.AppendLine("[INFO] " + status.ToString());
                }
            }
            sb.AppendLine("[INFO] Bonus gates mapping=" + appliers[0].mappingVerifiedByUser +
                ", enabled=" + appliers[0].enableActualBonuses +
                " | Applied Damage +" + appliers[0].AppliedDamageBonus.ToString("0.###") +
                ", ASPD +" + appliers[0].AppliedAttackSpeedBonus.ToString("0.###"));
            sb.AppendLine("[INFO] SETS popup exists at runtime ONLY while Inventory UI component is active; Edit Mode audit cannot see generated children.");
        }
        sb.AppendLine("[MANUAL] Open Inventory (I), press SETS, verify cards and scrolling, close X/outside, equip/unequip and return.");
        sb.AppendLine("[MANUAL] Test 1920x1080, 1280x720, 1024x768, small Game view; check no Equip/Unequip clicks blocked when popup closed.");
        sb.AppendLine("[MANUAL] Save/Profile, Level Up 7C, Boss HP/Gold/Equipment bonuses unchanged; actual gameplay QA still required.");
        sb.AppendLine("RESULT PASS=" + pass + " WARN=" + warn + " ERROR=" + error);
        return sb.ToString();
    }

    private static void Check(StringBuilder b, ref int pass, ref int warn, ref int error, bool result, string name)
    {
        if (result) { pass++; b.AppendLine("[PASS] " + name); }
        else { error++; b.AppendLine("[ERROR] " + name); }
    }

    private void Audit(bool log)
    {
        lastReport = BuildAudit();
        if (log) Debug.Log(lastReport);
        Repaint();
    }

    private void Export()
    {
        string path = EditorUtility.SaveFilePanel("Save 7D.3 QA report", "",
            "MS7D3_EquipmentSetsUI_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt", "txt");
        if (string.IsNullOrEmpty(path)) return;
        lastReport = BuildAudit();
        File.WriteAllText(path, lastReport, new UTF8Encoding(false));
        Debug.Log("[7D.3] Read-only QA TXT exported: " + path);
    }

    private void Remove()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        MS7D3EquipmentSetsUI[] displays = InScene<MS7D3EquipmentSetsUI>(scene);
        if (displays.Length == 0) { Debug.Log("[7D.3] Nothing to remove."); return; }
        if (!EditorUtility.DisplayDialog("Remove 7D.3 only",
            "Delete only 7D.3 Equipment Sets UI component(s) from this Scene? All Equipment/Inventory/Save/7D.1/7D.2 remains.",
            "Remove 7D.3", "Cancel")) return;
        try
        {
            string backup = BackupScene(scene);
            foreach (var display in displays) Undo.DestroyObjectImmediate(display);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[7D.3 REMOVE] Removed " + displays.Length + " 7D.3 components only. Backup: " + backup);
            Audit(true);
        }
        catch (Exception e) { Debug.LogError("[7D.3 REMOVE ABORT] " + e.Message); }
    }
}
#endif
