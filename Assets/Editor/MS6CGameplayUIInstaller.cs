#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Unity 6.6 - Cosmetic-only; all existing Button.onClick, SkillManager, PauseMenuManager,
// GameManager, AudioManager, Boss and HUD scripts remain on their existing objects.
public static class MS6CGameplayUIInstaller
{
    private const string Root = "Assets/MonsterSurvival/UI6C";
    private const string Art = Root + "/Art/";
    private const string BackupPath = Root + "/Backups/SampleScene_BEFORE_6C.unity";
    private static readonly Color Light = new Color(.94f, .90f, .99f, 1f);
    private static readonly Color Muted = new Color(.72f, .67f, .82f, 1f);
    private static readonly Color Gold = new Color(.95f, .73f, .45f, 1f);
    private static readonly Color Dim = new Color(.035f, .027f, .058f, .79f);

    [MenuItem("Tools/MonsterSurvival/6C Gameplay UI/1 - AUDIT OPEN Scene (READ ONLY)")]
    public static void Audit()
    {
        var scene = SceneManager.GetActiveScene();
        var canvas = FindCanvas(scene);
        if (!canvas) { Debug.LogError("[6C] No Canvas in the open scene."); return; }
        var pause = FindDirectChild(canvas.transform, "PausePanel");
        var skill = FindDirectChild(canvas.transform, "SkillPanel");
        var victory = FindDirectChild(canvas.transform, "VictoryPanel");
        var defeat = FindDirectChild(canvas.transform, "GameOverPanel");
        Debug.Log("[6C QA] Scene=" + scene.name +
            " | Pause=" + State(pause) + " | Skills=" + State(skill) +
            " | Victory=" + State(victory) + " | GameOver=" + State(defeat) +
            " | Theme=" + (canvas.GetComponent<MS6CStyleSnapshot>() ? "INSTALLED" : "NOT INSTALLED") +
            ". ACTIVE in EDIT MODE is not proof of a gameplay bug.");
        if (!pause || !skill || !victory || !defeat)
            Debug.LogWarning("[6C] Some expected panels are missing; installer will skip missing panels.");
    }

