#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MS3D1EquipmentInstaller
{
    private const string Root = "Assets/MonsterSurvival/Equipment3D1";
    private const string Art = Root + "/Art";
    private const string Demo = Root + "/Demo";
    private const string CatalogPath = Root + "/EquipmentCatalog.asset";
    private const string SourceDemo = "Assets/MonsterSurvival/Inventory3A/Demo/Pickup_Crystal.prefab";
    private const string RarityPath = "Assets/MonsterSurvival/Inventory3C/RarityCatalog.asset";
    private static readonly Color Purple = new Color(.13f,.10f,.22f,.99f);
    private static readonly Color Pale = new Color(.94f,.91f,.99f,1f);
    private static readonly Color Muted = new Color(.67f,.65f,.78f,1f);
    private static readonly Color Gold = new Color(1f,.78f,.43f,1f);

    [MenuItem("Tools/MonsterSurvival/3D Equipment/1 - INSTALL Equipment 3D.1 (OPEN SampleScene)")]
    public static void Install()
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog("3D.1", "Stop Play Mode first.", "OK"); return; }
        Scene scene = SceneManager.GetActiveScene();
        var oldUI = Find3AUI(scene);
        if (oldUI == null || oldUI.inventory == null || oldUI.cells == null || oldUI.cells.Length != 20)
        {
            EditorUtility.DisplayDialog("3D.1", "Open SampleScene with a working 3A Inventory (20 slots) first. This installer does NOT rebuild 3A.", "OK");
            return;
        }
        if (oldUI.GetComponent<MS3DEquipmentUI>())
        {
            EditorUtility.DisplayDialog("3D.1", "Equipment UI already installed. Use 2 - AUDIT; do not reinstall.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Equipment 3D.1", "Back up the OPEN Scene, add Weapon / Armor / Accessory slots alongside the existing Inventory UI and create 3 demo equipment pickups?\n\n3A/3B/3C scripts, loot chances, gameplay stats and HUD will NOT be overwritten.", "INSTALL", "Cancel")) return;

        Backup(scene);
        EnsureFolder(Demo);
        var catalog = AssetDatabase.LoadAssetAtPath<MS3DEquipmentCatalog>(CatalogPath);
        if (!catalog)
        {
            catalog = ScriptableObject.CreateInstance<MS3DEquipmentCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        var sword = EnsureDemoItem("DuskBlade", "dusk_blade_3d1", "DUSK BLADE", "Weapon • Magical blade. Stats will be added in 3D.2.", "MS3D_DuskBlade.png");
        var armor = EnsureDemoItem("VioletArmor", "violet_armor_3d1", "VIOLET ARMOR", "Armor • Rune-plated protection. Stats will be added in 3D.2.", "MS3D_VioletArmor.png");
        var ring = EnsureDemoItem("WardenRing", "warden_ring_3d1", "WARDEN RING", "Accessory • An ancient charm. Stats will be added in 3D.2.", "MS3D_WardenRing.png");
        AddMapping(catalog, sword, MS3DEquipmentType.Weapon, MS3DQuality.Rare);
        AddMapping(catalog, armor, MS3DEquipmentType.Armor, MS3DQuality.Epic);
        AddMapping(catalog, ring, MS3DEquipmentType.Accessory, MS3DQuality.Legendary);
        EditorUtility.SetDirty(catalog);
        TryRegister3CRarity(sword, 1);
        TryRegister3CRarity(armor, 2);
        TryRegister3CRarity(ring, 3);
        MakeDemoPickup("Pickup_DuskBlade", sword);
        MakeDemoPickup("Pickup_VioletArmor", armor);
        MakeDemoPickup("Pickup_WardenRing", ring);

        var player = oldUI.inventory.gameObject;
        var equipped = player.GetComponent<MS3DPlayerEquipment>();
        if (!equipped) equipped = Undo.AddComponent<MS3DPlayerEquipment>(player);
        Undo.RecordObject(equipped, "Connect Equipment Catalog");
        equipped.catalog = catalog;
        EditorUtility.SetDirty(equipped);
        BuildUI(oldUI, equipped);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Equipment 3D.1] INSTALLED: 3 slots + 3 demo pickups. No gameplay stats affected. Test with Play -> pick gear -> I -> choose item -> EQUIP.");
        EditorUtility.DisplayDialog("3D.1 Installed", "Import succeeded. Drag demo pickups from Equipment3D1/Demo into SampleScene. Play -> pick gear -> I -> select item -> EQUIP. Use each equipment card to UNEQUIP.", "OK");
    }

    [MenuItem("Tools/MonsterSurvival/3D Equipment/2 - AUDIT Equipment (READ ONLY)")]
    public static void Audit()
    {
        Scene scene = SceneManager.GetActiveScene();
        var ui = Find3AUI(scene);
        var eq = ui ? ui.GetComponent<MS3DEquipmentUI>() : null;
        var manager = ui && ui.inventory ? ui.inventory.GetComponent<MS3DPlayerEquipment>() : null;
        int clickable = 0;
        if (ui != null && ui.cells != null)
            foreach (var cell in ui.cells) if (cell && cell.button && cell.frame && cell.frame.raycastTarget) clickable++;
        var cat = AssetDatabase.LoadAssetAtPath<MS3DEquipmentCatalog>(CatalogPath);
        Debug.Log("[Equipment 3D.1 QA] Scene=" + scene.name + " | Inventory3A=" + (ui ? "PASS" : "MISSING") +
            " | SlotsClickable=" + clickable + "/20 | EquipmentUI=" + (eq ? "PASS" : "MISSING") +
            " | PlayerEquipment=" + (manager ? "PASS" : "MISSING") + " | Catalog=" + (cat ? "PASS" : "MISSING") +
            " | Entries=" + (cat && cat.entries != null ? cat.entries.Count : 0) +
            " | EquipButton=" + (eq && eq.equipButton ? "PASS" : "MISSING") +
            " | Window=" + (eq && eq.equipmentWindow ? "PASS" : "MISSING"));
    }

    [MenuItem("Tools/MonsterSurvival/3D Equipment/3 - REMOVE Equipment UI and Component (OPEN Scene)")]
    public static void Remove()
    {
        if (Application.isPlaying) { Debug.LogWarning("[3D.1] Stop Play Mode first."); return; }
        var ui = Find3AUI(SceneManager.GetActiveScene());
        var eq = ui ? ui.GetComponent<MS3DEquipmentUI>() : null;
        if (!eq) { Debug.LogWarning("[3D.1] No Equipment UI in open Scene."); return; }
        if (!EditorUtility.DisplayDialog("3D.1 Remove", "Remove ONLY equipment UI and PlayerEquipment component? Existing 3A Inventory, demo assets and 3C styles are preserved.", "REMOVE", "Cancel")) return;
        if (eq.inventoryWindow)
        {
            Undo.RecordObject(eq.inventoryWindow, "Restore 3A window");
            eq.inventoryWindow.anchoredPosition = eq.originalWindowPosition;
            eq.inventoryWindow.localScale = eq.originalWindowScale;
        }
        if (eq.originalUseButtonRect)
        {
            Undo.RecordObject(eq.originalUseButtonRect, "Restore 3A USE button");
            eq.originalUseButtonRect.anchorMin = eq.originalUseAnchorMin;
            eq.originalUseButtonRect.anchorMax = eq.originalUseAnchorMax;
            eq.originalUseButtonRect.offsetMin = eq.originalUseButtonRect.offsetMax = Vector2.zero;
        }
        var shade = ui.panel ? ui.panel.transform : null;
        if (shade)
        {
            var overlay = shade.Find("MS3D_EquipmentWindow");
            if (overlay) Undo.DestroyObjectImmediate(overlay.gameObject);
            var invWindow = shade.Find("InventoryWindow");
            if (invWindow)
            {
                var equipButton = invWindow.Find("MS3D_EquipButton");
                if (equipButton) Undo.DestroyObjectImmediate(equipButton.gameObject);
            }
        }
        var manager = eq.equipment;
        Undo.DestroyObjectImmediate(eq);
        if (manager) Undo.DestroyObjectImmediate(manager);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("[3D.1] Removed equipment UI; original 3A window restored. Assets retained.");
    }

    private static void BuildUI(MS3AInventoryUI inventoryUI, MS3DPlayerEquipment playerEquipment)
    {
        var shade = inventoryUI.panel ? inventoryUI.panel.transform : null;
        var oldWindow = shade ? shade.Find("InventoryWindow") as RectTransform : null;
        if (!oldWindow || !inventoryUI.useButton)
            throw new InvalidOperationException("3A inventory hierarchy has been customized. Missing InventoryWindow or UseButton. No UI was changed.");
        var add = Undo.AddComponent<MS3DEquipmentUI>(inventoryUI.gameObject);
        add.inventoryUI = inventoryUI;
        add.equipment = playerEquipment;
        add.unequipButtons = new Button[3];
        add.slotIcons = new Image[3];
        add.slotBorders = new Image[3];
        add.slotNames = new TMP_Text[3];
        add.inventoryWindow = oldWindow;
        add.originalWindowPosition = oldWindow.anchoredPosition;
        add.originalWindowScale = oldWindow.localScale;
        add.originalUseButtonRect = inventoryUI.useButton.GetComponent<RectTransform>();
        add.originalUseAnchorMin = add.originalUseButtonRect.anchorMin;
        add.originalUseAnchorMax = add.originalUseButtonRect.anchorMax;
        // Save snapshot first: Remove can always restore even if responsive layout has run.
        Undo.RecordObject(oldWindow, "Equipment UI Layout");
        oldWindow.anchoredPosition = new Vector2(-186,0);
        Undo.RecordObject(add.originalUseButtonRect, "Move USE button");
        add.originalUseButtonRect.anchorMin = new Vector2(.73f,.13f);
        add.originalUseButtonRect.anchorMax = new Vector2(.94f,.205f);
        add.originalUseButtonRect.offsetMin = add.originalUseButtonRect.offsetMax = Vector2.zero;
        add.equipButton = MakeButton("MS3D_EquipButton", oldWindow, "EQUIP", new Vector2(.73f,.035f), new Vector2(.94f,.112f), 19);

        var equipWin = Panel("MS3D_EquipmentWindow", shade, new Vector2(.5f,.5f),new Vector2(.5f,.5f),Purple);
        equipWin.sizeDelta = new Vector2(346,670);
        equipWin.anchoredPosition = new Vector2(351,0);
        equipWin.gameObject.AddComponent<Outline>().effectColor = new Color(.53f,.36f,.72f,.77f);
        add.equipmentWindow = equipWin;
        Label("EQUIPMENT", equipWin, "Title", 31, Gold, new Vector2(.09f,.875f),new Vector2(.92f,.958f),TextAlignmentOptions.Left);
        Label("3 SLOTS  /  CLICK TO UNEQUIP", equipWin, "Subtitle", 14, Muted, new Vector2(.085f,.836f),new Vector2(.96f,.887f),TextAlignmentOptions.Left);
        string[] kinds = { "WEAPON", "ARMOR", "ACCESSORY" };
        for (int i=0;i<3;i++)
        {
            float top=.805f - i*.222f;
            var card = Panel("Slot_" + kinds[i], equipWin, new Vector2(.085f,top-.193f),new Vector2(.915f,top),new Color(.085f,.075f,.155f,.98f));
            var accent = Panel("RarityBar",card,new Vector2(0,0),new Vector2(.022f,1f),new Color(.35f,.22f,.52f));
            accent.GetComponent<Image>().raycastTarget=false;
            add.slotBorders[i] = accent.GetComponent<Image>();
            Label(kinds[i], card, "SlotTitle", 18, Gold, new Vector2(.08f,.74f),new Vector2(.94f,.97f),TextAlignmentOptions.Left);
            var imageRect = Rect("Icon",card,new Vector2(.06f,.20f),new Vector2(.33f,.71f));
            Image img = imageRect.gameObject.AddComponent<Image>();
            img.preserveAspect=true; img.raycastTarget=false; img.enabled=false;
            add.slotIcons[i] = img;
            add.slotNames[i] = Label("EMPTY",card,"ItemName",14,Pale,new Vector2(.36f,.35f),new Vector2(.97f,.72f),TextAlignmentOptions.Left);
            add.unequipButtons[i] = MakeButton("Unequip_" + kinds[i],card,"UNEQUIP",new Vector2(.40f,.09f),new Vector2(.94f,.34f),15);
        }
        add.hint = Label("Select gear in the 20-slot Inventory, then EQUIP.",equipWin,"Status",14,Muted,
            new Vector2(.085f,.04f),new Vector2(.94f,.153f),TextAlignmentOptions.TopLeft);
        add.hint.textWrappingMode = TextWrappingModes.Normal;
        EditorUtility.SetDirty(add);
    }

    private static MS3AInventoryUI Find3AUI(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == "MS3A_InventoryCanvas") return root.GetComponent<MS3AInventoryUI>();
        return null;
    }
    private static void Backup(Scene scene)
    {
        string folder=Root+"/Backups";
        EnsureFolder(folder);
        if (string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("Save SampleScene before installing.");
        string backup = folder+"/"+Path.GetFileNameWithoutExtension(scene.path)+"_BEFORE_3D1.unity";
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(backup))
        {
            EditorSceneManager.SaveScene(scene);
            if (!AssetDatabase.CopyAsset(scene.path,backup)) throw new IOException("Cannot back up Scene: "+scene.path);
        }
    }
    private static void AddMapping(MS3DEquipmentCatalog catalog,MS3AItemData item,MS3DEquipmentType type,MS3DQuality quality)
    {
        if (catalog.entries == null) catalog.entries = new System.Collections.Generic.List<MS3DEquipmentDefinition>();
        foreach (var row in catalog.entries) if (row != null && row.item == item) return;
        catalog.entries.Add(new MS3DEquipmentDefinition { item=item,type=type,quality=quality });
    }
    private static MS3AItemData EnsureDemoItem(string file,string id,string name,string description,string art)
    {
        string path=Demo+"/"+file+".asset";
        var item = AssetDatabase.LoadAssetAtPath<MS3AItemData>(path);
        if (item) return item; // NEVER override user edits
        Sprite icon = ImportSprite(Art+"/"+art);
        item=ScriptableObject.CreateInstance<MS3AItemData>();
        item.itemId=id; item.displayName=name; item.description=description;
        item.icon=icon; item.itemKind=MS3AItemKind.Material; item.maxStack=1; item.healAmount=0;
        AssetDatabase.CreateAsset(item,path);
        return item;
    }
    private static Sprite ImportSprite(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!imp) throw new IOException("Missing equipment icon: " + path);
        if (imp.textureType != TextureImporterType.Sprite || imp.spriteImportMode != SpriteImportMode.Single)
        {
            imp.textureType=TextureImporterType.Sprite;
            imp.spriteImportMode=SpriteImportMode.Single;
            imp.alphaIsTransparency=true;
            imp.mipmapEnabled=false;
            imp.filterMode=FilterMode.Bilinear;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    private static void MakeDemoPickup(string name,MS3AItemData item)
    {
        string path=Demo+"/"+name+".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path)) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SourceDemo))
        {
            if (!AssetDatabase.CopyAsset(SourceDemo,path)) throw new IOException("Could not clone pickup prefab: "+path);
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var pickup=root.GetComponent<MS3AItemPickup>();
                if (!pickup) throw new InvalidOperationException("3A pickup prefab missing MS3AItemPickup.");
                pickup.item=item; pickup.amount=1;
                var world=root.GetComponent<SpriteRenderer>();
                if (world) world.sprite=item.icon;
                var smaller=root.transform.Find("MS3B1_SmallerWorldVisual");
                if (smaller)
                {
                    var image=smaller.GetComponent<SpriteRenderer>();
                    if (image) image.sprite=item.icon;
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        else
        {
            var go=new GameObject(name);
            try
            {
                var image=go.AddComponent<SpriteRenderer>(); image.sprite=item.icon; image.sortingOrder=10;
                var body=go.AddComponent<Rigidbody2D>();body.bodyType=RigidbodyType2D.Kinematic;body.gravityScale=0;
                var trigger=go.AddComponent<CircleCollider2D>(); trigger.isTrigger=true;trigger.radius=.8f;
                var pickup=go.AddComponent<MS3AItemPickup>();pickup.item=item;pickup.amount=1;
                // Visual-only small size; pickup collider remains large enough for easy collection.
                var child=new GameObject("MS3B1_SmallerWorldVisual");
                child.transform.SetParent(go.transform,false);
                child.transform.localScale=Vector3.one*.15f;
                var display=child.AddComponent<SpriteRenderer>();display.sprite=item.icon;display.sortingOrder=11;
                image.enabled=false;
                PrefabUtility.SaveAsPrefabAsset(go,path);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }

    // Optional: synchronize equipment demo quality with 3C if that catalog exists.
    // SerializedObject avoids hard compile dependency on the 3C package.
    private static void TryRegister3CRarity(MS3AItemData item,int rarityValue)
    {
        var asset=AssetDatabase.LoadMainAssetAtPath(RarityPath);
        if (!asset) return;
        var serialized=new SerializedObject(asset);
        var list=serialized.FindProperty("items");
        if (list==null || !list.isArray) return;
        for (int i=0;i<list.arraySize;i++)
        {
            var cell=list.GetArrayElementAtIndex(i);
            if (cell.FindPropertyRelative("item").objectReferenceValue == item) return;
        }
        int n=list.arraySize; list.InsertArrayElementAtIndex(n);
        var entry=list.GetArrayElementAtIndex(n);
        entry.FindPropertyRelative("item").objectReferenceValue=item;
        entry.FindPropertyRelative("rarity").enumValueIndex=rarityValue;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(asset);
    }

    private static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
    {
        var obj=new GameObject(name,typeof(RectTransform)); obj.layer=5;
        Undo.RegisterCreatedObjectUndo(obj,"3D1 UI");
        obj.transform.SetParent(parent,false);
        var rt=obj.GetComponent<RectTransform>();
        rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=rt.offsetMax=Vector2.zero;
        return rt;
    }
    private static RectTransform Panel(string name,Transform parent,Vector2 min,Vector2 max,Color color)
    {
        var rect=Rect(name,parent,min,max);
        var image=rect.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;
        return rect;
    }
    private static TMP_Text Label(string text,Transform parent,string name,int size,Color tint,Vector2 min,Vector2 max,TextAlignmentOptions alignment)
    {
        var rect=Rect(name,parent,min,max);
        var tmp=rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.fontSize=size;tmp.fontSizeMin=10;tmp.fontSizeMax=size;tmp.enableAutoSizing=true;
        tmp.text=text;tmp.color=tint;tmp.alignment=alignment;tmp.raycastTarget=false;
        tmp.textWrappingMode=TextWrappingModes.NoWrap;
        return tmp;
    }
    private static Button MakeButton(string name,Transform parent,string title,Vector2 min,Vector2 max,int fontSize)
    {
        var rect=Panel(name,parent,min,max,new Color(.26f,.15f,.40f,1));
        var image=rect.GetComponent<Image>();image.raycastTarget=true; // 3A.1 click safety
        var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
        var colors=button.colors;colors.normalColor=Color.white;
        colors.highlightedColor=new Color(1f,.86f,1f);
        colors.pressedColor=new Color(.72f,.57f,.82f);
        colors.disabledColor=new Color(.48f,.43f,.54f,.50f);
        colors.fadeDuration=.15f;button.colors=colors;
        Label(title,rect,"Text",fontSize,Pale,Vector2.zero,Vector2.one,TextAlignmentOptions.Center);
        return button;
    }
    private static void EnsureFolder(string full)
    {
        string[] parts=full.Split('/'); string current=parts[0];
        for(int i=1;i<parts.Length;i++)
        {
            string next=current+"/"+parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current,parts[i]);
            current=next;
        }
    }
}
#endif
