#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Builds a NEW independent UI; never deletes old menu/gameplay objects.
public static class MMV3Installer
{
    private const string Root = "Assets/MonsterSurvival/MenuV3";
    private const string Art = Root + "/Art/";
    private const string Backup = Root + "/Backups";
    private const string V3Canvas = "MMV3_Canvas";
    private const string BootstrapName = "MMV3_GameplayBootstrap";
    private static readonly Color Ink = new Color(.055f,.052f,.102f,1f);
    private static readonly Color Muted = new Color(.69f,.68f,.78f,1f);
    private static readonly Color White = new Color(.94f,.90f,.99f,1f);
    private static readonly Color Purple = new Color(.71f,.43f,.95f,1f);
    private static readonly Color Gold = new Color(.94f,.72f,.43f,1f);

    [MenuItem("Tools/MonsterSurvival/6B V3/1 - INSTALL Main Menu + Map Flow (BOTH scenes)")]
    public static void Install()
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog("Menu V3", "Stop Play Mode before installing.", "OK"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string mainPath = FindScenePath("MainMenu");
        string gamePath = FindScenePath("SampleScene");
        if (mainPath == null || gamePath == null)
        {
            EditorUtility.DisplayDialog("Menu V3", "Could not find MainMenu.unity and SampleScene.unity in Assets. See README.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Menu V3 — replace VISUALS only",
            "Back up both Scenes, disable OLD MENU UI, install NEW Menu V3 and selected-map bootstrap.\n\n" +
            "Gameplay, Boss, AudioManager, WaveManager, WaveMapSwitcher, MapBiomeRules4C will NOT be replaced. Continue?", "Install", "Cancel")) return;

        EnsureFolder(Backup);
        BackupScene(mainPath, Backup + "/MainMenu_BEFORE_V3.unity");
        BackupScene(gamePath, Backup + "/SampleScene_BEFORE_V3.unity");
        PrepareSprites();

        // Read existing 4C sprite references first: use user's own 4B preview PNGs where available.
        Scene game = EditorSceneManager.OpenScene(gamePath, OpenSceneMode.Single);
        var old4C = FindSceneComponent<MapSelectionScreen4C>(game);
        Sprite[] mapSprites = new Sprite[3];
        if (old4C)
        {
            mapSprites[0] = old4C.meadowPreview;
            mapSprites[1] = old4C.desertPreview;
            mapSprites[2] = old4C.ruinsPreview;
            // Do not delete the old component: only disable its display/startup logic.
            old4C.enabled = false;
            old4C.gameObject.SetActive(false);
        }
        else Debug.LogWarning("[Menu V3] MapSelectionScreen4C not found. New bootstrap still requires the map dependencies.");

        GameObject previousBootstrap = FindRoot(game, BootstrapName);
        if (previousBootstrap) UnityEngine.Object.DestroyImmediate(previousBootstrap);
        GameObject bootstrapGo = new GameObject(BootstrapName);
        SceneManager.MoveGameObjectToScene(bootstrapGo, game);
        MMV3GameplayBootstrap bootstrap = bootstrapGo.AddComponent<MMV3GameplayBootstrap>();
        bootstrap.waveManager = FindSceneComponent<WaveManager>(game);
        if (bootstrap.waveManager)
        {
            bootstrap.enemySpawner = bootstrap.waveManager.enemySpawner;
            bootstrap.mapSwitcher = bootstrap.waveManager.GetComponent<WaveMapSwitcher>();
            bootstrap.biomeRules = bootstrap.waveManager.GetComponent<MapBiomeRules4C>();
        }
        if (!bootstrap.waveManager || !bootstrap.enemySpawner || !bootstrap.mapSwitcher || !bootstrap.biomeRules)
            Debug.LogError("[Menu V3] MISSING gameplay dependency. See Validator; leave backups untouched.");
        EditorSceneManager.MarkSceneDirty(game);
        EditorSceneManager.SaveScene(game);

        // Build MainMenu AFTER reading the preview references.
        Scene main = EditorSceneManager.OpenScene(mainPath, OpenSceneMode.Single);
        GameObject oldV3 = FindRoot(main, V3Canvas);
        if (oldV3) UnityEngine.Object.DestroyImmediate(oldV3);
        GameObject legacy = FindRoot(main, "MainMenuCanvas");
        if (legacy)
        {
            // Original AudioManager prefab is nested inside MainMenuCanvas in this project.
            // Move it to Scene root BEFORE disabling the legacy canvas.
            foreach (AudioManager audio in legacy.GetComponentsInChildren<AudioManager>(true))
            {
                audio.transform.SetParent(null, true);
                audio.gameObject.SetActive(true);
                Debug.Log("[Menu V3] Preserved AudioManager outside disabled legacy Canvas.");
            }
            legacy.SetActive(false);
        }
        GameObject oldOverlay = FindRoot(main, "MM6B1_Canvas");
        if (oldOverlay) oldOverlay.SetActive(false);
        var oldController = FindSceneComponent<MainMenuController>(main);
        if (oldController) oldController.enabled = false;
        BuildMainMenu(main, mapSprites);
        EditorSceneManager.MarkSceneDirty(main);
        EditorSceneManager.SaveScene(main);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Menu V3] INSTALLED: both scenes, old UI disabled, map selection moved to MainMenu.");
        EditorUtility.DisplayDialog("Menu V3 installed",
            "Done. MainMenu is open. Enter Play Mode. PLAY -> select map -> START.\n" +
            "IMPORTANT: Check both scenes are enabled in Build Profiles.", "OK");
    }

    [MenuItem("Tools/MonsterSurvival/6B V3/2 - VALIDATE installed scenes (READ ONLY)")]
    public static void Validate()
    {
        string mainPath = FindScenePath("MainMenu"), gamePath = FindScenePath("SampleScene");
        if (mainPath == null || gamePath == null) { Debug.LogError("[Menu V3] Missing Scene asset."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene main = EditorSceneManager.OpenScene(mainPath, OpenSceneMode.Single);
        GameObject v3Root = FindRoot(main, V3Canvas);
        MMV3MenuController v3 = v3Root ? v3Root.GetComponentInChildren<MMV3MenuController>(true) : null;
        Debug.Log("[Menu V3 QA] MainMenu: " + (v3 ? "PASS" : "MISSING V3 controller") +
                  ", legacyCanvas=" + ActiveName(FindRoot(main,"MainMenuCanvas")) +
                  ", oldV2Canvas=" + ActiveName(FindRoot(main,"MM6B1_Canvas")));
        Scene game = EditorSceneManager.OpenScene(gamePath, OpenSceneMode.Single);
        var bp = FindSceneComponent<MMV3GameplayBootstrap>(game);
        var fourC = FindSceneComponent<MapSelectionScreen4C>(game);
        Debug.Log("[Menu V3 QA] SampleScene: bootstrap=" + (bp ? "PASS" : "MISSING") +
                  ", old4C=" + (fourC ? (fourC.isActiveAndEnabled ? "STILL RUNNING!" : "DISABLED") : "MISSING") +
                  ", waves=" + (bp && bp.waveManager ? "CONNECTED" : "MISSING") +
                  ", mapSwitcher=" + (bp && bp.mapSwitcher && bp.mapSwitcher.maps.Count >= 3 ? "3+ MAPS" : "MISSING") +
                  ", biomeRules=" + (bp && bp.biomeRules ? "CONNECTED" : "MISSING"));
        EditorSceneManager.OpenScene(mainPath, OpenSceneMode.Single);
    }

    [MenuItem("Tools/MonsterSurvival/6B V3/3 - RESTORE OLD UI states (BOTH scenes)")]
    public static void Restore()
    {
        if (Application.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!EditorUtility.DisplayDialog("Menu V3", "Disable Menu V3 and re-enable the old UI system? The V3 assets will remain in the Project.", "Restore", "Cancel")) return;
        string mainPath=FindScenePath("MainMenu"), gamePath=FindScenePath("SampleScene");
        if (mainPath == null || gamePath == null) return;
        Scene game=EditorSceneManager.OpenScene(gamePath,OpenSceneMode.Single);
        var old4C=FindSceneComponent<MapSelectionScreen4C>(game);
        if (old4C) { old4C.gameObject.SetActive(true); old4C.enabled=true; }
        var boot=FindSceneComponent<MMV3GameplayBootstrap>(game);
        if (boot) boot.enabled=false;
        EditorSceneManager.MarkSceneDirty(game); EditorSceneManager.SaveScene(game);
        Scene main=EditorSceneManager.OpenScene(mainPath,OpenSceneMode.Single);
        var root=FindRoot(main,V3Canvas); if(root)root.SetActive(false);
        var old=FindRoot(main,"MainMenuCanvas"); if(old)old.SetActive(true);
        var v2=FindRoot(main,"MM6B1_Canvas"); if(v2)v2.SetActive(false);
        var c=FindSceneComponent<MainMenuController>(main); if(c)c.enabled=true;
        EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main);
        EditorUtility.DisplayDialog("Menu V3", "Old main menu and old map selection restored. Menu V3 is disabled.", "OK");
    }

    private static void BuildMainMenu(Scene main, Sprite[] mapSprites)
    {
        GameObject go = new GameObject(V3Canvas, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(go, main);
        Canvas canvas = go.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=200;
        CanvasScaler scaler=go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080);
        scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight=.5f;
        RectTransform bg=Rect("V3_Backdrop",go.transform,Vector2.zero,Vector2.one);
        Image background=bg.gameObject.AddComponent<Image>();
        background.sprite=Load("V3_MenuBackdrop.png");background.raycastTarget=false;
        background.color=Color.white;

        // Independent controller on a full-screen active RectTransform.
        RectTransform controllerRect=Rect("MMV3_Root",go.transform,Vector2.zero,Vector2.one);
        var menu=controllerRect.gameObject.AddComponent<MMV3MenuController>();
        menu.gameplaySceneName="SampleScene";

        RectTransform home=Rect("PAGE_Home",controllerRect,Vector2.zero,Vector2.one);
        menu.homePage=home.gameObject;
        var column=Rect("Home_LeftColumn",home,new Vector2(.075f,.08f),new Vector2(.51f,.88f));
        Text("MENU / 01",column,18,Gold,new Vector2(.03f,.9f),new Vector2(.95f,.97f),TextAlignmentOptions.Left);
        Text("MONSTER",column,80,White,new Vector2(.03f,.72f),new Vector2(.98f,.90f),TextAlignmentOptions.Left);
        Text("SURVIVAL",column,80,White,new Vector2(.03f,.62f),new Vector2(.98f,.80f),TextAlignmentOptions.Left);
        Text("SURVIVE THE WAVES   /   VERSION 1.2",column,21,Muted,new Vector2(.03f,.58f),new Vector2(.99f,.67f),TextAlignmentOptions.Left);
        RectTransform buttonArea=Rect("Buttons",column,new Vector2(.03f,.06f),new Vector2(.70f,.54f));
        menu.playButton=Button("BTN_Play",buttonArea,"PLAY",30,true,0.76f,0.995f);
        menu.mapsButton=Button("BTN_Maps",buttonArea,"MAP SELECT",29,false,.52f,.745f);
        menu.settingsButton=Button("BTN_Settings",buttonArea,"SETTINGS",29,false,.28f,.505f);
        menu.quitButton=Button("BTN_Quit",buttonArea,"QUIT",29,false,.04f,.265f);
        Text("MONSTERSURVIVAL   ·   MAIN MENU V3",home,16,Muted,new Vector2(.085f,.035f),new Vector2(.48f,.1f),TextAlignmentOptions.Left);
        Text("THE SHADOWS AWAKEN",home,18,Purple,new Vector2(.68f,.078f),new Vector2(.95f,.13f),TextAlignmentOptions.Center);

        // Complete map selection lives in MAIN MENU, before loading gameplay.
        RectTransform maps=Rect("PAGE_SelectMap",controllerRect,Vector2.zero,Vector2.one);
        menu.mapsPage=maps.gameObject;
        Panel("MapBackdrop",maps,Vector2.zero,Vector2.one,new Color(.014f,.018f,.035f,.96f));
        Text("WORLD SELECTION",maps,60,White,new Vector2(.07f,.87f),new Vector2(.8f,.97f),TextAlignmentOptions.Left);
        Text("CHOOSE ONE LAND  ·  YOUR CHOICE APPLIES TO ALL FIVE WAVES",maps,23,Muted,new Vector2(.07f,.81f),new Vector2(.90f,.87f),TextAlignmentOptions.Left);
        menu.mapsBackButton=Button("BTN_BackToMenu",maps,"BACK",24,false,.89f,.965f, new Vector2(.81f,.885f),new Vector2(.94f,.956f));
        menu.mapCards=new Button[3]; menu.mapCardBorders=new Image[3];menu.mapStates=new TMP_Text[3];
        string[] mapNames={"GREEN MEADOW","SAND DUNES","ANCIENT RUINS"};
        string[] diffs={"EASY / HEALING","MEDIUM / SWARMS","HARD / SURVIVAL"};
        string[] feats={"Regenerate 2 HP every 12 seconds","20% faster spawns; +2 enemies","+8 enemies; 20% shorter breaks"};
        string[] fallbacks={"Meadow_Card.png","Desert_Card.png","AncientRuins_Card.png"};
        for(int i=0;i<3;i++)
        {
            float x0=.07f+i*.2915f, x1=x0+.273f;
            RectTransform card=Panel("MapCard_"+(i+1),maps,new Vector2(x0,.235f),new Vector2(x1,.782f),Color.white,"CardBase.png");
            var b=card.gameObject.AddComponent<Button>();menu.mapCards[i]=b;card.GetComponent<Image>().raycastTarget=true;b.targetGraphic=card.GetComponent<Image>();
            var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1f,.95f,1f);colors.pressedColor=new Color(.79f,.76f,.85f); b.colors=colors;
            Image selectedBorder=Panel("SelectionAccent",card,new Vector2(0,.98f),new Vector2(1,1),new Color(.2f,.17f,.3f,1)).GetComponent<Image>();
            selectedBorder.raycastTarget=false;menu.mapCardBorders[i]=selectedBorder;
            Image mapImage=Panel("BiomeImage",card,new Vector2(.035f,.32f),new Vector2(.965f,.93f),Color.white).GetComponent<Image>();
            mapImage.sprite=mapSprites!=null && i<mapSprites.Length && mapSprites[i]!=null?mapSprites[i]:Load(fallbacks[i]);
            mapImage.preserveAspect=false;mapImage.raycastTarget=false;
            Text(mapNames[i],card,35,White,new Vector2(.055f,.213f),new Vector2(.94f,.32f),TextAlignmentOptions.Left);
            Text(diffs[i],card,20,Gold,new Vector2(.055f,.166f),new Vector2(.94f,.226f),TextAlignmentOptions.Left);
            Text(feats[i],card,19,Muted,new Vector2(.055f,.07f),new Vector2(.94f,.162f),TextAlignmentOptions.Left);
            menu.mapStates[i]=Text("CLICK TO SELECT",card,17,Purple,new Vector2(.055f,.01f),new Vector2(.95f,.07f),TextAlignmentOptions.Left);
        }
        menu.selectedMapCaption=Text("SELECTED MAP",maps,24,White,new Vector2(.06f,.08f),new Vector2(.61f,.15f),TextAlignmentOptions.Left);
        menu.mapsStartButton=Button("BTN_StartAdventure",maps,"START ADVENTURE",27,true,.075f,.17f,new Vector2(.66f,.072f),new Vector2(.94f,.16f));
        menu.mapsPage.SetActive(false);

        RectTransform settings=Rect("PAGE_Settings",controllerRect,Vector2.zero,Vector2.one);
        menu.settingsPage=settings.gameObject;
        Panel("SettingsOverlay",settings,Vector2.zero,Vector2.one,new Color(.017f,.017f,.034f,.89f));
        RectTransform frame=Panel("SettingsPanel",settings,new Vector2(.30f,.205f),new Vector2(.70f,.795f),Color.white,"PanelDark.png");
        Text("SETTINGS",frame,57,White,new Vector2(.10f,.78f),new Vector2(.90f,.94f),TextAlignmentOptions.Center);
        Text("AUDIO  /  VOLUME",frame,21,Gold,new Vector2(.13f,.70f),new Vector2(.87f,.79f),TextAlignmentOptions.Center);
        Text("MUSIC",frame,24,White,new Vector2(.13f,.56f),new Vector2(.68f,.63f),TextAlignmentOptions.Left);
        menu.musicPercent=Text("60%",frame,24,Muted,new Vector2(.76f,.56f),new Vector2(.9f,.63f),TextAlignmentOptions.Right);
        menu.musicSlider=Slider("MusicSlider",frame,.49f);
        Text("SOUND EFFECTS",frame,24,White,new Vector2(.13f,.34f),new Vector2(.72f,.41f),TextAlignmentOptions.Left);
        menu.sfxPercent=Text("80%",frame,24,Muted,new Vector2(.76f,.34f),new Vector2(.9f,.41f),TextAlignmentOptions.Right);
        menu.sfxSlider=Slider("SFXSlider",frame,.27f);
        menu.settingsBackButton=Button("BTN_SettingsBack",frame,"BACK TO MENU",24,false,.065f,.18f,new Vector2(.23f,.06f),new Vector2(.77f,.21f));
        menu.settingsPage.SetActive(false);
        menu.footerMessage=Text("SELECT YOUR DESTINY",controllerRect,16,Muted,new Vector2(.38f,.005f),new Vector2(.62f,.035f),TextAlignmentOptions.Center);
        Debug.Log("[Menu V3] MainMenu scene built from scratch; old UI preserved but disabled.");
    }

    private static Slider Slider(string name,Transform parent,float y)
    {
        RectTransform root=Rect(name,parent,new Vector2(.14f,y),new Vector2(.87f,y+.052f));
        var s=root.gameObject.AddComponent<Slider>();
        Image back=Panel("Track",root,new Vector2(0,.25f),new Vector2(1,.75f),new Color(.16f,.14f,.25f,1)).GetComponent<Image>();
        back.raycastTarget=true;
        RectTransform fill=Rect("FillArea",root,new Vector2(.012f,.25f),new Vector2(.988f,.75f));
        Image fi=Panel("Fill",fill,Vector2.zero,Vector2.one,new Color(.59f,.35f,.86f,1)).GetComponent<Image>();fi.raycastTarget=false;
        RectTransform handle=Rect("HandleSlideArea",root,Vector2.zero,Vector2.one);
        RectTransform knob=Rect("Handle",handle,new Vector2(0,.5f),new Vector2(0,.5f));
        knob.sizeDelta=new Vector2(24,24);
        Image knobImg=knob.gameObject.AddComponent<Image>();knobImg.color=Gold;
        s.fillRect=fi.rectTransform;s.handleRect=knob;s.targetGraphic=knobImg;
        s.minValue=0f;s.maxValue=1f;s.wholeNumbers=false;
        s.direction=UnityEngine.UI.Slider.Direction.LeftToRight;
        return s;
    }

    private static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
    {
        GameObject go=new GameObject(name,typeof(RectTransform));go.layer=5;
        go.transform.SetParent(parent,false);
        RectTransform rt=go.GetComponent<RectTransform>();
        rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=rt.offsetMax=Vector2.zero;
        return rt;
    }

    private static RectTransform Panel(string name,Transform parent,Vector2 min,Vector2 max,Color tint,string spriteName=null)
    {
        RectTransform rt=Rect(name,parent,min,max);
        Image im=rt.gameObject.AddComponent<Image>();im.color=tint;
        if(!string.IsNullOrEmpty(spriteName))
        {
            im.sprite=Load(spriteName);
            if(im.sprite)im.type=Image.Type.Sliced;
        }
        im.raycastTarget=false;
        return rt;
    }

    private static TMP_Text Text(string str,Transform parent,int size,Color color,Vector2 min,Vector2 max,TextAlignmentOptions align)
    {
        RectTransform rt=Rect("TXT_"+str.Substring(0,Mathf.Min(18,str.Length)).Replace(' ','_'),parent,min,max);
        var t=rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text=str;t.fontSize=size;t.color=color;t.alignment=align;
        t.textWrappingMode=TextWrappingModes.NoWrap;
        t.enableAutoSizing=true;t.fontSizeMin=Mathf.Max(12,size/2);t.fontSizeMax=size;
        t.raycastTarget=false;
        if(TMP_Settings.defaultFontAsset)t.font=TMP_Settings.defaultFontAsset;
        return t;
    }

    private static Button Button(string name,Transform parent,string label,int size,bool primary,float y0,float y1)
    {
        return Button(name,parent,label,size,primary,y0,y1,new Vector2(0,y0),new Vector2(1,y1));
    }

    private static Button Button(string name,Transform parent,string label,int size,bool primary,float unusedY0,float unusedY1,Vector2 min,Vector2 max)
    {
        var rt=Panel(name,parent,min,max,Color.white,primary?"ButtonPrimary.png":"ButtonBase.png");
        Image image=rt.GetComponent<Image>(); image.raycastTarget=true;
        var b=rt.gameObject.AddComponent<Button>();b.targetGraphic=image;
        var colors=b.colors;
        colors.highlightedColor=new Color(1f,.88f,1f);
        colors.pressedColor=new Color(.76f,.68f,.89f);
        b.colors=colors;
        Text(label,rt,size,White,new Vector2(.065f,.07f),new Vector2(.96f,.93f),TextAlignmentOptions.Center);
        return b;
    }

    private static Sprite Load(string file)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(Art+file);
    }

    private static void PrepareSprites()
    {
        foreach(string file in new[]{"V3_MenuBackdrop.png","Meadow_Card.png","Desert_Card.png","AncientRuins_Card.png","ButtonBase.png","ButtonPrimary.png","PanelDark.png","CardBase.png"})
        {
            string path=Art+file;
            var imp=AssetImporter.GetAtPath(path) as TextureImporter;
            if(imp==null)throw new Exception("Missing V3 art: "+path);
            imp.textureType=TextureImporterType.Sprite;
            imp.spriteImportMode=SpriteImportMode.Single;
            imp.alphaIsTransparency=true;
            imp.filterMode=FilterMode.Bilinear;
            imp.mipmapEnabled=false;
            imp.textureCompression=TextureImporterCompression.Uncompressed;
            if(file.Contains("Button")||file.Contains("Panel")||file.Contains("CardBase"))
                imp.spriteBorder=new Vector4(24,24,24,24);
            imp.SaveAndReimport();
        }
    }

    private static T FindSceneComponent<T>(Scene scene) where T:Component
    {
        foreach(GameObject root in scene.GetRootGameObjects())
        {
            T c=root.GetComponentInChildren<T>(true);
            if(c)return c;
        }
        return null;
    }
    private static GameObject FindRoot(Scene scene,string name)
    {
        foreach(GameObject go in scene.GetRootGameObjects())
            if(go.name==name)return go;
        return null;
    }
    private static string ActiveName(GameObject obj) {return obj? (obj.activeSelf?"ACTIVE":"OFF") : "MISSING";}

    private static string FindScenePath(string name)
    {
        foreach(string guid in AssetDatabase.FindAssets("t:Scene "+name))
        {
            string p=AssetDatabase.GUIDToAssetPath(guid);
            if(Path.GetFileNameWithoutExtension(p).Equals(name,StringComparison.OrdinalIgnoreCase) && !p.Contains("/Backups/"))return p;
        }
        return null;
    }

    private static void BackupScene(string scene,string backup)
    {
        if(AssetDatabase.LoadAssetAtPath<SceneAsset>(backup))return; // preserve first unmodified backup
        if(!AssetDatabase.CopyAsset(scene,backup))
            throw new Exception("Cannot back up scene "+scene);
        AssetDatabase.ImportAsset(backup);
    }
    private static void EnsureFolder(string path)
    {
        string[] segments=path.Split('/');string current=segments[0];
        for(int i=1;i<segments.Length;i++)
        {
            string next=current+"/"+segments[i];
            if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,segments[i]);
            current=next;
        }
    }
}
#endif
