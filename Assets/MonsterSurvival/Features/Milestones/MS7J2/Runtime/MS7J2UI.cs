using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Native UGUI + actual MainMenu TMP font; never bake words into a background image.
internal static class MS7J2UI
{
    internal static readonly Color Ink = new Color(.035f, .032f, .069f, 1f);
    internal static readonly Color Card = new Color(.115f, .085f, .175f, 1f);
    internal static readonly Color CardHover = new Color(.170f, .117f, .251f, 1f);
    internal static readonly Color CardPressed = new Color(.095f, .062f, .150f, 1f);
    internal static readonly Color Gold = new Color(.925f, .705f, .395f, 1f);
    internal static readonly Color PurpleLine = new Color(.395f, .265f, .560f, 1f);
    internal static readonly Color Pale = new Color(.923f, .881f, .970f, 1f);
    internal static readonly Color Muted = new Color(.678f, .644f, .752f, 1f);
    internal static readonly Color SoftGold = new Color(.73f, .55f, .37f, 1f);

    internal static RectTransform Rect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        if (parent != null && parent.gameObject.scene.IsValid() && parent.gameObject.scene.isLoaded && go.scene != parent.gameObject.scene)
            SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
        RectTransform t = go.GetComponent<RectTransform>();
        t.SetParent(parent, false);
        return t;
    }
    internal static void Anchors(RectTransform t, float x0, float y0, float x1, float y1)
    {
        t.anchorMin = new Vector2(x0, y0);
        t.anchorMax = new Vector2(x1, y1);
        t.pivot = new Vector2(.5f, .5f);
        t.offsetMin = Vector2.zero;
        t.offsetMax = Vector2.zero;
    }
    internal static RectTransform Fixed(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 pos)
    {
        var t = Rect(name, parent);
        t.anchorMin = t.anchorMax = anchor;
        t.pivot = new Vector2(.5f, .5f);
        t.sizeDelta = size;
        t.anchoredPosition = pos;
        return t;
    }
    internal static Image Image(RectTransform t, Color color, bool raycast = false)
    {
        Image img = t.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }
    internal static RectTransform Rule(string name, Transform parent, Color color, float x0, float y0, float x1, float y1)
    {
        var t = Rect(name, parent);
        Anchors(t, x0, y0, x1, y1);
        Image(t, color);
        return t;
    }
    internal static GameObject Label(string name, Transform parent, string value, int size, Color color,
        TextAlignmentOptions alignment, TMP_FontAsset selectedFont, float x0, float y0, float x1, float y1,
        bool bold = false)
    {
        RectTransform t = Rect(name, parent);
        Anchors(t, x0, y0, x1, y1);
        if (selectedFont != null)
        {
            TextMeshProUGUI tmp = t.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = selectedFont;
            tmp.text = value;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.enableAutoSizing = false;
            tmp.overflowMode = TextOverflowModes.Truncate; // Avoid TMP U+2026 warning with LiberationSans SDF.
            tmp.raycastTarget = false;
        }
        else
        {
            // Only used if the MainMenu TMP font is genuinely unavailable.
            Text legacy = t.gameObject.AddComponent<Text>();
            legacy.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            legacy.text = value;
            legacy.fontSize = size;
            legacy.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            legacy.color = color;
            legacy.resizeTextForBestFit = false;
            legacy.alignment = alignment == TextAlignmentOptions.Center ? TextAnchor.MiddleCenter :
                               alignment == TextAlignmentOptions.Right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            legacy.horizontalOverflow = HorizontalWrapMode.Overflow;
            legacy.verticalOverflow = VerticalWrapMode.Truncate;
            legacy.raycastTarget = false;
        }
        return t.gameObject;
    }
    internal static void SetLabel(GameObject obj, string text)
    {
        if (!obj) return;
        var tmp = obj.GetComponent<TMP_Text>();
        if (tmp) tmp.text = text;
        else { var legacy = obj.GetComponent<Text>(); if (legacy) legacy.text = text; }
    }
    internal static Canvas Canvas(string name, Transform parent, int sorting)
    {
        RectTransform rt = Rect(name, parent);
        var canvas = rt.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sorting;
        var scale = rt.gameObject.AddComponent<CanvasScaler>();
        scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scale.referenceResolution = new Vector2(1920, 1080);
        scale.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scale.matchWidthOrHeight = .5f;
        rt.gameObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }
}
