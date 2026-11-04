using UnityEngine;

/// <summary>Visual-only, independent death sparkle. No collider, Health, Tag or rewards.</summary>
public sealed class MS7E3DeathBurst : MonoBehaviour
{
    private static Sprite whiteSprite;
    private SpriteRenderer[] bits;
    private Vector3[] velocities;
    private float lifetime;
    private float elapsed;
    private Color tint;

    public static void Spawn(Vector3 position, Color color, int count, float seconds)
    {
        GameObject root = new GameObject("MS7E3_DeathBurst_VisualOnly");
        root.transform.position = new Vector3(position.x, position.y, -0.12f);
        MS7E3DeathBurst fx = root.AddComponent<MS7E3DeathBurst>();
        fx.Begin(color, Mathf.Clamp(count, 3, 18), Mathf.Max(0.15f, seconds));
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null) return whiteSprite;
        Texture2D texture = Texture2D.whiteTexture;
        whiteSprite = Sprite.Create(texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), Mathf.Max(1f, texture.width));
        whiteSprite.name = "MS7E3_Runtime_WhiteSpark";
        whiteSprite.hideFlags = HideFlags.HideAndDontSave;
        return whiteSprite;
    }

    private void Begin(Color color, int count, float seconds)
    {
        lifetime = seconds;
        tint = color;
        bits = new SpriteRenderer[count];
        velocities = new Vector3[count];
        Sprite sprite = GetWhiteSprite();
        for (int i = 0; i < count; i++)
        {
            GameObject bit = new GameObject("Spark_" + i);
            bit.transform.SetParent(transform, false);
            bit.transform.localPosition = Vector3.zero;
            bit.transform.localScale = Vector3.one * Random.Range(0.06f, 0.13f);
            bit.transform.localEulerAngles = new Vector3(0f, 0f, 45f);
            SpriteRenderer renderer = bit.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = tint;
            renderer.sortingOrder = 70;
            bits[i] = renderer;
            float a = (Mathf.PI * 2f * i / count) + Random.Range(-0.18f, 0.18f);
            float s = Random.Range(1.0f, 2.4f);
            velocities[i] = new Vector3(Mathf.Cos(a) * s, Mathf.Sin(a) * s, 0f);
        }
        Destroy(gameObject, lifetime + 0.03f);
    }

    private void Update()
    {
        if (bits == null) return;
        elapsed += Time.deltaTime;
        float p = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, lifetime));
        for (int i = 0; i < bits.Length; i++)
        {
            if (bits[i] == null) continue;
            bits[i].transform.localPosition += velocities[i] * Time.deltaTime * (1f - 0.42f * p);
            bits[i].transform.Rotate(0f, 0f, 210f * Time.deltaTime);
            Color c = tint;
            c.a = tint.a * (1f - p) * (1f - p);
            bits[i].color = c;
        }
    }
}
