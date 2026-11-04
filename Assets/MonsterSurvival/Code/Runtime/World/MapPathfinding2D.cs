using System.Collections.Generic;
using UnityEngine;

// MonsterSurvival 4B.5 — SHARED, bounded A* for static obstacles on an active map.
// Put ONE instance on the WaveManager scene object. No texture/Tilemap generation at runtime.
// Cells are checked lazily and cached until the active map changes.
[DisallowMultipleComponent]
public class MapPathfinding2D : MonoBehaviour
{
    [Header("Scene references")]
    public MapSafeSpawn2D safeSpawn;
    public WaveMapSwitcher mapSwitcher;

    [Header("Navigation obstacles")]
    public LayerMask obstacleLayers;
    [Min(0.25f)] public float cellSize = 0.8f;
    [Min(0.05f)] public float agentClearance = 0.33f;
    [Min(2)] public int searchRadiusCells = 24;
    [Range(50, 2000)] public int maxExpandedNodes = 550;
    public bool showWarnings = true;

    private int cachedMap = int.MinValue;
    private readonly Dictionary<Vector2Int, bool> walkableCache = new Dictionary<Vector2Int, bool>();

    private class Node
    {
        public Vector2Int cell;
        public float g;
        public float h;
        public int parent;
        public bool closed;
        public float F { get { return g + h; } }
        public Node(Vector2Int c, float cost, float heuristic, int p)
        { cell = c; g = cost; h = heuristic; parent = p; }
    }

