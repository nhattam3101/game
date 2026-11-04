using System.Collections.Generic;
using UnityEngine;

// Shadow Titan Phase 2: one zone targets Player; 3 randomized nearby zone patterns.
// NEW: The boss stops charging when the zones EXPLODE (end of warning),
// not when explosion/aftermath visuals finish. VFX continues independently.
// Preserves Shockwave, zone damage, shield support, and the boss's HP/Victory logic.
[RequireComponent(typeof(Health))]
public class BossVoidBurstAttack : MonoBehaviour
{
    [Header("Boss References")]
    public BossPhase2Controller phaseController;
    public VoidBurstZone voidBurstZonePrefab;

    [Header("Void Burst Pattern")]
    [Min(1)] public int numberOfZones = 4;
    // Legacy field kept so older Prefab values remain serialized. No longer used for placement.
    [HideInInspector] public float distanceFromBoss = 2.2f;

    // NEW: Three placement patterns. The first zone still targets the Player.
    public enum BurstPattern
    {
        RandomScatter = 0,
        SurroundPlayer = 1,
        RiftLine = 2
    }

    [Header("Multiple Patterns (NEW)")]
    [Tooltip("ON: chon ngau nhien mot trong 3 kieu moi lan Boss tung Void Burst.")]
    public bool randomizePatternEachCast = true;
    [Tooltip("Dung kieu nay neu tat Randomize Pattern Each Cast.")]
    public BurstPattern fixedPattern = BurstPattern.RandomScatter;
    [Tooltip("Ban kinh cua cac vung bao vay Player. Su dung khi chon SurroundPlayer.")]
    [Min(0.5f)] public float surroundRadius = 3.6f;
    [Tooltip("Khoang cach giua cac tam vung tren duong thang. Su dung khi chon RiftLine.")]
    [Min(0.5f)] public float lineSpacing = 2.8f;
    [Tooltip("Hien thong tin kieu tan cong duoc chon trong Console.")]
    public bool logSelectedPattern = true;

    [Header("Random Zones Near Player (Existing)")]
    [Tooltip("BAT: vung dau tien luon nam ngay vi tri Player luc Boss bat dau gong.")]
    public bool spawnOneZoneOnPlayer = true;
    [Tooltip("Khoang cach nho nhat tu Player den tam cac vung con lai.")]
    [Min(0f)] public float minDistanceFromPlayer = 2.7f;
    [Tooltip("Khoang cach xa nhat tu Player den tam cac vung con lai.")]
    [Min(0.1f)] public float maxDistanceFromPlayer = 4.8f;
    [Tooltip("Khoang cach toi thieu giua TAM hai vung de tranh de len nhau.")]
    [Min(0f)] public float minDistanceBetweenZones = 2.5f;
    [Tooltip("So lan thu tim vi tri cho moi vung. Tang neu ban do chat.")]
    [Range(10, 200)] public int placementAttemptsPerZone = 80;

    [Header("Zone Size And Damage (Existing)")]
    [Min(0.1f)] public float zoneRadius = 1.25f;
    [Min(0.1f)] public float warningDuration = 1.5f;
    [Min(0f)] public float damagePerZone = 18f;

    [Header("Optional Map Limits (World Coordinates)")]
    [Tooltip("BAT neu ban do co bien gioi. Gioi han tam vung no ben trong map, cach ria map toi thieu zoneRadius.")]
    public bool limitZonesToMap = false;
    public Vector2 mapMin = new Vector2(-12f, -7f);
    public Vector2 mapMax = new Vector2(12f, 7f);

    [Header("Casting Intervals (Phase 2 Only)")]
    [Min(0f)] public float firstCastDelay = 2f;
    [Min(0.1f)] public float castInterval = 9f;

