#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 7D.4.1 opt-in activation gateway for ALREADY INSTALLED 7D.2.
/// No changes to gameplay scripts, item data, Profile, Save or PlayerStats.
/// Changing the two serialized enable flags requires explicit confirmation,
/// Edit Mode, strict compatibility preflight and a Scene backup.
/// Existing 7D.4 window performs live capture and release QA.
/// </summary>
public sealed class MS7D41GuidedBonusQA : EditorWindow
{
    private const string Base = "Tools/MonsterSurvival/7D Equipment Sets/";
    private string audit = "Run preflight first.";
    private Vector2 scroll;
    private bool confirmedRealMapping;

    [MenuItem(Base + "25 - Open 7D.4.1 Guided Bonus Activation")]
    public static void Open()
    {
        var window = GetWindow<MS7D41GuidedBonusQA>("7D.4.1 Guided QA");
        window.minSize = new Vector2(620, 410);
        window.Show();
    }

    [MenuItem(Base + "26 - 7D.4.1 Read-only Preflight")]
    public static void PreflightMenu()
    {
        var window = GetWindow<MS7D41GuidedBonusQA>("7D.4.1 Guided QA");
        window.RunPreflight();
        Debug.Log(window.audit);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("7D.4.1 — GUIDED SET BONUS ACTIVATION", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Optional companion for 7D.4. Does NOT fake QA or change gear. " +
            "Enable / Disable buttons only change the two existing 7D.2 Inspector gates, " +
            "in Edit Mode, after backup and your explicit confirmation. Runtime stats are managed ONLY by existing 7D.2.",
            MessageType.Info);
        EditorGUILayout.LabelField("Scene: " + SceneManager.GetActiveScene().name +
            " | Play: " + EditorApplication.isPlaying);
        if (GUILayout.Button("1 - PRE-FLIGHT: REAL 3 SLOT + RULES + PLAYERSTATS (READ ONLY)", GUILayout.Height(33)))
            RunPreflight();
        EditorGUILayout.Space(4);
        confirmedRealMapping = EditorGUILayout.ToggleLeft(
            "I have personally tested 3D.1 Equip/Unequip and stable real IDs in Play Mode", confirmedRealMapping);
        EditorGUI.BeginDisabledGroup(EditorApplication.isPlayingOrWillChangePlaymode || !confirmedRealMapping);
        if (GUILayout.Button("2 - ENABLE BOTH 7D.2 BONUS GATES (BACKUP + CONFIRM)", GUILayout.Height(34)))
            SetGates(true);
        EditorGUI.EndDisabledGroup();
        EditorGUI.BeginDisabledGroup(EditorApplication.isPlayingOrWillChangePlaymode);
        if (GUILayout.Button("3 - DISABLE BOTH GATES (BACKUP + CONFIRM)", GUILayout.Height(30)))
            SetGates(false);
        EditorGUI.EndDisabledGroup();
        if (GUILayout.Button("4 - EXPORT CURRENT READ-ONLY SNAPSHOT TXT", GUILayout.Height(30)))
            Export();
        EditorGUILayout.HelpBox("After ENABLE: enter Play Mode, use Tools > MonsterSurvival > " +
            "7D Equipment Sets > 21 - Open 7D.4 Final Equipment QA. " +
            "Capture 3/3 -> 2/3 -> 1/3 -> 2/3 -> 3/3. " +
            "This window does not replace the original 7D.4 release gate.", MessageType.Warning);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(audit, GUILayout.MinHeight(175), GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private static T[] CurrentSceneComponents<T>() where T : Component
    {
        var list = new List<T>();
        Scene s = SceneManager.GetActiveScene();
        foreach (T entry in Resources.FindObjectsOfTypeAll<T>())
            if (entry != null && !EditorUtility.IsPersistent(entry) &&
                entry.gameObject.scene.IsValid() && entry.gameObject.scene == s)
                list.Add(entry);
        return list.ToArray();
    }

    private static FieldInfo FloatField(Type type, string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type t = type; t != null; t = t.BaseType)
        {
            FieldInfo f = t.GetField(name, flags);
            if (f != null) return f.FieldType == typeof(float) && !f.IsStatic && !f.IsInitOnly ? f : null;
        }
        return null;
    }

    private bool Preflight(out MS7D2SetBonusApplier applier, out string result)
    {
        applier = null;
        var sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 7D.4.1 - READ ONLY PRE-FLIGHT");
        sb.AppendLine("Unity=" + Application.unityVersion + " | Scene=" + SceneManager.GetActiveScene().name +
                      " | PlayMode=" + EditorApplication.isPlaying + " | Time=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.name != "SampleScene")
        { sb.AppendLine("[ERROR] Open saved gameplay SampleScene first."); result = sb.ToString(); return false; }
        PlayerStats[] ps = CurrentSceneComponents<PlayerStats>();
        MS3DPlayerEquipment[] gs = CurrentSceneComponents<MS3DPlayerEquipment>();
        MS7D1EquippedItemReader[] rs = CurrentSceneComponents<MS7D1EquippedItemReader>();
        MS7D1EquipmentSetRegistry[] ss = CurrentSceneComponents<MS7D1EquipmentSetRegistry>();
        MS7D2SetBonusApplier[] aps = CurrentSceneComponents<MS7D2SetBonusApplier>();
        if (ps.Length != 1 || gs.Length != 1 || rs.Length != 1 || ss.Length != 1 || aps.Length != 1)
        {
            sb.AppendLine("[ERROR] Need exactly one each: PlayerStats=" + ps.Length + ", 3DEquipment=" + gs.Length +
                          ", Reader=" + rs.Length + ", Registry=" + ss.Length + ", Applier=" + aps.Length);
            result = sb.ToString(); return false;
        }
        PlayerStats player = ps[0]; MS3DPlayerEquipment gear = gs[0];
        MS7D1EquippedItemReader reader = rs[0]; MS7D1EquipmentSetRegistry registry = ss[0];
        applier = aps[0];
        if (gear.gameObject != player.gameObject || reader.gameObject != player.gameObject ||
            registry.gameObject != player.gameObject || applier.gameObject != player.gameObject ||
            reader.equipmentSource != gear || registry.reader != reader ||
            applier.playerStats != player || applier.equipmentReader != reader || applier.setRegistry != registry)
        { sb.AppendLine("[ERROR] All components/links must match the SAME scene Player."); result = sb.ToString(); return false; }
        sb.AppendLine("[PASS] Exactly one Player and complete 3D.1 -> 7D.1 -> 7D.2 links");
        if (reader.threeSlotCollectionPath != "" || reader.slot1Path != "weapon" ||
            reader.slot2Path != "armor" || reader.slot3Path != "accessory" ||
            reader.itemIdPath != "itemId" || reader.allowObjectNameFallback)
        { sb.AppendLine("[ERROR] Reader paths must be weapon/armor/accessory, itemId, name fallback OFF."); result = sb.ToString(); return false; }
        sb.AppendLine("[PASS] Exact 3D.1 slot paths and itemId mapping");
        string[] ids; string readMsg;
        if (!reader.TryRead(out ids, out readMsg) || ids == null || ids.Length != 3)
        { sb.AppendLine("[ERROR] Reader failed: " + readMsg); result = sb.ToString(); return false; }
        sb.AppendLine("[PASS] " + readMsg);
        for (int i=0; i<3; i++)
        {
            MS3DEquipmentType type = i == 0 ? MS3DEquipmentType.Weapon : i == 1 ? MS3DEquipmentType.Armor : MS3DEquipmentType.Accessory;
            MS3AItemData item = gear.GetEquipped(type);
            string original = item != null ? (item.itemId ?? "").Trim() : "";
            if (!string.Equals(original, (ids[i] ?? "").Trim(), StringComparison.Ordinal))
            { sb.AppendLine("[ERROR] Slot " + i + " differs from real GetEquipped API."); result = sb.ToString(); return false; }
        }
        sb.AppendLine("[PASS] Reader IDs identical to all three real 3D.1 slots");
        float wantedDamage, wantedAttack; string diagnostic;
        if (!applier.TryEvaluate(out wantedDamage, out wantedAttack, out diagnostic))
        { sb.AppendLine("[ERROR] 7D.2 TryEvaluate failed: " + diagnostic); result = sb.ToString(); return false; }
        sb.AppendLine("[PASS] " + diagnostic);
        FieldInfo damage = FloatField(player.GetType(), applier.damageMultiplierField);
        FieldInfo speed = FloatField(player.GetType(), applier.attackSpeedMultiplierField);
        if (damage == null || speed == null || damage == speed)
        { sb.AppendLine("[ERROR] PlayerStats multiplier fields not writable/distinct floats."); result = sb.ToString(); return false; }
        sb.AppendLine("[PASS] Writable PlayerStats multipliers exist and are distinct");
        sb.AppendLine("[INFO] Desired set bonuses: Damage +" + wantedDamage.ToString("0.###") + " | ASPD +" + wantedAttack.ToString("0.###"));
        sb.AppendLine("[INFO] Applied by 7D.2 in this Play session: Damage +" + applier.AppliedDamageBonus.ToString("0.###") +
                      " | ASPD +" + applier.AppliedAttackSpeedBonus.ToString("0.###"));
        sb.AppendLine("[INFO] Raw PlayerStats: DMG=" + damage.GetValue(player) + " | ASPD=" + speed.GetValue(player));
        sb.AppendLine("[INFO] Gates: mapping=" + applier.mappingVerifiedByUser +
                      " enabled=" + applier.enableActualBonuses);
        sb.AppendLine("[INFO] 7D.2 diagnostic: " + applier.LastDiagnostic);
        sb.AppendLine("[PASS] SAFE TO ENABLE GATES after deliberate player mapping confirmation (not a runtime QA PASS)");
        result = sb.ToString(); return true;
    }

    private void RunPreflight()
    {
        MS7D2SetBonusApplier applier; string report;
        bool pass = Preflight(out applier, out report);
        audit = report;
        if (pass) Debug.Log("[7D.4.1] Preflight PASS (no writes)\n" + report);
        else Debug.LogWarning("[7D.4.1] Preflight did not pass (no writes)\n" + report);
        Repaint();
    }

    private static string BackupScene(Scene scene, string tag)
    {
        if (string.IsNullOrEmpty(scene.path)) throw new IOException("Save SampleScene before modifying gates.");
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unable to save Scene before backup.");
        string backupDir = Path.Combine(Directory.GetCurrentDirectory(), "MS_Backups", "7D41");
        Directory.CreateDirectory(backupDir);
        string backupName = Path.GetFileNameWithoutExtension(scene.path) + "_" + tag + "_" +
                            DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity";
        string backup = Path.Combine(backupDir, backupName);
        File.Copy(Path.GetFullPath(scene.path), backup, false);
        return backup;
    }

    private void SetGates(bool enable)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogError("[7D.4.1] Exit Play Mode before toggling scene gates."); return; }
        if (enable && !confirmedRealMapping)
        { Debug.LogWarning("[7D.4.1] Confirm real 3D.1 Equip/Unequip mapping before enabling."); return; }
        MS7D2SetBonusApplier applier; string report;
        if (!Preflight(out applier, out report))
        { audit = report; Debug.LogError("[7D.4.1] Preflight blocked gate change\n" + report); return; }
        string verb = enable ? "ENABLE" : "DISABLE";
        if (!EditorUtility.DisplayDialog("7D.4.1 - " + verb + " REAL BONUSES",
            "This changes ONLY two serialized gate flags in the current SampleScene. " +
            (enable ? "The existing 7D.2 runtime system WILL adjust PlayerStats multipliers during Play Mode. " +
                      "Do not claim final QA before 7D.4 reports real bonus transitions. " :
                      "In the next run, 7D.2 will remain preview-only. ") +
            "The Scene will be backed up and saved. Continue?", verb + " GATES", "Cancel")) return;
        try
        {
            string backup = BackupScene(applier.gameObject.scene, enable ? "before_enable" : "before_disable");
            Undo.RecordObject(applier, "7D.4.1 " + verb + " bonus gates");
            applier.mappingVerifiedByUser = enable;
            applier.enableActualBonuses = enable;
            EditorUtility.SetDirty(applier);
            EditorSceneManager.MarkSceneDirty(applier.gameObject.scene);
            if (!EditorSceneManager.SaveScene(applier.gameObject.scene))
                throw new IOException("Could not save Scene. Restore from " + backup);
            Debug.Log("[7D.4.1] " + verb + " gates saved. Scene backup: " + backup);
            confirmedRealMapping = false;
            RunPreflight();
        }
        catch (Exception ex) { Debug.LogError("[7D.4.1] Gate change failed: " + ex.Message); }
    }

    private void Export()
    {
        MS7D2SetBonusApplier applier; string report;
        Preflight(out applier, out report);
        audit = report;
        string filepath = EditorUtility.SaveFilePanel("Export 7D.4.1 snapshot", "",
            "MS7D41_BonusGateQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt", "txt");
        if (string.IsNullOrEmpty(filepath)) return;
        File.WriteAllText(filepath, report, new UTF8Encoding(false));
        Debug.Log("[7D.4.1] Saved read-only audit: " + filepath);
    }
}
#endif
