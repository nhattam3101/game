using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

// Attach ONLY to the WaveManager GameObject. Read-only: never calls AddCoins,
// AddExp, TakeDamage, Victory, any Save API, or modifies a boss component.
[DisallowMultipleComponent]
public sealed class MS7A4BossEncounterTracker : MonoBehaviour
{
    [Header("References (auto-resolved if empty)")]
    public WaveManager waveManager;
    public WaveMapSwitcher mapSwitcher;
    public PlayerProgression playerProgression;
    public Health playerHealth;

    [Header("Observation")]
    [Tooltip("Scans the private bossHealth reference owned by WaveManager; does NOT count manually placed boss prefabs.")]
    [Min(0.05f)] public float pollInterval = 0.15f;
    public bool logSessionSummary = true;
    public bool showMinimalOverlay = false;

    [Header("Interpretation only; no balance fields changed")]
    [Min(1f)] public float titanTargetMinSeconds = 45f;
    [Min(1f)] public float titanTargetMaxSeconds = 110f;
    [Min(1f)] public float wardenTargetMinSeconds = 70f;
    [Min(1f)] public float wardenTargetMaxSeconds = 150f;

    // Static list is shared with the Editor report window during Play Mode.
    public static readonly List<MS7A4EncounterRecord> Sessions = new List<MS7A4EncounterRecord>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetListOnNewPlay() { Sessions.Clear(); }

    private FieldInfo bossHealthField;
    private Health activeBoss;
    private MS7A4EncounterRecord activeRecord;
    private float startGameTime;
    private float lastScanUnscaled;
    private float lastPlayerHP;
    private float lastBossHP;
    private int lastCoins;
    private int firstCoins;
    private int lastExp;
    private int lastLevel;
    private int lastExpThreshold;
    private bool settling;
    private Coroutine settleRoutine;

    public MS7A4EncounterRecord LiveRecord { get { return activeRecord; } }
    public float LiveFightSeconds { get { return activeRecord == null ? 0f : Mathf.Max(0f, Time.time - startGameTime); } }
    public string Diagnostic { get; private set; }

    private void Awake()
    {
        if (waveManager == null) waveManager = GetComponent<WaveManager>();
        if (mapSwitcher == null) mapSwitcher = GetComponent<WaveMapSwitcher>();
        bossHealthField = typeof(WaveManager).GetField("bossHealth", BindingFlags.Instance | BindingFlags.NonPublic);
        Diagnostic = bossHealthField == null ? "WaveManager.bossHealth not found; check project version." : "Ready";
        ResolvePlayer();
    }

    private void ResolvePlayer()
    {
        if (playerProgression != null && playerHealth != null) return;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        if (playerProgression == null) playerProgression = player.GetComponent<PlayerProgression>();
        if (playerHealth == null) playerHealth = player.GetComponent<Health>();
    }

    private Health ReadWaveManagerBoss()
    {
        if (waveManager == null || bossHealthField == null) return null;
        try { return bossHealthField.GetValue(waveManager) as Health; }
        catch (Exception ex) { Diagnostic = "Boss read failed: " + ex.Message; return null; }
    }

    private void Update()
    {
        if (Time.unscaledTime - lastScanUnscaled < Mathf.Max(0.05f, pollInterval)) return;
        lastScanUnscaled = Time.unscaledTime;
        if (activeRecord == null)
        {
            Health boss = ReadWaveManagerBoss();
            if (boss != null && boss.CurrentHealth > 0f) Begin(boss);
            return;
        }
        SampleEconomy();
        if (!settling && playerHealth != null && playerHealth.CurrentHealth <= 0f)
            Finish("Player defeated");
        if (!settling && activeBoss == null) Finish("Boss disappeared (unverified)");
    }

    private void Begin(Health boss)
    {
        ResolvePlayer();
        if (playerProgression == null || playerHealth == null)
        {
            Diagnostic = "Player Health/PlayerProgression missing; cannot start encounter.";
            return;
        }
        activeBoss = boss;
        string bossLabel = boss.GetComponent<GraveWardenBossAI>() != null ? "Grave Warden" :
            (boss.GetComponent<BossSpecialAttack>() != null || boss.GetComponent<BossVoidBurstAttack>() != null ? "Shadow Titan" :
            (string.IsNullOrEmpty(waveManager.bossName) ? boss.name : waveManager.bossName));
        string mapLabel = "Unknown map";
        if (mapSwitcher != null)
        {
            int idx = mapSwitcher.ActiveMapIndex;
            if (idx >= 0 && idx < mapSwitcher.maps.Count && mapSwitcher.maps[idx] != null)
                mapLabel = mapSwitcher.maps[idx].mapName;
            else mapLabel = "Map index " + idx;
        }
        activeRecord = new MS7A4EncounterRecord
        {
            bossName = bossLabel,
            mapName = mapLabel,
            sceneName = SceneManager.GetActiveScene().name,
            wave = waveManager.CurrentWave,
            utcDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"),
            bossInitialHP = boss.CurrentHealth,
            playerHPAtStart = playerHealth.CurrentHealth,
            result = "IN PROGRESS",
            note = "Gold/EXP and HP loss include all sources during the boss encounter; not boss-only attribution."
        };
        startGameTime = Time.time; // Scaled time excludes pauses.
        lastPlayerHP = playerHealth.CurrentHealth;
        lastBossHP = boss.CurrentHealth;
        firstCoins = lastCoins = playerProgression.coins;
        lastExp = playerProgression.currentExp;
        lastLevel = playerProgression.level;
        lastExpThreshold = Mathf.Max(1, playerProgression.expToNextLevel);
        boss.OnHealthChanged += OnBossHealthChanged;
        boss.OnDied += OnBossDied;
        playerHealth.OnHealthChanged += OnPlayerHealthChanged;
        playerProgression.OnProgressChanged += OnPlayerProgressChanged;
        Diagnostic = "Encounter recording: " + bossLabel + " on " + mapLabel;
        if (logSessionSummary) Debug.Log("[7A.4] START " + Diagnostic);
    }

