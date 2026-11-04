using UnityEngine;

/// <summary>
/// World-space event visual and single hit. No colliders, Save changes or enemy modifications.
/// All geometry is sampled from a fixed position so Player can dodge the telegraph.
/// </summary>
[DisallowMultipleComponent]
public sealed class MS7F2ArenaEvent : MonoBehaviour
{
    public enum EventKind { TwinStrike, Crossfire, HollowRing }
    private enum Phase { Warning, Impact, Finished }

    private MS7F2ArenaEventDirector owner;
    private Transform player;
    private Health playerHealth;
    private Phase phase = Phase.Finished;
    private EventKind kind;
    private LineRenderer[] outlines;
    private Material ownedMaterial;
    private float timeLeft;
    private float impactDuration;
    private float damage;
    private float angle;
    private Color warningColor;
    private Color impactColor;
    private float circleRadius, twinOffset, beamHalfWidth, beamHalfLength, ringInnerRadius, ringOuterRadius;

    public EventKind Kind { get { return kind; } }

    public void Initialize(MS7F2ArenaEventDirector director, Transform target, Health health,
        Vector3 position, EventKind type, float rotationDegrees,
        float warningSeconds, float visibleImpactSeconds, float hitDamage,
        float twinRadius, float twoCentersOffset, float crossWidth, float crossHalfLength,
        float innerRadius, float outerRadius, Color warningTint, Color impactTint)
    {
        owner = director;
        player = target;
        playerHealth = health;
        transform.position = position;
        kind = type;
        angle = rotationDegrees;
        damage = Mathf.Max(0f, hitDamage);
        timeLeft = Mathf.Max(0.2f, warningSeconds);
        impactDuration = Mathf.Max(0.1f, visibleImpactSeconds);
        circleRadius = Mathf.Max(0.25f, twinRadius);
        twinOffset = Mathf.Max(0.1f, twoCentersOffset);
        beamHalfWidth = Mathf.Max(0.1f, crossWidth * 0.5f);
        beamHalfLength = Mathf.Max(0.4f, crossHalfLength);
        ringInnerRadius = Mathf.Max(0.15f, innerRadius);
        ringOuterRadius = Mathf.Max(ringInnerRadius + 0.3f, outerRadius);
        warningColor = warningTint;
        impactColor = impactTint;
        BuildOutlines();
        phase = Phase.Warning;
    }

    // This static geometry can be tested without a scene or damage calls.
    public static bool IsDangerAt(EventKind type, Vector2 point, Vector2 center, float degrees,
        float twinRadius, float twinOffset, float crossWidth, float crossHalfLength,
        float ringInnerRadius, float ringOuterRadius)
    {
        float a = degrees * Mathf.Deg2Rad;
        Vector2 d = point - center;
        float lx = d.x * Mathf.Cos(a) + d.y * Mathf.Sin(a);
        float ly = -d.x * Mathf.Sin(a) + d.y * Mathf.Cos(a);
        if (type == EventKind.TwinStrike)
        {
            float r2 = twinRadius * twinRadius;
            return (new Vector2(lx - twinOffset, ly)).sqrMagnitude <= r2 ||
                   (new Vector2(lx + twinOffset, ly)).sqrMagnitude <= r2;
        }
        if (type == EventKind.Crossfire)
        {
            float half = crossWidth * 0.5f;
            return (Mathf.Abs(lx) <= crossHalfLength && Mathf.Abs(ly) <= half) ||
                   (Mathf.Abs(ly) <= crossHalfLength && Mathf.Abs(lx) <= half);
        }
        float dsq = d.sqrMagnitude;
        return dsq >= ringInnerRadius * ringInnerRadius && dsq <= ringOuterRadius * ringOuterRadius;
    }

