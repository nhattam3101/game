#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Read-only 3D.5 end-to-end audit. Does not simulate clicks or mutate objects.
public sealed class MS3D5EquipmentQAWindow : EditorWindow
{
    private static readonly string[] ManualTests = {
        "Open Inventory with I, select 20 different cells, X and USE work",
        "Equip Weapon; icon, slot glow and world ring appear",
        "Swap Weapon; old weapon returns to inventory, new stats apply",
        "Unequip Armor; Max HP bonus removed correctly without free healing",
        "Equip/unquip with Skill buffs; Skill bonuses remain unchanged",
        "Equip Legendary; golden, stronger pulse shown",
        "Inventory full; failed swap/unequip never loses gear",
        "Pause/Skill/Victory/GameOver overlays do not overlap Inventory",
        "Kill Shadow Titan; legacy 3B drops and 3D4 gear can coexist",
        "Kill Grave Warden; exclusive loot appears when rolled",
        "Restart stage; no stale world VFX, no lost button listeners",
        "Check 16:9, 16:10, 4:3 and 21:9 at stable resolutions"
    };
    private readonly bool[] manual = new bool[12];
    private Vector2 scroll;
    private string lastReport = "Press SCAN to audit the currently open Scene.";
    private double lastScan;

    [MenuItem("Tools/MonsterSurvival/3D Equipment/16 - OPEN 3D.5 Live Final QA (READ ONLY)")]
    public static void OpenWindow()
    {
        var w = GetWindow<MS3D5EquipmentQAWindow>("3D.5 Equipment QA");
        w.minSize = new Vector2(480, 450);
        w.Scan();
    }

    private void OnEnable() { EditorApplication.playModeStateChanged += OnPlayModeChange; }
    private void OnDisable() { EditorApplication.playModeStateChanged -= OnPlayModeChange; }
    private void OnPlayModeChange(PlayModeStateChange change) { Repaint(); }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("MONSTERSURVIVAL — 3D.5 EQUIPMENT FINAL QA", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("READ ONLY. Automated results inspect references and current state; they do NOT prove every UI interaction works. Use the manual checklist for gameplay tests.", MessageType.Info);
        EditorGUILayout.LabelField("Mode", Application.isPlaying ? "PLAY MODE (live snapshot)" : "EDIT MODE (structure only)");
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("SCAN CURRENT SCENE", GUILayout.Height(28))) Scan();
            if (GUILayout.Button("EXPORT TXT", GUILayout.Height(28))) Export();
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("LATEST AUTOMATED SCAN", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(lastReport, GUILayout.MinHeight(165));
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("MANUAL PLAY MODE CHECKLIST (confirm only when personally tested)", EditorStyles.boldLabel);
        for (int i = 0; i < ManualTests.Length; i++)
            manual[i] = EditorGUILayout.ToggleLeft((i + 1).ToString("00") + ". " + ManualTests[i], manual[i]);
        int done = 0; foreach (bool b in manual) if (b) done++;
        EditorGUILayout.LabelField("Manually confirmed: " + done + "/" + ManualTests.Length);
        if (GUILayout.Button("CLEAR MANUAL CHECKMARKS")) Array.Clear(manual, 0, manual.Length);
        EditorGUILayout.EndScrollView();
    }

