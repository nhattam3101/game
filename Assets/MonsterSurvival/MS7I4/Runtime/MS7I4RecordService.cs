using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// 7I.4 separate profile-scoped Endless history (never a normal Victory save).
// Bank Gold is granted ONLY through an explicit MainMenu 5A1 TrySave, with a per-run marker.
[Serializable]
public sealed class MS7I4RewardTicket
{
    public string runId;
    public int clearedWave;
    public int bossesDefeated;
    public int amount;
    public string endedUtc;
}

[Serializable]
public sealed class MS7I4ProfileRecord
{
    public int schema = 1;
    public int slot;
    public string profileId;
    public int bestReachedWave;
    public int bestClearedWave;
    public int gamesCompleted;
    public int totalEndlessBossesDefeated;
    public string lastEndedUtc;
    public string lastRunId;
    public List<MS7I4RewardTicket> pending = new List<MS7I4RewardTicket>();
}

[Serializable]
internal sealed class MS7I4DiskEnvelope
{
    public string magic = "MS7I4_RECORD_1";
    public string sha256;
    public string payload;
}

public static class MS7I4RecordService
{
    public const string ClaimPrefix = "endless.7i4.claim.";
    private const string Magic = "MS7I4_RECORD_1";
    private static bool writing;
    private static bool claiming;

    public static int RewardFor(int clearedWave, int bossKills)
    {
        int completed = Mathf.Clamp(clearedWave - 5, 0, 10);
        int bosses = Mathf.Clamp(bossKills, 0, 4);
        return completed * 10 + bosses * 25; // Hard cap: 200 Gold per verified ended run.
    }

    public static bool ValidRunId(string s)
    {
        if (s == null || s.Length != 32) return false;
        foreach (char c in s)
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
        return true;
    }

    public static string ClaimMarker(string runId) { return ClaimPrefix + runId; }

    public static bool TryReadSaveIdentity(int slot, out MS5A1SaveData data, out string reason)
    {
        data = null; reason = "INVALID PROFILE";
        if (slot < 1 || slot > 3 || MS5A2GoldBank.SelectedSlot != slot)
        { reason = "Profile slot changed or not selected"; return false; }
        bool recovered = false;
        try
        {
            if (!MS5A1SaveService.TryLoad(slot, out data, out reason, out recovered) ||
                data == null || recovered || string.IsNullOrEmpty(data.profileId))
            { reason = "5A Save missing, unsafe, or backup-only: " + reason; return false; }
            reason = "VERIFIED PROFILE";
            return true;
        }
        catch (Exception e) { reason = "5A load exception: " + e.Message; return false; }
    }

    private static string PathFor(int slot)
    {
        return Path.Combine(Path.Combine(Application.persistentDataPath, "MS7I4"),
            "endless_profile_" + slot.ToString("00") + ".json");
    }

