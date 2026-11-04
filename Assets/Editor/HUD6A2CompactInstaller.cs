#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Non-destructive open-scene HUD builder. Does NOT disable the old HUD.
public static class HUD6A2CompactInstaller
{
    private const string Root = "Assets/MonsterSurvival/HUD6A2_Minimal";
    private const string SpritePanel = Root + "/Sprites/HUD_ThinPanel.png";
    private const string SpriteBar = Root + "/Sprites/HUD_RoundWhite.png";

    [MenuItem("Tools/MonsterSurvival/6A.2 Compact HUD/1 - Create HUD in OPEN Scene")]
    public static void BuildSceneHUD()
    {
        var existing = GameObject.Find("HUD6A2_Minimal");
        if (existing != null)
        {
            EditorUtility.DisplayDialog("6A.2", "HUD6A2_Minimal already exists in this scene. No duplicates created.", "OK");
            Selection.activeGameObject = existing;
            return;
        }
        PrepareSprite(SpritePanel);
        PrepareSprite(SpriteBar);
        var panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePanel);
        var barSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteBar);
        if (panelSprite == null || barSprite == null)
        {
            EditorUtility.DisplayDialog("6A.2", "Missing UI sprites. Copy Assets folder from ZIP completely.", "OK");
            return;
        }

        var go = new GameObject("HUD6A2_Minimal", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Undo.RegisterCreatedObjectUndo(go, "Create 6A.2 HUD");
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var controller = go.AddComponent<HUD6A2CompactView>();
        var rootRect = go.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        // Top-left: compact 292x90 player status. No large borders or oversized UI.
        var left = Rect("PlayerStatus", go.transform, 0, 1, 0, 1, 25, -22, 292, 84);
        Graphic(left, "Panel", panelSprite, new Color(0.13f, 0.14f, 0.20f, 0.82f), 0, 0, 292, 84);
        controller.levelValue = Text(left, "Level", "LV 1", 12, new Color(0.95f, 0.83f, 0.61f), 13, -9, 70, 19, TextAlignmentOptions.Left);
        controller.healthValue = Text(left, "HealthValue", "100 / 100", 11, new Color(0.95f, 0.92f, 0.96f), 190, -10, 91, 18, TextAlignmentOptions.Right);
        Graphic(left, "HPBack", barSprite, new Color(0.17f, 0.11f, 0.19f, 1), 13, -34, 265, 11);
        controller.healthDelayedFill = Graphic(left, "HPDelay", barSprite, new Color(0.87f, 0.64f, 0.36f), 13, -34, 265, 11, true);
        controller.healthImmediateFill = Graphic(left, "HPFill", barSprite, new Color(0.89f, 0.28f, 0.41f), 13, -34, 265, 11, true);
        Graphic(left, "EXPBack", barSprite, new Color(0.18f, 0.16f, 0.27f, 1), 13, -59, 265, 5);
        controller.experienceFill = Graphic(left, "EXPFill", barSprite, new Color(0.61f, 0.43f, 0.94f), 13, -59, 265, 5, true);
        controller.experienceValue = Text(left, "EXPValue", "EXP 0 / 10", 9, new Color(0.67f, 0.63f, 0.77f), 15, -69, 263, 11, TextAlignmentOptions.Left);

        // Top-right: wave and mirrored time, plus gold beneath.
        var right = Rect("GameStatus", go.transform, 1, 1, 1, 1, -25, -22, 224, 65);
        Graphic(right, "Panel", panelSprite, new Color(0.13f, 0.14f, 0.20f, 0.72f), 0, 0, 224, 65);
        controller.waveValue = Text(right, "Wave", "WAVE 1", 13, new Color(0.89f, 0.81f, 0.98f), 13, -10, 118, 20, TextAlignmentOptions.Left);
        controller.timerValue = Text(right, "Timer", "", 13, new Color(0.90f, 0.89f, 0.94f), 130, -10, 79, 20, TextAlignmentOptions.Right);
        Text(right, "GoldIcon", "◆", 13, new Color(0.96f, 0.75f, 0.31f), 15, -39, 20, 18, TextAlignmentOptions.Left);
        controller.goldValue = Text(right, "Gold", "0", 13, new Color(0.95f, 0.87f, 0.71f), 34, -39, 175, 18, TextAlignmentOptions.Left);

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            controller.playerHealth = player.GetComponent<Health>();
            controller.playerProgression = player.GetComponent<PlayerProgression>();
        }
        controller.waveManager = Object.FindFirstObjectByType<WaveManager>();
        if (controller.waveManager != null)
            controller.existingWaveText = controller.waveManager.waveText;

        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
        EditorUtility.DisplayDialog("6A.2 Compact HUD", "Created HUD6A2_Minimal in current Scene. It does not disable old UI. Save scene (Ctrl+S), test in Play Mode, then hide ONLY duplicated old UI graphics.", "OK");
    }

    [MenuItem("Tools/MonsterSurvival/6A.2 Compact HUD/2 - Select HUD in Scene")]
    public static void SelectHUD()
    {
        var go = GameObject.Find("HUD6A2_Minimal");
        if (go == null)
        {
            EditorUtility.DisplayDialog("6A.2", "HUD not found. Run Create HUD in OPEN Scene first.", "OK");
            return;
        }
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
    }

    private static void PrepareSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }

    private static RectTransform Rect(string name, Transform parent, float ax, float ay, float px, float py, float x, float y, float w, float h)
    {
        var g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        var r = g.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(ax, ay);
        r.anchorMax = r.anchorMin;
        r.pivot = new Vector2(px, py);
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(w, h);
        return r;
    }

    private static Image Graphic(RectTransform parent, string name, Sprite sprite, Color color,
        float x, float y, float w, float h, bool fill = false)
    {
        var r = Rect(name, parent, 0, 1, 0, 1, x, y, w, h);
        var image = r.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        if (fill)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = 0;
            image.fillAmount = 1;
        }
        return image;
    }

    private static TMP_Text Text(RectTransform parent, string name, string value, float size, Color color,
        float x, float y, float w, float h, TextAlignmentOptions align)
    {
        var r = Rect(name, parent, 0, 1, 0, 1, x, y, w, h);
        var text = r.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = align;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }
}
#endif
