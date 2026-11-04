using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// 7F.4 is a tuning layer for the existing 7F.2 Director.
// It NEVER spawns an event, writes Save data or changes damage application.
[DisallowMultipleComponent]
[DefaultExecutionOrder(-400)]
public sealed class MS7F4MapBalanceDirector : MonoBehaviour
{
    [Serializable]
    public sealed class WaveRule
    {
        [Min(1)] public int wave = 2;
        [Range(0.5f, 2f)] public float damageScale = 1f;
        [Range(0.65f, 2f)] public float warningScale = 1f;
        [Range(0.6f, 1.5f)] public float shapeScale = 1f;
        [Range(0.5f, 2f)] public float firstDelayScale = 1f;
        [Range(0.5f, 2f)] public float intervalScale = 1f;
        [Range(1, 2)] public int maxEventsPerWave = 1;
    }

    [Serializable]
    public sealed class MapProfile
    {
        public string mapId = "SampleScene";
        public bool enabled = true;
        public List<WaveRule> waves = new List<WaveRule>();
    }

    [Header("Existing 7F.2 reference — no replacement")]
    public WaveManager waveManager;
    public MS7F2ArenaEventDirector arena7F2;

    [Header("Explicit opt-in — OFF after installation")]
    public bool enableMapBalancing = false;
    [Tooltip("Leave empty to match the active Unity Scene name. If all maps use SampleScene, enter the map ID assigned by your map selection system.")]
    public string mapIdOverride = "";
    public List<MapProfile> mapProfiles = new List<MapProfile>();

    [Header("Hard safety caps (7F.2 single hit / safe dodge)")]
    [Min(0.8f)] public float minimumWarningSeconds = 0.9f;
    [Range(2f, 10f)] public float maximumImpactDamage = 8f;
    [Range(1, 2)] public int maximumEventsPerWave = 1;
    [Min(0.65f)] public float minimumRingSafeCenter = 0.8f;

    public bool IsTuning { get { return tuned; } }
    public int AppliedWave { get { return activeWave; } }
    public string ActiveMapId { get { return CurrentMapId(); } }
    public string AppliedProfileId { get { return activeProfile; } }
    public int AppliedCount { get; private set; }
    public int RestoreCount { get; private set; }
    public int BossSkipCount { get; private set; }
    public int NoProfileCount { get; private set; }
    public bool PauseObserved { get; private set; }

    private struct Baseline
    {
        public float delay, interval, warning, damage;
        public float twinRadius, twinOffset, crossWidth, crossLength, ringInner, ringOuter;
        public int eventsPerWave;
    }

    private Baseline original;
    private bool baselineSaved;
    private bool tuned;
    private int activeWave = -1;
    private int lastBossSkip = -1;
    private int lastMissingWave = -1;
    private string activeProfile = "";
    private readonly List<string> trace = new List<string>();

    public static bool WaveEligible(bool optedIn, bool original7F2Enabled, int wave,
        int firstAffectedWave, int totalWaves, int bossWave, bool spawnerActive, bool gameOver)
    {
        return optedIn && original7F2Enabled && spawnerActive && !gameOver &&
            wave >= firstAffectedWave && wave <= totalWaves && wave != bossWave;
    }

    public static float SafeDamage(float baseValue, float scale, float absoluteCap)
    {
        return Mathf.Clamp(Mathf.Max(0f, baseValue) * Mathf.Clamp(scale, 0.5f, 2f),
            0f, Mathf.Clamp(absoluteCap, 2f, 10f));
    }

    public static float SafeWarning(float baseValue, float scale, float minimum)
    {
        return Mathf.Clamp(Mathf.Max(0.2f, baseValue) * Mathf.Clamp(scale, 0.65f, 2f),
            Mathf.Clamp(minimum, 0.8f, 3f), 3f);
    }

