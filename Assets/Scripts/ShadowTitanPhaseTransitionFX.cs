using UnityEngine;

// Visual-only Phase 2 FX. Does not modify Health, SpriteRenderer colour,
// Animator, boss movement, Shockwave, Void Burst or Victory.
[RequireComponent(typeof(BossPhase2Controller))]
public class ShadowTitanPhaseTransitionFX : MonoBehaviour
{
    [Header("References (Optional)")]
    public BossPhase2Controller phaseController;
    public SpriteRenderer bossSprite;
    public Material effectMaterial;
    public AudioClip transformationSound;
    [Range(0f, 1f)] public float soundVolume = 0.8f;

    [Header("Phase Transition")]
    [Min(0.1f)] public float transitionDuration = 1.4f;
    [Min(0.1f)] public float startingRadius = 0.35f;
    [Min(0.2f)] public float burstRadius = 2.6f;
    [Min(0.005f)] public float transitionRingWidth = 0.09f;
    [Range(4, 24)] public int energyRays = 10;

    [Header("Permanent Phase 2 Aura")]
    public bool showAuraAfterTransformation = true;
    [Min(0.1f)] public float auraRadius = 1.8f;
    [Min(0.005f)] public float auraWidth = 0.045f;
    [Min(0f)] public float auraPulseSpeed = 3f;
    [Range(0f, 0.4f)] public float auraPulseAmount = 0.08f;

    [Header("Visual Appearance")]
    public Color ringColor = new Color(0.82f, 0.25f, 1f, 0.95f);
    public Color rayColor = new Color(1f, 0.50f, 0.94f, 0.9f);
    public Color auraColor = new Color(0.56f, 0.17f, 1f, 0.40f);
    [Range(24, 128)] public int circleSegments = 72;
    public int sortingOrderOffset = 6;

    private bool triggered;
    private float elapsed;
    private LineRenderer expandingRing;
    private LineRenderer contractingRing;
    private LineRenderer auraRing;
    private LineRenderer[] rays;
    private Material generatedMaterial;
    private Vector3[] circlePoints;
    private Vector3[] rayPoints = new Vector3[2];

    private void Awake()
    {
        if (phaseController == null)
            phaseController = GetComponent<BossPhase2Controller>();
        if (bossSprite == null)
            bossSprite = GetComponent<SpriteRenderer>();

        if (effectMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
                generatedMaterial = new Material(shader);
        }

        int segments = Mathf.Max(24, circleSegments);
        circlePoints = new Vector3[segments];
        expandingRing = MakeLine("Phase2_ExpandingRing", transitionRingWidth, true);
        contractingRing = MakeLine("Phase2_ContractingRing", transitionRingWidth * 0.6f, true);
        auraRing = MakeLine("Phase2_AuraRing", auraWidth, true);

        rays = new LineRenderer[Mathf.Max(4, energyRays)];
        for (int i = 0; i < rays.Length; i++)
            rays[i] = MakeLine("Phase2_EnergyRay_" + i, transitionRingWidth * 0.55f, false);

        SetVisualsEnabled(false);
    }

