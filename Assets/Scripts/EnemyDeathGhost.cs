
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class EnemyDeathGhost : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;

    public void Play(
        float duration,
        float riseSpeed,
        float finalScale)
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        StartCoroutine(
            Animate(
                Mathf.Max(0.05f, duration),
                riseSpeed,
                Mathf.Clamp01(finalScale)
            )
        );
    }

    private IEnumerator Animate(
        float duration,
        float riseSpeed,
        float finalScale)
    {
        Color startColor = spriteRenderer.color;
        Vector3 startScale = transform.localScale;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsed / duration
            );

            // Bay nhe len tren
            transform.position += Vector3.up *
                riseSpeed * Time.deltaTime;

            // Thu nho dan
            transform.localScale = Vector3.Lerp(
                startScale,
                startScale * finalScale,
                progress
            );

            // Mo dan
            Color color = startColor;
            color.a = startColor.a * (1f - progress);

            spriteRenderer.color = color;

            yield return null;
        }

        Destroy(gameObject);
    }
}