    [Header("Phase 2 Cast Animation")]
    [Tooltip("Animator tren ShadowTitan (khong phai VB_CrackFX / VB_ImpactFX).")]
    public Animator castAnimator;
    public string castTriggerName = "VoidBurst";
    [Tooltip("Che do cu: so giay Boss gong neu Stop Charge At Explosion = false.")]
    [Min(0.1f)] public float castWindupDuration = 1.5f;

    [Header("Stop Charging When Void Burst Explodes")]
    [Tooltip("BAT: Boss ngung gong khi het Warning Duration (luc vung no); hieu ung no van tiep tuc.")]
    public bool stopChargeAtExplosion = true;
    [Tooltip("Ten State Idle trong Animator ShadowTitan.controller.")]
    public string phase2IdleStateName = "ShadowTitan_Phase2_Idle";

    [Header("Legacy Cast Synchronization")]
    [Tooltip("Chi ap dung khi Stop Charge At Explosion = false: tinh ca explosion va aftermath vao thoi gian gong.")]
    public bool synchronizeZoneLifetimeWithCharge = true;
    [Tooltip("Chi ap dung khi Stop Charge At Explosion = false. Khong nen xoa VFX tai thoi diem no.")]
    public bool removeZonesWhenChargeEnds = false;

    [Header("Stand Still While Charging")]
    public bool lockMovementDuringWindup = true;
    public EnemyController movementController;
    public Rigidbody2D bossRigidbody;

    private Health bossHealth;
    private Transform player;
    private bool phase2Started;
    private bool warnedMissingPrefab;
    private bool warnedMissingTrigger;
    private bool warnedShortCharge;
    private bool warnedMissingIdle;
    private bool warnedMissingPlayer;
    private float castTimer;
    private float windupTimer;
    private bool isWindingUp;
    private bool movementLocked;
    private bool movementWasEnabled;
    private RigidbodyConstraints2D constraintsBeforeLock;
    private readonly List<VoidBurstZone> activeZones = new List<VoidBurstZone>();

