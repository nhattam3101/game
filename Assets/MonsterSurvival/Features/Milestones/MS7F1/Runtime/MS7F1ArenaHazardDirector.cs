using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 7F.1 opt-in arena hazards: additive world-space visuals and single-hit attacks.
/// Does NOT alter existing WaveManager/EnemySpawner, Gold, EXP, Boss, HUD or Save.
/// Attach to the existing WaveManager gameObject using the Editor installer.
/// </summary>
[DisallowMultipleComponent]
public sealed class MS7F1ArenaHazardDirector : MonoBehaviour
{
    [Header("Existing references")]
    public WaveManager waveManager;
    public EnemySpawner enemySpawner;

    [Header("Safety - OFF until preflight")]
    public bool enableArenaHazards = false;
    [Min(1)] public int firstAffectedWave = 2;
    public bool excludeBossWave = true;

    [Header("Cadence - suited to short waves (about 5s)")]
    [Min(0f)] public float firstHazardDelay = 1f;
    [Min(0.5f)] public float nextHazardInterval = 2.4f;
    [Min(1)] public int maxHazardsPerWave = 2;
    [Min(1)] public int maxActiveHazards = 1;

    [Header("Telegraph and impact")]
    [Min(0.2f)] public float warningSeconds = 1.0f;
    [Min(0.1f)] public float activeSeconds = 0.65f;
    [Range(0.3f, 4f)] public float radius = 1.2f;
    [Min(0f)] public float damage = 8f;
    public Color warningColor = new Color(0.65f, 0.37f, 0.94f, 0.95f);
    public Color impactColor = new Color(1f, 0.57f, 0.23f, 1f);

    [Header("Safety and diagnostics")]
    public LayerMask blockedLayers;
    [Min(0f)] public float blockedCheckRadius = 0.25f;
    [Min(10)] public int maxTraceEntries = 120;
    public bool logToConsole = false;

    public int SpawnedTotal { get; private set; }
    public int WarningsTotal { get; private set; }
    public int ActivationsTotal { get; private set; }
    public int PlayerWithinRadiusTotal { get; private set; }
    public int PlayerHpReducedTotal { get; private set; }
    public int CancelledAtWaveEnd { get; private set; }
    public int BossWaveSkips { get; private set; }
    public int CurrentHazardWave { get; private set; } = -1;
    public int ActiveCount { get { return hazards.Count; } }
    public bool SawWave2 { get; private set; }
    public bool SawWave3 { get; private set; }
    public bool SawWave4 { get; private set; }

    private readonly List<MS7F1ArenaHazard> hazards = new List<MS7F1ArenaHazard>();
    private readonly List<string> trace = new List<string>();
    private float nextSpawnCountdown;
    private int spawnedInWave;
    private bool inEligibleWave;
    private int lastBossSkip = -1;
    private Transform player;
    private Health playerHealth;

    public static bool IsEligible(bool enabled, int wave, int first, int total, int boss,
        bool skipBoss, bool spawnerIsActive, bool isGameOver, bool paused)
    {
        return enabled && !paused && !isGameOver && spawnerIsActive &&
               wave >= first && wave <= total && !(skipBoss && wave == boss);
    }

    public static bool WithinCircle(Vector2 center, Vector2 point, float r)
    {
        return (center - point).sqrMagnitude <= r * r;
    }

    private void Awake() { ResolveReferences(); }

