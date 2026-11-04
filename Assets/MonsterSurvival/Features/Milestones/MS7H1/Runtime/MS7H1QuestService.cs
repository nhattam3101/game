using System;
using System.Collections.Generic;
using UnityEngine;

// 7H.1: No custom save file, no PlayerPrefs, no side effects on reads.
// Uses the REAL 5A.1 TrySave transaction for BOTH bankGold and claim marker.
public enum MS7H1QuestId { FirstVictory = 0, DefeatTitan = 1, DefeatWarden = 2 }

public sealed class MS7H1QuestView
{
    public MS7H1QuestId id;
    public string title;
    public string description;
    public int reward;
    public bool completed;
    public bool claimed;
}

public sealed class MS7H1QuestSnapshot
{
    public int slot;
    public int gold;
    public int revision;
    public bool valid;
    public bool backupRecovery;
    public string message;
    public MS7H1QuestView[] quests;
}

public static class MS7H1QuestService
{
    public const string MarkerPrefix = "quest.7h1.claimed.";

    public static string Title(MS7H1QuestId id)
    {
        switch (id)
        {
            case MS7H1QuestId.FirstVictory: return "First Victory";
            case MS7H1QuestId.DefeatTitan: return "Shadow Titan Slayer";
            default: return "Grave Warden Slayer";
        }
    }
    public static string Description(MS7H1QuestId id)
    {
        switch (id)
        {
            case MS7H1QuestId.FirstVictory: return "Win a match and commit the 5A.3 Victory save.";
            case MS7H1QuestId.DefeatTitan: return "Unlock the existing Shadow Titan achievement (5C).";
            default: return "Unlock the existing Grave Warden achievement (5C).";
        }
    }
    public static int Reward(MS7H1QuestId id)
    {
        switch (id)
        {
            case MS7H1QuestId.FirstVictory: return 60;
            case MS7H1QuestId.DefeatTitan: return 120;
            default: return 160;
        }
    }
    public static string Marker(MS7H1QuestId id)
    {
        return MarkerPrefix + ((int)id).ToString();
    }
    public static bool IsCompleted(MS5A1SaveData data, MS7H1QuestId id)
    {
        if (data == null) return false;
        switch (id)
        {
            case MS7H1QuestId.FirstVictory:
                return !string.IsNullOrEmpty(data.lastCommittedRunId);
            case MS7H1QuestId.DefeatTitan:
                return HasId(data, "achievement.shadow_titan");
            case MS7H1QuestId.DefeatWarden:
                return HasId(data, "achievement.grave_warden");
            default: return false;
        }
    }
    public static bool HasId(MS5A1SaveData data, string id)
    {
        return data != null && data.unlockedContentIds != null && data.unlockedContentIds.Contains(id);
    }

    // PURE IN-MEMORY grant rule; the durable service always loads fresh before calling.
    public static bool TryApplyToData(MS5A1SaveData data, MS7H1QuestId quest, out string error)
    {
        error = "";
        if (data == null) { error = "Missing profile"; return false; }
        if (!Enum.IsDefined(typeof(MS7H1QuestId), quest)) { error = "Unknown quest"; return false; }
        if (!IsCompleted(data, quest)) { error = "Quest not completed"; return false; }
        if (HasId(data, Marker(quest))) { error = "Reward already claimed"; return false; }
        int reward = Reward(quest);
        if (data.bankGold < 0 || (long)data.bankGold + reward > int.MaxValue)
        { error = "Invalid Gold or overflow"; return false; }
        if (data.unlockedContentIds == null) data.unlockedContentIds = new List<string>();
        data.bankGold += reward;
        data.unlockedContentIds.Add(Marker(quest));
        return true;
    }

    public static MS7H1QuestSnapshot ReadSnapshot(int slot)
    {
        var snapshot = new MS7H1QuestSnapshot { slot = slot, valid = false, message = "PENDING" };
        snapshot.quests = new MS7H1QuestView[3];
        MS5A1SaveData data = null;
        bool recovered = false;
        string result = "";
        if (slot < 1 || slot > 3) snapshot.message = "BLOCKED: profile must be 01..03";
        else
        {
            try
            {
                snapshot.valid = MS5A1SaveService.TryLoad(slot, out data, out result, out recovered);
                snapshot.backupRecovery = recovered;
                snapshot.message = snapshot.valid ? "VALID" : "NOT READY: " + result;
                if (snapshot.valid && data != null)
                {
                    snapshot.gold = data.bankGold;
                    snapshot.revision = data.revision;
                    if (recovered) snapshot.message = "RECOVERED BACKUP: claims disabled; verify profile first";
                }
                else snapshot.valid = false;
            }
            catch (Exception e) { snapshot.valid = false; snapshot.message = "READ ERROR: " + e.Message; }
        }
        for (int i = 0; i < 3; ++i)
        {
            var id = (MS7H1QuestId)i;
            snapshot.quests[i] = new MS7H1QuestView
            {
                id = id, title = Title(id), description = Description(id), reward = Reward(id),
                completed = snapshot.valid && IsCompleted(data, id),
                claimed = snapshot.valid && HasId(data, Marker(id))
            };
        }
        return snapshot;
    }

    private static bool claiming;
    public static bool TryClaim(int slot, MS7H1QuestId quest, out string message)
    {
        message = "BLOCKED";
        if (claiming) { message = "Another claim is being processed"; return false; }
        if (slot < 1 || slot > 3 || !Enum.IsDefined(typeof(MS7H1QuestId), quest))
        { message = "Invalid profile or quest"; return false; }
        // Fail closed if MainMenu changed profile while the user clicked.
        if (slot != MS5A2GoldBank.SelectedSlot)
        { message = "Profile changed. Reopen the quest board."; return false; }
        if (!Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MainMenu")
        { message = "Claim in Play Mode MainMenu only"; return false; }
        claiming = true;
        try
        {
            MS5A1SaveData data;
            string loadResult;
            bool recovered;
            if (!MS5A1SaveService.TryLoad(slot, out data, out loadResult, out recovered) || data == null)
            { message = "Profile cannot be loaded safely: " + loadResult; return false; }
            if (recovered) { message = "Recovered backup: verify with Manage Saves before claiming"; return false; }
            if (!TryApplyToData(data, quest, out message)) return false;

            // 5A.1 atomic save protects the Gold + quest marker as one revision.
            MS5A1SaveData saved;
            string saveResult;
            if (!MS5A1SaveService.TrySave(slot, data, out saved, out saveResult))
            { message = "SAVE FAILED: " + saveResult + " (no reward confirmed)"; return false; }
            if (saved == null || !HasId(saved, Marker(quest)))
            { message = "SAVE NEEDS AUDIT: check slot revision; do not claim again blindly"; return false; }
            message = "CLAIMED +" + Reward(quest) + " Bank Gold / slot " + slot + " / revision " + saved.revision;
            Debug.Log("[7H.1] " + message);
            return true;
        }
        catch (Exception e) { message = "CLAIM EXCEPTION: " + e.Message + " (inspect Save history)"; Debug.LogException(e); return false; }
        finally { claiming = false; }
    }
}
