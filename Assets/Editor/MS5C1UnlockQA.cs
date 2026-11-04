#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// 5C.1: no scene modifications. Audit is read-only; QA uses exclusively an unused qa_slot_02.
public static class MS5C1UnlockQA
{
    private const string Menu = "Tools/MonsterSurvival/5C Unlock System/";

    [MenuItem(Menu + "1 - AUDIT Unlocks (READ ONLY)")]
    public static void Audit()
    {
        foreach (MS5C1UnlockDefinition def in MS5C1UnlockCatalog.GetAll())
        {
            Debug.Log("[5C.1 Catalog] " + def.id + " | " + def.category +
                " | cost=" + def.price + " | starter=" + def.starterUnlocked +
                " | achievement=" + def.achievementOnly);
        }
        for (int slot = 1; slot <= 3; ++slot)
        {
            MS5C1UnlockService.Snapshot snapshot;
            string message;
            if (!MS5C1UnlockService.TryGetSnapshot(slot, out snapshot, out message))
            { Debug.LogWarning("[5C.1 AUDIT] Profile " + slot + " blocked: " + message); continue; }
            string unlocks = "";
            foreach (MS5C1UnlockService.UnlockRow row in snapshot.rows)
                if (row.unlocked) unlocks += " " + row.id;
            Debug.Log("[5C.1 AUDIT] Profile " + slot + ": gold=" + snapshot.bankGold +
                      " rev=" + snapshot.revision + " status=" + message +
                      " unlocked=" + unlocks);
        }
        Debug.Log("[5C.1 AUDIT] READ ONLY. Current Map Select has NOT been gated.");
    }

