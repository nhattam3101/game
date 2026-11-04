using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Health))]
public class RangedEnemyController : MonoBehaviour
{
    [Header("Movement")]
    [Min(0f)] public float moveSpeed = 1.8f;
    [Min(0.1f)] public float desiredDistance = 4f;
    [Min(0f)] public float distanceTolerance = 0.7f;

    [Header("Ranged Attack")]
    public EnemyProjectile projectilePrefab;
    public Transform firePoint;
    [Min(0.1f)] public float attackRange = 7f;
    [Min(0f)] public float projectileDamage = 8f;
    [Min(0.1f)] public float attackInterval = 2f;

    [Header("Animation (Optional)")]
    [Tooltip("Animator co Trigger Attack. De trong neu Animator nam tren GameObject nay hoac con.")]
    public Animator attackAnimator;

    [Header("Rewards")]
    [Min(0)] public int expReward = 8;
    [Min(0)] public int coinReward = 4;

    private Rigidbody2D rb;
    private Health enemyHealth;
    private Health playerHealth;
    private EnemySlowEffect slowEffect;
    private Transform player;
    private float attackTimer;
    private bool hasAttackTrigger;
    private bool isDead;
    private bool rewardGiven;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        enemyHealth = GetComponent<Health>();
        slowEffect = GetComponent<EnemySlowEffect>();

        // Tim Animator neu khong duoc gan tu Inspector.
        if (attackAnimator == null)
            attackAnimator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        if (navigation == null) navigation = FindFirstObjectByType<MapPathfinding2D>();
        if (obstacleLayers.value == 0 && navigation != null)
            obstacleLayers = navigation.obstacleLayers;
        if (requireClearShot && obstacleLayers.value == 0)
            Debug.LogWarning("RangedEnemy: Chua chon Obstacles layer; line-of-sight tam duoc bo qua.", this);

