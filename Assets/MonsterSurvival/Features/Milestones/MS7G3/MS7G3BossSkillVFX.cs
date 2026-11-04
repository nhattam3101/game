using System;
using System.Text;
using UnityEngine;

// 7G.3 - VISUAL ONLY. Follows the authoritative 7G.1/7G.2 warning outlines.
// No Health/Shield/AI/Animator/Stats/Save writes; no damage or hit-box logic.
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class MS7G3BossSkillVFX : MonoBehaviour
{
    public enum BossSkill { Auto = 0, TitanRiftLanes = 1, GraveSoulReap = 2 }

    [Header("Safety: OFF after installation")]
    public bool enableEnhancedVFX = false;
    public BossSkill targetSkill = BossSkill.Auto;

    [Header("Appearance - translucent so gameplay remains readable")]
    [Range(0.1f, 0.9f)] public float warningGlowAlpha = 0.23f;
    [Range(0.1f, 0.9f)] public float chargeTrailAlpha = 0.70f;
    [Range(1.1f, 5f)] public float glowWidthMultiplier = 2.6f;
    [Range(0.25f, 3f)] public float trailCyclesPerWarning = 1.7f;
    [Range(0.08f, 0.36f)] public float trailPerimeterFraction = 0.17f;
    [Range(0.15f, 0.65f)] public float bossChargeAuraRadius = 0.42f;
    public Color titanWarningGlow = new Color(0.67f, 0.36f, 1f, 0.85f);
    public Color graveWarningGlow = new Color(0.88f, 0.52f, 1f, 0.85f);
    public Color impactGlow = new Color(1f, 0.78f, 0.37f, 1f);
    [Range(0, 8)] public int sortingOrderAboveOriginal = 1;
    public Material optionalLineMaterial;

    public int WarningFxCount { get; private set; }
    public int ImpactFxCount { get; private set; }
    public int EarlyCleanupCount { get; private set; }
    public int MissingGeometryFrames { get; private set; }
    public bool PauseObserved { get; private set; }
    public int SyncedOutlineCount { get; private set; }
    public bool IsEffectActive { get { return state != VisualState.None; } }
    public string ResolvedSkillName { get { return resolved == BossSkill.TitanRiftLanes ? "TitanRiftLanes" : resolved == BossSkill.GraveSoulReap ? "GraveSoulReap" : "Unresolved"; } }

    private enum VisualState { None, Warning, Impact }

    private sealed class Overlay
    {
        public LineRenderer source;
        public LineRenderer glow;
        public LineRenderer trail;
        public Vector3[] points;
        public float[] cumulative;
        public float perimeter;
        public readonly Vector3[] trailPoints = new Vector3[9];
        public int count;
    }

    private readonly Overlay[] slots = new Overlay[3];
    private readonly Vector3[] auraPoints = new Vector3[48];
    private BossSkill resolved;
    private MS7G1TitanRiftLanes titan;
    private MS7G2GraveSoulReap grave;
    private Health bossHealth;
    private Material generatedMaterial;
    private LineRenderer aura;
    private VisualState state;
    private int seenWarnings;
    private int seenImpacts;
    private float phaseElapsed;
    private float visualLifetime;
    private int slotCount;
    private bool warnedNoShader;

    private void Awake()
    {
        bossHealth = GetComponent<Health>();
        titan = GetComponent<MS7G1TitanRiftLanes>();
        grave = GetComponent<MS7G2GraveSoulReap>();
        ResolveSource();
    }

    private void ResolveSource()
    {
        resolved = BossSkill.Auto;
        if ((targetSkill == BossSkill.Auto || targetSkill == BossSkill.TitanRiftLanes) && titan != null)
            resolved = BossSkill.TitanRiftLanes;
        else if ((targetSkill == BossSkill.Auto || targetSkill == BossSkill.GraveSoulReap) && grave != null)
            resolved = BossSkill.GraveSoulReap;
    }

    private bool SkillIsLive()
    {
        if (bossHealth == null || bossHealth.CurrentHealth <= 0f) return false;
        if (resolved == BossSkill.TitanRiftLanes) return titan != null && titan.isActiveAndEnabled && titan.enableRiftLanes;
        if (resolved == BossSkill.GraveSoulReap) return grave != null && grave.isActiveAndEnabled && grave.enableSoulReap;
        return false;
    }

    private int SourceWarnings()
    {
        return resolved == BossSkill.TitanRiftLanes && titan != null ? titan.WarningCount :
               resolved == BossSkill.GraveSoulReap && grave != null ? grave.WarningsStarted : 0;
    }

    private int SourceImpacts()
    {
        return resolved == BossSkill.TitanRiftLanes && titan != null ? titan.ImpactCount :
               resolved == BossSkill.GraveSoulReap && grave != null ? grave.Impacts : 0;
    }

    private float SourceWarningDuration()
    {
        return resolved == BossSkill.TitanRiftLanes ? Mathf.Max(0.8f, titan.warningSeconds) :
               resolved == BossSkill.GraveSoulReap ? Mathf.Max(1.2f, grave.warningDuration) : 1.8f;
    }

    private float SourceImpactDuration()
    {
        return resolved == BossSkill.TitanRiftLanes ? Mathf.Max(0.1f, titan.impactVisualSeconds) :
               resolved == BossSkill.GraveSoulReap ? Mathf.Max(0.05f, grave.impactFlashDuration) : 0.2f;
    }

    private void LateUpdate()
    {
        if (!enableEnhancedVFX || !SkillIsLive())
        {
            ClearVisuals();
            return;
        }
        if (Time.timeScale <= 0f)
        {
            PauseObserved = true;
            return;  // including shimmer: DO NOT use unscaledDeltaTime
        }

        int warnings = SourceWarnings();
        int impacts = SourceImpacts();

        // An original cast can begin before the visual component has found its LineRenderers.
        // Retry next frame; never show fabricated geometry or invisible gameplay hits.
        if (warnings > seenWarnings)
        {
            if (CaptureOriginalGeometry())
            {
                seenWarnings = warnings;
                WarningFxCount++;
                state = VisualState.Warning;
                phaseElapsed = 0f;
                visualLifetime = SourceWarningDuration();
            }
            else if (impacts >= warnings) seenWarnings = warnings; // cast ended before a valid visual snapshot
            else MissingGeometryFrames++;
        }
        if (impacts > seenImpacts)
        {
            seenImpacts = impacts;
            if (state != VisualState.None)
            {
                state = VisualState.Impact;
                phaseElapsed = 0f;
                visualLifetime = SourceImpactDuration();
                ImpactFxCount++;
            }
        }
        if (state == VisualState.None) return;

        // When the original cancels / despawns, hide visual extras immediately.
        bool originalVisible = true;
        for (int i = 0; i < slotCount; i++)
        {
            if (slots[i] == null || slots[i].source == null || !slots[i].source.enabled)
            { originalVisible = false; break; }
        }
        if (!originalVisible)
        {
            if (state == VisualState.Warning) EarlyCleanupCount++;
            ClearVisuals();
            return;
        }

        phaseElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(phaseElapsed / Mathf.Max(0.05f, visualLifetime));
        Color color = state == VisualState.Warning ?
            (resolved == BossSkill.TitanRiftLanes ? titanWarningGlow : graveWarningGlow) : impactGlow;
        float alphaScale = state == VisualState.Warning ?
            (0.65f + 0.35f * progress) : (1f - progress);
        for (int i = 0; i < slotCount; i++)
        {
            Overlay slot = slots[i];
            if (slot == null) continue;
            Color glowColor = color;
            glowColor.a = Mathf.Clamp01(color.a * warningGlowAlpha * alphaScale);
            slot.glow.startColor = slot.glow.endColor = glowColor;
            slot.glow.enabled = true;
            if (state == VisualState.Warning)
            {
                UpdateTrail(slot, Mathf.Repeat(progress * trailCyclesPerWarning + i * 0.21f, 1f));
                Color trailColor = Color.Lerp(color, Color.white, 0.5f);
                trailColor.a = Mathf.Clamp01(chargeTrailAlpha * alphaScale);
                slot.trail.startColor = slot.trail.endColor = trailColor;
                slot.trail.enabled = true;
            }
            else slot.trail.enabled = false;
        }
        UpdateChargeAura(color, progress, state == VisualState.Impact ? 1f - progress : 1f);
        if (state == VisualState.Impact && phaseElapsed > visualLifetime + 0.03f)
            ClearVisuals();
    }

    private bool CaptureOriginalGeometry()
    {
        int required = resolved == BossSkill.TitanRiftLanes ? 3 : 1;
        LineRenderer[] found = new LineRenderer[3];
        for (int i = 0; i < required; i++)
        {
            string name = resolved == BossSkill.TitanRiftLanes ? "7G1_RiftLane_" + i : "MS7G2_SoulReap_Outline";
            Transform t = transform.Find(name);
            if (t == null) return false;
            LineRenderer line = t.GetComponent<LineRenderer>();
            if (line == null || !line.enabled || line.positionCount < 3) return false;
            found[i] = line;
        }
        if (!EnsureMaterial()) return false;
        slotCount = required;
        SyncedOutlineCount = required;
        for (int i = 0; i < required; i++)
        {
            Overlay slot = slots[i];
            if (slot == null) { slot = new Overlay(); slots[i] = slot; }
            LineRenderer source = found[i];
            slot.source = source;
            int n = source.positionCount;
            if (slot.points == null || slot.points.Length != n)
            {
                slot.points = new Vector3[n];
                slot.cumulative = new float[n + 1];
            }
            slot.count = n;
            source.GetPositions(slot.points);
            slot.cumulative[0] = 0f;
            for (int j = 1; j <= n; j++)
                slot.cumulative[j] = slot.cumulative[j - 1] +
                                     Vector3.Distance(slot.points[j - 1], slot.points[j % n]);
            slot.perimeter = slot.cumulative[n];
            if (slot.glow == null) slot.glow = MakeLine("7G3_Glow_" + i, source, true, n);
            if (slot.trail == null) slot.trail = MakeLine("7G3_ChargeTrail_" + i, source, false, slot.trailPoints.Length);
            slot.glow.positionCount = n;
            slot.glow.SetPositions(slot.points);  // snapshot: do not chase the player during warning
            slot.glow.startWidth = slot.glow.endWidth = source.startWidth * glowWidthMultiplier;
            slot.trail.startWidth = slot.trail.endWidth = Mathf.Max(0.035f, source.startWidth * 1.5f);
            slot.glow.enabled = true;
            slot.trail.enabled = true;
        }
        if (aura == null) aura = MakeAura(found[0]);
        return true;
    }

    private bool EnsureMaterial()
    {
        if (optionalLineMaterial != null) return true;
        if (generatedMaterial != null) return true;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            if (!warnedNoShader) Debug.LogWarning("[7G.3] Missing line shader. Optional VFX disabled; base attacks unaffected.", this);
            warnedNoShader = true;
            return false;
        }
        generatedMaterial = new Material(shader)
        {
            name = "MS7G3_BossSkillVFX_Runtime",
            hideFlags = HideFlags.DontSave
        };
        return true;
    }

    private LineRenderer MakeLine(string name, LineRenderer original, bool closed, int count)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = optionalLineMaterial != null ? optionalLineMaterial : generatedMaterial;
        line.useWorldSpace = true;
        line.loop = closed;
        line.alignment = LineAlignment.View;
        line.positionCount = count;
        line.numCornerVertices = 2;
        line.numCapVertices = 2;
        line.sortingLayerID = original.sortingLayerID;
        line.sortingOrder = original.sortingOrder + sortingOrderAboveOriginal;
        line.enabled = false;
        return line;
    }

    private LineRenderer MakeAura(LineRenderer original)
    {
        LineRenderer line = MakeLine("7G3_BossChargeAura", original, true, auraPoints.Length);
        line.startWidth = line.endWidth = 0.04f;
        return line;
    }

    // Keep the trail within one lap: do not draw a fake diagonal across the safe area at wrap.
    public static float TrailU(float start, float fraction, float segmentT)
    {
        float f = Mathf.Clamp(fraction, 0f, 0.5f);
        return Mathf.Clamp01(start) * (1f - f) + f * Mathf.Clamp01(segmentT);
    }

    private void UpdateTrail(Overlay slot, float startProgress)
    {
        if (slot.perimeter < 0.01f) { slot.trail.enabled = false; return; }
        for (int i = 0; i < slot.trailPoints.Length; i++)
        {
            float u = TrailU(startProgress, trailPerimeterFraction, i / (float)(slot.trailPoints.Length - 1));
            float d = u * slot.perimeter;
            int edge = 0;
            while (edge < slot.count - 1 && slot.cumulative[edge + 1] < d) edge++;
            float len = slot.cumulative[edge + 1] - slot.cumulative[edge];
            float k = len > 0.0001f ? Mathf.Clamp01((d - slot.cumulative[edge]) / len) : 0f;
            slot.trailPoints[i] = Vector3.Lerp(slot.points[edge], slot.points[(edge + 1) % slot.count], k);
        }
        slot.trail.SetPositions(slot.trailPoints);
    }

    private void UpdateChargeAura(Color color, float progress, float alphaScale)
    {
        if (aura == null) return;
        float radius = bossChargeAuraRadius * (1f + 0.1f * Mathf.Sin(progress * Mathf.PI * 4f));
        if (state == VisualState.Impact) radius *= Mathf.Lerp(1f, 1.65f, progress);
        float spin = progress * Mathf.PI * 1.5f;
        Vector3 center = transform.position;
        for (int i = 0; i < auraPoints.Length; i++)
        {
            float a = (i / (float)auraPoints.Length) * Mathf.PI * 2f + spin;
            auraPoints[i] = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
        }
        aura.SetPositions(auraPoints);
        Color c = color;
        c.a = Mathf.Clamp01(alphaScale * 0.45f);
        aura.startColor = aura.endColor = c;
        aura.enabled = true;
    }

    private void ClearVisuals()
    {
        state = VisualState.None;
        phaseElapsed = 0f;
        if (aura != null) aura.enabled = false;
        for (int i = 0; i < slots.Length; i++)
        {
            Overlay slot = slots[i];
            if (slot == null) continue;
            if (slot.glow != null) slot.glow.enabled = false;
            if (slot.trail != null) slot.trail.enabled = false;
        }
    }

    private void OnDisable() { ClearVisuals(); }
    private void OnDestroy()
    {
        ClearVisuals();
        if (generatedMaterial != null) Destroy(generatedMaterial);
    }

    public string RuntimeReport()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("7G.3 BOSS SKILL VFX (VISUAL ONLY / READ ONLY REPORT)");
        sb.AppendLine("Boss=" + name + " Source=" + ResolvedSkillName + " Enabled=" + enableEnhancedVFX);
        sb.AppendLine("WarningFX=" + WarningFxCount + " ImpactFX=" + ImpactFxCount +
                      " EarlyCleanup=" + EarlyCleanupCount + " GeometryNotReadyFrames=" + MissingGeometryFrames);
        sb.AppendLine("SyncedOutlines=" + SyncedOutlineCount + " Active=" + IsEffectActive + " PauseSeen=" + PauseObserved);
        sb.AppendLine("SourceWarnings=" + SourceWarnings() + " SourceImpacts=" + SourceImpacts());
        sb.AppendLine("NOTE: Source counters do not prove visible pixels, shields, old AI behavior, boss HP or Save/Profile.");
        return sb.ToString();
    }
}
