using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

// Install ON the existing WaveManager in SampleScene. No gameplay or 5A core edits.
// Only synchronizes AFTER MS5A3PersistentRun.Committed; never records GameOver/Retry.
[DisallowMultipleComponent]
public sealed class MS7H2VictoryProgressObserver : MonoBehaviour
{
    public MS5A3PersistentRun persistentRun;
    public WaveManager waveManager;
    public PlayerProgression progression;
    [SerializeField] private int capturedSlot;
    [SerializeField] private int baselineCoins;
    [SerializeField] private bool bossDefeated;
    [SerializeField] private bool finished;
    [SerializeField] private int syncAttempts;
    [SerializeField] private string status = "Waiting for saved Victory";
    public string Status { get { return status; } }
    public int SyncAttempts { get { return syncAttempts; } }
    public bool BossDefeated { get { return bossDefeated; } }
    public bool Finished { get { return finished; } }

    private static readonly FieldInfo BossHealthField = typeof(WaveManager).GetField("bossHealth", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo StartingCoinsField = typeof(MS5A3PersistentRun).GetField("startingCoins", BindingFlags.NonPublic | BindingFlags.Instance);
    private Health observedBoss;
    private float nextAttempt;
    private bool initialized;

    private void Start()
    {
        if (SceneManager.GetActiveScene().name != "SampleScene") { enabled = false; return; }
        capturedSlot = MS5A2GoldBank.SelectedSlot;
        if (!persistentRun) persistentRun = FindFirstObjectByType<MS5A3PersistentRun>();
        if (!waveManager) waveManager = GetComponent<WaveManager>();
        if (!progression) progression = FindFirstObjectByType<PlayerProgression>();
        baselineCoins = progression ? progression.coins : 0;
        if (persistentRun != null && StartingCoinsField != null)
        {
            object original = StartingCoinsField.GetValue(persistentRun);
            if (original is int) baselineCoins = (int)original;
        }
        initialized = persistentRun != null && waveManager != null && progression != null && BossHealthField != null;
        status = initialized ? "Watching real 5A3 Victory and WaveManager boss" :
            "PENDING: 5A3/Progression/WaveManager.bossHealth missing; no progress writes";
        if (!initialized) Debug.LogWarning("[7H2] " + status, this);
    }
    private void Update()
    {
        if (!initialized || finished || !Application.isPlaying) return;
        ObserveRealBoss();
        if (!persistentRun.Committed) return;
        if (Time.unscaledTime < nextAttempt) return;
        nextAttempt = Time.unscaledTime + 2f;
        Sync();
    }
    private void ObserveRealBoss()
    {
        if (observedBoss != null || waveManager == null) return;
        Health current = BossHealthField.GetValue(waveManager) as Health;
        if (current == null || current.CurrentHealth <= 0f) return;
        observedBoss = current;
        observedBoss.OnDied += OnRealBossDied;
    }
    private void OnRealBossDied()
    {
        // Only a Health reference owned by WaveManager.bossHealth can set this flag.
        bossDefeated = true;
        status = "WaveManager Boss OnDied observed; waiting for 5A3 committed Victory";
    }
    private void Sync()
    {
        if (!persistentRun || !persistentRun.Committed || !progression) return;
        syncAttempts++;
        int gold = Mathf.Max(0, progression.coins - baselineCoins);
        string result;
        bool ok = MS7H2ProgressService.TrySyncVictory(capturedSlot, gold, bossDefeated, out result);
        status = result;
        if (ok || result == "Run already synchronized")
        {
            finished = true;
            Debug.Log("[7H2] " + status, this);
        }
        else if (syncAttempts == 1 || syncAttempts % 8 == 0)
            Debug.LogWarning("[7H2] Sync pending (retry every 2s): " + status, this);
    }
    [ContextMenu("7H.2 / Retry saved Victory progress")]
    public void RetryProgressSync()
    {
        if (Application.isPlaying && initialized && persistentRun && persistentRun.Committed)
            Sync();
    }
    private void OnDisable()
    {
        if (observedBoss != null) observedBoss.OnDied -= OnRealBossDied;
        if (initialized && !finished && Application.isPlaying && persistentRun && persistentRun.Committed)
            Sync();
    }
}
