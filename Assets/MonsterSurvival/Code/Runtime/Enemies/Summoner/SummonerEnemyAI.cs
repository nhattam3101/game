using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Gắn lên Prefab SummonerEnemy (bản sao NormalEnemy).
// KHÔNG thay EnemyController, Health, Slow, HitEffects.
[RequireComponent(typeof(Health))]
public class SummonerEnemyAI : MonoBehaviour
{
    [Header("References")]
    public GameObject minionPrefab;
    public Transform summonPoint;
    public Animator summonerAnimator;
    public EnemyController enemyMovement;
    public EnemySpawner enemySpawner;
    // Resolve from scene at runtime; do NOT assign scene object to prefab asset.
    private MapSafeSpawn2D safeSpawn;

    [Header("Summon Settings")]
    [Min(1f)] public float summonInterval = 8f;
    [Min(0.05f)] public float castDuration = 0.8f;
    [Min(1)] public int minionsPerCast = 2;
    [Min(1)] public int maxActiveMinions = 4;
    [Min(0.2f)] public float minSpawnRadius = 1f;
    [Min(0.3f)] public float maxSpawnRadius = 2.5f;
    [Min(0f)] public float minDistanceFromPlayer = 1.4f;
    [Min(1)] public int placementAttempts = 16;

    [Header("Safety Limits")]
    public bool freezeDuringCast = true;
    public bool obeyGlobalEnemyLimit = true;
    [Min(0)] public int extraMinionAllowance = 4;
    [Min(1f)] public float minionLifetime = 18f;
    public LayerMask blockedLayers; // Chỉ chọn tường / vật cản (không chọn Enemy).
    [Min(0f)] public float clearanceRadius = 0.35f;

    [Header("Animator")]
    public string summonTrigger = "Summon";
    public string hitTrigger = "Hit";
    public bool animateHit = true;
    public bool verboseLog = false;

    private Health selfHealth;
    private Rigidbody2D body;
    private Transform player;
    private float lastHP;
    private float summonTimer;
    private bool dead;
    private bool casting;
    private bool movementWasEnabled;
    private RigidbodyConstraints2D constraintsBeforeCast;
    private Coroutine castCoroutine;
    private bool hasSummonTrigger;
    private bool hasHitTrigger;
    private readonly List<SummonedMinion> activeMinions = new List<SummonedMinion>();

    public int ActiveMinionCount
    {
        get { activeMinions.RemoveAll(item => item == null); return activeMinions.Count; }
    }

    private void Awake()
    {
        selfHealth = GetComponent<Health>();
        body = GetComponent<Rigidbody2D>();
        if (enemyMovement == null) enemyMovement = GetComponent<EnemyController>();
        if (summonerAnimator == null) summonerAnimator = GetComponentInChildren<Animator>();
        if (summonPoint == null) summonPoint = transform;
        if (enemySpawner == null) enemySpawner = FindFirstObjectByType<EnemySpawner>();
        safeSpawn = FindFirstObjectByType<MapSafeSpawn2D>();
        CacheAnimatorParameters();
        summonTimer = summonInterval;
        lastHP = selfHealth.CurrentHealth;
    }

    private void Start()
    {
        GameObject obj = GameObject.FindGameObjectWithTag("Player");
        if (obj != null) player = obj.transform;
        if (minionPrefab == null) Debug.LogWarning(name + ": Chưa gắn Minion Prefab.");
    }

    private void OnEnable()
    {
        if (selfHealth == null) selfHealth = GetComponent<Health>();
        if (selfHealth != null)
        {
            selfHealth.OnDied += OnSummonerDied;
            selfHealth.OnHealthChanged += OnHealthChanged;
            lastHP = selfHealth.CurrentHealth;
        }
    }

    private void OnDisable()
    {
        if (selfHealth != null)
        {
            selfHealth.OnDied -= OnSummonerDied;
            selfHealth.OnHealthChanged -= OnHealthChanged;
        }
        if (castCoroutine != null)
        {
            StopCoroutine(castCoroutine);
            castCoroutine = null;
        }
        EndCastMovementLock();
    }

    private void OnDestroy()
    {
        // Minion không tồn tại mãi sau khi Summoner biến mất.
        for (int i = activeMinions.Count - 1; i >= 0; i--)
            if (activeMinions[i] != null) activeMinions[i].DespawnFromOwner();
        activeMinions.Clear();
    }

    private void OnSummonerDied()
    {
        dead = true;
        // Dừng chiêu: OnDestroy sẽ dọn minion còn sống.
        if (castCoroutine != null)
        {
            StopCoroutine(castCoroutine);
            castCoroutine = null;
        }
        EndCastMovementLock();
    }

    private void OnHealthChanged(float hp, float maxHp)
    {
        if (animateHit && !dead && hp > 0f && hp < lastHP && hasHitTrigger && summonerAnimator != null)
            summonerAnimator.SetTrigger(hitTrigger);
        lastHP = hp;
    }

