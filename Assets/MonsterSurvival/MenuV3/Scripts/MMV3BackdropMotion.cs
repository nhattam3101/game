using UnityEngine;
using UnityEngine.UI;

// Visual-only, independent of the scene/menu/gameplay transition.
// Rendered behind MMV3_Root, does not receive pointer events.
[RequireComponent(typeof(CanvasRenderer))]
[DisallowMultipleComponent]
public sealed class MMV3BackdropMotion : MaskableGraphic
{
    [Header("Existing Menu V3 References")]
    [Tooltip("Image on MMV3_Canvas/V3_Backdrop (the original V3 background)")]
    public RectTransform backdrop;
    [Tooltip("MMV3_Root/PAGE_Home - motion fades when on other pages")]
    public GameObject homePage;

    [Header("Visual motion (0 = disabled)")]
    [Range(0f, 2f)] public float intensity = 1f;
    [Range(0f, 25f)] public float driftPixels = 7f;
    [Range(0f, 3f)] public float slowZoomPercent = 1.1f;
    [Range(0f, 10f)] public float runeRotationDegreesPerSecond = 1.2f;
    [Range(0, 80)] public int moteCount = 42;
    [Range(0.1f, 2f)] public float motesSpeed = 0.55f;
    public bool animateWhenMapOrSettingsOpen = false;

