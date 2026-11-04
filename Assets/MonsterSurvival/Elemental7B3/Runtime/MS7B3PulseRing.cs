using UnityEngine;

// One-shot world-space impact ring; purely visual.
[RequireComponent(typeof(LineRenderer))]
public sealed class MS7B3PulseRing : MonoBehaviour
{
    private LineRenderer line;
    private Color tint;
    private float lifetime;
    private float age;

    public void Initialize(Vector3 position, Color color, Material material, float seconds)
    {
        transform.position = position;
        tint = color;
        lifetime = Mathf.Max(.07f, seconds);
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 20;
        line.startWidth = line.endWidth = .035f;
        line.sortingOrder = 132;
        if (material != null) line.sharedMaterial = material;
        for (int i = 0; i < 20; i++)
        {
            float angle = i * Mathf.PI * 2f / 20f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
        }
        transform.localScale = Vector3.one * .13f;
    }

    private void Update()
    {
        if (line == null || Time.timeScale <= 0f) return;
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / lifetime);
        transform.localScale = Vector3.one * Mathf.Lerp(.12f, .49f, t);
        Color c = tint;
        c.a *= 1f - t;
        line.startColor = line.endColor = c;
        if (age >= lifetime) Destroy(gameObject);
    }
}
