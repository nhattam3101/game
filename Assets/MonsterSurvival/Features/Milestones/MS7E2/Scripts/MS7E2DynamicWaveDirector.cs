using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// 7E.2: opt-in wave pacing add-on. Does not spawn enemies or edit WaveManager,
/// EnemySpawner, EliteSpawnDirector, Save, Boss, equipment or rewards source.
/// Reuses the EXISTING EnemySpawner and its safe-spawn/maxEnemy logic.
/// </summary>
[DisallowMultipleComponent]
public sealed class MS7E2DynamicWaveDirector : MonoBehaviour
{
    public enum Encounter { None, Rush, Siege, Swarm }

    [Header("Existing references (WaveManager owner)")]
    public WaveManager waveManager;
    public EnemySpawner enemySpawner;

    [Header("Safety: OFF until audited")]
    public bool enableDynamicEncounters = false;
    [Min(1)] public int firstAffectedWave = 2;
    public bool excludeBossWave = true;

    [Header("Optional one short intensity pulse per active wave")]
    public bool enableSurge = true;
    [Min(1f)] public float surgeDelaySeconds = 14f;
    [Min(1f)] public float surgeDurationSeconds = 7f;
    [Range(0.5f, 1f)] public float surgeSpawnIntervalFactor = 0.82f;

    [Header("Limits and diagnostics")]
    [Min(0.1f)] public float minimumSpawnInterval = 0.65f;
    [Min(0)] public int maximumTraceEvents = 120;
    public bool logEventsToConsole = false;

    // Read-only runtime counters; not serialized and never used for Save/Profile.
    public int EncounterStarts { get; private set; }
    public int SurgeStarts { get; private set; }
    public int SuccessfulRestores { get; private set; }
    public int BossWaveSkips { get; private set; }
    public Encounter CurrentEncounter { get; private set; }
    public int CurrentEncounterWave { get; private set; } = -1;
    public bool CurrentlyApplying { get { return ownsSettings; } }
    public bool CurrentlySurging { get { return ownsSettings && surgeActive; } }

    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
    private FieldInfo intervalField;
    private FieldInfo normalField;
    private FieldInfo fastField;
    private FieldInfo tankField;
    private bool ownsSettings;
    private bool surgeActive;
    private bool surgeCompleted;
    private float activeWaveSeconds;
    private int lastBossSkipWave = -1;

    private struct SpawnValues
    {
        public float interval, normal, fast, tank;
    }

