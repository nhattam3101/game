using System.Collections.Generic;
using UnityEngine;

// Spawns Elite prefabs during the active portion of a wave.
// Does not change EnemySpawner or WaveManager.
public class EliteSpawnDirector : MonoBehaviour
{
    [Header("References")]
    public WaveManager waveManager;
    public EnemySpawner enemySpawner;
    public GameManager gameManager;
    public GameObject elitePrefab;

    [Header("Wave Rules")]
    [Min(1)] public int firstEliteWave = 3;
    public bool spawnDuringBossWave = false;

    [Header("Spawn Settings")]
    [Min(0f)] public float firstSpawnDelay = 8f;
    [Min(0.2f)] public float spawnInterval = 18f;
    [Min(1)] public int maxAliveElites = 2;
    [Range(0.02f, 0.4f)] public float viewportMargin = 0.12f;
    [Min(0f)] public float minimumDistanceFromPlayer = 3f;

    private readonly List<GameObject> spawnedElites = new List<GameObject>();
    private Transform player;
    private Camera mainCamera;
    private float spawnTimer;
    private bool waveWasActive;
    private int previousWave = -1;

    private void Start()
    {
        if (waveManager == null)
            waveManager = FindFirstObjectByType<WaveManager>();

        if (waveManager != null && enemySpawner == null)
            enemySpawner = waveManager.enemySpawner;

        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
            player = playerObject.transform;

        mainCamera = Camera.main;

        if (waveManager == null || enemySpawner == null ||
            gameManager == null || elitePrefab == null ||
            player == null || mainCamera == null)
        {
            Debug.LogWarning("EliteSpawnDirector: Missing one or more references.");
            enabled = false;
        }
    }

    private void Update()
    {
        if (Time.timeScale == 0f || gameManager.IsGameOver)
            return;

        int wave = waveManager.CurrentWave;

        // EnemySpawner is enabled only during the fighting portion of a wave.
        bool eligibleWave = wave >= firstEliteWave &&
                            wave <= waveManager.totalWaves &&
                            (spawnDuringBossWave || wave != waveManager.bossWave);

        bool waveActive = eligibleWave && enemySpawner.enabled;
        if (!waveActive)
        {
            waveWasActive = false;
            return;
        }

        // Reset first-spawn delay when a new active wave begins.
        if (!waveWasActive || previousWave != wave)
        {
            previousWave = wave;
            waveWasActive = true;
            spawnTimer = firstSpawnDelay;
        }

        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f)
            return;

        spawnTimer = spawnInterval;
        spawnedElites.RemoveAll(elite => elite == null);

        if (spawnedElites.Count >= maxAliveElites)
            return;

        Vector3 spawnPosition;
        if (!TryGetSpawnPosition(out spawnPosition))
            return;

        GameObject elite = Instantiate(elitePrefab, spawnPosition, Quaternion.identity);
        spawnedElites.Add(elite);

        Debug.Log("EliteSpawnDirector: Spawned elite in Wave " + wave);
    }

    private bool TryGetSpawnPosition(out Vector3 result)
    {
        float margin = Mathf.Clamp(viewportMargin, 0.02f, 0.4f);
        float minimumDistanceSqr = minimumDistanceFromPlayer * minimumDistanceFromPlayer;

        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector3 viewportPoint = new Vector3(
                Random.Range(margin, 1f - margin),
                Random.Range(margin, 1f - margin),
                Mathf.Abs(mainCamera.transform.position.z)
            );

            Vector3 candidate = mainCamera.ViewportToWorldPoint(viewportPoint);
            candidate.z = 0f;

            if ((candidate - player.position).sqrMagnitude < minimumDistanceSqr)
                continue;

            result = candidate;
            return true;
        }

        result = Vector3.zero;
        return false;
    }
}
