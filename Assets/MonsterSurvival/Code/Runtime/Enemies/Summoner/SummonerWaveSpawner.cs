using System.Collections.Generic;
using UnityEngine;

// Gắn BỔ SUNG lên WaveManager GameObject, cạnh HealerWaveSpawner.
// Không chỉnh sửa WaveManager hoặc EnemySpawner cũ.
public class SummonerWaveSpawner : MonoBehaviour
{
    [Header("References")]
    public WaveManager waveManager;
    public EnemySpawner enemySpawner;
    public GameObject summonerPrefab;
    public Camera spawnCamera;
    [Header("Safe Spawn 4B.3 (optional)")]
    public MapSafeSpawn2D safeSpawn;

    [Header("Wave Rules")]
    [Min(1)] public int firstWave = 4;
    [Min(1)] public int lastWave = 4; // Wave 5 mặc định dành cho Shadow Titan.
    [Min(1f)] public float spawnInterval = 14f;
    [Min(1)] public int maxActiveSummoners = 1;
    [Min(1)] public int maxSpawnsPerWave = 2;
    public bool obeyEnemyLimit = true;

    [Header("Spawn Position")]
    [Range(0f, 0.4f)] public float viewportMargin = 0.1f;
    [Min(0f)] public float minDistanceFromPlayer = 3.5f;
    [Min(0f)] public float clearanceRadius = 0.5f;
    public LayerMask blockedLayers;
    [Min(1)] public int placementAttempts = 30;
    public bool verboseLog = true;

    private readonly List<GameObject> activeSummoners = new List<GameObject>();
    private float timer;
    private int observedWave = -1;
    private int spawnedInWave;

    private void Awake()
    {
        if (waveManager == null) waveManager = GetComponent<WaveManager>();
        if (enemySpawner == null && waveManager != null) enemySpawner = waveManager.enemySpawner;
        if (spawnCamera == null) spawnCamera = Camera.main;
        if (safeSpawn == null) safeSpawn = GetComponent<MapSafeSpawn2D>();
        timer = spawnInterval;
    }

    private void Update()
    {
        if (waveManager == null || enemySpawner == null || summonerPrefab == null) return;
        if (waveManager.gameManager != null && waveManager.gameManager.IsGameOver) return;
        if (Time.timeScale == 0f) return;
        int wave = waveManager.CurrentWave;
        if (wave != observedWave)
        {
            observedWave = wave;
            spawnedInWave = 0;
            timer = spawnInterval;
        }
        if (wave < firstWave || wave > lastWave || !enemySpawner.enabled) return;
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = Mathf.Max(1f, spawnInterval);
        activeSummoners.RemoveAll(obj => obj == null);
        if (activeSummoners.Count >= maxActiveSummoners || spawnedInWave >= maxSpawnsPerWave) return;
        if (obeyEnemyLimit && GameObject.FindGameObjectsWithTag("Enemy").Length >= enemySpawner.maxEnemies) return;
        TrySpawn(wave);
    }

    private void TrySpawn(int wave)
    {
        if (spawnCamera == null) spawnCamera = Camera.main;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (spawnCamera == null || player == null) return;

        if (safeSpawn != null)
        {
            LayerMask combined = safeSpawn.obstacleLayers.value | blockedLayers.value;
            Vector3 safePosition;
            if (!safeSpawn.TryFindViewportPoint(
                spawnCamera, player.transform, out safePosition,
                viewportMargin, minDistanceFromPlayer, clearanceRadius,
                placementAttempts, combined))
            {
                if (verboseLog) Debug.LogWarning("SummonerWaveSpawner: No safe location; skipped attempt.");
                return;
            }
            GameObject safeSummoner = Instantiate(summonerPrefab, safePosition, Quaternion.identity);
            activeSummoners.Add(safeSummoner);
            spawnedInWave++;
            if (verboseLog) Debug.Log("SummonerWaveSpawner: Spawn Summoner in Wave " + wave);
            return;
        }

        float depth = Mathf.Abs(spawnCamera.transform.position.z);
        float min = viewportMargin;
        float max = 1f - viewportMargin;
        for (int attempt = 0; attempt < placementAttempts; attempt++)
        {
            Vector3 viewport = new Vector3(Random.Range(min, max), Random.Range(min, max), depth);
            Vector3 candidate = spawnCamera.ViewportToWorldPoint(viewport);
            candidate.z = 0f;
            if (Vector2.Distance(candidate, player.transform.position) < minDistanceFromPlayer) continue;
            if (blockedLayers.value != 0 && Physics2D.OverlapCircle(candidate, clearanceRadius, blockedLayers) != null) continue;
            GameObject summoner = Instantiate(summonerPrefab, candidate, Quaternion.identity);
            activeSummoners.Add(summoner);
            spawnedInWave++;
            if (verboseLog) Debug.Log("SummonerWaveSpawner: Spawn Summoner in Wave " + wave);
            return;
        }
        if (verboseLog) Debug.LogWarning("SummonerWaveSpawner: Không tìm thấy vị trí Spawn hợp lệ.");
    }
}