    private void Scan()
    {
        StringBuilder s = new StringBuilder();
        Scene scene = SceneManager.GetActiveScene();
        s.AppendLine("MonsterSurvival Equipment 3D.5 Final QA | " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        s.AppendLine("Scene: " + scene.name + " | Unity: " + Application.unityVersion +
                     " | Mode: " + (Application.isPlaying ? "Play" : "Edit"));
        s.AppendLine("----------------------------------------------------------");
        MS3DEquipmentUI ui = FindScene<MS3DEquipmentUI>(scene);
        if (!ui) { Mark(s, "FAIL", "MS3DEquipmentUI missing; open SampleScene with installed Equipment 3D.1."); lastReport = s.ToString(); Repaint(); return; }
        var inv = ui.inventoryUI;
        var eq = ui.equipment;
        var vfx = ui.GetComponent<MS3D5EquipmentVFX>();
        Mark(s, inv ? "PASS" : "FAIL", "3A Inventory UI reference");
        Mark(s, eq ? "PASS" : "FAIL", "3D.1 PlayerEquipment reference");
        int uiCells = inv != null && inv.cells != null ? inv.cells.Length : 0;
        int clickable = 0;
        if (inv && inv.cells != null)
            foreach (var cell in inv.cells)
                if (cell && cell.button && cell.frame && cell.frame.raycastTarget && cell.button.targetGraphic) clickable++;
        Mark(s, uiCells == 20 && clickable == 20 ? "PASS" : "WARN", "Inventory cells=" + uiCells + ", pointer-ready=" + clickable + "/20");
        Mark(s, inv && inv.useButton && inv.closeButton ? "PASS" : "FAIL", "USE and Close button references");
        int unButtons = 0;
        if (ui.unequipButtons != null) foreach (var b in ui.unequipButtons) if (b) unButtons++;
        Mark(s, ui.equipButton && unButtons == 3 ? "PASS" : "FAIL", "EQUIP and 3 UNEQUIP buttons");
        Mark(s, eq && eq.catalog ? "PASS" : "FAIL", "Equipment Catalog configured");
        if (eq && eq.catalog) Mark(s, "INFO", "Equipment Catalog definitions: " + (eq.catalog.entries == null ? 0 : eq.catalog.entries.Count));
        var world = eq ? eq.GetComponent<MS3D5WorldEquipBurst>() : null;
        Mark(s, world && world.ringSprite ? "PASS" : "FAIL", "World equip pulse and ring sprite");
        Mark(s, vfx && vfx.equipment == eq && vfx.playerWorldFX == world ? "PASS" : "FAIL", "3D.5 VFX event references");
        int glows = 0, rings = 0;
        if (vfx)
        {
            for (int i = 0; i < 3; i++)
            {
                if (vfx.rarityGlows != null && i < vfx.rarityGlows.Length && vfx.rarityGlows[i] &&
                    !vfx.rarityGlows[i].raycastTarget) glows++;
                if (vfx.pulseRings != null && i < vfx.pulseRings.Length && vfx.pulseRings[i] &&
                    !vfx.pulseRings[i].raycastTarget) rings++;
            }
        }
        Mark(s, glows == 3 && rings == 3 ? "PASS" : "FAIL", "Non-blocking VFX: glow=" + glows + "/3, pulse=" + rings + "/3");
        var stats = eq ? eq.GetComponent<MS3D2EquipmentStats>() : null;
        var advanced = eq ? eq.GetComponent<MS3D3AdvancedStats>() : null;
        Mark(s, stats && stats.bonusCatalog && stats.playerStats && stats.playerHealth ? "PASS" : "WARN", "3D.2 Stats with catalog/PlayerStats/Health");
        Mark(s, advanced && advanced.advancedCatalog && advanced.shooter ? "PASS" : "WARN", "3D.3 Advanced bonuses with shooter");
        if (Application.isPlaying && eq)
        {
            int occupied = 0;
            MS3D2Bonus expected = default(MS3D2Bonus);
            for (int i = 0; i < 3; i++)
            {
                var item = eq.GetEquipped((MS3DEquipmentType)i);
                if (!item) continue;
                occupied++;
                MS3DEquipmentDefinition def = null;
                bool valid = eq.catalog && eq.catalog.TryGet(item, out def) && def != null;
                Mark(s, valid ? "PASS" : "WARN", "Equipped " + (MS3DEquipmentType)i + ": " + item.displayName + (valid ? " / " + def.quality : " (missing catalog entry)"));
                if (valid && stats && stats.bonusCatalog) expected += stats.bonusCatalog.GetBonus(item);
            }
            Mark(s, "INFO", "Occupied equipment slots: " + occupied + "/3");
            if (stats && stats.bonusCatalog)
            {
                MS3D2Bonus actual = stats.AppliedBonuses;
                bool matches = Same(expected.damagePercent, actual.damagePercent) &&
                               Same(expected.attackSpeedPercent, actual.attackSpeedPercent) &&
                               Same(expected.moveSpeedPercent, actual.moveSpeedPercent) &&
                               Same(expected.critChancePoints, actual.critChancePoints) &&
                               Same(expected.maxHealthFlat, actual.maxHealthFlat);
                Mark(s, stats.isActiveAndEnabled && matches ? "PASS" : "WARN", "3D.2 expected VS applied gear bonuses: " + (matches ? "MATCH" : "DIFFER"));
                Mark(s, "INFO", "Expected: " + expected.CompactText() + " | Applied: " + actual.CompactText());
            }
            Mark(s, "INFO", "Time.timeScale=" + Time.timeScale.ToString("0.00") + " (UI tests must be confirmed manually)");
        }
        else Mark(s, "INFO", "Edit Mode cannot validate Equip events, HP or actual stats at runtime.");
        string[] bossTables = AssetDatabase.FindAssets("t:MS3D4BossEquipmentTable");
        Mark(s, bossTables.Length >= 2 ? "PASS" : "WARN", "3D.4 boss equipment tables found: " + bossTables.Length + " (asset scan only)");
        s.AppendLine("----------------------------------------------------------");
        s.AppendLine("Automated audit does not modify Scene, Inventory, equipment or stats.");
        lastReport = s.ToString();
        lastScan = EditorApplication.timeSinceStartup;
        Repaint();
    }

    private static bool Same(float a, float b) { return Mathf.Abs(a - b) < .001f; }
    private static void Mark(StringBuilder s, string status, string message)
    {
        s.AppendLine("[" + status + "] " + message);
    }
    private static T FindScene<T>(Scene scene) where T : Component
    {
        if (!scene.IsValid()) return null;
        foreach (var root in scene.GetRootGameObjects())
        {
            T c = root.GetComponentInChildren<T>(true);
            if (c) return c;
        }
        return null;
    }
    private void Export()
    {
        if (string.IsNullOrEmpty(lastReport)) Scan();
        string path = EditorUtility.SaveFilePanel("Export 3D.5 QA report", Path.GetDirectoryName(Application.dataPath),
            "MonsterSurvival_3D5_QA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"), "txt");
        if (string.IsNullOrEmpty(path)) return;
        StringBuilder text = new StringBuilder(lastReport);
        text.AppendLine("-------------- MANUAL VERIFICATION --------------");
        for (int i = 0; i < ManualTests.Length; i++)
            text.AppendLine((manual[i] ? "[CONFIRMED] " : "[NOT CONFIRMED] ") + ManualTests[i]);
        text.AppendLine("Manually confirmed checks are user's observations, not auto-verified.");
        File.WriteAllText(path, text.ToString(), Encoding.UTF8);
        Debug.Log("[3D.5 QA] Exported " + path);
    }
}
#endif