    private void Awake()
    {
        bossHealth = GetComponent<Health>();
        if (phaseController == null) phaseController = GetComponent<BossPhase2Controller>();
        if (castAnimator == null) castAnimator = GetComponent<Animator>();
        if (castAnimator == null) castAnimator = GetComponentInChildren<Animator>();
        if (movementController == null) movementController = GetComponent<EnemyController>();
        if (bossRigidbody == null) bossRigidbody = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null) player = playerObject.transform;
        if (phaseController == null)
            Debug.LogWarning("BossVoidBurstAttack: BossPhase2Controller is missing!");
    }

    private void Update()
    {
        if (bossHealth == null || bossHealth.CurrentHealth <= 0f)
        {
            CancelCharge();
            return;
        }
        if (Time.timeScale <= 0f) return;
        if (phaseController == null || !phaseController.IsPhase2)
        {
            if (isWindingUp) CancelCharge();
            return;
        }

        if (!phase2Started)
        {
            phase2Started = true;
            castTimer = firstCastDelay;
            Debug.Log("Shadow Titan: Void Burst unlocked in Phase 2.");
        }

        if (voidBurstZonePrefab == null)
        {
            if (!warnedMissingPrefab)
            {
                warnedMissingPrefab = true;
                Debug.LogWarning("BossVoidBurstAttack: Assign VoidBurstZone_V2 prefab!");
            }
            if (isWindingUp) CancelCharge();
            return;
        }

        if (isWindingUp)
        {
            windupTimer -= Time.deltaTime;
            if (windupTimer <= 0f)
                FinishCharge();
            return;
        }

        castTimer -= Time.deltaTime;
        if (castTimer <= 0f)
            BeginCast();
    }

    private void BeginCast()
    {
        isWindingUp = true;

        // Key change: charging ends at detonation, not at the end of the VFX tail.
        // In this mode Cast Windup Duration does not override Warning Duration.
        windupTimer = stopChargeAtExplosion
            ? Mathf.Max(0.1f, warningDuration)
            : Mathf.Max(0.1f, castWindupDuration);

        LockBossMovement();
        TriggerCastAnimation();
        SpawnBurstPattern(); // Warning begins at the SAME moment as boss charge.
        Debug.Log("Shadow Titan: Void Burst charging; zones appear now.");
    }

    private void FinishCharge()
    {
        isWindingUp = false;

        if (stopChargeAtExplosion)
        {
            // DO NOT destroy zones here! They must explode and play their own
            // explosion + aftermath before VoidBurstZone destroys itself.
            ReturnToPhase2Idle();
            Debug.Log("Shadow Titan: Void Burst exploded; charge animation stopped.");
        }
        else if (removeZonesWhenChargeEnds)
        {
            ClearActiveZones(); // Optional legacy behaviour only.
        }

        UnlockBossMovement();
        castTimer = Mathf.Max(0.1f, castInterval);
    }

    private void CancelCharge()
    {
        bool wasCasting = isWindingUp;
        isWindingUp = false;
        ClearActiveZones();
        UnlockBossMovement();
        if (wasCasting && bossHealth != null && bossHealth.CurrentHealth > 0f)
            ReturnToPhase2Idle();
    }

    private void SpawnBurstPattern()
    {
        if (bossHealth == null || bossHealth.CurrentHealth <= 0f || voidBurstZonePrefab == null)
            return;

        // Players may be recreated by Retry. Find the current Player at CAST TIME.
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }
        if (player == null)
        {
            if (!warnedMissingPlayer)
            {
                warnedMissingPlayer = true;
                Debug.LogWarning("BossVoidBurstAttack: Cannot find GameObject with tag 'Player'. No zones spawned.");
            }
            return;
        }

        float zoneWarning = Mathf.Max(0.1f, warningDuration);
        if (!stopChargeAtExplosion && synchronizeZoneLifetimeWithCharge)
        {
            float vfxTail = Mathf.Max(0.05f, voidBurstZonePrefab.explosionDuration)
                            + Mathf.Max(0f, voidBurstZonePrefab.aftermathDuration);
            float availableWarning = windupTimer - vfxTail;
            if (availableWarning < 0.1f && !warnedShortCharge)
            {
                warnedShortCharge = true;
                Debug.LogWarning(
                    "BossVoidBurstAttack: Charge too short for explosion + aftermath. " +
                    "Increase Cast Windup Duration.");
            }
            zoneWarning = Mathf.Max(0.1f, Mathf.Min(zoneWarning, availableWarning));
        }

        activeZones.RemoveAll(zone => zone == null);

        // Snapshot once per cast: warning zones never chase a moving Player.
        Vector2 playerCenter = player.position;
        int requested = Mathf.Max(1, numberOfZones);
        List<Vector2> chosenCenters = new List<Vector2>(requested);

        if (spawnOneZoneOnPlayer)
        {
            // Intentionally exact, even if Player is next to a map edge.
            chosenCenters.Add(playerCenter);
        }

        BurstPattern pattern = randomizePatternEachCast
            ? (BurstPattern)Random.Range(0, 3)
            : fixedPattern;

        if (pattern == BurstPattern.SurroundPlayer)
            PlaceSurroundPattern(chosenCenters, playerCenter, requested);
        else if (pattern == BurstPattern.RiftLine)
            PlaceLinePattern(chosenCenters, playerCenter, requested);
        else
            FillWithRandomScatter(chosenCenters, playerCenter, requested);

        if (chosenCenters.Count < requested)
        {
            Debug.LogWarning("BossVoidBurstAttack: Could place only " + chosenCenters.Count +
                             "/" + requested + " zones. Check map bounds/spacing.");
        }
        if (logSelectedPattern)
        {
            Debug.Log("Shadow Titan Void Burst pattern: " + pattern +
                      "; zones=" + chosenCenters.Count +
                      "; one-on-Player=" + spawnOneZoneOnPlayer);
        }

        foreach (Vector2 center in chosenCenters)
        {
            Vector3 location = new Vector3(center.x, center.y, transform.position.z);
            VoidBurstZone zone = Instantiate(voidBurstZonePrefab, location, Quaternion.identity);
            zone.Configure(zoneRadius, damagePerZone, zoneWarning, bossHealth);
            activeZones.Add(zone);
        }
    }

    // Pattern 1: each remaining zone randomly scatters near the Player.
    private void FillWithRandomScatter(List<Vector2> centers, Vector2 origin, int requested)
    {
        while (centers.Count < requested)
        {
            Vector2 candidate;
            if (!TryFindScatterPoint(centers, origin, out candidate)) break;
            centers.Add(candidate);
        }
    }

    // Pattern 2: remaining zones form a broken ring around Player.
    // A random rotation makes each cast look different.
    private void PlaceSurroundPattern(List<Vector2> centers, Vector2 origin, int requested)
    {
        int needed = requested - centers.Count;
        if (needed <= 0) return;

        float startAngle = Random.Range(0f, Mathf.PI * 2f);
        float radius = Mathf.Max(0.5f, surroundRadius);
        for (int i = 0; i < needed; i++)
        {
            bool placed = false;
            float targetAngle = startAngle + 2f * Mathf.PI * i / needed;
            int attempts = Mathf.Clamp(placementAttemptsPerZone, 10, 200);
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                float angle = targetAngle + Random.Range(-0.18f, 0.18f);
                float r = Mathf.Max(0.5f, radius + Random.Range(-0.25f, 0.25f));
                Vector2 candidate = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
                if (!CanPlaceAt(candidate, centers)) continue;
                centers.Add(candidate);
                placed = true;
                break;
            }
            if (!placed)
            {
                // A tight map may not allow the ring. Try a nearby scatter point.
                Vector2 fallback;
                if (TryFindScatterPoint(centers, origin, out fallback))
                    centers.Add(fallback);
                else
                    break;
            }
        }
    }

    // Pattern 3: a rotated line of warning zones, crossing the Player location.
    // Index order: +1, -1, +2, -2...; the center is handled above.
    private void PlaceLinePattern(List<Vector2> centers, Vector2 origin, int requested)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        int needed = requested - centers.Count;
        int attempts = Mathf.Clamp(placementAttemptsPerZone, 10, 200);

        for (int i = 0; i < needed; i++)
        {
            int order = i + 1;
            int step = (order + 1) / 2;
            float signedStep = order % 2 == 1 ? step : -step;
            bool placed = false;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                float jitter = Random.Range(-0.12f, 0.12f);
                Vector2 candidate = origin + direction * signedStep * Mathf.Max(0.5f, lineSpacing)
                                             + perpendicular * jitter;
                if (!CanPlaceAt(candidate, centers)) continue;
                centers.Add(candidate);
                placed = true;
                break;
            }
            if (!placed)
            {
                Vector2 fallback;
                if (TryFindScatterPoint(centers, origin, out fallback))
                    centers.Add(fallback);
                else
                    break;
            }
        }
    }

    private bool TryFindScatterPoint(List<Vector2> centers, Vector2 origin, out Vector2 found)
    {
        float minimum = Mathf.Max(0f, minDistanceFromPlayer);
        float maximum = Mathf.Max(minimum, maxDistanceFromPlayer);
        int attempts = Mathf.Clamp(placementAttemptsPerZone, 10, 200);
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = Mathf.Sqrt(Random.Range(minimum * minimum, maximum * maximum));
            Vector2 candidate = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            if (!CanPlaceAt(candidate, centers)) continue;
            found = candidate;
            return true;
        }
        found = origin;
        return false;
    }

    private bool CanPlaceAt(Vector2 center, List<Vector2> placed)
    {
        if (!IsInsideMap(center, zoneRadius)) return false;
        float minimum = Mathf.Max(0f, minDistanceBetweenZones);
        for (int i = 0; i < placed.Count; i++)
            if (Vector2.Distance(center, placed[i]) < minimum)
                return false;
        return true;
    }

    private bool IsInsideMap(Vector2 center, float radius)
    {
        if (!limitZonesToMap) return true;
        float left = Mathf.Min(mapMin.x, mapMax.x) + radius;
        float right = Mathf.Max(mapMin.x, mapMax.x) - radius;
        float bottom = Mathf.Min(mapMin.y, mapMax.y) + radius;
        float top = Mathf.Max(mapMin.y, mapMax.y) - radius;
        return left <= right && bottom <= top &&
               center.x >= left && center.x <= right &&
               center.y >= bottom && center.y <= top;
    }

    private void ClearActiveZones()
    {
        foreach (VoidBurstZone zone in activeZones)
            if (zone != null) Destroy(zone.gameObject);
        activeZones.Clear();
    }

    private void LockBossMovement()
    {
        if (!lockMovementDuringWindup || movementLocked) return;
        movementLocked = true;

        if (movementController != null)
        {
            movementWasEnabled = movementController.enabled;
            movementController.enabled = false;
        }
        if (bossRigidbody != null)
        {
            constraintsBeforeLock = bossRigidbody.constraints;
            bossRigidbody.linearVelocity = Vector2.zero;
            bossRigidbody.angularVelocity = 0f;
            bossRigidbody.constraints = constraintsBeforeLock |
                RigidbodyConstraints2D.FreezePositionX |
                RigidbodyConstraints2D.FreezePositionY;
        }
    }

    private void UnlockBossMovement()
    {
        if (!movementLocked) return;
        movementLocked = false;
        if (bossRigidbody != null) bossRigidbody.constraints = constraintsBeforeLock;
        if (movementController != null)
            movementController.enabled = movementWasEnabled &&
                (bossHealth != null && bossHealth.CurrentHealth > 0f);
    }

    private void TriggerCastAnimation()
    {
        if (castAnimator == null || castAnimator.runtimeAnimatorController == null ||
            string.IsNullOrEmpty(castTriggerName)) return;

        bool found = false;
        foreach (AnimatorControllerParameter parameter in castAnimator.parameters)
        {
            if (parameter.name == castTriggerName &&
                parameter.type == AnimatorControllerParameterType.Trigger)
            {
                found = true;
                break;
            }
        }
        if (found) castAnimator.SetTrigger(castTriggerName);
        else if (!warnedMissingTrigger)
        {
            warnedMissingTrigger = true;
            Debug.LogWarning("BossVoidBurstAttack: Animator missing Trigger '" +
                             castTriggerName + "'. Skill still works.");
        }
    }

    private void ReturnToPhase2Idle()
    {
        if (castAnimator == null || castAnimator.runtimeAnimatorController == null ||
            string.IsNullOrEmpty(phase2IdleStateName)) return;

        // Prevent a pending trigger from re-entering the charge state.
        foreach (AnimatorControllerParameter parameter in castAnimator.parameters)
        {
            if (parameter.name == castTriggerName &&
                parameter.type == AnimatorControllerParameterType.Trigger)
            {
                castAnimator.ResetTrigger(castTriggerName);
                break;
            }
        }

        // Explicitly switch back to Idle now, even if the Attack -> Idle
        // transition's Exit Time has not been reached yet.
        string idlePath = "Base Layer." + phase2IdleStateName;
        int idleHash = Animator.StringToHash(idlePath);
        if (castAnimator.HasState(0, idleHash))
        {
            castAnimator.Play(idleHash, 0, 0f);
        }
        else if (!warnedMissingIdle)
        {
            warnedMissingIdle = true;
            Debug.LogWarning("BossVoidBurstAttack: Animator has no state '" +
                             idlePath + "'. Check Phase 2 Idle state name.");
        }
    }

    private void OnDisable() { CancelCharge(); }
    private void OnDestroy() { CancelCharge(); }
}
