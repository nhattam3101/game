using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

// Independent, versioned statistics sidecar. No edits to MS5A1SaveData or game save files.
[Serializable]
public sealed class MS5D3StatsData
{
    public int version = 1;
    public int slot;
    public int revision;
    public int victories;
    public long totalVictoryGold;
    public int shadowTitanKills;
    public int graveWardenKills;
    public string lastVictoryUtc = "";
    public string lastBossUtc = "";
    // All committed event IDs: idempotent across scene reloads, retries and crashes.
    public List<string> committedEventIds = new List<string>();
}

[Serializable]
internal sealed class MS5D3StatsEnvelope
{
    public string magic;
    public string checksum;
    public string payload;
}

public static class MS5D3StatsStore
{
    private const string Magic = "MS_STATS_5D3_V1";
    private const int MaxBytes = 2 * 1024 * 1024;
    public static string DirectoryPath
    {
        get { return Path.Combine(Application.persistentDataPath, "MonsterSurvival", "ProfileStats5D3"); }
    }

    public static string GetPath(int slot)
    {
        EnsureSlot(slot);
        return Path.Combine(DirectoryPath, "stats_" + slot.ToString("00") + ".json");
    }

    private static void EnsureSlot(int slot)
    {
        if (slot < 1 || slot > 3) throw new ArgumentOutOfRangeException("slot");
    }

    public static bool TryRead(int slot, out MS5D3StatsData data, out string message, out bool backupOnly)
    {
        return TryReadAt(DirectoryPath, slot, out data, out message, out backupOnly);
    }

    private static string PathAt(string folder, int slot)
    {
        return Path.Combine(folder, "stats_" + slot.ToString("00") + ".json");
    }

    private static bool TryReadAt(string folder, int slot, out MS5D3StatsData data, out string message, out bool backupOnly)
    {
        EnsureSlot(slot);
        data = null;
        backupOnly = false;
        string path = PathAt(folder, slot);
        string primaryError;
        if (VerifyFile(path, slot, out data, out primaryError))
        {
            message = "VALID";
            return true;
        }
        MS5D3StatsData backup;
        string backupError;
        if (VerifyFile(path + ".bak", slot, out backup, out backupError))
        {
            data = backup;
            backupOnly = true;
            message = "BACKUP ONLY — restore manually before recording more statistics";
            return true;
        }
        message = !File.Exists(path) && !File.Exists(path + ".bak")
            ? "NO_STATS — no 5D.3 counters have been recorded yet"
            : "INVALID_STATS — primary: " + primaryError + "; backup: " + backupError;
        return false;
    }

    public static bool TryRecordVictory(int slot, string runId, int bankedGold, out string message)
    {
        return TryRecordAt(DirectoryPath, slot, "victory:" + runId, 0, bankedGold, out message);
    }

    public static bool TryRecordBossKill(int slot, string bossId, string eventId, out string message)
    {
        int boss = bossId == "achievement.shadow_titan" ? 1 :
                   bossId == "achievement.grave_warden" ? 2 : 0;
        if (boss == 0) { message = "Unknown boss ID"; return false; }
        return TryRecordAt(DirectoryPath, slot, "boss:" + eventId, boss, 0, out message);
    }

    // boss=0 victory; boss=1 Shadow Titan; boss=2 Grave Warden.
    private static bool TryRecordAt(string folder, int slot, string eventId, int boss, int gold, out string message)
    {
        EnsureSlot(slot);
        if (string.IsNullOrEmpty(eventId) || eventId.Length > 180 || gold < 0)
        { message = "INVALID_EVENT"; return false; }
        MS5D3StatsData data;
        bool fromBackup;
        string status;
        if (!TryReadAt(folder, slot, out data, out status, out fromBackup))
        {
            if (!status.StartsWith("NO_STATS", StringComparison.Ordinal))
            { message = "READ BLOCKED: " + status; return false; }
            data = new MS5D3StatsData { slot = slot };
        }
        if (fromBackup) { message = "BACKUP ONLY: writes blocked"; return false; }
        if (data.committedEventIds == null) data.committedEventIds = new List<string>();
        if (data.committedEventIds.Contains(eventId))
        { message = "ALREADY_RECORDED"; return true; }
        if (data.revision == int.MaxValue || (boss == 0 && data.victories == int.MaxValue) ||
            (boss == 1 && data.shadowTitanKills == int.MaxValue) ||
            (boss == 2 && data.graveWardenKills == int.MaxValue) ||
            data.totalVictoryGold > long.MaxValue - gold)
        { message = "COUNTER_OVERFLOW"; return false; }
        data.committedEventIds.Add(eventId);
        if (boss == 0)
        {
            data.victories++;
            data.totalVictoryGold += gold;
            data.lastVictoryUtc = DateTime.UtcNow.ToString("o");
        }
        else
        {
            if (boss == 1) data.shadowTitanKills++;
            else data.graveWardenKills++;
            data.lastBossUtc = DateTime.UtcNow.ToString("o");
        }
        data.revision++;
        return TryWriteAt(folder, slot, data, out message);
    }

