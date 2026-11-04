using System;
using System.Collections.Generic;

// 5B.1 PURE SAVE TRANSACTIONS. Does not touch PlayerProgression.coins, Inventory,
// Equipment, GameManager, or runtime combat stats. 5B.2 UI consumes this service;
// 5B.3 applies bonuses at gameplay startup. One purchase = one verified TrySave.
public static class MS5B1PermanentUpgrades
{
    public sealed class Row
    {
        public string id;
        public string title;
        public string description;
        public int level;
        public int maxLevel;
        public int nextCost; // -1 means maxed out
    }

    public sealed class Snapshot
    {
        public int slot;
        public int bankGold;
        public int revision;
        public bool isNewProfile;
        public readonly List<Row> rows = new List<Row>();

        public int LevelOf(string id)
        {
            foreach (Row row in rows)
                if (string.Equals(row.id, id, StringComparison.Ordinal)) return row.level;
            return 0;
        }
    }

    public sealed class PurchaseReceipt
    {
        public int slot;
        public string upgradeId;
        public int previousLevel;
        public int newLevel;
        public int goldSpent;
        public int remainingBankGold;
        public int savedRevision;
    }

    // READ ONLY. NO_SAVE is displayed as a fresh profile, but not written.
    public static bool TryGetSnapshot(int slot, out Snapshot snapshot, out string message, bool qa = false)
    {
        snapshot = null;
        if (slot < 1 || slot > 3) { message = "Invalid profile slot (1-3 only)."; return false; }
        MS5A1SaveData profile;
        bool recovered;
        string loadMessage;
        bool loaded = MS5A1SaveService.TryLoad(slot, out profile, out loadMessage, out recovered, qa);
        bool isNew = !loaded && IsNoSave(loadMessage);
        if (!loaded && !isNew) { message = "LOAD BLOCKED: " + loadMessage; return false; }
        if (isNew) profile = MS5A1SaveService.CreateFresh(slot, qa);
        if (profile.permanentUpgrades == null)
        { message = "SAVE INVALID: permanentUpgrades list is missing."; return false; }
        snapshot = new Snapshot();
        snapshot.slot = slot;
        snapshot.bankGold = profile.bankGold;
        snapshot.revision = profile.revision;
        snapshot.isNewProfile = isNew;
        foreach (MS5B1UpgradeDefinition definition in MS5B1UpgradeDefinitions.GetAll())
        {
            int level = ReadLevel(profile.permanentUpgrades, definition.id);
            if (level < 0 || level > definition.maxLevel)
            { message = "INVALID UPGRADE LEVEL: " + definition.id; snapshot = null; return false; }
            snapshot.rows.Add(new Row {
                id = definition.id,
                title = definition.title,
                description = definition.bonusDescription,
                level = level,
                maxLevel = definition.maxLevel,
                nextCost = definition.PriceForNextLevel(level)
            });
        }
        message = isNew ? "NEW PROFILE (read only)" : (recovered ? "BACKUP RECOVERED (read only; buying disabled until primary repaired)" : "SAVED PROFILE");
        return true;
    }

    // Requires expectedRevision from the UI's last read. Duplicate/stale buy requests
    // with the same revision cannot spend twice. Refresh snapshot after EVERY purchase.
    public static bool TryPurchase(int slot, string upgradeId, int expectedRevision,
                                   out PurchaseReceipt receipt, out string message, bool qa = false)
    {
        receipt = null;
        if (slot < 1 || slot > 3) { message = "INVALID_SLOT"; return false; }
        if (expectedRevision < 0) { message = "INVALID_REVISION"; return false; }
        MS5B1UpgradeDefinition definition;
        if (!MS5B1UpgradeDefinitions.TryGet(upgradeId, out definition))
        { message = "UNKNOWN_UPGRADE"; return false; }

        MS5A1SaveData profile;
        bool recovered;
        string loadMessage;
        if (!MS5A1SaveService.TryLoad(slot, out profile, out loadMessage, out recovered, qa))
        {
            // New empty profiles have zero Gold and cannot purchase upgrades.
            // Corrupt/wrong-version profiles must never be overwritten.
            message = IsNoSave(loadMessage) ? "NOT_ENOUGH_BANK_GOLD (new profile)" : "LOAD_BLOCKED: " + loadMessage;
            return false;
        }
        if (recovered)
        { message = "RECOVERED_BACKUP: restore the verified backup in 5A.4 before making purchases."; return false; }
        if (profile.revision != expectedRevision)
        { message = "STALE_REVISION: reload the profile before purchasing."; return false; }
        if (profile.permanentUpgrades == null)
        { message = "INVALID_UPGRADE_LIST"; return false; }
        int currentLevel = ReadLevel(profile.permanentUpgrades, definition.id);
        if (currentLevel < 0 || currentLevel > definition.maxLevel)
        { message = "INVALID_UPGRADE_LEVEL"; return false; }
        int price = definition.PriceForNextLevel(currentLevel);
        if (price < 0) { message = "MAX_LEVEL"; return false; }
        if (profile.bankGold < price) { message = "NOT_ENOUGH_BANK_GOLD"; return false; }

        // Mutate only the in-memory 5A.1 save DTO, including all existing 5A.3 data.
        bool found = false;
        foreach (MS5A1UpgradeEntry entry in profile.permanentUpgrades)
        {
            if (entry == null) { message = "INVALID_UPGRADE_ENTRY"; return false; }
            if (!string.Equals(entry.id, upgradeId, StringComparison.Ordinal)) continue;
            entry.level = currentLevel + 1;
            found = true;
            break;
        }
        if (!found) profile.permanentUpgrades.Add(new MS5A1UpgradeEntry { id = upgradeId, level = 1 });
        profile.bankGold -= price;

        MS5A1SaveData saved;
        string saveMessage;
        if (!MS5A1SaveService.TrySave(slot, profile, out saved, out saveMessage, qa))
        { message = "PURCHASE_FAILED: " + saveMessage; return false; }
        receipt = new PurchaseReceipt {
            slot = slot, upgradeId = upgradeId,
            previousLevel = currentLevel, newLevel = currentLevel + 1,
            goldSpent = price, remainingBankGold = saved.bankGold,
            savedRevision = saved.revision
        };
        message = "PURCHASE_OK: " + upgradeId + " Lv." + receipt.newLevel +
                  " cost " + price + " Bank Gold; revision=" + saved.revision;
        return true;
    }

    private static bool IsNoSave(string value)
    {
        return !string.IsNullOrEmpty(value) && value.StartsWith("NO_SAVE", StringComparison.Ordinal);
    }

    private static int ReadLevel(List<MS5A1UpgradeEntry> entries, string id)
    {
        int level = 0;
        if (entries == null) return -1;
        foreach (MS5A1UpgradeEntry entry in entries)
        {
            if (entry == null) return -1;
            if (string.Equals(entry.id, id, StringComparison.Ordinal)) level = entry.level;
        }
        return level;
    }
}
