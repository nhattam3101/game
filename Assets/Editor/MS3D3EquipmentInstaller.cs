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

public static class MS3D3EquipmentInstaller
{
    private const string Root = "Assets/MonsterSurvival/Equipment3D3";
    private const string Demo = Root + "/Demo";
    private const string Art = Root + "/Art";
    private const string Backup = Root + "/Backups";
    private const string ExtraPath = Root + "/AdvancedAffixes.asset";
    private const string EqPath = "Assets/MonsterSurvival/Equipment3D1/EquipmentCatalog.asset";
    private const string BasicPath = "Assets/MonsterSurvival/Equipment3D2/EquipmentBonuses.asset";
    private const string RarityPath = "Assets/MonsterSurvival/Inventory3C/RarityCatalog.asset";
    private const string SourcePickup = "Assets/MonsterSurvival/Equipment3D1/Demo/Pickup_DuskBlade.prefab";

    private struct Spec
    {
        public string slug, id, name, description, icon;
        public MS3DEquipmentType type;
        public MS3DQuality tier;
        public MS3D2Bonus baseBonus;
        public MS3D3AdvancedBonus advanced;
        public Spec(string slug, string id, string name, string desc, string icon, MS3DEquipmentType type, MS3DQuality tier,
            float dmg, float atk, float move, float crit, float hp, float flat, float range, float shotSpeed)
        {
            this.slug=slug; this.id=id; this.name=name; this.description=desc; this.icon=icon; this.type=type; this.tier=tier;
            baseBonus=new MS3D2Bonus {damagePercent=dmg,attackSpeedPercent=atk,moveSpeedPercent=move,critChancePoints=crit,maxHealthFlat=hp};
            advanced=new MS3D3AdvancedBonus {flatBulletDamage=flat,attackRangeFlat=range,bulletSpeedPercent=shotSpeed};
        }
    }

    private static readonly Spec[] Items = {
        new Spec("AshenKnife","ashen_knife_3d3","ASHEN KNIFE","Common weapon. Lightweight and easy to find.","MS3D3_AshenKnife.png",MS3DEquipmentType.Weapon,MS3DQuality.Common,8,0,0,0,0,0,.25f,0),
        new Spec("MistMail","mist_mail_3d3","MIST MAIL","Rare armor reinforced with runic threads.","MS3D3_MistMail.png",MS3DEquipmentType.Armor,MS3DQuality.Rare,0,0,0,0,18,0,0,0),
        new Spec("HollowBow","hollow_bow_3d3","HOLLOW BOW","Epic weapon. Longer reach and faster magical shots.","MS3D3_HollowBow.png",MS3DEquipmentType.Weapon,MS3DQuality.Epic,24,8,0,0,0,1f,1f,18),
        new Spec("StarlitCharm","starlit_charm_3d3","STARLIT CHARM","Epic accessory filled with starlight.","MS3D3_StarlitCharm.png",MS3DEquipmentType.Accessory,MS3DQuality.Epic,0,0,10,6,0,0,0,10),
        new Spec("ReaperEdge","reaper_edge_3d3","REAPER EDGE","Legendary weapon. Warden-forged edge.","MS3D3_ReaperEdge.png",MS3DEquipmentType.Weapon,MS3DQuality.Legendary,34,16,0,0,0,3f,1.2f,22),
        new Spec("SoulguardPlate","soulguard_plate_3d3","SOULGUARD PLATE","Legendary armor. Powerful protection with a mobility bonus.","MS3D3_SoulguardPlate.png",MS3DEquipmentType.Armor,MS3DQuality.Legendary,0,0,5,0,45,0,0,0)
    };