    public static bool SameMap(string a, string b)
    {
        return !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) &&
            string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private void Awake()
    {
        Resolve();
        CaptureBaseline();
    }

    private void Resolve()
    {
        if (waveManager == null) waveManager = GetComponent<WaveManager>();
        if (arena7F2 == null) arena7F2 = GetComponent<MS7F2ArenaEventDirector>();
    }

    private string CurrentMapId()
    {
        return string.IsNullOrWhiteSpace(mapIdOverride) ?
            SceneManager.GetActiveScene().name : mapIdOverride.Trim();
    }

    private MapProfile FindProfile(string id)
    {
        if (mapProfiles == null) return null;
        for (int i = 0; i < mapProfiles.Count; ++i)
        {
            MapProfile p = mapProfiles[i];
            if (p != null && p.enabled && SameMap(p.mapId, id)) return p;
        }
        return null;
    }

    private static WaveRule FindWaveRule(MapProfile profile, int wave)
    {
        if (profile == null || profile.waves == null) return null;
        for (int i = 0; i < profile.waves.Count; ++i)
        {
            WaveRule rule = profile.waves[i];
            if (rule != null && rule.wave == wave) return rule;
        }
        return null;
    }

    private void CaptureBaseline()
    {
        if (baselineSaved || arena7F2 == null) return;
        original = new Baseline
        {
            delay = arena7F2.firstEventDelay,
            interval = arena7F2.nextEventInterval,
            warning = arena7F2.warningSeconds,
            damage = arena7F2.impactDamage,
            twinRadius = arena7F2.twinCircleRadius,
            twinOffset = arena7F2.twinCenterOffset,
            crossWidth = arena7F2.crossfireWidth,
            crossLength = arena7F2.crossfireHalfLength,
            ringInner = arena7F2.ringInnerRadius,
            ringOuter = arena7F2.ringOuterRadius,
            eventsPerWave = arena7F2.maxEventsPerWave
        };
        baselineSaved = true;
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        Resolve();
        CaptureBaseline();
        if (waveManager == null || arena7F2 == null || !baselineSaved)
        {
            Restore("missing Wave or 7F.2");
            return;
        }
        if (!enableMapBalancing || !arena7F2.enableMultipleEvents)
        {
            Restore("opt-out or 7F.2 disabled");
            return;
        }
        // Do not restore/change settings in the middle of a pause.
        if (Time.timeScale <= 0f) { PauseObserved = true; return; }

        int wave = waveManager.CurrentWave;
        bool gameOver = waveManager.gameManager != null && waveManager.gameManager.IsGameOver;
        bool active = WaveEligible(true, arena7F2.enableMultipleEvents, wave,
            arena7F2.firstAffectedWave, waveManager.totalWaves, waveManager.bossWave,
            arena7F2.enemySpawner != null && arena7F2.enemySpawner.enabled, gameOver);
        if (!active)
        {
            Restore("break/boss/game-over/outside waves");
            if (wave == waveManager.bossWave && lastBossSkip != wave)
            {
                lastBossSkip = wave;
                BossSkipCount++;
                Log("BOSS SKIP " + wave + " — original 7F.2 untouched");
            }
            return;
        }

        string mapId = CurrentMapId();
        MapProfile profile = FindProfile(mapId);
        WaveRule rule = FindWaveRule(profile, wave);
        if (rule == null)
        {
            Restore("no map/wave profile (7F.2 baseline)");
            if (lastMissingWave != wave)
            {
                lastMissingWave = wave;
                NoProfileCount++;
                Log("BASELINE wave=" + wave + " map=" + mapId + " (no matching rule)");
            }
            return;
        }
        if (tuned && activeWave == wave && activeProfile == profile.mapId) return;
        Restore("new wave/profile");
        Apply(rule, wave, profile.mapId);
    }

