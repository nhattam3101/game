using UnityEngine;

// Stage 4C: optional game-mode modifiers for a map selected before a match.
// Does not edit WaveManager, EnemySpawner, Skills, XP, Boss or enemy prefabs.
[DisallowMultipleComponent]
public class MapBiomeRules4C : MonoBehaviour
{
    [Header("Existing scene components")]
    public WaveManager waveManager;
    public EnemySpawner enemySpawner;
    public Health playerHealth;

    [Header("Meadow: peaceful recovery")]
    [Min(0f)] public float meadowHealAmount = 2f;
    [Min(1f)] public float meadowHealEverySeconds = 12f;

    [Header("Desert: enemy waves arrive faster")]
    [Range(0.25f, 1f)] public float desertSpawnIntervalFactor = 0.8f;
    [Min(0)] public int desertBonusEnemyCap = 2;

    [Header("Ruins: larger groups and shorter breaks")]
    [Min(0)] public int ruinsBonusEnemyCap = 8;
    [Range(0.25f, 1f)] public float ruinsBreakDurationFactor = 0.8f;

    private int selectedMap = -1;
    private int observedWave = -1;
    private float healingTimer;
    private float originalBreakDuration;
    private bool started;

    private void Awake()
    {
        if (waveManager == null) waveManager = GetComponent<WaveManager>();
        if (enemySpawner == null && waveManager != null) enemySpawner = waveManager.enemySpawner;
        if (playerHealth == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerHealth = player.GetComponent<Health>();
        }
    }

    // Called once by the selection screen BEFORE WaveManager is enabled.
    public void StartSelectedMap(int index)
    {
        if (index < 0 || index > 2)
        {
            Debug.LogWarning("MapBiomeRules4C: Unknown map index " + index, this);
            return;
        }
        if (started) return;
        if (waveManager == null)
        {
            Debug.LogError("MapBiomeRules4C: Missing WaveManager!", this);
            return;
        }
        selectedMap = index;
        observedWave = -1;
        healingTimer = 0f;
        originalBreakDuration = waveManager.breakDuration;
        if (selectedMap == 2)
            waveManager.breakDuration = originalBreakDuration * ruinsBreakDurationFactor;
        started = true;
        Debug.Log("[4C] Activated map rules: " + (selectedMap == 0 ? "Meadow" : selectedMap == 1 ? "Desert" : "Ruins"));
    }

    private void LateUpdate()
    {
        if (!started || waveManager == null || Time.timeScale == 0f) return;
        if (waveManager.gameManager != null && waveManager.gameManager.IsGameOver) return;

        int wave = waveManager.CurrentWave;
        if (wave > 0 && wave != observedWave)
        {
            // WaveManager.ConfigureWave writes the base values at the start of each wave.
            // Apply exactly once per wave to avoid multiplying repeatedly.
            observedWave = wave;
            if (enemySpawner != null)
            {
                if (selectedMap == 1)
                {
                    enemySpawner.spawnInterval = Mathf.Max(0.1f,
                        enemySpawner.spawnInterval * desertSpawnIntervalFactor);
                    enemySpawner.maxEnemies += desertBonusEnemyCap;
                }
                else if (selectedMap == 2)
                {
                    enemySpawner.maxEnemies += ruinsBonusEnemyCap;
                }
            }
        }

        if (selectedMap != 0 || wave <= 0 || playerHealth == null ||
            playerHealth.CurrentHealth <= 0f || meadowHealAmount <= 0f) return;

        healingTimer += Time.deltaTime;
        if (healingTimer >= meadowHealEverySeconds)
        {
            healingTimer = 0f;
            playerHealth.Heal(meadowHealAmount);
        }
    }
}
