using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 7I.3 - Only runs in 7I.1 Endless Wave 6+, after 7I.2 mode handoff.
// Installed OFF. Requires the guarded 7I.3 integration hooks in MS7I1EndlessDirector.
// Does NOT modify Health, save, achievements, rewards or WaveManager's boss flags.
[DisallowMultipleComponent]
public sealed class MS7I3EndlessBossRotation : MonoBehaviour
{
    [Header("Existing scene objects")]
    public MS7I1EndlessDirector endless;
    public WaveManager waveManager;
    public EnemySpawner spawner;

    [Header("Real Boss prefabs - drag from Project, not scene")]
    public GameObject shadowTitanPrefab;
    public GameObject graveWardenPrefab;

    [Header("Optional - OFF on install")]
    public bool enableBossRotation = false;
    [Min(6)] public int firstRotatingBossWave = 10;
    [Range(2, 20)] public int bossEveryNWaves = 5;
    [Range(0, 20)] public int maximumAddedWaveSeconds = 10;
    [Range(10, 100)] public int reachMaxDifficultyByWave = 30;
    [Range(1f, 5f)] public float retrySafeSpawnSeconds = 2f;

    public int BossesSpawned { get; private set; }
    public int BossesDefeated { get; private set; }
    public int MaxObservedWave { get; private set; }
    public int SafeSpawnRetries { get; private set; }
    public int Errors { get; private set; }
    public int ActiveBossWave { get; private set; }
    public bool ActiveBossDefeated { get; private set; }
    public string Status { get; private set; } = "7I.3 disabled";
    public IList<string> Trace { get { return log.AsReadOnly(); } }

    private readonly List<string> log = new List<string>();
    private Health liveBossHealth;
    private GameObject liveBoss;
    private bool gateValid;
    private float lastDuration;

    private void Awake()
    {
        if (!endless) endless = GetComponent<MS7I1EndlessDirector>();
        if (!waveManager) waveManager = GetComponent<WaveManager>();
        if (!spawner && waveManager) spawner = waveManager.enemySpawner;
        if (!enableBossRotation) return;
        string reason;
        if (!DependenciesValid(out reason))
        {
            enableBossRotation = false; // fail closed, don't damage normal gameplay
            Errors++;
            Status = "DISABLED: " + reason;
            Debug.LogError("[7I.3] " + Status, this);
        }
        else Status = "READY: Endless only; waiting for Wave " + firstRotatingBossWave;
    }

    public bool DependenciesValid(out string reason)
    {
        if (!endless || !waveManager || !spawner || endless.waveManager != waveManager ||
            endless.enemySpawner != spawner || waveManager.enemySpawner != spawner)
        { reason = "7I.1, WaveManager and EnemySpawner references must be exact"; return false; }
        if (firstRotatingBossWave < 6 || bossEveryNWaves < 2)
        { reason = "Invalid boss schedule"; return false; }
        if (!shadowTitanPrefab || !graveWardenPrefab || shadowTitanPrefab == graveWardenPrefab ||
            !shadowTitanPrefab.GetComponent<Health>() || !graveWardenPrefab.GetComponent<Health>() ||
            graveWardenPrefab.GetComponentInChildren<GraveWardenBossAI>(true) == null)
        { reason = "Two distinct REAL Boss prefabs required (Health, GraveWardenBossAI)"; return false; }
        if (!waveManager.safeSpawn && !waveManager.GetComponent<MapSafeSpawn2D>())
        { reason = "SafeSpawn4B.3 reference is mandatory for rotating boss safety"; return false; }
        reason = "VALID";
        return true;
    }

    public bool IsRotatingBossWave(int wave)
    {
        return wave >= firstRotatingBossWave && bossEveryNWaves >= 2 &&
            (wave - firstRotatingBossWave) % bossEveryNWaves == 0;
    }

    public string PlannedBossName(int wave)
    {
        if (!IsRotatingBossWave(wave)) return "NONE";
        int index = (wave - firstRotatingBossWave) / bossEveryNWaves;
        return index % 2 == 0 ? "SHADOW TITAN" : "GRAVE WARDEN";
    }

    private GameObject PlannedBossPrefab(int wave)
    {
        return PlannedBossName(wave) == "SHADOW TITAN" ? shadowTitanPrefab : graveWardenPrefab;
    }

