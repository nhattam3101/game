using UnityEngine;

// Optional wave balancing overlay. No changes to WaveManager / EnemySpawner source.
// WaveManager.ConfigureWave executes at wave start; this overlay follows in Update,
// during the Wave announcement, before EnemySpawner is enabled for spawning.
[DisallowMultipleComponent]
public sealed class MS7A1WaveBalanceApplier : MonoBehaviour
{
    [Header("References")]
    public WaveManager waveManager;
    public MS7A1WaveBalanceProfile profile;

    [Header("Behaviour")]
    [Tooltip("False = audit/preview only; True = write spawn configuration at each wave start.")]
    public bool applyAtRuntime = false;
    public bool logChanges = true;

    public int LastAppliedWave { get; private set; }

    private EnemySpawner activeSpawner;
    private int observedWave = -1;
    private bool snapshotValid;
    private int snapshotWave;
    private float beforeInterval;
    private int beforeMax;
    private float beforeNormal, beforeFast, beforeTank;
    private float beforeRangedInterval;
    private int beforeMaxRanged;

    private void Awake()
    {
        if (waveManager == null) waveManager = GetComponent<WaveManager>();
    }

    private void OnEnable()
    {
        observedWave = -1;
        LastAppliedWave = 0;
        snapshotValid = false;
    }

    private void Update()
    {
        if (waveManager == null || profile == null) return;
        int wave = waveManager.CurrentWave;
        if (wave < 1 || wave == observedWave) return;
        observedWave = wave;

        MS7A1WaveBalanceProfile.WaveRow row;
        if (!profile.TryGetRow(wave, out row) || !MS7A1WaveBalanceProfile.ValidRow(row))
        {
            Debug.LogWarning("[7A.1] Missing/invalid/duplicate profile row for Wave " + wave + ". Original WaveManager values kept.", this);
            return;
        }

        if (!applyAtRuntime)
        {
            if (logChanges) Debug.Log("[7A.1 PREVIEW] Wave " + wave + " -> interval " + row.spawnInterval + "s, max " + row.maxEnemies + ", weights " + row.normalWeight + "/" + row.fastWeight + "/" + row.tankWeight, this);
            return;
        }
        if (waveManager.enemySpawner == null) return;

        // Snapshot this wave's ORIGINAL configured values for safe toggle-off.
        activeSpawner = waveManager.enemySpawner;
        snapshotWave = wave;
        beforeInterval = activeSpawner.spawnInterval;
        beforeMax = activeSpawner.maxEnemies;
        beforeNormal = activeSpawner.normalWeight;
        beforeFast = activeSpawner.fastWeight;
        beforeTank = activeSpawner.tankWeight;
        beforeRangedInterval = waveManager.rangedSpawnInterval;
        beforeMaxRanged = waveManager.maxRangedEnemies;
        snapshotValid = true;

        activeSpawner.spawnInterval = Mathf.Max(0.1f, row.spawnInterval);
        activeSpawner.maxEnemies = Mathf.Max(1, row.maxEnemies);
        activeSpawner.normalWeight = row.normalWeight;
        activeSpawner.fastWeight = row.fastWeight;
        activeSpawner.tankWeight = row.tankWeight;

        // Optional Ranged Enemy tuning; does not change rangedStartWave or the prefab.
        waveManager.rangedSpawnInterval = Mathf.Max(0.1f, row.rangedSpawnInterval);
        waveManager.maxRangedEnemies = Mathf.Max(1, row.maxRangedEnemies);
        LastAppliedWave = wave;

        if (logChanges)
            Debug.Log("[7A.1 ACTIVE] Wave " + wave + ": interval=" + row.spawnInterval +
                " max=" + row.maxEnemies + " normal/fast/tank=" +
                row.normalWeight + "/" + row.fastWeight + "/" + row.tankWeight +
                " rangedInterval=" + row.rangedSpawnInterval, this);
    }

    private void OnDisable()
    {
        RestoreIfCurrentWave();
    }

    private void OnDestroy()
    {
        RestoreIfCurrentWave();
    }

    private void RestoreIfCurrentWave()
    {
        if (!snapshotValid || waveManager == null || activeSpawner == null ||
            waveManager.CurrentWave != snapshotWave) return;

        // Restore only if the runtime override is still present. This protects
        // changes made later by another system as far as possible.
        MS7A1WaveBalanceProfile.WaveRow row;
        if (profile == null || !profile.TryGetRow(snapshotWave, out row)) return;
        if (Mathf.Approximately(activeSpawner.spawnInterval, row.spawnInterval)) activeSpawner.spawnInterval = beforeInterval;
        if (activeSpawner.maxEnemies == row.maxEnemies) activeSpawner.maxEnemies = beforeMax;
        if (Mathf.Approximately(activeSpawner.normalWeight, row.normalWeight)) activeSpawner.normalWeight = beforeNormal;
        if (Mathf.Approximately(activeSpawner.fastWeight, row.fastWeight)) activeSpawner.fastWeight = beforeFast;
        if (Mathf.Approximately(activeSpawner.tankWeight, row.tankWeight)) activeSpawner.tankWeight = beforeTank;
        if (Mathf.Approximately(waveManager.rangedSpawnInterval, row.rangedSpawnInterval)) waveManager.rangedSpawnInterval = beforeRangedInterval;
        if (waveManager.maxRangedEnemies == row.maxRangedEnemies) waveManager.maxRangedEnemies = beforeMaxRanged;
        snapshotValid = false;
    }
}