    private void BuildOutlines()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader != null)
        {
            ownedMaterial = new Material(shader);
            ownedMaterial.name = "MS7F2_Event_Visual_RuntimeOnly";
        }

        outlines = new LineRenderer[2];
        for (int i = 0; i < 2; i++)
        {
            GameObject outline = new GameObject("Outline_" + (i + 1));
            outline.transform.SetParent(transform, false);
            LineRenderer line = outline.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.startWidth = 0.065f;
            line.endWidth = 0.065f;
            line.sortingOrder = 20;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            if (ownedMaterial != null) line.sharedMaterial = ownedMaterial;
            outlines[i] = line;
        }

        if (kind == EventKind.TwinStrike)
        {
            DrawCircle(outlines[0], new Vector2(-twinOffset, 0f), circleRadius);
            DrawCircle(outlines[1], new Vector2(twinOffset, 0f), circleRadius);
        }
        else if (kind == EventKind.Crossfire)
        {
            DrawRectangle(outlines[0], beamHalfLength, beamHalfWidth);
            DrawRectangle(outlines[1], beamHalfWidth, beamHalfLength);
        }
        else
        {
            DrawCircle(outlines[0], Vector2.zero, ringInnerRadius);
            DrawCircle(outlines[1], Vector2.zero, ringOuterRadius);
        }

        for (int i = 0; i < outlines.Length; i++)
            outlines[i].transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        Tint(warningColor);
    }

    private static void DrawCircle(LineRenderer line, Vector2 center, float r)
    {
        const int segments = 64;
        line.positionCount = segments;
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            line.SetPosition(i, new Vector3(center.x + Mathf.Cos(a) * r,
                center.y + Mathf.Sin(a) * r, 0f));
        }
    }

    private static void DrawRectangle(LineRenderer line, float halfX, float halfY)
    {
        line.positionCount = 4;
        line.SetPosition(0, new Vector3(-halfX, -halfY, 0f));
        line.SetPosition(1, new Vector3(halfX, -halfY, 0f));
        line.SetPosition(2, new Vector3(halfX, halfY, 0f));
        line.SetPosition(3, new Vector3(-halfX, halfY, 0f));
    }

    private void Tint(Color c)
    {
        if (outlines == null) return;
        for (int i = 0; i < outlines.Length; i++)
        {
            if (outlines[i] == null) continue;
            outlines[i].startColor = c;
            outlines[i].endColor = c;
        }
    }

    private void Update()
    {
        if (phase == Phase.Finished || Time.timeScale <= 0f) return;
        if (owner == null || !owner.IsStillEligible) { Cancel(); return; }
        timeLeft -= Time.deltaTime;
        if (phase == Phase.Warning)
        {
            Color tint = warningColor;
            tint.a *= 0.7f + 0.3f * Mathf.Sin(Time.time * 12f);
            Tint(tint);
            if (timeLeft <= 0f) Impact();
        }
        else if (phase == Phase.Impact)
        {
            Color tint = impactColor;
            tint.a *= Mathf.Clamp01(timeLeft / impactDuration);
            Tint(tint);
            if (timeLeft <= 0f) Cancel();
        }
    }

    private void Impact()
    {
        phase = Phase.Impact;
        timeLeft = impactDuration;
        Tint(impactColor);
        bool inDanger = player != null && IsDangerAt(kind,
            player.position, transform.position, angle, circleRadius, twinOffset,
            beamHalfWidth * 2f, beamHalfLength, ringInnerRadius, ringOuterRadius);
        bool hpReduced = false;
        if (inDanger && playerHealth != null && damage > 0f)
        {
            float before = playerHealth.CurrentHealth;
            playerHealth.TakeDamage(damage); // Existing shield behaviour remains authoritative.
            hpReduced = playerHealth.CurrentHealth < before - 0.001f;
        }
        if (owner != null) owner.RecordImpact(kind, inDanger, hpReduced);
    }

    public void Cancel()
    {
        if (phase == Phase.Finished) return;
        phase = Phase.Finished;
        if (owner != null) owner.RecordEventFinished(this);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (ownedMaterial != null) Destroy(ownedMaterial);
    }
}
