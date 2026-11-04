#if UNITY_EDITOR
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MonsterSurvival.Menu6B1;

public static class MM6B1Installer
{
    const string RootName = "MM6B1_Canvas";
    const string Base = "Assets/MonsterSurvival/Features/UI/MM6B1_MainMenuV2/Art/";

    [MenuItem("Tools/MonsterSurvival/6B.1 Main Menu v2/1 - Create in OPEN Main Menu Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("6B.1", "Stop Play Mode before installing.", "OK");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("6B.1", "Open your Main Menu scene first.", "OK");
            return;
        }
        if (GameObject.Find(RootName) != null)
        {
            EditorUtility.DisplayDialog("6B.1", "MM6B1_Canvas already exists. No changes made. Remove it before rebuilding.", "OK");
            return;
        }
        bool looksLikeGameplay = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Any(x => x != null && x.gameObject.scene == scene && (x.GetType().Name == "WaveManager" || x.GetType().Name == "EnemySpawner"));
        if (looksLikeGameplay)
        {
            EditorUtility.DisplayDialog("6B.1", "This scene contains WaveManager / EnemySpawner. Open the real Main Menu scene. Installer will not modify a gameplay scene.", "OK");
            return;
        }
        Button[] existingButtons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(x => x.gameObject.scene == scene).ToArray();
        Button oldPlay = FindButton(existingButtons, "play", "start", "choi", "newgame");
        Button oldSettings = FindButton(existingButtons, "settings", "setting", "caidat", "options");
        Button oldMaps = FindButton(existingButtons, "map", "maps", "band o", "chonban do", "choosemap", "mapselect", "levelselect");
        Button oldQuit = FindButton(existingButtons, "quit", "exit", "thoat");
        GameObject oldSettingsPanel = FindPanel(scene, "SettingsWindow", "SettingsPanel", "SettingsMenu");
        GameObject oldMapsPanel = FindPanel(scene, "MapSelectionPanel", "MapSelectPanel", "MapSelectionScreen", "SelectMapPanel");
        if (oldPlay == null)
        {
            bool proceed = EditorUtility.DisplayDialog("6B.1 - Review", "Existing PLAY button not detected. New menu will be visual-only until you assign Old Play Button manually in the Inspector. Continue?", "Build menu", "Cancel");
            if (!proceed) return;
        }
        PrepareSprite("MM6B1_Background.png");
        PrepareSprite("MM6B1_Sigil.png");
        var font = TMP_Settings.defaultFontAsset;
        if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font == null)
        {
            EditorUtility.DisplayDialog("6B.1", "Could not find a TMP font. Import TMP Essential Resources and retry.", "OK");
            return;
        }

        GameObject canvasGo = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Main Menu v2");
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 80;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        RectTransform root = NewRect("MM6B1_Root", canvasGo.transform);
        Stretch(root);
        CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
        var background = NewImage("AtmosphereBackground", root, new Color32(15, 15, 25, 255));
        Stretch(background.rectTransform);
        background.sprite = LoadSprite("MM6B1_Background.png");
        background.type = Image.Type.Simple;
        background.raycastTarget = true; // Block clicks on old menu behind overlay.
        var shade = NewImage("LeftShade", root, new Color32(6, 8, 15, 140));
        var sr = shade.rectTransform;
        sr.anchorMin = new Vector2(0, 0);
        sr.anchorMax = new Vector2(.59f, 1);
        sr.offsetMin = Vector2.zero; sr.offsetMax = Vector2.zero;
        shade.raycastTarget = false;

        RectTransform ring = NewImage("ArcaneSigil", root, new Color32(255,255,255,155)).rectTransform;
        ring.GetComponent<Image>().sprite = LoadSprite("MM6B1_Sigil.png");
        ring.GetComponent<Image>().raycastTarget = false;
        SetRect(ring, new Vector2(.76f, .51f), new Vector2(670, 670), Vector2.zero);
        var motion = ring.gameObject.AddComponent<MM6B1Motion>();
        motion.target = ring;

        RectTransform leftLine = NewImage("LeftAccent", root, new Color32(193,139,242,175)).rectTransform;
        SetRect(leftLine, new Vector2(.087f, .635f), new Vector2(3, 330), Vector2.zero);
        RectTransform title = NewText("Title", root, font, "MONSTER SURVIVAL", 72, new Color32(240, 226, 255, 255), FontStyles.Bold, TextAlignmentOptions.Left).rectTransform;
        SetRect(title, new Vector2(.10f, .80f), new Vector2(780, 100), new Vector2(390,0));
        RectTransform sub = NewText("Subtitle", root, font, "SURVIVE THE WAVES  /  VERSION 1.2", 24, new Color32(202, 165, 246, 235), FontStyles.Normal, TextAlignmentOptions.Left).rectTransform;
        SetRect(sub, new Vector2(.10f, .737f), new Vector2(770, 42), new Vector2(385,0));

