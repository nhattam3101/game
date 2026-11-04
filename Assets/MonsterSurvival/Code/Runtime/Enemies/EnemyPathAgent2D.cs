using System.Collections.Generic;
using UnityEngine;

// Add to EnemyController prefabs instead of EnemyObstacleAvoidance2D (4B.4).
// Runs AFTER EnemyController.FixedUpdate, changes only steering direction.
// Preserves EnemyController speed / Slow / attack / EXP / Gold / death.
[DefaultExecutionOrder(1150)]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyController))]
[DisallowMultipleComponent]
public class EnemyPathAgent2D : MonoBehaviour
{
    [Header("Scene reference (auto-finds if empty)")]
    public MapPathfinding2D navigation;
    [Header("Navigation")]
    [Min(0.15f)] public float recalculateInterval = 0.7f;
    [Min(0.05f)] public float waypointReachDistance = 0.3f;
    [Min(0.2f)] public float playerMovementToRecalculate = 1.25f;
    public bool showDebugPath = false;

    private EnemyController movement;
    private Rigidbody2D body;
    private Transform player;
    private float nextRecalculate;
    private Vector2 lastDestination;
    private readonly List<Vector2> route = new List<Vector2>();
    private int waypoint;

    private void Awake()
    {
        movement = GetComponent<EnemyController>();
        body = GetComponent<Rigidbody2D>();
        nextRecalculate = Time.time + Random.Range(0f, 0.5f);
    }

    private void Start()
    {
        if (navigation == null)
            navigation = FindAnyObjectByType<MapPathfinding2D>();
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    private void FixedUpdate()
    {
        if (Time.timeScale == 0f || movement == null || !movement.enabled || body == null)
            return;
        Vector2 oldVelocity = body.linearVelocity;
        float speed = oldVelocity.magnitude;
        if (speed < 0.01f) { route.Clear(); return; }
        if (navigation == null || navigation.obstacleLayers.value == 0) return;
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return;
            player = p.transform;
        }

        Vector2 here = body.position;
        Vector2 goal = player.position;
        if (!navigation.IsBlocked(here, goal))
        { route.Clear(); waypoint = 0; return; }

        bool expired = Time.time >= nextRecalculate;
        bool targetMoved = (goal - lastDestination).sqrMagnitude >
                           playerMovementToRecalculate * playerMovementToRecalculate;
        if (expired && (route.Count == 0 || waypoint >= route.Count || targetMoved))
        {
            navigation.TryFindPath(here, goal, route);
            waypoint = 0;
            lastDestination = goal;
            nextRecalculate = Time.time + recalculateInterval;
        }
        else if (expired)
        {
            nextRecalculate = Time.time + recalculateInterval;
        }

        while (waypoint < route.Count &&
               (route[waypoint] - here).sqrMagnitude <= waypointReachDistance * waypointReachDistance)
            ++waypoint;
        if (waypoint >= route.Count) return;

        Vector2 toWaypoint = route[waypoint] - here;
        if (toWaypoint.sqrMagnitude > 0.001f)
            body.linearVelocity = toWaypoint.normalized * speed;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugPath || !Application.isPlaying) return;
        Gizmos.color = Color.cyan;
        Vector3 p = transform.position;
        for (int i = waypoint; i < route.Count; i++)
        {
            Gizmos.DrawLine(p, route[i]);
            Gizmos.DrawWireSphere(route[i], 0.14f);
            p = route[i];
        }
    }
}