    // Called by 7I.1 once AFTER its own safe baseline calculation, only on Endless waves.
    // Bounded composition adjustments; no HP or damage multiplier and no spawner cap increase.
    public void AdjustWaveSpawner(int wave)
    {
        if (!Ready || !spawner) return;
        MaxObservedWave = Mathf.Max(MaxObservedWave, wave);
        float t = Difficulty01(wave);
        spawner.normalWeight = Mathf.Lerp(45f, 33f, t);
        spawner.fastWeight = Mathf.Lerp(35f, 40f, t);
        spawner.tankWeight = Mathf.Lerp(20f, 27f, t);
        spawner.maxEnemies = Mathf.Min(endless.maxConcurrentEnemies, Mathf.Max(1, spawner.maxEnemies));
        spawner.spawnInterval = Mathf.Max(endless.minimumEndlessSpawnInterval, spawner.spawnInterval);
    }

    // Called by 7I.1 at the start of each Wave's combat timer. Increasing length is
    // intentional and bounded; Pause still works because 7I.1 uses Time.deltaTime.
    public float AdjustWaveDuration(int wave, float originalSeconds)
    {
        lastDuration = originalSeconds;
        if (!Ready) return originalSeconds;
        lastDuration = Mathf.Clamp(originalSeconds + Mathf.RoundToInt(Difficulty01(wave) * maximumAddedWaveSeconds),
            originalSeconds, originalSeconds + maximumAddedWaveSeconds);
        return lastDuration;
    }

    public bool Ready
    {
        get { return enabled && enableBossRotation && endless && endless.RuntimeEnabled && endless.EndlessStarted; }
    }

    private float Difficulty01(int wave)
    {
        return Mathf.Clamp01((wave - 6f) / Mathf.Max(1f, reachMaxDifficultyByWave - 6f));
    }

    // Called after 7I.1's wave combat timer ends and its EnemySpawner is disabled.
    // Gate cannot be cleared until the REAL spawned Boss's Health.OnDied fires.
    public IEnumerator RunBossGate(int wave)
    {
        gateValid = false;
        if (!Ready || !IsRotatingBossWave(wave)) { gateValid = true; yield break; }
        if (waveManager == null || waveManager.gameManager == null || waveManager.gameManager.IsGameOver)
            yield break;

        string reason;
        if (!DependenciesValid(out reason))
        {
            Errors++; Status = "BLOCKED: " + reason; Record(Status); yield break;
        }
        ActiveBossWave = wave;
        ActiveBossDefeated = false;
        gateValid = false;
        Status = "WAVE " + wave + " ROTATING BOSS: " + PlannedBossName(wave);
        Record(Status);
        if (spawner) spawner.enabled = false;

        while (!liveBossHealth && waveManager && waveManager.gameManager && !waveManager.gameManager.IsGameOver)
        {
            if (TrySpawnRealBoss(wave)) break;
            SafeSpawnRetries++;
            Status = "WAITING FOR SAFE BOSS SPAWN / Wave " + wave;
            if (waveManager.waveText) waveManager.waveText.text = Status;
            yield return new WaitForSeconds(Mathf.Max(1f, retrySafeSpawnSeconds));
        }
        while (!ActiveBossDefeated && waveManager && waveManager.gameManager && !waveManager.gameManager.IsGameOver)
        {
            if (spawner) spawner.enabled = false;
            if (waveManager.waveText) waveManager.waveText.text = "ENDLESS " + wave + " | DEFEAT " + PlannedBossName(wave);
            yield return null;
        }
        if (waveManager && waveManager.gameManager && !waveManager.gameManager.IsGameOver && ActiveBossDefeated)
        {
            gateValid = true;
            Status = "ENDLESS BOSS WAVE " + wave + " CLEARED";
            Record(Status);
        }
        else
        {
            Status = "BOSS GATE CANCELLED (Game Over / dependency lost)";
            Record(Status);
        }
        CleanupBossReference();
    }

    public bool CanAdvance(int wave)
    {
        return !IsRotatingBossWave(wave) || (gateValid && ActiveBossWave == wave && ActiveBossDefeated);
    }

