using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 7H.2: In-save milestones. No PlayerPrefs, parallel SaveData or Gold balance polling.
// One latest-run marker plus compact totals; all writes use the existing 5A.1 TrySave.
public enum MS7H2QuestId { ThreeVictories = 0, TwoBosses = 1, EarnFiveHundred = 2 }

public sealed class MS7H2QuestView
{
    public MS7H2QuestId id;
    public string title;
    public string description;
    public int current;
    public int target;
    public int reward;
    public bool claimed;
    public bool completed;
}

public sealed class MS7H2Snapshot
{
    public int slot;
    public bool valid;
    public bool backupRecovery;
    public string message;
    public int bankGold;
    public int revision;
    public int wins;
    public int bosses;
    public int earnedGold;
    public MS7H2QuestView[] quests;
}

public static class MS7H2ProgressService
{
    private const string Prefix = "quest.7h2.";
    private const string WinsKey = Prefix + "progress.wins.";
    private const string BossKey = Prefix + "progress.bosses.";
    private const string GoldKey = Prefix + "progress.gold.";
    private const string LastKey = Prefix + "progress.last.";
    private const string ClaimKey = Prefix + "claimed.";
    private static bool transactionBusy;

    public static string Title(MS7H2QuestId id)
    {
        switch (id)
        {
            case MS7H2QuestId.ThreeVictories: return "Seasoned Survivor";
            case MS7H2QuestId.TwoBosses: return "Boss Hunter";
            default: return "Gold Collector";
        }
    }
    public static string Description(MS7H2QuestId id)
    {
        switch (id)
        {
            case MS7H2QuestId.ThreeVictories: return "Win 3 saved matches.";
            case MS7H2QuestId.TwoBosses: return "Defeat 2 WaveManager-spawned Bosses in winning runs.";
            default: return "Earn 500 Gold in winning runs (not Bank balance).";
        }
    }
    public static int Target(MS7H2QuestId id)
    {
        switch (id)
        {
            case MS7H2QuestId.ThreeVictories: return 3;
            case MS7H2QuestId.TwoBosses: return 2;
            default: return 500;
        }
    }
    public static int Reward(MS7H2QuestId id)
    {
        switch (id)
        {
            case MS7H2QuestId.ThreeVictories: return 150;
            case MS7H2QuestId.TwoBosses: return 220;
            default: return 200;
        }
    }
    public static string ClaimMarker(MS7H2QuestId id) { return ClaimKey + (int)id; }
    private static bool ValidId(MS7H2QuestId id) { return (int)id >= 0 && (int)id <= 2; }
    private static List<string> Ids(MS5A1SaveData data)
    {
        return data.unlockedContentIds ?? (data.unlockedContentIds = new List<string>());
    }
    private static string ReadMarker(List<string> ids, string prefix)
    {
        if (ids == null) return "";
        for (int i = ids.Count - 1; i >= 0; i--)
        {
            string s = ids[i];
            if (s != null && s.StartsWith(prefix, StringComparison.Ordinal)) return s.Substring(prefix.Length);
        }
        return "";
    }
    private static int ReadCount(List<string> ids, string prefix)
    {
        int count;
        return int.TryParse(ReadMarker(ids, prefix), out count) && count > 0 ? count : 0;
    }
    private static void ReplaceMarker(List<string> ids, string prefix, string value)
    {
        for (int i = ids.Count - 1; i >= 0; i--)
            if (ids[i] != null && ids[i].StartsWith(prefix, StringComparison.Ordinal)) ids.RemoveAt(i);
        ids.Add(prefix + value);
    }
    public static int Wins(MS5A1SaveData data) { return data == null ? 0 : ReadCount(data.unlockedContentIds, WinsKey); }
    public static int Bosses(MS5A1SaveData data) { return data == null ? 0 : ReadCount(data.unlockedContentIds, BossKey); }
    public static int EarnedGold(MS5A1SaveData data) { return data == null ? 0 : ReadCount(data.unlockedContentIds, GoldKey); }
    public static bool IsClaimed(MS5A1SaveData data, MS7H2QuestId id)
    {
        return data != null && data.unlockedContentIds != null && data.unlockedContentIds.Contains(ClaimMarker(id));
    }
    private static int ValueFor(MS5A1SaveData data, MS7H2QuestId id)
    {
        switch (id)
        {
            case MS7H2QuestId.ThreeVictories: return Wins(data);
            case MS7H2QuestId.TwoBosses: return Bosses(data);
            default: return EarnedGold(data);
        }
    }
    public static bool IsComplete(MS5A1SaveData data, MS7H2QuestId id)
    {
        return data != null && ValidId(id) && ValueFor(data, id) >= Target(id);
    }