    private void Update()
    {
        if (phaseController == null)
            return;

        if (!triggered && phaseController.IsPhase2)
            StartTransition();

        if (!triggered)
            return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, transitionDuration));
        Vector3 center = transform.position;

        if (t < 1f)
        {
            float eased = 1f - (1f - t) * (1f - t);
            DrawCircle(expandingRing, center, Mathf.Lerp(startingRadius, burstRadius, eased));
            DrawCircle(contractingRing, center, Mathf.Lerp(burstRadius * 0.75f, startingRadius, t));
            SetAlpha(expandingRing, ringColor, 1f - t);
            SetAlpha(contractingRing, ringColor, (1f - t) * 0.55f);
            UpdateEnergyRays(center, eased, 1f - t);
        }
        else
        {
            expandingRing.enabled = false;
            contractingRing.enabled = false;
            foreach (LineRenderer ray in rays)
                ray.enabled = false;
        }

        auraRing.enabled = showAuraAfterTransformation;
        if (showAuraAfterTransformation)
        {
            float pulse = Mathf.Sin(elapsed * auraPulseSpeed);
            float radius = auraRadius * (1f + auraPulseAmount * pulse);
            DrawCircle(auraRing, center, radius);
            float opacity = t < 1f ? t : 1f;
            SetAlpha(auraRing, auraColor, opacity * (0.85f + 0.15f * pulse));
        }
    }

    private void StartTransition()
    {
        triggered = true;
        elapsed = 0f;
        expandingRing.enabled = true;
        contractingRing.enabled = true;
        foreach (LineRenderer ray in rays)
            ray.enabled = true;

        if (transformationSound != null && AudioManager.Instance != null &&
            AudioManager.Instance.sfxSource != null)
            AudioManager.Instance.sfxSource.PlayOneShot(transformationSound, soundVolume);

        Debug.Log("Shadow Titan: Phase 2 transformation VFX started.");
    }

    private void UpdateEnergyRays(Vector3 center, float progress, float alpha)
    {
        for (int i = 0; i < rays.Length; i++)
        {
            float angle = 2f * Mathf.PI * i / rays.Length + elapsed * 0.7f;
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            float start = Mathf.Lerp(0.08f, burstRadius * 0.3f, progress);
            float end = Mathf.Lerp(0.5f, burstRadius * 1.05f, progress);
            rayPoints[0] = center + direction * start;
            rayPoints[1] = center + direction * end;
            rays[i].SetPositions(rayPoints);
            SetAlpha(rays[i], rayColor, alpha);
        }
    }

    private void DrawCircle(LineRenderer line, Vector3 center, float radius)
    {
        for (int i = 0; i < circlePoints.Length; i++)
        {
            float angle = 2f * Mathf.PI * i / circlePoints.Length;
            circlePoints[i] = center + new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0f);
        }
        line.SetPositions(circlePoints);
    }

    private static void SetAlpha(LineRenderer line, Color baseColor, float opacity)
    {
        Color color = baseColor;
        color.a = Mathf.Clamp01(baseColor.a * opacity);
        line.startColor = color;
        line.endColor = color;
    }

    private LineRenderer MakeLine(string objectName, float width, bool loop)
    {
        GameObject obj = new GameObject(objectName);
        obj.transform.SetParent(transform, false);
        LineRenderer lr = obj.AddComponent<LineRenderer>();
        lr.useWorldSpace = true; // Boss's Transform Scale will not resize the effect.
        lr.loop = loop;
        lr.alignment = LineAlignment.View;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.numCornerVertices = 6;
        lr.numCapVertices = 6;
        lr.positionCount = loop ? circlePoints.Length : 2;
        lr.sortingLayerID = bossSprite != null ? bossSprite.sortingLayerID : 0;
        lr.sortingOrder = (bossSprite != null ? bossSprite.sortingOrder : 0) + sortingOrderOffset;
        Material material = effectMaterial != null ? effectMaterial : generatedMaterial;
        if (material != null)
            lr.sharedMaterial = material;
        else
            Debug.LogWarning("ShadowTitanPhaseTransitionFX: Missing compatible shader; assign Effect Material.", this);
        return lr;
    }

    private void SetVisualsEnabled(bool enabled)
    {
        expandingRing.enabled = enabled;
        contractingRing.enabled = enabled;
        auraRing.enabled = enabled;
        foreach (LineRenderer ray in rays)
            ray.enabled = enabled;
    }

    private void OnDisable()
    {
        if (expandingRing != null) expandingRing.enabled = false;
        if (contractingRing != null) contractingRing.enabled = false;
        if (auraRing != null) auraRing.enabled = false;
        if (rays != null)
            foreach (LineRenderer ray in rays)
                if (ray != null) ray.enabled = false;
    }

    private void OnDestroy()
    {
        if (generatedMaterial != null)
            Destroy(generatedMaterial);
    }
}
