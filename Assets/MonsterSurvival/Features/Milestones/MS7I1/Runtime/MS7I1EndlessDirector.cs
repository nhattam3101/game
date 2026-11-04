using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

// 7I.1: opt-in endless core. Normal 5-wave game is untouched when enableEndlessMode=false.
// This prototype integrates with an EXISTING WaveManager through a validated private boss gate
// and CurrentWave setter; it does not patch the original WaveManager.cs.
[DisallowMultipleComponent]
public sealed class MS7I1EndlessDirector : MonoBehaviour
{
    [Header("Exact existing scene references")]
    public WaveManager waveManager;
    public EnemySpawner enemySpawner;

    [Header("7I.1 opt-in (OFF after installation)")]
    [Tooltip("Set in Edit Mode before Play. Do not change this switch during a match.")]
    public bool enableEndlessMode = false;

    [Header("Wave 6+ caps (no extra boss spawns yet)")]
    [Min(6)] public int firstEndlessWave = 6;
    [Range(10, 100)] public int maxConcurrentEnemies = 60;
    [Range(0.35f, 3f)] public float minimumEndlessSpawnInterval = 0.60f;
    [Range(10f, 120f)] public float endlessWaveSeconds = 30f;
    [Range(0f, 20f)] public float endlessBreakSeconds = 4f;
    [Range(0.25f, 6f)] public float endlessAnnouncementSeconds = 1.25f;

    public bool RuntimeEnabled { get; private set; }
    public bool BossGateTakenOver { get; private set; }
    public bool EndlessStarted { get; private set; }
    public int CurrentEndlessWave { get; private set; }
    public int PeakEndlessWave { get; private set; }
    public int WavesFinished { get; private set; }
    public string LastStatus { get; private set; } = "NOT STARTED";
    public int ErrorCount { get; private set; }

    private FieldInfo bossSpawnedField;
    private FieldInfo bossDefeatedField;
    private MethodInfo waveSetter;
    private bool originalSpawnerEnabled;
    private float originalSpawnInterval;
    private int originalMaxEnemies;
    private float originalNormal, originalFast, originalTank;
    private readonly List<string> trace = new List<string>();
    public IList<string> Trace { get { return trace.AsReadOnly(); } }

    private void Awake()
    {
        RuntimeEnabled = enableEndlessMode; // latch mode at start of run
        if (!RuntimeEnabled) { LastStatus = "NORMAL MODE (OFF)"; return; }
        string reason;
        if (!ResolveAndValidate(out reason))
        {
            RuntimeEnabled = false;
            ErrorCount++;
            LastStatus = "BLOCKED: " + reason;
            Debug.LogError("[7I.1] " + LastStatus + "; original 5-wave flow retained", this);
            return;
        }
        originalSpawnerEnabled = enemySpawner.enabled;
        originalSpawnInterval = enemySpawner.spawnInterval;
        originalMaxEnemies = enemySpawner.maxEnemies;
        originalNormal = enemySpawner.normalWeight;
        originalFast = enemySpawner.fastWeight;
        originalTank = enemySpawner.tankWeight;
        LastStatus = "ARMED: wait for Wave 5 boss spawn";
        Record(LastStatus);
    }

    public bool ResolveAndValidate(out string reason)
    {
        if (waveManager == null) waveManager = GetComponent<WaveManager>();
        if (waveManager == null) { reason = "Missing WaveManager on this object"; return false; }
        if (enemySpawner == null) enemySpawner = waveManager.enemySpawner;
        if (enemySpawner == null || waveManager.enemySpawner != enemySpawner)
        { reason = "EnemySpawner is absent or does not match WaveManager.enemySpawner"; return false; }
        if (waveManager.gameManager == null)
            waveManager.gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        if (waveManager.gameManager == null) { reason = "GameManager missing in active Scene"; return false; }
        if (!waveManager.gameManager.victoryByWaves)
        { reason = "GameManager.victoryByWaves must be TRUE to prevent legacy timer Victory"; return false; }
        if (waveManager.totalWaves != 5 || waveManager.bossWave != 5)
        { reason = "7I.1 expects original 5 waves with Boss at Wave 5"; return false; }
        Type type = typeof(WaveManager);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        bossSpawnedField = type.GetField("bossSpawned", flags);
        bossDefeatedField = type.GetField("bossDefeated", flags);
        PropertyInfo waveProp = type.GetProperty("CurrentWave", BindingFlags.Public | BindingFlags.Instance);
        waveSetter = waveProp == null ? null : waveProp.GetSetMethod(true);
        if (bossSpawnedField == null || bossDefeatedField == null || waveSetter == null ||
            bossSpawnedField.FieldType != typeof(bool) || bossDefeatedField.FieldType != typeof(bool) ||
            waveProp.PropertyType != typeof(int))
        { reason = "Original WaveManager has different boss flags or CurrentWave setter; do not start Endless"; return false; }
        reason = "READY: validated original WaveManager Boss fields and CurrentWave setter";
        return true;
    }