    private Vector2 originalAnchoredPosition;
    private Vector3 originalScale;
    private bool positionCached;
    private float time;
    private float visibility = 1f;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        raycastTarget = false;
        CacheBackdrop();
    }

    private void CacheBackdrop()
    {
        if (positionCached || backdrop == null) return;
        originalAnchoredPosition = backdrop.anchoredPosition;
        originalScale = backdrop.localScale;
        positionCached = true;
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (!positionCached) CacheBackdrop();
        float dt = Time.unscaledDeltaTime;
        time += dt;
        bool onHome = homePage == null || homePage.activeInHierarchy;
        float target = onHome || animateWhenMapOrSettingsOpen ? 1f : 0.15f;
        visibility = Mathf.MoveTowards(visibility, target, dt * 1.5f);
        if (backdrop != null && positionCached)
        {
            float x = Mathf.Sin(time * 0.26f) * driftPixels * intensity;
            float y = Mathf.Cos(time * 0.19f) * driftPixels * 0.6f * intensity;
            backdrop.anchoredPosition = originalAnchoredPosition + new Vector2(x, y);
            float zoom = 1f + (slowZoomPercent * 0.01f * intensity) *
                (0.7f + 0.3f * Mathf.Sin(time * 0.30f));
            backdrop.localScale = new Vector3(originalScale.x * zoom,
                originalScale.y * zoom, originalScale.z);
        }
        SetVerticesDirty();
    }

    protected override void OnDisable()
    {
        RestoreBackdrop();
        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        RestoreBackdrop();
        base.OnDestroy();
    }

    private void RestoreBackdrop()
    {
        if (!positionCached || backdrop == null) return;
        backdrop.anchoredPosition = originalAnchoredPosition;
        backdrop.localScale = originalScale;
    }

    // All particles and rune accents share ONE uGUI Graphic (no scene objects per particle).
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        float w = r.width;
        float h = r.height;
        if (w < 8f || h < 8f || intensity <= 0f) return;
        float x0 = r.xMin;
        float y0 = r.yMin;
        float show = Mathf.Clamp01(visibility * intensity);
        float t = time;

        // Long diagonal, very low opacity trails; most energy on the right side.
        for (int i = 0; i < 12; ++i)
        {
            float f = Mathf.Repeat(0.05f + 0.11f * i + t * 0.0038f * motesSpeed, 1.3f);
            float baseX = x0 + w * (0.25f + f * 0.70f);
            float lean = h * 0.18f;
            Color trail = new Color(0.32f, 0.44f, 0.78f,
                (0.035f + 0.014f * Mathf.Sin(t * 0.6f + i)) * show);
            Line(vh, new Vector2(baseX, y0 - 18),
                new Vector2(baseX + lean, y0 + h + 18), 1f, trail);
        }

        // Tiny slow-moving star motes (stable pseudo-random seeds, no allocations).
        for (int i = 0; i < moteCount; ++i)
        {
            float fx = Hash(i * 19 + 3);
            float fy = Hash(i * 37 + 11);
            float mx = Mathf.Repeat(fx + t * (0.0012f + Hash(i + 91)*0.0010f)*motesSpeed, 1f);
            float my = Mathf.Repeat(fy + t * (0.0011f + Hash(i + 161)*0.0013f)*motesSpeed, 1f);
            float twinkle = 0.52f + 0.48f * Mathf.Sin(t * (0.45f + fx * 1.7f) + fy * 6.28318f);
            float opacity = (0.10f + 0.18f*twinkle) * show;
            Color c = (i%4==0) ? new Color(.89f,.67f,.97f, opacity)
                                 : new Color(.49f,.54f,.95f, opacity);
            float radius = i%7==0 ? 2.2f : 1.15f;
            Vector2 p = new Vector2(x0 + mx*w, y0 + my*h);
            Diamond(vh, p, radius, c);
        }

        // The V3 wallpaper already contains a main sigil. Add only animated
        // subtle arc/tick highlights instead of replacing the artwork.
        Vector2 center = new Vector2(x0 + w * 0.746f, y0 + h * 0.542f);
        float radiusOuter = Mathf.Min(w * 0.159f, h * 0.285f);
        float a0 = t * runeRotationDegreesPerSecond * Mathf.Deg2Rad;
        for (int i = 0; i < 18; ++i)
        {
            float angle = (i * 6.2831853f / 18f) + a0;
            float s = Mathf.Sin(angle);
            float c = Mathf.Cos(angle);
            Vector2 p1 = center + new Vector2(c,s) * (radiusOuter * 0.95f);
            Vector2 p2 = center + new Vector2(c,s) * (radiusOuter * 1.035f);
            float alpha = (0.095f + 0.065f*Mathf.Sin(t*.55f + i)) * show;
            Line(vh,p1,p2,Mathf.Max(1f,radiusOuter*.006f),new Color(.96f,.71f,.42f,alpha));
        }
        // Opposite-rotating short purple arcs, deliberately subdued.
        for (int arc = 0; arc < 3; ++arc)
        {
            float start = arc * 2.094395f - a0 * 0.65f;
            float arcRadius = radiusOuter * (arc==1 ? 0.88f : 1.075f);
            for (int n = 0; n < 15; ++n)
            {
                float a = start + n * 0.018f;
                float b = a + 0.018f;
                Vector2 pa = center + new Vector2(Mathf.Cos(a),Mathf.Sin(a)) * arcRadius;
                Vector2 pb = center + new Vector2(Mathf.Cos(b),Mathf.Sin(b)) * arcRadius;
                Line(vh,pa,pb,Mathf.Max(1f,radiusOuter*0.004f),
                    new Color(.70f,.40f,.98f,.13f*show));
            }
        }
    }

    private static float Hash(int n)
    {
        float v = Mathf.Sin(n * 78.233f + 12.9898f) * 43758.5453f;
        return v - Mathf.Floor(v);
    }

    private static void Diamond(VertexHelper vh, Vector2 p, float size, Color tint)
    {
        int index = vh.currentVertCount;
        AddVert(vh,new Vector2(p.x-size,p.y),tint);
        AddVert(vh,new Vector2(p.x,p.y+size),tint);
        AddVert(vh,new Vector2(p.x+size,p.y),tint);
        AddVert(vh,new Vector2(p.x,p.y-size),tint);
        vh.AddTriangle(index,index+1,index+2);
        vh.AddTriangle(index,index+2,index+3);
    }

    private static void Line(VertexHelper vh, Vector2 start, Vector2 end, float thickness, Color tint)
    {
        Vector2 dir = end-start;
        if (dir.sqrMagnitude < 0.01f) return;
        Vector2 perpendicular = new Vector2(-dir.y,dir.x).normalized*(thickness*.5f);
        int index = vh.currentVertCount;
        AddVert(vh,start-perpendicular,tint);
        AddVert(vh,start+perpendicular,tint);
        AddVert(vh,end+perpendicular,tint);
        AddVert(vh,end-perpendicular,tint);
        vh.AddTriangle(index,index+1,index+2);
        vh.AddTriangle(index,index+2,index+3);
    }

    private static void AddVert(VertexHelper vh, Vector2 point, Color color)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = point;
        vertex.color = color;
        vertex.uv0 = Vector2.zero;
        vh.AddVert(vertex);
    }
}
