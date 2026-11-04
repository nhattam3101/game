using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 7F.2 rotating arena events. Opt-in OFF by default; reuses existing WaveManager / Player Health.
/// Temporarily suspends the 7F.1 director while enabled, restores its original setting on exit.
/// Does not edit WaveManager, Boss, Save, Economy, PlayerStats or Elite scripts.
/// </summary>
[DisallowMultipleComponent]
public sealed class MS7F2ArenaEventDirector : MonoBehaviour
{
    [Header("Original systems (do not duplicate)")]
    public WaveManager waveManager;
    public EnemySpawner enemySpawner;
    public MS7F1ArenaHazardDirector legacy7F1;

    [Header("Opt-in safety: OFF after install")]
    public bool enableMultipleEvents = false;
    public bool suspend7F1WhileActive = true;
    [Min(1)] public int firstAffectedWave = 2;
    public bool excludeBossWave = true;

    [Header("Short wave cadence")]
    [Min(0f)] public float firstEventDelay = 0.8f;
    [Min(0.6f)] public float nextEventInterval = 2.2f;
    [Min(1)] public int maxEventsPerWave = 1;
    [Min(1)] public int maxConcurrentEvents = 1;

    [Header("Warning / single impact")]
    [Min(0.2f)] public float warningSeconds = 0.9f;
    [Min(0.1f)] public float impactVisibleSeconds = 0.55f;
    [Min(0f)] public float impactDamage = 6f;

    [Header("Shapes")]
    [Min(0.25f)] public float twinCircleRadius = 1.08f;
    [Min(0.1f)] public float twinCenterOffset = 0.84f;
    [Min(0.2f)] public float crossfireWidth = 0.72f;
    [Min(0.5f)] public float crossfireHalfLength = 2.8f;
    [Min(0.15f)] public float ringInnerRadius = 0.7f;
    [Min(0.45f)] public float ringOuterRadius = 2.05f;

    [Header("Visual / placement")]
    public Color warningTint = new Color(0.63f, 0.38f, 0.97f, 0.96f);
    public Color activeTint = new Color(1f, 0.55f, 0.24f, 0.98f);
    public LayerMask blockedLayers;
    [Min(0f)] public float blockedCheckRadius = 0.3f;
    [Min(20)] public int maxTraceEntries = 150;
    public bool logToConsole = false;

    public int TwinStarts { get; private set; }
    public int CrossfireStarts { get; private set; }
    public int RingStarts { get; private set; }
    public int TotalEvents { get; private set; }
    public int TotalImpacts { get; private set; }
    public int PlayerInDangerImpacts { get; private set; }
    public int PlayerHpReducedImpacts { get; private set; }
    public int WaveCancelled { get; private set; }
    public int BossSkips { get; private set; }
    public int LegacySuspensions { get; private set; }
    public int LegacyRestores { get; private set; }
    public bool PauseObserved { get; private set; }
    public int ActiveEventCount { get { return events.Count; } }
    public int ActiveWave { get; private set; } = -1;

    private readonly List<MS7F2ArenaEvent> events = new List<MS7F2ArenaEvent>();
    private readonly List<string> trace = new List<string>();
    private Transform player;
    private Health playerHealth;
    private int eventsInWave;
    private float countdown;
    private bool wasEligible;
    private int lastBossSkip = -1;
    private bool legacySuspended;
    private bool legacyPriorEnabled;

    public static bool Eligible(bool optedIn, int wave, int firstWave, int totalWaves,
        int bossWave, bool spawnerActive, bool gameOver, bool paused)
    {
        // Boss wave ALWAYS excluded: keep boss mechanics isolated even if user changes options.
        return optedIn && !paused && !gameOver && spawnerActive &&
            wave >= firstWave && wave <= totalWaves && wave != bossWave;
    }

    public static MS7F2ArenaEvent.EventKind ForWave(int wave, int firstWave)
    {
        int n = Mathf.Max(0, wave - firstWave) % 3;
        return n == 0 ? MS7F2ArenaEvent.EventKind.TwinStrike :
               n == 1 ? MS7F2ArenaEvent.EventKind.Crossfire :
                        MS7F2ArenaEvent.EventKind.HollowRing;
    }

    private void Awake() { Resolve(); }
    private void Start() { if (enableMultipleEvents) SuspendLegacy(); }

    private void Resolve()
    {
        if (waveManager == null) waveManager = GetComponent<WaveManager>();
        if (waveManager != null && enemySpawner == null) enemySpawner = waveManager.enemySpawner;
        if (legacy7F1 == null) legacy7F1 = GetComponent<MS7F1ArenaHazardDirector>();
        if (player == null)
        {
            GameObject obj = GameObject.FindGameObjectWithTag("Player");
            if (obj != null) { player = obj.transform; playerHealth = obj.GetComponent<Health>(); }
        }
    }