    private SpawnValues baseline;
    private SpawnValues lastWritten;
    private readonly List<string> trace = new List<string>();

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        // No field mutation on enable. Only Update may activate after user opt-in.
        CurrentEncounter = Encounter.None;
        CurrentEncounterWave = -1;
        activeWaveSeconds = 0f;
    }

    private void ResolveReferences()
    {
        if (waveManager == null) waveManager = GetComponent<WaveManager>();
        if (waveManager != null && enemySpawner == null)
            enemySpawner = waveManager.enemySpawner;
    }

    /// <summary>Strict compatibility validation; no writes to existing spawner.</summary>
    public bool TryValidateSpawnerFields(out string reason)
    {
        ResolveReferences();
        if (waveManager == null || enemySpawner == null)
        {
            reason = "Assign existing WaveManager and WaveManager.enemySpawner.";
            return false;
        }
        if (waveManager.enemySpawner != enemySpawner)
        {
            reason = "Director's EnemySpawner differs from WaveManager.enemySpawner. No changes allowed.";
            return false;
        }
        Type type = enemySpawner.GetType();
        intervalField = ValidFloatField(type, "spawnInterval");
        normalField = ValidFloatField(type, "normalWeight");
        fastField = ValidFloatField(type, "fastWeight");
        tankField = ValidFloatField(type, "tankWeight");
        if (intervalField == null || normalField == null || fastField == null || tankField == null)
        {
            reason = "Existing EnemySpawner needs PUBLIC FLOAT fields: spawnInterval, normalWeight, fastWeight, tankWeight. Nothing was modified.";
            return false;
        }
        reason = "Verified existing EnemySpawner public float spawnInterval / normalWeight / fastWeight / tankWeight.";
        return true;
    }

    private static FieldInfo ValidFloatField(Type type, string name)
    {
        FieldInfo f = type.GetField(name, PublicInstance);
        return f != null && f.FieldType == typeof(float) && !f.IsInitOnly ? f : null;
    }

    private SpawnValues ReadValues()
    {
        return new SpawnValues
        {
            interval = (float)intervalField.GetValue(enemySpawner),
            normal = (float)normalField.GetValue(enemySpawner),
            fast = (float)fastField.GetValue(enemySpawner),
            tank = (float)tankField.GetValue(enemySpawner)
        };
    }

    private void WriteValues(SpawnValues values)
    {
        intervalField.SetValue(enemySpawner, values.interval);
        normalField.SetValue(enemySpawner, values.normal);
        fastField.SetValue(enemySpawner, values.fast);
        tankField.SetValue(enemySpawner, values.tank);
        lastWritten = values;
    }

    private static bool Nearly(float a, float b)
    {
        return Mathf.Abs(a - b) <= 0.0002f;
    }

    private void RestoreOwnedSettings(string reason)
    {
        if (!ownsSettings) return;
        // Restore only fields still holding OUR values: don't overwrite another add-on's update.
        if (enemySpawner != null && intervalField != null)
        {
            SpawnValues now = ReadValues();
            int restored = 0;
            if (Nearly(now.interval, lastWritten.interval)) { intervalField.SetValue(enemySpawner, baseline.interval); restored++; }
            if (Nearly(now.normal, lastWritten.normal)) { normalField.SetValue(enemySpawner, baseline.normal); restored++; }
            if (Nearly(now.fast, lastWritten.fast)) { fastField.SetValue(enemySpawner, baseline.fast); restored++; }
            if (Nearly(now.tank, lastWritten.tank)) { tankField.SetValue(enemySpawner, baseline.tank); restored++; }
            if (restored == 4) SuccessfulRestores++;
            Trace("RESTORE wave=" + CurrentEncounterWave + " fields=" + restored + "/4 (" + reason + ")");
        }
        ownsSettings = false;
        surgeActive = false;
        surgeCompleted = false;
        CurrentEncounter = Encounter.None;
        CurrentEncounterWave = -1;
        activeWaveSeconds = 0f;
    }

    public static Encounter GetEncounterForWave(int wave, int firstWave)
    {
        if (wave < firstWave) return Encounter.None;
        int cycle = (wave - firstWave) % 3;
        return cycle == 0 ? Encounter.Rush : (cycle == 1 ? Encounter.Siege : Encounter.Swarm);
    }

    private SpawnValues BuildProfile(Encounter mode, bool surge)
    {
        SpawnValues v = baseline;
        switch (mode)
        {
            case Encounter.Rush:
                v.interval *= 0.85f;
                v.normal *= 0.78f;
                v.fast *= 2.15f;
                v.tank *= 0.85f;
                break;
            case Encounter.Siege:
                v.interval *= 1.10f;
                v.normal *= 0.82f;
                v.fast *= 0.70f;
                v.tank *= 2.60f;
                break;
            case Encounter.Swarm:
                v.interval *= 0.73f;
                v.normal *= 1.28f;
                v.fast *= 1.10f;
                v.tank *= 0.85f;
                break;
        }
        if (surge) v.interval *= surgeSpawnIntervalFactor;
        v.interval = Mathf.Max(minimumSpawnInterval, v.interval);
        v.normal = Mathf.Max(0f, v.normal);
        v.fast = Mathf.Max(0f, v.fast);
        v.tank = Mathf.Max(0f, v.tank);
        return v;
    }

    private void StartEncounter(int wave)
    {
        string reason;
        if (!TryValidateSpawnerFields(out reason))
        {
            Trace("SKIP: " + reason);
            return;
        }
        baseline = ReadValues();
        CurrentEncounterWave = wave;
        CurrentEncounter = GetEncounterForWave(wave, firstAffectedWave);
        activeWaveSeconds = 0f;
        surgeActive = false;
        surgeCompleted = false;
        WriteValues(BuildProfile(CurrentEncounter, false));
        ownsSettings = true;
        EncounterStarts++;
        Trace("START wave=" + wave + " mode=" + CurrentEncounter +
              " originalInterval=" + baseline.interval.ToString("F2") +
              " currentInterval=" + lastWritten.interval.ToString("F2"));
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        // Paused game: do not change timing or spawner values.
        if (Time.timeScale <= 0f) return;
        ResolveReferences();
        if (waveManager == null || enemySpawner == null) return;

        int wave = waveManager.CurrentWave;
        bool boss = wave == waveManager.bossWave;
        bool inRange = wave >= firstAffectedWave && wave <= waveManager.totalWaves;
        bool ended = waveManager.gameManager != null && waveManager.gameManager.IsGameOver;
        bool eligible = enableDynamicEncounters && inRange && enemySpawner.enabled &&
                        !ended && (!excludeBossWave || !boss);

        if (!eligible)
        {
            if (ownsSettings) RestoreOwnedSettings("not an eligible active wave");
            if (enableDynamicEncounters && boss && excludeBossWave && lastBossSkipWave != wave)
            {
                lastBossSkipWave = wave;
                BossWaveSkips++;
                Trace("BOSS SKIP wave=" + wave + " (unchanged)");
            }
            return;
        }

        if (!ownsSettings || CurrentEncounterWave != wave)
        {
            if (ownsSettings) RestoreOwnedSettings("wave changed");
            StartEncounter(wave);
        }
        if (!ownsSettings) return;
        activeWaveSeconds += Time.deltaTime;
        if (enableSurge && !surgeCompleted && !surgeActive && activeWaveSeconds >= surgeDelaySeconds)
        {
            surgeActive = true;
            WriteValues(BuildProfile(CurrentEncounter, true));
            SurgeStarts++;
            Trace("SURGE START wave=" + wave + " interval=" + lastWritten.interval.ToString("F2"));
        }
        if (surgeActive && activeWaveSeconds >= surgeDelaySeconds + surgeDurationSeconds)
        {
            surgeActive = false;
            surgeCompleted = true;
            WriteValues(BuildProfile(CurrentEncounter, false));
            Trace("SURGE END wave=" + wave);
        }
    }

    private void OnDisable()
    {
        RestoreOwnedSettings("component disabled");
    }

    private void OnDestroy()
    {
        RestoreOwnedSettings("component destroyed");
    }

    private void Trace(string message)
    {
        string entry = Time.time.ToString("F2") + "s | " + message;
        if (maximumTraceEvents > 0)
        {
            trace.Add(entry);
            while (trace.Count > maximumTraceEvents) trace.RemoveAt(0);
        }
        if (logEventsToConsole) Debug.Log("[7E.2] " + message, this);
    }

    public string GetRuntimeSummary()
    {
        string reason;
        bool refsOkay = TryValidateSpawnerFields(out reason);
        return "Wave=" + (waveManager != null ? waveManager.CurrentWave.ToString() : "?") +
               " | OptIn=" + enableDynamicEncounters +
               " | SpawnerFields=" + refsOkay + " (" + reason + ")" +
               " | Mode=" + CurrentEncounter + " | Applying=" + ownsSettings +
               " | Surge=" + surgeActive + " | Starts=" + EncounterStarts +
               " | SurgeStarts=" + SurgeStarts + " | FullRestores=" + SuccessfulRestores +
               " | BossSkips=" + BossWaveSkips;
    }

    public string GetTraceText()
    {
        StringBuilder builder = new StringBuilder();
        if (trace.Count == 0) builder.AppendLine("[PENDING] No runtime encounter events captured yet.");
        for (int i = 0; i < trace.Count; i++) builder.AppendLine(trace[i]);
        return builder.ToString();
    }

    private void OnValidate()
    {
        firstAffectedWave = Mathf.Max(1, firstAffectedWave);
        surgeDelaySeconds = Mathf.Max(1f, surgeDelaySeconds);
        surgeDurationSeconds = Mathf.Max(1f, surgeDurationSeconds);
        minimumSpawnInterval = Mathf.Max(0.1f, minimumSpawnInterval);
        maximumTraceEvents = Mathf.Clamp(maximumTraceEvents, 0, 200);
    }
}
