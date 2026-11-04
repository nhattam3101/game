using UnityEngine;

// MonsterSurvival 4B.4: lightweight avoidance for the existing EnemyController.
// Does not edit EnemyController, Health, attack, rewards or Slow.
// This is LOCAL steering for small, separated obstacles, NOT full A* pathfinding.
// Add to Normal / Fast / Tank / Healer / Summoner / Minion prefabs that use EnemyController.
[RequireComponent(typeof(Rigidbody2D))]
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public class EnemyObstacleAvoidance2D : MonoBehaviour
{
    [Header("Existing components")]
    public EnemyController enemyMovement;
    public Rigidbody2D body;

    [Header("Obstacle detection")]
    public LayerMask obstacleLayers;
    [Min(0.05f)] public float bodyClearance = 0.28f;
    [Min(0.25f)] public float lookAhead = 1.3f;
    [Min(0.04f)] public float scanInterval = 0.12f;

    [Header("Steering")]
    [Range(0f, 1f)] public float outwardBias = 0.28f;
    [Min(0.1f)] public float sideHoldSeconds = 0.7f;
    public bool showGizmos = false;

    private Transform player;
    private float nextScanTime;
    private float holdUntil;
    private int preferredSide = 0;
    private Vector2 steerDirection;
    private bool avoiding;

    private void Awake()
    {
        if (enemyMovement == null) enemyMovement = GetComponent<EnemyController>();
        if (body == null) body = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        GameObject obj = GameObject.FindGameObjectWithTag("Player");
        if (obj != null) player = obj.transform;
        // Collision layers should only contain walls / rocks / trees, NOT Enemy / Ground / Player.
        if (obstacleLayers.value == 0)
            Debug.LogWarning(name + ": EnemyObstacleAvoidance2D - assign Obstacles layer in Inspector.", this);
    }

    private void FixedUpdate()
    {
        if (Time.timeScale == 0f || body == null || enemyMovement == null || !enemyMovement.enabled)
        {
            avoiding = false;
            return;
        }

        // EnemyController's FixedUpdate has already calculated speed (including Slow).
        // Only change the movement DIRECTION while preserving its speed.
        Vector2 originalVelocity = body.linearVelocity;
        float speed = originalVelocity.magnitude;
        if (speed < 0.01f || obstacleLayers.value == 0)
        {
            avoiding = false;
            return;
        }

        if (player == null)
        {
            GameObject obj = GameObject.FindGameObjectWithTag("Player");
            if (obj != null) player = obj.transform;
            if (player == null) return;
        }

        Vector2 goal = (Vector2)player.position - body.position;
        if (goal.sqrMagnitude < 0.0001f) return;
        Vector2 towardGoal = goal.normalized;

        if (Time.fixedTime >= nextScanTime)
        {
            nextScanTime = Time.fixedTime + Mathf.Max(0.04f, scanInterval);
            RecalculateSteering(towardGoal);
        }

        if (avoiding)
        {
            // Optional component does not change EnemyController's combat/rewards.
            body.linearVelocity = steerDirection * speed;
        }
    }

    private void RecalculateSteering(Vector2 toPlayer)
    {
        float searchDistance = Mathf.Max(lookAhead, bodyClearance * 2f);
        RaycastHit2D hit = Physics2D.CircleCast(
            body.position, bodyClearance, toPlayer,
            searchDistance, obstacleLayers);

        if (hit.collider == null)
        {
            avoiding = false;
            preferredSide = 0;
            return;
        }

        Vector2 normal = hit.normal;
        if (normal.sqrMagnitude < 0.001f)
            normal = -toPlayer;
        normal.Normalize();

        Vector2 tangent = new Vector2(-normal.y, normal.x);
        // Select a side once and keep it briefly to reduce left/right jitter.
        if (preferredSide == 0 || Time.fixedTime >= holdUntil)
        {
            Vector2 positive = (tangent + normal * outwardBias).normalized;
            Vector2 negative = (-tangent + normal * outwardBias).normalized;
            float scorePositive = Vector2.Dot(positive, toPlayer);
            float scoreNegative = Vector2.Dot(negative, toPlayer);
            // Check available space before committing to a side.
            bool positiveFree = Physics2D.CircleCast(
                body.position, bodyClearance, positive,
                Mathf.Max(bodyClearance, 0.6f), obstacleLayers).collider == null;
            bool negativeFree = Physics2D.CircleCast(
                body.position, bodyClearance, negative,
                Mathf.Max(bodyClearance, 0.6f), obstacleLayers).collider == null;
            if (positiveFree && !negativeFree) preferredSide = 1;
            else if (!positiveFree && negativeFree) preferredSide = -1;
            else preferredSide = scorePositive >= scoreNegative ? 1 : -1;
            holdUntil = Time.fixedTime + Mathf.Max(0.1f, sideHoldSeconds);
        }

        Vector2 direction = tangent * preferredSide + normal * outwardBias;
        steerDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : toPlayer;
        avoiding = true;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || !Application.isPlaying) return;
        Gizmos.color = avoiding ? Color.yellow : Color.green;
        Gizmos.DrawWireSphere(transform.position, bodyClearance);
        Gizmos.DrawLine(transform.position,
            transform.position + (Vector3)steerDirection * lookAhead);
    }
}
