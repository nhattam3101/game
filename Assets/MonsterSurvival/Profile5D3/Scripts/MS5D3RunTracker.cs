using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Tracks official victories AFTER 5A.3 saves, and actual WaveManager-spawned Boss deaths.
// Does not edit PlayerProgression, MS5A1 profiles, WaveManager, Health or achievement scripts.
[DisallowMultipleComponent]
[DefaultExecutionOrder(600)]
public sealed class MS5D3RunTracker : MonoBehaviour
{
    public MS5A3PersistentRun persistentRun;
    public WaveManager waveManager;
    public PlayerProgression progression;

    private static readonly FieldInfo RunIdField = typeof(MS5A3PersistentRun).GetField("runId", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo StartingCoinsField = typeof(MS5A3PersistentRun).GetField("startingCoins", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo BossHealthField = typeof(WaveManager).GetField("bossHealth", BindingFlags.Instance | BindingFlags.NonPublic);

    private int capturedSlot;
    private string runId;
    private int startingCoins;
    private bool ready;
    private bool victoryRecorded;
    private Health trackedBoss;
    private int bossSerial;
    private readonly List<PendingBoss> pending = new List<PendingBoss>();
    private float nextRetry;
    private bool warned;

    private sealed class PendingBoss
    {
        public string bossId;
        public string eventId;
    }

    private void Start()
    {
        capturedSlot = MS5A2GoldBank.SelectedSlot;
        if (!persistentRun) persistentRun = FindFirstObjectByType<MS5A3PersistentRun>();
        if (!waveManager) waveManager = FindFirstObjectByType<WaveManager>();
        if (!progression) progression = FindFirstObjectByType<PlayerProgression>();
        if (!persistentRun || !progression || !waveManager || RunIdField == null || StartingCoinsField == null)
        {
            Debug.LogError("[5D.3] Missing 5A.3 run/Progression/WaveManager or changed 5A.3 fields. Tracking disabled.", this);
            enabled = false;
            return;
        }
        runId = RunIdField.GetValue(persistentRun) as string;
        startingCoins = (int)StartingCoinsField.GetValue(persistentRun);
        ready = !string.IsNullOrEmpty(runId);
        if (!ready)
        {
            Debug.LogError("[5D.3] 5A.3 run ID not initialized. No statistics will be recorded.", this);
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (!ready) return;
        if (MS5A2GoldBank.SelectedSlot != capturedSlot)
        {
            if (!warned)
            {
                warned = true;
                Debug.LogWarning("[5D.3] Profile changed during run. Stats tracking frozen for safety.", this);
            }
            Unsubscribe();
            return;
        }
        WatchOfficialBoss();
        if (Time.unscaledTime < nextRetry) return;
        nextRetry = Time.unscaledTime + 1f;
        FlushPendingBosses();
        CheckCommittedVictory();
    }

    private void WatchOfficialBoss()
    {
        if (BossHealthField == null) return;
        Health current = BossHealthField.GetValue(waveManager) as Health;
        if (current == trackedBoss) return;
        Unsubscribe();
        if (!current || current.CurrentHealth <= 0f) return;
        trackedBoss = current;
        trackedBoss.OnDied += OnOfficialBossDied;
    }

    private void OnOfficialBossDied()
    {
        Health defeated = trackedBoss;
        Unsubscribe();
        if (!defeated || defeated.CurrentHealth > 0f ||
            waveManager.CurrentWave < waveManager.bossWave ||
            MS5A2GoldBank.SelectedSlot != capturedSlot) return;
        string name = waveManager.bossPrefab ? waveManager.bossPrefab.name.ToLowerInvariant() : "";
        if (!name.Contains("warden") && !name.Contains("titan"))
            name = (waveManager.bossName ?? "").ToLowerInvariant();
        bool titan = name.Contains("titan");
        bool warden = name.Contains("warden");
        if (titan == warden) // ambiguous; do not invent a Boss kill
        {
            Debug.LogWarning("[5D.3] Unknown Boss identity; no counter increment.", this);
            return;
        }
        bossSerial++;
        pending.Add(new PendingBoss
        {
            bossId = titan ? "achievement.shadow_titan" : "achievement.grave_warden",
            eventId = runId + ":" + bossSerial
        });
        FlushPendingBosses();
    }

    private void FlushPendingBosses()
    {
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            string message;
            if (MS5D3StatsStore.TryRecordBossKill(capturedSlot, pending[i].bossId, pending[i].eventId, out message))
                pending.RemoveAt(i);
            else if (Time.frameCount % 300 == 0)
                Debug.LogWarning("[5D.3] Boss count write pending: " + message, this);
        }
    }

    private void CheckCommittedVictory()
    {
        if (victoryRecorded || !persistentRun.Committed) return;
        MS5A1SaveData save;
        string result;
        bool fromBackup;
        if (!MS5A1SaveService.TryLoad(capturedSlot, out save, out result, out fromBackup) || fromBackup || save == null ||
            save.lastCommittedRunId != runId)
        {
            if (Time.frameCount % 300 == 0)
                Debug.LogWarning("[5D.3] Victory not verified in primary 5A.1 save. Statistics NOT committed.", this);
            return;
        }
        int earnedGold = Mathf.Max(0, progression.coins - startingCoins);
        string message;
        if (MS5D3StatsStore.TryRecordVictory(capturedSlot, runId, earnedGold, out message))
        {
            victoryRecorded = true;
            Debug.Log("[5D.3] Victory statistics verified and stored, Gold=" + earnedGold + ", Profile=" + capturedSlot, this);
        }
        else if (Time.frameCount % 300 == 0)
            Debug.LogWarning("[5D.3] Victory committed in 5A.3; statistics write pending: " + message, this);
    }

    private void Unsubscribe()
    {
        if (trackedBoss) trackedBoss.OnDied -= OnOfficialBossDied;
        trackedBoss = null;
    }
    private void OnDisable() { Unsubscribe(); }
    private void OnDestroy() { Unsubscribe(); }
}
