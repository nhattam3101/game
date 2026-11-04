using System;
using System.Text;
using UnityEngine;

// MonsterSurvival 7G.1 - additive Shadow Titan ability.
// No edits to existing Boss, Health, WaveManager, VoidBurst or Save scripts.
[DisallowMultipleComponent]
[RequireComponent(typeof(Health))]
[DefaultExecutionOrder(500)]
public sealed class MS7G1TitanRiftLanes : MonoBehaviour
{
    [Header("Release Safety - OFF after install")]
    public bool enableRiftLanes = false;
    public bool phaseTwoOnly = true;
    public bool avoidVoidBurstOverlap = true;

    [Header("Existing Boss Phase 2")]
    public BossPhase2Controller phaseController;

    [Header("Fair Telegraph and Damage")]
    [Min(0.8f)] public float warningSeconds = 1.8f;
    [Min(0.1f)] public float impactVisualSeconds = 0.28f;
    [Min(3f)] public float cooldownSeconds = 16f;
    [Min(0f)] public float firstCastDelayAfterPhase2 = 5f;
    [Range(0f, 15f)] public float damage = 9f;
    [Min(1f)] public float laneLength = 5.8f;
    [Range(0.15f, 0.65f)] public float laneHalfWidth = 0.28f;
    [Range(10f, 45f)] public float sideAngleDegrees = 28f;
    [Min(0f)] public float safeNearBossRadius = 0.75f;

    [Header("Visuals only")]
    public Color warningColor = new Color(0.66f, 0.35f, 0.98f, 0.90f);
    public Color impactColor = new Color(1f, 0.65f, 0.30f, 1f);
    [Range(0.01f, 0.12f)] public float outlineWidth = 0.055f;
    public int sortingOrderAboveBoss = 2;

    public int WarningCount { get; private set; }
    public int ImpactCount { get; private set; }
    public int HitCount { get; private set; }
    public int DodgeCount { get; private set; }
    public int CancelCount { get; private set; }
    public int VoidBurstDeferCount { get; private set; }
    public bool PauseObserved { get; private set; }
    public bool Phase2Observed { get; private set; }

    private const int LaneCount = 3;
    private readonly Vector2[] laneDirections = new Vector2[LaneCount];
    private readonly Vector3[][] laneCorners = { new Vector3[4], new Vector3[4], new Vector3[4] };
    private readonly LineRenderer[] outlines = new LineRenderer[LaneCount];

    private Health bossHealth;
    private Health playerHealth;
    private Transform player;
    private SpriteRenderer bossSprite;
    private Material generatedMaterial;
    private Vector2 castOrigin;
    private float nextCastTime;
    private float phaseTwoFirstSeenAt = -1f;
    private float elapsed;
    private bool warningActive;
    private bool impactVisualActive;
    private bool initializedVisuals;

    private void Awake()
    {
        bossHealth = GetComponent<Health>();
        bossSprite = GetComponent<SpriteRenderer>();
        if (phaseController == null) phaseController = GetComponent<BossPhase2Controller>();
    }

