using System;
using UnityEngine;

// Optional sample backgrounds for Stage 4A, generated at runtime.
// Put this component on EACH Map root (not on WaveManager).
// This is decorative ONLY: creates no colliders, no enemies and no hazards.
[DisallowMultipleComponent]
public class ProceduralMapBackdrop2D : MonoBehaviour
{
    public enum MapTheme { Meadow, Desert, AncientRuins }

    [Header("Visual Theme")]
    public MapTheme theme = MapTheme.Meadow;
    public Camera targetCamera;

    [Header("Background Rendering")]
    [Min(32)] public int textureSize = 256;
    [Min(1)] public int pixelsPerUnit = 64;
    [Min(0f)] public float cameraPadding = 5f;
    public int sortingOrder = -100;
    public bool followCamera = true;
    public int randomSeed = 74;

    private GameObject groundObject;
    private SpriteRenderer groundRenderer;
    private Texture2D ownedTexture;
    private Sprite ownedSprite;
    private Color32[] pixels;
    private System.Random random;

    private void OnEnable()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (groundRenderer == null)
            BuildGround();
        UpdateGroundSizeAndPosition();
    }

    private void LateUpdate()
    {
        if (followCamera)
            UpdateGroundSizeAndPosition();
    }

    private void BuildGround()
    {
        int size = Mathf.Clamp(textureSize, 32, 1024);
        random = new System.Random(randomSeed + (int)theme * 10007);
        pixels = new Color32[size * size];

        Color32 baseColor;
        if (theme == MapTheme.Meadow) baseColor = Hex("77A977");
        else if (theme == MapTheme.Desert) baseColor = Hex("CEB17B");
        else baseColor = Hex("646879");

        // Flat-colored, low-contrast block variations that fit the game's chibi style.
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int jitter = random.Next(-7, 8);
                pixels[y * size + x] = Shift(baseColor, jitter);
            }
        }

        if (theme == MapTheme.Meadow) PaintMeadow(size);
        else if (theme == MapTheme.Desert) PaintDesert(size);
        else PaintRuins(size);

        ownedTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        ownedTexture.name = "Map_Backdrop_" + theme;
        ownedTexture.filterMode = FilterMode.Point;
        ownedTexture.wrapMode = TextureWrapMode.Repeat;
        ownedTexture.SetPixels32(pixels);
        ownedTexture.Apply(false, false);

        ownedSprite = Sprite.Create(
            ownedTexture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            Mathf.Max(1, pixelsPerUnit),
            0,
            SpriteMeshType.FullRect);

        groundObject = new GameObject("GeneratedGround_DO_NOT_EDIT");
        groundObject.transform.SetParent(transform, false);
        groundRenderer = groundObject.AddComponent<SpriteRenderer>();
        groundRenderer.sprite = ownedSprite;
        groundRenderer.drawMode = SpriteDrawMode.Tiled;
        groundRenderer.sortingOrder = sortingOrder;
    }

    private void UpdateGroundSizeAndPosition()
    {
        if (groundRenderer == null || targetCamera == null)
            return;

        if (targetCamera.orthographic)
        {
            float height = 2f * targetCamera.orthographicSize + cameraPadding * 2f;
            float width = height * targetCamera.aspect + cameraPadding * 2f;
            groundRenderer.size = new Vector2(width, height);
        }
        else
        {
            groundRenderer.size = new Vector2(40f, 25f);
        }

        Vector3 center = followCamera ? targetCamera.transform.position : transform.position;
        groundObject.transform.position = new Vector3(center.x, center.y, 0f);
    }

    private void PaintMeadow(int s)
    {
        for (int i = 0; i < 85; i++)
        {
            int x = random.Next(s), y = random.Next(s);
            int radius = random.Next(2, 7);
            Color32 grass = random.Next(2) == 0 ? Hex("52895F") : Hex("9BCB85");
            Circle(s, x, y, radius, grass);
            if (i % 7 == 0)
            {
                Circle(s, x + 1, y + 1, 2, Hex("E9D67F"));
                Pixel(s, x + 1, y + 1, Hex("FFF3CD"));
            }
        }
        for (int i = 0; i < 35; i++)
        {
            int x = random.Next(s), y = random.Next(s);
            Line(s, x, y, x + 2, y + 6, Hex("3F7850"));
            Line(s, x + 2, y + 6, x + 5, y + 2, Hex("3F7850"));
        }
    }

    private void PaintDesert(int s)
    {
        for (int band = 20; band < s + 35; band += 33)
        {
            for (int x = 0; x < s - 1; x++)
            {
                int y = band + Mathf.RoundToInt(5f * Mathf.Sin(x * 0.045f + band));
                Pixel(s, x, y, Hex("E7C895"));
                Pixel(s, x, y + 1, Hex("E7C895"));
                Pixel(s, x, y - 3, Hex("B89561"));
            }
        }
        for (int i = 0; i < 40; i++)
        {
            int x = random.Next(s), y = random.Next(s);
            int r = random.Next(2, 7);
            Circle(s, x, y, r, Hex("AA8D63"));
            Circle(s, x - 1, y + 1, Mathf.Max(1, r - 2), Hex("DBC195"));
        }
    }

    private void PaintRuins(int s)
    {
        int tile = 48;
        Color32 seam = Hex("454A58");
        for (int x = 0; x < s; x += tile)
            for (int y = 0; y < s; y++) Pixel(s, x, y, seam);
        for (int y = 0; y < s; y += tile)
            for (int x = 0; x < s; x++) Pixel(s, x, y, seam);

        for (int i = 0; i < 45; i++)
        {
            int x = random.Next(s), y = random.Next(s);
            Line(s, x, y, x + random.Next(-7, 8), y + random.Next(2, 11), Hex("4D5360"));
            if (i % 6 == 0) Circle(s, x + 3, y + 2, 3, Hex("8B82AB"));
        }
    }

    private void Pixel(int s, int x, int y, Color32 c)
    {
        // Wrapped coordinates make edge decoration seamless with tiled rendering.
        x = ((x % s) + s) % s;
        y = ((y % s) + s) % s;
        pixels[y * s + x] = c;
    }

    private void Circle(int s, int cx, int cy, int r, Color32 c)
    {
        for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
                if (x * x + y * y <= r * r) Pixel(s, cx + x, cy + y, c);
    }

    private void Line(int s, int x0, int y0, int x1, int y1, Color32 c)
    {
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        while (true)
        {
            Pixel(s, x0, y0, c);
            if (x0 == x1 && y0 == y1) break;
            int e2 = err * 2;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    private static Color32 Hex(string hex)
    {
        Color c;
        if (!ColorUtility.TryParseHtmlString("#" + hex, out c)) c = Color.white;
        return c;
    }

    private static Color32 Shift(Color32 c, int amount)
    {
        return new Color32(
            (byte)Mathf.Clamp(c.r + amount, 0, 255),
            (byte)Mathf.Clamp(c.g + amount, 0, 255),
            (byte)Mathf.Clamp(c.b + amount, 0, 255), 255);
    }

    private void OnDestroy()
    {
        if (ownedSprite != null) Destroy(ownedSprite);
        if (ownedTexture != null) Destroy(ownedTexture);
    }
}