        // Chỉ gửi Trigger nếu Animator có tham số Attack hợp lệ.
        if (attackAnimator != null &&
            attackAnimator.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter parameter in attackAnimator.parameters)
            {
                if (parameter.name == "Attack" &&
                    parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    hasAttackTrigger = true;
                    break;
                }
            }
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
            playerHealth = playerObject.GetComponent<Health>();
        }
        else
        {
            Debug.LogWarning("RangedEnemy: Khong tim thay Player!");
        }

        enemyHealth.OnDied += Die;
        // De nguoi choi thay quai truoc khi vien dan dau tien xuat hien.
        attackTimer = attackInterval;
    }

    // 4B.5: Optional advanced navigation. Keep existing movement/attack values.
    [Header("4B.5 - Pathfinding / Line of Sight")]
    public MapPathfinding2D navigation; // Scene object; auto-found when spawned.
    public LayerMask obstacleLayers; // Obstacles ONLY; do not include Player / Enemy.
    public bool requireClearShot = true;
    [Min(0.2f)] public float pathRefreshInterval = 0.75f;
    [Min(0.3f)] public float repositionRefreshInterval = 1.2f;
    [Min(0.1f)] public float navigationWaypointRadius = 0.32f;
    [Min(4)] public int firingPositionSamples = 12;

    private readonly System.Collections.Generic.List<Vector2> currentRoute =
        new System.Collections.Generic.List<Vector2>();
    private int routeIndex;
    private float nextRouteTime;
    private Vector2 routeGoal;
    private float nextRepositionTime;
    private Vector2 repositionGoal;
    private bool hasRepositionGoal;

    private void Update()
    {
        if (Time.timeScale == 0f || isDead || player == null ||
            playerHealth == null || playerHealth.CurrentHealth <= 0f)
            return;

        attackTimer -= Time.deltaTime;
        float distance = Vector2.Distance(transform.position, player.position);
        if (distance <= attackRange && attackTimer <= 0f &&
            (!requireClearShot || HasClearShot()))
        {
            ShootAtPlayer();
            attackTimer = attackInterval;
        }
    }

    private void FixedUpdate()
    {
        if (Time.timeScale == 0f || isDead || player == null ||
            playerHealth == null || playerHealth.CurrentHealth <= 0f)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 toPlayer = (Vector2)player.position - rb.position;
        float distance = toPlayer.magnitude;
        if (distance < 0.0001f)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float multiplier = slowEffect != null ? slowEffect.SpeedMultiplier : 1f;
        float effectiveSpeed = moveSpeed * multiplier;
        if (effectiveSpeed <= 0f)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 toward = toPlayer.normalized;
        Vector2 goal;
        bool move = false;
        float tooClose = Mathf.Max(0f, desiredDistance - distanceTolerance);
        float tooFar = desiredDistance + distanceTolerance;

        if (distance < tooClose)
        {
            // Retreat when Player is too close; navigate around rocks while retreating.
            goal = rb.position - toward * Mathf.Max(1.5f, tooClose - distance + 1.2f);
            move = true;
            hasRepositionGoal = false;
        }
        else if (distance > tooFar)
        {
            // Approach first; A* helps if a wall separates the enemy and Player.
            goal = (Vector2)player.position - toward * desiredDistance;
            move = true;
            hasRepositionGoal = false;
        }
        else if (requireClearShot && !HasClearShot())
        {
            // In shooting range but hidden behind cover: move to a firing lane.
            if (!hasRepositionGoal || Time.time >= nextRepositionTime)
            {
                hasRepositionGoal = FindFiringPosition(out repositionGoal);
                nextRepositionTime = Time.time + repositionRefreshInterval;
            }
            goal = repositionGoal;
            move = hasRepositionGoal;
        }
        else
        {
            // Good range and line of sight: stop and shoot, as before.
            rb.linearVelocity = Vector2.zero;
            currentRoute.Clear();
            routeIndex = 0;
            hasRepositionGoal = false;
            return;
        }

        if (!move)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }
        Vector2 moveDirection = GetNavigatedDirection(goal);
        rb.linearVelocity = moveDirection * effectiveSpeed;
    }

    private bool HasClearShot()
    {
        if (obstacleLayers.value == 0) return true;
        Vector2 from = firePoint != null ? (Vector2)firePoint.position : rb.position;
        Vector2 to = player != null ? (Vector2)player.position : from;
        Vector2 diff = to - from;
        if (diff.sqrMagnitude < 0.0001f) return true;
        // Thin line-of-sight for the projectile (unlike body-clearance path checks).
        return Physics2D.CircleCast(from, 0.06f, diff.normalized, diff.magnitude,
                                    obstacleLayers).collider == null;
    }

    private bool FindFiringPosition(out Vector2 found)
    {
        found = rb.position;
        if (player == null) return false;
        float bestScore = float.MaxValue;
        bool success = false;
        int samples = Mathf.Max(4, firingPositionSamples);
        Vector2 center = player.position;
        float desired = Mathf.Min(desiredDistance, attackRange - 0.4f);
        desired = Mathf.Max(0.5f, desired);
        for (int i = 0; i < samples; i++)
        {
            float angle = (Mathf.PI * 2f * i / samples);
            Vector2 candidate = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * desired;
            if (navigation != null && !navigation.IsWalkable(candidate)) continue;
            if (obstacleLayers.value != 0 &&
                Physics2D.OverlapCircle(candidate, 0.36f, obstacleLayers) != null)
                continue;
            Vector2 diff = center - candidate;
            if (obstacleLayers.value != 0 &&
                Physics2D.CircleCast(candidate, 0.06f, diff.normalized, diff.magnitude,
                                     obstacleLayers).collider != null) continue;
            float score = (candidate - rb.position).sqrMagnitude;
            if (score < bestScore)
            {
                bestScore = score;
                found = candidate;
                success = true;
            }
        }
        return success;
    }

    private Vector2 GetNavigatedDirection(Vector2 goal)
    {
        Vector2 toGoal = goal - rb.position;
        if (toGoal.sqrMagnitude < 0.1f * 0.1f) return Vector2.zero;
        if (navigation == null || navigation.obstacleLayers.value == 0)
            return toGoal.normalized; // Legacy fallback when no navigation is configured.

        if (!navigation.IsBlocked(rb.position, goal))
        {
            currentRoute.Clear();
            routeIndex = 0;
            return toGoal.normalized;
        }

        bool destinationChanged = (routeGoal - goal).sqrMagnitude > 1f;
        if (Time.time >= nextRouteTime &&
            (destinationChanged || currentRoute.Count == 0 || routeIndex >= currentRoute.Count))
        {
            navigation.TryFindPath(rb.position, goal, currentRoute);
            routeGoal = goal;
            routeIndex = 0;
            nextRouteTime = Time.time + pathRefreshInterval;
        }

        while (routeIndex < currentRoute.Count &&
               (currentRoute[routeIndex] - rb.position).sqrMagnitude <=
                   navigationWaypointRadius * navigationWaypointRadius)
            routeIndex++;

        if (routeIndex < currentRoute.Count)
        {
            Vector2 step = currentRoute[routeIndex] - rb.position;
            return step.sqrMagnitude > 0.0001f ? step.normalized : Vector2.zero;
        }
        return Vector2.zero; // Never push straight into a known wall on failed path.
    }

    private void ShootAtPlayer()
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("RangedEnemy: Chua gan EnemyProjectile prefab!");
            return;
        }

        Vector2 direction = (Vector2)(player.position - transform.position);
        if (direction.sqrMagnitude < 0.0001f)
            return;

        direction.Normalize();

        Vector3 spawnPosition = firePoint != null
            ? firePoint.position
            : transform.position + (Vector3)(direction * 0.45f);

        EnemyProjectile projectile = Instantiate(
            projectilePrefab, spawnPosition, Quaternion.identity
        );

        projectile.Initialize(direction, projectileDamage, enemyHealth);

        // Ban dan thanh cong: phat hoat anh tan cong.
        if (hasAttackTrigger && attackAnimator != null)
            attackAnimator.SetTrigger("Attack");
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        rb.linearVelocity = Vector2.zero;

        if (!rewardGiven)
        {
            rewardGiven = true;
            GiveRewards();
        }
        // Health.cs trong du an se xu ly hieu ung chet va huy quai.
    }

    private void GiveRewards()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
            return;

        PlayerProgression progression =
            playerObject.GetComponent<PlayerProgression>();
        if (progression == null)
        {
            Debug.LogWarning("RangedEnemy: Player thieu PlayerProgression!");
            return;
        }

        if (expReward > 0)
            progression.AddExp(expReward);

        if (coinReward > 0)
            progression.AddCoins(coinReward);

        Debug.Log(gameObject.name + " thuong " + expReward +
                  " EXP va " + coinReward + " Gold.");
    }

    private void OnDestroy()
    {
        if (enemyHealth != null)
            enemyHealth.OnDied -= Die;
    }
}
