using UnityEngine;

// OPTIONAL: Attach only to Main Camera if camera shake is desired.
// Runs late so it doesn't rewrite the Player, boss, or camera-follow scripts.
[DefaultExecutionOrder(10000)]
public class VoidBurstCameraShake : MonoBehaviour
{
    private float timeRemaining;
    private float totalDuration;
    private float amplitude;
    private Vector3 previousOffset;

    public void RequestShake(Vector3 explosionPosition, float strength,
        float duration, float maxDistance)
    {
        float distance = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.y),
            new Vector2(explosionPosition.x, explosionPosition.y));
        if (distance >= Mathf.Max(0.1f, maxDistance)) return;

        float falloff = 1f - distance / Mathf.Max(0.1f, maxDistance);
        float appliedStrength = Mathf.Max(0f, strength) * falloff;
        if (appliedStrength <= 0f) return;

        // Several overlapping zones cannot multiply shake without limit.
        amplitude = Mathf.Max(amplitude, appliedStrength);
        totalDuration = Mathf.Max(0.05f, duration);
        timeRemaining = Mathf.Max(timeRemaining, totalDuration);
    }

    private void LateUpdate()
    {
        // Remove only our own previously applied camera displacement.
        // LateUpdate + a high execution order helps compatibility with follow scripts.
        transform.position -= previousOffset;
        previousOffset = Vector3.zero;

        if (Time.timeScale <= 0f || timeRemaining <= 0f)
            return;

        timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        float fade = Mathf.Clamp01(timeRemaining / Mathf.Max(0.05f, totalDuration));
        float t = Time.time * 49f;
        float x = (Mathf.PerlinNoise(0.37f, t) * 2f - 1f) * amplitude * fade;
        float y = (Mathf.PerlinNoise(t, 0.91f) * 2f - 1f) * amplitude * fade;
        previousOffset = new Vector3(x, y, 0f);
        transform.position += previousOffset;
        if (timeRemaining <= 0f)
            amplitude = 0f;
    }

    private void OnDisable()
    {
        transform.position -= previousOffset;
        previousOffset = Vector3.zero;
        timeRemaining = 0f;
        amplitude = 0f;
    }
}
