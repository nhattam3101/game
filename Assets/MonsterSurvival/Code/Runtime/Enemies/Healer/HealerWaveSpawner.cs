using System.Collections.Generic;
using UnityEngine;

// Add this as an additional component on the WaveManager GameObject.
// It reads CurrentWave and EnemySpawner.enabled without editing WaveManager.
public class HealerWaveSpawner : MonoBehaviour
{
    [Header("References")]
    public WaveManager waveManager;
    public EnemySpawner enemySpawner;
    public GameObject healerPrefab;
    public Camera spawnCamera;
    [Header("Safe Spawn 4B.3 (optional)")]
    public MapSafeSpawn2D safeSpawn;

    [Header("Wave Rules")]
    [Min(1)] public int firstWave = 3;
    [Min(1)] public int lastWave = 4;
    [Min(1f)] public float spawnInterval = 10f;
    [Min(1)] public int maxActiveHealers = 2;
    [Min(1)] public int maxSpawnsPerWave = 3;
    public bool obeyEnemyLimit = true;

    [Header("Position")]
    [Range(0f, 0.4f)] public float viewportMargin = 0.1f;
    [Min(0f)] public float minDistanceFromPlayer = 3.5f;
    [Min(0f)] public float clearanceRadius = 0.45f;
    public LayerMask blockedLayers; // Set to walls / obstacles, not Enemy.
    [Min(1)] public int placementAttempts = 20;

    private readonly List<GameObject> activeHealers = new List<GameObject>();
    private float spawnTimer;
    private int lastObservedWave = -1;
    private int spawnedThisWave;

    private void Awake()
    {
        if (waveManager == null) waveManager = GetComponent<WaveManager>();
        if (enemySpawner == null && waveManager != null) enemySpawner = waveManager.enemySpawner;
        if (spawnCamera == null) spawnCamera = Camera.main;
        if (safeSpawn == null) safeSpawn = GetComponent<MapSafeSpawn2D>();
        spawnTimer = spawnInterval;
    }

    private void Update()
    {
        if (waveManager == null || healerPrefab == null || enemySpawner == null) return;
        if (waveManager.gameManager != null && waveManager.gameManager.IsGameOver) return;
        if (Time.timeScale == 0f) return;

        int wave = waveManager.CurrentWave;
        if (wave != lastObservedWave)
        {
            lastObservedWave = wave;
            spawnedThisWave = 0;
            spawnTimer = spawnInterval;
        }

        // WaveManager disables EnemySpawner during breaks; do not spawn in those breaks.
        if (!enemySpawner.enabled || wave < firstWave || wave > lastWave) return;
        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f) return;
        spawnTimer = Mathf.Max(1f, spawnInterval);

        activeHealers.RemoveAll(item => item == null);
        if (activeHealers.Count >= maxActiveHealers || spawnedThisWave >= maxSpawnsPerWave)
            return;

        if (obeyEnemyLimit &&
            GameObject.FindGameObjectsWithTag("Enemy").Length >= enemySpawner.maxEnemies)
            return;

        TrySpawnHealer(wave);
    }

    private void TrySpawnHealer(int wave)
    {
        if (spawnCamera == null) spawnCamera = Camera.main;
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (spawnCamera == null || playerObject == null) return;

        if (safeSpawn != null)
        {
            LayerMask combined = safeSpawn.obstacleLayers.value | blockedLayers.value;
            Vector3 safePosition;
            if (!safeSpawn.TryFindViewportPoint(
                spawnCamera, playerObject.transform, out safePosition,
                viewportMargin, minDistanceFromPlayer, clearanceRadius,
                placementAttempts, combined))
                return;

            GameObject safeHealer = Instantiate(healerPrefab, safePosition, Quaternion.identity);
            activeHealers.Add(safeHealer);
            spawnedThisWave++;
            Debug.Log("HealerWaveSpawner: Spawn Healer in Wave " + wave);
            return;
        }

        float depth = Mathf.Abs(spawnCamera.transform.position.z);
        float low = viewportMargin;
        float high = 1f - viewportMargin;

        for (int i = 0; i < placementAttempts; i++)
        {
            Vector3 viewportPoint = new Vector3(
                Random.Range(low, high), Random.Range(low, high), depth);
            Vector3 worldPosition = spawnCamera.ViewportToWorldPoint(viewportPoint);
            worldPosition.z = 0f;

            if (Vector2.Distance(worldPosition, playerObject.transform.position) < minDistanceFromPlayer)
                continue;
            if (blockedLayers.value != 0 &&
                Physics2D.OverlapCircle(worldPosition, clearanceRadius, blockedLayers) != null)
                continue;

            GameObject healer = Instantiate(healerPrefab, worldPosition, Quaternion.identity);
            activeHealers.Add(healer);
            spawnedThisWave++;
            Debug.Log("HealerWaveSpawner: Spawn Healer in Wave " + wave);
            return;
        }
        // Skip this attempt instead of spawning in walls or directly on the Player.
    }
}
