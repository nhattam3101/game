using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// MonsterSurvival 7J.4 - additive, presentation-only responsive layout.
// Attach to the SAME PlayerHub object as MS7J2PlayerHubPolish.
// Does not alter host MainMenu, Save/Profile, Quest, or Endless transactions.
[DisallowMultipleComponent]
[DefaultExecutionOrder(17000)]
public sealed class MS7J4AdaptiveHubLayout : MonoBehaviour
{
    [Header("Layout limits (Unity UI reference units)")]
    [Min(600f)] public float maxContentWidth = 1580f;
    [Min(480f)] public float maxSingleColumnWidth = 940f;
    [Range(1.1f, 1.9f)] public float singleColumnBelowAspect = 1.48f;
    [Min(80f)] public float cardHeight = 136f;
    [Min(0f)] public float gap = 18f;
    [Header("Safety")]
    public bool respectScreenSafeArea = true;
    public bool verboseLog = false;

    public string Status { get; private set; } = "WAITING FOR 7J2 DASHBOARD";
    public int Columns { get; private set; }
    public float CanvasWidth { get; private set; }
    public float CanvasHeight { get; private set; }
    public float ViewportWidth { get; private set; }
    public float ContentHeight { get; private set; }
    public bool VerticalScrollRequired { get; private set; }

    private RectTransform dashboard;
    private RectTransform view;
    private RectTransform content;
    private GridLayoutGroup grid;
    private ScrollRect scroll;
    private RectTransform title;
    private RectTransform subtitle;
    private RectTransform section;
    private RectTransform badge;
    private RectTransform badgeLabel;
    private RectTransform headerRule;
    private RectTransform headerGold;
    private RectTransform footerRule;
    private RectTransform footerExit;
    private RectTransform footerStatus;
    private float nextProbe;
    private float lastW = -1, lastH = -1;
    private Rect lastSafe;
    private bool initialized;
    private bool dashboardWasVisible;

    // Pure, deterministic layout policy; used by the accompanying Editor tests.
    public static int ChooseColumns(float width, float height, float breakpoint)
    {
        if (width < 100f || height < 100f) return 1;
        return width / height < breakpoint || width < 1120f ? 1 : 2;
    }
    public static float FitWidth(float availableWidth, float maxWidth)
    {
        return Mathf.Max(220f, Mathf.Min(Mathf.Max(220f, availableWidth), maxWidth));
    }
    public static float AvailableCardWidth(float contentWidth, int columns, float spacing, float padding)
    {
        return Mathf.Max(220f, (contentWidth - padding * 2f - spacing * (columns - 1)) / Mathf.Max(1, columns));
    }

