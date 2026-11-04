using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// 7F.5: READ ONLY observer. Never changes Scene components, Save/Profile, HP, Waves or Spawner.
[DisallowMultipleComponent]
[DefaultExecutionOrder(2000)]
public sealed class MS7F5ArenaFinalRecorder : MonoBehaviour
{
    [Header("Existing components on WaveManager (read only)")]
    public WaveManager waveManager;
    public WaveMapSwitcher mapSwitcher;
    public MS7F1ArenaHazardDirector legacy7F1;
    public MS7F2ArenaEventDirector arena7F2;
    public MS7F3ArenaVFXDirector arena7F3;
    public MS7F4MapBalanceDirector mapBalance7F4;
    public MS7F41AutoMapIdBinder mapBinder7F41;

    [Header("Observer only")]
    public bool enableObservation = true;
    [Range(50, 600)] public int traceLimit = 180;

    public int Samples { get; private set; }
    public bool SawWave2 { get; private set; }
    public bool SawWave3 { get; private set; }
    public bool SawWave4 { get; private set; }
    public bool SawBossWave { get; private set; }
    public bool SawTwinAt2 { get; private set; }
    public bool SawCrossfireAt3 { get; private set; }
    public bool SawRingAt4 { get; private set; }
    public bool SawAllThreeVFX { get { return arena7F3 != null && arena7F3.TwinVisuals > 0 &&
        arena7F3.CrossfireVisuals > 0 && arena7F3.RingVisuals > 0; } }
    public bool SawPause { get; private set; }
    public bool PauseCountersStable { get; private set; }
    public bool BossNo7F2Events { get; private set; } = true;
    public bool BossNo7F3Visuals { get; private set; } = true;
    public bool Boss7F4NotTuning { get; private set; } = true;
    public bool OverlapDetected { get; private set; }
    public bool MapMatchedSeen { get; private set; }
    public bool MapMismatchSeen { get; private set; }
    public string ObservedMapId { get; private set; } = "";
    public int MapTransitions { get; private set; }
    public bool Saw7F4ApplyAt2 { get; private set; }
    public bool Saw7F4ApplyAt3 { get; private set; }
    public bool Saw7F4ApplyAt4 { get; private set; }
    public bool Saw7F4Restore { get; private set; }

    private readonly List<string> trace = new List<string>();
    private int lastWave = int.MinValue;
    private int lastTwin, lastCross, lastRing, lastApplies, lastRestores, lastEvents, lastVisuals;
    private float pauseElapsed = -1f;
    private int pausedEvents, pausedImpacts, pausedVisuals, paused7F4Applies;
    private bool bossFirstFrame;
    private int bossStartEvents, bossStartVisuals;
    private float clock;