    private void Update()
    {
        if (dead || Time.timeScale == 0f || casting || minionPrefab == null) return;
        if (selfHealth == null || selfHealth.CurrentHealth <= 0f) return;
        if (player == null)
        {
            GameObject obj = GameObject.FindGameObjectWithTag("Player");
            if (obj != null) player = obj.transform;
            if (player == null) return;
        }
        summonTimer -= Time.deltaTime;
        if (summonTimer > 0f) return;
        summonTimer = Mathf.Max(1f, summonInterval);
        activeMinions.RemoveAll(item => item == null);
        if (activeMinions.Count >= maxActiveMinions) return;
        castCoroutine = StartCoroutine(CastAndSummon());
    }

    private IEnumerator CastAndSummon()
    {
        casting = true;
        BeginCastMovementLock();
        if (hasSummonTrigger && summonerAnimator != null)
            summonerAnimator.SetTrigger(summonTrigger);
        if (verboseLog) Debug.Log(name + ": Bắt đầu triệu hồi.");

        yield return new WaitForSeconds(Mathf.Max(0.05f, castDuration));
        if (!dead && selfHealth != null && selfHealth.CurrentHealth > 0f)
            SpawnMinions();
        EndCastMovementLock();
        casting = false;
        castCoroutine = null;
    }

    private void BeginCastMovementLock()
    {
        if (!freezeDuringCast) return;
        movementWasEnabled = enemyMovement != null && enemyMovement.enabled;
        if (enemyMovement != null) enemyMovement.enabled = false;
        if (body != null)
        {
            constraintsBeforeCast = body.constraints;
            body.linearVelocity = Vector2.zero;
            body.constraints |= RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezePositionY;
        }
    }

    private void EndCastMovementLock()
    {
        if (!casting || !freezeDuringCast) return;
        if (body != null) body.constraints = constraintsBeforeCast;
        if (!dead && enemyMovement != null && movementWasEnabled) enemyMovement.enabled = true;
        casting = false;
    }

    private void SpawnMinions()
    {
        activeMinions.RemoveAll(item => item == null);
        int available = Mathf.Min(minionsPerCast, maxActiveMinions - activeMinions.Count);
        if (available <= 0) return;
        Vector2 origin = summonPoint != null ? (Vector2)summonPoint.position : (Vector2)transform.position;
        for (int i = 0; i < available; i++)
        {
            if (obeyGlobalEnemyLimit && enemySpawner != null &&
                GameObject.FindGameObjectsWithTag("Enemy").Length >= enemySpawner.maxEnemies + extraMinionAllowance)
                break;

            Vector2 position;
            if (!TryFindValidPosition(origin, out position)) continue;
            GameObject obj = Instantiate(minionPrefab, position, Quaternion.identity);
            SummonedMinion minion = obj.GetComponent<SummonedMinion>();
            if (minion == null) minion = obj.AddComponent<SummonedMinion>();
            minion.Initialize(this, minionLifetime);
            activeMinions.Add(minion);
            if (verboseLog) Debug.Log(name + ": Đã tạo " + obj.name);
        }
    }

    private bool TryFindValidPosition(Vector2 origin, out Vector2 result)
    {
        float maxR = Mathf.Max(minSpawnRadius, maxSpawnRadius);
        for (int attempt = 0; attempt < placementAttempts; attempt++)
        {
            Vector2 candidate = origin + Random.insideUnitCircle.normalized * Random.Range(minSpawnRadius, maxR);
            if (safeSpawn != null)
            {
                LayerMask combined = safeSpawn.obstacleLayers.value | blockedLayers.value;
                if (!safeSpawn.IsSafe(candidate, player, minDistanceFromPlayer,
                    clearanceRadius, combined)) continue;
            }
            else
            {
                if (player != null && Vector2.Distance(candidate, player.position) < minDistanceFromPlayer) continue;
                if (blockedLayers.value != 0 &&
                    Physics2D.OverlapCircle(candidate, clearanceRadius, blockedLayers) != null) continue;
            }
            result = candidate;
            return true;
        }
        result = origin;
        return false;
    }

    private void CacheAnimatorParameters()
    {
        if (summonerAnimator == null || summonerAnimator.runtimeAnimatorController == null) return;
        foreach (AnimatorControllerParameter p in summonerAnimator.parameters)
        {
            if (p.type != AnimatorControllerParameterType.Trigger) continue;
            if (p.name == summonTrigger) hasSummonTrigger = true;
            if (p.name == hitTrigger) hasHitTrigger = true;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.7f, 0.2f, 1f, 0.8f);
        Gizmos.DrawWireSphere(summonPoint != null ? summonPoint.position : transform.position, maxSpawnRadius);
    }
}