    [MenuItem("Tools/MonsterSurvival/6C Gameplay UI/2 - APPLY V2 THEME to OPEN Scene")]
    public static void Install()
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog("6C", "Stop Play Mode first.", "OK"); return; }
        var scene = SceneManager.GetActiveScene();
        var canvas = FindCanvas(scene);
        if (!canvas) { EditorUtility.DisplayDialog("6C", "Open SampleScene: Canvas not found.", "OK"); return; }
        if (canvas.GetComponent<MS6CStyleSnapshot>())
        {
            EditorUtility.DisplayDialog("6C", "Already installed. Use RESTORE before re-applying to avoid stacking.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("6C - Skin existing UI only",
            "Back up SampleScene; recolor/restyle existing Pause, Skill, Victory, GameOver. " +
            "Set their default Scene visibility OFF. No gameplay code will be replaced. Continue?", "Apply", "Cancel")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        BackupScene(scene);
        PrepareArt();
        var snapshot = Undo.AddComponent<MS6CStyleSnapshot>(canvas.gameObject);
        Undo.RecordObject(snapshot, "6C Save UI visual state");
        var pause = FindDirectChild(canvas.transform, "PausePanel");
        var skill = FindDirectChild(canvas.transform, "SkillPanel");
        var victory = FindDirectChild(canvas.transform, "VictoryPanel");
        var defeat = FindDirectChild(canvas.transform, "GameOverPanel");
        var panel = Load("Panel6C.png");
        var regular = Load("Button6C.png");
        var primary = Load("ButtonPrimary6C.png");
        var skillCard = Load("SkillCard6C.png");
        var resultCard = Load("ResultsCard6C.png");

        if (pause)
        {
            StyleOverlay(pause, snapshot);
            var window = FindDirectChild(pause.transform, "PauseWindow");
            if (window)
            {
                StyleImage(window.GetComponent<Image>(), panel, Color.white, snapshot);
                if (window.TryGetComponent<RectTransform>(out var rect))
                {
                    SaveRect(snapshot, rect);
                    Undo.RecordObject(rect, "6C Resize Pause");
                    rect.sizeDelta = new Vector2(540f, 610f);
                }
                StyleTexts(window.transform, snapshot, false);
                StyleButtons(window.transform, regular, primary, snapshot);
                // Do not modify any OnClick listeners.
            }
            AddEntrance(pause, snapshot);
        }
        if (skill)
        {
            StyleOverlay(skill, snapshot);
            var cards = FindDirectChild(skill.transform, "SkillCardsContainer");
            if (cards)
            {
                foreach (var btn in cards.GetComponentsInChildren<Button>(true))
                {
                    StyleImage(btn.GetComponent<Image>(), skillCard, Color.white, snapshot);
                    AddHover(btn, snapshot);
                }
                // Keep runtime SkillManager rarity colors and skill descriptions untouched.
            }
            AddEntrance(skill, snapshot);
        }
        if (victory) StyleResults(victory, "VICTORY", "THE WAVES ARE YOURS", resultCard, regular, primary, snapshot);
        if (defeat) StyleResults(defeat, "GAME OVER", "RISE AND TRY AGAIN", resultCard, regular, primary, snapshot);
        foreach (GameObject p in new [] {pause, skill, victory, defeat})
        {
            if (!p) continue;
            SaveActive(snapshot, p);
            Undo.RecordObject(p, "6C Default panel off");
            p.SetActive(false);
        }
        // Styles runtime-instantiated SettingsWindow while leaving all audio references intact.
        var dyn = Undo.AddComponent<MS6CRuntimeSettingsSkin>(canvas.gameObject);
        dyn.panelSprite = panel;
        dyn.buttonSprite = regular;
        snapshot.generatedComponents.Add(dyn);

        // Visual conflict guard: never touches GameObject active flags or gameplay state.
        var guard = Undo.AddComponent<MS6CModalCoordinator>(canvas.gameObject);
        guard.pausePanel = pause;
        guard.skillPanel = skill;
        guard.victoryPanel = victory;
        guard.gameOverPanel = defeat;
        var pauseWindow = pause ? FindDirectChild(pause.transform, "PauseWindow") : null;
        guard.pauseWindow = pauseWindow ? pauseWindow.GetComponent<RectTransform>() : null;
        snapshot.generatedComponents.Add(guard);

        EditorUtility.SetDirty(snapshot);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[6C] Installed theme in " + scene.name + ". Gameplay scripts and existing button listeners untouched.");
        EditorUtility.DisplayDialog("6C installed", "The UI theme was applied. Pause/Skill/Victory/GameOver start hidden. Open Play Mode and test each flow.", "OK");
    }

    [MenuItem("Tools/MonsterSurvival/6C Gameplay UI/3 - RESTORE PREVIOUS UI (OPEN Scene)")]
    public static void Restore()
    {
        if (Application.isPlaying) return;
        var canvas = FindCanvas(SceneManager.GetActiveScene());
        var s = canvas ? canvas.GetComponent<MS6CStyleSnapshot>() : null;
        if (!s) { EditorUtility.DisplayDialog("6C", "No 6C snapshot found in this Scene.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("6C Restore", "Restore image colors/sprites, fonts, layout and initial panel active states?", "Restore", "Cancel")) return;
        // Remove runtime effects before restoring buttons.
        for (int i = s.generatedComponents.Count - 1; i >= 0; --i)
            if (s.generatedComponents[i]) Undo.DestroyObjectImmediate(s.generatedComponents[i]);
        foreach (var go in s.generatedObjects)
            if (go) Undo.DestroyObjectImmediate(go);
        foreach (var o in s.buttons)
        {
            if (!o.button) continue;
            Undo.RecordObject(o.button, "Restore button");
            o.button.transition = o.transition;
            o.button.colors = o.colors;
        }
        foreach (var o in s.images)
        {
            if (!o.image) continue;
            Undo.RecordObject(o.image, "Restore image");
            o.image.sprite = o.sprite; o.image.color = o.color;
            o.image.type = o.type; o.image.raycastTarget = o.raycastTarget;
        }
        foreach (var o in s.texts)
        {
            if (!o.text) continue;
            Undo.RecordObject(o.text, "Restore text");
            o.text.color = o.color; o.text.fontSize = o.fontSize;
        }
        foreach (var o in s.rects)
        {
            if (!o.rect) continue;
            Undo.RecordObject(o.rect, "Restore layout");
            o.rect.anchorMin = o.anchorMin; o.rect.anchorMax = o.anchorMax;
            o.rect.pivot = o.pivot; o.rect.sizeDelta = o.sizeDelta;
            o.rect.anchoredPosition = o.anchoredPosition;
        }
        foreach (var o in s.activeStates)
        {
            if (!o.gameObject) continue;
            Undo.RecordObject(o.gameObject, "Restore active flags");
            o.gameObject.SetActive(o.active);
        }
        Undo.DestroyObjectImmediate(s);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("[6C] Restored original UI styles using on-scene snapshot; all original listeners intact.");
    }

    private static void StyleOverlay(GameObject obj, MS6CStyleSnapshot s)
    {
        var im = obj.GetComponent<Image>();
        if (im) StyleImage(im, null, Dim, s);
    }
    private static void StyleResults(GameObject parent, string heading, string caption,
        Sprite panelSprite, Sprite regular, Sprite primary, MS6CStyleSnapshot s)
    {
        StyleOverlay(parent, s);
        var bg = CreateDecor(parent.transform, "MS6C_ResultCard", panelSprite, s);
        var r = bg.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = new Vector2(770f, 390f);
        bg.transform.SetAsFirstSibling();
        MakeTitle(parent.transform, heading, new Vector2(0f, 115f), 53f, Gold, s);
        MakeTitle(parent.transform, caption, new Vector2(0f, 53f), 22f, Muted, s);
        int index = 0;
        foreach (Button b in parent.GetComponentsInChildren<Button>(true))
        {
            StyleImage(b.GetComponent<Image>(), index == 0 ? primary : regular, Color.white, s);
            if (b.TryGetComponent<RectTransform>(out var br))
            {
                SaveRect(s, br);
                Undo.RecordObject(br, "6C Result Buttons");
                br.anchorMin = br.anchorMax = new Vector2(.5f,.5f);
                br.pivot = new Vector2(.5f,.5f);
                br.sizeDelta = new Vector2(295f, 72f);
                br.anchoredPosition = new Vector2(index == 0 ? -157f : 157f, -87f);
            }
            foreach (TMP_Text text in b.GetComponentsInChildren<TMP_Text>(true))
            {
                SaveText(s,text); Undo.RecordObject(text,"6C Result Text");
                text.color = Light;
                text.fontSize = 25f;
            }
            AddHover(b, s);
            index++;
        }
        AddEntrance(parent, s);
    }
    private static void StyleTexts(Transform root, MS6CStyleSnapshot s, bool skipDynamic)
    {
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (!t) continue;
            SaveText(s,t); Undo.RecordObject(t, "6C Text theme");
            if (t.name.ToLowerInvariant().Contains("title") || t.text.ToUpperInvariant().Contains("PAUSED"))
            { t.color = Gold; t.fontSize = Mathf.Max(t.fontSize, 42f); }
            else t.color = Light;
        }
    }
    private static void StyleButtons(Transform root, Sprite regular, Sprite primary, MS6CStyleSnapshot s)
    {
        foreach (var b in root.GetComponentsInChildren<Button>(true))
        {
            bool strong = b.name.ToLowerInvariant().Contains("resume") || b.name.ToLowerInvariant().Contains("restart");
            StyleImage(b.GetComponent<Image>(), strong ? primary : regular, Color.white, s);
            AddHover(b,s);
        }
    }
    private static void StyleImage(Image im, Sprite newSprite, Color color, MS6CStyleSnapshot s)
    {
        if (!im) return;
        SaveImage(s,im);
        Undo.RecordObject(im, "6C Theme Image");
        im.sprite = newSprite;
        im.color = color;
        if (newSprite) im.type = Image.Type.Sliced;
    }
    private static void AddHover(Button b, MS6CStyleSnapshot s)
    {
        if (!b || b.GetComponent<MS6CButtonMotion>()) return;
        s.buttons.Add(new MS6CStyleSnapshot.ButtonState {button=b,transition=b.transition,colors=b.colors});
        Undo.RecordObject(b, "6C Button hover");
        b.transition = Selectable.Transition.None;
        var motion = Undo.AddComponent<MS6CButtonMotion>(b.gameObject);
        s.generatedComponents.Add(motion);
    }
    private static void AddEntrance(GameObject g, MS6CStyleSnapshot s)
    {
        if (!g || g.GetComponent<MS6CPanelEntrance>()) return;
        var canvasGroup = g.GetComponent<CanvasGroup>();
        if (!canvasGroup) { canvasGroup=Undo.AddComponent<CanvasGroup>(g); s.generatedComponents.Add(canvasGroup); }
        var fx = Undo.AddComponent<MS6CPanelEntrance>(g);
        s.generatedComponents.Add(fx);
    }
    private static GameObject CreateDecor(Transform parent, string name, Sprite sprite, MS6CStyleSnapshot s)
    {
        GameObject g = new GameObject(name, typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(g, "Create 6C decor");
        g.layer = 5;
        g.transform.SetParent(parent,false);
        var image = g.GetComponent<Image>();
        image.sprite = sprite; image.type = Image.Type.Sliced;
        image.raycastTarget = false;
        s.generatedObjects.Add(g);
        return g;
    }
    private static void MakeTitle(Transform parent, string label, Vector2 at, float size, Color color, MS6CStyleSnapshot s)
    {
        GameObject g=new GameObject("MS6C_"+label.Replace(" ",""),typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(g,"Create 6C title");
        g.layer=5;g.transform.SetParent(parent,false);
        var rt=g.GetComponent<RectTransform>();
        rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);
        rt.anchoredPosition=at;rt.sizeDelta=new Vector2(680f,72f);
        var t=g.AddComponent<TextMeshProUGUI>();
        t.text=label;t.fontSize=size;t.color=color;t.alignment=TextAlignmentOptions.Center;
        t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.NoWrap;
        s.generatedObjects.Add(g);
    }
    private static void SaveImage(MS6CStyleSnapshot s, Image im)
    {
        foreach(var o in s.images) if(o.image==im)return;
        s.images.Add(new MS6CStyleSnapshot.ImageState {image=im,sprite=im.sprite,color=im.color,type=im.type,raycastTarget=im.raycastTarget});
    }
    private static void SaveText(MS6CStyleSnapshot s, TMP_Text text)
    {
        foreach(var o in s.texts) if(o.text==text)return;
        s.texts.Add(new MS6CStyleSnapshot.TextState {text=text,color=text.color,fontSize=text.fontSize});
    }
    private static void SaveRect(MS6CStyleSnapshot s, RectTransform r)
    {
        foreach(var o in s.rects) if(o.rect==r)return;
        s.rects.Add(new MS6CStyleSnapshot.RectState { rect=r,anchorMin=r.anchorMin,anchorMax=r.anchorMax,
            anchoredPosition=r.anchoredPosition,sizeDelta=r.sizeDelta,pivot=r.pivot});
    }
    private static void SaveActive(MS6CStyleSnapshot s, GameObject go)
    {
        foreach(var o in s.activeStates) if(o.gameObject==go)return;
        s.activeStates.Add(new MS6CStyleSnapshot.ActiveState {gameObject=go,active=go.activeSelf});
    }
    private static GameObject FindCanvas(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "Canvas" && root.GetComponent<Canvas>()) return root;
        }
        return null;
    }
    private static GameObject FindDirectChild(Transform p, string n)
    {
        if(!p)return null;
        var t=p.Find(n);
        return t?t.gameObject:null;
    }
    private static string State(GameObject g) { return g ? (g.activeSelf?"ACTIVE":"OFF") : "MISSING"; }
    private static Sprite Load(string filename) { return AssetDatabase.LoadAssetAtPath<Sprite>(Art+filename); }
    private static void PrepareArt()
    {
        foreach (var name in new[]{"Panel6C.png","Button6C.png","ButtonPrimary6C.png","SkillCard6C.png","ResultsCard6C.png"})
        {
            string path=Art+name;
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null) throw new Exception("Missing art: "+path);
            importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single;
            importer.alphaIsTransparency=true;
            importer.mipmapEnabled=false;
            importer.filterMode=FilterMode.Bilinear;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.spriteBorder=new Vector4(25,25,25,25);
            importer.SaveAndReimport();
        }
    }
    private static void BackupScene(Scene scene)
    {
        if (scene.name!="SampleScene") Debug.LogWarning("[6C] Open scene isn't SampleScene. UI must have expected hierarchy.");
        string scenePath=scene.path;
        if (string.IsNullOrEmpty(scenePath)) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BackupPath)) return;
        EnsureFolder("Assets/MonsterSurvival/UI6C/Backups");
        if(!AssetDatabase.CopyAsset(scenePath,BackupPath))
            Debug.LogWarning("[6C] Scene backup copy failed! Make a manual copy before editing.");
    }
    private static void EnsureFolder(string path)
    {
        string[] parts=path.Split('/');string current=parts[0];
        for(int i=1;i<parts.Length;i++)
        {
            string next=current+"/"+parts[i];
            if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);
            current=next;
        }
    }
}
#endif
