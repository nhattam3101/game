using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Physical obstacle with visible warning. Requires user-configured wallPrefab and warningSprite.
[DisallowMultipleComponent]
public class GraveWardenTombWall5B1 : MonoBehaviour
{
    [Header("Must assign in GraveWarden prefab")]
    public GameObject wallPrefab; // SpriteRenderer + BoxCollider2D(non-trigger), NO Enemy AI.
    public Sprite warningSprite;
    public LayerMask obstacleLayers;
    public MapSafeSpawn2D safeSpawn;
    public MapPathfinding2D navigation;

    [Header("Attack")]
    [Min(0.25f)] public float warningSeconds = 1f;
    [Min(0.5f)] public float wallDurationPhase1 = 5f;
    [Min(0.5f)] public float wallDurationPhase2 = 6f;
    [Min(0f)] public float emergeDamage = 8f;
    [Min(0.5f)] public float minPlayerDistance = 1.5f;
    [Min(0.5f)] public float placementForwardDistance = 3f;
    [Min(0.5f)] public float wallSideSeparation = 2f;
    [Range(1, 3)] public int maxAliveWalls = 3;
    public int wallSortingOrder = 5;

    private GraveWardenBossAI boss;
    private GraveWardenVFX5B3 upgradedVfx;
    private readonly List<GameObject> walls = new List<GameObject>();
    private readonly List<GameObject> warnings = new List<GameObject>();
    private readonly List<Coroutine> expiryRoutines = new List<Coroutine>();

    private void Awake()
    {
        boss = GetComponent<GraveWardenBossAI>();
        upgradedVfx = GetComponent<GraveWardenVFX5B3>();
        if (safeSpawn == null) safeSpawn = FindAnyObjectByType<MapSafeSpawn2D>();
        if (navigation == null) navigation = FindAnyObjectByType<MapPathfinding2D>();
        int obstacles = LayerMask.NameToLayer("Obstacles");
        if (obstacleLayers.value == 0 && obstacles >= 0) obstacleLayers = 1 << obstacles;
    }

    public bool CanCast()
    {
        walls.RemoveAll(w => w == null);
        return wallPrefab != null && warningSprite != null && boss != null && boss.Player != null &&
               walls.Count < maxAliveWalls && wallPrefab.GetComponentInChildren<Collider2D>() != null &&
               wallPrefab.GetComponentInChildren<SpriteRenderer>() != null &&
               wallPrefab.GetComponentInChildren<SpriteRenderer>().sprite != null &&
               LayerMask.NameToLayer("Obstacles") >= 0;
    }

