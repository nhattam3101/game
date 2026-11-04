using System;
using System.Reflection;
using System.Text;
using UnityEngine;

// MonsterSurvival 7G.2 - an OPTIONAL, additive Grave Warden Phase 2 attack.
// Does NOT modify the existing GraveWardenBossAI, animations, boss Health or Save.
// The old AI's private cast flags are read only; missing flags FAIL CLOSED.
[RequireComponent(typeof(GraveWardenBossAI), typeof(Health))]
[DisallowMultipleComponent]
public sealed class MS7G2GraveSoulReap : MonoBehaviour
{
    [Header("7G.2 - OFF until manually enabled")]
    public bool enableSoulReap = false;
    public bool phaseTwoOnly = true;

    [Header("Timing (scaled game time; pauses at Time.timeScale = 0)")]
    [Min(0.2f)] public float firstCastDelayAfterPhase2 = 5f;
    [Min(3f)] public float cooldown = 17f;
    [Min(1.2f)] public float warningDuration = 1.65f;
    [Min(0.05f)] public float impactFlashDuration = 0.27f;
    [Min(0.1f)] public float retryIfOldAIBusySeconds = 1.0f;

    [Header("Soul Reap Arc - safe inner area and outside area")]
    [Min(0.3f)] public float innerRadius = 1.35f;
    [Min(0.8f)] public float outerRadius = 4.3f;
    [Range(25f, 75f)] public float halfArcAngleDegrees = 48f;
    [Min(0f)] public float damage = 11f;
    [Tooltip("Uses MapPathfinding2D.obstacleLayers if available: walls block a hit.")]
    public bool respectMapWalls = true;

    [Header("Outline VFX - purple -> gold, no changes to Boss HUD")]
    public Color warningPurple = new Color(0.70f, 0.30f, 0.96f, 0.92f);
    public Color chargedGold = new Color(1f, 0.77f, 0.36f, 1f);
    [Range(12, 72)] public int arcSegments = 36;
    [Min(0.015f)] public float outlineWidth = 0.075f;
    public int bossSpriteSortingOffset = 7;
    public Material customLineMaterial;

    [Header("Optional debug")]
    public bool logCastEvents = false;

    public int WarningsStarted { get; private set; }
    public int Impacts { get; private set; }
    public int PlayerInside { get; private set; }
    public int HpReduced { get; private set; }
    public int ShieldBlocked { get; private set; }
    public int PlayerDodged { get; private set; }
    public int WallBlocked { get; private set; }
    public int InterruptedByOldAI { get; private set; }
    public int CancelledOther { get; private set; }
    public bool OldCastFlagsAvailable { get { return castField != null && phaseTransitionField != null; } }
    public bool WarningActive { get { return state == AttackState.Warning; } }

    private enum AttackState { Idle, Warning, Impact }
    private AttackState state = AttackState.Idle;
    private GraveWardenBossAI ai;
    private Health bossHealth;
    private Transform player;
    private Health playerHealth;
    private FieldInfo castField;
    private FieldInfo phaseTransitionField;
    private bool warnedMissingAIGuard;
    private bool phaseInitialized;
    private float nextCastAt;
    private float stateUntil;
    private Vector2 castCenter;
    private Vector2 castDirection;
    private LineRenderer outline;
    private Material generatedMaterial;
    private Vector3[] polygon;
    private readonly string[] recentEvents = new string[24];
    private int eventIndex;
    private int eventCount;

    private void Awake()
    {
        ai = GetComponent<GraveWardenBossAI>();
        bossHealth = GetComponent<Health>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        castField = typeof(GraveWardenBossAI).GetField("casting", flags);
        phaseTransitionField = typeof(GraveWardenBossAI).GetField("pendingPhaseTwo", flags);
        if (castField == null || phaseTransitionField == null || castField.FieldType != typeof(bool) ||
            phaseTransitionField.FieldType != typeof(bool))
        {
            castField = phaseTransitionField = null;
        }
    }

    private void Start()
    {
        BuildOutline();
        if (outline != null) outline.enabled = false;
    }

    private void OnEnable()
    {
        phaseInitialized = false;
        state = AttackState.Idle;
    }

