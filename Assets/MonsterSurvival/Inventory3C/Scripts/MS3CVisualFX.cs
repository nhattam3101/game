using UnityEngine;

// Internal tiny procedural sprites shared across every drop; no URP shader or VFX Graph needed.
internal static class MS3CVisualFX
{
    private static Sprite dot;
    public static Sprite SoftDot
    {
        get
        {
            if (dot != null) return dot;
            const int size = 48;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "MS3C_SoftDot_Runtime";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f - size / 2f) / (size / 2f);
                    float dy = (y + .5f - size / 2f) / (size / 2f);
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - r);
                    alpha = alpha * alpha * (3f - 2f * alpha);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            texture.Apply();
            dot = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
            dot.name = "MS3C_SoftDot_Runtime";
            return dot;
        }
    }

    public static SpriteRenderer CreateGlow(Transform parent, string name, Sprite sprite, Color tint, int sortingLayer, int sortingOrder)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = tint;
        sr.sortingLayerID = sortingLayer;
        sr.sortingOrder = sortingOrder;
        return sr;
    }
}
