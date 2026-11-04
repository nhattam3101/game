using System;
using System.Collections.Generic;

// 5C.1: Read-only snapshots and atomic Gold-for-unlock transactions.
// IMPORTANT: This service NEVER edits Inventory, Equipment, Map Selection, or gameplay spawns.
// The Save Core (5A.1) handles checksum, backup and disk revision validation.
public static class MS5C1UnlockService
{
    public sealed class UnlockRow
    {
        public string id;
        public string title;
        public MS5C1UnlockCategory category;
        public int price;
        public bool unlocked;
        public bool achievementOnly;
    }

    public sealed class Snapshot
    {
        public int slot;
        public int revision;
        public int bankGold;
        public bool isNew;
        public bool fromBackup;
        public readonly List<UnlockRow> rows = new List<UnlockRow>();
        public bool IsUnlocked(string id)
        {
            if (MS5C1UnlockCatalog.IsStarter(id)) return true;
            foreach (UnlockRow row in rows)
                if (string.Equals(row.id, id, StringComparison.Ordinal)) return row.unlocked;
            return false;
        }
    }

    public sealed class UnlockReceipt
    {
        public int slot;
        public string contentId;
        public int goldSpent;
        public int bankGold;
        public int revision;
    }

    public static bool TryGetSnapshot(int slot, out Snapshot snapshot, out string message, bool qa = false)
    {
        snapshot = null;
        if (slot < 1 || slot > 3) { message = "INVALID_SLOT"; return false; }
        MS5A1SaveData data;
        bool recovered;
        string loadMessage;
        bool loaded = MS5A1SaveService.TryLoad(slot, out data, out loadMessage, out recovered, qa);
        bool fresh = !loaded && IsNoSave(loadMessage);
        if (!loaded && !fresh) { message = "LOAD_BLOCKED: " + loadMessage; return false; }
        if (fresh) data = MS5A1SaveService.CreateFresh(slot, qa);
        if (data.unlockedContentIds == null) { message = "INVALID_UNLOCK_LIST"; return false; }
        snapshot = new Snapshot {
            slot = slot, revision = data.revision, bankGold = data.bankGold,
            isNew = fresh, fromBackup = recovered
        };
        foreach (MS5C1UnlockDefinition def in MS5C1UnlockCatalog.GetAll())
        {
            snapshot.rows.Add(new UnlockRow {
                id = def.id, title = def.title, category = def.category, price = def.price,
                achievementOnly = def.achievementOnly,
                unlocked = def.starterUnlocked || data.unlockedContentIds.Contains(def.id)
            });
        }
        message = fresh ? "NEW_PROFILE_READ_ONLY" :
                  recovered ? "RECOVERED_BACKUP_READ_ONLY" : "SAVED_PROFILE";
        return true;
    }

    public static bool TryPurchase(int slot, string contentId, int expectedRevision,
                                   out UnlockReceipt receipt, out string message, bool qa = false)
    {
        receipt = null;
        if (slot < 1 || slot > 3) { message = "INVALID_SLOT"; return false; }
        if (expectedRevision < 0) { message = "INVALID_REVISION"; return false; }
        MS5C1UnlockDefinition def;
        if (!MS5C1UnlockCatalog.TryGet(contentId, out def)) { message = "UNKNOWN_CONTENT"; return false; }
        if (def.starterUnlocked) { message = "ALREADY_UNLOCKED_STARTER"; return false; }
        if (def.achievementOnly) { message = "ACHIEVEMENT_ONLY"; return false; }
        if (def.price <= 0) { message = "INVALID_PRICE"; return false; }

        MS5A1SaveData data;
        if (!TryLoadWritable(slot, qa, expectedRevision, out data, out message)) return false;
        if (data.unlockedContentIds.Contains(contentId)) { message = "ALREADY_UNLOCKED"; return false; }
        if (data.bankGold < def.price) { message = "NOT_ENOUGH_BANK_GOLD"; return false; }

        // Only change these two fields on the verified in-memory DTO; all existing
        // 5A.3 Inventory/Equipment and 5B permanent upgrades remain untouched.
        data.bankGold -= def.price;
        data.unlockedContentIds.Add(contentId);
        return Persist(slot, contentId, def.price, data, qa, out receipt, out message);
    }

    // Authorized gameplay integrations (5C.3) may call this after a verified Boss kill.
    // This core does not auto-detect kills and does not expose a free unlock UI button.
    public static bool TryGrantAchievement(int slot, string contentId, int expectedRevision,
                                           out UnlockReceipt receipt, out string message, bool qa = false)
    {
        receipt = null;
        MS5C1UnlockDefinition def;
        if (!MS5C1UnlockCatalog.TryGet(contentId, out def) || !def.achievementOnly)
        { message = "NOT_AN_ACHIEVEMENT"; return false; }
        MS5A1SaveData data;
        if (!TryLoadWritable(slot, qa, expectedRevision, out data, out message)) return false;
        if (data.unlockedContentIds.Contains(contentId)) { message = "ALREADY_UNLOCKED"; return false; }
        data.unlockedContentIds.Add(contentId);
        return Persist(slot, contentId, 0, data, qa, out receipt, out message);
    }

    private static bool TryLoadWritable(int slot, bool qa, int expectedRevision,
                                        out MS5A1SaveData data, out string message)
    {
        data = null;
        if (slot < 1 || slot > 3) { message = "INVALID_SLOT"; return false; }
        if (expectedRevision < 0) { message = "INVALID_REVISION"; return false; }
        bool fromBackup;
        string reason;
        if (!MS5A1SaveService.TryLoad(slot, out data, out reason, out fromBackup, qa))
        {
            message = IsNoSave(reason) ? "NO_BANK_GOLD_NEW_PROFILE" : "LOAD_BLOCKED: " + reason;
            return false;
        }
        if (fromBackup) { message = "BACKUP_ONLY_RESTORE_FIRST"; return false; }
        if (data.revision != expectedRevision) { message = "STALE_REVISION"; return false; }
        if (data.unlockedContentIds == null) { message = "INVALID_UNLOCK_LIST"; return false; }
        message = "OK"; // Required for the successful out-message path (CS0177).
        return true;
    }

    private static bool Persist(int slot, string id, int spent, MS5A1SaveData data, bool qa,
                                out UnlockReceipt receipt, out string message)
    {
        receipt = null;
        MS5A1SaveData saved;
        string result;
        if (!MS5A1SaveService.TrySave(slot, data, out saved, out result, qa))
        { message = "UNLOCK_SAVE_FAILED: " + result; return false; }
        receipt = new UnlockReceipt {
            slot = slot, contentId = id, goldSpent = spent,
            bankGold = saved.bankGold, revision = saved.revision
        };
        message = "UNLOCK_OK " + id + " revision=" + saved.revision;
        return true;
    }

    private static bool IsNoSave(string value)
    {
        return !string.IsNullOrEmpty(value) && value.StartsWith("NO_SAVE", StringComparison.Ordinal);
    }
}
