
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class EnrageAuraAnimation : MonoBehaviour
{
    [Header("Pulse Settings")]
    public float pulseSpeed = 3f;
    public float minScale = 1.4f;
    public float maxScale = 1.6f;

    [Range(0f, 1f)]
    public float minAlpha = 0.5f;

    [Range(0f, 1f)]
    public float maxAlpha = 0.9f;

    private SpriteRenderer spriteRenderer;
    private Color originalColor;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalColor = spriteRenderer.color;
    }

    private void Update()
    {
        float pulse =
            (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;

        float scale =
            Mathf.Lerp(minScale, maxScale, pulse);

        transform.localScale =
            new Vector3(scale, scale, 1f);

        Color color = originalColor;

        color.a = Mathf.Lerp(
            minAlpha,
            maxAlpha,
            pulse
        );

        spriteRenderer.color = color;
    }
}
