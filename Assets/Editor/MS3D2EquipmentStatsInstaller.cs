#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MS3D2EquipmentStatsInstaller
{
    private const string Base = "Assets/MonsterSurvival/Equipment3D2";
    private const string CatalogAsset = Base + "/EquipmentBonuses.asset";
    private const string BackupFolder = Base + "/Backups";

    [MenuItem("Tools/MonsterSurvival/3D Equipment/4 - INSTALL 3D.2 Equipment Stats (OPEN Scene)")]
    public static void Install()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Equipment 3D.2", "Stop Play Mode before installing.", "OK");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        var ui = FindUI(scene);
        var eqUI = ui ? ui.GetComponent<MS3DEquipmentUI>() : null;
        var equipment = eqUI ? eqUI.equipment : null;
        if (!eqUI || !equipment || !equipment.catalog ||
            !equipment.GetComponent<PlayerStats>() || !equipment.GetComponent<Health>())
        {
            EditorUtility.DisplayDialog("Equipment 3D.2",
                "Requires working 3D.1 Equipment and PlayerStats + Health on the Player. Run 3D.1 AUDIT first.", "OK");
            return;
        }
        if (equipment.GetComponent<MS3D2EquipmentStats>())
        {
            EditorUtility.DisplayDialog("Equipment 3D.2", "Already installed. Use menu 5 to audit. No changes made.", "OK");
            return;
        }
        if (string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog("Equipment 3D.2", "Save SampleScene before installing.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Equipment 3D.2 — Stat Bonuses",
            "Add real gear stats to 3D.1 and compact bonus labels to its three slots?\n\n" +
            "Preserves existing PlayerStats, Health, SkillManager, 3D.1 Item Slots, loot tables, Boss and HUD.\n\n" +
            "The scene will be backed up before modifications.", "INSTALL", "Cancel")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Backup(scene);
        var bonuses = GetOrCreateCatalog();
        AddDefaultBonuses(equipment.catalog, bonuses);

        var stats = Undo.AddComponent<MS3D2EquipmentStats>(equipment.gameObject);
        Undo.RecordObject(stats, "Configure 3D.2 stat modifiers");
        stats.equipment = equipment;
        stats.playerStats = equipment.GetComponent<PlayerStats>();
        stats.playerHealth = equipment.GetComponent<Health>();
        stats.bonusCatalog = bonuses;
        EditorUtility.SetDirty(stats);

        if (eqUI.equipmentWindow && eqUI.slotNames != null && eqUI.slotNames.Length >= 3)
        {
            var labels = Undo.AddComponent<MS3D2EquipmentStatLabels>(eqUI.gameObject);
            labels.equipmentUI = eqUI;
            labels.bonusCatalog = bonuses;
            labels.statTexts = new TMP_Text[3];
            labels.existingNameRects = new RectTransform[3];
            labels.savedNameMin = new Vector2[3];
            labels.savedNameMax = new Vector2[3];
            for (int i = 0; i < 3; i++)
            {
                var nameLabel = eqUI.slotNames[i];
                if (!nameLabel) continue;
                var nameRect = nameLabel.rectTransform;
                labels.existingNameRects[i] = nameRect;
                labels.savedNameMin[i] = nameRect.anchorMin;
                labels.savedNameMax[i] = nameRect.anchorMax;
                Undo.RecordObject(nameRect, "Make room for 3D.2 stat line");
                nameRect.anchorMin = new Vector2(.36f, .44f);
                nameRect.anchorMax = new Vector2(.97f, .73f);
                nameRect.offsetMin = nameRect.offsetMax = Vector2.zero;
                var go = new GameObject("MS3D2_BonusText_" + i, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Create Equipment Bonus Text");
                go.layer = nameRect.gameObject.layer;
                go.transform.SetParent(nameRect.parent, false);
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(.35f, .29f);
                rect.anchorMax = new Vector2(.98f, .47f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var text = go.AddComponent<TextMeshProUGUI>();
                text.text = "NO BONUS";
                text.fontSize = 10.5f;
                text.enableAutoSizing = true;
                text.fontSizeMin = 8f;
                text.fontSizeMax = 11f;
                text.alignment = TextAlignmentOptions.Left;
                text.textWrappingMode = TextWrappingModes.Normal;
                text.raycastTarget = false;
                text.color = new Color(.7f, .68f, .77f);
                if (TMP_Settings.defaultFontAsset) text.font = TMP_Settings.defaultFontAsset;
                labels.statTexts[i] = text;
                // Keep 3D.1's existing Unequip buttons and all listeners unchanged.
            }
            EditorUtility.SetDirty(labels);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[3D.2] INSTALL complete: additive equipment bonuses + 3 stat labels. Skill and HP scripts unchanged.");
        EditorUtility.DisplayDialog("3D.2 installed", "Save complete. Play -> pickup demo gear -> I -> EQUIP; watch PlayerStats + Health, then UNEQUIP. Use menu 5 - AUDIT.", "OK");
    }

    [MenuItem("Tools/MonsterSurvival/3D Equipment/5 - AUDIT 3D.2 Stats (READ ONLY)")]
    public static void Audit()
    {
        var scene = SceneManager.GetActiveScene();
        var ui = FindUI(scene);
        var eqUI = ui ? ui.GetComponent<MS3DEquipmentUI>() : null;
        var eq = eqUI ? eqUI.equipment : null;
        var stats = eq ? eq.GetComponent<MS3D2EquipmentStats>() : null;
        var labels = eqUI ? eqUI.GetComponent<MS3D2EquipmentStatLabels>() : null;
        var catalog = AssetDatabase.LoadAssetAtPath<MS3D2BonusCatalog>(CatalogAsset);
        int count = 0;
        if (labels && labels.statTexts != null)
            foreach (var t in labels.statTexts) if (t) count++;
        Debug.Log("[3D.2 QA] Scene=" + scene.name +
            " | Equipment3D1=" + (eq ? "PASS" : "MISSING") +
            " | PlayerStats=" + (eq && eq.GetComponent<PlayerStats>() ? "PASS" : "MISSING") +
            " | Health=" + (eq && eq.GetComponent<Health>() ? "PASS" : "MISSING") +
            " | 3D2 Runtime=" + (stats ? "PASS" : "MISSING") +
            " | Catalog=" + (catalog ? "PASS" : "MISSING") +
            " | Entries=" + (catalog && catalog.entries != null ? catalog.entries.Count : 0) +
            " | Stat Labels=" + count + "/3" +
            " | Same Catalog=" + (stats && stats.bonusCatalog == catalog ? "YES" : "NO"));
    }

    [MenuItem("Tools/MonsterSurvival/3D Equipment/6 - REMOVE 3D.2 Stats (OPEN Scene)")]
    public static void Remove()
    {
        if (Application.isPlaying) { Debug.LogWarning("[3D.2] Stop Play Mode first."); return; }
        Scene scene = SceneManager.GetActiveScene();
        var ui = FindUI(scene);
        var eqUI = ui ? ui.GetComponent<MS3DEquipmentUI>() : null;
        var eq = eqUI ? eqUI.equipment : null;
        var labels = eqUI ? eqUI.GetComponent<MS3D2EquipmentStatLabels>() : null;
        var stats = eq ? eq.GetComponent<MS3D2EquipmentStats>() : null;
        if (!stats && !labels) { Debug.LogWarning("[3D.2] No 3D.2 components in open Scene."); return; }
        if (!EditorUtility.DisplayDialog("REMOVE 3D.2", "Remove only the 3D.2 stat component and text labels? 3D.1 equipment and Catalog assets remain intact.", "REMOVE", "Cancel")) return;
        if (labels)
        {
            if (labels.existingNameRects != null)
                for (int i=0; i<labels.existingNameRects.Length; i++)
                {
                    var rect=labels.existingNameRects[i];
                    if (!rect) continue;
                    Undo.RecordObject(rect, "Restore 3D.1 equipment item label");
                    if (labels.savedNameMin != null && i < labels.savedNameMin.Length) rect.anchorMin = labels.savedNameMin[i];
                    if (labels.savedNameMax != null && i < labels.savedNameMax.Length) rect.anchorMax = labels.savedNameMax[i];
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                }
            if (labels.statTexts != null)
                foreach (var label in labels.statTexts)
                    if (label) Undo.DestroyObjectImmediate(label.gameObject);
            Undo.DestroyObjectImmediate(labels);
        }
        if (stats) Undo.DestroyObjectImmediate(stats);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[3D.2] REMOVED runtime stats and extra texts. 3D.1 preserved; EquipmentBonuses.asset retained.");
    }

    private static MS3AInventoryUI FindUI(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == "MS3A_InventoryCanvas") return root.GetComponent<MS3AInventoryUI>();
        return null;
    }

    private static MS3D2BonusCatalog GetOrCreateCatalog()
    {
        EnsureFolder(Base);
        var item = AssetDatabase.LoadAssetAtPath<MS3D2BonusCatalog>(CatalogAsset);
        if (item) return item;
        item = ScriptableObject.CreateInstance<MS3D2BonusCatalog>();
        AssetDatabase.CreateAsset(item, CatalogAsset);
        return item;
    }

    private static void AddDefaultBonuses(MS3DEquipmentCatalog equipmentCatalog, MS3D2BonusCatalog bonuses)
    {
        if (!equipmentCatalog || equipmentCatalog.entries == null) return;
        if (bonuses.entries == null) bonuses.entries = new List<MS3D2BonusEntry>();
        foreach (var def in equipmentCatalog.entries)
        {
            if (def == null || !def.item || bonuses.HasEntry(def.item)) continue;
            var stat = new MS3D2Bonus();
            switch (def.item.itemId)
            {
                case "dusk_blade_3d1": stat.damagePercent=20f; stat.attackSpeedPercent=10f; break;
                case "violet_armor_3d1": stat.maxHealthFlat=25f; break;
                case "warden_ring_3d1": stat.moveSpeedPercent=12f; stat.critChancePoints=5f; break;
            }
            bonuses.entries.Add(new MS3D2BonusEntry { item = def.item, bonuses = stat });
        }
        EditorUtility.SetDirty(bonuses);
    }

    private static void Backup(Scene scene)
    {
        EnsureFolder(BackupFolder);
        string path = BackupFolder + "/" + Path.GetFileNameWithoutExtension(scene.path) + "_BEFORE_3D2.unity";
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(path))
        {
            if (!AssetDatabase.CopyAsset(scene.path, path))
                throw new IOException("Could not backup scene: " + scene.path);
        }
    }

    private static void EnsureFolder(string path)
    {
        var segments = path.Split('/');
        string current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string next=current+"/"+segments[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[i]);
            current=next;
        }
    }
}
#endif
