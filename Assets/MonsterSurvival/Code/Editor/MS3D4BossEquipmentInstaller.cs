#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Explicit prefab-only installer: intentionally does not touch SpawnManager, WaveManager,
// Health, any Boss AI script, any 3B table or an open Scene.
public sealed class MS3D4BossEquipmentInstaller : EditorWindow
{
    private const string Root = "Assets/MonsterSurvival/Features/Equipment/Equipment3D4";
    private const string Demo = Root + "/Content";
    private const string Art = Root + "/Art";
    private const string Tables = Root + "/BossTables";
    private const string EquipmentPath = "Assets/MonsterSurvival/Features/Equipment/Equipment3D1/EquipmentCatalog.asset";
    private const string BasePath = "Assets/MonsterSurvival/Features/Equipment/Equipment3D2/EquipmentBonuses.asset";
    private const string AdvancedPath = "Assets/MonsterSurvival/Features/Equipment/Equipment3D3/AdvancedAffixes.asset";
    private const string RarityPath = "Assets/MonsterSurvival/Features/Inventory/Inventory3C/RarityCatalog.asset";
    private const string SourcePickup = "Assets/MonsterSurvival/Features/Equipment/Equipment3D1/Content/Pickup_DuskBlade.prefab";

    private struct ExclusiveSpec
    {
        public string slug, id, display, flavor, icon;
        public MS3DEquipmentType slot;
        public MS3DQuality quality;
        public MS3D2Bonus baseBonus;
        public MS3D3AdvancedBonus advanced;
        public ExclusiveSpec(string slug, string id, string display, string flavor, string icon,
            MS3DEquipmentType slot, MS3DQuality quality, MS3D2Bonus basic, MS3D3AdvancedBonus advanced)
        {
            this.slug = slug; this.id = id; this.display = display;
            this.flavor = flavor; this.icon = icon; this.slot = slot;
            this.quality = quality; baseBonus = basic; this.advanced = advanced;
        }
    }

    private static readonly ExclusiveSpec[] Exclusives = {
        new ExclusiveSpec("TitanheartAegis", "titanheart_aegis_3d4", "TITANHEART AEGIS",
            "Shadow Titan exclusive. Epic armor that shelters its wielder.", "MS3D4_TitanheartAegis.png",
            MS3DEquipmentType.Armor, MS3DQuality.Epic,
            new MS3D2Bonus{maxHealthFlat=34f,damagePercent=5f},
            new MS3D3AdvancedBonus{}),
        new ExclusiveSpec("WardenOath", "warden_oath_3d4", "WARDEN'S OATH",
            "Grave Warden exclusive. Legendary relic bound to the last oath.", "MS3D4_WardenOath.png",
            MS3DEquipmentType.Accessory, MS3DQuality.Legendary,
            new MS3D2Bonus{moveSpeedPercent=12f,critChancePoints=8f},
            new MS3D3AdvancedBonus{bulletSpeedPercent=16f})
    };

    private GameObject bossTitan;
    private GameObject bossWarden;
    private Vector2 scroll;

    [MenuItem("Tools/MonsterSurvival/3D Equipment/10 - SETUP 3D.4 Boss Equipment Drops")]
    public static void Open() { GetWindow<MS3D4BossEquipmentInstaller>("3D.4 Boss Loot").Show(); }

