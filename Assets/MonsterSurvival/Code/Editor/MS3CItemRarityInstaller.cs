#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 3C attaches independent visual components; all 3A/3B scripts remain untouched.
public static class MS3CItemRarityInstaller
{
    private const string Root = "Assets/MonsterSurvival/Features/Inventory/Inventory3C";
    private const string Demo = "Assets/MonsterSurvival/Features/Inventory/Inventory3A/Content";
    private const string Tables = "Assets/MonsterSurvival/Features/Inventory/Inventory3B/LootTables";
    private const string CatalogPath = Root + "/RarityCatalog.asset";
    private const string Backup = Root + "/Backups";

    private static readonly string[] PickupPaths =
    {
        Demo + "/Pickup_Potion.prefab", Demo + "/Pickup_Crystal.prefab",
        Root + "/Content/Pickup_ArcaneRune.prefab", Root + "/Content/Pickup_WardenSigil.prefab"
    };

    [MenuItem("Tools/MonsterSurvival/3C Item Rarity/1 - INSTALL Rarity and Loot FX (Scene + Prefabs)")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("3C", "Stop Play Mode first.", "OK"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var potion = AssetDatabase.LoadAssetAtPath<MS3AItemData>(Demo + "/Potion.asset");
        var crystal = AssetDatabase.LoadAssetAtPath<MS3AItemData>(Demo + "/Crystal.asset");
        if (!potion || !crystal || !AssetDatabase.LoadAssetAtPath<GameObject>(PickupPaths[0]) ||
            !AssetDatabase.LoadAssetAtPath<GameObject>(PickupPaths[1]))
        {
            EditorUtility.DisplayDialog("3C missing dependency", "3A Potion, Crystal and Pickup prefabs missing. Make sure Inventory 3A is installed first.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Install Inventory 3C", "Add rarity catalog, ground pickup glow/bob/sparkles, collection burst and colored Inventory slot borders?\n\nExisting loot chances, pickup sizes, collider, UI buttons and gameplay scripts will NOT change. Extra Boss rewards are optional and separate.", "INSTALL", "Cancel")) return;

        EnsureFolder(Root + "/Content");
        EnsureFolder(Backup);
        var catalog = AssetDatabase.LoadAssetAtPath<MS3CRarityCatalog>(CatalogPath);
        if (!catalog)
        {
            catalog = ScriptableObject.CreateInstance<MS3CRarityCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        AddIfAbsent(catalog, potion, MS3CRarity.Common);
        AddIfAbsent(catalog, crystal, MS3CRarity.Rare);
        var epic = EnsureDemo("ArcaneRune", "arcane_rune_epic", "ARCANE RUNE",
            "Epic material. Future enhancement resource.", "MS3C_ArcaneRune.png", MS3CRarity.Epic, catalog);
        var legendary = EnsureDemo("WardenSigil", "warden_sigil_legendary", "WARDEN'S SIGIL",
            "Legendary boss trophy. Future crafting resource.", "MS3C_WardenSigil.png", MS3CRarity.Legendary, catalog);
        CreateDemoPickup(PickupPaths[2], PickupPaths[1], epic, MS3CRarity.Epic);
        CreateDemoPickup(PickupPaths[3], PickupPaths[1], legendary, MS3CRarity.Legendary);

        int changed = 0;
        foreach (string path in PickupPaths)
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(path)) continue;
            if (AddFxToPrefab(path, catalog)) changed++;
        }
        int sceneChanges = AddUIToOpenScene(catalog);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("[Inventory 3C] FX prefabs configured: " + changed + "; Inventory UI configured: " + sceneChanges + ". Boss drop tables unchanged.");
        EditorUtility.DisplayDialog("Inventory 3C", "Ready. Updated " + changed + " pickup prefab(s). UI scene update=" + sceneChanges + ".\n\nPress Play and inspect loot drops. Optional boss table rules are a SEPARATE command.", "OK");
    }