    public bool TryValidate(out string issue)
    {
        Resolve();
        if (waveManager == null || enemySpawner == null) { issue = "Missing WaveManager or EnemySpawner"; return false; }
        if (waveManager.enemySpawner != enemySpawner) { issue = "Spawner reference mismatch"; return false; }
        if (player == null || playerHealth == null) { issue = "Player tag or Health missing"; return false; }
        if (legacy7F1 != null && legacy7F1.enableArenaHazards && !suspend7F1WhileActive)
        { issue = "7F.1 currently ON; turn on suspend7F1WhileActive to prevent overlapping hazards"; return false; }
        issue = "References valid; opt-in is deliberate. Boss wave is always protected";
        return true;
    }

    public bool IsStillEligible
    {
        get
        {
            if (waveManager == null || enemySpawner == null) return false;
            bool gameOver = waveManager.gameManager != null && waveManager.gameManager.IsGameOver;
            return wasEligible && ActiveWave == waveManager.CurrentWave &&
                   Eligible(enableMultipleEvents, waveManager.CurrentWave, firstAffectedWave,
                       waveManager.totalWaves, waveManager.bossWave, enemySpawner.enabled, gameOver, false);
        }
    }

    private void SuspendLegacy()
    {
        if (!suspend7F1WhileActive || legacySuspended || legacy7F1 == null) return;
        legacyPriorEnabled = legacy7F1.enableArenaHazards;
        // Preserve the original flag (even if false); never edit scene assets at runtime.
        legacy7F1.enableArenaHazards = false;
        legacySuspended = true;
        LegacySuspensions++;
        Log("7F.1 SUSPEND (remembered enabled=" + legacyPriorEnabled + ")");
    }

    private void RestoreLegacy()
    {
        if (!legacySuspended) return;
        if (legacy7F1 != null) legacy7F1.enableArenaHazards = legacyPriorEnabled;
        legacySuspended = false;
        LegacyRestores++;
        Log("7F.1 RESTORE (original enabled=" + legacyPriorEnabled + ")");
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        Resolve();
        if (!enableMultipleEvents)
        {
            if (wasEligible) StopWave("opt-out");
            RestoreLegacy();
            return;
        }
        // Freeze warning, impact, and scheduling during pause, without touching 7F.1.
        if (Time.timeScale <= 0f) { PauseObserved = true; return; }
        if (waveManager == null || enemySpawner == null) return;
        if (!legacySuspended) SuspendLegacy();
        if (legacy7F1 != null && legacy7F1.enableArenaHazards)
        {
            // Never run both hazard systems together if suspension was disabled in Inspector.
            if (wasEligible) StopWave("7F.1 overlap protection");
            return;
        }

        int wave = waveManager.CurrentWave;
        bool gameOver = waveManager.gameManager != null && waveManager.gameManager.IsGameOver;
        bool eligible = Eligible(true, wave, firstAffectedWave, waveManager.totalWaves,
            waveManager.bossWave, enemySpawner.enabled, gameOver, false);
        if (!eligible)
        {
            if (wasEligible) StopWave("break / boss / game over");
            if (wave == waveManager.bossWave && lastBossSkip != wave)
            { BossSkips++; lastBossSkip = wave; Log("BOSS SKIP wave=" + wave); }
            return;
        }
        if (!wasEligible || ActiveWave != wave)
        {
            if (wasEligible) StopWave("wave changed");
            wasEligible = true;
            ActiveWave = wave;
            countdown = Mathf.Max(0f, firstEventDelay);
            eventsInWave = 0;
            Log("WAVE START " + wave + " => " + ForWave(wave, firstAffectedWave));
        }
        events.RemoveAll(x => x == null);
        if (eventsInWave >= maxEventsPerWave || events.Count >= maxConcurrentEvents) return;
        countdown -= Time.deltaTime;
        if (countdown > 0f) return;
        countdown = Mathf.Max(0.6f, nextEventInterval);
        TrySpawnEvent();
    }

    private void TrySpawnEvent()
    {
        string issue;
        if (!TryValidate(out issue)) { Log("SKIP: " + issue); return; }
        Vector3 point = player.position; point.z = 0f;
        if (blockedLayers.value != 0 &&
            Physics2D.OverlapCircle(point, blockedCheckRadius, blockedLayers) != null)
        { Log("SKIP: location blocked by specified layers"); return; }
        MS7F2ArenaEvent.EventKind kind = ForWave(ActiveWave, firstAffectedWave);
        GameObject go = new GameObject("MS7F2_" + kind + "_RUNTIME");
        MS7F2ArenaEvent e = go.AddComponent<MS7F2ArenaEvent>();
        events.Add(e);
        e.Initialize(this, player, playerHealth, point, kind, Random.Range(0f, 180f),
            warningSeconds, impactVisibleSeconds, impactDamage,
            twinCircleRadius, twinCenterOffset, crossfireWidth, crossfireHalfLength,
            ringInnerRadius, ringOuterRadius, warningTint, activeTint);
        eventsInWave++;
        TotalEvents++;
        if (kind == MS7F2ArenaEvent.EventKind.TwinStrike) TwinStarts++;
        else if (kind == MS7F2ArenaEvent.EventKind.Crossfire) CrossfireStarts++;
        else RingStarts++;
        Log("TELEGRAPH wave=" + ActiveWave + " kind=" + kind + " damage=" + impactDamage.ToString("F1"));
    }

