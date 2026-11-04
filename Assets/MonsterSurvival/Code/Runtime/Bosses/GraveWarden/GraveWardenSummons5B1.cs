using System.Collections.Generic;
using UnityEngine;

// Reuses the user's existing enemy movement, rewards, slow and A* components on minion prefabs.
[DisallowMultipleComponent]
public class GraveWardenSummons5B1 : MonoBehaviour
{
    [Header("Required prefabs (duplicate your working MinionEnemy)")]
    public GameObject wraithPrefab;
    public GameObject reanimatedWarriorPrefab;
    public Sprite soulMarkerSprite;

    [Header("Scene helpers (auto-found at runtime)")]
    public MapSafeSpawn2D safeSpawn;
    public LayerMask obstacleLayers;

    [Header("Wraith settings")]
    [Min(1)] public int phase1WraithCount = 2;
    [Min(1)] public int phase2WraithCount = 3;
    [Min(1)] public int maxLiveWraiths = 4;
    [Min(0.5f)] public float summonRadius = 2f;
    [Min(0.25f)] public float summonClearance = 0.45f;

    [Header("Reanimate Fallen")]
    [Min(1)] public int reanimateCount = 2;
    [Min(1)] public int maxLiveWarriors = 2;
    [Min(1)] public int maxSoulMarkers = 8;
    [Min(0.25f)] public float soulMarkerLifetime = 10f;

    private GraveWardenBossAI boss;
    private GraveWardenVFX5B3 upgradedVfx;
    private GraveWardenSoulHarvest5B1 harvest;
    private readonly List<GameObject> wraiths = new List<GameObject>();
    private readonly List<GameObject> warriors = new List<GameObject>();
    private readonly List<SoulMark> markers = new List<SoulMark>();

    private class SoulMark
    {
        public Vector2 worldPosition;
        public float until;
        public GameObject vfx;
    }

    private void Awake()
    {
        boss = GetComponent<GraveWardenBossAI>();
        upgradedVfx = GetComponent<GraveWardenVFX5B3>();
        harvest = GetComponent<GraveWardenSoulHarvest5B1>();
        if (safeSpawn == null) safeSpawn = FindAnyObjectByType<MapSafeSpawn2D>();
        int layer = LayerMask.NameToLayer("Obstacles");
        if (obstacleLayers.value == 0 && layer >= 0) obstacleLayers = 1 << layer;
    }

    private void Update()
    {
        for (int i = markers.Count - 1; i >= 0; i--)
        {
            if (Time.time < markers[i].until) continue;
            if (markers[i].vfx != null) Destroy(markers[i].vfx);
            markers.RemoveAt(i);
        }
    }

    public bool CanSummonWraiths()
    {
        wraiths.RemoveAll(x => x == null);
        return boss != null && !boss.IsDead && IsEnemyPrefabValid(wraithPrefab) &&
            wraiths.Count < maxLiveWraiths;
    }

    public int SpawnWraiths(bool phaseTwo, bool empowered)
    {
        if (!CanSummonWraiths()) return 0;
        int desired = phaseTwo ? phase2WraithCount : phase1WraithCount;
        if (empowered) desired++;
        desired = Mathf.Min(desired, maxLiveWraiths - wraiths.Count);
        int count = 0;
        for (int i = 0; i < desired; i++)
        {
            Vector3 position;
            if (!TryFindSpawn(out position)) continue;
            GameObject minion = Instantiate(wraithPrefab, position, Quaternion.identity);
            minion.name = "GW_Wraith_Runtime";
            if (upgradedVfx != null) upgradedVfx.PlayWraithPortal(position);
            DisableLegacyMinionExpiry(minion);
            GraveWardenMinionLink5B1 link = minion.GetComponent<GraveWardenMinionLink5B1>();
            if (link == null) link = minion.AddComponent<GraveWardenMinionLink5B1>();
            link.Bind(this, true);
            wraiths.Add(minion);
            count++;
        }
        return count;
    }

    public bool CanReanimate()
    {
        warriors.RemoveAll(x => x == null);
        return boss != null && !boss.IsDead && IsEnemyPrefabValid(reanimatedWarriorPrefab) &&
            warriors.Count < maxLiveWarriors && HasValidMarker();
    }