    private void OnBossHealthChanged(float hp, float maxHp)
    {
        if (activeRecord == null || settling) return;
        float lost = Mathf.Max(0f, lastBossHP - hp);
        if (lost > 0f) { activeRecord.bossDamageObserved += lost; activeRecord.bossHitEvents++; }
        lastBossHP = hp;
    }

    private void OnPlayerHealthChanged(float hp, float maxHp)
    {
        if (activeRecord == null || settling) return;
        float lost = Mathf.Max(0f, lastPlayerHP - hp);
        if (lost > 0f) { activeRecord.playerHPLost += lost; activeRecord.playerDamageEvents++; }
        lastPlayerHP = hp;
    }

    private void OnPlayerProgressChanged() { SampleEconomy(); }

    private void SampleEconomy()
    {
        if (activeRecord == null || playerProgression == null) return;
        int coins = playerProgression.coins;
        if (coins > lastCoins) activeRecord.goldEarned += coins - lastCoins;
        activeRecord.goldNetChange = coins - firstCoins;
        lastCoins = coins;

        int level = playerProgression.level;
        int exp = playerProgression.currentExp;
        if (level < lastLevel)
        {
            activeRecord.note += " [WARNING: Player level decreased; EXP attribution may be incomplete.]";
        }
        else
        {
            // PlayerProgression.AddExp spends expToNextLevel at EACH level-up.
            // Reconstruct thresholds so multiple levels gained between samples
            // are not mistaken for negative EXP.
            int gained = CalculateExpEarned(lastExp, lastLevel, lastExpThreshold, exp, level);
            if (gained > 0) activeRecord.expEarned += gained;
        }
        lastLevel = level;
        lastExp = exp;
        lastExpThreshold = Mathf.Max(1, playerProgression.expToNextLevel);
    }

    // Public for Editor-only isolated math QA. Mirrors PlayerProgression.AddExp.
    public static int CalculateExpEarned(int previousExp, int previousLevel, int previousThreshold,
        int currentExp, int currentLevel)
    {
        if (currentLevel < previousLevel) return 0;
        long gained = (long)currentExp - previousExp;
        int threshold = Mathf.Max(1, previousThreshold);
        int steps = Math.Min(500, Math.Max(0, currentLevel - previousLevel));
        for (int i = 0; i < steps; i++)
        {
            gained += threshold;
            threshold = Mathf.CeilToInt(threshold * 1.3f);
        }
        return gained > 0 ? (int)Math.Min(gained, int.MaxValue) : 0;
    }

    private void OnBossDied()
    {
        if (activeRecord == null || settling) return;
        // Defer one unscaled frame to include any OTHER OnDied listeners giving rewards.
        settling = true;
        settleRoutine = StartCoroutine(EndAfterRewardEvents());
    }

    private IEnumerator EndAfterRewardEvents()
    {
        yield return null;
        yield return new WaitForSecondsRealtime(0.1f);
        SampleEconomy();
        Finish("Boss defeated");
        settleRoutine = null;
    }

    private void Finish(string outcome)
    {
        if (activeRecord == null) return;
        SampleEconomy();
        activeRecord.result = outcome;
        activeRecord.fightSeconds = Mathf.Max(0f, Time.time - startGameTime);
        Sessions.Add(activeRecord);
        if (logSessionSummary)
            Debug.Log("[7A.4] " + outcome + " / " + activeRecord.bossName +
                " / " + activeRecord.mapName + " / " + activeRecord.fightSeconds.ToString("F1") +
                "s / HP loss=" + activeRecord.playerHPLost.ToString("F1") +
                " / hit-events=" + activeRecord.playerDamageEvents +
                " / run Gold+=" + activeRecord.goldEarned + " / EXP+=" + activeRecord.expEarned);
        CleanupBindings();
        activeRecord = null;
        activeBoss = null;
        settling = false;
    }

    private void CleanupBindings()
    {
        if (activeBoss != null)
        {
            activeBoss.OnHealthChanged -= OnBossHealthChanged;
            activeBoss.OnDied -= OnBossDied;
        }
        if (playerHealth != null) playerHealth.OnHealthChanged -= OnPlayerHealthChanged;
        if (playerProgression != null) playerProgression.OnProgressChanged -= OnPlayerProgressChanged;
    }

    private void OnDisable()
    {
        if (activeRecord != null) Finish(settling ? "Boss defeated (scene closed before reward settle)" : "Interrupted / scene unloaded");
        CleanupBindings();
    }

    private void OnGUI()
    {
        if (!showMinimalOverlay || !Application.isPlaying) return;
        string status = activeRecord == null ? "7A.4: Awaiting WaveManager boss" :
            "7A.4 " + activeRecord.bossName + " | HP lost " + activeRecord.playerHPLost.ToString("F0") +
            " | Gold+ " + activeRecord.goldEarned + " | EXP+ " + activeRecord.expEarned;
        GUI.Box(new Rect(10, 10, Mathf.Min(Screen.width - 20, 550), 36), status);
    }
}