        var play = MakeMenuButton("Btn_Play", "PLAY", root, font, .592f, new Color32(133,83,187,236));
        var map = MakeMenuButton("Btn_Maps", "MAP SELECT", root, font, .499f, new Color32(35,35,51,243));
        var settings = MakeMenuButton("Btn_Settings", "SETTINGS", root, font, .406f, new Color32(35,35,51,243));
        var quit = MakeMenuButton("Btn_Quit", "QUIT", root, font, .313f, new Color32(35,35,51,243));

        RectTransform status = NewText("StatusInfo", root, font, "", 20, new Color32(222,180,122,255), FontStyles.Normal, TextAlignmentOptions.Left).rectTransform;
        SetRect(status, new Vector2(.10f, .225f), new Vector2(800, 45), new Vector2(400,0));
        RectTransform footer = NewText("Footer", root, font, "MONSTERSURVIVAL  •  MAIN MENU V2", 17, new Color32(149,139,169,255), FontStyles.Normal, TextAlignmentOptions.Left).rectTransform;
        SetRect(footer, new Vector2(.10f, .09f), new Vector2(700, 34), new Vector2(350,0));
        RectTransform caption = NewText("VisualCaption", root, font, "SHADOWS AWAKEN", 22, new Color32(196,156,241,210), FontStyles.Normal, TextAlignmentOptions.Center).rectTransform;
        SetRect(caption, new Vector2(.76f, .105f), new Vector2(600, 40), Vector2.zero);

        var view = root.gameObject.AddComponent<MM6B1View>();
        view.overlay = group;
        view.playButton = play;
        view.mapsButton = map;
        view.settingsButton = settings;
        view.quitButton = quit;
        view.statusText = status.GetComponent<TMP_Text>();
        view.oldPlayButton = oldPlay;
        view.oldSettingsButton = oldSettings;
        view.oldMapsButton = oldMaps;
        view.oldQuitButton = oldQuit;
        view.oldSettingsPanel = oldSettingsPanel;
        view.oldMapsPanel = oldMapsPanel;

