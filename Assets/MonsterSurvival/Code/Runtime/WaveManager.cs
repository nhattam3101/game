
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    [Header("References")]
    public EnemySpawner enemySpawner;
    public GameManager gameManager;

    [Header("Boss Settings")]
    public GameObject bossPrefab;
    public BossHealthBarUI bossHealthBar;

    [Min(1)]
    public int bossWave = 5;

    public string bossName = "SHADOW TITAN";

    [Header("Wave Settings")]
    [Min(1)]
    public int totalWaves = 5;

    [Min(1f)]
    public float waveDuration = 30f;

    [Min(0f)]
    public float breakDuration = 5f;

    [Min(0f)]
    public float announcementDuration = 1.5f;

    [Header("Difficulty Settings")]
    [Min(0.1f)]
    public float firstWaveSpawnInterval = 2f;

    [Min(0f)]
    public float intervalReductionPerWave = 0.25f;

    [Min(0.1f)]
    public float minimumSpawnInterval = 0.6f;

    [Min(1)]
    public int firstWaveMaxEnemies = 20;

    [Min(0)]
    public int extraEnemiesPerWave = 5;

    [Header("Ranged Enemy Settings (Optional)")]
    public GameObject rangedEnemyPrefab;

    [Min(1)] public int rangedStartWave = 2;
    [Min(0.1f)] public float rangedSpawnInterval = 8f;
    [Min(1)] public int maxRangedEnemies = 3;
    [Range(0.05f, 0.35f)] public float rangedSpawnViewportMargin = 0.12f;

    [Header("Safe Spawn 4B.3 (optional)")]
    public MapSafeSpawn2D safeSpawn;
    [Min(0f)] public float rangedMinDistanceFromPlayer = 3.5f;
    [Min(0.05f)] public float rangedSpawnClearance = 0.5f;
    [Min(0f)] public float bossMinDistanceFromPlayer = 4f;
    [Min(0.05f)] public float bossSpawnClearance = 1.5f;
    [Range(1, 100)] public int bossSpawnAttempts = 40;

    // Danh sach chi theo doi quai ban xa, khong sua EnemySpawner cu.
    private readonly List<GameObject> activeRangedEnemies =
        new List<GameObject>();

    [Header("Wave UI")]
    public TMP_Text waveText;
    public TMP_Text announcementText;

    public int CurrentWave { get; private set; }

    private Health bossHealth;
    private bool bossSpawned;
    private bool bossDefeated;
    private float bossSpawnRetryTimer;

    private void Start()
    {
        if (safeSpawn == null) safeSpawn = GetComponent<MapSafeSpawn2D>();
        if (enemySpawner == null)
        {
            Debug.LogError(
                "WaveManager: Chua gan EnemySpawner!"
            );

            enabled = false;
            return;
        }

        if (gameManager == null)
        {
            gameManager =
                FindFirstObjectByType<GameManager>();
        }

        if (gameManager == null)
        {
            Debug.LogError(
                "WaveManager: Khong tim thay GameManager!"
            );

            enemySpawner.enabled = false;
            enabled = false;
            return;
        }

        // Chi yeu cau Boss khi man choi co Wave Boss
        if (totalWaves >= bossWave)
        {
            if (bossPrefab == null)
            {
                Debug.LogError(
                    "WaveManager: Chua gan Boss Prefab!"
                );

                enemySpawner.enabled = false;
                enabled = false;
                return;
            }

            if (bossPrefab.GetComponent<Health>() == null)
            {
                Debug.LogError(
                    "WaveManager: Boss Prefab thieu Health!"
                );

                enemySpawner.enabled = false;
                enabled = false;
                return;
            }
        }

        enemySpawner.enabled = false;

        if (announcementText != null)
        {
            announcementText.gameObject.SetActive(false);
        }

        bossSpawned = false;
        bossDefeated = false;

        StartCoroutine(RunWaves());
    }

    private IEnumerator RunWaves()
    {
        for (int wave = 1; wave <= totalWaves; wave++)
        {
            if (gameManager.IsGameOver)
            {
                yield break;
            }

            CurrentWave = wave;

            ConfigureWave(wave);

            yield return ShowAnnouncement(
                "WAVE " + wave,
                announcementDuration
            );

            if (gameManager.IsGameOver)
            {
                yield break;
            }

            // Tao Boss khi den Wave duoc chi dinh
            if (wave == bossWave)
            {
                SpawnBoss();
            }

            enemySpawner.enabled = true;

            Debug.Log(
                "Bat dau Wave " + wave
            );

            // Moi wave co bo dem spawn Ranged Enemy rieng.
            float rangedSpawnTimer = rangedSpawnInterval;
            bossSpawnRetryTimer = 2f;
            float remainingTime = waveDuration;

            // Dem nguoc thoi gian Wave
            while (remainingTime > 0f)
            {
                if (gameManager.IsGameOver)
                {
                    enemySpawner.enabled = false;
                    yield break;
                }

                remainingTime = Mathf.Max(
                    0f,
                    remainingTime - Time.deltaTime
                );

                // Neu boss chua spawn vi thieu vi tri an toan, thu lai sau moi 2s.
                if (wave == bossWave && !bossSpawned && Time.deltaTime > 0f)
                {
                    bossSpawnRetryTimer -= Time.deltaTime;
                    if (bossSpawnRetryTimer <= 0f)
                    {
                        bossSpawnRetryTimer = 2f;
                        SpawnBoss();
                    }
                }

                // Khong spawn khi Pause (deltaTime = 0) / GameOver.
                if (Time.deltaTime > 0f &&
                    rangedEnemyPrefab != null &&
                    wave >= rangedStartWave)
                {
                    rangedSpawnTimer -= Time.deltaTime;
                    if (rangedSpawnTimer <= 0f)
                    {
                        TrySpawnRangedEnemy(wave);
                        rangedSpawnTimer = rangedSpawnInterval;
                    }
                }

                UpdateWaveText(
                    "WAVE " + wave + "/" + totalWaves +
                    " | " +
                    Mathf.CeilToInt(remainingTime) + "s"
                );

                yield return null;
            }

            enemySpawner.enabled = false;

            Debug.Log(
                "Ket thuc Wave " + wave
            );

            // Never award Victory without a boss merely because all safe positions
            // were blocked during the wave. Keep retrying until a valid point exists.
            if (wave == totalWaves && wave == bossWave && !bossSpawned)
            {
                UpdateWaveText("WAITING FOR SAFE BOSS SPAWN...");
                Debug.LogWarning("WaveManager: Boss not spawned. Clear obstacles / verify Ground Tilemap and MapSafeSpawn2D.");
                while (!bossSpawned)
                {
                    if (gameManager.IsGameOver) yield break;
                    SpawnBoss();
                    if (bossSpawned) break;
                    yield return new WaitForSeconds(2f);
                }
            }

            // Wave cuoi: doi Boss bi tieu diet
            if (wave == totalWaves &&
                bossSpawned &&
                !bossDefeated)
            {
                UpdateWaveText(
                    "DEFEAT THE BOSS!"
                );

                while (!bossDefeated)
                {
                    if (gameManager.IsGameOver)
                    {
                        yield break;
                    }

                    yield return null;
                }
            }

            // Thoi gian nghi giua cac Wave
            if (wave < totalWaves)
            {
                float remainingBreak = breakDuration;

                while (remainingBreak > 0f)
                {
                    if (gameManager.IsGameOver)
                    {
                        yield break;
                    }

                    remainingBreak = Mathf.Max(
                        0f,
                        remainingBreak - Time.deltaTime
                    );

                    UpdateWaveText(
                        "Nghi truoc Wave " + (wave + 1) +
                        " | " +
                        Mathf.CeilToInt(remainingBreak) + "s"
                    );

                    yield return null;
                }
            }
        }

        // Ket thuc tat ca cac Wave
        enemySpawner.enabled = false;

        if (gameManager.IsGameOver)
        {
            yield break;
        }

        UpdateWaveText(
            "HOAN THANH " + totalWaves + " WAVES!"
        );

        yield return ShowAnnouncement(
            "ALL WAVES COMPLETE!",
            2f
        );

        if (!gameManager.IsGameOver &&
            gameManager.victoryByWaves)
        {
            gameManager.Victory();
        }
    }

    // ========================================
    // RANGED ENEMY - khong thay doi EnemySpawner.
    // ========================================
    private void TrySpawnRangedEnemy(int wave)
    {
        if (rangedEnemyPrefab == null ||
            gameManager == null || gameManager.IsGameOver)
            return;

        activeRangedEnemies.RemoveAll(enemy => enemy == null);

        // Gioi han rieng theo wave; khong tinh Boss la Ranged Enemy.
        int allowed = Mathf.Min(maxRangedEnemies, Mathf.Max(1, wave - rangedStartWave + 1));
        if (activeRangedEnemies.Count >= allowed)
            return;

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("WaveManager: Khong tim thay Main Camera de spawn RangedEnemy!");
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
            return;

        // Chon mot vi tri BEN TRONG camera, uu tien cach xa Player.
        float low = rangedSpawnViewportMargin;
        float high = 1f - rangedSpawnViewportMargin;
        Vector2[] viewportPoints =
        {
            new Vector2(low, low),
            new Vector2(low, high),
            new Vector2(high, low),
            new Vector2(high, high),
            new Vector2(0.5f, low),
            new Vector2(0.5f, high),
            new Vector2(low, 0.5f),
            new Vector2(high, 0.5f)
        };

        float depth = Mathf.Abs(mainCamera.transform.position.z);
        Vector3 spawnPosition = Vector3.zero;
        float bestDistance = -1f;
        bool foundSafeRangedPoint = false;
        LayerMask rangedLayers = safeSpawn != null ? safeSpawn.obstacleLayers : default(LayerMask);

        foreach (Vector2 viewportPoint in viewportPoints)
        {
            Vector3 candidate = mainCamera.ViewportToWorldPoint(
                new Vector3(viewportPoint.x, viewportPoint.y, depth)
            );
            candidate.z = 0f;

            if (safeSpawn != null && !safeSpawn.IsSafe(
                candidate, playerObject.transform,
                rangedMinDistanceFromPlayer, rangedSpawnClearance,
                rangedLayers)) continue;

            float distance = Vector2.Distance(
                candidate, playerObject.transform.position
            );
            if (distance > bestDistance)
            {
                bestDistance = distance;
                spawnPosition = candidate;
                foundSafeRangedPoint = true;
            }
        }

        // No safe corners/edges? Search remaining positions inside the view.
        if (safeSpawn != null && !foundSafeRangedPoint &&
            !safeSpawn.TryFindViewportPoint(
                mainCamera, playerObject.transform, out spawnPosition,
                rangedSpawnViewportMargin, rangedMinDistanceFromPlayer,
                rangedSpawnClearance, 30))
            return;

        GameObject newEnemy = Instantiate(
            rangedEnemyPrefab, spawnPosition, Quaternion.identity
        );
        activeRangedEnemies.Add(newEnemy);

        Debug.Log("Wave " + wave + ": Spawn RangedEnemy tai " + spawnPosition);
    }

    private void SpawnBoss()
    {
        if (bossSpawned || bossPrefab == null)
        {
            return;
        }

        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError(
                "WaveManager: Khong tim thay Main Camera!"
            );

            return;
        }

        // Tim vi tri cach Player xa nhat
        // trong bon goc cua Camera.
        Vector2[] corners = new Vector2[]
        {
            new Vector2(0.15f, 0.15f),
            new Vector2(0.85f, 0.15f),
            new Vector2(0.15f, 0.85f),
            new Vector2(0.85f, 0.85f)
        };

        GameObject player =
            GameObject.FindGameObjectWithTag("Player");

        Vector3 spawnPosition =
            mainCamera.ViewportToWorldPoint(
                new Vector3(0.85f, 0.85f, 0f)
            );

        float bestDistance = -1f;
        bool foundSafeBossPoint = false;
        LayerMask bossLayers = safeSpawn != null ? safeSpawn.obstacleLayers : default(LayerMask);

        foreach (Vector2 corner in corners)
        {
            Vector3 candidate =
                mainCamera.ViewportToWorldPoint(
                    new Vector3(corner.x, corner.y, 0f)
                );
            candidate.z = 0f;

            if (safeSpawn != null && !safeSpawn.IsSafe(
                candidate, player != null ? player.transform : null,
                bossMinDistanceFromPlayer, bossSpawnClearance,
                bossLayers)) continue;

            float distance = 0f;

            if (player != null)
            {
                distance = Vector2.Distance(
                    candidate,
                    player.transform.position
                );
            }

            if (distance > bestDistance)
            {
                bestDistance = distance;
                spawnPosition = candidate;
                foundSafeBossPoint = true;
            }
        }

        if (safeSpawn != null && !foundSafeBossPoint &&
            !safeSpawn.TryFindViewportPoint(
                mainCamera, player != null ? player.transform : null,
                out spawnPosition, 0.12f, bossMinDistanceFromPlayer,
                bossSpawnClearance, bossSpawnAttempts))
        {
            // Do not create boss inside a wall or award fake Victory.
            return;
        }

        spawnPosition.z = 0f;

        GameObject bossObject = Instantiate(
            bossPrefab,
            spawnPosition,
            Quaternion.identity
        );

        bossHealth =
            bossObject.GetComponent<Health>();

        if (bossHealth == null)
        {
            Debug.LogError(
                "Boss duoc tao nhung khong co Health!"
            );

            Destroy(bossObject);
            return;
        }

        bossSpawned = true;
        bossDefeated = false;

        bossHealth.OnDied += OnBossDied;

        if (bossHealthBar != null)
        {
            // Grave Warden uses the purple/gold 6A.1 HP bar attached to its prefab.
            // Do not bind the OLD red HUD. Preserve the existing red bar for
            // Shadow Titan and all other non-Grave-Warden bosses.
            bool isGraveWarden =
                bossObject.GetComponentInChildren<GraveWardenBossAI>(true) != null;

            if (isGraveWarden)
            {
                bossHealthBar.UnbindBoss();
            }
            else
            {
                bossHealthBar.BindBoss(
                    bossHealth,
                    bossName
                );
            }
        }
        else
        {
            Debug.LogWarning(
                "WaveManager: Chua gan BossHealthBarUI!"
            );
        }

        Debug.Log(
            bossName + " da xuat hien!"
        );
    }

    private void OnBossDied()
    {
        bossDefeated = true;

        if (bossHealth != null)
        {
            bossHealth.OnDied -= OnBossDied;
        }

        bossHealth = null;

        Debug.Log(
            bossName + " da bi tieu diet!"
        );
    }

    private void ConfigureWave(int wave)
    {
        enemySpawner.spawnInterval = Mathf.Max(
            minimumSpawnInterval,
            firstWaveSpawnInterval -
            (wave - 1) * intervalReductionPerWave
        );

        enemySpawner.maxEnemies = Mathf.Max(
            1,
            firstWaveMaxEnemies +
            (wave - 1) * extraEnemiesPerWave
        );

        if (wave == 1)
        {
            enemySpawner.normalWeight = 100f;
            enemySpawner.fastWeight = 0f;
            enemySpawner.tankWeight = 0f;
        }
        else if (wave == 2)
        {
            enemySpawner.normalWeight = 70f;
            enemySpawner.fastWeight = 30f;
            enemySpawner.tankWeight = 0f;
        }
        else
        {
            float normal = Mathf.Max(
                35f,
                60f - (wave - 3) * 5f
            );

            float fast = 30f;

            float tank =
                100f - normal - fast;

            enemySpawner.normalWeight = normal;
            enemySpawner.fastWeight = fast;
            enemySpawner.tankWeight = tank;
        }

        Debug.Log(
            "Wave " + wave +
            " | Spawn Interval: " +
            enemySpawner.spawnInterval +
            " | Max Enemies: " +
            enemySpawner.maxEnemies
        );
    }

    private IEnumerator ShowAnnouncement(
        string message,
        float duration
    )
    {
        if (announcementText != null)
        {
            announcementText.text = message;
            announcementText.gameObject.SetActive(true);
        }

        if (duration > 0f)
        {
            yield return new WaitForSeconds(duration);
        }

        if (announcementText != null)
        {
            announcementText.gameObject.SetActive(false);
        }
    }

    private void UpdateWaveText(string message)
    {
        if (waveText != null)
        {
            waveText.text = message;
        }
    }

    private void OnDestroy()
    {
        if (bossHealth != null)
        {
            bossHealth.OnDied -= OnBossDied;
        }
    }
}