    private void Awake() { ResolveReferences(); }
    private void ResolveReferences()
    {
        if (waveManager == null) waveManager = GetComponent<WaveManager>();
        if (mapSwitcher == null) mapSwitcher = GetComponent<WaveMapSwitcher>();
        if (legacy7F1 == null) legacy7F1 = GetComponent<MS7F1ArenaHazardDirector>();
        if (arena7F2 == null) arena7F2 = GetComponent<MS7F2ArenaEventDirector>();
        if (arena7F3 == null) arena7F3 = GetComponent<MS7F3ArenaVFXDirector>();
        if (mapBalance7F4 == null) mapBalance7F4 = GetComponent<MS7F4MapBalanceDirector>();
        if (mapBinder7F41 == null) mapBinder7F41 = GetComponent<MS7F41AutoMapIdBinder>();
    }
    private void Start()
    {
        ResolveReferences();
        if (arena7F2 != null)
        {
            lastTwin = arena7F2.TwinStarts; lastCross = arena7F2.CrossfireStarts;
            lastRing = arena7F2.RingStarts; lastEvents = arena7F2.TotalEvents;
        }
        if (arena7F3 != null) lastVisuals = arena7F3.VFXInitialized;
        if (mapBalance7F4 != null)
        { lastApplies = mapBalance7F4.AppliedCount; lastRestores = mapBalance7F4.RestoreCount; }
        Trace("OBSERVER START: READ ONLY");
    }
    private void Update()
    {
        if (!Application.isPlaying || !enableObservation) return;
        ResolveReferences();
        if (waveManager == null) return;
        ++Samples;
        clock += Time.unscaledDeltaTime;
        int wave = waveManager.CurrentWave;
        if (wave != lastWave)
        {
            Trace("WAVE " + lastWave + " -> " + wave);
            lastWave = wave;
            if (wave == 2) SawWave2 = true;
            if (wave == 3) SawWave3 = true;
            if (wave == 4) SawWave4 = true;
        }
        if (arena7F2 != null)
        {
            if (arena7F2.TwinStarts > lastTwin)
            { if (wave == 2) SawTwinAt2 = true; Trace("7F2 TWIN start at wave=" + wave); }
            if (arena7F2.CrossfireStarts > lastCross)
            { if (wave == 3) SawCrossfireAt3 = true; Trace("7F2 CROSSFIRE start at wave=" + wave); }
            if (arena7F2.RingStarts > lastRing)
            { if (wave == 4) SawRingAt4 = true; Trace("7F2 HOLLOW RING start at wave=" + wave); }
            lastTwin = arena7F2.TwinStarts;
            lastCross = arena7F2.CrossfireStarts;
            lastRing = arena7F2.RingStarts;
            lastEvents = arena7F2.TotalEvents;
            if (arena7F2.enableMultipleEvents && legacy7F1 != null && legacy7F1.enableArenaHazards)
                OverlapDetected = true;
        }
        if (arena7F3 != null) lastVisuals = arena7F3.VFXInitialized;
        if (mapBalance7F4 != null)
        {
            if (mapBalance7F4.AppliedCount > lastApplies)
            {
                int targetWave = mapBalance7F4.AppliedWave;
                if (targetWave == 2) Saw7F4ApplyAt2 = true;
                if (targetWave == 3) Saw7F4ApplyAt3 = true;
                if (targetWave == 4) Saw7F4ApplyAt4 = true;
                Trace("7F4 APPLY wave=" + targetWave + " map='" + mapBalance7F4.AppliedProfileId + "'");
            }
            if (mapBalance7F4.RestoreCount > lastRestores)
            { Saw7F4Restore = true; Trace("7F4 RESTORE count=" + mapBalance7F4.RestoreCount); }
            lastApplies = mapBalance7F4.AppliedCount;
            lastRestores = mapBalance7F4.RestoreCount;
        }
        if (mapBinder7F41 != null && mapBalance7F4 != null && mapSwitcher != null)
        {
            int index;
            string id;
            if (mapBinder7F41.TryGetActualMap(out index, out id))
            {
                bool match = mapBinder7F41.enableAutoBinding && mapBinder7F41.ProfileMatched &&
                    MS7F4MapBalanceDirector.SameMap(mapBalance7F4.ActiveMapId, id);
                if (match) MapMatchedSeen = true;
                else if (mapBinder7F41.enableAutoBinding && wave > 0) MapMismatchSeen = true;
                if (!string.Equals(ObservedMapId, id, StringComparison.Ordinal))
                { ObservedMapId = id; MapTransitions++; Trace("ACTIVE MAP '" + id + "' index=" + index + " matched=" + match); }
            }
        }
        if (wave == waveManager.bossWave && wave > 0)
        {
            SawBossWave = true;
            if (!bossFirstFrame)
            {
                bossFirstFrame = true;
                bossStartEvents = arena7F2 != null ? arena7F2.TotalEvents : 0;
                bossStartVisuals = arena7F3 != null ? arena7F3.VFXInitialized : 0;
                Trace("BOSS ENTER: arena event counters snapshot");
            }
            if (arena7F2 != null && (arena7F2.TotalEvents > bossStartEvents || arena7F2.ActiveEventCount > 0))
                BossNo7F2Events = false;
            if (arena7F3 != null && (arena7F3.VFXInitialized > bossStartVisuals || arena7F3.ActiveVisuals > 0))
                BossNo7F3Visuals = false;
            if (mapBalance7F4 != null && mapBalance7F4.IsTuning) Boss7F4NotTuning = false;
        }
        if (Time.timeScale <= 0f)
        {
            if (!SawPause && wave >= 2 && wave != waveManager.bossWave &&
                waveManager.enemySpawner != null && waveManager.enemySpawner.enabled)
            {
                SawPause = true; pauseElapsed = 0f;
                pausedEvents = arena7F2 != null ? arena7F2.TotalEvents : 0;
                pausedImpacts = arena7F2 != null ? arena7F2.TotalImpacts : 0;
                pausedVisuals = arena7F3 != null ? arena7F3.ImpactVisuals : 0;
                paused7F4Applies = mapBalance7F4 != null ? mapBalance7F4.AppliedCount : 0;
                Trace("PAUSE START (counters snapshot)");
            }
            if (pauseElapsed >= 0f) pauseElapsed += Time.unscaledDeltaTime;
            if (pauseElapsed >= 0.5f &&
                pausedEvents == (arena7F2 != null ? arena7F2.TotalEvents : 0) &&
                pausedImpacts == (arena7F2 != null ? arena7F2.TotalImpacts : 0) &&
                pausedVisuals == (arena7F3 != null ? arena7F3.ImpactVisuals : 0) &&
                paused7F4Applies == (mapBalance7F4 != null ? mapBalance7F4.AppliedCount : 0))
                PauseCountersStable = true;
        }
        else if (pauseElapsed >= 0f) pauseElapsed = -1f;
    }
    private void Trace(string s)
    {
        trace.Add(clock.ToString("F2") + "s | " + s);
        while (trace.Count > Mathf.Clamp(traceLimit, 50, 600)) trace.RemoveAt(0);
    }
    public string GetRuntimeReport()
    {
        StringBuilder b = new StringBuilder();
        b.AppendLine("7F.5 READ-ONLY ARENA OBSERVER");
        b.AppendLine("Samples=" + Samples + " Wave=" + (waveManager != null ? waveManager.CurrentWave : -1));
        b.AppendLine("MapId='" + ObservedMapId + "' MapTransitions=" + MapTransitions + " Matched=" + MapMatchedSeen + " Mismatch=" + MapMismatchSeen);
        b.AppendLine("Waves2_3_4=" + SawWave2 + "/" + SawWave3 + "/" + SawWave4 + " BossSeen=" + SawBossWave);
        b.AppendLine("EventsAtCorrectWaves Twin/Cross/Ring=" + SawTwinAt2 + "/" + SawCrossfireAt3 + "/" + SawRingAt4);
        b.AppendLine("7F4ApplyAt2_3_4=" + Saw7F4ApplyAt2 + "/" + Saw7F4ApplyAt3 + "/" + Saw7F4ApplyAt4 + " RestoreSeen=" + Saw7F4Restore);
        b.AppendLine("BossNoNew7F2Events=" + BossNo7F2Events + " BossNoNew7F3Visuals=" + BossNo7F3Visuals + " Boss7F4NotTuning=" + Boss7F4NotTuning);
        b.AppendLine("7F1OverlapDetected=" + OverlapDetected + " PauseSeen=" + SawPause + " PauseCountersStable=" + PauseCountersStable);
        b.AppendLine("--- OBSERVER TRACE ---");
        foreach (string s in trace) b.AppendLine(s);
        return b.ToString();
    }
}