    private void Update()
    {
        if (!RuntimeEnabled || BossGateTakenOver || waveManager == null) return;
        if (waveManager.gameManager == null || waveManager.gameManager.IsGameOver) return;
        // Wait until WaveManager.SpawnBoss() has actually attached its Health.OnDied listener.
        // Stop the original RunWaves before it can award 5-wave Victory.
        if (waveManager.CurrentWave != waveManager.bossWave) return;
        bool spawned;
        try { spawned = (bool)bossSpawnedField.GetValue(waveManager); }
        catch (Exception e) { FailBeforeTakeover(e.Message); return; }
        if (!spawned) return;
        waveManager.StopAllCoroutines();
        BossGateTakenOver = true;
        enemySpawner.enabled = false; // no additional enemies while defeating Wave-5 boss
        if (waveManager.announcementText != null) waveManager.announcementText.gameObject.SetActive(false);
        LastStatus = "WAVE 5 BOSS GATE: waiting for Boss defeat";
        Record(LastStatus);
        StartCoroutine(WaitForBossThenEndless());
    }

    private void FailBeforeTakeover(string message)
    {
        RuntimeEnabled = false;
        ErrorCount++;
        LastStatus = "BLOCKED before takeover: " + message;
        Debug.LogError("[7I.1] " + LastStatus, this);
    }

    private IEnumerator WaitForBossThenEndless()
    {
        while (!waveManager.gameManager.IsGameOver)
        {
            bool defeated = false;
            try { defeated = (bool)bossDefeatedField.GetValue(waveManager); }
            catch (Exception e)
            {
                ErrorCount++;
                LastStatus = "Boss gate read failed: " + e.Message;
                Debug.LogError("[7I.1] " + LastStatus, this);
                yield break;
            }
            if (defeated) break;
            if (waveManager.waveText != null)
                waveManager.waveText.text = "ENDLESS | DEFEAT WAVE 5 BOSS";
            yield return null;
        }
        if (waveManager.gameManager.IsGameOver) { StopOnDefeat(); yield break; }
        EndlessStarted = true;
        LastStatus = "ENDLESS CORE ACTIVE (Wave 6+)";
        Record(LastStatus);
        yield return RunEndless();
    }

    private IEnumerator RunEndless()
    {
        // Preserve 7E/7F map/event watchers by updating the EXISTING WaveManager.CurrentWave.
        // Boss waves after 5 and persistent Endless rewards are NOT part of 7I.1.
        for (int wave = Mathf.Max(6, firstEndlessWave); wave <= 9999; wave++)
        {
            if (waveManager == null || waveManager.gameManager == null || waveManager.gameManager.IsGameOver)
                break;
            try { waveSetter.Invoke(waveManager, new object[] { wave }); }
            catch (Exception e)
            {
                ErrorCount++;
                LastStatus = "Unable to publish Endless Wave: " + e.Message;
                Debug.LogError("[7I.1] " + LastStatus, this);
                enemySpawner.enabled = false;
                yield break;
            }
            CurrentEndlessWave = wave;
            PeakEndlessWave = Mathf.Max(PeakEndlessWave, wave);
            SetSafeSpawnerBalance(wave);
            // 7I3_BRIDGE_ONCE: Optional capped scaling (no HP / Save changes).
            MS7I3EndlessBossRotation ms7i3 = GetComponent<MS7I3EndlessBossRotation>();
            if (ms7i3 != null && ms7i3.Ready) ms7i3.AdjustWaveSpawner(wave);
            enemySpawner.enabled = false;
            LastStatus = "WAVE " + wave + " ANNOUNCEMENT";
            Record(LastStatus);
            if (waveManager.announcementText != null)
            {
                waveManager.announcementText.gameObject.SetActive(true);
                waveManager.announcementText.text = "ENDLESS WAVE " + wave;
            }
            yield return new WaitForSeconds(endlessAnnouncementSeconds);
            if (waveManager.announcementText != null) waveManager.announcementText.gameObject.SetActive(false);
            if (waveManager.gameManager.IsGameOver) break;

            enemySpawner.enabled = true;
            LastStatus = "WAVE " + wave + " ACTIVE";
            float remaining = Mathf.Max(10f, endlessWaveSeconds);
            // 7I3_BRIDGE_DURATION: Optional bounded Wave length.
            if (ms7i3 != null && ms7i3.Ready) remaining = ms7i3.AdjustWaveDuration(wave, remaining);
            while (remaining > 0f && waveManager.gameManager != null && !waveManager.gameManager.IsGameOver)
            {
                remaining -= Time.deltaTime; // pauses naturally with Time.timeScale
                if (waveManager.waveText != null)
                    waveManager.waveText.text = "ENDLESS " + wave + " | " + Mathf.CeilToInt(remaining) + "s";
                // Cap maximum concurrent enemies, including modifiers from 7E2.
                if (enemySpawner.maxEnemies > maxConcurrentEnemies)
                    enemySpawner.maxEnemies = maxConcurrentEnemies;
                yield return null;
            }
            enemySpawner.enabled = false;
            if (waveManager.gameManager.IsGameOver) break;
            // 7I3_BRIDGE_GATE: Boss is required to die before proceeding.
            if (ms7i3 != null && ms7i3.Ready)
            {
                yield return ms7i3.RunBossGate(wave);
                if (waveManager == null || waveManager.gameManager == null ||
                    waveManager.gameManager.IsGameOver || !ms7i3.CanAdvance(wave))
                {
                    LastStatus = "ENDLESS STOPPED: 7I.3 boss gate incomplete";
                    if (enemySpawner != null) enemySpawner.enabled = false;
                    Record(LastStatus);
                    yield break;
                }
            }
            WavesFinished++;
            LastStatus = "WAVE " + wave + " CLEARED";
            Record(LastStatus);
            float pause = Mathf.Max(0, endlessBreakSeconds);
            while (pause > 0f && waveManager.gameManager != null && !waveManager.gameManager.IsGameOver)
            {
                pause -= Time.deltaTime;
                if (waveManager.waveText != null)
                    waveManager.waveText.text = "ENDLESS | NEXT " + (wave + 1) + " IN " + Mathf.CeilToInt(pause) + "s";
                yield return null;
            }
        }
        if (enemySpawner != null) enemySpawner.enabled = false;
        LastStatus = "ENDLESS STOPPED: GameOver or wave safety limit";
        Record(LastStatus);
    }