    public int Reanimate(bool empowered)
    {
        if (!CanReanimate()) return 0;
        int desired = Mathf.Min(reanimateCount + (empowered ? 1 : 0), maxLiveWarriors - warriors.Count);
        int created = 0;
        for (int i = markers.Count - 1; i >= 0 && created < desired; i--)
        {
            SoulMark marker = markers[i];
            if (Time.time >= marker.until) continue;
            // If another obstacle appeared there, keep the marker for a later attempt.
            if (!IsSafe(marker.worldPosition)) continue;
            GameObject warrior = Instantiate(reanimatedWarriorPrefab, marker.worldPosition, Quaternion.identity);
            warrior.name = "GW_ReanimatedWarrior_Runtime";
            if (upgradedVfx != null) upgradedVfx.PlayReanimate(marker.worldPosition);
            DisableLegacyMinionExpiry(warrior);
            GraveWardenMinionLink5B1 link = warrior.GetComponent<GraveWardenMinionLink5B1>();
            if (link == null) link = warrior.AddComponent<GraveWardenMinionLink5B1>();
            link.Bind(this, false);
            warriors.Add(warrior);
            if (marker.vfx != null) Destroy(marker.vfx);
            markers.RemoveAt(i);
            created++;
        }
        return created;
    }

    private bool HasValidMarker()
    {
        for (int i = 0; i < markers.Count; i++)
            if (Time.time < markers[i].until && IsSafe(markers[i].worldPosition)) return true;
        return false;
    }

    // MinionEnemy prefab from the Summoner system may contain SummonedMinion.
    // Its owner is null here, so it would despawn immediately in Update.
    // Disable that legacy lifespan handler ONLY on this boss-created clone.
    private static void DisableLegacyMinionExpiry(GameObject instance)
    {
        MonoBehaviour[] behaviours = instance.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
            if (behaviours[i] != null && behaviours[i].GetType().Name == "SummonedMinion")
                behaviours[i].enabled = false;
    }

    private static bool IsEnemyPrefabValid(GameObject prefab)
    {
        return prefab != null && prefab.GetComponent<Health>() != null &&
            prefab.GetComponent<EnemyController>() != null;
    }

    private bool TryFindSpawn(out Vector3 position)
    {
        position = Vector3.zero;
        if (boss == null || boss.Player == null) return false;
        for (int i = 0; i < 16; i++)
        {
            Vector2 candidate = (Vector2)transform.position + Random.insideUnitCircle.normalized *
                                Random.Range(summonRadius, summonRadius + 1.4f);
            if (!IsSafe(candidate)) continue;
            position = candidate;
            return true;
        }
        return false;
    }

    private bool IsSafe(Vector2 world)
    {
        if (boss == null || boss.Player == null) return false;
        if (Vector2.Distance(world, boss.Player.position) < 1f) return false;
        if (safeSpawn != null)
            return safeSpawn.IsSafe(world, boss.Player, 1f, summonClearance, obstacleLayers);
        return obstacleLayers.value == 0 || Physics2D.OverlapCircle(world, summonClearance, obstacleLayers) == null;
    }

    // Called ONLY by Health.OnDied, not by despawn/Destroy. The boss may award a soul.
    public void OnControlledMinionDied(Vector2 position, bool wasWraith)
    {
        if (boss == null || boss.IsDead) return;
        if (harvest != null) harvest.GainFromDefeatedMinion();
        if (wasWraith) PlaceSoulMarker(position);
    }

    private void PlaceSoulMarker(Vector2 position)
    {
        // Visible marker is mandatory: never resurrect at an unmarked, invisible position.
        if (soulMarkerSprite == null) return;
        if (markers.Count >= maxSoulMarkers)
        {
            SoulMark oldest = markers[0];
            if (oldest.vfx != null) Destroy(oldest.vfx);
            markers.RemoveAt(0);
        }
        GameObject vfx = new GameObject("GW_Reanimate_Marker");
        vfx.transform.position = position;
        SpriteRenderer sr = vfx.AddComponent<SpriteRenderer>();
        sr.sprite = soulMarkerSprite;
        sr.color = new Color(0.72f, 0.33f, 1f, 0.9f);
        sr.sortingOrder = 10;
        float size = Mathf.Max(0.01f, soulMarkerSprite.bounds.size.x);
        vfx.transform.localScale = Vector3.one * (0.9f / size);
        markers.Add(new SoulMark
        {
            worldPosition = position,
            until = Time.time + soulMarkerLifetime,
            vfx = vfx
        });
    }

    public void ClearAll()
    {
        for (int i = 0; i < wraiths.Count; i++) if (wraiths[i] != null) Destroy(wraiths[i]);
        for (int i = 0; i < warriors.Count; i++) if (warriors[i] != null) Destroy(warriors[i]);
        wraiths.Clear();
        warriors.Clear();
        for (int i = 0; i < markers.Count; i++) if (markers[i].vfx != null) Destroy(markers[i].vfx);
        markers.Clear();
    }
}

// Added at runtime to EACH boss-owned minion; no need to change EnemyController.cs.
