using UnityEngine;

// Short-lived jittered electric core drawn on top of 7B.1's existing arc.
[RequireComponent(typeof(LineRenderer))]
public sealed class MS7B3ArcFlash : MonoBehaviour
{
    private LineRenderer line;
    private Color baseColor;
    private float duration;
    private float elapsed;

    public void Initialize(Vector3 from, Vector3 to, Material material, float lifetime)
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 7;
        line.loop = false;
        line.sortingOrder = 135;
        line.startWidth = .045f;
        line.endWidth = .026f;
        if (material != null) line.sharedMaterial = material;
        baseColor = new Color(.92f, .98f, 1f, .95f);
        line.startColor = baseColor;
        line.endColor = baseColor;
        Vector3 forward = to - from;
        Vector3 normal = Vector3.Cross(forward.normalized, Vector3.forward);
        float amplitude = Mathf.Min(.19f, forward.magnitude * .10f);
        for (int i = 0; i < line.positionCount; i++)
        {
            float t = (float)i / (line.positionCount - 1);
            float offset = i == 0 || i == line.positionCount - 1 ? 0f : Random.Range(-amplitude, amplitude);
            line.SetPosition(i, Vector3.Lerp(from, to, t) + normal * offset);
        }
        duration = Mathf.Max(.05f, lifetime);
    }

    private void Update()
    {
        if (line == null) return;
        if (Time.timeScale <= 0f) return;
        elapsed += Time.deltaTime;
        float alpha = Mathf.Clamp01(1f - elapsed / Mathf.Max(.05f, duration));
        Color col = baseColor;
        col.a *= alpha;
        line.startColor = col;
        line.endColor = col;
        if (elapsed >= duration) Destroy(gameObject);
    }
}