    private static readonly Vector2Int[] Neighbors =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0),
        new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 1), new Vector2Int(-1, 1),
        new Vector2Int(1, -1), new Vector2Int(-1, -1)
    };

    private void Awake()
    {
        if (safeSpawn == null) safeSpawn = GetComponent<MapSafeSpawn2D>();
        if (mapSwitcher == null) mapSwitcher = GetComponent<WaveMapSwitcher>();
        if (obstacleLayers.value == 0 && safeSpawn != null)
            obstacleLayers = safeSpawn.obstacleLayers;
        if (obstacleLayers.value == 0 && showWarnings)
            Debug.LogWarning("MapPathfinding2D: Chon Layer Obstacles trong Inspector.", this);
    }

    private void CheckActiveMap()
    {
        int active = mapSwitcher != null ? mapSwitcher.ActiveMapIndex : 0;
        if (active == cachedMap) return;
        cachedMap = active;
        walkableCache.Clear();
    }

    private Vector2Int ToCell(Vector2 world)
    {
        return new Vector2Int(Mathf.FloorToInt(world.x / cellSize),
                              Mathf.FloorToInt(world.y / cellSize));
    }

    private Vector2 ToWorld(Vector2Int cell)
    {
        return new Vector2((cell.x + 0.5f) * cellSize,
                           (cell.y + 0.5f) * cellSize);
    }

    // Physics-based line-of-sight / clearance. Obstacles must have Collider2D on the layer.
    public bool IsBlocked(Vector2 from, Vector2 to, float clearance = -1f)
    {
        if (obstacleLayers.value == 0) return false;
        Vector2 delta = to - from;
        if (delta.sqrMagnitude < 0.0001f) return false;
        float radius = clearance > 0f ? clearance : agentClearance;
        return Physics2D.CircleCast(from, radius, delta.normalized, delta.magnitude,
                                    obstacleLayers).collider != null;
    }

    public bool IsWalkable(Vector2 world)
    {
        CheckActiveMap();
        return CheckCell(ToCell(world));
    }

    private bool CheckCell(Vector2Int cell)
    {
        bool allowed;
        if (walkableCache.TryGetValue(cell, out allowed)) return allowed;
        Vector2 world = ToWorld(cell);
        if (safeSpawn != null)
            allowed = safeSpawn.IsSafe(world, null, 0f, agentClearance, obstacleLayers);
        else
            allowed = obstacleLayers.value == 0 ||
                      Physics2D.OverlapCircle(world, agentClearance, obstacleLayers) == null;
        walkableCache[cell] = allowed;
        return allowed;
    }

    // Finds a short, bounded route; destination outside the local window is clamped.
    // Empty output means no known route. The caller may retry when the target moves.
    public bool TryFindPath(Vector2 startWorld, Vector2 targetWorld, List<Vector2> result)
    {
        if (result == null) return false;
        result.Clear();
        CheckActiveMap();
        if (obstacleLayers.value == 0)
        {
            result.Add(targetWorld);
            return true;
        }

        Vector2Int start = ToCell(startWorld);
        Vector2Int rawGoal = ToCell(targetWorld);
        int radius = Mathf.Max(2, searchRadiusCells);
        Vector2Int goal = new Vector2Int(
            Mathf.Clamp(rawGoal.x, start.x - radius, start.x + radius),
            Mathf.Clamp(rawGoal.y, start.y - radius, start.y + radius));

        // Adjacent target is clear: no expensive A* necessary.
        if (rawGoal == goal && !IsBlocked(startWorld, targetWorld) && CheckCell(goal))
        {
            result.Add(targetWorld);
            return true;
        }

        List<Node> nodes = new List<Node>(128);
        Dictionary<Vector2Int, int> lookup = new Dictionary<Vector2Int, int>();
        nodes.Add(new Node(start, 0f, Heuristic(start, goal), -1));
        lookup[start] = 0;
        int bestIndex = 0;
        float bestH = nodes[0].h;
        int expanded = 0;
        bool reachedGoal = false;

        while (expanded < Mathf.Max(50, maxExpandedNodes))
        {
            int currentIndex = -1;
            float lowestF = float.MaxValue;
            for (int i = 0; i < nodes.Count; ++i)
            {
                if (nodes[i].closed) continue;
                if (nodes[i].F < lowestF)
                { lowestF = nodes[i].F; currentIndex = i; }
            }
            if (currentIndex < 0) break;
            Node current = nodes[currentIndex];
            current.closed = true;
            ++expanded;
            if (current.h < bestH)
            { bestH = current.h; bestIndex = currentIndex; }
            if (current.cell == goal)
            { bestIndex = currentIndex; reachedGoal = true; break; }

            for (int j = 0; j < Neighbors.Length; ++j)
            {
                Vector2Int d = Neighbors[j];
                Vector2Int next = current.cell + d;
                if (Mathf.Abs(next.x - start.x) > radius ||
                    Mathf.Abs(next.y - start.y) > radius || !CheckCell(next)) continue;
                // No diagonal corner cutting through adjacent solid cells.
                if (d.x != 0 && d.y != 0 &&
                    (!CheckCell(current.cell + new Vector2Int(d.x, 0)) ||
                     !CheckCell(current.cell + new Vector2Int(0, d.y)))) continue;

                float cost = current.g + ((d.x != 0 && d.y != 0) ? 1.41421356f : 1f);
                int existing;
                if (lookup.TryGetValue(next, out existing))
                {
                    if (cost < nodes[existing].g)
                    {
                        nodes[existing].g = cost;
                        nodes[existing].parent = currentIndex;
                        nodes[existing].closed = false;
                    }
                }
                else
                {
                    int index = nodes.Count;
                    nodes.Add(new Node(next, cost, Heuristic(next, goal), currentIndex));
                    lookup[next] = index;
                }
            }
        }

        if (bestIndex == 0) return false;
        List<Vector2> reverse = new List<Vector2>();
        int trace = bestIndex;
        while (trace > 0)
        {
            reverse.Add(ToWorld(nodes[trace].cell));
            trace = nodes[trace].parent;
            if (trace < 0) break;
        }
        for (int i = reverse.Count - 1; i >= 0; --i)
            result.Add(reverse[i]);

        // Only append the actual target when it is reached and the final segment is clear.
        if (reachedGoal && rawGoal == goal && !IsBlocked(result[result.Count - 1], targetWorld))
            result.Add(targetWorld);
        return result.Count > 0;
    }

    private static float Heuristic(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return (dx + dy) + (1.41421356f - 2f) * Mathf.Min(dx, dy);
    }
}
