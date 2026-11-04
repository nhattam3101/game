using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 5B.1 - REPLACE ONLY the old GraveWardenBossAI.cs (same class, same prefab).
// Do not place another script with the same class in Assets.
[RequireComponent(typeof(Health), typeof(Rigidbody2D))]
[DisallowMultipleComponent]
public class GraveWardenBossAI : MonoBehaviour
{
    [Header("Movement - preserved from 5A")]
    [Min(0f)] public float moveSpeed = 1.5f;
    [Min(0.5f)] public float stopDistance = 2.2f;
    [Min(0.1f)] public float repathInterval = 0.85f;
    public MapPathfinding2D navigation;

    [Header("New combat director")]
    [Min(0f)] public float firstAttackDelay = 2.5f;
    [Min(0.5f)] public float minimumTimeBetweenCasts = 1.5f;
    [Range(0.1f, 0.9f)] public float phaseTwoThreshold = 0.5f;
    [Min(0.2f)] public float phaseTwoTransitionSeconds = 1.5f;
    [Min(0.1f)] public float summonCooldown = 12f;
    [Min(0.1f)] public float tombWallCooldown = 14f;
    [Min(0.1f)] public float spiritOrbitCooldown = 18f;
    [Min(0.1f)] public float reanimateCooldown = 20f;
    [Range(0.25f, 1f)] public float phaseTwoCooldownFactor = 0.83f;
    [Min(0.2f)] public float summonCastSeconds = 1f;
    [Min(0.2f)] public float orbitCastSeconds = 0.8f;
    [Min(0.2f)] public float reanimateCastSeconds = 1.2f;
    [Header("Movement Lock During Skills - 5B.1 Patch")]
    [Tooltip("Stop the boss while summoning Wraiths.")]
    public bool stopDuringSummonWraiths = true;
    [Tooltip("Stop the boss while casting Tomb Wall, including the warning delay.")]
    public bool stopDuringTombWall = true;
    [Tooltip("Stop the boss while activating Spirit Orbit. Uncheck to allow walking while casting.")]
    public bool stopDuringSpiritOrbit = false;
    [Tooltip("Stop the boss while reanimating fallen minions.")]
    public bool stopDuringReanimateFallen = true;
    public bool verboseLog = true;

    private Health health;
    private Rigidbody2D body;
    private Transform player;
    private Health playerHealth;
    private GraveWardenTombWall5B1 tombWall;
    private GraveWardenSummons5B1 summons;
    private GraveWardenSpiritOrbit5B1 orbit;
    private GraveWardenSoulHarvest5B1 harvest;
    private GraveWardenAnimatorBridge5B1 visuals;

    private readonly List<Vector2> path = new List<Vector2>();
    private float nextPathTime;
    private int waypointIndex;
    private float nextSummon, nextWall, nextOrbit, nextReanimate;
    private bool dead, phaseTwo, pendingPhaseTwo, casting, movementLocked;
    private float lastObservedHealth;
    private Coroutine combatRoutine;

    public bool IsDead { get { return dead; } }
    public bool IsPhaseTwo { get { return phaseTwo; } }
    public Health BossHealth { get { return health; } }
    public Transform Player { get { return player; } }
    public Health PlayerHealth { get { return playerHealth; } }

    private void Awake()
    {
        health = GetComponent<Health>();
        lastObservedHealth = health.CurrentHealth;
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        if (navigation == null) navigation = FindAnyObjectByType<MapPathfinding2D>();
        tombWall = GetComponent<GraveWardenTombWall5B1>();
        summons = GetComponent<GraveWardenSummons5B1>();
        orbit = GetComponent<GraveWardenSpiritOrbit5B1>();
        harvest = GetComponent<GraveWardenSoulHarvest5B1>();
        visuals = GetComponent<GraveWardenAnimatorBridge5B1>();
    }

    private void OnEnable()
    {
        if (health == null) health = GetComponent<Health>();
        health.OnDied += OnBossDied;
        health.OnHealthChanged += OnHealthChanged;
    }

