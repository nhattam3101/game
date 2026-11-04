using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

// Save Data CORE 5A.1. Explicit calls only: no automatic scene loading, no PlayerStats edits,
// and no Inventory/Equipment mutation. Works in Editor and standalone Unity players.
public static class MS5A1SaveService
{
    private const string Magic = "MONSTERSURVIVAL_SAVE_5A1";
    private const int FormatVersion = 1;
    private const int MaxFileBytes = 2 * 1024 * 1024;

    public static string SaveDirectory
    {
        get { return Path.Combine(Application.persistentDataPath, "MonsterSurvival", "SaveData"); }
    }

    // QA data is written under DISTINCT file names, NEVER a real profile.
    public static string GetSavePath(int slot, bool qa = false)
    {
        EnsureSlot(slot);
        string name = (qa ? "qa_" : "") + "slot_" + slot.ToString("00") + ".json";
        return Path.Combine(SaveDirectory, name);
    }

    public static string GetBackupPath(int slot, bool qa = false)
    {
        return GetSavePath(slot, qa) + ".bak";
    }

    public static MS5A1SaveData CreateFresh(int slot, bool qa = false)
    {
        EnsureSlot(slot);
        string now = DateTime.UtcNow.ToString("o");
        return new MS5A1SaveData
        {
            profileId = ProfileId(slot, qa),
            createdUtc = now,
            lastSavedUtc = string.Empty,
            revision = 0,
            bankGold = 0,
            unlockedContentIds = new List<string>(),
            permanentUpgrades = new List<MS5A1UpgradeEntry>()
        };
    }

    // True: data verified. recovered=true: loaded valid .bak, primary is still untouched.
    public static bool TryLoad(int slot, out MS5A1SaveData data, out string message,
                               out bool recovered, bool qa = false)
    {
        EnsureSlot(slot);
        data = null;
        recovered = false;
        string path = GetSavePath(slot, qa);
        string backup = GetBackupPath(slot, qa);

        string reason;
        ReadState mainState = TryReadFile(path, ProfileId(slot, qa), out data, out reason);
        if (mainState == ReadState.Valid)
        {
            message = "OK — profile hợp lệ, revision=" + data.revision;
            return true;
        }
        if (mainState == ReadState.WrongVersion)
        {
            data = null;
            message = "KHÔNG TẢI: bản lưu thuộc phiên bản mới/khác. " + reason;
            return false;
        }

        MS5A1SaveData fallback;
        string backupReason;
        ReadState backupState = TryReadFile(backup, ProfileId(slot, qa), out fallback, out backupReason);
        if (backupState == ReadState.Valid)
        {
            data = fallback;
            recovered = true;
            message = "RECOVERED từ file .bak (revision=" + data.revision + "). " +
                      "File chính chưa bị ghi đè; có thể mất tiến trình của lần lưu cuối.";
            return true;
        }
        data = null;
        if (mainState == ReadState.Missing && backupState == ReadState.Missing)
            message = "NO_SAVE — chưa có dữ liệu. Dùng CreateFresh để bắt đầu.";
        else if (backupState == ReadState.WrongVersion)
            message = "KHÔNG TẢI: backup thuộc phiên bản khác. " + backupReason;
        else
            message = "SAVE_INVALID — file chính: " + reason + "; backup: " + backupReason;
        return false;
    }

