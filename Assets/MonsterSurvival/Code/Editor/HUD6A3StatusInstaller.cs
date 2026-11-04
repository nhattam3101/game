#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Stage 6A.3 - upgrade only the upper-right GameStatus under HUD6A2_Minimal.
// Does NOT edit gameplay scripts and NEVER deletes the original GameStatus.
public static class HUD6A3StatusInstaller
{
    private const string AssetRoot = "Assets/MonsterSurvival/Features/UI/HUD6A3_GameStatus";
    private const string PanelPath = AssetRoot + "/Sprites/HUD6A3_Panel.png";
    private const string GoldIconPath = AssetRoot + "/Sprites/HUD6A3_GoldIcon.png";
    private const string OldName = "GameStatus";
    private const string NewName = "GameStatus_6A3";

    [MenuItem("Tools/MonsterSurvival/6A.3 Wave Timer Gold/1 - Upgrade OPEN Scene")]
    public static void Upgrade()
    {
        if (!CanEdit()) return;
        var root = GameObject.Find("HUD6A2_Minimal");
        if (root == null || root.GetComponent<HUD6A2CompactView>() == null)
        {
            EditorUtility.DisplayDialog("HUD 6A.3", "Khong tim thay HUD6A2_Minimal va HUD6A2CompactView. Cai HUD 6A.2 FIXED truoc, va mo dung gameplay scene.", "OK");
            return;
        }
        if (root.transform.Find(NewName) != null)
        {
            EditorUtility.DisplayDialog("HUD 6A.3", "GameStatus_6A3 da ton tai, khong tao trung.", "OK");
            Selection.activeGameObject = root.transform.Find(NewName).gameObject;
            return;
        }
        var old = root.transform.Find(OldName);
        if (old == null)
        {
            EditorUtility.DisplayDialog("HUD 6A.3", "Khong tim thay GameStatus cua 6A.2. Kiem tra Hierarchy.", "OK");
            return;
        }

        ConfigureSprite(PanelPath);
        ConfigureSprite(GoldIconPath);
        var panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelPath);
        var goldSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GoldIconPath);
        if (panelSprite == null || goldSprite == null)
        {
            EditorUtility.DisplayDialog("HUD 6A.3", "Khong tim thay sprite PNG cua 6A.3. Copy day du Assets trong ZIP.", "OK");
            return;
        }

        // New right status card: 238x62 anchored to top-right, keeping center clear.
        var right = MakeRect(NewName, root.transform, Vector2.one, Vector2.one, new Vector2(-25,-22), new Vector2(238, 66));
        Undo.RegisterCreatedObjectUndo(right.gameObject, "Install HUD 6A.3");

        var background = MakeRect("Background", right, new Vector2(0,1), new Vector2(0,1), new Vector2(0,0), new Vector2(238,66));
        var bg = background.gameObject.AddComponent<Image>();
        bg.sprite = panelSprite;
        bg.raycastTarget = false;
        bg.color = Color.white;

        var accent = MakeRect("WaveAccent", right, new Vector2(0,1), new Vector2(0,1), new Vector2(14,-10), new Vector2(3,18));
        var accentImage = accent.gameObject.AddComponent<Image>();
        accentImage.color = new Color(.60f,.40f,.87f,.75f);
        accentImage.raycastTarget = false;

        var wave = MakeText("Wave", right, "WAVE 1/5", 12.5f, new Color(.85f,.76f,.95f), 24,-9,118,20, TextAlignmentOptions.Left);
        var timer = MakeText("Timer", right, "--:--", 12f, new Color(.94f,.93f,.99f), 142,-9,83,20, TextAlignmentOptions.Right);

        var divider = MakeRect("Divider", right, new Vector2(0,1), new Vector2(0,1), new Vector2(16,-32), new Vector2(206,1));
        var dividerImage = divider.gameObject.AddComponent<Image>();
        dividerImage.color = new Color(.62f,.52f,.76f,.30f);
        dividerImage.raycastTarget = false;

        var gold = MakeRect("GoldIcon", right, new Vector2(0,1), new Vector2(0,1), new Vector2(17,-40), new Vector2(13,13));
        var goldImage = gold.gameObject.AddComponent<Image>();
        goldImage.sprite = goldSprite;
        goldImage.raycastTarget = false;
        var goldText = MakeText("Gold", right, "0", 13f, new Color(.96f,.83f,.61f), 37,-36,182,19, TextAlignmentOptions.Left);

        var status = right.gameObject.AddComponent<HUD6A3GameStatusView>();
        var priorView = root.GetComponent<HUD6A2CompactView>();
        status.playerProgression = priorView.playerProgression;
        status.waveManager = priorView.waveManager;
        status.waveSourceText = priorView.existingWaveText;
        if (status.waveManager != null && status.waveSourceText == null)
            status.waveSourceText = status.waveManager.waveText;
        status.timeSourceText = FindOriginalSceneTimeText(root.scene);
        status.waveLabel = wave;
        status.timerLabel = timer;
        status.goldLabel = goldText;
        status.waveAccent = accentImage;
        status.goldIcon = goldImage;

        // The previous status remains intact but hidden (reversible).
        Undo.RecordObject(old.gameObject, "Hide old 6A.2 GameStatus");
        old.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = right.gameObject;
        EditorGUIUtility.PingObject(right.gameObject);
        EditorUtility.DisplayDialog("HUD 6A.3", "Da nang cap GameStatus. GameStatus 6A.2 cu duoc an, KHONG xoa. Luu scene Ctrl+S va vao Play Mode kiem tra. Ban can tu an TMP Text cu o Canvas neu bi trung.", "OK");
    }

    [MenuItem("Tools/MonsterSurvival/6A.3 Wave Timer Gold/2 - Restore 6A.2 OPEN Scene")]
    public static void Restore()
    {
        if (!CanEdit()) return;
        var root = GameObject.Find("HUD6A2_Minimal");
        if (root == null) return;
        var upgraded = root.transform.Find(NewName);
        var old = root.transform.Find(OldName);
        if (upgraded != null)
            Undo.DestroyObjectImmediate(upgraded.gameObject);
        if (old != null)
        {
            Undo.RecordObject(old.gameObject, "Restore 6A.2 GameStatus");
            old.gameObject.SetActive(true);
        }
        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorUtility.DisplayDialog("HUD 6A.3", "Da khoi phuc GameStatus 6A.2. Khong sua HUD player/Boss/Gameplay.", "OK");
    }

    [MenuItem("Tools/MonsterSurvival/6A.3 Wave Timer Gold/3 - Select NEW GameStatus")]
    public static void SelectNew()
    {
        var root = GameObject.Find("HUD6A2_Minimal");
        var tr = root != null ? root.transform.Find(NewName) : null;
        if (tr != null) { Selection.activeGameObject = tr.gameObject; EditorGUIUtility.PingObject(tr.gameObject); }
    }

    private static bool CanEdit()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("HUD 6A.3", "Dung Play Mode truoc khi chay Installer.", "OK");
            return false;
        }
        return true;
    }

    private static void ConfigureSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single)
            return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static TMP_Text FindOriginalSceneTimeText(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (var root in scene.GetRootGameObjects())
        {
            // Do not accidentally choose our newer HUD timer label.
            if (root.name == "HUD6A2_Minimal") continue;
            if (root.GetComponent<Canvas>() == null) continue;
            var original = root.transform.Find("Time");
            if (original == null) original = root.transform.Find("TimerText");
            if (original != null)
            {
                var found = original.GetComponent<TMP_Text>();
                if (found == null) found = original.GetComponentInChildren<TMP_Text>(true);
                if (found != null) return found;
            }
        }
        return null;
    }

    private static RectTransform MakeRect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        var rect = g.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static TMP_Text MakeText(string name, RectTransform parent, string content, float fontSize, Color color,
        float x, float y, float w, float h, TextAlignmentOptions alignment)
    {
        var rect = MakeRect(name, parent, new Vector2(0,1), new Vector2(0,1), new Vector2(x,y), new Vector2(w,h));
        var t = rect.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = content;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = alignment;
        t.textWrappingMode = TextWrappingModes.NoWrap; // Unity 6.6, NO obsolete enableWordWrapping.
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        return t;
    }
}
#endif
