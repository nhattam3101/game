using UnityEngine;

/// <summary>
/// A single telegraphed world-space hazard. No collider, no enemy damage, no Save writes.
/// The telegraph stays visible for warningSeconds; damage is evaluated once at activation.
/// </summary>
[DisallowMultipleComponent]
public sealed class MS7F1ArenaHazard : MonoBehaviour
{
    private enum Phase { Warning, Active, Finished }

    private MS7F1ArenaHazardDirector owner;
    private Transform target;
    private Health targetHealth;
    private LineRenderer ring;
    private Material ownedMaterial;
    private Phase phase;
    private float phaseTimer;
    private float radius;
    private float damage;
    private Color warningColor;
    private Color activeColor;
    private bool initialized;
    private float warningDuration;
    private float activeDuration;

    public void Initialize(
        MS7F1ArenaHazardDirector director, Transform player, Health health,
        Vector3 position, float hazardRadius, float warningSeconds,
        float activeSeconds, float hitDamage, Color warnTint, Color hitTint)
    {
        owner = director;
        target = player;
        targetHealth = health;
        radius = Mathf.Max(0.2f, hazardRadius);
        damage = Mathf.Max(0f, hitDamage);
        warningDuration = Mathf.Max(0.2f, warningSeconds);
        activeDuration = Mathf.Max(0.1f, activeSeconds);
        warningColor = warnTint;
        activeColor = hitTint;
        transform.position = position;
        phase = Phase.Warning;
        phaseTimer = warningDuration;
        BuildRing();
        initialized = true;
    }

    private void BuildRing()
    {
        ring = gameObject.AddComponent<LineRenderer>();
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = 64;
        ring.startWidth = 0.065f;
        ring.endWidth = 0.065f;
        ring.numCornerVertices = 2;
        ring.numCapVertices = 2;
        ring.sortingOrder = 20;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader != null)
        {
            ownedMaterial = new Material(shader);
            ownedMaterial.name = "MS7F1_HazardRing_RuntimeOnly";
            ring.sharedMaterial = ownedMaterial;
        }
        for (int i = 0; i < ring.positionCount; i++)
        {
            float a = (i / (float)ring.positionCount) * Mathf.PI * 2f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }
        ring.startColor = warningColor;
        ring.endColor = warningColor;
    }

    private void Update()
    {
        if (!initialized || Time.timeScale <= 0f) return;
        if (owner == null || !owner.IsStillEligible)
        {
            Finish();
            return;
        }

        phaseTimer -= Time.deltaTime;
        if (phase == Phase.Warning)
        {
            float pulse = 0.68f + 0.32f * Mathf.Sin(Time.time * 12f);
            Color tint = warningColor;
            tint.a = warningColor.a * pulse;
            if (ring != null) { ring.startColor = tint; ring.endColor = tint; }
            if (phaseTimer <= 0f) Activate();
        }
        else if (phase == Phase.Active)
        {
            float alpha = Mathf.Clamp01(phaseTimer / activeDuration);
            Color tint = activeColor;
            tint.a *= alpha;
            if (ring != null) { ring.startColor = tint; ring.endColor = tint; }
            if (phaseTimer <= 0f) Finish();
        }
    }

    private void Activate()
    {
        phase = Phase.Active;
        phaseTimer = activeDuration;
        bool inRadius = target != null &&
                        ((Vector2)(target.position - transform.position)).sqrMagnitude <= radius * radius;
        bool hpReduced = false;
        if (inRadius && targetHealth != null && damage > 0f)
        {
            float before = targetHealth.CurrentHealth;
            targetHealth.TakeDamage(damage); // Existing shield/Health rules remain authoritative.
            hpReduced = targetHealth.CurrentHealth < before - 0.001f;
        }
        if (ring != null) { ring.startColor = activeColor; ring.endColor = activeColor; }
        if (owner != null) owner.RecordActivation(inRadius, hpReduced);
    }

    public void Cancel()
    {
        Finish();
    }

    private void Finish()
    {
        if (phase == Phase.Finished) return;
        phase = Phase.Finished;
        initialized = false;
        if (owner != null) owner.RecordFinished(this);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (ownedMaterial != null) Destroy(ownedMaterial);
    }
}