    private void Update()
    {
        if (!enableRiftLanes || bossHealth == null || bossHealth.CurrentHealth <= 0f)
        {
            StopVisuals();
            return;
        }
        if (Time.timeScale <= 0f)
        {
            PauseObserved = true;
            return; // warning, impact and cooldown all use scaled Time
        }

        if (phaseTwoOnly && (phaseController == null || !phaseController.IsPhase2))
        {
            StopVisuals();
            return;
        }

        if (phaseController != null && phaseController.IsPhase2)
        {
            Phase2Observed = true;
            if (phaseTwoFirstSeenAt < 0f)
            {
                phaseTwoFirstSeenAt = Time.time;
                nextCastTime = Time.time + Mathf.Max(0f, firstCastDelayAfterPhase2);
            }
        }
        else if (!phaseTwoOnly && phaseTwoFirstSeenAt < 0f)
        {
            phaseTwoFirstSeenAt = Time.time;
            nextCastTime = Time.time + Mathf.Max(0f, firstCastDelayAfterPhase2);
        }

        if (impactVisualActive)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= Mathf.Max(0.1f, impactVisualSeconds)) StopVisuals();
            return;
        }

        if (warningActive)
        {
            // An existing Void Burst may start during the Rift warning. Cancel rather than stack.
            if (avoidVoidBurstOverlap && AnyVoidBurstZone())
            {
                CancelCount++;
                StopVisuals();
                nextCastTime = Time.time + 2.0f;
                return;
            }
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.8f, warningSeconds));
            float pulse = 0.70f + 0.30f * Mathf.Sin(progress * Mathf.PI * 8f) * Mathf.Sin(progress * Mathf.PI * 8f);
            SetOutlineColor(new Color(warningColor.r, warningColor.g, warningColor.b, warningColor.a * pulse));
            if (elapsed >= Mathf.Max(0.8f, warningSeconds)) Impact();
            return;
        }

        if (Time.time < nextCastTime) return;
        if (avoidVoidBurstOverlap && AnyVoidBurstZone())
        {
            VoidBurstDeferCount++;
            nextCastTime = Time.time + 1.5f;
            return;
        }
        if (!FindLivePlayer())
        {
            nextCastTime = Time.time + 1.5f;
            return;
        }
        BeginWarning();
    }

    private bool FindLivePlayer()
    {
        // Retry may instantiate a new Player. Reacquire before every cast.
        GameObject obj = GameObject.FindGameObjectWithTag("Player");
        if (obj == null) return false;
        player = obj.transform;
        playerHealth = obj.GetComponent<Health>();
        return playerHealth != null && playerHealth.CurrentHealth > 0f;
    }

    private static bool AnyVoidBurstZone()
    {
        return UnityEngine.Object.FindObjectsByType<VoidBurstZone>(FindObjectsSortMode.None).Length > 0;
    }

    private void BeginWarning()
    {
        if (!EnsureVisuals())
        {
            nextCastTime = Time.time + 3f;
            return;
        }
        castOrigin = transform.position;
        Vector2 toPlayer = (Vector2)player.position - castOrigin;
        if (toPlayer.sqrMagnitude < 0.0001f) toPlayer = Vector2.up;
        toPlayer.Normalize();
        for (int i = 0; i < LaneCount; i++)
        {
            float offset = i == 0 ? 0f : i == 1 ? -sideAngleDegrees : sideAngleDegrees;
            float angle = offset * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(
                toPlayer.x * Mathf.Cos(angle) - toPlayer.y * Mathf.Sin(angle),
                toPlayer.x * Mathf.Sin(angle) + toPlayer.y * Mathf.Cos(angle));
            laneDirections[i] = dir;
            Vector2 right = new Vector2(-dir.y, dir.x);
            Vector2 near = castOrigin + dir * safeNearBossRadius;
            Vector2 far = castOrigin + dir * laneLength;
            laneCorners[i][0] = near + right * laneHalfWidth;
            laneCorners[i][1] = far + right * laneHalfWidth;
            laneCorners[i][2] = far - right * laneHalfWidth;
            laneCorners[i][3] = near - right * laneHalfWidth;
            outlines[i].SetPositions(laneCorners[i]);
            outlines[i].enabled = true;
        }
        warningActive = true;
        impactVisualActive = false;
        elapsed = 0f;
        WarningCount++;
        SetOutlineColor(warningColor);
        Debug.Log("[7G.1] Shadow Titan Rift Lanes warning " + WarningCount, this);
    }

    private void Impact()
    {
        warningActive = false;
        impactVisualActive = true;
        elapsed = 0f;
        ImpactCount++;
        SetOutlineColor(impactColor);
        bool inLane = player != null && playerHealth != null &&
                      playerHealth.CurrentHealth > 0f && InAnyLane(player.position);
        if (inLane)
        {
            float before = playerHealth.CurrentHealth;
            // Use existing Health API so PlayerShield and hit pipelines remain in control.
            playerHealth.TakeDamage(Mathf.Clamp(damage, 0f, 15f));
            if (playerHealth != null && playerHealth.CurrentHealth < before) HitCount++;
            Debug.Log("[7G.1] Rift impact inside=True hpReduced=" + (playerHealth != null && playerHealth.CurrentHealth < before), this);
        }
        else
        {
            DodgeCount++;
            Debug.Log("[7G.1] Rift impact inside=False, player dodged", this);
        }
        nextCastTime = Time.time + Mathf.Max(3f, cooldownSeconds);
    }

    // Pure non-allocating point-vs-three-lanes test; overlaps award no extra hit.
    private bool InAnyLane(Vector2 point)
    {
        Vector2 delta = point - castOrigin;
        for (int i = 0; i < LaneCount; i++)
        {
            Vector2 dir = laneDirections[i];
            float forward = Vector2.Dot(delta, dir);
            float sideways = Mathf.Abs(delta.x * dir.y - delta.y * dir.x);
            if (forward >= safeNearBossRadius && forward <= laneLength && sideways <= laneHalfWidth)
                return true;
        }
        return false;
    }

    private bool EnsureVisuals()
    {
        if (initializedVisuals) return true;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            Debug.LogWarning("[7G.1] Cannot find compatible line shader; assign Sprites/Default or URP Unlit in project.", this);
            return false;
        }
        generatedMaterial = new Material(shader) { name = "MS7G1_RiftLines_Runtime", hideFlags = HideFlags.DontSave };
        for (int i = 0; i < LaneCount; i++)
        {
            GameObject obj = new GameObject("7G1_RiftLane_" + i);
            obj.transform.SetParent(transform, false);
            LineRenderer lr = obj.AddComponent<LineRenderer>();
            lr.useWorldSpace = true; // Snapshot remains still while Player dodges.
            lr.loop = true;
            lr.positionCount = 4;
            lr.alignment = LineAlignment.View;
            lr.startWidth = outlineWidth;
            lr.endWidth = outlineWidth;
            lr.numCornerVertices = 2;
            lr.numCapVertices = 2;
            lr.sharedMaterial = generatedMaterial;
            if (bossSprite != null)
            {
                lr.sortingLayerID = bossSprite.sortingLayerID;
                lr.sortingOrder = bossSprite.sortingOrder + sortingOrderAboveBoss;
            }
            lr.enabled = false;
            outlines[i] = lr;
        }
        initializedVisuals = true;
        return true;
    }

    private void SetOutlineColor(Color c)
    {
        foreach (LineRenderer lr in outlines)
        {
            if (lr == null) continue;
            lr.startColor = c;
            lr.endColor = c;
        }
    }

    private void StopVisuals()
    {
        warningActive = false;
        impactVisualActive = false;
        elapsed = 0f;
        foreach (LineRenderer lr in outlines)
            if (lr != null) lr.enabled = false;
    }

    private void OnDisable() { StopVisuals(); }
    private void OnDestroy()
    {
        StopVisuals();
        if (generatedMaterial != null) Destroy(generatedMaterial);
    }

    public string RuntimeReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("MS7G1 SHADOW RIFT LANES - READ ONLY RUNTIME");
        sb.AppendLine("Enabled=" + enableRiftLanes + " Phase2Observed=" + Phase2Observed +
                      " Phase2Now=" + (phaseController != null && phaseController.IsPhase2) +
                      " BossHP=" + (bossHealth != null ? bossHealth.CurrentHealth.ToString("F1") : "MISSING"));
        sb.AppendLine("Warnings=" + WarningCount + " Impacts=" + ImpactCount + " HPReducedHits=" + HitCount +
                      " Dodges=" + DodgeCount + " VoidBurstDefers=" + VoidBurstDeferCount +
                      " OverlapCancels=" + CancelCount + " PauseSeen=" + PauseObserved);
        sb.AppendLine("InWarning=" + warningActive + " ImpactFXActive=" + impactVisualActive);
        sb.AppendLine("NOTE: Counters do not prove pixel visibility, no-double-damage across all Boss skills, Save/Profile or boss victory.");
        return sb.ToString();
    }

    // Inspector-only clamp. Existing Boss Health and other skills are never touched.
    private void OnValidate()
    {
        warningSeconds = Mathf.Max(0.8f, warningSeconds);
        impactVisualSeconds = Mathf.Max(0.1f, impactVisualSeconds);
        cooldownSeconds = Mathf.Max(3f, cooldownSeconds);
        laneLength = Mathf.Max(1f, laneLength);
        safeNearBossRadius = Mathf.Clamp(safeNearBossRadius, 0f, laneLength - 0.25f);
        damage = Mathf.Clamp(damage, 0f, 15f);
    }
}