    private static string Hash(string value)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] b = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            StringBuilder hex = new StringBuilder(64);
            for (int i = 0; i < b.Length; i++) hex.Append(b[i].ToString("x2"));
            return hex.ToString();
        }
    }

    private static MS7I4ProfileRecord Empty(int slot, string profileId)
    {
        return new MS7I4ProfileRecord { slot = slot, profileId = profileId,
            pending = new List<MS7I4RewardTicket>() };
    }

    private static bool Validate(MS7I4ProfileRecord r, int slot, string profileId, out string reason)
    {
        reason = "INVALID RECORD";
        if (r == null || r.schema != 1 || r.slot != slot || r.profileId != profileId ||
            r.pending == null || r.pending.Count > 64 || r.bestReachedWave < 0 ||
            r.bestReachedWave > 9999 || r.bestClearedWave < 0 ||
            r.bestClearedWave > r.bestReachedWave || r.gamesCompleted < 0 ||
            r.totalEndlessBossesDefeated < 0) return false;
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (MS7I4RewardTicket t in r.pending)
        {
            if (t == null || !ValidRunId(t.runId) || !ids.Add(t.runId) ||
                t.clearedWave < 6 || t.clearedWave > 9999 ||
                t.bossesDefeated < 0 || t.bossesDefeated > 9999 ||
                t.amount < 0 || t.amount != RewardFor(t.clearedWave, t.bossesDefeated))
            { reason = "Invalid or duplicate pending reward"; return false; }
        }
        reason = "VALID";
        return true;
    }

    // Non-mutating read. It MUST NOT auto-fallback from a corrupt primary to a stale backup.
    public static bool TryRead(int slot, string profileId, out MS7I4ProfileRecord record, out string reason)
    {
        record = null; reason = "BLOCKED";
        if (slot < 1 || slot > 3 || string.IsNullOrEmpty(profileId)) return false;
        string path = PathFor(slot);
        if (!File.Exists(path))
        {
            record = Empty(slot, profileId);
            reason = "NEW EMPTY RECORD (NO DISK WRITE)";
            return true;
        }
        try
        {
            if (new FileInfo(path).Length > 250000) { reason = "Oversized record; inspect manually"; return false; }
            MS7I4DiskEnvelope envelope = JsonUtility.FromJson<MS7I4DiskEnvelope>(File.ReadAllText(path, Encoding.UTF8));
            if (envelope == null || envelope.magic != Magic || string.IsNullOrEmpty(envelope.payload) ||
                !string.Equals(envelope.sha256, Hash(envelope.payload), StringComparison.Ordinal))
            { reason = "Checksum mismatch / corrupt record; no writes"; return false; }
            MS7I4ProfileRecord parsed = JsonUtility.FromJson<MS7I4ProfileRecord>(envelope.payload);
            if (!Validate(parsed, slot, profileId, out reason)) return false;
            record = parsed;
            return true;
        }
        catch (Exception e) { reason = "Read exception: " + e.Message; return false; }
    }

    private static bool TryWrite(int slot, MS7I4ProfileRecord record, out string reason)
    {
        reason = "BLOCKED";
        if (writing) { reason = "Another 7I4 write is active"; return false; }
        if (!Validate(record, slot, record == null ? "" : record.profileId, out reason)) return false;
        writing = true;
        string temp = PathFor(slot) + ".tmp";
        try
        {
            string path = PathFor(slot);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string payload = JsonUtility.ToJson(record);
            MS7I4DiskEnvelope envelope = new MS7I4DiskEnvelope { magic = Magic,
                sha256 = Hash(payload), payload = payload };
            File.WriteAllText(temp, JsonUtility.ToJson(envelope), Encoding.UTF8);
            // Verify temporary file checksum BEFORE replacing the existing record.
            string raw = File.ReadAllText(temp, Encoding.UTF8);
            MS7I4DiskEnvelope verification = JsonUtility.FromJson<MS7I4DiskEnvelope>(raw);
            if (verification == null || verification.sha256 != Hash(verification.payload))
            { reason = "Temp file verification failed"; return false; }
            if (File.Exists(path))
            {
                string backup = path + ".bak";
                // Do not silently delete a corrupt primary or attempt recovery over it.
                MS7I4ProfileRecord existing; string read;
                if (!TryRead(slot, record.profileId, out existing, out read))
                { reason = "Existing history invalid: " + read; return false; }
                File.Copy(path, backup, true);
                // Replace is atomic on supported local file systems; a failure preserves .bak.
                File.Replace(temp, path, backup);
            }
            else File.Move(temp, path);
            reason = "7I4 RECORD WRITTEN; NOT A 5A VICTORY";
            return true;
        }
        catch (Exception e) { reason = "Record write failed: " + e.Message; return false; }
        finally { writing = false; try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
    }

    // Called ONCE only after a real Endless GameOver (not Restart, Quit or Victory).
    public static bool TryRecordDefeat(int slot, string boundProfileId, string runId,
        int reachedWave, int clearedWave, int bossKills, out string result)
    {
        result = "BLOCKED";
        if (!Application.isPlaying || SceneManager.GetActiveScene().name != "SampleScene" ||
            !ValidRunId(runId) || reachedWave < 6 || reachedWave > 9999 ||
            clearedWave < 5 || clearedWave > reachedWave || bossKills < 0 ||
            bossKills > 9999 || MS5A2GoldBank.SelectedSlot != slot)
        { result = "Not a valid Endless defeat capture"; return false; }
        MS5A1SaveData save;
        if (!TryReadSaveIdentity(slot, out save, out result) || save.profileId != boundProfileId)
        { result = "Profile identity changed during match"; return false; }
        MS7I4ProfileRecord record;
        if (!TryRead(slot, boundProfileId, out record, out result)) return false;
        if (record.lastRunId == runId || record.pending.Exists(x => x.runId == runId))
        { result = "Run already recorded; duplicate blocked"; return false; }
        int reward = RewardFor(clearedWave, bossKills);
        if (reward > 0 && record.pending.Count >= 64)
        { result = "Pending queue full (64); claim existing rewards before another run"; return false; }
        record.bestReachedWave = Math.Max(record.bestReachedWave, reachedWave);
        record.bestClearedWave = Math.Max(record.bestClearedWave, clearedWave);
        if (record.gamesCompleted == int.MaxValue || record.totalEndlessBossesDefeated > int.MaxValue - bossKills)
        { result = "Record counter overflow"; return false; }
        record.gamesCompleted++;
        record.totalEndlessBossesDefeated += bossKills;
        record.lastRunId = runId;
        record.lastEndedUtc = DateTime.UtcNow.ToString("O");
        if (reward > 0)
            record.pending.Add(new MS7I4RewardTicket { runId = runId, clearedWave = clearedWave,
                bossesDefeated = bossKills, amount = reward, endedUtc = record.lastEndedUtc });
        return TryWrite(slot, record, out result);
    }

    // Read-only pending preview: excludes tickets already claimed in 5A Save.
    public static bool TrySnapshot(int slot, out MS7I4ProfileRecord record,
        out int pendingCount, out int pendingGold, out string result)
    {
        record = null; pendingCount = 0; pendingGold = 0;
        MS5A1SaveData save;
        if (!TryReadSaveIdentity(slot, out save, out result)) return false;
        if (!TryRead(slot, save.profileId, out record, out result)) return false;
        foreach (MS7I4RewardTicket ticket in record.pending)
        {
            if (save.unlockedContentIds != null && save.unlockedContentIds.Contains(ClaimMarker(ticket.runId))) continue;
            pendingCount++;
            pendingGold += ticket.amount;
        }
        return true;
    }

    // Explicit MainMenu claim. Saves Bank Gold and one claim ID together in 5A1.
    // Never triggers the normal Victory event, 5A3 PersistentRun or 7H Quest observers.
    public static bool TryClaimOne(int slot, out string result)
    {
        result = "BLOCKED";
        if (claiming) { result = "Claim already in progress"; return false; }
        if (!Application.isPlaying || SceneManager.GetActiveScene().name != "MainMenu" ||
            slot < 1 || slot > 3 || MS5A2GoldBank.SelectedSlot != slot)
        { result = "Claim in MainMenu Play Mode with selected Profile only"; return false; }
        claiming = true;
        try
        {
            MS5A1SaveData save;
            if (!TryReadSaveIdentity(slot, out save, out result)) return false;
            MS7I4ProfileRecord record;
            if (!TryRead(slot, save.profileId, out record, out result)) return false;
            if (save.unlockedContentIds == null) { result = "Save has no marker list"; return false; }
            MS7I4RewardTicket next = null;
            foreach (MS7I4RewardTicket ticket in record.pending)
                if (!save.unlockedContentIds.Contains(ClaimMarker(ticket.runId))) { next = ticket; break; }
            if (next == null) { result = "No unclaimed Endless rewards"; return false; }
            if (save.bankGold < 0 || (long)save.bankGold + next.amount > int.MaxValue)
            { result = "Gold overflow / invalid bank Gold"; return false; }
            string marker = ClaimMarker(next.runId);
            save.bankGold += next.amount;
            save.unlockedContentIds.Add(marker);
            MS5A1SaveData saved;
            string transaction;
            if (!MS5A1SaveService.TrySave(slot, save, out saved, out transaction) || saved == null)
            { result = "5A SAVE REJECTED: " + transaction; return false; }
            // A failed readback is NOT a license to pay again. Inspect 5A Save history first.
            MS5A1SaveData confirmed;
            string verify;
            if (!TryReadSaveIdentity(slot, out confirmed, out verify) ||
                confirmed.unlockedContentIds == null || !confirmed.unlockedContentIds.Contains(marker) ||
                confirmed.bankGold < save.bankGold)
            { result = "SAVE NEEDS AUDIT; do not press CLAIM again blindly: " + verify; return false; }
            // Best-effort prune after atomic Gold/marker commit. Failure is harmless to Gold idempotency.
            record.pending.RemoveAll(x => x.runId == next.runId);
            string prune;
            bool cleaned = TryWrite(slot, record, out prune);
            result = "CLAIMED +" + next.amount + " Bank Gold / Profile " + slot +
                " / Save Rev " + confirmed.revision + (cleaned ? "" : " / record cleanup pending: " + prune);
            return true;
        }
        catch (Exception e) { result = "CLAIM EXCEPTION: " + e.Message + "; inspect 5A Save before retry"; return false; }
        finally { claiming = false; }
    }

    public static string PureTests()
    {
        int pass = 0, fail = 0;
        Action<bool> ck = b => { if (b) pass++; else fail++; };
        ck(RewardFor(5, 0) == 0);
        ck(RewardFor(6, 0) == 10);
        ck(RewardFor(10, 1) == 75);
        ck(RewardFor(15, 2) == 150);
        ck(RewardFor(40, 40) == 200);
        ck(RewardFor(9999, 0) == 100);
        ck(RewardFor(5, 99) == 100); // hard capped even for invalid fixture
        ck(ValidRunId(new string('a', 32)));
        ck(!ValidRunId("bad|marker"));
        ck(ClaimMarker(new string('b', 32)).Length < 100);
        ck(ClaimPrefix.StartsWith("endless.7i4.claim."));
        ck(ValidRunId(Guid.NewGuid().ToString("N")));
        return "7I.4 PURE RULES PASS=" + pass + " FAIL=" + fail + " (no Scene/Save/disk writes)";
    }
}