    private void Apply(WaveRule rule, int wave, string mapId)
    {
        // All values use the snapshot taken before this component changed 7F.2.
        arena7F2.firstEventDelay = Mathf.Clamp(original.delay * Mathf.Clamp(rule.firstDelayScale, 0.5f, 2f), 0.6f, 4f);
        arena7F2.nextEventInterval = Mathf.Clamp(original.interval * Mathf.Clamp(rule.intervalScale, 0.5f, 2f), 1.5f, 6f);
        arena7F2.warningSeconds = SafeWarning(original.warning, rule.warningScale, minimumWarningSeconds);
        arena7F2.impactDamage = SafeDamage(original.damage, rule.damageScale, maximumImpactDamage);
        int cap = Mathf.Clamp(maximumEventsPerWave, 1, 2);
        arena7F2.maxEventsPerWave = Mathf.Clamp(rule.maxEventsPerWave, 1, cap);

        float scale = Mathf.Clamp(rule.shapeScale, 0.6f, 1.5f);
        arena7F2.twinCircleRadius = Mathf.Clamp(original.twinRadius * scale, 0.7f, 1.35f);
        arena7F2.twinCenterOffset = Mathf.Clamp(original.twinOffset * scale, 0.65f, 1.2f);
        arena7F2.crossfireWidth = Mathf.Clamp(original.crossWidth * scale, 0.4f, 0.9f);
        arena7F2.crossfireHalfLength = Mathf.Clamp(original.crossLength * scale, 2f, 3.2f);

        // Safety: ring always has a visible reachable centre and is not excessively wide.
        arena7F2.ringInnerRadius = Mathf.Clamp(original.ringInner * scale,
            Mathf.Clamp(minimumRingSafeCenter, 0.65f, 1.15f), 1.15f);
        arena7F2.ringOuterRadius = Mathf.Clamp(original.ringOuter * scale,
            arena7F2.ringInnerRadius + 0.55f, 2.4f);

        activeWave = wave;
        activeProfile = mapId;
        tuned = true;
        AppliedCount++;
        Log("APPLY map=" + mapId + " wave=" + wave +
            " kind=" + MS7F2ArenaEventDirector.ForWave(wave, arena7F2.firstAffectedWave) +
            " warning=" + arena7F2.warningSeconds.ToString("F2") +
            " damage=" + arena7F2.impactDamage.ToString("F2") +
            " maxEvents=" + arena7F2.maxEventsPerWave);
    }

    private void Restore(string reason)
    {
        if (!tuned || arena7F2 == null || !baselineSaved) return;
        arena7F2.firstEventDelay = original.delay;
        arena7F2.nextEventInterval = original.interval;
        arena7F2.warningSeconds = original.warning;
        arena7F2.impactDamage = original.damage;
        arena7F2.maxEventsPerWave = original.eventsPerWave;
        arena7F2.twinCircleRadius = original.twinRadius;
        arena7F2.twinCenterOffset = original.twinOffset;
        arena7F2.crossfireWidth = original.crossWidth;
        arena7F2.crossfireHalfLength = original.crossLength;
        arena7F2.ringInnerRadius = original.ringInner;
        arena7F2.ringOuterRadius = original.ringOuter;
        RestoreCount++;
        Log("RESTORE wave=" + activeWave + " fields=11/11 (" + reason + ")");
        activeWave = -1;
        activeProfile = "";
        tuned = false;
    }

    private void OnDisable()
    {
        Restore("component disabled");
    }

    private void OnDestroy()
    {
        Restore("component destroyed");
    }

    private void Log(string message)
    {
        trace.Add(Time.time.ToString("F2") + "s | " + message);
        if (trace.Count > 120) trace.RemoveAt(0);
    }

