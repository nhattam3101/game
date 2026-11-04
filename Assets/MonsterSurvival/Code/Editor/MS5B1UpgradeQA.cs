#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Editor tools are read-only by default. Isolated tests run ONLY against an UNUSED qa_slot_03.
public static class MS5B1UpgradeQA
{
    private const string MenuRoot = "Tools/MonsterSurvival/5B Permanent Upgrades/";

    [MenuItem(MenuRoot + "1 - AUDIT 5B.1 Profiles (READ ONLY)")]
    public static void Audit()
    {
        for (int slot = 1; slot <= 3; slot++)
        {
            MS5B1PermanentUpgrades.Snapshot data;
            string message;
            if (!MS5B1PermanentUpgrades.TryGetSnapshot(slot, out data, out message))
            { Debug.LogWarning("[5B.1 AUDIT] Profile " + slot + " BLOCKED: " + message); continue; }
            string rows = "";
            foreach (MS5B1PermanentUpgrades.Row row in data.rows)
                rows += " | " + row.id + "=" + row.level + "/" + row.maxLevel +
                        " nextGold=" + row.nextCost;
            Debug.Log("[5B.1 AUDIT] Profile " + slot + " bankGold=" + data.bankGold +
                      " revision=" + data.revision + " " + message + rows);
        }
        Debug.Log("[5B.1 AUDIT] READ ONLY; no game saves or Scenes changed.");
    }

    [MenuItem(MenuRoot + "2 - RUN 5B.1 Isolated QA (UNUSED QA SLOT 03)")]
    public static void RunIsolated()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogWarning("[5B.1 QA] Stop Play Mode before running."); return; }
        const int slot = 3;
        string primary = MS5A1SaveService.GetSavePath(slot, true);
        string backup = MS5A1SaveService.GetBackupPath(slot, true);
        string temp = primary + ".tmp";
        if (File.Exists(primary) || File.Exists(backup) || File.Exists(temp))
        {
            Debug.LogWarning("[5B.1 QA] QA slot 03 ALREADY EXISTS. No files modified. " +
                             "Preserve your existing QA files; use AUDIT instead.");
            return;
        }
        if (!EditorUtility.DisplayDialog("5B.1 Isolated QA",
            "Run tests on an UNUSED qa_slot_03 only?\n" +
            "Temporary QA files will be deleted afterwards.\n" +
            "Real Profile 01/02/03, Inventory, Equipment, Scenes will never be written.",
            "RUN TESTS", "CANCEL")) return;
        int passed = 0, total = 0;
        try
        {
            Check(ref passed, ref total, MS5B1UpgradeDefinitions.GetAll().Length == 4, "Four upgrade definitions");
            MS5A1SaveData fresh = MS5A1SaveService.CreateFresh(slot, true);
            fresh.bankGold = 5000;
            fresh.unlockedContentIds.Add("qa.unlock");
            fresh.lastCommittedRunId = "qa_run_123";
            MS5A1SaveData seeded;
            string msg;
            bool created = MS5A1SaveService.TrySave(slot, fresh, out seeded, out msg, true);
            Check(ref passed, ref total, created && seeded != null && seeded.revision == 1, "Seed isolated QA profile");
            if (!created) throw new InvalidOperationException("Cannot seed QA profile: " + msg);

            MS5B1PermanentUpgrades.Snapshot before;
            bool seen = MS5B1PermanentUpgrades.TryGetSnapshot(slot, out before, out msg, true);
            Check(ref passed, ref total, seen && before.bankGold == 5000 && before.LevelOf("strength") == 0,
                  "Read initial Gold and upgrade level");

            MS5B1PermanentUpgrades.PurchaseReceipt receipt;
            bool bought = MS5B1PermanentUpgrades.TryPurchase(slot, "strength", before.revision, out receipt, out msg, true);
            Check(ref passed, ref total, bought && receipt != null && receipt.goldSpent == 60 &&
                  receipt.newLevel == 1 && receipt.remainingBankGold == 4940,
                  "First purchase spends exactly 60 Gold");
            bool stale = MS5B1PermanentUpgrades.TryPurchase(slot, "strength", before.revision, out receipt, out msg, true);
            Check(ref passed, ref total, !stale && msg.StartsWith("STALE_REVISION", StringComparison.Ordinal),
                  "Duplicate request with stale revision rejected");

            MS5B1PermanentUpgrades.Snapshot next;
            MS5B1PermanentUpgrades.TryGetSnapshot(slot, out next, out msg, true);
            bought = MS5B1PermanentUpgrades.TryPurchase(slot, "strength", next.revision, out receipt, out msg, true);
            Check(ref passed, ref total, bought && receipt != null && receipt.goldSpent == 120 &&
                  receipt.remainingBankGold == 4820 && receipt.newLevel == 2,
                  "Second level costs 120 Gold");

            MS5B1PermanentUpgrades.TryGetSnapshot(slot, out next, out msg, true);
            bought = MS5B1PermanentUpgrades.TryPurchase(slot, "vitality", next.revision, out receipt, out msg, true);
            Check(ref passed, ref total, bought && receipt != null && receipt.goldSpent == 50 &&
                  receipt.remainingBankGold == 4770, "Independent vitality upgrade costs 50 Gold");

            MS5B1PermanentUpgrades.Snapshot after;
            bool reloaded = MS5B1PermanentUpgrades.TryGetSnapshot(slot, out after, out msg, true);
            Check(ref passed, ref total, reloaded && after.revision == 4 && after.bankGold == 4770 &&
                  after.LevelOf("strength") == 2 && after.LevelOf("vitality") == 1,
                  "Verified reload after three atomic purchases");
            bool unknown = MS5B1PermanentUpgrades.TryPurchase(slot, "not_known", after.revision,
                                                               out receipt, out msg, true);
            Check(ref passed, ref total, !unknown && msg == "UNKNOWN_UPGRADE", "Unknown upgrade rejected");

            MS5A1SaveData final;
            bool recovered;
            bool loaded = MS5A1SaveService.TryLoad(slot, out final, out msg, out recovered, true);
            Check(ref passed, ref total, loaded && final != null && final.unlockedContentIds.Contains("qa.unlock") &&
                  final.lastCommittedRunId == "qa_run_123", "Existing SaveData fields preserved");
        }
        catch (Exception e)
        {
            Debug.LogError("[5B.1 QA] Interrupted: " + e.Message);
        }
        finally
        {
            // Only safe because this test refused to start if ANY QA slot file existed.
            try { if (File.Exists(primary)) File.Delete(primary); } catch (Exception e) { Debug.LogError("QA cleanup primary: " + e.Message); }
            try { if (File.Exists(backup)) File.Delete(backup); } catch (Exception e) { Debug.LogError("QA cleanup backup: " + e.Message); }
            try { if (File.Exists(temp)) File.Delete(temp); } catch (Exception e) { Debug.LogError("QA cleanup temp: " + e.Message); }
        }
        Debug.Log("[5B.1 QA] RESULT " + passed + "/" + total + " checks. " +
                  "Only temporary qa_slot_03 was used; real profiles not modified.");
    }

    private static void Check(ref int passed, ref int total, bool condition, string label)
    {
        total++;
        if (condition) { passed++; Debug.Log("[5B.1 QA] PASS " + label); }
        else Debug.LogError("[5B.1 QA] FAIL " + label);
    }
}
#endif
