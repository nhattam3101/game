using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

// Independent, READ-ONLY inspection of individual primary/back-up files.
// Does not change 5A.1 SaveService or the JSON format.
public static class MS5A4SaveRecovery
{
    public enum FileState { Missing, Valid, Invalid, WrongVersion }
    [Serializable] private sealed class Envelope
    {
        public string magic;
        public int formatVersion;
        public string sha256;
        public string payloadJson;
    }
    public sealed class Report
    {
        public int slot;
        public FileState primaryState, backupState;
        public string primaryDetail, backupDetail;
        public MS5A1SaveData primary, backup;
        public bool CanRestore { get { return backupState == FileState.Valid && primaryState != FileState.WrongVersion; } }
    }
    private const int MaxBytes = 2 * 1024 * 1024;
    private const string Magic = "MONSTERSURVIVAL_SAVE_5A1";
    public static Report InspectSlot(int slot)
    {
        if (slot < 1 || slot > 3) throw new ArgumentOutOfRangeException("slot");
        Report r = new Report(); r.slot = slot;
        string expected = "slot_" + slot.ToString("00");
        r.primaryState = InspectFile(MS5A1SaveService.GetSavePath(slot), expected, out r.primary, out r.primaryDetail);
        r.backupState = InspectFile(MS5A1SaveService.GetBackupPath(slot), expected, out r.backup, out r.backupDetail);
        return r;
    }
    public static FileState InspectFile(string path, string expectedId, out MS5A1SaveData data, out string description)
    {
        data = null; description = "Missing";
        try
        {
            if (!File.Exists(path)) return FileState.Missing;
            FileInfo info = new FileInfo(path);
            if (info.Length <= 0 || info.Length > MaxBytes) { description = "Invalid file length"; return FileState.Invalid; }
            string text = File.ReadAllText(path, Encoding.UTF8);
            Envelope env = JsonUtility.FromJson<Envelope>(text);
            if (env == null || env.magic != Magic) { description = "Invalid save header"; return FileState.Invalid; }
            if (env.formatVersion != 1) { description = "Unsupported format " + env.formatVersion; return FileState.WrongVersion; }
            if (string.IsNullOrEmpty(env.payloadJson) || string.IsNullOrEmpty(env.sha256))
            { description = "Empty payload/checksum"; return FileState.Invalid; }
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(env.payloadJson));
                StringBuilder hex = new StringBuilder(64);
                foreach (byte b in bytes) hex.Append(b.ToString("x2"));
                if (!string.Equals(hex.ToString(), env.sha256, StringComparison.OrdinalIgnoreCase))
                { description = "SHA256 mismatch"; return FileState.Invalid; }
            }
            MS5A1SaveData candidate = JsonUtility.FromJson<MS5A1SaveData>(env.payloadJson);
            if (candidate == null) { description = "Null payload"; return FileState.Invalid; }
            if (candidate.schemaVersion != MS5A1SaveData.SchemaVersion)
            { description = "Unsupported schema " + candidate.schemaVersion; return FileState.WrongVersion; }
            if (candidate.profileId != expectedId || candidate.revision < 0 || candidate.bankGold < 0 ||
                string.IsNullOrEmpty(candidate.createdUtc))
            { description = "Invalid profile ID/revision/gold/time"; return FileState.Invalid; }
            if (candidate.unlockedContentIds == null || candidate.permanentUpgrades == null ||
                candidate.unlockedContentIds.Count > 1024 || candidate.permanentUpgrades.Count > 1024)
            { description = "Invalid progression arrays"; return FileState.Invalid; }
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in candidate.unlockedContentIds)
            {
                if (!ValidId(id) || !ids.Add(id)) { description = "Invalid/duplicate unlocked ID"; return FileState.Invalid; }
            }
            ids.Clear();
            foreach (MS5A1UpgradeEntry entry in candidate.permanentUpgrades)
            {
                if (entry == null || !ValidId(entry.id) || entry.level < 0 || !ids.Add(entry.id))
                { description = "Invalid/duplicate upgrade"; return FileState.Invalid; }
            }
            // Older profiles may not have an Inventory snapshot. When present, require exact 20 + 3 shape.
            if (candidate.inventorySnapshotPresent)
            {
                if (candidate.inventorySlots == null || candidate.inventorySlots.Count != 20 ||
                    candidate.equippedItems == null || candidate.equippedItems.Count != 3)
                { description = "Invalid Inventory/Equipment snapshot length"; return FileState.Invalid; }
                for (int i = 0; i < 20; i++)
                {
                    MS5A3SavedStack entry = candidate.inventorySlots[i];
                    if (entry == null || entry.quantity < 0 ||
                        (entry.quantity == 0 && !string.IsNullOrEmpty(entry.itemId)) ||
                        (entry.quantity > 0 && !ValidId(entry.itemId)))
                    { description = "Invalid inventory slot " + i; return FileState.Invalid; }
                }
                for (int i = 0; i < 3; i++)
                {
                    MS5A3SavedEquipped entry = candidate.equippedItems[i];
                    if (entry == null || entry.type != i ||
                        (!string.IsNullOrEmpty(entry.itemId) && !ValidId(entry.itemId)))
                    { description = "Invalid equipment slot " + i; return FileState.Invalid; }
                }
            }
            data = candidate;
            description = "Verified SHA-256, rev " + candidate.revision;
            return FileState.Valid;
        }
        catch (Exception e)
        {
            description = e.GetType().Name + ": " + e.Message;
            return FileState.Invalid;
        }
    }
    private static bool ValidId(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 96) return false;
        foreach (char c in value)
            if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.')) return false;
        return true;
    }
    public static string FormatTime(string utc)
    {
        DateTime value;
        if (string.IsNullOrEmpty(utc) || !DateTime.TryParse(utc, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out value)) return "Chưa lưu";
        return value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }
    public static int ItemCount(MS5A1SaveData d)
    {
        if (d == null || !d.inventorySnapshotPresent || d.inventorySlots == null) return -1;
        int count = 0;
        foreach (MS5A3SavedStack item in d.inventorySlots) if (item != null && item.quantity > 0) count++;
        return count;
    }
    public static int GearCount(MS5A1SaveData d)
    {
        if (d == null || !d.inventorySnapshotPresent || d.equippedItems == null) return -1;
        int count = 0;
        foreach (MS5A3SavedEquipped item in d.equippedItems) if (item != null && !string.IsNullOrEmpty(item.itemId)) count++;
        return count;
    }
    // Manual archive: never alters the active .json or .bak and never destroys an earlier archive.
    public static bool TryArchiveSlot(int slot, out string message)
    {
        if (slot < 1 || slot > 3) { message = "Invalid slot"; return false; }
        MS5A1SaveData data;
        string reason;
        string path = MS5A1SaveService.GetSavePath(slot);
        if (InspectFile(path, "slot_" + slot.ToString("00"), out data, out reason) != FileState.Valid)
        { message = "Only a verified primary save can be archived: " + reason; return false; }
        try
        {
            string target = UniqueArchiveName(path, "manual");
            File.Copy(path, target, false);
            MS5A1SaveData copy; string detail;
            if (InspectFile(target, data.profileId, out copy, out detail) != FileState.Valid)
                throw new IOException("Archive verification failed: " + detail);
            message = "Snapshot created: " + target; return true;
        }
        catch (Exception e) { message = "ARCHIVE FAILED: " + e.Message; return false; }
    }
    // Real-slot entry point: requires an explicit two-stage UI confirmation. Never called automatically.
    public static bool TryRestoreSlotBackup(int slot, out string message)
    {
        if (slot < 1 || slot > 3) { message = "Invalid slot"; return false; }
        return TryRestoreVerifiedBackup(MS5A1SaveService.GetSavePath(slot),
            MS5A1SaveService.GetBackupPath(slot), "slot_" + slot.ToString("00"), out message);
    }
    // Also used by the isolated Editor QA against test-only paths.
    public static bool TryRestoreVerifiedBackup(string primary, string backup, string expectedId, out string message)
    {
        MS5A1SaveData backupData, primaryData;
        string backupReason, primaryReason;
        FileState source = InspectFile(backup, expectedId, out backupData, out backupReason);
        FileState target = InspectFile(primary, expectedId, out primaryData, out primaryReason);
        if (source != FileState.Valid) { message = "RESTORE BLOCKED: backup not verified: " + backupReason; return false; }
        if (target == FileState.WrongVersion)
        { message = "RESTORE BLOCKED: primary belongs to another save version. Preserve manually."; return false; }
        string staging = primary + ".5a4.tmp";
        string oldArchive = null;
        try
        {
            string directory = Path.GetDirectoryName(primary);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            if (File.Exists(primary))
            {
                oldArchive = UniqueArchiveName(primary, "before_restore");
                File.Copy(primary, oldArchive, false); // preserve even corrupted bytes
            }
            File.Copy(backup, staging, true);
            MS5A1SaveData staged; string stageReason;
            if (InspectFile(staging, expectedId, out staged, out stageReason) != FileState.Valid)
                throw new IOException("Staging verification failed: " + stageReason);
            if (File.Exists(primary))
            {
                try { File.Replace(staging, primary, null); }
                catch (PlatformNotSupportedException) { File.Copy(staging, primary, true); }
                catch (IOException) { File.Copy(staging, primary, true); }
            }
            else File.Move(staging, primary);
            MS5A1SaveData restored; string verify;
            if (InspectFile(primary, expectedId, out restored, out verify) != FileState.Valid)
                throw new IOException("Restored save failed final verification: " + verify);
            message = "RESTORED .bak revision " + restored.revision +
                (oldArchive == null ? " (no previous primary)" : ". Previous primary archived: " + oldArchive) +
                ". Existing .bak kept unchanged.";
            return true;
        }
        catch (Exception e) { message = "RESTORE FAILED: " + e.Message + ". The .bak file was not altered."; return false; }
        finally { try { if (File.Exists(staging)) File.Delete(staging); } catch (Exception) { } }
    }
    private static string UniqueArchiveName(string primary, string purpose)
    {
        string folder = Path.Combine(Path.GetDirectoryName(primary), "5A4_ManualHistory");
        Directory.CreateDirectory(folder);
        string stem = Path.GetFileNameWithoutExtension(primary);
        string prefix = Path.Combine(folder, stem + "_" + purpose + "_" +
            DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture));
        string candidate = prefix + ".json";
        for (int i = 1; File.Exists(candidate); i++) candidate = prefix + "_" + i + ".json";
        return candidate;
    }
}
