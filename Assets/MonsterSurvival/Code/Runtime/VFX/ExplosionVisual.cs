
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ExplosionVisual : MonoBehaviour
{
    [Header("Animation Settings")]
    [Min(0.05f)]
    public float duration = 0.35f;

    public Color explosionColor =
        new Color(1f, 0.45f, 0.1f, 0.7f);

    private SpriteRenderer spriteRenderer;

    private float elapsed;
    private float finalDiameter;

    private void Awake()
    {
        spriteRenderer =
            GetComponent<SpriteRenderer>();
    }

    public void Initialize(float radius)
    {
        elapsed = 0f;

        // Sprite Circle mac dinh co kich thuoc 1x1.
        finalDiameter = Mathf.Max(
            0.1f,
            radius * 2f
        );

        // Bat dau tu mot vong tron nho.
        transform.localScale =
            Vector3.one * 0.1f;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = explosionColor;
        }
    }

    private void Update()
    {
        if (finalDiameter <= 0f)
            return;

        elapsed += Time.deltaTime;

        float t = Mathf.Clamp01(
            elapsed / duration
        );

        // Vong no mo rong.
        float diameter = Mathf.Lerp(
            0.1f,
            finalDiameter,
            t
        );

        transform.localScale =
            Vector3.one * diameter;

        // Vong no mo dan.
        if (spriteRenderer != null)
        {
            Color color = explosionColor;

            color.a = Mathf.Lerp(
                explosionColor.a,
                0f,
                t
            );

            spriteRenderer.color = color;
        }

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
