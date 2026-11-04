#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 5B.5 transaction regression on qa_slot_01 ONLY (never slot_01 real profile).
/// Refuses to start when ANY of its QA files already exist.
/// </summary>
public static class MS5B5IsolatedPurchaseQA
{
    [MenuItem("Tools/MonsterSurvival/5B Permanent Upgrades/15 - RUN 5B.5 ISOLATED PURCHASE QA (UNUSED QA SLOT 01)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogWarning("[5B.5] Exit Play Mode before running QA."); return; }
        const int slot = 1;
        string primary = MS5A1SaveService.GetSavePath(slot, true);
        string backup = MS5A1SaveService.GetBackupPath(slot, true);
        string temp = primary + ".tmp";
        if (File.Exists(primary) || File.Exists(backup) || File.Exists(temp))
        {
            Debug.LogWarning("[5B.5] UNUSED qa_slot_01 required. Found existing QA file. Nothing touched. " + primary);
            return;
        }
        if (!EditorUtility.DisplayDialog("5B.5 ISOLATED PURCHASE QA",
            "Test ONLY the unused QA file qa_slot_01.json (NOT real Profile 01).\n" +
            "The QA test will create and clean up its own temporary files.\n\n" +
            "Real save slots, scenes, PlayerStats and Bank Gold will not change.", "RUN QA", "CANCEL")) return;

        int pass = 0, total = 0;
        try
        {
            MS5B1UpgradeDefinition[] defs = MS5B1UpgradeDefinitions.GetAll();
            Check(ref pass,ref total, defs.Length == 4, "Four upgrade definitions exist");
            HashSet<string> ids = new HashSet<string>();
            bool unique = true;
            foreach (MS5B1UpgradeDefinition def in defs)
                if (def == null || !ids.Add(def.id) || def.baseCost <= 0 || def.maxLevel != 10) unique = false;
            Check(ref pass,ref total, unique, "Valid unique IDs / price / level limit");
            MS5B3BonusValues zero, max;
            string error0, error10;
            bool a = MS5B3BonusMath.TryCalculate(0,0,0,0,out zero,out error0);
            bool b = MS5B3BonusMath.TryCalculate(10,10,10,10,out max,out error10);
            Check(ref pass,ref total,a && b && Nearly(max.maxHealth,100) && Nearly(max.damage,.5f) && Nearly(max.attackSpeed,.3f) && Nearly(max.moveSpeed,.2f),"Bonus math at L0/L10");

            MS5A1SaveData seed = MS5A1SaveService.CreateFresh(slot, true);
            seed.bankGold = 2000;
            seed.unlockedContentIds.Add("qa_only_unlock");
            seed.lastCommittedRunId = "qa_5b5_run";
            string msg;
            MS5A1SaveData saved;
            bool ok = MS5A1SaveService.TrySave(slot,seed,out saved,out msg,true);
            Check(ref pass,ref total,ok && saved != null && saved.revision == 1,"Create isolated profile revision 1");
            if (!ok) throw new IOException("Cannot seed QA file: " + msg);
            MS5B1PermanentUpgrades.Snapshot current;
            ok = MS5B1PermanentUpgrades.TryGetSnapshot(slot,out current,out msg,true);
            Check(ref pass,ref total,ok && current.bankGold == 2000 && current.LevelOf("strength")==0,"Read isolated Gold / level");
            if (!ok) throw new IOException("QA snapshot read failed: " + msg);
            int oldRevision = current.revision;
            MS5B1PermanentUpgrades.PurchaseReceipt receipt;
            ok = MS5B1PermanentUpgrades.TryPurchase(slot,"strength",oldRevision,out receipt,out msg,true);
            Check(ref pass,ref total,ok && receipt != null && receipt.goldSpent==60 && receipt.newLevel==1 && receipt.remainingBankGold==1940,"One Strength purchase costs 60 Gold");
            bool duplicate = MS5B1PermanentUpgrades.TryPurchase(slot,"strength",oldRevision,out receipt,out msg,true);
            Check(ref pass,ref total,!duplicate && msg.StartsWith("STALE_REVISION",StringComparison.Ordinal),"Duplicate/stale click blocked");
            MS5B1PermanentUpgrades.TryGetSnapshot(slot,out current,out msg,true);
            ok = MS5B1PermanentUpgrades.TryPurchase(slot,"strength",current.revision,out receipt,out msg,true);
            Check(ref pass,ref total,ok && receipt != null && receipt.goldSpent==120 && receipt.remainingBankGold==1820,"Strength second level costs 120 Gold");
            MS5B1PermanentUpgrades.TryGetSnapshot(slot,out current,out msg,true);
            ok = MS5B1PermanentUpgrades.TryPurchase(slot,"vitality",current.revision,out receipt,out msg,true);
            Check(ref pass,ref total,ok && receipt != null && receipt.goldSpent==50 && receipt.remainingBankGold==1770,"Vitality independently costs 50 Gold");
            MS5B1PermanentUpgrades.TryGetSnapshot(slot,out current,out msg,true);
            bool unknown = MS5B1PermanentUpgrades.TryPurchase(slot,"qa_invalid_id",current.revision,out receipt,out msg,true);
            Check(ref pass,ref total,!unknown && msg == "UNKNOWN_UPGRADE","Unknown upgrade rejected");
            MS5A1SaveData loaded; bool recovered;
            ok = MS5A1SaveService.TryLoad(slot,out loaded,out msg,out recovered,true);
            Check(ref pass,ref total,ok && !recovered && loaded != null && loaded.bankGold==1770 &&
                  loaded.lastCommittedRunId=="qa_5b5_run" && loaded.unlockedContentIds.Contains("qa_only_unlock"),
                  "Reload maintains Gold / run marker / unlock fields");
            if (!ok) throw new IOException("QA profile reload failed: " + msg);

            loaded.bankGold = 0;
            ok = MS5A1SaveService.TrySave(slot,loaded,out saved,out msg,true);
            if (ok)
            {
                MS5B1PermanentUpgrades.TryGetSnapshot(slot,out current,out msg,true);
                bool blocked = MS5B1PermanentUpgrades.TryPurchase(slot,"agility",current.revision,out receipt,out msg,true);
                Check(ref pass,ref total,!blocked && msg=="NOT_ENOUGH_BANK_GOLD","Insufficient Gold blocked without spending");
            }
            else Check(ref pass,ref total,false,"Insufficient Gold setup saved");

            loaded = saved;
            if (loaded == null) throw new IOException("QA step missing saved profile");
            loaded.bankGold = 10000;
            if (loaded.permanentUpgrades == null) loaded.permanentUpgrades = new List<MS5A1UpgradeEntry>();
            bool found = false;
            foreach (MS5A1UpgradeEntry e in loaded.permanentUpgrades)
                if (e != null && e.id=="agility") { e.level = 10; found = true; break; }
            if (!found) loaded.permanentUpgrades.Add(new MS5A1UpgradeEntry{ id="agility",level=10 });
            ok = MS5A1SaveService.TrySave(slot,loaded,out saved,out msg,true);
            if (ok)
            {
                MS5B1PermanentUpgrades.TryGetSnapshot(slot,out current,out msg,true);
                bool blocked = MS5B1PermanentUpgrades.TryPurchase(slot,"agility",current.revision,out receipt,out msg,true);
                Check(ref pass,ref total,!blocked && msg=="MAX_LEVEL","Max level cannot be purchased again");
            }
            else Check(ref pass,ref total,false,"Max level setup saved");
            MS5B3BonusValues invalid; string eText;
            bool invalidAccepted = MS5B3BonusMath.TryCalculate(11,0,0,0,out invalid,out eText);
            Check(ref pass,ref total,!invalidAccepted,"Level > 10 rejected by bonus math");
        }
        catch (Exception ex) { Debug.LogError("[5B.5 QA] STOPPED: " + ex); }
        finally
        {
            // The test never starts if these files already exist, so cleanup targets only its own QA files.
            try { if (File.Exists(primary)) File.Delete(primary); }
            catch(Exception ex) { Debug.LogError("[5B.5 QA] Cannot delete QA primary: "+ex.Message); }
            try { if (File.Exists(backup)) File.Delete(backup); }
            catch(Exception ex) { Debug.LogError("[5B.5 QA] Cannot delete QA backup: "+ex.Message); }
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch(Exception ex) { Debug.LogError("[5B.5 QA] Cannot delete QA temp: "+ex.Message); }
        }
        Debug.Log("[5B.5 QA] RESULT " + pass + "/" + total + " | QA slot 01 only. " +
                  (pass == total ? "ALL TESTS PASSED" : "CHECK FAILED TESTS ABOVE"));
    }

    private static bool Nearly(float a,float b){return Mathf.Abs(a-b)<.0001f;}
    private static void Check(ref int passed,ref int total,bool ok,string label)
    {
        total++;
        if(ok){passed++;Debug.Log("[5B.5 QA] PASS " + total + " - " + label);}
        else Debug.LogError("[5B.5 QA] FAIL " + total + " - " + label);
    }
}
#endif
