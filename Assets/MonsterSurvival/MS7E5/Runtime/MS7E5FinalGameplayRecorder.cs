using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 7E.5 FINAL GAMEPLAY QA -- strictly read-only runtime observation.
/// No enemy spawning, HP, stats, rewards, equipment, save or UI modifications.
/// Attach to the existing WaveManager through the 7E.5 Editor installer.
/// </summary>
[DisallowMultipleComponent]
public sealed class MS7E5FinalGameplayRecorder : MonoBehaviour
{
    [Header("Existing components; installer fills references")]
    public WaveManager waveManager;
    public EnemySpawner enemySpawner;
    public MS7E2DynamicWaveDirector dynamicWaves;

    [Header("Read-only capture; automatic during Play Mode")]
    public bool recordLiveEvidence = true;
    [Min(0.2f)] public float sampleInterval = 0.5f;
    [Min(20)] public int maximumEvents = 120;

    public int Samples { get; private set; }
    public int WaveChanges { get; private set; }
    public bool SawRush { get; private set; }
    public bool SawSiege { get; private set; }
    public bool SawSwarm { get; private set; }
    public bool SawBossExcluded { get; private set; }
    public bool SawSpawnerRestore { get; private set; }
    public bool SawSurge { get; private set; }
    public bool SawBulwark { get; private set; }
    public bool SawSwift { get; private set; }
    public bool SawRegenerator { get; private set; }
    public bool SawSetBonusActive { get; private set; }
    public bool SawSetBonusRetracted { get; private set; }
    public bool ObservedTimeScaleZero { get; private set; }

    public bool Recording { get { return Application.isPlaying && recordLiveEvidence; } }

    private readonly List<string> events = new List<string>();
    private float elapsed;
    private int previousWave = -999;
    private int previousRestores;
    private int previousSurges;
    private float previousSetDamage = -1f;
    private float previousSetSpeed = -1f;
    private MS7D2SetBonusApplier setBonus;

    private void Awake() { ResolveReferences(); }

    private void ResolveReferences()
    {
        if (waveManager == null) waveManager = GetComponent<WaveManager>();
        if (waveManager != null && enemySpawner == null) enemySpawner = waveManager.enemySpawner;
        if (dynamicWaves == null) dynamicWaves = GetComponent<MS7E2DynamicWaveDirector>();
        if (setBonus == null) setBonus = FindFirstObjectByType<MS7D2SetBonusApplier>();
    }

    private void OnEnable()
    {
        if (Application.isPlaying && recordLiveEvidence)
            Add("CAPTURE ACTIVE: READ ONLY; monitor attached to " + name);
    }

    private void Update()
    {
        if (!Application.isPlaying || !recordLiveEvidence) return;
        if (Time.timeScale <= 0f) ObservedTimeScaleZero = true;
        elapsed += Time.unscaledDeltaTime;
        if (elapsed < Mathf.Max(0.2f, sampleInterval)) return;
        elapsed = 0f;
        Sample();
    }

    private void Sample()
    {
        ResolveReferences();
        Samples++;
        if (waveManager != null)
        {
            int wave = waveManager.CurrentWave;
            if (wave != previousWave)
            {
                if (previousWave != -999) WaveChanges++;
                Add("WAVE " + previousWave + " -> " + wave +
                    " | spawnerEnabled=" + (enemySpawner != null && enemySpawner.enabled));
                previousWave = wave;
            }
            if (dynamicWaves != null)
            {
                bool isActive = dynamicWaves.CurrentlyApplying;
                if (wave == 2 && isActive && dynamicWaves.CurrentEncounter == MS7E2DynamicWaveDirector.Encounter.Rush && !SawRush)
                { SawRush = true; Add("WAVE 2: RUSH observed while applying"); }
                if (wave == 3 && isActive && dynamicWaves.CurrentEncounter == MS7E2DynamicWaveDirector.Encounter.Siege && !SawSiege)
                { SawSiege = true; Add("WAVE 3: SIEGE observed while applying"); }
                if (wave == 4 && isActive && dynamicWaves.CurrentEncounter == MS7E2DynamicWaveDirector.Encounter.Swarm && !SawSwarm)
                { SawSwarm = true; Add("WAVE 4: SWARM observed while applying"); }
                if (wave == waveManager.bossWave && dynamicWaves.excludeBossWave && !isActive && !SawBossExcluded)
                { SawBossExcluded = true; Add("BOSS WAVE: 7E.2 currently NOT applying (boss behavior still requires manual test)"); }
                if (dynamicWaves.SuccessfulRestores > previousRestores)
                { SawSpawnerRestore = true; Add("7E.2 FULL SPAWNER RESTORE count=" + dynamicWaves.SuccessfulRestores); }
                if (dynamicWaves.SurgeStarts > previousSurges)
                { SawSurge = true; Add("7E.2 SURGE count=" + dynamicWaves.SurgeStarts); }
                previousRestores = dynamicWaves.SuccessfulRestores;
                previousSurges = dynamicWaves.SurgeStarts;
            }
        }

        // The three Affixes are detected from actual live Elite objects, never prefab assets.
        MS7E1EliteAffix[] affixes = FindObjectsByType<MS7E1EliteAffix>(FindObjectsSortMode.None);
        for (int i = 0; i < affixes.Length; i++)
        {
            MS7E1EliteAffix elite = affixes[i];
            if (elite == null || !elite.Initialized) continue;
            switch (elite.ActiveAffix)
            {
                case MS7E1EliteAffix.AffixKind.Bulwark:
                    if (!SawBulwark) { SawBulwark = true; Add("LIVE ELITE AFFIX: Bulwark"); }
                    break;
                case MS7E1EliteAffix.AffixKind.Swift:
                    if (!SawSwift) { SawSwift = true; Add("LIVE ELITE AFFIX: Swift"); }
                    break;
                case MS7E1EliteAffix.AffixKind.Regenerator:
                    if (!SawRegenerator) { SawRegenerator = true; Add("LIVE ELITE AFFIX: Regenerator"); }
                    break;
            }
        }

        if (setBonus != null)
        {
            float dmg = setBonus.AppliedDamageBonus;
            float aspd = setBonus.AppliedAttackSpeedBonus;
            if (dmg > 0.0001f || aspd > 0.0001f) SawSetBonusActive = true;
            if ((previousSetDamage > 0.0001f && dmg < previousSetDamage - 0.0001f) ||
                (previousSetSpeed > 0.0001f && aspd < previousSetSpeed - 0.0001f))
            {
                SawSetBonusRetracted = true;
                Add("7D bonus retracted: DMG " + previousSetDamage.ToString("F3") + "->" + dmg.ToString("F3") +
                    " ASPD " + previousSetSpeed.ToString("F3") + "->" + aspd.ToString("F3"));
            }
            previousSetDamage = dmg;
            previousSetSpeed = aspd;
        }
    }