    public string GetRuntimeReport()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("7F.4 MAP-SPECIFIC EVENT BALANCE — READ ONLY REPORT");
        sb.AppendLine("PlayMode=" + Application.isPlaying + " OptIn=" + enableMapBalancing +
            " Scene=" + SceneManager.GetActiveScene().name + " EffectiveMapId=" + ActiveMapId);
        sb.AppendLine("Wave=" + (waveManager == null ? "?" : waveManager.CurrentWave.ToString()) +
            " AppliedProfile=" + (string.IsNullOrEmpty(activeProfile) ? "NONE" : activeProfile) +
            " Tuned=" + tuned);
        sb.AppendLine("Applies=" + AppliedCount + " Restores=" + RestoreCount +
            " BossSkips=" + BossSkipCount + " UnmatchedWaves=" + NoProfileCount +
            " PauseObserved=" + PauseObserved);
        if (arena7F2 != null)
        {
            sb.AppendLine("7F.2 Enabled=" + arena7F2.enableMultipleEvents +
                " kind=" + (waveManager != null ? MS7F2ArenaEventDirector.ForWave(
                    waveManager.CurrentWave, arena7F2.firstAffectedWave).ToString() : "?") +
                " warning=" + arena7F2.warningSeconds.ToString("F2") +
                " impactDamage=" + arena7F2.impactDamage.ToString("F2") +
                " maxEvents=" + arena7F2.maxEventsPerWave);
            sb.AppendLine("7F.2 TotalEvents=" + arena7F2.TotalEvents + " BossSkips=" + arena7F2.BossSkips +
                " 7F1Suspensions=" + arena7F2.LegacySuspensions +
                " 7F1Restores=" + arena7F2.LegacyRestores);
        }
        sb.AppendLine("--- TRACE ---");
        if (trace.Count == 0) sb.AppendLine("[PENDING] No runtime map balance transitions yet");
        foreach (string t in trace) sb.AppendLine(t);
        sb.AppendLine("NOTE: 7F.2 retains Twin/Crossfire/Ring rotation; this add-on changes ONLY per-map/per-wave balance.");
        sb.AppendLine("NOTE: A restore log confirms field assignments, not actual visibility / Save or safe physical route.");
        return sb.ToString();
    }

    public void SeedDemoProfiles()
    {
        if (mapProfiles == null) mapProfiles = new List<MapProfile>();
        mapProfiles.Clear();
        mapProfiles.Add(new MapProfile
        {
            mapId = "SampleScene",
            waves = new List<WaveRule>
            {
                new WaveRule { wave = 2, damageScale = 0.9f, warningScale = 1.2f, shapeScale = 0.9f, firstDelayScale = 1.0f, maxEventsPerWave = 1 },
                new WaveRule { wave = 3, damageScale = 1f, warningScale = 1.15f, shapeScale = 1f, firstDelayScale = 1f, maxEventsPerWave = 1 },
                new WaveRule { wave = 4, damageScale = 1.05f, warningScale = 1.1f, shapeScale = 1f, firstDelayScale = 1f, maxEventsPerWave = 1 }
            }
        });
        mapProfiles.Add(new MapProfile
        {
            mapId = "Map_Forest_DEMO",
            waves = new List<WaveRule>
            {
                new WaveRule { wave = 2, damageScale = 0.8f, warningScale = 1.35f, shapeScale = 0.85f },
                new WaveRule { wave = 3, damageScale = 0.85f, warningScale = 1.3f, shapeScale = 0.9f },
                new WaveRule { wave = 4, damageScale = 0.9f, warningScale = 1.25f, shapeScale = 0.95f }
            }
        });
        mapProfiles.Add(new MapProfile
        {
            mapId = "Map_Ruins_DEMO",
            waves = new List<WaveRule>
            {
                new WaveRule { wave = 2, damageScale = 1.1f, warningScale = 1.05f, shapeScale = 1.05f },
                new WaveRule { wave = 3, damageScale = 1.15f, warningScale = 1.05f, shapeScale = 1.05f },
                new WaveRule { wave = 4, damageScale = 1.2f, warningScale = 1f, shapeScale = 1.1f }
            }
        });
    }
}