    private void Start()
    {
        FindPlayer();
        if (visuals != null) visuals.Trigger("Spawn_Intro");
        if (summons == null || tombWall == null || orbit == null || harvest == null)
            Debug.LogError("[GraveWarden 5B1] Missing one or more 5B1 components on Boss prefab. See README.", this);
        float start = Time.time + firstAttackDelay;
        nextSummon = nextWall = nextOrbit = nextReanimate = start;
        combatRoutine = StartCoroutine(CombatDirector());
    }

    private void FindPlayer()
    {
        GameObject obj = GameObject.FindGameObjectWithTag("Player");
        if (obj == null) return;
        player = obj.transform;
        playerHealth = obj.GetComponent<Health>();
    }

    private bool PlayerAlive()
    {
        return player != null && playerHealth != null && playerHealth.CurrentHealth > 0f;
    }

    private void OnHealthChanged(float current, float maximum)
    {
        if (current > 0f && current < lastObservedHealth && !casting && !pendingPhaseTwo && visuals != null)
            visuals.Trigger("Hit");
        lastObservedHealth = current;
        if (!dead && !phaseTwo && !pendingPhaseTwo && current > 0f && maximum > 0f &&
            current <= maximum * phaseTwoThreshold)
            pendingPhaseTwo = true;
    }

    private void Update()
    {
        if (visuals != null)
            visuals.SetWalking(!dead && !movementLocked && body.linearVelocity.sqrMagnitude > 0.02f, phaseTwo);
    }