    public void RecordImpact(MS7F2ArenaEvent.EventKind kind, bool inDanger, bool hpReduced)
    {
        TotalImpacts++;
        if (inDanger) PlayerInDangerImpacts++;
        if (hpReduced) PlayerHpReducedImpacts++;
        Log("IMPACT " + kind + " inside=" + inDanger + " HP_reduced=" + hpReduced + " (shield can block)");
    }

    public void RecordEventFinished(MS7F2ArenaEvent e) { events.Remove(e); }

    private void StopWave(string reason)
    {
        int n = events.Count;
        for (int i = events.Count - 1; i >= 0; i--)
            if (events[i] != null) events[i].Cancel();
        events.Clear();
        WaveCancelled += n;
        Log("WAVE STOP " + ActiveWave + " | cancelled=" + n + " | " + reason);
        ActiveWave = -1;
        eventsInWave = 0;
        wasEligible = false;
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        if (wasEligible) StopWave("component disabled");
        RestoreLegacy();
    }
    private void OnDestroy()
    {
        if (!Application.isPlaying) return;
        if (wasEligible) StopWave("component destroyed");
        RestoreLegacy();
    }

    public string GetRuntimeReport()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("7F.2 MULTIPLE ARENA EVENTS - LIVE READ ONLY");
        sb.AppendLine("Playing=" + Application.isPlaying + " Enabled=" + enableMultipleEvents +
            " CurrentWave=" + (waveManager != null ? waveManager.CurrentWave.ToString() : "?") +
            " ActiveEventCount=" + ActiveEventCount);
        sb.AppendLine("Twin=" + TwinStarts + " Crossfire=" + CrossfireStarts + " HollowRing=" + RingStarts +
            " Events=" + TotalEvents + " Impacts=" + TotalImpacts +
            " InDanger=" + PlayerInDangerImpacts + " HpReduced=" + PlayerHpReducedImpacts);
        sb.AppendLine("Cancelled=" + WaveCancelled + " BossSkips=" + BossSkips +
            " PauseSeen=" + PauseObserved + " Legacy7F1Suspensions=" + LegacySuspensions +
            " Legacy7F1Restores=" + LegacyRestores + " CurrentlySuspended=" + legacySuspended);
        sb.AppendLine("NOTE: HP reduction counts are not direct proof of visible pixels or Save; shields may block damage.");
        sb.AppendLine("--- LIVE TRACE ---");
        if (trace.Count == 0) sb.AppendLine("[PENDING] No events collected");
        foreach (string entry in trace) sb.AppendLine(entry);
        return sb.ToString();
    }

    private void Log(string message)
    {
        string entry = Time.time.ToString("F2") + "s | " + message;
        trace.Add(entry);
        while (trace.Count > Mathf.Max(20, maxTraceEntries)) trace.RemoveAt(0);
        if (logToConsole) Debug.Log("[7F.2] " + message, this);
    }

    private void OnValidate()
    {
        firstAffectedWave = Mathf.Max(1, firstAffectedWave);
        firstEventDelay = Mathf.Max(0f, firstEventDelay);
        nextEventInterval = Mathf.Max(0.6f, nextEventInterval);
        maxEventsPerWave = Mathf.Max(1, maxEventsPerWave);
        maxConcurrentEvents = Mathf.Max(1, maxConcurrentEvents);
        warningSeconds = Mathf.Max(0.2f, warningSeconds);
        impactVisibleSeconds = Mathf.Max(0.1f, impactVisibleSeconds);
        impactDamage = Mathf.Max(0f, impactDamage);
        twinCircleRadius = Mathf.Max(0.25f, twinCircleRadius);
        twinCenterOffset = Mathf.Max(0.1f, twinCenterOffset);
        crossfireWidth = Mathf.Max(0.2f, crossfireWidth);
        crossfireHalfLength = Mathf.Max(0.5f, crossfireHalfLength);
        ringInnerRadius = Mathf.Max(0.15f, ringInnerRadius);
        ringOuterRadius = Mathf.Max(ringInnerRadius + 0.3f, ringOuterRadius);
        blockedCheckRadius = Mathf.Max(0f, blockedCheckRadius);
        maxTraceEntries = Mathf.Clamp(maxTraceEntries, 20, 500);
    }
}
