using System;
using UnityEngine;

/// <summary>
/// 5A.2 - Persistent bank, separate from PlayerProgression.coins (per-run).
/// All writes use the 5A.1 verified save service. This class never changes inventory or stats.
/// </summary>
public static class MS5A2GoldBank
{
    private const string SelectedSlotKey = "MS5A2_SelectedProfileSlot";

    public static int SelectedSlot
    {
        get
        {
            int slot = PlayerPrefs.GetInt(SelectedSlotKey, 1);
            return Mathf.Clamp(slot, 1, 3);
        }
    }

    public static void SelectSlot(int slot)
    {
        if (slot < 1 || slot > 3)
            throw new ArgumentOutOfRangeException("slot", "Only slots 1-3 are supported.");
        PlayerPrefs.SetInt(SelectedSlotKey, slot);
        PlayerPrefs.Save(); // Only profile selection lives in PlayerPrefs; bankGold is JSON.
    }

    /// <summary>Read-only. NO_SAVE means a new profile with bankGold=0, not a file error.</summary>
    public static bool TryGetBalance(int slot, out int gold, out int revision,
                                     out string status, bool qa = false)
    {
        gold = 0;
        revision = 0;
        MS5A1SaveData data;
        bool recovered;
        string message;
        if (MS5A1SaveService.TryLoad(slot, out data, out message, out recovered, qa))
        {
            gold = data.bankGold;
            revision = data.revision;
            status = recovered ? "BACKUP RECOVERED" : "SAVED";
            return true;
        }
        if (IsNoSave(message))
        {
            status = "NEW PROFILE";
            return true;
        }
        status = "LOAD ERROR: " + message;
        return false;
    }

    /// <summary>
    /// Deposit is called once per completed victorious run by MS5A2BankOnVictory.
    /// Call on main thread. No money is awarded if loading fails or revision conflicts.
    /// </summary>
    public static bool TryDeposit(int slot, int amount, out int resultingGold,
                                  out string message, bool qa = false)
    {
        resultingGold = -1;
        if (amount < 0)
        {
            message = "REJECTED: cannot deposit negative Gold.";
            return false;
        }
        MS5A1SaveData profile;
        bool recovered;
        string loadMessage;
        if (!MS5A1SaveService.TryLoad(slot, out profile, out loadMessage, out recovered, qa))
        {
            if (!IsNoSave(loadMessage))
            {
                message = "DEPOSIT BLOCKED: " + loadMessage;
                return false;
            }
            profile = MS5A1SaveService.CreateFresh(slot, qa);
        }

        long total = (long)profile.bankGold + amount;
        if (total > int.MaxValue)
        {
            message = "DEPOSIT BLOCKED: bank Gold exceeds int range.";
            return false;
        }
        profile.bankGold = (int)total;
        MS5A1SaveData saved;
        string saveMessage;
        if (!MS5A1SaveService.TrySave(slot, profile, out saved, out saveMessage, qa))
        {
            message = "DEPOSIT FAILED: " + saveMessage;
            return false;
        }
        resultingGold = saved.bankGold;
        message = "Deposited +" + amount + " Gold to profile " + slot +
                  "; new bank=" + resultingGold + "; revision=" + saved.revision +
                  (recovered ? " (backup recovered)" : "");
        return true;
    }

    private static bool IsNoSave(string message)
    {
        return !string.IsNullOrEmpty(message) && message.StartsWith("NO_SAVE", StringComparison.Ordinal);
    }
}
