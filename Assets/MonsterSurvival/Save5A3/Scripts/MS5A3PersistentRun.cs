using System;
using UnityEngine;

// 5A.3: atomic VICTORY commit for Gold+Inventory+Gear in ONE 5A.1 TrySave.
// This REPLACES the old 5A.2 watcher in SampleScene (installer disables it).
// Inventory is restored only into a clean Player, once at the start of the run.
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class MS5A3PersistentRun : MonoBehaviour
{
    public GameManager gameManager;
    public PlayerProgression progression;
    public MS3APlayerInventory inventory;
    public MS3DPlayerEquipment equipment;
    public MS5A3ItemRegistry itemRegistry;
    public MS5A2BankOnVictory legacyGoldWatcher;

    [SerializeField] private bool legacyWatcherWasEnabled = true; // installer rollback
    [SerializeField] private int capturedProfileSlot;
    [SerializeField] private int startingCoins;
    [SerializeField] private string runId;
    [SerializeField] private bool ready;
    [SerializeField] private bool victoryObserved;
    [SerializeField] private bool committed;
    [SerializeField] private string status;

    public bool LegacyWatcherWasEnabled { get { return legacyWatcherWasEnabled; } }
    public string Status { get { return status; } }
    public bool Committed { get { return committed; } }
    public void SetLegacyOriginalState(bool state) { legacyWatcherWasEnabled = state; }

    private void Start()
    {
        capturedProfileSlot = MS5A2GoldBank.SelectedSlot;
        if (!gameManager) gameManager = FindFirstObjectByType<GameManager>();
        if (!progression) progression = FindFirstObjectByType<PlayerProgression>();
        if (!inventory) inventory = FindFirstObjectByType<MS3APlayerInventory>();
        if (!equipment && inventory) equipment = inventory.GetComponent<MS3DPlayerEquipment>();
        startingCoins = progression ? progression.coins : 0;
        runId = Guid.NewGuid().ToString("N");
        ready = false; victoryObserved = false; committed = false;
        if (!gameManager || !progression || !inventory || !equipment || !itemRegistry)
        { status = "MISSING DEPENDENCIES — no automatic save"; Debug.LogError("[5A.3] " + status, this); return; }
        if (legacyGoldWatcher && legacyGoldWatcher.enabled)
        { status = "ERROR: legacy 5A.2 watcher active — risk of double banking";
          Debug.LogError("[5A.3] " + status + ". Disable MS5A2BankOnVictory in Scene.", this); return; }
        System.Collections.Generic.Dictionary<string, MS3AItemData> lookup;
        string error;
        if (!itemRegistry.TryBuild(out lookup, out error))
        { status = "REGISTRY INVALID: " + error; Debug.LogError("[5A.3] " + status, this); return; }
        MS5A1SaveData profile; string loadResult; bool recovered;
        bool exists = MS5A1SaveService.TryLoad(capturedProfileSlot, out profile, out loadResult, out recovered);
        if (!exists)
        {
            if (loadResult == null || !loadResult.StartsWith("NO_SAVE", StringComparison.Ordinal))
            { status = "PROFILE INVALID: " + loadResult; Debug.LogError("[5A.3] " + status, this); return; }
            ready = true;
            status = "NEW PROFILE — clean inventory; Victory will create save";
            return;
        }
        if (profile.inventorySnapshotPresent)
        {
            MS3AItemData[] items, worn; int[] quantities;
            if (!MS5A3SnapshotCodec.TryResolve(profile, itemRegistry, equipment.catalog,
                                                 out items, out quantities, out worn, out error))
            { status = "RESTORE BLOCKED: " + error; Debug.LogError("[5A.3] " + status, this); return; }
            if (!MS5A3SnapshotCodec.IsEmptyPlayer(inventory, equipment))
            { status = "RESTORE BLOCKED: Player starts with items or equipped gear; will NOT overwrite";
              Debug.LogError("[5A.3] " + status, this); return; }
            if (!inventory.TryRestoreSnapshot(items, quantities) || !equipment.TryRestoreSnapshot(worn))
            { status = "RESTORE FAILED (check catalog / prefab)"; Debug.LogError("[5A.3] " + status, this); return; }
            // Existing UI is allowed to update via existing events; explicit redraw for a panel already open.
            var ui = FindFirstObjectByType<MS3AInventoryUI>();
            if (ui) ui.Redraw();
        }
        ready = true;
        status = profile.inventorySnapshotPresent ? "RESTORED 20+3 slots for profile " + capturedProfileSlot
                                                  : "LEGACY PROFILE — no item snapshot; start empty";
        if (recovered) status += " (RECOVERED BACKUP)";
        Debug.Log("[5A.3] " + status, this);
    }

    private void Update()
    {
        if (!ready || victoryObserved || !gameManager || !gameManager.IsGameOver) return;
        bool won = gameManager.victoryPanel && gameManager.victoryPanel.activeSelf;
        bool lost = gameManager.gameOverPanel && gameManager.gameOverPanel.activeSelf;
        victoryObserved = true;
        if (won && !lost) TryCommitVictory();
        else { status = "DEFEAT: persistent Inventory/Equipment and Gold unchanged"; }
    }

    [ContextMenu("5A.3 / RETRY pending Victory save")]
    public void RetryPendingVictory()
    {
        if (Application.isPlaying && ready && victoryObserved && !committed &&
            gameManager && gameManager.victoryPanel && gameManager.victoryPanel.activeSelf)
            TryCommitVictory();
    }

    private void TryCommitVictory()
    {
        if (committed || !ready) return;
        MS5A1SaveData profile; string loadResult; bool recovered;
        if (!MS5A1SaveService.TryLoad(capturedProfileSlot, out profile, out loadResult, out recovered))
        {
            if (loadResult == null || !loadResult.StartsWith("NO_SAVE", StringComparison.Ordinal))
            { status = "SAVE BLOCKED: " + loadResult; Debug.LogError("[5A.3] " + status, this); return; }
            profile = MS5A1SaveService.CreateFresh(capturedProfileSlot);
        }
        // Durable run ID avoids duplicate reward if TrySave succeeded but client lost response.
        if (!string.IsNullOrEmpty(profile.lastCommittedRunId) && profile.lastCommittedRunId == runId)
        { committed = true; status = "Already committed this run"; return; }
        int award = Mathf.Max(0, progression.coins - startingCoins);
        long total = (long)profile.bankGold + award;
        if (total > int.MaxValue) { status = "SAVE BLOCKED: bankGold overflow"; Debug.LogError("[5A.3] " + status, this); return; }
        string error;
        if (!MS5A3SnapshotCodec.TryCapture(inventory, equipment, itemRegistry, profile, out error))
        { status = "SAVE BLOCKED: " + error; Debug.LogError("[5A.3] " + status, this); return; }
        profile.bankGold = (int)total;
        profile.lastCommittedRunId = runId;
        MS5A1SaveData saved; string result;
        if (!MS5A1SaveService.TrySave(capturedProfileSlot, profile, out saved, out result))
        { status = "SAVE FAILED: " + result; Debug.LogError("[5A.3] " + status + " — retry via component Context Menu before leaving Scene.", this); return; }
        committed = true;
        status = "VICTORY SAVED: +" + award + " bank Gold & 20 inventory + 3 equipped; rev=" + saved.revision;
        Debug.Log("[5A.3] " + status, this);
    }
}