    private void ResolveReferences()
    {
        if (waveManager == null) waveManager = GetComponent<WaveManager>();
        if (waveManager != null && enemySpawner == null)
            enemySpawner = waveManager.enemySpawner;
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) { player = p.transform; playerHealth = p.GetComponent<Health>(); }
        }
    }

    public bool TryValidate(out string reason)
    {
        ResolveReferences();
        if (waveManager == null || enemySpawner == null)
        { reason = "WaveManager or EnemySpawner missing"; return false; }
        if (waveManager.enemySpawner != enemySpawner)
        { reason = "EnemySpawner mismatches WaveManager.enemySpawner"; return false; }
        if (player == null || playerHealth == null)
        { reason = "Player tag / Health not found (enter Scene with Player)"; return false; }
        reason = "Wave/EnemySpawner/PlayerHealth references valid";
        return true;
    }

    public bool IsStillEligible
    {
        get
        {
            if (waveManager == null || enemySpawner == null) return false;
            bool gameOver = waveManager.gameManager != null && waveManager.gameManager.IsGameOver;
            return IsEligible(enableArenaHazards, waveManager.CurrentWave, firstAffectedWave,
                waveManager.totalWaves, waveManager.bossWave, excludeBossWave,
                enemySpawner.enabled, gameOver, false) &&
                inEligibleWave && CurrentHazardWave == waveManager.CurrentWave;
        }
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        // Pause: retain ring and countdown exactly; do not cancel.
        if (Time.timeScale <= 0f) return;
        ResolveReferences();
        if (waveManager == null || enemySpawner == null) return;

        int wave = waveManager.CurrentWave;
        bool gameOver = waveManager.gameManager != null && waveManager.gameManager.IsGameOver;
        bool eligible = IsEligible(enableArenaHazards, wave, firstAffectedWave,
            waveManager.totalWaves, waveManager.bossWave, excludeBossWave,
            enemySpawner.enabled, gameOver, false);

        if (!eligible)
        {
            if (inEligibleWave) EndWave("inactive/break/end");
            if (enableArenaHazards && excludeBossWave && wave == waveManager.bossWave && lastBossSkip != wave)
            { lastBossSkip = wave; BossWaveSkips++; Log("BOSS SKIP wave=" + wave); }
            return;
        }
        if (!inEligibleWave || CurrentHazardWave != wave)
        {
            if (inEligibleWave) EndWave("wave changed");
            inEligibleWave = true;
            CurrentHazardWave = wave;
            if (wave == 2) SawWave2 = true;
            if (wave == 3) SawWave3 = true;
            if (wave == 4) SawWave4 = true;
            spawnedInWave = 0;
            nextSpawnCountdown = Mathf.Max(0f, firstHazardDelay);
            Log("WAVE ACTIVE " + wave);
        }
        hazards.RemoveAll(h => h == null);
        if (spawnedInWave >= maxHazardsPerWave || hazards.Count >= maxActiveHazards) return;
        nextSpawnCountdown -= Time.deltaTime;
        if (nextSpawnCountdown <= 0f)
        {
            nextSpawnCountdown = Mathf.Max(0.5f, nextHazardInterval);
            SpawnHazard();
        }
    }

    private void SpawnHazard()
    {
        string reason;
        if (!TryValidate(out reason)) { Log("SKIP: " + reason); return; }
        Vector3 center = player.position;  // Snapshot target location; warning permits dodging.
        center.z = 0f;
        if (blockedLayers.value != 0 &&
            Physics2D.OverlapCircle(center, blockedCheckRadius, blockedLayers) != null)
        { Log("SKIP: center blocked by selected hazard obstacle layers"); return; }
        GameObject go = new GameObject("MS7F1_TelegraphedHazard_RUNTIME");
        go.transform.position = center;
        MS7F1ArenaHazard hazard = go.AddComponent<MS7F1ArenaHazard>();
        hazards.Add(hazard);
        hazard.Initialize(this, player, playerHealth, center, radius, warningSeconds,
            activeSeconds, damage, warningColor, impactColor);
        spawnedInWave++;
        SpawnedTotal++;
        WarningsTotal++;
        Log("WARNING wave=" + CurrentHazardWave + " radius=" + radius.ToString("F2") +
            " damage=" + damage.ToString("F1"));
    }

    public void RecordActivation(bool inRadius, bool hpReduced)
    {
        ActivationsTotal++;
        if (inRadius) PlayerWithinRadiusTotal++;
        if (hpReduced) PlayerHpReducedTotal++;
        Log("ACTIVATE wave=" + CurrentHazardWave + " playerInRadius=" + inRadius +
            " hpReduced=" + hpReduced + " (shield may block)");
    }

    public void RecordFinished(MS7F1ArenaHazard hazard)
    {
        hazards.Remove(hazard);
    }

    private void EndWave(string reason)
    {
        int count = hazards.Count;
        for (int i = hazards.Count - 1; i >= 0; i--)
        {
            MS7F1ArenaHazard h = hazards[i];
            if (h != null) h.Cancel();
        }
        hazards.Clear();
        CancelledAtWaveEnd += count;
        Log("WAVE STOP " + CurrentHazardWave + " | cancelled=" + count + " | " + reason);
        inEligibleWave = false;
        CurrentHazardWave = -1;
        spawnedInWave = 0;
    }

    private void OnDisable() { if (Application.isPlaying) EndWave("component disabled"); }
    private void OnDestroy() { if (Application.isPlaying) EndWave("component destroyed"); }

    private void Log(string message)
    {
        string entry = Time.time.ToString("F2") + "s | " + message;
        trace.Add(entry);
        while (trace.Count > Mathf.Max(10, maxTraceEntries)) trace.RemoveAt(0);
        if (logToConsole) Debug.Log("[7F.1] " + message, this);
    }

    public string GetRuntimeReport()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("7F.1 ARENA HAZARD RUNTIME (READ ONLY)");
        sb.AppendLine("Playing=" + Application.isPlaying + " | Enabled=" + enableArenaHazards +
            " | CurrentWave=" + (waveManager == null ? "?" : waveManager.CurrentWave.ToString()));
        sb.AppendLine("Spawned=" + SpawnedTotal + " Warnings=" + WarningsTotal +
            " Activated=" + ActivationsTotal + " InRadius=" + PlayerWithinRadiusTotal +
            " HPReduced=" + PlayerHpReducedTotal);
        sb.AppendLine("Observed Wave2=" + SawWave2 + " Wave3=" + SawWave3 + " Wave4=" + SawWave4);
        sb.AppendLine("WaveEndCancelled=" + CancelledAtWaveEnd +
            " BossSkips=" + BossWaveSkips + " ActiveHazards=" + ActiveCount);
        sb.AppendLine("HitAttempts are not guaranteed HP damage; PlayerShield may block. No persistency tested.");
        sb.AppendLine("--- TRACE ---");
        if (trace.Count == 0) sb.AppendLine("[PENDING] No runtime events");
        else for (int i = 0; i < trace.Count; i++) sb.AppendLine(trace[i]);
        return sb.ToString();
    }

    private void OnValidate()
    {
        firstAffectedWave = Mathf.Max(1, firstAffectedWave);
        firstHazardDelay = Mathf.Max(0f, firstHazardDelay);
        nextHazardInterval = Mathf.Max(0.5f, nextHazardInterval);
        maxHazardsPerWave = Mathf.Max(1, maxHazardsPerWave);
        maxActiveHazards = Mathf.Max(1, maxActiveHazards);
        warningSeconds = Mathf.Max(0.2f, warningSeconds);
        activeSeconds = Mathf.Max(0.1f, activeSeconds);
        damage = Mathf.Max(0f, damage);
        blockedCheckRadius = Mathf.Max(0f, blockedCheckRadius);
        maxTraceEntries = Mathf.Clamp(maxTraceEntries, 10, 500);
    }
}