    // Pure in-memory mutation. Caller must first validate the current 5A saved run id.
    // Exactly one increment per committed run; no increase for a repeated Sync/Retry.
    public static bool TryApplyRunToData(MS5A1SaveData data, string committedRunId,
        int runGold, bool realBossDefeated, out string error)
    {
        error = "";
        if (data == null || string.IsNullOrEmpty(committedRunId) || data.lastCommittedRunId != committedRunId)
        { error = "No matching committed Victory in 5A Save"; return false; }
        if (committedRunId.Length > 100 || runGold < 0)
        { error = "Invalid run values"; return false; }
        List<string> ids = Ids(data);
        if (ReadMarker(ids, LastKey) == committedRunId)
        { error = "Run already synchronized"; return false; }
        int wins = Wins(data), bosses = Bosses(data), gold = EarnedGold(data);
        if (wins == int.MaxValue || (realBossDefeated && bosses == int.MaxValue) ||
            (long)gold + runGold > int.MaxValue)
        { error = "Progress overflow; no save written"; return false; }
        ReplaceMarker(ids, WinsKey, (wins + 1).ToString());
        ReplaceMarker(ids, BossKey, (bosses + (realBossDefeated ? 1 : 0)).ToString());
        ReplaceMarker(ids, GoldKey, (gold + runGold).ToString());
        ReplaceMarker(ids, LastKey, committedRunId);
        return true;
    }
    // Pure in-memory grant. Gold and claim mark become durable in the SAME 5A TrySave.
    public static bool TryApplyClaimToData(MS5A1SaveData data, MS7H2QuestId id, out string error)
    {
        error = "";
        if (data == null || !ValidId(id)) { error = "Missing profile or quest"; return false; }
        if (!IsComplete(data, id)) { error = "Quest progress is insufficient"; return false; }
        if (IsClaimed(data, id)) { error = "Already claimed"; return false; }
        int reward = Reward(id);
        if (data.bankGold < 0 || (long)data.bankGold + reward > int.MaxValue)
        { error = "Bank Gold overflow or invalid"; return false; }
        data.bankGold += reward;
        Ids(data).Add(ClaimMarker(id));
        return true;
    }
    public static MS7H2Snapshot ReadSnapshot(int slot)
    {
        MS7H2Snapshot snapshot = new MS7H2Snapshot { slot = slot, quests = new MS7H2QuestView[3], message = "No valid Save" };
        MS5A1SaveData data = null;
        if (slot >= 1 && slot <= 3)
        {
            try
            {
                string result; bool recovered;
                snapshot.valid = MS5A1SaveService.TryLoad(slot, out data, out result, out recovered) && data != null;
                snapshot.backupRecovery = recovered;
                snapshot.message = snapshot.valid ? (recovered ? "BACKUP RECOVERY: claims blocked" : "VALID") : "NOT READY: " + result;
                if (snapshot.valid)
                {
                    snapshot.bankGold = data.bankGold;
                    snapshot.revision = data.revision;
                    snapshot.wins = Wins(data);
                    snapshot.bosses = Bosses(data);
                    snapshot.earnedGold = EarnedGold(data);
                }
            }
            catch (Exception ex) { snapshot.valid = false; snapshot.message = "READ ERROR: " + ex.Message; }
        }
        for (int i = 0; i < snapshot.quests.Length; i++)
        {
            MS7H2QuestId id = (MS7H2QuestId)i;
            snapshot.quests[i] = new MS7H2QuestView
            {
                id = id, title = Title(id), description = Description(id),
                current = snapshot.valid ? ValueFor(data, id) : 0,
                target = Target(id), reward = Reward(id),
                claimed = snapshot.valid && IsClaimed(data, id),
                completed = snapshot.valid && IsComplete(data, id)
            };
        }
        return snapshot;
    }

    // Called ONLY by 7H2 Observer after 5A3.Committed becomes true in SampleScene.
    public static bool TrySyncVictory(int slot, int earnedGold, bool bossDefeated, out string message)
    {
        message = "BLOCKED";
        if (transactionBusy) { message = "Another 7H2 transaction active"; return false; }
        if (!Application.isPlaying || SceneManager.GetActiveScene().name != "SampleScene" ||
            slot < 1 || slot > 3 || MS5A2GoldBank.SelectedSlot != slot)
        { message = "Not a valid gameplay session/profile"; return false; }
        transactionBusy = true;
        try
        {
            MS5A1SaveData data; string load; bool recovered;
            if (!MS5A1SaveService.TryLoad(slot, out data, out load, out recovered) || data == null || recovered)
            { message = "Save is missing, invalid or backup-only: " + load; return false; }
            string error;
            if (!TryApplyRunToData(data, data.lastCommittedRunId, earnedGold, bossDefeated, out error))
            { message = error; return false; }
            MS5A1SaveData saved; string result;
            if (!MS5A1SaveService.TrySave(slot, data, out saved, out result) || saved == null)
            { message = "7H2 Progress SAVE FAILED: " + result; return false; }
            message = "PROGRESS SAVED profile=" + slot + " wins=" + Wins(saved) +
                      " bosses=" + Bosses(saved) + " goldEarned=" + EarnedGold(saved) +
                      " revision=" + saved.revision;
            return true;
        }
        catch (Exception ex) { message = "SYNC EXCEPTION: " + ex.Message; Debug.LogException(ex); return false; }
        finally { transactionBusy = false; }
    }
    public static bool TryClaim(int slot, MS7H2QuestId id, out string message)
    {
        message = "BLOCKED";
        if (transactionBusy) { message = "Another 7H2 transaction active"; return false; }
        if (!Application.isPlaying || SceneManager.GetActiveScene().name != "MainMenu" ||
            slot < 1 || slot > 3 || slot != MS5A2GoldBank.SelectedSlot || !ValidId(id))
        { message = "Claim from the selected MainMenu profile only"; return false; }
        transactionBusy = true;
        try
        {
            MS5A1SaveData data; string load; bool recovered;
            if (!MS5A1SaveService.TryLoad(slot, out data, out load, out recovered) || data == null || recovered)
            { message = "Unsafe Save: " + load; return false; }
            if (!TryApplyClaimToData(data, id, out message)) return false;
            MS5A1SaveData saved; string result;
            if (!MS5A1SaveService.TrySave(slot, data, out saved, out result) || saved == null || !IsClaimed(saved, id))
            { message = "SAVE NOT VERIFIED: " + result + "; inspect Manage Saves"; return false; }
            message = "CLAIMED +" + Reward(id) + " Gold / Profile " + slot + " / Rev " + saved.revision;
            Debug.Log("[7H2] " + message);
            return true;
        }
        catch (Exception ex) { message = "CLAIM EXCEPTION: " + ex.Message; Debug.LogException(ex); return false; }
        finally { transactionBusy = false; }
    }
}
