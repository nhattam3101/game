using UnityEngine;
using UnityEngine.UI;

// V3.2 crystal overlay: an animated independent uGUI mesh.
// Add under V3_Backdrop so existing background drift/zoom also moves the crystal.
// Original V3 backdrop, game logic, map flow and input are untouched.
[RequireComponent(typeof(CanvasRenderer))]
[DisallowMultipleComponent]
public sealed class MMV32CrystalFX : MaskableGraphic
{
    [Header("Main Menu page (optional)")]
    public GameObject homePage;

    [Header("Crystal animation")]
    [Range(0f, 2f)] public float intensity = 1f;
    [Range(0f, 6f)] public float bobPixels = 1.7f;
    [Range(0f, 3f)] public float pulsePercent = 1.4f;
    [Range(0f, 5f)] public float shimmerSpeed = 1.0f;
    [Range(0f, 1f)] public float particleVisibility = 0.6f;

    private float elapsed;
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
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        elapsed += Time.unscaledDeltaTime;
        float target = homePage == null || homePage.activeInHierarchy ? 1f : 0.12f;
        visibility = Mathf.MoveTowards(visibility, target, Time.unscaledDeltaTime * 2f);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (intensity <= 0f) return;

        // Designed for the ORIGINAL V3_MenuBackdrop.png center jewel:
        // center = 74.8% width, 54.2% height; at 1920 x 1080 the gem is ~84 x 200 px.
        // The opaque outer matte stays fixed and hides the gem painted in the backdrop;
        // only the facets move inside this fixed outline, avoiding ghosted duplicates.
        float t = elapsed;
        float alpha = Mathf.Clamp01(intensity * visibility);
        float bob = Mathf.Sin(t * 1.25f) * bobPixels * intensity;
        float swell = 1f + Mathf.Sin(t * 1.72f) * pulsePercent * 0.01f * intensity;
        Vector2 mid = new Vector2(0f, bob);

        // Soft low-alpha aura. Render BEFORE fixed matte and jewel.
        for (int i = 4; i >= 1; --i)
        {
            float factor = 1f + i * 0.115f;
            float glowA = (0.008f + 0.010f * (5 - i)) * alpha * (0.75f + 0.25f * Mathf.Sin(t * 1.45f));
            Diamond(vh, mid, 44f * factor, 104f * factor,
                new Color(.66f, .25f, .97f, glowA));
        }

        // Matte diamond exactly covering the original painted center jewel,
        // leaving the surrounding gold frame and rune circle visible.
        Diamond(vh, Vector2.zero, 44.0f, 104.5f, new Color(.14f,.065f,.25f,1f));
        Diamond(vh, mid, 39f * swell, 96f * swell, new Color(.38f,.135f,.62f,alpha));

        // Animated facets make the shape appear internally refractive.
        float ww = 39f * swell;
        float hh = 96f * swell;
        Vector2 top = mid + new Vector2(0f,hh);
        Vector2 right = mid + new Vector2(ww,0f);
        Vector2 bottom = mid + new Vector2(0f,-hh);
        Vector2 left = mid + new Vector2(-ww,0f);
        float dance = Mathf.Sin(t * shimmerSpeed * 1.4f);
        Vector2 seam = mid + new Vector2(-5f + dance * 4f, 2f);
        Triangle(vh,top,left,seam,new Color(.70f,.35f,.92f,0.80f*alpha));
        Triangle(vh,top,seam,right,new Color(.43f,.16f,.79f,0.87f*alpha));
        Triangle(vh,left,bottom,seam,new Color(.39f,.11f,.70f,0.96f*alpha));
        Triangle(vh,right,seam,bottom,new Color(.28f,.09f,.50f,0.97f*alpha));
        // Short traveling gleam stays inside the diamond.
        float phase = 0.5f + 0.5f * Mathf.Sin(t * shimmerSpeed * 1.3f);
        Vector2 seamTop = mid + new Vector2(-5f + 4f*dance, 57f - phase*28f);
        Vector2 seamBottom = mid + new Vector2(-5f + 4f*dance, -45f + phase*30f);
        Stroke(vh,seamTop,seamBottom,2.7f,
            new Color(.96f,.73f,1f,(.18f + .33f * phase)*alpha));
        Stroke(vh,top,right,1.9f,new Color(.97f,.73f,1f,.65f*alpha));
        Stroke(vh,right,bottom,1.8f,new Color(.74f,.39f,1f,.42f*alpha));
        Stroke(vh,bottom,left,1.6f,new Color(.75f,.43f,1f,.45f*alpha));
        Stroke(vh,left,top,2f,new Color(1f,.82f,1f,.71f*alpha));

        // Floating tiny motes — zero physics, zero raycasts, no allocations.
        for (int i = 0; i < 7; ++i)
        {
            float a = i * 0.8976f + t * (.17f + (i%3) * .08f);
            float r = 63f + 11f * Mathf.Sin(t * .6f + i);
            Vector2 p = new Vector2(Mathf.Cos(a)*r,Mathf.Sin(a)*r*1.45f);
            float glow = (.23f + .18f * Mathf.Sin(t * 1.4f + i))*particleVisibility*alpha;
            Diamond(vh,p,(i%3==0?3.5f:2.1f),(i%3==0?5.5f:2.8f),new Color(.88f,.55f,1f,glow));
        }
    }

    private static void Diamond(VertexHelper vh,Vector2 center,float rx,float ry,Color c)
    {
        int start=vh.currentVertCount;
        Add(vh,center+Vector2.up*ry,c);
        Add(vh,center+Vector2.right*rx,c);
        Add(vh,center+Vector2.down*ry,c);
        Add(vh,center+Vector2.left*rx,c);
        vh.AddTriangle(start,start+1,start+2);
        vh.AddTriangle(start,start+2,start+3);
    }

    private static void Triangle(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Color tint)
    {
        int start=vh.currentVertCount;
        Add(vh,a,tint);Add(vh,b,tint);Add(vh,c,tint);
        vh.AddTriangle(start,start+1,start+2);
    }

    private static void Stroke(VertexHelper vh,Vector2 a,Vector2 b,float width,Color tint)
    {
        Vector2 d=b-a;
        if(d.sqrMagnitude<0.001f) return;
        Vector2 p=new Vector2(-d.y,d.x).normalized*(width*.5f);
        int idx=vh.currentVertCount;
        Add(vh,a-p,tint); Add(vh,a+p,tint);
        Add(vh,b+p,tint); Add(vh,b-p,tint);
        vh.AddTriangle(idx,idx+1,idx+2);
        vh.AddTriangle(idx,idx+2,idx+3);
    }

    private static void Add(VertexHelper vh,Vector2 point,Color c)
    {
        UIVertex v=UIVertex.simpleVert;
        v.position=point;
        v.color=c;
        v.uv0=Vector2.zero;
        vh.AddVert(v);
    }
}