        // Do not change / disable old menu objects or settings/audio components.
        Selection.activeGameObject = root.gameObject;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorUtility.DisplayDialog("6B.1 Installed", "Created MM6B1_Canvas. Old Main Menu is untouched. Check Old Play/Settings/Maps links on MM6B1_Root before testing. Save the scene (Ctrl+S).", "OK");
    }

    [MenuItem("Tools/MonsterSurvival/6B.1 Main Menu v2/2 - Validate OPEN Scene (READ ONLY)")]
    public static void Validate()
    {
        var go = GameObject.Find("MM6B1_Root");
        if (go == null) { EditorUtility.DisplayDialog("6B.1 QA", "No MM6B1_Root found in open scene.", "OK"); return; }
        var v = go.GetComponent<MM6B1View>();
        if (v == null) { EditorUtility.DisplayDialog("6B.1 QA", "MM6B1View missing!", "OK"); return; }
        var sb = new StringBuilder();
        sb.AppendLine("Main Menu V2 CHECK");
        sb.AppendLine("Play bridge: " + (v.oldPlayButton != null ? "CONNECTED" : "MISSING - assign in Inspector"));
        sb.AppendLine("Settings bridge: " + (v.oldSettingsButton != null || v.oldSettingsPanel != null ? "CONNECTED" : "MISSING"));
        sb.AppendLine("Settings panel reference: " + (v.oldSettingsPanel != null ? "FOUND" : "NOT FOUND - assign if your Settings is a separate window"));
        sb.AppendLine("Map bridge: " + (v.oldMapsButton != null || v.oldMapsPanel != null ? "CONNECTED" : "NOT CONNECTED - stage 6B.2"));
        sb.AppendLine("Quit bridge: " + (v.oldQuitButton != null ? "CONNECTED" : "FALLBACK TO Application.Quit"));
        bool hasEventSystem = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Any(es => es.gameObject.scene == SceneManager.GetActiveScene());
        sb.AppendLine("EventSystem: " + (hasEventSystem ? "FOUND" : "MISSING - create one using GameObject > UI > Event System"));
        sb.AppendLine("Old menu untouched. Gameplay scripts untouched.");
        Debug.Log(sb.ToString(), v);
        EditorUtility.DisplayDialog("6B.1 QA", sb.ToString(), "OK");
    }

    [MenuItem("Tools/MonsterSurvival/6B.1 Main Menu v2/3 - Remove V2 Overlay (OPEN Scene)")]
    public static void Remove()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var go = GameObject.Find(RootName);
        if (go == null) { EditorUtility.DisplayDialog("6B.1", "No V2 overlay found.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("6B.1 - Remove", "Delete only MM6B1_Canvas from the open scene? Original Main Menu remains untouched.", "Remove V2", "Cancel")) return;
        Undo.DestroyObjectImmediate(go);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    static string Plain(string input)
    {
        string s = (input ?? "").ToLowerInvariant().Replace('đ','d').Normalize(NormalizationForm.FormD);
        var sb=new StringBuilder();
        foreach(char c in s) if(CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c)) sb.Append(c);
        return sb.ToString();
    }
    static Button FindButton(Button[] buttons, params string[] tokens)
    {
        foreach (var b in buttons)
        {
            var t=b.GetComponentInChildren<TMP_Text>(true);
            var legacyText=b.GetComponentInChildren<Text>(true);
            string match=Plain(b.name+" "+(t != null ? t.text : (legacyText != null ? legacyText.text : "")));
            foreach (var token in tokens)
                if (match.Contains(Plain(token))) return b;
        }
        return null;
    }
    static GameObject FindPanel(Scene scene, params string[] names)
    {
        var transforms = UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach(var t in transforms)
        {
            if(t.gameObject.scene != scene) continue;
            foreach(var name in names) if(string.Equals(Plain(t.name),Plain(name),StringComparison.Ordinal)) return t.gameObject;
        }
        return null;
    }
    static void PrepareSprite(string filename)
    {
        string path = Base + filename;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("[6B.1] Sprite texture not imported: " + path);
            return;
        }
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
    static Sprite LoadSprite(string filename) { return AssetDatabase.LoadAssetAtPath<Sprite>(Base + filename); }
    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
    static Image NewImage(string name, Transform parent, Color tint)
    {
        var go=new GameObject(name, typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
        go.transform.SetParent(parent,false);
        var image=go.GetComponent<Image>(); image.color=tint;
        return image;
    }
    static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color, FontStyles style, TextAlignmentOptions alignment)
    {
        var go=new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),typeof(TextMeshProUGUI));
        go.transform.SetParent(parent,false);
        var t=go.GetComponent<TextMeshProUGUI>();
        t.font=font;
        t.text=text;
        t.fontSize=size;
        t.color=color;
        t.fontStyle=style;
        t.alignment=alignment;
        t.textWrappingMode=TextWrappingModes.NoWrap;
        t.raycastTarget=false;
        t.overflowMode=TextOverflowModes.Ellipsis;
        return t;
    }
    static Button MakeMenuButton(string name, string label, Transform parent, TMP_FontAsset font, float y, Color bg)
    {
        var image=NewImage(name,parent,bg);
        var rt=image.rectTransform;
        SetRect(rt,new Vector2(.10f,y),new Vector2(515,76),new Vector2(258,0));
        var button=image.gameObject.AddComponent<Button>();
        button.targetGraphic=image;
        var colors=button.colors;
        colors.normalColor=Color.white;
        colors.highlightedColor=new Color(1.12f,1.07f,1.20f,1f);
        colors.pressedColor=new Color(.85f,.79f,.94f,1f);
        colors.selectedColor=colors.highlightedColor;
        button.colors=colors;
        var txt=NewText("Label",rt,font,label,31,new Color32(245,236,251,255),FontStyles.Bold,TextAlignmentOptions.Left);
        Stretch(txt.rectTransform);
        txt.rectTransform.offsetMin=new Vector2(32,0);
        txt.rectTransform.offsetMax=new Vector2(-10,0);
        var stripe=NewImage("Accent",rt,new Color32(222,177,112,220)).rectTransform;
        stripe.anchorMin=new Vector2(0,0);stripe.anchorMax=new Vector2(0,1);stripe.pivot=new Vector2(0,.5f);
        stripe.anchoredPosition=Vector2.zero;
        stripe.sizeDelta=new Vector2(5,0);
        stripe.GetComponent<Image>().raycastTarget=false;
        return button;
    }
    static void Stretch(RectTransform rect)
    {
        rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
        rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;
    }
    static void SetRect(RectTransform r, Vector2 anchor, Vector2 size, Vector2 centeredOffset)
    {
        r.anchorMin=anchor;r.anchorMax=anchor;
        r.pivot=new Vector2(.5f,.5f);
        r.sizeDelta=size;
        r.anchoredPosition=centeredOffset;
    }
}
#endif