    [MenuItem("Tools/MonsterSurvival/3C Item Rarity/2 - AUDIT Rarity Setup (READ ONLY)")]
    public static void Audit()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<MS3CRarityCatalog>(CatalogPath);
        Debug.Log("[3C QA] Catalog=" + (catalog ? "PRESENT" : "MISSING") + " mappings=" + (catalog && catalog.items != null ? catalog.items.Count : 0));
        foreach (string p in PickupPaths)
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            MS3CPickupFX fx = go ? go.GetComponent<MS3CPickupFX>() : null;
            Debug.Log("[3C QA] " + p + " asset=" + (go ? "YES" : "NO") + " FX=" + (fx ? "YES" : "NO") + " catalog=" + (fx && fx.rarityCatalog ? "YES" : "NO") +
                " resized=" + (go && go.transform.Find("MS3B1_SmallerWorldVisual") ? "YES" : "NO (uses original scale)"));
        }
        var ui = UnityEngine.Object.FindFirstObjectByType<MS3AInventoryUI>(FindObjectsInactive.Include);
        var style = ui ? ui.GetComponent<MS3CInventoryRarityUI>() : null;
        Debug.Log("[3C QA] Scene=" + SceneManager.GetActiveScene().name + " 3A UI=" + (ui ? "YES" : "NO") + " rarity UI=" + (style ? "YES" : "NO"));
    }

    [MenuItem("Tools/MonsterSurvival/3C Item Rarity/3 - REMOVE Rarity FX (Scene + Prefabs)")]
    public static void Remove()
    {
        if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!EditorUtility.DisplayDialog("Remove Inventory 3C visuals?", "Remove ONLY 3C visual components and 3C rarity text from the open Scene and four known demo pickup prefabs? Item data, Loot Tables, pickup sizes and all 3A / 3B logic remain untouched.", "REMOVE", "Cancel")) return;
        foreach (string path in PickupPaths)
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(path)) continue;
            GameObject prefab = null;
            try
            {
                prefab = PrefabUtility.LoadPrefabContents(path);
                var fx = prefab.GetComponent<MS3CPickupFX>();
                if (fx) UnityEngine.Object.DestroyImmediate(fx);
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            catch (Exception ex) { Debug.LogError("[3C] Remove FX failed: " + path + " / " + ex.Message); }
            finally { if (prefab) PrefabUtility.UnloadPrefabContents(prefab); }
        }
        var ui = UnityEngine.Object.FindFirstObjectByType<MS3AInventoryUI>(FindObjectsInactive.Include);
        if (ui != null)
        {
            var style = ui.GetComponent<MS3CInventoryRarityUI>();
            if (style) Undo.DestroyObjectImmediate(style);
            Transform label = ui.transform.Find("InventoryPanel/InventoryWindow/MS3C_RarityLabel");
            if (label) Undo.DestroyObjectImmediate(label.gameObject);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("[3C] Visual FX removed. Assets and original loot rules were not deleted.");
    }

    [MenuItem("Tools/MonsterSurvival/3C Item Rarity/4 - OPTIONAL Add Epic and Legendary Boss Drops")]
    public static void OptionalBossRewards()
    {
        if (EditorApplication.isPlaying) return;
        var epic = AssetDatabase.LoadAssetAtPath<MS3AItemData>(Root + "/Content/ArcaneRune.asset");
        var legend = AssetDatabase.LoadAssetAtPath<MS3AItemData>(Root + "/Content/WardenSigil.asset");
        var epicPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPaths[2]);
        var legendPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPaths[3]);
        var boss1 = AssetDatabase.LoadAssetAtPath<MS3BLootTable>(Tables + "/Loot_Boss1_ShadowTitan.asset");
        var boss2 = AssetDatabase.LoadAssetAtPath<MS3BLootTable>(Tables + "/Loot_Boss2_GraveWarden.asset");
        if (!epic || !legend || !epicPrefab || !legendPrefab || !boss1 || !boss2)
        { EditorUtility.DisplayDialog("3C", "Run 3C INSTALL first; all 3B Boss tables must exist.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("Optional Boss rewards", "ADD new Epic / Legendary rules to TWO existing Boss Loot Tables?\n\nShadow Titan: Epic 32%. Grave Warden: Epic 55%, Legendary 12%.\nExisting Potion/Crystal percentages will NOT be changed. Boss maxDropObjects will increase to 5 so new rewards can actually drop.\n\nThis is OPTIONAL; you can undo table changes via Ctrl+Z before closing Unity or manual Inspector edits.", "ADD REWARDS", "Cancel")) return;
        Undo.RecordObject(boss1, "3C Optional ShadowTitan Rewards");
        Undo.RecordObject(boss2, "3C Optional GraveWarden Rewards");
        AddLootEntryIfMissing(boss1, epic, epicPrefab, 32f);
        AddLootEntryIfMissing(boss2, epic, epicPrefab, 55f);
        AddLootEntryIfMissing(boss2, legend, legendPrefab, 12f);
        boss1.maxDropObjects = Mathf.Max(5, boss1.maxDropObjects);
        boss2.maxDropObjects = Mathf.Max(5, boss2.maxDropObjects);
        EditorUtility.SetDirty(boss1); EditorUtility.SetDirty(boss2);
        AssetDatabase.SaveAssets();
        Debug.Log("[3C] Optional Boss Epic/Legendary rules added (duplicates avoided).");
    }

    [MenuItem("Tools/MonsterSurvival/3C Item Rarity/5 - REMOVE OPTIONAL Boss Epic Legendary Rules")]
    public static void RemoveOptionalBossRewards()
    {
        if (EditorApplication.isPlaying) return;
        var epic = AssetDatabase.LoadAssetAtPath<MS3AItemData>(Root + "/Content/ArcaneRune.asset");
        var legend = AssetDatabase.LoadAssetAtPath<MS3AItemData>(Root + "/Content/WardenSigil.asset");
        var boss1 = AssetDatabase.LoadAssetAtPath<MS3BLootTable>(Tables + "/Loot_Boss1_ShadowTitan.asset");
        var boss2 = AssetDatabase.LoadAssetAtPath<MS3BLootTable>(Tables + "/Loot_Boss2_GraveWarden.asset");
        if (!epic || !legend || !boss1 || !boss2) { Debug.LogWarning("[3C] Optional rewards absent or missing Boss tables."); return; }
        if (!EditorUtility.DisplayDialog("Remove optional Boss drops", "Remove only the Arcane Rune / Warden's Sigil entries from the two Boss tables? Potion/Crystal remain unchanged. The maxDropObjects cap is intentionally not reduced, since you might have customized it.", "REMOVE OPTIONAL RULES", "Cancel")) return;
        Undo.RecordObject(boss1, "3C Remove Optional Boss1 Rewards");
        Undo.RecordObject(boss2, "3C Remove Optional Boss2 Rewards");
        int removed = boss1.entries.RemoveAll(e => e != null && (e.item == epic || e.item == legend));
        removed += boss2.entries.RemoveAll(e => e != null && (e.item == epic || e.item == legend));
        EditorUtility.SetDirty(boss1); EditorUtility.SetDirty(boss2);
        AssetDatabase.SaveAssets();
        Debug.Log("[3C] Removed " + removed + " optional Boss loot rules; original rules kept.");
    }

    private static void AddLootEntryIfMissing(MS3BLootTable table, MS3AItemData item, GameObject prefab, float chance)
    {
        foreach (MS3BLootEntry entry in table.entries) if (entry != null && entry.item == item) return;
        table.entries.Add(new MS3BLootEntry { item = item, pickupPrefab = prefab, chancePercent = chance, minAmount = 1, maxAmount = 1 });
    }

    private static void AddIfAbsent(MS3CRarityCatalog cat, MS3AItemData item, MS3CRarity rarity)
    {
        if (!item) return;
        foreach (MS3CRarityEntry row in cat.items) if (row != null && row.item == item) return;
        cat.items.Add(new MS3CRarityEntry { item = item, rarity = rarity });
        EditorUtility.SetDirty(cat);
    }

    private static MS3AItemData EnsureDemo(string assetName, string itemId, string displayName,
        string description, string artFile, MS3CRarity rarity, MS3CRarityCatalog catalog)
    {
        string path = Root + "/Content/" + assetName + ".asset";
        MS3AItemData item = AssetDatabase.LoadAssetAtPath<MS3AItemData>(path);
        if (!item)
        {
            item = ScriptableObject.CreateInstance<MS3AItemData>();
            item.itemId = itemId;
            item.displayName = displayName;
            item.description = description;
            item.itemKind = MS3AItemKind.Material;
            item.maxStack = rarity == MS3CRarity.Legendary ? 5 : 10;
            item.icon = ImportSprite(Root + "/Art/" + artFile);
            AssetDatabase.CreateAsset(item, path);
        }
        AddIfAbsent(catalog, item, rarity);
        return item;
    }

    private static Sprite ImportSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) throw new Exception("Missing 3C art: " + path);
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void CreateDemoPickup(string dest, string template, MS3AItemData item, MS3CRarity rarity)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(dest)) return; // NEVER overwrite customized prefab
        if (!AssetDatabase.CopyAsset(template, dest)) { Debug.LogError("[3C] Could not create " + dest); return; }
        GameObject root = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents(dest);
            var pickup = root.GetComponent<MS3AItemPickup>();
            if (pickup) { pickup.item = item; pickup.amount = 1; }
            SpriteRenderer sr = root.GetComponent<SpriteRenderer>();
            Transform small = root.transform.Find("MS3B1_SmallerWorldVisual");
            SpriteRenderer display = small ? small.GetComponent<SpriteRenderer>() : sr;
            if (display && item.icon) display.sprite = item.icon;
            if (sr && item.icon) sr.sprite = item.icon; // root remains disabled if 3B.1 already applied
            PrefabUtility.SaveAsPrefabAsset(root, dest);
        }
        catch (Exception ex) { Debug.LogError("[3C] Creating demo pickup failed: " + ex.Message); }
        finally { if (root) PrefabUtility.UnloadPrefabContents(root); }
    }

    private static bool AddFxToPrefab(string path, MS3CRarityCatalog catalog)
    {
        GameObject root = null;
        try
        {
            BackupPrefab(path);
            root = PrefabUtility.LoadPrefabContents(path);
            MS3AItemPickup pickup = root.GetComponent<MS3AItemPickup>();
            if (!pickup) return false;
            MS3CPickupFX fx = root.GetComponent<MS3CPickupFX>();
            if (!fx) fx = root.AddComponent<MS3CPickupFX>();
            fx.rarityCatalog = catalog;
            return PrefabUtility.SaveAsPrefabAsset(root, path) != null;
        }
        catch (Exception ex) { Debug.LogError("[3C] Failed to configure: " + path + " / " + ex.Message); return false; }
        finally { if (root) PrefabUtility.UnloadPrefabContents(root); }
    }

    private static int AddUIToOpenScene(MS3CRarityCatalog catalog)
    {
        var ui = UnityEngine.Object.FindFirstObjectByType<MS3AInventoryUI>(FindObjectsInactive.Include);
        if (ui == null) { Debug.LogWarning("[3C] Inventory UI not found in open Scene. Open SampleScene and rerun Install to add rarity borders."); return 0; }
        var style = ui.GetComponent<MS3CInventoryRarityUI>();
        if (!style) style = Undo.AddComponent<MS3CInventoryRarityUI>(ui.gameObject);
        Undo.RecordObject(style, "3C Configure Rarity UI");
        style.inventoryUI = ui;
        style.rarityCatalog = catalog;
        Transform window = ui.transform.Find("InventoryPanel/InventoryWindow");
        if (window)
        {
            Transform existing = window.Find("MS3C_RarityLabel");
            if (!existing)
            {
                GameObject label = new GameObject("MS3C_RarityLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
                label.transform.SetParent(window, false);
                label.layer = 5;
                Undo.RegisterCreatedObjectUndo(label, "3C Rarity Label");
                RectTransform rect = label.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(.735f, .177f);
                rect.anchorMax = new Vector2(.937f, .236f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var tmp = label.GetComponent<TextMeshProUGUI>();
                tmp.alignment = TextAlignmentOptions.Right;
                tmp.fontSize = 17;
                tmp.text = "";
                tmp.raycastTarget = false;
                style.rarityLabel = tmp;
            }
            else style.rarityLabel = existing.GetComponent<TMP_Text>();
        }
        EditorUtility.SetDirty(style);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        return 1;
    }

    private static void BackupPrefab(string path)
    {
        string guid = AssetDatabase.AssetPathToGUID(path);
        if (string.IsNullOrEmpty(guid)) return;
        string root = Directory.GetParent(Application.dataPath).FullName;
        string folder = Path.Combine(root, "MonsterSurvival_3C_PrefabBackups");
        Directory.CreateDirectory(folder);
        string backup = Path.Combine(folder, guid + ".prefab");
        if (!File.Exists(backup))
        {
            string source = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(source)) File.Copy(source, backup);
        }
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