    private void Add(string message)
    {
        if (maximumEvents <= 0) return;
        events.Add(Time.realtimeSinceStartup.ToString("F1") + "s | " + message);
        if (events.Count > Mathf.Clamp(maximumEvents, 20, 500)) events.RemoveAt(0);
    }

    public string GetEvidence()
    {
        StringBuilder s = new StringBuilder();
        s.AppendLine("=== 7E.5 LIVE OBSERVER (READ ONLY) ===");
        s.AppendLine("RecorderActive=" + Recording + " | Samples=" + Samples + " | WaveChanges=" + WaveChanges);
        s.AppendLine("WAVE2_RUSH=" + SawRush + " | WAVE3_SIEGE=" + SawSiege + " | WAVE4_SWARM=" + SawSwarm);
        s.AppendLine("BOSS_7E2_EXCLUDED=" + SawBossExcluded + " | FULL_RESTORE_SEEN=" + SawSpawnerRestore +
            " | SURGE_SEEN=" + SawSurge + " | TIMESCALE_ZERO_SEEN=" + ObservedTimeScaleZero);
        s.AppendLine("ELITES_LIVE Bulwark=" + SawBulwark + " Swift=" + SawSwift + " Regenerator=" + SawRegenerator);
        s.AppendLine("7D_SET_BONUS_ACTIVE_SEEN=" + SawSetBonusActive + " | RETRACTED_SEEN=" + SawSetBonusRetracted);
        if (dynamicWaves != null)
        {
            s.AppendLine("7E.2 " + dynamicWaves.GetRuntimeSummary());
            s.AppendLine("--- 7E.2 TRACE ---");
            s.AppendLine(dynamicWaves.GetTraceText());
        }
        s.AppendLine("7E.3 initialized=" + MS7E3EliteFeedback.SpawnVisuals +
            " hitFlashes=" + MS7E3EliteFeedback.HitFlashes +
            " enrage=" + MS7E3EliteFeedback.EnrageWarnings +
            " deathBursts=" + MS7E3EliteFeedback.DeathBursts);
        s.AppendLine("7E.4 deaths=" + MS7E4EliteRewards.DeathEvents +
            " grantedDeaths=" + MS7E4EliteRewards.GrantedDeaths +
            " extraExp=" + MS7E4EliteRewards.ExtraExpGranted +
            " extraGold=" + MS7E4EliteRewards.ExtraGoldGranted);
        s.AppendLine("7E.4 payout breakdown: Bulwark=" + MS7E4EliteRewards.BulwarkPayouts +
            " Swift=" + MS7E4EliteRewards.SwiftPayouts +
            " Regenerator=" + MS7E4EliteRewards.RegeneratorPayouts);
        s.AppendLine("7E.4 disabledSkips=" + MS7E4EliteRewards.SkippedDisabled +
            " noPlayerSkips=" + MS7E4EliteRewards.SkippedNoPlayer +
            " unreadySkips=" + MS7E4EliteRewards.SkippedNotInitialized);
        s.AppendLine("--- 7E.4 TRACE ---");
        s.AppendLine(MS7E4EliteRewards.RecentEvents());
        s.AppendLine("--- 7E.3 TRACE ---");
        s.AppendLine(MS7E3EliteFeedback.GetRecentTrace());
        s.AppendLine("--- 7E.5 OBSERVER EVENTS ---");
        if (events.Count == 0) s.AppendLine("[PENDING] No observer events recorded.");
        else foreach (string item in events) s.AppendLine(item);
        s.AppendLine("NOTE: event counters DO NOT prove visible pixels, XP/Gold wallet totals, Save/Profile or bosses' HP.");
        return s.ToString();
    }

    private void OnValidate()
    {
        sampleInterval = Mathf.Max(0.2f, sampleInterval);
        maximumEvents = Mathf.Clamp(maximumEvents, 20, 500);
    }
}