    // On-disk revision must match caller data revision: prevents stale writes.
    // On successful write returns the revised object; the input object is NOT mutated.
    public static bool TrySave(int slot, MS5A1SaveData input, out MS5A1SaveData saved,
                               out string message, bool qa = false)
    {
        EnsureSlot(slot);
        saved = null;
        string expectedId = ProfileId(slot, qa);
        string reason;
        if (!ValidateData(input, expectedId, out reason))
        {
            message = "SAVE_REJECTED — " + reason;
            return false;
        }

        string path = GetSavePath(slot, qa);
        string backup = GetBackupPath(slot, qa);
        string temp = path + ".tmp";

        try
        {
            Directory.CreateDirectory(SaveDirectory);
            MS5A1SaveData existing;
            ReadState state = TryReadFile(path, expectedId, out existing, out reason);
            if (state == ReadState.WrongVersion)
            {
                message = "SAVE_REJECTED — phiên bản file đang có khác. Không ghi đè.";
                return false;
            }
            if (state == ReadState.Corrupt)
            {
                // Only repair a corrupt primary if an independently verified backup exists.
                MS5A1SaveData restored;
                string backupReason;
                if (TryReadFile(backup, expectedId, out restored, out backupReason) != ReadState.Valid)
                {
                    message = "SAVE_REJECTED — file chính hỏng và không có backup hợp lệ. " +
                              "Hãy sao lưu thủ công trước khi can thiệp.";
                    return false;
                }
                File.Copy(backup, path, true); // preserve the valid .bak
                existing = restored;
                state = ReadState.Valid;
            }
            if (state == ReadState.Missing)
            {
                // A missing primary can still have a valid backup (e.g. interrupted move).
                MS5A1SaveData restored;
                string backupReason;
                ReadState bs = TryReadFile(backup, expectedId, out restored, out backupReason);
                if (bs == ReadState.WrongVersion || bs == ReadState.Corrupt)
                {
                    message = "SAVE_REJECTED — backup hiện có không hợp lệ. Không ghi đè.";
                    return false;
                }
                if (bs == ReadState.Valid)
                {
                    File.Copy(backup, path, true);
                    existing = restored;
                    state = ReadState.Valid;
                }
            }
            int currentRevision = state == ReadState.Valid ? existing.revision : 0;
            if (input.revision != currentRevision)
            {
                message = "SAVE_CONFLICT — revision bạn giữ=" + input.revision +
                          ", revision hiện tại=" + currentRevision + ". Hãy tải lại trước khi lưu.";
                return false;
            }
            if (currentRevision == int.MaxValue)
            {
                message = "SAVE_REJECTED — revision vượt giới hạn.";
                return false;
            }

            MS5A1SaveData next = JsonUtility.FromJson<MS5A1SaveData>(JsonUtility.ToJson(input));
            next.revision = currentRevision + 1;
            next.lastSavedUtc = DateTime.UtcNow.ToString("o");
            string payload = JsonUtility.ToJson(next, false);
            MS5A1SaveEnvelope envelope = new MS5A1SaveEnvelope
            {
                magic = Magic,
                formatVersion = FormatVersion,
                sha256 = Hash(payload),
                payloadJson = payload
            };
            string text = JsonUtility.ToJson(envelope, true);
            if (Encoding.UTF8.GetByteCount(text) > MaxFileBytes)
            {
                message = "SAVE_REJECTED — file vượt 2 MB.";
                return false;
            }
            File.WriteAllText(temp, text, new UTF8Encoding(false));
            MS5A1SaveData staged;
            string stagedError;
            if (TryReadFile(temp, expectedId, out staged, out stagedError) != ReadState.Valid)
                throw new IOException("Không xác thực được file .tmp: " + stagedError);

            if (File.Exists(path))
            {
                try
                {
                    File.Replace(temp, path, backup);
                }
                catch (PlatformNotSupportedException)
                {
                    ReplaceFallback(temp, path, backup);
                }
                catch (IOException)
                {
                    ReplaceFallback(temp, path, backup);
                }
            }
            else
            {
                File.Move(temp, path);
            }

            MS5A1SaveData verified;
            string verifyReason;
            if (TryReadFile(path, expectedId, out verified, out verifyReason) != ReadState.Valid)
                throw new IOException("File lưu cuối không hợp lệ: " + verifyReason);
            saved = verified;
            message = "SAVE_OK — revision=" + saved.revision + ", file=" + path;
            return true;
        }
        catch (Exception ex)
        {
            message = "SAVE_FAILED — " + ex.GetType().Name + ": " + ex.Message +
                      ". Backup cũ (nếu có) được giữ để phục hồi.";
            return false;
        }
        finally
        {
            // A partial .tmp may exist after interruption; never treat it as a valid profile.
            // Removing here is best-effort only; do not mask original errors.
            try { if (File.Exists(temp)) File.Delete(temp); } catch (Exception) { }
        }
    }

    private static void ReplaceFallback(string temp, string path, string backup)
    {
        // Compatible fallback for filesystems where File.Replace is not supported.
        // Previous valid save is copied before the temporary file replaces it.
        File.Copy(path, backup, true);
        File.Delete(path);
        File.Move(temp, path);
    }