    [MenuItem(Menu + "2 - RUN Isolated Unlock QA (UNUSED QA SLOT 02)")]
    public static void IsolatedQA()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogWarning("[5C.1 QA] Stop Play Mode first."); return; }
        const int slot = 2;
        string file = MS5A1SaveService.GetSavePath(slot, true);
        string backup = MS5A1SaveService.GetBackupPath(slot, true);
        string temp = file + ".tmp";
        if (File.Exists(file) || File.Exists(backup) || File.Exists(temp))
        {
            Debug.LogWarning("[5C.1 QA] QA slot 02 already has files. REFUSED; no files altered.");
            return;
        }
        if (!EditorUtility.DisplayDialog("5C.1 Isolated QA",
            "Test on an UNUSED qa_slot_02 only?\\nAll created QA files are removed after tests.\\nReal profiles remain untouched.",
            "RUN QA", "CANCEL")) return;
        int pass = 0, total = 0;
        try
        {
            MS5C1UnlockService.Snapshot snap;
            string reason;
            bool canRead = MS5C1UnlockService.TryGetSnapshot(slot, out snap, out reason, true);
            Check(ref pass, ref total, canRead && snap.isNew && snap.bankGold == 0 &&
                  snap.IsUnlocked("map.green_meadow") && !snap.IsUnlocked("map.sand_dunes"),
                  "New profile read-only starter map");
            Check(ref pass, ref total, MS5C1UnlockCatalog.GetAll().Length == 7,
                  "Seven fixed catalog entries");
            MS5A1SaveData fresh = MS5A1SaveService.CreateFresh(slot, true);
            fresh.bankGold = 2000;
            fresh.permanentUpgrades.Add(new MS5A1UpgradeEntry {id="strength", level=2});
            fresh.lastCommittedRunId = "qa_run_preserve";
            MS5A1SaveData saved;
            string result;
            bool created = MS5A1SaveService.TrySave(slot, fresh, out saved, out result, true);
            Check(ref pass, ref total, created && saved != null && saved.revision == 1, "Seed QA profile");
            if (!created) throw new InvalidOperationException(result);
            MS5C1UnlockService.UnlockReceipt receipt;
            bool bought = MS5C1UnlockService.TryPurchase(slot, "map.sand_dunes", 1, out receipt, out result, true);
            Check(ref pass, ref total, bought && receipt != null && receipt.goldSpent == 250 &&
                  receipt.bankGold == 1750 && receipt.revision == 2, "Buy Sand Dunes exactly once");
            bool stale = MS5C1UnlockService.TryPurchase(slot, "map.ancient_ruins", 1,
                out receipt, out result, true);
            Check(ref pass, ref total, !stale && result == "STALE_REVISION", "Stale revision refused");
            bool duplicate = MS5C1UnlockService.TryPurchase(slot, "map.sand_dunes", 2,
                out receipt, out result, true);
            Check(ref pass, ref total, !duplicate && result == "ALREADY_UNLOCKED", "Duplicate unlock refused");
            bool bad = MS5C1UnlockService.TryPurchase(slot, "unknown.map", 2,
                out receipt, out result, true);
            Check(ref pass, ref total, !bad && result == "UNKNOWN_CONTENT", "Unknown ID refused");
            bool noFree = MS5C1UnlockService.TryPurchase(slot, "achievement.shadow_titan", 2,
                out receipt, out result, true);
            Check(ref pass, ref total, !noFree && result == "ACHIEVEMENT_ONLY", "Achievements not sold");
            bool grant = MS5C1UnlockService.TryGrantAchievement(slot, "achievement.shadow_titan", 2,
                out receipt, out result, true);
            Check(ref pass, ref total, grant && receipt != null && receipt.goldSpent == 0 &&
                  receipt.bankGold == 1750, "Verified achievement grant does not spend Gold");
            bool grantAgain = MS5C1UnlockService.TryGrantAchievement(slot, "achievement.shadow_titan", 3,
                out receipt, out result, true);
            Check(ref pass, ref total, !grantAgain && result == "ALREADY_UNLOCKED", "Duplicate achievement refused");
            MS5C1UnlockService.Snapshot after;
            bool read = MS5C1UnlockService.TryGetSnapshot(slot, out after, out result, true);
            Check(ref pass, ref total, read && after.bankGold == 1750 && after.revision == 3 &&
                  after.IsUnlocked("map.sand_dunes") && after.IsUnlocked("achievement.shadow_titan"),
                  "Reload preserved unlocks and Gold");
            MS5A1SaveData stored;
            bool recovered;
            bool load = MS5A1SaveService.TryLoad(slot, out stored, out result, out recovered, true);
            Check(ref pass, ref total, load && !recovered && stored.lastCommittedRunId == "qa_run_preserve" &&
                  stored.permanentUpgrades.Count == 1 && stored.permanentUpgrades[0].level == 2,
                  "Save retained existing upgrade and run fields");
            bool tooCostly = MS5C1UnlockService.TryPurchase(slot, "equipment.wardens_oath", 3,
                out receipt, out result, true);
            Check(ref pass, ref total, tooCostly && receipt != null && receipt.bankGold == 650,
                  "Gear unlock spends Gold and keeps remaining balance");
            bool notEnough = MS5C1UnlockService.TryPurchase(slot, "equipment.reaper_edge", 4,
                out receipt, out result, true);
            Check(ref pass, ref total, !notEnough && result == "NOT_ENOUGH_BANK_GOLD",
                  "Insufficient Gold refused");
            MS5C1UnlockService.TryGetSnapshot(slot, out after, out result, true);
            Check(ref pass, ref total, after.revision == 4 && after.bankGold == 650,
                  "Failed requests did not change revision or Gold");
        }
        catch (Exception e) { Debug.LogError("[5C.1 QA] Interrupted: " + e.Message); }
        finally
        {
            // Safe because tests refuse to start unless all three QA paths are absent.
            foreach (string p in new [] {file, backup, temp})
                try { if (File.Exists(p)) File.Delete(p); }
                catch (Exception e) { Debug.LogError("[5C.1 QA] Cleanup failed " + p + ": " + e.Message); }
        }
        Debug.Log("[5C.1 QA] RESULT " + pass + "/" + total +
                  " checks (isolated QA only; real profiles were not modified).");
    }

    private static void Check(ref int passed, ref int total, bool condition, string title)
    {
        total++;
        if (condition) { passed++; Debug.Log("[5C.1 QA] PASS " + title); }
        else Debug.LogError("[5C.1 QA] FAIL " + title);
    }
}
#endif