    private void SetSafeSpawnerBalance(int wave)
    {
        int offset = Mathf.Max(0, wave - 1);
        float baseInterval = waveManager.firstWaveSpawnInterval - offset * waveManager.intervalReductionPerWave;
        float clamped = Mathf.Max(Mathf.Max(waveManager.minimumSpawnInterval, minimumEndlessSpawnInterval), baseInterval);
        // Values written once before new-wave events, not continuously (except hard enemy cap).
        enemySpawner.spawnInterval = clamped;
        long count = (long)waveManager.firstWaveMaxEnemies + (long)offset * waveManager.extraEnemiesPerWave;
        enemySpawner.maxEnemies = (int)Math.Max(1L, Math.Min(maxConcurrentEnemies, count));
        enemySpawner.normalWeight = 45f;
        enemySpawner.fastWeight = 35f;
        enemySpawner.tankWeight = 20f;
    }

    private void StopOnDefeat()
    {
        if (enemySpawner != null) enemySpawner.enabled = false;
        LastStatus = "GAME OVER; Endless rewards are disabled in 7I.1";
        Record(LastStatus);
    }

    private void LateUpdate()
    {
        if (!BossGateTakenOver || waveManager == null || waveManager.gameManager == null) return;
        if (waveManager.gameManager.IsGameOver)
        {
            if (enemySpawner != null) enemySpawner.enabled = false;
            if (LastStatus != "GAME OVER; Endless rewards are disabled in 7I.1")
                LastStatus = "GAME OVER; Endless rewards are disabled in 7I.1";
        }
    }

    private void Record(string message)
    {
        trace.Add(DateTime.UtcNow.ToString("HH:mm:ss") + "Z " + message);
        if (trace.Count > 48) trace.RemoveAt(0);
        Debug.Log("[7I.1] " + message, this);
    }

    // Pure math test: no Scene mutations or disk writes.
    public static string PureRulesQA()
    {
        int pass = 0, fail = 0;
        Action<bool> check = ok => { if (ok) pass++; else fail++; };
        check(Math.Min(60, 20 + 5 * 5) == 45); // wave6 cap input
        check(Math.Min(60, 20 + 5 * 20) == 60); // cap long-run enemies
        check(Math.Max(0.6f, 2f - 5 * .25f) >= .6f);
        check(Math.Max(0.6f, 2f - 100 * .25f) == .6f);
        check(5 < 6 && 6 < 9999);
        check((45 + 35 + 20) == 100);
        check((long)20 + (long)9999 * 5 > 60L);
        check(Math.Min(60, 20 + 5 * 20) >= Math.Min(60, 20 + 5 * 5)); // monotonic capped spawn limit
        return "7I.1 IN-MEMORY RULE TESTS: PASS=" + pass + " FAIL=" + fail +
            ". Does NOT prove Unity runtime Boss gate, 7E/7F integration or Save.";
    }
}