    private bool TrySpawnRealBoss(int wave)
    {
        GameObject prefab = PlannedBossPrefab(wave);
        if (!prefab || !waveManager) return false;
        Camera cam = Camera.main;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        MapSafeSpawn2D safe = waveManager.safeSpawn ? waveManager.safeSpawn : waveManager.GetComponent<MapSafeSpawn2D>();
        if (!cam || !player || !safe) return false;
        float depth = Mathf.Abs(cam.transform.position.z);
        float best = -1f;
        Vector3 chosen = Vector3.zero;
        bool found = false;
        Vector2[] points = { new Vector2(.15f,.15f), new Vector2(.85f,.15f),
            new Vector2(.15f,.85f), new Vector2(.85f,.85f), new Vector2(.5f,.85f), new Vector2(.5f,.15f) };
        foreach (Vector2 uv in points)
        {
            Vector3 candidate = cam.ViewportToWorldPoint(new Vector3(uv.x, uv.y, depth));
            candidate.z = 0f;
            if (!safe.IsSafe(candidate, player.transform, waveManager.bossMinDistanceFromPlayer,
                waveManager.bossSpawnClearance, safe.obstacleLayers)) continue;
            float score = Vector2.Distance(candidate, player.transform.position);
            if (score > best) { chosen = candidate; best = score; found = true; }
        }
        if (!found && !safe.TryFindViewportPoint(cam, player.transform, out chosen, .12f,
            waveManager.bossMinDistanceFromPlayer, waveManager.bossSpawnClearance,
            safe.obstacleLayers, waveManager.bossSpawnAttempts)) return false;
        chosen.z = 0f;
        GameObject spawned = Instantiate(prefab, chosen, Quaternion.identity);
        Health health = spawned.GetComponent<Health>();
        if (!health) { Destroy(spawned); Errors++; Record("[FAIL] Spawned boss has no Health"); return false; }
        liveBoss = spawned;
        liveBossHealth = health;
        liveBossHealth.OnDied += OnRotatingBossDied;
        BossesSpawned++;
        if (waveManager.bossHealthBar)
        {
            if (spawned.GetComponentInChildren<GraveWardenBossAI>(true))
                waveManager.bossHealthBar.UnbindBoss(); // use Grave's own purple/gold follow HP
            else waveManager.bossHealthBar.BindBoss(liveBossHealth, "SHADOW TITAN");
        }
        Record("SPAWNED " + PlannedBossName(wave) + " @ Wave " + wave + "; prefab HP not modified");
        return true;
    }

    private void OnRotatingBossDied()
    {
        if (ActiveBossDefeated) return;
        ActiveBossDefeated = true;
        BossesDefeated++;
        Record("BOSS DEFEATED @ Wave " + ActiveBossWave);
        CleanupBossReference();
    }

    private void CleanupBossReference()
    {
        if (liveBossHealth) liveBossHealth.OnDied -= OnRotatingBossDied;
        liveBossHealth = null;
        liveBoss = null;
    }

    private void OnDestroy() { CleanupBossReference(); }

    private void Record(string msg)
    {
        log.Add(DateTime.UtcNow.ToString("HH:mm:ss") + "Z " + msg);
        if (log.Count > 64) log.RemoveAt(0);
        Debug.Log("[7I.3] " + msg, this);
    }

    public static string PureTests()
    {
        int pass = 0, fail = 0;
        Action<bool> test = x => { if (x) pass++; else fail++; };
        test(PlannedNameFor(10, 10, 5) == "SHADOW TITAN");
        test(PlannedNameFor(15, 10, 5) == "GRAVE WARDEN");
        test(PlannedNameFor(20, 10, 5) == "SHADOW TITAN");
        test(PlannedNameFor(9, 10, 5) == "NONE");
        test(PlannedNameFor(11, 10, 5) == "NONE");
        test(Mathf.Clamp01((30f - 6f) / 24f) == 1f);
        test(Mathf.Clamp01((6f - 6f) / 24f) == 0f);
        test(Mathf.Lerp(45f,33f,1f) + Mathf.Lerp(35f,40f,1f) + Mathf.Lerp(20f,27f,1f) == 100f);
        test(Mathf.Min(60, 120) == 60);
        test(Mathf.Max(.6f, .4f) == .6f);
        return "7I.3 PURE RULES PASS=" + pass + " FAIL=" + fail + "; no scene/save/HP writes.";
    }
    private static string PlannedNameFor(int wave, int first, int stride)
    {
        if (wave < first || stride < 2 || (wave-first)%stride != 0) return "NONE";
        return ((wave-first)/stride) % 2 == 0 ? "SHADOW TITAN" : "GRAVE WARDEN";
    }
}