    private static bool TryWriteAt(string folder, int slot, MS5D3StatsData data, out string message)
    {
        string path = PathAt(folder, slot);
        string temp = path + ".tmp";
        try
        {
            Directory.CreateDirectory(folder);
            string payload = JsonUtility.ToJson(data);
            MS5D3StatsEnvelope envelope = new MS5D3StatsEnvelope
            {
                magic = Magic, payload = payload, checksum = Hash(payload)
            };
            string text = JsonUtility.ToJson(envelope, true);
            if (Encoding.UTF8.GetByteCount(text) > MaxBytes)
            { message = "STATS_FILE_TOO_LARGE"; return false; }
            File.WriteAllText(temp, text, new UTF8Encoding(false));
            MS5D3StatsData staged;
            string reason;
            if (!VerifyFile(temp, slot, out staged, out reason))
            { message = "TEMP_VERIFICATION_FAILED: " + reason; return false; }
            if (File.Exists(path))
            {
                try { File.Replace(temp, path, path + ".bak"); }
                catch (PlatformNotSupportedException) { FallbackReplace(temp, path); }
                catch (IOException) { FallbackReplace(temp, path); }
            }
            else File.Move(temp, path);
            message = "SAVED revision=" + data.revision;
            return true;
        }
        catch (Exception ex)
        {
            message = "WRITE_FAILED: " + ex.Message;
            return false;
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { }
        }
    }

    private static void FallbackReplace(string temp, string path)
    {
        // The verified former primary becomes the backup before overwrite.
        File.Copy(path, path + ".bak", true);
        File.Copy(temp, path, true);
        File.Delete(temp);
    }

    private static bool VerifyFile(string path, int slot, out MS5D3StatsData data, out string reason)
    {
        data = null;
        reason = "MISSING";
        try
        {
            if (!File.Exists(path)) return false;
            FileInfo fi = new FileInfo(path);
            if (fi.Length <= 0 || fi.Length > MaxBytes) { reason = "INVALID_SIZE"; return false; }
            MS5D3StatsEnvelope envelope = JsonUtility.FromJson<MS5D3StatsEnvelope>(File.ReadAllText(path, Encoding.UTF8));
            if (envelope == null || envelope.magic != Magic || string.IsNullOrEmpty(envelope.payload) ||
                !string.Equals(envelope.checksum, Hash(envelope.payload), StringComparison.OrdinalIgnoreCase))
            { reason = "CHECKSUM_OR_FORMAT_INVALID"; return false; }
            MS5D3StatsData obj = JsonUtility.FromJson<MS5D3StatsData>(envelope.payload);
            if (obj == null || obj.version != 1 || obj.slot != slot || obj.revision < 0 ||
                obj.victories < 0 || obj.totalVictoryGold < 0 || obj.shadowTitanKills < 0 || obj.graveWardenKills < 0 ||
                obj.committedEventIds == null || obj.committedEventIds.Count != obj.revision)
            { reason = "PAYLOAD_INVALID"; return false; }
            data = obj;
            reason = "VALID";
            return true;
        }
        catch (Exception ex) { reason = ex.Message; return false; }
    }

    private static string Hash(string text)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
            StringBuilder sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    // Runs only inside a generated TEMP directory, never 5A save files or real 5D.3 profile stats.
    public static string RunIsolatedQa()
    {
        string folder = Path.Combine(Application.temporaryCachePath, "MS5D3_QA_" + Guid.NewGuid().ToString("N"));
        int pass = 0;
        StringBuilder log = new StringBuilder();
        try
        {
            string message;
            MS5D3StatsData data;
            bool backup;
            bool t = !TryReadAt(folder, 1, out data, out message, out backup) && message.StartsWith("NO_STATS", StringComparison.Ordinal);
            Check(t, "Empty stats is not mistaken for zero history", log, ref pass);
            Check(TryRecordAt(folder, 1, "victory:run-A", 0, 123, out message), "Record first victory", log, ref pass);
            Check(TryRecordAt(folder, 1, "victory:run-A", 0, 123, out message) && message == "ALREADY_RECORDED", "Duplicate victory ignored", log, ref pass);
            Check(TryRecordAt(folder, 1, "boss:run-A:1", 1, 0, out message), "Record Titan defeat", log, ref pass);
            Check(TryRecordAt(folder, 1, "boss:run-A:2", 2, 0, out message), "Record Warden defeat", log, ref pass);
            Check(TryReadAt(folder, 1, out data, out message, out backup) && data.victories == 1 &&
                  data.totalVictoryGold == 123 && data.shadowTitanKills == 1 && data.graveWardenKills == 1,
                  "Counters persist correctly", log, ref pass);
            Check(data != null && data.revision == 3 && File.Exists(PathAt(folder, 1) + ".bak"), "Revision & backup exist", log, ref pass);
            File.WriteAllText(PathAt(folder, 1), "CORRUPT QA FILE");
            Check(TryReadAt(folder, 1, out data, out message, out backup) && backup, "Corrupt primary loads verified backup", log, ref pass);
            Check(!TryRecordAt(folder, 1, "victory:run-B", 0, 7, out message) && message.StartsWith("BACKUP ONLY", StringComparison.Ordinal),
                  "Backup-only prevents writes", log, ref pass);
            log.AppendLine("RESULT: " + pass + "/9 PASS (isolated temp only)");
        }
        catch (Exception ex) { log.AppendLine("QA ABORTED: " + ex); }
        finally
        {
            try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
            catch (Exception ex) { log.AppendLine("Temp cleanup warning: " + ex.Message); }
        }
        return log.ToString();
    }

    private static void Check(bool ok, string label, StringBuilder log, ref int pass)
    {
        log.AppendLine((ok ? "PASS " : "FAIL ") + label);
        if (ok) pass++;
    }
}