    public IEnumerator CastWalls(bool phaseTwo, bool empowered)
    {
        if (!CanCast()) yield break;
        Transform player = boss.Player;
        Vector2 forward = ((Vector2)player.position - (Vector2)transform.position).normalized;
        if (forward.sqrMagnitude < 0.01f) forward = Vector2.up;
        Vector2 sideways = new Vector2(-forward.y, forward.x);
        Vector2 middle = (Vector2)player.position + forward * placementForwardDistance;
        int desired = phaseTwo ? 3 : 2;
        int available = Mathf.Min(desired, maxAliveWalls - walls.Count);
        if (empowered) available = Mathf.Min(maxAliveWalls - walls.Count, available + 1);
        List<Vector2> spots = new List<Vector2>();
        List<GameObject> newWarnings = new List<GameObject>();
        for (int i = 0; i < available; i++)
        {
            // Side by side, NOT a closed cage around the player.
            float lateral = (i - (available - 1) * 0.5f) * wallSideSeparation;
            Vector2 spot = middle + sideways * lateral;
            if (!IsSafeSpot(spot, player)) continue;
            spots.Add(spot);
            GameObject warning = new GameObject("GW_TombWall_Telegraph");
            warning.transform.position = spot;
            SpriteRenderer sr = warning.AddComponent<SpriteRenderer>();
            sr.sprite = warningSprite;
            sr.color = new Color(1f, 0.7f, 0.15f, 0.82f);
            sr.sortingOrder = wallSortingOrder - 1;
            float diameter = Mathf.Max(0.01f, warningSprite.bounds.size.x);
            warning.transform.localScale = Vector3.one * (1.6f / diameter);
            warnings.Add(warning);
            newWarnings.Add(warning);
        }
        if (spots.Count == 0) yield break;
        boss.TriggerAnimation("TombWall_Summon");
        yield return new WaitForSeconds(warningSeconds);
        if (boss == null || boss.IsDead)
        {
            ClearWarnings(newWarnings);
            yield break;
        }
        for (int i = 0; i < spots.Count; i++)
        {
            Vector2 position = spots[i];
            if (!IsSafeSpot(position, player)) continue;
            GameObject wall = Instantiate(wallPrefab, position, Quaternion.identity);
            wall.name = "GW_TombWall_Runtime";
            if (upgradedVfx != null) upgradedVfx.PlayWallRise(wall);
            wall.layer = LayerMask.NameToLayer("Obstacles");
            Collider2D collider = wall.GetComponentInChildren<Collider2D>();
            if (collider != null)
            {
                collider.gameObject.layer = wall.layer;
                collider.isTrigger = false;
                Physics2D.SyncTransforms();
            }
            // Fair-play safeguard: do not leave the Player with fewer than two
            // unobstructed short escape directions once this wall appears.
            if (FreeEscapeDirections(player) < 2)
            {
                Destroy(wall);
                continue;
            }
            walls.Add(wall);
            if (collider != null && boss.PlayerHealth != null && boss.Player != null &&
                collider.OverlapPoint(boss.Player.position))
                boss.PlayerHealth.TakeDamage(emergeDamage, boss.BossHealth);
            expiryRoutines.Add(StartCoroutine(RemoveAfter(wall, (phaseTwo ? wallDurationPhase2 : wallDurationPhase1) + (empowered ? 1.5f : 0f))));
        }
        ClearWarnings(newWarnings);
        RefreshNav();
    }

    private int FreeEscapeDirections(Transform player)
    {
        if (player == null || obstacleLayers.value == 0) return 4;
        Vector2 start = player.position;
        Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        int free = 0;
        for (int i = 0; i < directions.Length; i++)
            if (Physics2D.CircleCast(start, 0.25f, directions[i], 2.2f,
                    obstacleLayers).collider == null)
                free++;
        return free;
    }

    private bool IsSafeSpot(Vector2 pos, Transform player)
    {
        if (player != null && Vector2.Distance(pos, player.position) < minPlayerDistance) return false;
        if (safeSpawn != null) return safeSpawn.IsSafe(pos, player, minPlayerDistance, 0.6f, obstacleLayers);
        return obstacleLayers.value == 0 || Physics2D.OverlapCircle(pos, 0.6f, obstacleLayers) == null;
    }

    private IEnumerator RemoveAfter(GameObject wall, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (wall != null)
        {
            walls.Remove(wall);
            Destroy(wall);
            // Delay until the destroyed Collider is removed at the end of this frame.
            yield return null;
            RefreshNav();
        }
    }

    private void RefreshNav()
    {
        // Optional 3-line cache patch supplied in Docs. No hard compile dependency.
        if (navigation != null)
            navigation.SendMessage("InvalidateDynamicObstacles", SendMessageOptions.DontRequireReceiver);
    }

    private void ClearWarnings(List<GameObject> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            warnings.Remove(list[i]);
            if (list[i] != null) Destroy(list[i]);
        }
    }

    public void ClearAll()
    {
        StopAllCoroutines();
        expiryRoutines.Clear();
        for (int i = 0; i < walls.Count; i++) if (walls[i] != null) Destroy(walls[i]);
        walls.Clear();
        for (int i = 0; i < warnings.Count; i++) if (warnings[i] != null) Destroy(warnings[i]);
        warnings.Clear();
        RefreshNav();
    }
}