    private enum ReadState { Missing, Valid, Corrupt, WrongVersion }

    private static ReadState TryReadFile(string path, string expectedId,
                                         out MS5A1SaveData data, out string reason)
    {
        data = null;
        reason = "Không tồn tại";
        try
        {
            if (!File.Exists(path)) return ReadState.Missing;
            FileInfo fi = new FileInfo(path);
            if (fi.Length <= 0 || fi.Length > MaxFileBytes)
            {
                reason = "Kích thước file không hợp lệ";
                return ReadState.Corrupt;
            }
            string json = File.ReadAllText(path, Encoding.UTF8);
            MS5A1SaveEnvelope env = JsonUtility.FromJson<MS5A1SaveEnvelope>(json);
            if (env == null || env.magic != Magic)
            {
                reason = "Sai magic marker";
                return ReadState.Corrupt;
            }
            if (env.formatVersion != FormatVersion)
            {
                reason = "formatVersion=" + env.formatVersion;
                return ReadState.WrongVersion;
            }
            if (string.IsNullOrEmpty(env.payloadJson) ||
                string.IsNullOrEmpty(env.sha256) ||
                !string.Equals(env.sha256, Hash(env.payloadJson), StringComparison.OrdinalIgnoreCase))
            {
                reason = "Checksum SHA256 không khớp";
                return ReadState.Corrupt;
            }
            MS5A1SaveData candidate = JsonUtility.FromJson<MS5A1SaveData>(env.payloadJson);
            if (candidate != null && candidate.schemaVersion != MS5A1SaveData.SchemaVersion)
            {
                reason = "schemaVersion=" + candidate.schemaVersion;
                return ReadState.WrongVersion;
            }
            string detail;
            if (!ValidateData(candidate, expectedId, out detail))
            {
                reason = detail;
                return ReadState.Corrupt;
            }
            data = candidate;
            reason = "OK";
            return ReadState.Valid;
        }
        catch (Exception ex)
        {
            reason = ex.GetType().Name + ": " + ex.Message;
            return ReadState.Corrupt;
        }
    }

    private static bool ValidateData(MS5A1SaveData data, string expectedId, out string reason)
    {
        if (data == null) { reason = "data=null"; return false; }
        if (data.schemaVersion != MS5A1SaveData.SchemaVersion) { reason = "schemaVersion"; return false; }
        if (data.profileId != expectedId) { reason = "profileId không khớp"; return false; }
        if (string.IsNullOrEmpty(data.createdUtc) || data.revision < 0 || data.bankGold < 0)
        { reason = "Ngày tạo/revision/gold không hợp lệ"; return false; }
        if (data.unlockedContentIds == null || data.permanentUpgrades == null)
        { reason = "Danh sách dữ liệu rỗng/null"; return false; }
        if (data.unlockedContentIds.Count > 1024 || data.permanentUpgrades.Count > 1024)
        { reason = "Quá nhiều phần tử"; return false; }
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < data.unlockedContentIds.Count; i++)
            if (!ValidId(data.unlockedContentIds[i]) || !seen.Add(data.unlockedContentIds[i]))
            { reason = "unlockedContentIds trùng hoặc sai id"; return false; }
        seen.Clear();
        foreach (MS5A1UpgradeEntry e in data.permanentUpgrades)
            if (e == null || !ValidId(e.id) || e.level < 0 || !seen.Add(e.id))
            { reason = "permanentUpgrades trùng hoặc dữ liệu không hợp lệ"; return false; }
        reason = "OK";
        return true;
    }

    private static bool ValidId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 96) return false;
        foreach (char c in id)
            if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.')) return false;
        return true;
    }

    private static string Hash(string value)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            StringBuilder sb = new StringBuilder(64);
            foreach (byte b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    private static void EnsureSlot(int slot)
    {
        if (slot < 1 || slot > 3) throw new ArgumentOutOfRangeException("slot", "Chỉ hỗ trợ slot 1–3.");
    }

    private static string ProfileId(int slot, bool qa)
    {
        return (qa ? "qa_" : "") + "slot_" + slot.ToString("00");
    }
}