    private void OnEnable() { AutoFindBossPrefabs(); }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        GUILayout.Label("MONSTERSURVIVAL 3D.4  /  BOSS EQUIPMENT DROPS", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Attach a SEPARATE equipment drop component to the actual SPAWNED Boss Prefabs. Existing 3B loot tables, Health, Boss AI, WaveManager and SampleScene will NOT be edited.", MessageType.Info);
        EditorGUILayout.Space(8);
        bossTitan = (GameObject)EditorGUILayout.ObjectField("Shadow Titan prefab", bossTitan, typeof(GameObject), false);
        bossWarden = (GameObject)EditorGUILayout.ObjectField("Grave Warden prefab", bossWarden, typeof(GameObject), false);
        if (GUILayout.Button("AUTO-FIND BOSS PREFABS (review results)")) AutoFindBossPrefabs();
        EditorGUILayout.HelpBox("Confirm both exact prefab assets. Some projects contain unused test prefabs. Select the Boss prefab assigned to your WaveManager (not just a Scene instance).", MessageType.Warning);
        EditorGUILayout.Space(6);
        GUILayout.Label("DEFAULT REWARD TABLES", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("SHADOW TITAN: Hollow Bow Epic 100%, Soulguard Plate Legendary 22%, Titanheart Aegis Epic exclusive 32%", EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("GRAVE WARDEN: Starlit Charm Epic 100%, Reaper Edge Legendary 24%, Warden's Oath Legendary exclusive 28%", EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space(8);
        if (GUILayout.Button("INSTALL 3D.4 + PATCH TWO CONFIRMED PREFABS", GUILayout.Height(38))) Install();
        if (GUILayout.Button("AUDIT CONFIGURATION (READ ONLY)", GUILayout.Height(29))) AuditSelection();
        if (GUILayout.Button("REMOVE 3D.4 COMPONENT FROM THESE PREFABS", GUILayout.Height(29))) Remove();
        EditorGUILayout.HelpBox("REMOVE only removes the 3D.4 component from the selected prefab(s); it does not delete item assets, other loot or your custom catalog entries. See README for full backups.", MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    private void AutoFindBossPrefabs()
    {
        GameObject a = null, b = null;
        int bestA = 0, bestB = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] {"Assets"}))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("/Backups/") || path.Contains("/Content/") || path.Contains("/Test/")) continue;
            var candidate = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!candidate || !candidate.GetComponentInChildren<Health>(true)) continue;
            string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            int sA = (name.Contains("shadow") ? 4 : 0) + (name.Contains("titan") ? 6 : 0);
            int sB = (name.Contains("grave") ? 5 : 0) + (name.Contains("warden") ? 5 : 0);
            if (sA > bestA && sA >= 6) { a = candidate; bestA = sA; }
            if (sB > bestB && sB >= 6) { b = candidate; bestB = sB; }
        }
        if (a) bossTitan = a;
        if (b) bossWarden = b;
    }

    private static bool CheckPrefab(GameObject prefab, string label, out string path)
    {
        path = prefab ? AssetDatabase.GetAssetPath(prefab) : null;
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("[3D.4] " + label + " must reference a prefab ASSET in Project window.");
            return false;
        }
        if (prefab.GetComponentInChildren<Health>(true) == null)
        {
            Debug.LogError("[3D.4] " + label + " prefab has no Health: " + path);
            return false;
        }
        return true;
    }

    private static bool ValidatePrerequisites()
    {
        string[] required = { EquipmentPath, BasePath, AdvancedPath, RarityPath, SourcePickup,
            "Assets/MonsterSurvival/Features/Equipment/Equipment3D3/Content/HollowBow.asset",
            "Assets/MonsterSurvival/Features/Equipment/Equipment3D3/Content/SoulguardPlate.asset",
            "Assets/MonsterSurvival/Features/Equipment/Equipment3D3/Content/StarlitCharm.asset",
            "Assets/MonsterSurvival/Features/Equipment/Equipment3D3/Content/ReaperEdge.asset",
            "Assets/MonsterSurvival/Features/Equipment/Equipment3D3/Content/Pickup_HollowBow.prefab",
            "Assets/MonsterSurvival/Features/Equipment/Equipment3D3/Content/Pickup_SoulguardPlate.prefab",
            "Assets/MonsterSurvival/Features/Equipment/Equipment3D3/Content/Pickup_StarlitCharm.prefab",
            "Assets/MonsterSurvival/Features/Equipment/Equipment3D3/Content/Pickup_ReaperEdge.prefab" };
        foreach (string path in required)
            if (AssetDatabase.LoadMainAssetAtPath(path) == null)
            {
                Debug.LogError("[3D.4] Missing prerequisite: " + path);
                EditorUtility.DisplayDialog("3D.4", "Missing dependency:\n" + path + "\n\nInstall 3D.1, 3D.2, 3D.3 and 3C first. NO changes were made.", "OK");
                return false;
            }
        foreach (ExclusiveSpec s in Exclusives)
            if (!AssetDatabase.LoadMainAssetAtPath(Art + "/" + s.icon))
            {
                Debug.LogError("[3D.4] Missing icon file " + s.icon);
                return false;
            }
        foreach (string pickupPath in new[] {
            SourcePickup,
            "Assets/MonsterSurvival/Features/Equipment/Equipment3D3/Content/Pickup_HollowBow.prefab",
            "Assets/MonsterSurvival/Features/Equipment/Equipment3D3/Content/Pickup_SoulguardPlate.prefab",
            "Assets/MonsterSurvival/Features/Equipment/Equipment3D3/Content/Pickup_StarlitCharm.prefab",
            "Assets/MonsterSurvival/Features/Equipment/Equipment3D3/Content/Pickup_ReaperEdge.prefab"
        })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(pickupPath);
            if (!prefab || !prefab.GetComponent<MS3AItemPickup>())
            {
                Debug.LogError("[3D.4] Invalid pickup prefab: " + pickupPath);
                EditorUtility.DisplayDialog("3D.4", "Pickup prefab missing MS3AItemPickup:\n" + pickupPath + "\n\nNo changes were made.", "OK");
                return false;
            }
        }
        return true;
    }

    private void Install()
    {
        if (EditorApplication.isPlaying) { Alert("Stop Play Mode before installing."); return; }
        if (!ValidatePrerequisites()) return;
        string titanPath, wardenPath;
        if (!CheckPrefab(bossTitan, "Shadow Titan", out titanPath) ||
            !CheckPrefab(bossWarden, "Grave Warden", out wardenPath)) { Alert("Choose both valid Boss prefab assets. See Console."); return; }
        if (titanPath == wardenPath) { Alert("The two Boss prefabs must be different assets."); return; }
        if (!EditorUtility.DisplayDialog("3D.4 Confirm Installation",
            "Add a separate equipment drop component to:\n" + titanPath + "\n" + wardenPath +
            "\n\nCreates two exclusive items and two new boss-equipment tables.\nExisting 3B loot tables stay untouched. Backups will be saved OUTSIDE Assets.", "INSTALL", "CANCEL")) return;
        if (!EditorSceneManagerSafeSave()) return;
        try
        {
            BackupBeforeInstall(new[] {titanPath, wardenPath, EquipmentPath, BasePath, AdvancedPath, RarityPath});
            EnsureFolder(Demo); EnsureFolder(Tables);
            var catalog = AssetDatabase.LoadAssetAtPath<MS3DEquipmentCatalog>(EquipmentPath);
            var basic = AssetDatabase.LoadAssetAtPath<MS3D2BonusCatalog>(BasePath);
            var advanced = AssetDatabase.LoadAssetAtPath<MS3D3AdvancedCatalog>(AdvancedPath);
            var rarity = AssetDatabase.LoadAssetAtPath<MS3CRarityCatalog>(RarityPath);
            Undo.RecordObject(catalog, "3D.4 equipment catalog");
            Undo.RecordObject(basic, "3D.4 bonuses catalog");
            Undo.RecordObject(advanced, "3D.4 advanced catalog");
            Undo.RecordObject(rarity, "3D.4 rarity catalog");
            foreach (ExclusiveSpec spec in Exclusives)
            {
                MS3AItemData item = EnsureItem(spec);
                MS3DEquipmentDefinition def = null;
                if (!catalog.TryGet(item, out def))
                {
                    if (catalog.entries == null) catalog.entries = new List<MS3DEquipmentDefinition>();
                    catalog.entries.Add(new MS3DEquipmentDefinition{item=item,type=spec.slot,quality=spec.quality,flavorText=spec.flavor});
                }
                if (!basic.HasEntry(item))
                {
                    if (basic.entries == null) basic.entries = new List<MS3D2BonusEntry>();
                    basic.entries.Add(new MS3D2BonusEntry{item=item,bonuses=spec.baseBonus});
                }
                if (!advanced.Contains(item))
                {
                    if (advanced.entries == null) advanced.entries = new List<MS3D3AdvancedEntry>();
                    advanced.entries.Add(new MS3D3AdvancedEntry{item=item,bonus=spec.advanced});
                }
                bool registered = false;
                if (rarity.items == null) rarity.items = new List<MS3CRarityEntry>();
                foreach (MS3CRarityEntry r in rarity.items) if (r != null && r.item == item) { registered = true; break; }
                if (!registered) rarity.items.Add(new MS3CRarityEntry{item=item,rarity=(MS3CRarity)spec.quality});
                EnsureExclusivePickup(spec, item, rarity);
            }
            EditorUtility.SetDirty(catalog);EditorUtility.SetDirty(basic);
            EditorUtility.SetDirty(advanced);EditorUtility.SetDirty(rarity);
            MS3D4BossEquipmentTable titan = EnsureTable("BossGear_ShadowTitan", new[] {
                MakeRow("Assets/MonsterSurvival/Features/Equipment/Equipment3D3/Content/HollowBow.asset", "Assets/MonsterSurvival/Features/Equipment/Equipment3D3/Content/Pickup_HollowBow.prefab",100f),
                MakeRow("Assets/MonsterSurvival/Features/Equipment/Equipment3D3/Content/SoulguardPlate.asset", "Assets/MonsterSurvival/Features/Equipment/Equipment3D3/Content/Pickup_SoulguardPlate.prefab",22f),
                MakeRow(Demo+"/TitanheartAegis.asset", Demo+"/Pickup_TitanheartAegis.prefab",32f)
            });
            MS3D4BossEquipmentTable warden = EnsureTable("BossGear_GraveWarden", new[] {
                MakeRow("Assets/MonsterSurvival/Features/Equipment/Equipment3D3/Content/StarlitCharm.asset", "Assets/MonsterSurvival/Features/Equipment/Equipment3D3/Content/Pickup_StarlitCharm.prefab",100f),
                MakeRow("Assets/MonsterSurvival/Features/Equipment/Equipment3D3/Content/ReaperEdge.asset", "Assets/MonsterSurvival/Features/Equipment/Equipment3D3/Content/Pickup_ReaperEdge.prefab",24f),
                MakeRow(Demo+"/WardenOath.asset", Demo+"/Pickup_WardenOath.prefab",28f)
            });
            PatchPrefab(titanPath, titan);
            PatchPrefab(wardenPath, warden);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("[3D.4] Installed on two Boss prefabs. Existing 3B loot preserved. Profiles can be edited in Equipment3D4/BossTables.");
            Alert("3D.4 installed. Test both Boss deaths in Play Mode. Open 11 - AUDIT. The two new exclusive items are equip-ready.");
        }
        catch (Exception e)
        {
            Debug.LogError("[3D.4] Installation stopped: " + e);
            Alert("Installation interrupted. Check Console. See backups in MonsterSurvival_3D4_Backups at project root. Do not rerun until resolved.");
        }
    }

    private static bool EditorSceneManagerSafeSave()
    {
        return UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
    }

    private static MS3D4BossEquipmentEntry MakeRow(string itemPath, string prefabPath, float chance)
    {
        var item=AssetDatabase.LoadAssetAtPath<MS3AItemData>(itemPath);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (!item || !prefab || !prefab.GetComponent<MS3AItemPickup>())
            throw new InvalidOperationException("Missing or invalid drop pair: " + itemPath + " / " + prefabPath);
        return new MS3D4BossEquipmentEntry{item=item,pickupPrefab=prefab,chancePercent=chance,minAmount=1,maxAmount=1};
    }

    private static MS3D4BossEquipmentTable EnsureTable(string name, MS3D4BossEquipmentEntry[] rows)
    {
        string path=Tables+"/"+name+".asset";
        var table=AssetDatabase.LoadAssetAtPath<MS3D4BossEquipmentTable>(path);
        if (table) return table; // Keep any user-tuned drop chances on re-install.
        table=ScriptableObject.CreateInstance<MS3D4BossEquipmentTable>();
        table.entries=new List<MS3D4BossEquipmentEntry>(rows);
        table.maxEquipmentDrops=3;table.spreadRadius=.65f;table.pickupLifetimeSeconds=120f;
        AssetDatabase.CreateAsset(table,path);
        return table;
    }

    private static MS3AItemData EnsureItem(ExclusiveSpec spec)
    {
        string path=Demo+"/"+spec.slug+".asset";
        var existing=AssetDatabase.LoadAssetAtPath<MS3AItemData>(path);
        if (existing) return existing;
        var importer=AssetImporter.GetAtPath(Art+"/"+spec.icon) as TextureImporter;
        if (!importer) throw new IOException("Missing texture importer for "+spec.icon);
        importer.textureType=TextureImporterType.Sprite;
        importer.spriteImportMode=SpriteImportMode.Single;
        importer.alphaIsTransparency=true;
        importer.mipmapEnabled=false;
        importer.filterMode=FilterMode.Bilinear;
        importer.SaveAndReimport();
        Sprite image=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"/"+spec.icon);
        if (!image) throw new IOException("Icon sprite import failed: "+spec.icon);
        var item=ScriptableObject.CreateInstance<MS3AItemData>();
        item.itemId=spec.id;item.displayName=spec.display;
        item.description=spec.flavor;item.icon=image;
        item.itemKind=MS3AItemKind.Material;item.maxStack=1;item.healAmount=0;
        AssetDatabase.CreateAsset(item,path);
        return item;
    }

    private static void EnsureExclusivePickup(ExclusiveSpec spec, MS3AItemData item, MS3CRarityCatalog rarity)
    {
        string path=Demo+"/Pickup_"+spec.slug+".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path)) return;
        if (!AssetDatabase.CopyAsset(SourcePickup,path)) throw new IOException("Could not copy pickup prefab from 3D1");
        GameObject contents=PrefabUtility.LoadPrefabContents(path);
        try
        {
            contents.name="Pickup_"+spec.slug;
            var pickup=contents.GetComponent<MS3AItemPickup>();
            if (!pickup) throw new InvalidOperationException("3D1 pickup lacks MS3AItemPickup");
            pickup.item=item;pickup.amount=1;
            var rootSprite=contents.GetComponent<SpriteRenderer>();
            if (rootSprite) { rootSprite.sprite=item.icon;rootSprite.enabled=false; }
            Transform visual=contents.transform.Find("MS3B1_SmallerWorldVisual");
            if (visual == null)
            {
                var obj=new GameObject("MS3B1_SmallerWorldVisual");
                visual=obj.transform;visual.SetParent(contents.transform,false);
                // 3B.1 scale is intentionally retained for cloned prefabs.
                visual.localScale=Vector3.one*.147f;
            }
            var sr=visual.GetComponent<SpriteRenderer>();
            if (!sr) sr=visual.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite=item.icon;sr.enabled=true;sr.sortingOrder=11;
            var fx=contents.GetComponent<MS3CPickupFX>();
            if (!fx) fx=contents.AddComponent<MS3CPickupFX>();
            fx.rarityCatalog=rarity;
            PrefabUtility.SaveAsPrefabAsset(contents,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    private static void PatchPrefab(string path, MS3D4BossEquipmentTable table)
    {
        GameObject prefabRoot=PrefabUtility.LoadPrefabContents(path);
        try
        {
            Health health=prefabRoot.GetComponent<Health>();
            if (!health) health=prefabRoot.GetComponentInChildren<Health>(true);
            if (!health) throw new InvalidOperationException("Boss has no Health: "+path);
            var extra=health.GetComponent<MS3D4BossEquipmentDrop>();
            if (!extra) extra=health.gameObject.AddComponent<MS3D4BossEquipmentDrop>();
            extra.table=table;
            EditorUtility.SetDirty(extra);
            if (!PrefabUtility.SaveAsPrefabAsset(prefabRoot,path))
                throw new IOException("Could not save prefab: "+path);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefabRoot); }
    }

    [MenuItem("Tools/MonsterSurvival/3D Equipment/11 - AUDIT 3D.4 Boss Loot (READ ONLY)")]
    public static void AuditMenu()
    {
        int tableCount=0, validRows=0;
        foreach (string name in new[] {"BossGear_ShadowTitan","BossGear_GraveWarden"})
        {
            var table=AssetDatabase.LoadAssetAtPath<MS3D4BossEquipmentTable>(Tables+"/"+name+".asset");
            if (!table) { Debug.LogWarning("[3D.4 AUDIT] Missing table: "+name); continue; }
            tableCount++;
            if (table.entries != null) foreach (var row in table.entries)
            {
                if (row != null && row.item && row.pickupPrefab && row.pickupPrefab.GetComponent<MS3AItemPickup>()) validRows++;
                else Debug.LogWarning("[3D.4 AUDIT] Invalid row in "+name);
            }
        }
        int prefabCount=0;
        foreach (string id in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(id);
            var obj=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (obj && obj.GetComponentInChildren<MS3D4BossEquipmentDrop>(true))
            {
                prefabCount++;
                var comp=obj.GetComponentInChildren<MS3D4BossEquipmentDrop>(true);
                Debug.Log("[3D.4 AUDIT] Configured prefab: "+path+" | Table="+(comp.table?comp.table.name:"MISSING"));
            }
        }
        int exclusiveCount=0;
        var cat=AssetDatabase.LoadAssetAtPath<MS3DEquipmentCatalog>(EquipmentPath);
        var bonus=AssetDatabase.LoadAssetAtPath<MS3D2BonusCatalog>(BasePath);
        var affix=AssetDatabase.LoadAssetAtPath<MS3D3AdvancedCatalog>(AdvancedPath);
        foreach (var s in Exclusives)
        {
            var item=AssetDatabase.LoadAssetAtPath<MS3AItemData>(Demo+"/"+s.slug+".asset");
            MS3DEquipmentDefinition def=null;
            if (item && cat && cat.TryGet(item,out def) && def != null && bonus && bonus.HasEntry(item) && affix && affix.Contains(item)) exclusiveCount++;
        }
        Debug.Log("[3D.4 AUDIT] Tables="+tableCount+"/2; valid drop rows="+validRows+"/6; exclusive gear="+exclusiveCount+"/2; prefabs configured="+prefabCount+" (expect 2 selected actual Boss prefabs). READ ONLY; Play Mode tests still necessary.");
    }

    private void AuditSelection()
    {
        AuditMenu();
        foreach (var boss in new[] {bossTitan,bossWarden})
        {
            string path=boss?AssetDatabase.GetAssetPath(boss):"(not selected)";
            MS3D4BossEquipmentDrop comp=boss?boss.GetComponentInChildren<MS3D4BossEquipmentDrop>(true):null;
            Debug.Log("[3D.4 AUDIT] Selected: "+path+" | component="+(comp?"YES":"NO")+" | table="+(comp && comp.table?comp.table.name:"NONE"));
        }
    }

    private void Remove()
    {
        if (EditorApplication.isPlaying) { Alert("Stop Play Mode before removing.");return; }
        string titanPath,wardenPath;
        if (!CheckPrefab(bossTitan,"Shadow Titan",out titanPath) ||
            !CheckPrefab(bossWarden,"Grave Warden",out wardenPath)) return;
        if (titanPath==wardenPath) { Alert("Choose two different Boss prefabs.");return; }
        if (!EditorUtility.DisplayDialog("REMOVE 3D.4 components",
            "Remove ONLY the MS3D4BossEquipmentDrop components from both selected prefabs?\nOther 3B drops stay. Equipment assets and user-tuned drop tables remain available.","REMOVE","CANCEL")) return;
        BackupBeforeInstall(new[]{titanPath,wardenPath});
        RemoveFromPrefab(titanPath);
        RemoveFromPrefab(wardenPath);
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        Alert("3D.4 components removed from selected prefabs. Other equipment and loot remain.");
    }

    private static void RemoveFromPrefab(string path)
    {
        GameObject root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (var component in root.GetComponentsInChildren<MS3D4BossEquipmentDrop>(true))
                UnityEngine.Object.DestroyImmediate(component);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void BackupBeforeInstall(string[] paths)
    {
        string root=Directory.GetParent(Application.dataPath).FullName;
        string dest=Path.Combine(root,"MonsterSurvival_3D4_Backups",
            DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
        foreach (string assetPath in paths)
        {
            if (string.IsNullOrEmpty(assetPath)) continue;
            string source=Path.Combine(root,assetPath);
            if (!File.Exists(source)) continue;
            string target=Path.Combine(dest,assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(source,target,true);
            if (File.Exists(source+".meta")) File.Copy(source+".meta",target+".meta",true);
        }
        Debug.Log("[3D.4] Backed up affected files to " + dest);
    }

    private static void EnsureFolder(string path)
    {
        string[] parts=path.Split('/');string current=parts[0];
        for(int i=1;i<parts.Length;i++)
        {
            string next=current+"/"+parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current,parts[i]);
            current=next;
        }
    }

    private static void Alert(string text) { EditorUtility.DisplayDialog("MonsterSurvival 3D.4",text,"OK"); }
}
#endif
