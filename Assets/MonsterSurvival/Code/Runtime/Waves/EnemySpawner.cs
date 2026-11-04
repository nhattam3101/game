
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public event System.Action<GameObject> OnEnemySpawned;

    [Header("Enemy Prefabs")]

    // Quai thuong - giu lai bien cu
    public GameObject enemyPrefab;

    // Quai di chuyen nhanh
    public GameObject fastEnemyPrefab;

    // Quai nhieu mau
    public GameObject tankEnemyPrefab;


    [Header("Spawn Settings")]

    [Min(0.1f)]
    public float spawnInterval = 2f;

    [Min(1)]
    public int maxEnemies = 20;


    [Header("Spawn Weights")]

    [Min(0f)]
    public float normalWeight = 65f;

    [Min(0f)]
    public float fastWeight = 25f;

    [Min(0f)]
    public float tankWeight = 10f;


    [Header("Spawn Area")]

    [Range(0f, 0.4f)]
    public float screenMargin = 0.08f;

    [Tooltip("Spawn from the camera border so enemies enter the fight instead of appearing on top of the player.")]
    public bool spawnFromViewportEdge = true;

    [Range(1, 30)]
    public int fallbackPositionAttempts = 12;

    [Header("Safe Spawn 4B.3 (optional)")]
    public MapSafeSpawn2D safeSpawn;
    [Min(0f)] public float minDistanceFromPlayer = 3.5f;
    [Min(0.05f)] public float spawnClearanceRadius = 0.45f;
    [Range(1, 100)] public int safeSpawnAttempts = 30;


    private Camera mainCamera;
    private float spawnTimer;
    private Transform player;


    private void Awake()
    {
        mainCamera = Camera.main;
        if (safeSpawn == null) safeSpawn = FindFirstObjectByType<MapSafeSpawn2D>();

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null) player = playerObject.transform;

        if (mainCamera == null)
        {
            Debug.LogError(
                "EnemySpawner: Khong tim thay Main Camera!"
            );
        }
    }

    private void OnEnable()
    {
        // Every wave gets a predictable opening beat instead of inheriting an old timer.
        spawnTimer = 0f;
    }


    private void Update()
    {
        // Khong spawn khi game tam dung
        if (Time.timeScale == 0f)
            return;

        if (mainCamera == null)
            return;

        // Khong co prefab nao
        if (enemyPrefab == null &&
            fastEnemyPrefab == null &&
            tankEnemyPrefab == null)
        {
            return;
        }

        spawnTimer += Time.deltaTime;

        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f;

            SpawnEnemy();
        }
    }


    private void SpawnEnemy()
    {
        // Dem tat ca quai co Tag Enemy
        GameObject[] enemies =
            GameObject.FindGameObjectsWithTag("Enemy");

        if (enemies.Length >= maxEnemies)
            return;


        // Chon ngau nhien loai quai
        GameObject selectedPrefab =
            SelectEnemyPrefab();

        if (selectedPrefab == null)
            return;


        // Gioi han vi tri ben trong Camera
        float minX = screenMargin;
        float maxX = 1f - screenMargin;

        float minY = screenMargin;
        float maxY = 1f - screenMargin;


        Vector3 viewportPosition = RandomViewportPoint(minX, maxX, minY, maxY);


        // Chuyen toa do Camera sang World
        Vector3 spawnPosition =
            mainCamera.ViewportToWorldPoint(
                viewportPosition
            );

        spawnPosition.z = 0f;

        // 4B.3: Neu da gan MapSafeSpawn2D, chi spawn tren ground an toan.
        // Khong fallback sang vi tri nguy hiem neu het lan thu.
        if (safeSpawn != null)
        {
            bool foundSafeEdge = false;
            int attempts = Mathf.Max(1, fallbackPositionAttempts);
            LayerMask obstacleLayers = safeSpawn.obstacleLayers;

            while (attempts-- > 0)
            {
                viewportPosition = RandomViewportPoint(minX, maxX, minY, maxY);
                Vector3 candidate = mainCamera.ViewportToWorldPoint(viewportPosition);
                candidate.z = 0f;

                if (!safeSpawn.IsSafe(candidate, player, minDistanceFromPlayer,
                    spawnClearanceRadius, obstacleLayers))
                    continue;

                spawnPosition = candidate;
                foundSafeEdge = true;
                break;
            }

            if (!foundSafeEdge && !safeSpawn.TryFindViewportPoint(
                    mainCamera, player, out spawnPosition,
                    screenMargin, minDistanceFromPlayer,
                    spawnClearanceRadius, safeSpawnAttempts))
                return;
        }
        else if (player != null)
        {
            int attempts = Mathf.Max(1, fallbackPositionAttempts);
            while (Vector2.Distance(spawnPosition, player.position) < minDistanceFromPlayer &&
                   --attempts > 0)
            {
                viewportPosition = RandomViewportPoint(minX, maxX, minY, maxY);
                spawnPosition = mainCamera.ViewportToWorldPoint(viewportPosition);
                spawnPosition.z = 0f;
            }

            if (Vector2.Distance(spawnPosition, player.position) < minDistanceFromPlayer)
                return;
        }

        // Tao quai tai vi tri ngau nhien
        GameObject spawned = Instantiate(
            selectedPrefab,
            spawnPosition,
            Quaternion.identity
        );

        OnEnemySpawned?.Invoke(spawned);
    }


    private Vector3 RandomViewportPoint(float minX, float maxX, float minY, float maxY)
    {
        if (!spawnFromViewportEdge)
            return new Vector3(Random.Range(minX, maxX), Random.Range(minY, maxY), 0f);

        switch (Random.Range(0, 4))
        {
            case 0: return new Vector3(minX, Random.Range(minY, maxY), 0f);
            case 1: return new Vector3(maxX, Random.Range(minY, maxY), 0f);
            case 2: return new Vector3(Random.Range(minX, maxX), minY, 0f);
            default: return new Vector3(Random.Range(minX, maxX), maxY, 0f);
        }
    }


    // Chon loai quai dua tren trong so
    private GameObject SelectEnemyPrefab()
    {
        // Chi tinh trong so cua Prefab da gan
        float normal = enemyPrefab != null
            ? Mathf.Max(0f, normalWeight)
            : 0f;

        float fast = fastEnemyPrefab != null
            ? Mathf.Max(0f, fastWeight)
            : 0f;

        float tank = tankEnemyPrefab != null
            ? Mathf.Max(0f, tankWeight)
            : 0f;


        float totalWeight =
            normal + fast + tank;


        // Neu tat ca trong so bang 0,
        // uu tien Prefab co san
        if (totalWeight <= 0f)
        {
            if (enemyPrefab != null)
                return enemyPrefab;

            if (fastEnemyPrefab != null)
                return fastEnemyPrefab;

            return tankEnemyPrefab;
        }


        // Random theo tong trong so
        float randomValue =
            Random.value * totalWeight;


        // Quai thuong
        if (randomValue < normal)
        {
            return enemyPrefab;
        }

        randomValue -= normal;


        // Quai nhanh
        if (randomValue < fast)
        {
            return fastEnemyPrefab;
        }


        // Quai Tank
        return tankEnemyPrefab;
    }


    private void OnDrawGizmosSelected()
    {
        // Khong thay doi khu vuc spawn
        // Phan nay de danh cho mo rong sau
    }
}