    private void FixedUpdate()
    {
        if (dead || movementLocked || pendingPhaseTwo || Time.timeScale == 0f || !PlayerAlive())
        {
            body.linearVelocity = Vector2.zero;
            return;
        }
        Vector2 here = body.position;
        Vector2 target = player.position;
        if (Vector2.Distance(here, target) <= stopDistance)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }
        Vector2 goal = target;
        if (navigation != null && navigation.obstacleLayers.value != 0 &&
            navigation.IsBlocked(here, target, 0.6f))
        {
            if (Time.time >= nextPathTime)
            {
                navigation.TryFindPath(here, target, path);
                waypointIndex = 0;
                nextPathTime = Time.time + repathInterval;
            }
            while (waypointIndex < path.Count &&
                   Vector2.Distance(here, path[waypointIndex]) < 0.35f)
                waypointIndex++;
            if (waypointIndex >= path.Count)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }
            goal = path[waypointIndex];
        }
        else path.Clear();
        Vector2 delta = goal - here;
        body.linearVelocity = delta.sqrMagnitude > 0.001f ? delta.normalized * moveSpeed : Vector2.zero;
    }

    private IEnumerator CombatDirector()
    {
        yield return new WaitForSeconds(firstAttackDelay);
        while (!dead)
        {
            if (!PlayerAlive())
            {
                if (player == null) FindPlayer();
                yield return new WaitForSeconds(0.3f);
                continue;
            }
            if (pendingPhaseTwo)
            {
                BeginCast(true); // Always freeze during Phase 2 transformation.
                if (visuals != null) visuals.Trigger("Phase2_Transform");
                yield return new WaitForSeconds(phaseTwoTransitionSeconds);
                if (dead) yield break;
                phaseTwo = true;
                pendingPhaseTwo = false;
                EndCast();
                if (verboseLog) Debug.Log("[GraveWarden 5B1] PHASE 2!", this);
                continue;
            }

            int skill = SelectReadySkill();
            if (skill < 0)
            {
                yield return new WaitForSeconds(0.2f);
                continue;
            }
            // 'casting' prevents overlapping attacks; 'movementLocked' is configurable
            // independently so movement is allowed for selected skills.
            bool freezeForThisSkill =
                skill == 0 ? stopDuringSummonWraiths :
                skill == 1 ? stopDuringTombWall :
                skill == 2 ? stopDuringSpiritOrbit :
                             stopDuringReanimateFallen;
            BeginCast(freezeForThisSkill);
            bool empowered = phaseTwo && harvest != null && harvest.TrySpendForEmpower();
            if (empowered && visuals != null) visuals.Trigger("Empowered_Cast");
            switch (skill)
            {
                case 0:
                    if (visuals != null) visuals.Trigger("Wraith_Prepare");
                    yield return new WaitForSeconds(summonCastSeconds);
                    if (!dead && PlayerAlive())
                    {
                        if (visuals != null) visuals.Trigger("Wraith_Summon");
                        summons.SpawnWraiths(phaseTwo, empowered);
                    }
                    nextSummon = Time.time + ScaledCooldown(summonCooldown);
                    break;
                case 1:
                    if (visuals != null) visuals.Trigger("TombWall_Prepare");
                    yield return tombWall.CastWalls(phaseTwo, empowered);
                    nextWall = Time.time + ScaledCooldown(tombWallCooldown);
                    break;
                case 2:
                    if (visuals != null) visuals.Trigger("SpiritOrbit_Cast");
                    yield return new WaitForSeconds(orbitCastSeconds);
                    if (!dead && PlayerAlive()) orbit.Activate(phaseTwo, empowered);
                    nextOrbit = Time.time + ScaledCooldown(spiritOrbitCooldown);
                    break;
                case 3:
                    if (visuals != null) visuals.Trigger("Reanimate_Cast");
                    yield return new WaitForSeconds(reanimateCastSeconds);
                    if (!dead && PlayerAlive())
                    {
                        if (visuals != null) visuals.Trigger("Reanimate_Rise");
                        summons.Reanimate(empowered);
                    }
                    nextReanimate = Time.time + ScaledCooldown(reanimateCooldown);
                    break;
            }
            EndCast();
            if (dead) yield break;
            yield return new WaitForSeconds(minimumTimeBetweenCasts);
        }
    }

    // Movement lock only changes Rigidbody2D velocity, never disables the
    // GameObject, movement component, Animator, Health, or Pathfinding.
    private void BeginCast(bool freezeMovement)
    {
        casting = true;
        movementLocked = freezeMovement;
        if (movementLocked && body != null)
            body.linearVelocity = Vector2.zero;
    }

    private void EndCast()
    {
        movementLocked = false;
        casting = false;
    }

    private float ScaledCooldown(float value)
    {
        return value * (phaseTwo ? phaseTwoCooldownFactor : 1f);
    }

    // Deterministic priority is changed after each cast by the cooldowns.
    // Phase 1: Summon > Wall > Orbit; Phase 2: Reanimate > Wall > Orbit > Summon.
    private int SelectReadySkill()
    {
        if (phaseTwo && Time.time >= nextReanimate && summons != null && summons.CanReanimate()) return 3;
        if (Time.time >= nextSummon && summons != null && summons.CanSummonWraiths()) return 0;
        if (Time.time >= nextWall && tombWall != null && tombWall.CanCast()) return 1;
        if (Time.time >= nextOrbit && orbit != null && orbit.CanActivate()) return 2;
        return -1;
    }

    public void TriggerAnimation(string trigger)
    {
        if (visuals != null) visuals.Trigger(trigger);
    }

    private void OnBossDied()
    {
        dead = true;
        movementLocked = false;
        casting = false;
        body.linearVelocity = Vector2.zero;
        if (combatRoutine != null) StopCoroutine(combatRoutine);
        if (tombWall != null) tombWall.ClearAll();
        if (orbit != null) orbit.ClearAll();
        if (summons != null) summons.ClearAll();
        if (visuals != null) visuals.Trigger("Death");
        // IMPORTANT: Health.OnDied / WaveManager own Victory; never invoke Victory here.
    }

    private void OnDisable()
    {
        movementLocked = false;
        casting = false;
        if (health != null)
        {
            health.OnDied -= OnBossDied;
            health.OnHealthChanged -= OnHealthChanged;
        }
        if (tombWall != null) tombWall.ClearAll();
        if (orbit != null) orbit.ClearAll();
        if (summons != null) summons.ClearAll();
    }
}
