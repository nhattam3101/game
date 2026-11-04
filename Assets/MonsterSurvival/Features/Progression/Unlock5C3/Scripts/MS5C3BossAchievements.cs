using System;
using System.Reflection;
using UnityEngine;

// Watches only the Health instance spawned and tracked by WaveManager.
// No Boss AI or Health script is replaced. Avoids rewarding unrelated deaths.
[DisallowMultipleComponent]
[DefaultExecutionOrder(500)]
public sealed class MS5C3BossAchievements : MonoBehaviour
{
    public WaveManager waveManager;
    private static readonly FieldInfo BossHealthField = typeof(WaveManager).GetField(
        "bossHealth", BindingFlags.NonPublic | BindingFlags.Instance);
    private Health subscribedBoss;
    private int profileAtRunStart;
    private bool grantedThisRun;
    private string pendingId;
    private bool failureLogged;

    private void Awake()
    {
        profileAtRunStart = MS5A2GoldBank.SelectedSlot;
        if (!waveManager) waveManager = FindFirstObjectByType<WaveManager>();
    }

    private void LateUpdate()
    {
        if (!waveManager || grantedThisRun || BossHealthField == null)
        {
            if (!failureLogged && BossHealthField == null)
            { failureLogged = true; Debug.LogError("[5C.3] WaveManager.bossHealth field not found. Boss achievements disabled.", this); }
            return;
        }
        Health current = BossHealthField.GetValue(waveManager) as Health;
        if (current == subscribedBoss) return;
        Unsubscribe();
        if (!current || current.CurrentHealth <= 0f) return;
        subscribedBoss = current;
        subscribedBoss.OnDied += OnBossDefeated;
        Debug.Log("[5C.3] Tracking spawned Boss for achievement: " + current.name, this);
    }

    private void OnBossDefeated()
    {
        Health defeated = subscribedBoss;
        Unsubscribe();
        if (grantedThisRun || !defeated || defeated.CurrentHealth > 0f || !waveManager ||
            waveManager.CurrentWave < waveManager.bossWave) return;
        // Prefer the actual spawn prefab name; an old bossName UI label
        // can remain stale after switching the boss prefab in Inspector.
        string identity = (waveManager.bossPrefab ? waveManager.bossPrefab.name : "").ToLowerInvariant();
        if (!identity.Contains("warden") && !identity.Contains("titan"))
            identity = (waveManager.bossName ?? "").ToLowerInvariant();
        bool warden = identity.Contains("warden");
        bool titan = identity.Contains("titan");
        if (warden == titan)
        {
            Debug.LogWarning("[5C.3] Ambiguous Boss identity; no achievement: " + identity, this);
            return;
        }
        pendingId = warden ? "achievement.grave_warden" : "achievement.shadow_titan";
        if (MS5A2GoldBank.SelectedSlot != profileAtRunStart)
        {
            Debug.LogWarning("[5C.3] Profile changed mid-run; achievement not saved.", this);
            return;
        }
        TryGrant();
    }

    private void TryGrant()
    {
        MS5C1UnlockService.Snapshot snapshot;
        string message;
        if (!MS5C1UnlockService.TryGetSnapshot(profileAtRunStart, out snapshot, out message))
        { Debug.LogWarning("[5C.3] Achievement read blocked: " + message, this); return; }
        if (snapshot.IsUnlocked(pendingId))
        {
            grantedThisRun = true;
            Debug.Log("[5C.3] Boss achievement already owned: " + pendingId, this);
            return;
        }
        if (snapshot.isNew || snapshot.fromBackup)
        { Debug.LogWarning("[5C.3] Achievement cannot write to new or backup-only profile: " + message, this); return; }
        MS5C1UnlockService.UnlockReceipt receipt;
        string result;
        if (MS5C1UnlockService.TryGrantAchievement(profileAtRunStart, pendingId, snapshot.revision,
            out receipt, out result))
        {
            grantedThisRun = true;
            Debug.Log("[5C.3] ACHIEVEMENT SAVED: " + pendingId + ", profile=" + profileAtRunStart +
                ", rev=" + receipt.revision, this);
        }
        else Debug.LogError("[5C.3] Achievement Save FAILED: " + result +
            ". Save remains untouched. Check Save/Recovery before leaving.", this);
    }

    private void Unsubscribe()
    {
        if (subscribedBoss) subscribedBoss.OnDied -= OnBossDefeated;
        subscribedBoss = null;
    }
    private void OnDisable() { Unsubscribe(); }
    private void OnDestroy() { Unsubscribe(); }
}