    [MenuItem("Tools/MonsterSurvival/3D Equipment/7 - INSTALL 3D.3 Rarity + Advanced Attributes (OPEN Scene)")]
    public static void Install()
    {
        if (Application.isPlaying) { Alert("Stop Play Mode before installing."); return; }
        Scene scene=SceneManager.GetActiveScene();
        var ui=FindUI(scene);
        var eui=ui ? ui.GetComponent<MS3DEquipmentUI>() : null;
        var eq=eui ? eui.equipment : null;
        var stats=eq ? eq.GetComponent<MS3D2EquipmentStats>() : null;
        var shooter=eq ? eq.GetComponent<PlayerShooter>() : null;
        var cat=AssetDatabase.LoadAssetAtPath<MS3DEquipmentCatalog>(EqPath);
        var baseCat=AssetDatabase.LoadAssetAtPath<MS3D2BonusCatalog>(BasicPath);
        if (!eui || !eq || !stats || !shooter || !cat || !baseCat || !eui.equipmentWindow)
        {
            Alert("3D.1 and 3D.2 must be installed first (Equipment UI, Equipment Stats, EquipmentCatalog, Bonuses, PlayerShooter). Open SampleScene. No changes made.");
            return;
        }
        if (eq.GetComponent<MS3D3AdvancedStats>() || ui.GetComponent<MS3D3GearPreviewUI>())
        { Alert("3D.3 already installed in this Scene. Use 8 - AUDIT. You may edit catalogs by Inspector."); return; }
        if (string.IsNullOrEmpty(scene.path)) { Alert("Save SampleScene first."); return; }
        if (!EditorUtility.DisplayDialog("3D.3 Equipment Rarity + Advanced Stats",
            "Back up the Scene and equipment catalogs, add six new equipment variants, then attach reversible advanced stat logic and a read-only selected-item detail line?\n\nWill NOT touch PlayerStats, PlayerShooter, Health, Inventory UI buttons or Boss drops.",
            "INSTALL","CANCEL")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BackupAssets(scene);
        EnsureFolder(Demo);
        var adv=AssetDatabase.LoadAssetAtPath<MS3D3AdvancedCatalog>(ExtraPath);
        if (!adv) { adv=ScriptableObject.CreateInstance<MS3D3AdvancedCatalog>(); AssetDatabase.CreateAsset(adv,ExtraPath); }
        Undo.RecordObject(cat,"3D.3 equipment definitions");
        Undo.RecordObject(baseCat,"3D.3 base bonuses");
        Undo.RecordObject(adv,"3D.3 advanced affixes");
        var rarity=AssetDatabase.LoadAssetAtPath<MS3CRarityCatalog>(RarityPath);
        if (rarity) Undo.RecordObject(rarity,"3D.3 item rarity");
        foreach (var spec in Items)
        {
            var item=EnsureItem(spec);
            if (cat.entries == null) cat.entries=new List<MS3DEquipmentDefinition>();
            if (!cat.TryGet(item,out MS3DEquipmentDefinition existing))
                cat.entries.Add(new MS3DEquipmentDefinition{item=item,type=spec.type,quality=spec.tier,flavorText=spec.description});
            if (baseCat.entries == null) baseCat.entries=new List<MS3D2BonusEntry>();
            if (!baseCat.HasEntry(item)) baseCat.entries.Add(new MS3D2BonusEntry{item=item,bonuses=spec.baseBonus});
            if (adv.entries == null) adv.entries=new List<MS3D3AdvancedEntry>();
            if (!adv.Contains(item)) adv.entries.Add(new MS3D3AdvancedEntry{item=item,bonus=spec.advanced});
            if (rarity)
            {
                if (rarity.items == null) rarity.items=new List<MS3CRarityEntry>();
                bool present=false;
                foreach (var r in rarity.items) if (r != null && r.item==item) { present=true; break; }
                if (!present) rarity.items.Add(new MS3CRarityEntry{item=item,rarity=(MS3CRarity)spec.tier});
            }
            MakePickup(spec,item,rarity);
        }
        EditorUtility.SetDirty(cat);EditorUtility.SetDirty(baseCat);EditorUtility.SetDirty(adv);
        if (rarity) EditorUtility.SetDirty(rarity);

        var runtime=Undo.AddComponent<MS3D3AdvancedStats>(eq.gameObject);
        runtime.equipment=eq;runtime.shooter=shooter;runtime.advancedCatalog=adv;
        EditorUtility.SetDirty(runtime);
        var preview=Undo.AddComponent<MS3D3GearPreviewUI>(ui.gameObject);
        preview.inventoryUI=ui;preview.equipmentUI=eui;preview.baseBonuses=baseCat;preview.advancedBonuses=adv;
        GameObject go=new GameObject("MS3D3_GearDetails",typeof(RectTransform),typeof(Image));
        Undo.RegisterCreatedObjectUndo(go,"3D.3 details bar");
        go.layer=5;
        var shade=ui.panel ? ui.panel.transform : ui.transform;
        go.transform.SetParent(shade,false);
        var rt=go.GetComponent<RectTransform>();
        rt.anchorMin=new Vector2(.5f,.025f);rt.anchorMax=new Vector2(.5f,.025f);
        rt.sizeDelta=new Vector2(1010f,65f);rt.anchoredPosition=Vector2.zero;
        var image=go.GetComponent<Image>();image.color=new Color(.05f,.04f,.095f,.92f);image.raycastTarget=false;
        var textGO=new GameObject("Description",typeof(RectTransform),typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(textGO,"3D.3 description");
        textGO.layer=5;textGO.transform.SetParent(go.transform,false);
        var tx=textGO.GetComponent<RectTransform>();
        tx.anchorMin=new Vector2(.025f,.09f);tx.anchorMax=new Vector2(.975f,.91f);tx.offsetMin=tx.offsetMax=Vector2.zero;
        var label=textGO.GetComponent<TextMeshProUGUI>();
        label.text="SELECT EQUIPMENT TO SEE RARITY / BASE ATTRIBUTES / ADVANCED AFFIXES";
        label.fontSize=15;label.enableAutoSizing=true;label.fontSizeMin=10;label.fontSizeMax=15;
        label.alignment=TextAlignmentOptions.Center;label.textWrappingMode=TextWrappingModes.Normal;
        label.color=new Color(.76f,.7f,.84f,1);label.raycastTarget=false;
        if (TMP_Settings.defaultFontAsset) label.font=TMP_Settings.defaultFontAsset;
        preview.details=label;EditorUtility.SetDirty(preview);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        Debug.Log("[3D.3] Installed: 6 demo variants, reversible shooter affixes and gear details. Old buttons and Boss drop tables unchanged.");
        Alert("Installed. Drag Pickup_* from Equipment3D3/Demo into SampleScene and test in Play Mode. Use menu 8 - AUDIT.");
    }

    [MenuItem("Tools/MonsterSurvival/3D Equipment/8 - AUDIT 3D.3 (READ ONLY)")]
    public static void Audit()
    {
        var scene=SceneManager.GetActiveScene();var ui=FindUI(scene);
        var eui=ui?ui.GetComponent<MS3DEquipmentUI>():null;
        var eq=eui?eui.equipment:null;
        var affix=eq?eq.GetComponent<MS3D3AdvancedStats>():null;
        var preview=ui?ui.GetComponent<MS3D3GearPreviewUI>():null;
        var cat=AssetDatabase.LoadAssetAtPath<MS3DEquipmentCatalog>(EqPath);
        var basic=AssetDatabase.LoadAssetAtPath<MS3D2BonusCatalog>(BasicPath);
        var adv=AssetDatabase.LoadAssetAtPath<MS3D3AdvancedCatalog>(ExtraPath);
        int ready=0;
        foreach (var spec in Items)
        {
            var item=AssetDatabase.LoadAssetAtPath<MS3AItemData>(Demo+"/"+spec.slug+".asset");
            if (!item) continue;
            MS3DEquipmentDefinition def=null;
            if (cat && cat.TryGet(item,out def) && def != null && basic && basic.HasEntry(item) && adv && adv.Contains(item)) ready++;
        }
        Debug.Log("[3D.3 QA] Scene="+scene.name+" Equipment="+(eq?"PASS":"MISSING")+
            " | 3D2="+(eq && eq.GetComponent<MS3D2EquipmentStats>()?"PASS":"MISSING")+
            " | PlayerShooter="+(eq && eq.GetComponent<PlayerShooter>()?"PASS":"MISSING")+
            " | AdvancedStats="+(affix?"PASS":"MISSING")+
            " | Details="+(preview && preview.details?"PASS":"MISSING")+
            " | New equipment registered="+ready+"/"+Items.Length+
            " | AffixCatalog="+(adv?"PASS":"MISSING")+
            " | EquipmentCatalog="+(cat?"PASS":"MISSING"));
    }

    [MenuItem("Tools/MonsterSurvival/3D Equipment/9 - REMOVE 3D.3 (OPEN Scene)")]
    public static void Remove()
    {
        if (Application.isPlaying) { Alert("Stop Play Mode before removing."); return; }
        var scene=SceneManager.GetActiveScene();var ui=FindUI(scene);
        var eui=ui?ui.GetComponent<MS3DEquipmentUI>():null;
        var eq=eui?eui.equipment:null;
        if (!ui || !eq) { Alert("No 3D.1/3D.3 UI found."); return; }
        if (!EditorUtility.DisplayDialog("3D.3 REMOVE", "Remove 3D.3 components and its six catalog registrations? Existing 3D.1/3D.2 and all item/pickup assets will remain. Any new 3D.3 gear placed in scene should be collected/removed separately.","REMOVE","CANCEL")) return;
        var runtime=eq.GetComponent<MS3D3AdvancedStats>();if (runtime) Undo.DestroyObjectImmediate(runtime);
        var preview=ui.GetComponent<MS3D3GearPreviewUI>();
        if (preview)
        {
            if (preview.details) Undo.DestroyObjectImmediate(preview.details.transform.parent.gameObject);
            Undo.DestroyObjectImmediate(preview);
        }
        var cat=AssetDatabase.LoadAssetAtPath<MS3DEquipmentCatalog>(EqPath);
        var baseCat=AssetDatabase.LoadAssetAtPath<MS3D2BonusCatalog>(BasicPath);
        var adv=AssetDatabase.LoadAssetAtPath<MS3D3AdvancedCatalog>(ExtraPath);
        var rarity=AssetDatabase.LoadAssetAtPath<MS3CRarityCatalog>(RarityPath);
        if (cat) Undo.RecordObject(cat,"Remove 3D.3 entries");
        if (baseCat) Undo.RecordObject(baseCat,"Remove 3D.3 bonuses");
        if (adv) Undo.RecordObject(adv,"Remove 3D.3 affixes");
        if (rarity) Undo.RecordObject(rarity,"Remove 3D.3 rarities");
        foreach (var spec in Items)
        {
            var item=AssetDatabase.LoadAssetAtPath<MS3AItemData>(Demo+"/"+spec.slug+".asset");
            if (!item) continue;
            if (cat && cat.entries != null) cat.entries.RemoveAll(r => r != null && r.item == item);
            if (baseCat && baseCat.entries != null) baseCat.entries.RemoveAll(r => r != null && r.item == item);
            if (adv && adv.entries != null) adv.entries.RemoveAll(r => r != null && r.item == item);
            if (rarity && rarity.items != null) rarity.items.RemoveAll(r => r != null && r.item == item);
        }
        if (cat) EditorUtility.SetDirty(cat);
        if (baseCat) EditorUtility.SetDirty(baseCat);
        if (adv) EditorUtility.SetDirty(adv);
        if (rarity) EditorUtility.SetDirty(rarity);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("[3D.3] Removed addon and six registrations; item assets kept. 3D1/3D2 unchanged.");
    }

    private static MS3AInventoryUI FindUI(Scene scene)
    {
        foreach (var obj in scene.GetRootGameObjects())
            if (obj.name=="MS3A_InventoryCanvas") return obj.GetComponent<MS3AInventoryUI>();
        return null;
    }

    private static MS3AItemData EnsureItem(Spec s)
    {
        string path=Demo+"/"+s.slug+".asset";
        var data=AssetDatabase.LoadAssetAtPath<MS3AItemData>(path);
        if (data) return data;
        string art=Art+"/"+s.icon;
        var importer=AssetImporter.GetAtPath(art) as TextureImporter;
        if (!importer) throw new IOException("Missing artwork: "+art);
        importer.textureType=TextureImporterType.Sprite;
        importer.spriteImportMode=SpriteImportMode.Single;
        importer.alphaIsTransparency=true;
        importer.mipmapEnabled=false;
        importer.filterMode=FilterMode.Bilinear;
        importer.SaveAndReimport();
        Sprite sprite=AssetDatabase.LoadAssetAtPath<Sprite>(art);
        data=ScriptableObject.CreateInstance<MS3AItemData>();
        data.itemId=s.id;data.displayName=s.name;data.description=s.description;
        data.icon=sprite;data.itemKind=MS3AItemKind.Material;data.maxStack=1;data.healAmount=0;
        AssetDatabase.CreateAsset(data,path);
        return data;
    }

    private static void MakePickup(Spec s,MS3AItemData item,MS3CRarityCatalog rarity)
    {
        string path=Demo+"/Pickup_"+s.slug+".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path)) return;
        var src=AssetDatabase.LoadAssetAtPath<GameObject>(SourcePickup);
        GameObject root=null;
        bool fromPrefab=src;
        if (fromPrefab)
        {
            if (!AssetDatabase.CopyAsset(SourcePickup,path)) throw new IOException("Could not clone demo pickup: "+path);
            root=PrefabUtility.LoadPrefabContents(path);
        }
        else
        {
            root=new GameObject("Pickup_"+s.slug);
            var world=root.AddComponent<SpriteRenderer>();world.sprite=item.icon;world.sortingOrder=10;
            var rb=root.AddComponent<Rigidbody2D>();rb.bodyType=RigidbodyType2D.Kinematic;rb.gravityScale=0;
            var coll=root.AddComponent<CircleCollider2D>();coll.isTrigger=true;coll.radius=.8f;
            root.AddComponent<MS3AItemPickup>();
        }
        try
        {
            root.name="Pickup_"+s.slug;
            var pickup=root.GetComponent<MS3AItemPickup>();
            if (!pickup) throw new InvalidOperationException("Pickup prefab is missing 3A MS3AItemPickup.");
            pickup.item=item;pickup.amount=1;
            var original=root.GetComponent<SpriteRenderer>();
            if (original) { original.sprite=item.icon; original.enabled=false; }
            var child=root.transform.Find("MS3B1_SmallerWorldVisual");
            if (!child)
            {
                var visual=new GameObject("MS3B1_SmallerWorldVisual");
                child=visual.transform;child.SetParent(root.transform,false);
                child.localScale=Vector3.one*.147f;
                visual.AddComponent<SpriteRenderer>();
            }
            var render=child.GetComponent<SpriteRenderer>();
            if (!render) render=child.gameObject.AddComponent<SpriteRenderer>();
            render.sprite=item.icon;render.enabled=true;render.sortingOrder=11;
            if (rarity)
            {
                var fx=root.GetComponent<MS3CPickupFX>();
                if (!fx) fx=root.AddComponent<MS3CPickupFX>();
                fx.rarityCatalog=rarity;
            }
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally
        {
            if (fromPrefab) PrefabUtility.UnloadPrefabContents(root);
            else UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void BackupAssets(Scene scene)
    {
        EnsureFolder(Backup);
        var paths=new string[]{scene.path,EqPath,BasicPath,RarityPath};
        foreach (var path in paths)
        {
            if (string.IsNullOrEmpty(path) || !AssetDatabase.LoadMainAssetAtPath(path)) continue;
            string destination=Backup+"/"+Path.GetFileNameWithoutExtension(path)+"_BEFORE_3D3"+Path.GetExtension(path);
            if (AssetDatabase.LoadMainAssetAtPath(destination)) continue;
            if (!AssetDatabase.CopyAsset(path,destination)) throw new IOException("Cannot back up "+path);
        }
    }
    private static void EnsureFolder(string path)
    {
        string[] sections=path.Split('/');string current=sections[0];
        for(int i=1;i<sections.Length;i++)
        {
            string next=current+"/"+sections[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current,sections[i]);
            current=next;
        }
    }
    private static void Alert(string message) { EditorUtility.DisplayDialog("MonsterSurvival 3D.3",message,"OK"); }
}
#endif
