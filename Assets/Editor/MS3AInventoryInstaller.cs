#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MS3AInventoryInstaller
{
    private const string Base = "Assets/MonsterSurvival/Inventory3A";
    private const string Demo = Base + "/Demo";
    private const string Art = Base + "/Art";
    private const string RootName = "MS3A_InventoryCanvas";
    private static readonly Color Gold = new Color(.93f,.73f,.46f,1f);
    private static readonly Color Pale = new Color(.93f,.90f,.98f,1f);
    private static readonly Color Muted = new Color(.73f,.69f,.81f,1f);

    [MenuItem("Tools/MonsterSurvival/3A Inventory/1 - INSTALL 20-slot Inventory (OPEN Scene)")]
    public static void Install()
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog("Inventory 3A", "Stop Play Mode first.", "OK"); return; }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) { Debug.LogError("[3A] Open SampleScene first."); return; }
        GameObject player = null;
        try { player = GameObject.FindGameObjectWithTag("Player"); }
        catch (UnityException) { /* Player tag missing */ }
        if (player == null || player.GetComponent<Health>() == null)
        { EditorUtility.DisplayDialog("Inventory 3A", "No active Player with tag Player and Health found. Open gameplay Scene first.", "OK"); return; }
        if (FindRoot(scene, RootName) != null)
        { EditorUtility.DisplayDialog("Inventory 3A", "Already installed in this Scene. Do not install twice.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("Install Inventory 3A", "Back up the current gameplay Scene, add a PlayerInventory component and build a hidden-by-default 20-slot UI? Existing HUD/skills/boss/menu stay untouched.", "Install", "Cancel")) return;

        BackupScene(scene);
        MS3APlayerInventory inventory = player.GetComponent<MS3APlayerInventory>();
        if (inventory == null) inventory = Undo.AddComponent<MS3APlayerInventory>(player);
        CreateSampleAssets();
        BuildCanvas(inventory);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Inventory 3A] Installed 20-slot UI. Press I in Play Mode. Drag demo pickup prefabs into Scene to test.");
        EditorUtility.DisplayDialog("Inventory 3A", "Installed. Demo item prefabs are in Assets/MonsterSurvival/Inventory3A/Demo. Drag a pickup into gameplay Scene for testing.", "OK");
    }

    [MenuItem("Tools/MonsterSurvival/3A Inventory/2 - AUDIT OPEN Scene (READ ONLY)")]
    public static void Audit()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject root = FindRoot(scene, RootName);
        var inv = UnityEngine.Object.FindFirstObjectByType<MS3APlayerInventory>(FindObjectsInactive.Include);
        var ui = root != null ? root.GetComponent<MS3AInventoryUI>() : null;
        int views = 0;
        if (ui && ui.cells != null)
            foreach (var c in ui.cells) if (c && c.button && c.frame) views++;
        Debug.Log("[Inventory 3A QA] scene=" + scene.name +
                  " | PlayerInventory=" + (inv ? "YES" : "MISSING") +
                  " | UI=" + (ui ? "YES" : "MISSING") +
                  " | cells=" + views + "/20 | EventSystem=" +
                  (UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) ? "YES" : "MISSING") +
                  " | Demo Potion=" + (AssetDatabase.LoadAssetAtPath<MS3AItemData>(Demo + "/Potion.asset") ? "YES" : "MISSING"));
    }

    [MenuItem("Tools/MonsterSurvival/3A Inventory/3 - REMOVE 3A Scene Objects (OPEN Scene)")]
    public static void Restore()
    {
        if (Application.isPlaying) return;
        Scene scene = SceneManager.GetActiveScene();
        if (!EditorUtility.DisplayDialog("Remove Inventory 3A", "Remove only MS3A_InventoryCanvas and MS3APlayerInventory from Player in this Scene? Demo assets will remain; gameplay scripts are untouched.", "Remove", "Cancel")) return;
        var uiRoot = FindRoot(scene, RootName);
        if (uiRoot) Undo.DestroyObjectImmediate(uiRoot);
        var inv = UnityEngine.Object.FindFirstObjectByType<MS3APlayerInventory>(FindObjectsInactive.Include);
        if (inv) Undo.DestroyObjectImmediate(inv);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Inventory 3A] Removed UI and component from open Scene. Demo assets left intact.");
    }

    private static void BuildCanvas(MS3APlayerInventory inventory)
    {
        GameObject canvasGo = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Inventory Canvas");
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 170;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;
        var ui = canvasGo.AddComponent<MS3AInventoryUI>();
        ui.inventory = inventory;

        RectTransform shade = Panel("InventoryPanel", canvasGo.transform, Vector2.zero, Vector2.one, new Color(.008f,.008f,.018f,.78f));
        // Entire overlay OFF at scene start; normal gameplay remains uncluttered.
        ui.panel = shade.gameObject;
        shade.GetComponent<Image>().raycastTarget = true; // block gameplay clicks while inventory is open
        var window = Panel("InventoryWindow", shade, new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Color(.055f,.05f,.105f,.985f));
        window.sizeDelta = new Vector2(660, 670);
        window.anchoredPosition = Vector2.zero;
        var outline = window.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(.58f,.36f,.77f,.80f);
        outline.effectDistance = new Vector2(2,-2);
        Label("INVENTORY", window, "InventoryTitle", 38, Gold, new Vector2(.06f,.865f), new Vector2(.84f,.955f), TextAlignmentOptions.Left);
        Label("20 SLOTS  /  PRESS I TO TOGGLE", window, "InventorySubtitle", 17, Muted, new Vector2(.06f,.82f), new Vector2(.94f,.88f), TextAlignmentOptions.Left);
        var close = MakeButton("Close", window, "X", new Vector2(.86f,.865f),new Vector2(.95f,.955f), 26);
        ui.closeButton = close;

        var grid = Rect("InventoryGrid", window, new Vector2(.055f,.245f), new Vector2(.945f,.805f));
        var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(100,82);
        layout.spacing = new Vector2(12,8);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 5;
        ui.cells = new MS3AInventorySlotUI[MS3APlayerInventory.Capacity];
        for (int i=0; i<MS3APlayerInventory.Capacity; i++)
        {
            var cellBg = Panel("Slot_"+(i+1).ToString("00"), grid, Vector2.zero,Vector2.one,new Color(.13f,.12f,.21f,.97f));
            var click = cellBg.gameObject.AddComponent<Button>();
            click.targetGraphic = cellBg.GetComponent<Image>();
            var slot = cellBg.gameObject.AddComponent<MS3AInventorySlotUI>();
            slot.button=click;
            slot.frame=cellBg.GetComponent<Image>();
            var iconRect = Rect("Icon",cellBg,new Vector2(.11f,.12f),new Vector2(.89f,.91f));
            var icon=iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = false;
            slot.icon=icon;
            slot.quantityText=Label("",cellBg,"Count",16,Pale,new Vector2(.58f,.02f),new Vector2(.95f,.32f),TextAlignmentOptions.Right);
            ui.cells[i]=slot;
        }
        var line=Panel("DetailsLine",window,new Vector2(.055f,.232f),new Vector2(.945f,.235f),new Color(.52f,.37f,.67f,1));
        line.GetComponent<Image>().raycastTarget=false;
        ui.detailName=Label("SELECT AN ITEM",window,"ItemName",23,Gold,new Vector2(.065f,.18f),new Vector2(.76f,.24f),TextAlignmentOptions.Left);
        ui.detailDescription=Label("Pick up items in the world. Press I to close.",window,"ItemDescription",17,Muted,new Vector2(.065f,.068f),new Vector2(.67f,.18f),TextAlignmentOptions.TopLeft);
        ui.detailDescription.textWrappingMode = TextWrappingModes.Normal;
        ui.useButton=MakeButton("UseButton",window,"USE",new Vector2(.73f,.07f),new Vector2(.94f,.17f),22);
        shade.gameObject.SetActive(false);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin=min; rt.anchorMax=max; rt.offsetMin=Vector2.zero; rt.offsetMax=Vector2.zero;
        return rt;
    }
    private static RectTransform Panel(string name, Transform parent,Vector2 min,Vector2 max,Color color)
    {
        var rt=Rect(name,parent,min,max);
        var img=rt.gameObject.AddComponent<Image>();
        img.color=color;
        img.raycastTarget=false;
        return rt;
    }
    private static TMP_Text Label(string message,Transform parent,string name,int size,Color col,Vector2 min,Vector2 max,TextAlignmentOptions align)
    {
        var rt=Rect(name,parent,min,max);
        var t=rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text=message;
        t.fontSize=size;
        t.color=col;
        t.alignment=align;
        t.textWrappingMode=TextWrappingModes.NoWrap;
        t.raycastTarget=false;
        t.enableAutoSizing=true;
        t.fontSizeMin=12;
        t.fontSizeMax=size;
        return t;
    }
    private static Button MakeButton(string name,Transform parent,string label,Vector2 min,Vector2 max,int fontSize)
    {
        var rt=Panel(name,parent,min,max,new Color(.30f,.18f,.44f,.98f));
        var img=rt.GetComponent<Image>();
        img.raycastTarget=true;
        var b=rt.gameObject.AddComponent<Button>();
        b.targetGraphic=img;
        var cb=b.colors;
        cb.normalColor=Color.white;
        cb.highlightedColor=new Color(1f,.87f,1f,1f);
        cb.pressedColor=new Color(.75f,.62f,.86f,1);
        b.colors=cb;
        Label(label,rt,"Label",fontSize,Pale,Vector2.zero,Vector2.one,TextAlignmentOptions.Center);
        return b;
    }

    private static void CreateSampleAssets()
    {
        Sprite pot = ImportIcon(Art + "/MS3A_Potion.png");
        Sprite crystal = ImportIcon(Art + "/MS3A_Crystal.png");
        var potion=AssetDatabase.LoadAssetAtPath<MS3AItemData>(Demo+"/Potion.asset");
        if (!potion)
        {
            potion=ScriptableObject.CreateInstance<MS3AItemData>();
            potion.itemId="heal_potion_small"; potion.displayName="HEALING POTION";
            potion.description="Restores 25 HP. Cannot be used at full health.";
            potion.icon=pot; potion.itemKind=MS3AItemKind.HealConsumable;
            potion.healAmount=25f; potion.maxStack=10;
            AssetDatabase.CreateAsset(potion,Demo+"/Potion.asset");
        }
        var ore=AssetDatabase.LoadAssetAtPath<MS3AItemData>(Demo+"/Crystal.asset");
        if (!ore)
        {
            ore=ScriptableObject.CreateInstance<MS3AItemData>();
            ore.itemId="violet_crystal";ore.displayName="VIOLET CRYSTAL";
            ore.description="A rare material. Keep it for future crafting features.";
            ore.icon=crystal; ore.itemKind=MS3AItemKind.Material;ore.maxStack=20;
            AssetDatabase.CreateAsset(ore,Demo+"/Crystal.asset");
        }
        CreatePickupPrefab("Pickup_Potion",potion,pot,2);
        CreatePickupPrefab("Pickup_Crystal",ore,crystal,5);
        AssetDatabase.SaveAssets();
    }
    private static Sprite ImportIcon(string path)
    {
        var ti=AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti==null) throw new Exception("Missing icon: "+path);
        if (ti.textureType!=TextureImporterType.Sprite || ti.spriteImportMode!=SpriteImportMode.Single)
        {
            ti.textureType=TextureImporterType.Sprite;
            ti.spriteImportMode=SpriteImportMode.Single;
            ti.alphaIsTransparency=true;
            ti.mipmapEnabled=false;
            ti.filterMode=FilterMode.Bilinear;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    private static void CreatePickupPrefab(string name,MS3AItemData item,Sprite icon,int count)
    {
        string path=Demo+"/"+name+".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path)) return;
        var go = new GameObject(name);
        try
        {
            var sp = go.AddComponent<SpriteRenderer>();
            sp.sprite=icon;
            sp.sortingOrder=10;
            go.transform.localScale=new Vector3(.35f,.35f,1f);
            var rb=go.AddComponent<Rigidbody2D>();
            rb.bodyType=RigidbodyType2D.Kinematic;
            rb.gravityScale=0;
            var collider=go.AddComponent<CircleCollider2D>();
            collider.isTrigger=true;
            collider.radius=.85f;
            var pickup=go.AddComponent<MS3AItemPickup>();
            pickup.item=item;
            pickup.amount=count;
            PrefabUtility.SaveAsPrefabAsset(go,path);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
    private static void BackupScene(Scene scene)
    {
        const string folder=Base+"/Backups";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Base,"Backups");
        var path=scene.path;
        if (string.IsNullOrEmpty(path)) return;
        string backupPath=folder+"/"+Path.GetFileNameWithoutExtension(path)+"_BEFORE_3A.unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(backupPath)==null)
        {
            EditorSceneManager.SaveScene(scene);
            if (!AssetDatabase.CopyAsset(path,backupPath)) Debug.LogWarning("[Inventory 3A] Could not create automatic Scene backup. Keep your manual backup.");
        }
    }
    private static GameObject FindRoot(Scene scene,string name)
    {
        foreach (var root in scene.GetRootGameObjects()) if (root.name==name) return root;
        return null;
    }
}
#endif