    // LateUpdate observes the older AI's Update and does not intercept its combat coroutine.
    private void LateUpdate()
    {
        if (!enableSoulReap || Time.timeScale <= 0f)
        {
            if (!enableSoulReap && state != AttackState.Idle) CancelAttack(false);
            return;
        }
        if (ai == null || bossHealth == null || !ai.enabled || ai.IsDead || bossHealth.CurrentHealth <= 0f)
        {
            if (state != AttackState.Idle) CancelAttack(false);
            return;
        }
        if (!OldCastFlagsAvailable)
        {
            if (!warnedMissingAIGuard)
            {
                warnedMissingAIGuard = true;
                Debug.LogWarning("[7G.2] Existing GraveWardenBossAI cast flags unavailable; Soul Reap FAILS CLOSED (no casting).", this);
            }
            if (state != AttackState.Idle) CancelAttack(false);
            return;
        }
        if (phaseTwoOnly && !ai.IsPhaseTwo)
        {
            phaseInitialized = false;
            if (state != AttackState.Idle) CancelAttack(false);
            return;
        }
        if (!phaseInitialized)
        {
            phaseInitialized = true;
            nextCastAt = Time.time + Mathf.Max(0.2f, firstCastDelayAfterPhase2);
        }
        if (IsOldAIBusy())
        {
            if (state != AttackState.Idle)
            {
                InterruptedByOldAI++;
                Trace("INTERRUPT - old AI started casting or phase transition");
                CancelAttack(false);
                nextCastAt = Time.time + Mathf.Max(0.1f, retryIfOldAIBusySeconds);
            }
            return;
        }
        if (state == AttackState.Warning)
        {
            if (!FindLivingPlayer()) { CancelAttack(true); return; }
            UpdateWarningColor();
            if (Time.time >= stateUntil) Impact();
            return;
        }
        if (state == AttackState.Impact)
        {
            if (Time.time >= stateUntil) HideOutline();
            return;
        }
        if (Time.time >= nextCastAt && FindLivingPlayer())
            BeginWarning();
    }

    private bool IsOldAIBusy()
    {
        // Reflection is read-only and cached; if the old script changes, casting is disabled.
        try { return (bool)castField.GetValue(ai) || (bool)phaseTransitionField.GetValue(ai); }
        catch (Exception ex)
        {
            castField = phaseTransitionField = null;
            Debug.LogWarning("[7G.2] Unable to inspect old AI safely: " + ex.Message, this);
            return true;
        }
    }