    private static RectTransform Find(Transform from, string name)
    {
        if (!from) return null;
        Transform child = from.Find(name);
        return child ? child as RectTransform : null;
    }
    private bool Bind()
    {
        dashboard = Find(transform, "MS7J2_ProfessionalDashboard");
        if (!dashboard) return false;
        view = Find(dashboard, "Seven tab navigation scroll");
        content = view ? Find(view, "MS7J2_ModuleGrid") : null;
        grid = content ? content.GetComponent<GridLayoutGroup>() : null;
        scroll = view ? view.GetComponent<ScrollRect>() : null;
        title = Find(dashboard, "PLAYER HUB");
        subtitle = Find(dashboard, "Subtitle");
        section = Find(dashboard, "Section number");
        badge = Find(dashboard, "Profile badge background");
        badgeLabel = badge ? Find(badge, "Live selected Profile") : null;
        headerRule = Find(dashboard, "Fine accent line");
        headerGold = Find(dashboard, "Header gold marker");
        footerRule = Find(dashboard, "Footer divider");
        footerExit = Find(dashboard, "Return to Main Menu");
        footerStatus = Find(dashboard, "Hub status");
        return view && content && grid && scroll && title && footerExit;
    }
    private void OnEnable()
    {
        initialized = false;
        lastW = -1f;
        lastH = -1f;
        nextProbe = 0f;
        dashboardWasVisible = false;
    }
    private void LateUpdate()
    {
        if (!Application.isPlaying || gameObject.scene.name != "PlayerHub") return;
        if (!initialized)
        {
            if (Time.unscaledTime < nextProbe) return;
            nextProbe = Time.unscaledTime + .2f;
            initialized = Bind();
            if (!initialized) return;
        }
        // The CanvasScaler exposes design-space dimensions (not Game View editor zoom).
        float w = dashboard.rect.width, h = dashboard.rect.height;
        if (w <= 1f || h <= 1f) return;
        Rect safe = respectScreenSafeArea ? Screen.safeArea : new Rect(0, 0, Screen.width, Screen.height);
        bool visible = dashboard.gameObject.activeInHierarchy;
        bool changed = Mathf.Abs(w - lastW) > 1f || Mathf.Abs(h - lastH) > 1f || safe != lastSafe || (visible && !dashboardWasVisible);
        dashboardWasVisible = visible;
        if (changed)
        {
            lastW = w; lastH = h; lastSafe = safe;
            Apply(w, h, safe);
        }
    }
    private static void Anchors(RectTransform t, float left, float bottom, float right, float top)
    {
        if (!t) return;
        t.anchorMin = new Vector2(left, bottom);
        t.anchorMax = new Vector2(right, top);
        t.pivot = new Vector2(.5f, .5f);
        t.offsetMin = Vector2.zero;
        t.offsetMax = Vector2.zero;
    }
    private static void SetFont(RectTransform rt, float size, bool noWrap)
    {
        if (!rt) return;
        TMP_Text tmp = rt.GetComponent<TMP_Text>();
        if (!tmp) return;
        tmp.fontSize = size;
        tmp.enableAutoSizing = false;
        tmp.textWrappingMode = noWrap ? TextWrappingModes.NoWrap : TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Truncate;
    }
    private void Apply(float width, float height, Rect safe)
    {
        CanvasWidth = width;
        CanvasHeight = height;
        float sx0 = 0f, sx1 = 1f, sy0 = 0f, sy1 = 1f;
        if (respectScreenSafeArea && Screen.width > 0 && Screen.height > 0)
        {
            sx0 = Mathf.Clamp01(safe.xMin / Screen.width);
            sx1 = Mathf.Clamp01(safe.xMax / Screen.width);
            sy0 = Mathf.Clamp01(safe.yMin / Screen.height);
            sy1 = Mathf.Clamp01(safe.yMax / Screen.height);
        }
        float safeWidth = width * (sx1 - sx0);
        float safeHeight = height * (sy1 - sy0);
        bool portrait = safeWidth / Mathf.Max(1f, safeHeight) < 1.1f;
        int columns = ChooseColumns(safeWidth, safeHeight, singleColumnBelowAspect);
        Columns = columns;
        float sidePadding = Mathf.Max(28f, Mathf.Min(70f, safeWidth * .052f));
        float usableWidth = Mathf.Max(320f, safeWidth - sidePadding * 2f);
        float max = columns == 1 ? maxSingleColumnWidth : maxContentWidth;
        float regionW = FitWidth(usableWidth, max);
        float start = (safeWidth - regionW) * .5f / width + sx0;
        float end = start + regionW / width;
        start = Mathf.Clamp(start, sx0, sx1);
        end = Mathf.Clamp(end, sx0, sx1);
        float left = start, right = end;
        float headerTitleTop = portrait ? .924f : .926f;
        float gridTop = portrait ? .727f : (columns == 1 ? .767f : .789f);
        float gridBottom = portrait ? .205f : (columns == 1 ? .190f : .179f);
        // Header and footer positions stay inside a safe-area-relative box.
        // Convert safe-space Y to canvas-space normalized Y.
        Func<float, float> Y = (f) => sy0 + f * (sy1 - sy0);
        Anchors(section, left, Y(.928f), Mathf.Min(right, left + .36f * (sx1-sx0)), Y(.958f));
        Anchors(title, left, Y(portrait ? .858f : .851f), portrait ? right : Mathf.Min(right, left + (right-left) * .71f), Y(headerTitleTop));
        Anchors(subtitle, left, Y(portrait ? .790f : .798f), right, Y(portrait ? .833f : .844f));
        if (portrait)
        {
            Anchors(badge, right - Mathf.Min(.28f, (right-left)*.45f), Y(.928f), right, Y(.962f));
            SetFont(title, 41f, true);
            SetFont(subtitle, 13f, true);
            SetFont(badgeLabel, 16f, true);
        }
        else
        {
            Anchors(badge, right - Mathf.Min(.19f, (right-left)*.31f), Y(.874f), right, Y(.929f));
            SetFont(title, columns == 1 ? 46f : 57f, true);
            SetFont(subtitle, columns == 1 ? 16f : 19f, true);
            SetFont(badgeLabel, columns == 1 ? 18f : 22f, true);
        }
        Anchors(headerRule, left, Y(.781f), right, Y(.783f));
        Anchors(headerGold, left, Y(.817f), left + Mathf.Min(.004f, (right-left)*.006f), Y(.926f));
        // Keep header rule ABOVE grid, including portrait layout.
        if (columns == 1) Anchors(headerRule, left, Y(gridTop + .023f), right, Y(gridTop + .025f));
        Anchors(view, left, Y(gridBottom), right, Y(gridTop));
        Anchors(footerRule, left, Y(.153f), right, Y(.155f));
        if (columns == 1)
        {
            Anchors(footerStatus, left, Y(.101f), right, Y(.144f));
            Anchors(footerExit, left, Y(.031f), Mathf.Min(right, left + Mathf.Min(.47f, (right-left) * .65f)), Y(.091f));
            SetFont(footerStatus, portrait ? 12f : 14f, true);
        }
        else
        {
            Anchors(footerExit, left, Y(.042f), left + (right-left)*.30f, Y(.128f));
            Anchors(footerStatus, left + (right-left)*.35f, Y(.044f), right, Y(.122f));
            SetFont(footerStatus, 15f, true);
        }
        RectTransform exitCaption = Find(footerExit, "Exit caption");
        SetFont(exitCaption, columns == 1 ? 18f : 21f, true);
        // Prefer a clean card grid that never exceeds the safe horizontal area.
        // ContentSizeFitter remains managed by the original 7J2 UI.
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.spacing = new Vector2(columns == 1 ? 0 : gap, gap);
        grid.padding = new RectOffset(4, 4, 4, 4);
        float cardW = AvailableCardWidth(regionW, columns, columns == 1 ? 0 : gap, 4f);
        float cardH = portrait ? 125f : (columns == 1 ? 132f : cardHeight);
        grid.cellSize = new Vector2(cardW, cardH);
        RectTransform[] children = new RectTransform[7];
        for (int i = 0; i < 7; i++)
        {
            Transform t = content.Find("Category_" + (i + 1));
            if (t) children[i] = t as RectTransform;
        }
        for (int i = 0; i < children.Length; i++)
        {
            RectTransform card = children[i];
            if (!card) continue;
            RectTransform idx = Find(card, "Index badge");
            RectTransform idxText = Find(idx, "Index number");
            RectTransform cardTitle = Find(card, "Category title");
            RectTransform cardDesc = Find(card, "Category description");
            float cTitleSize = cardW < 430f ? 19f : cardW < 640f ? 22f : 26f;
            float cDescSize = cardW < 430f ? 13f : cardW < 640f ? 15f : 17f;
            SetFont(idxText, cardW < 430f ? 22f : 26f, true);
            SetFont(cardTitle, cTitleSize, true);
            SetFont(cardDesc, cDescSize, true);
            // Maintain readable body text and avoid description overlap with title.
            Anchors(cardTitle, .185f, .485f, .975f, .83f);
            Anchors(cardDesc, .185f, .18f, .975f, .49f);
        }
        int rows = Mathf.CeilToInt(7f / columns);
        ContentHeight = rows * cardH + (rows - 1) * gap + grid.padding.top + grid.padding.bottom;
        ViewportWidth = regionW;
        VerticalScrollRequired = ContentHeight > (gridTop - gridBottom) * safeHeight + 2f;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        Canvas.ForceUpdateCanvases();
        if (scroll && scroll.content) scroll.verticalNormalizedPosition = 1f;
        Status = "RESPONSIVE ACTIVE " + columns + " COL / " + width.ToString("F0") + "x" + height.ToString("F0") +
            " / VIEW=" + regionW.ToString("F0") + " / SCROLL=" + VerticalScrollRequired;
        if (verboseLog) Debug.Log("[7J.4] " + Status, this);
    }
}