    private bool FindLivingPlayer()
    {
        if (player == null || playerHealth == null || playerHealth.CurrentHealth <= 0f)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            player = go != null ? go.transform : null;
            playerHealth = go != null ? go.GetComponent<Health>() : null;
        }
        return player != null && playerHealth != null && playerHealth.CurrentHealth > 0f;
    }

    private void BeginWarning()
    {
        if (outline == null) return; // missing compatible material/renderer: no invisible damage
        castCenter = transform.position;
        Vector2 delta = (Vector2)player.position - castCenter;
        castDirection = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.right;
        DrawSectorOnce();
        outline.enabled = true;
        outline.startColor = outline.endColor = warningPurple;
        state = AttackState.Warning;
        stateUntil = Time.time + Mathf.Max(1.2f, warningDuration);
        WarningsStarted++;
        Trace("WARNING Phase2 center=" + castCenter + " halfAngle=" + halfArcAngleDegrees);
    }

    private void UpdateWarningColor()
    {
        if (outline == null) return;
        float timeRemaining = Mathf.Max(0f, stateUntil - Time.time);
        float progress = 1f - timeRemaining / Mathf.Max(1.2f, warningDuration);
        Color c = Color.Lerp(warningPurple, chargedGold, progress * 0.85f);
        c.a *= 0.85f + 0.15f * Mathf.Sin(Time.time * 9f);
        outline.startColor = outline.endColor = c;
    }

    private void Impact()
    {
        state = AttackState.Impact;
        stateUntil = Time.time + Mathf.Max(0.05f, impactFlashDuration);
        nextCastAt = Time.time + Mathf.Max(3f, cooldown);
        Impacts++;
        if (outline != null) outline.startColor = outline.endColor = chargedGold;
        if (!FindLivingPlayer()) { Trace("IMPACT no living player"); return; }
        Vector2 pos = player.position;
        if (!IsInsideSector(pos, castCenter, castDirection, innerRadius, outerRadius, halfArcAngleDegrees))
        {
            PlayerDodged++;
            Trace("IMPACT DODGED outside warning sector");
            return;
        }
        if (respectMapWalls && ai.navigation != null && ai.navigation.obstacleLayers.value != 0 &&
            Physics2D.Linecast(castCenter, pos, ai.navigation.obstacleLayers))
        {
            WallBlocked++;
            Trace("IMPACT SAFE - obstacle between boss and player");
            return;
        }
        PlayerInside++;
        float beforeHp = playerHealth.CurrentHealth;
        var shield = player.GetComponent<PlayerShield>();
        int beforeShield = shield != null ? shield.ShieldCharges : 0;
        // One and only one TakeDamage call for this cast. Health applies shields itself.
        if (damage > 0f) playerHealth.TakeDamage(damage);
        if (playerHealth.CurrentHealth < beforeHp - 0.0001f) HpReduced++;
        else if (shield != null && shield.ShieldCharges < beforeShield) ShieldBlocked++;
        Trace("IMPACT inside; hpReduced=" + (playerHealth.CurrentHealth < beforeHp - 0.0001f) +
              " shieldConsumed=" + (shield != null && shield.ShieldCharges < beforeShield));
    }

    private void CancelAttack(bool countOther)
    {
        if (countOther) CancelledOther++;
        HideOutline();
    }

    private void HideOutline()
    {
        state = AttackState.Idle;
        if (outline != null) outline.enabled = false;
    }

    // Independent geometry testable without a Scene. Arc center is a SAFE zone.
    public static bool IsInsideSector(Vector2 point, Vector2 center, Vector2 forward,
        float inner, float outer, float halfAngle)
    {
        Vector2 delta = point - center;
        float sqr = delta.sqrMagnitude;
        float low = Mathf.Max(0.01f, inner);
        float high = Mathf.Max(low + 0.05f, outer);
        if (sqr < low * low || sqr > high * high) return false;
        if (forward.sqrMagnitude < 0.0001f) return false;
        return Vector2.Dot(delta.normalized, forward.normalized) >=
               Mathf.Cos(Mathf.Clamp(halfAngle, 1f, 89f) * Mathf.Deg2Rad) - 0.00001f;
    }

    private void BuildOutline()
    {
        var obj = new GameObject("MS7G2_SoulReap_Outline");
        obj.transform.SetParent(transform, false);
        outline = obj.AddComponent<LineRenderer>();
        outline.useWorldSpace = true;
        outline.loop = true;
        outline.alignment = LineAlignment.View;
        outline.textureMode = LineTextureMode.Stretch;
        outline.numCornerVertices = 4;
        outline.numCapVertices = 4;
        outline.startWidth = outline.endWidth = Mathf.Max(0.015f, outlineWidth);
        var sprite = GetComponentInChildren<SpriteRenderer>();
        if (sprite != null)
        {
            outline.sortingLayerID = sprite.sortingLayerID;
            outline.sortingOrder = sprite.sortingOrder + bossSpriteSortingOffset;
        }
        else outline.sortingOrder = bossSpriteSortingOffset;
        if (customLineMaterial != null) outline.sharedMaterial = customLineMaterial;
        else
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogWarning("[7G.2] Missing suitable line shader. Soul Reap stays inactive (no invisible damage).", this);
                Destroy(obj);
                outline = null;
                return;
            }
            generatedMaterial = new Material(shader) { name = "MS7G2_GeneratedLineMaterial" };
            outline.sharedMaterial = generatedMaterial;
        }
        int segments = Mathf.Clamp(arcSegments, 12, 72);
        polygon = new Vector3[2 * segments + 2];
        outline.positionCount = polygon.Length;
    }

    private void DrawSectorOnce()
    {
        if (outline == null) return;
        int n = (polygon.Length - 2) / 2;
        float inner = Mathf.Max(0.3f, innerRadius);
        float outer = Mathf.Max(inner + 0.5f, outerRadius);
        float baseAngle = Mathf.Atan2(castDirection.y, castDirection.x);
        float half = Mathf.Clamp(halfArcAngleDegrees, 25f, 75f) * Mathf.Deg2Rad;
        polygon[0] = AtRadius(baseAngle - half, inner);
        for (int i = 0; i <= n; i++)
            polygon[1 + i] = AtRadius(baseAngle - half + (2f * half * i / n), outer);
        polygon[n + 2] = AtRadius(baseAngle + half, inner);
        for (int i = n - 1; i >= 1; i--)
            polygon[n + 2 + (n - i)] = AtRadius(baseAngle - half + (2f * half * i / n), inner);
        outline.SetPositions(polygon);
    }

    private Vector3 AtRadius(float angle, float radius)
    {
        return new Vector3(castCenter.x + Mathf.Cos(angle) * radius,
            castCenter.y + Mathf.Sin(angle) * radius, transform.position.z);
    }

    private void Trace(string message)
    {
        recentEvents[eventIndex] = Time.time.ToString("F2") + "s | " + message;
        eventIndex = (eventIndex + 1) % recentEvents.Length;
        eventCount = Mathf.Min(eventCount + 1, recentEvents.Length);
        if (logCastEvents) Debug.Log("[7G.2] " + message, this);
    }

    public string BuildLiveReport()
    {
        var s = new StringBuilder();
        s.AppendLine("Boss=" + gameObject.name + " Enabled=" + enableSoulReap +
                     " Phase2=" + (ai != null && ai.IsPhaseTwo) +
                     " OldAIGuard=" + OldCastFlagsAvailable);
        s.AppendLine("Warnings=" + WarningsStarted + " Impacts=" + Impacts +
                     " PlayerInside=" + PlayerInside + " Dodged=" + PlayerDodged +
                     " HpReduced=" + HpReduced + " ShieldBlocked=" + ShieldBlocked +
                     " WallBlocked=" + WallBlocked + " InterruptedOldAI=" + InterruptedByOldAI +
                     " CancelledOther=" + CancelledOther + " CurrentlyWarning=" + WarningActive);
        s.AppendLine("--- 7G.2 TRACE ---");
        for (int i = 0; i < eventCount; i++)
        {
            int k = (eventIndex - eventCount + i + recentEvents.Length) % recentEvents.Length;
            s.AppendLine(recentEvents[k]);
        }
        return s.ToString();
    }

    private void OnDisable()
    {
        if (outline != null) outline.enabled = false;
        state = AttackState.Idle;
        phaseInitialized = false;
    }

    private void OnDestroy()
    {
        if (generatedMaterial != null) Destroy(generatedMaterial);
    }
}
