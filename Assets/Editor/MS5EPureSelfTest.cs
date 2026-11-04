#if UNITY_EDITOR
using System;
using System.Collections.Generic;

// Pure-in-memory API tests; no SaveService.TrySave, profile reads or scene mutations.
public static class MS5EPureSelfTest
{
    public static string Run()
    {
        int pass = 0, total = 0;
        var reasons = new List<string>();
        MS5B1UpgradeDefinition[] definitions = MS5B1UpgradeDefinitions.GetAll();
        Verify(++total, definitions.Length == 4, "4 upgrade definitions", ref pass, reasons);
        int cost = 0;
        bool priceOK = true;
        foreach (MS5B1UpgradeDefinition def in definitions)
        {
            if (def == null) { priceOK = false; continue; }
            for (int i = 0; i < 10; i++)
            {
                int price = def.PriceForNextLevel(i);
                priceOK &= price == def.baseCost * (i + 1);
                cost += price;
            }
            priceOK &= def.PriceForNextLevel(10) == -1;
        }
        Verify(++total, priceOK, "upgrade cost step and level cap", ref pass, reasons);
        Verify(++total, cost == 12375, "upgrade total Gold=12375", ref pass, reasons);
        MS5B3BonusValues zero, full;
        string problem;
        bool z = MS5B3BonusMath.TryCalculate(0,0,0,0,out zero,out problem);
        Verify(++total, z && Close(zero.maxHealth,0) && Close(zero.damage,0) && Close(zero.attackSpeed,0) && Close(zero.moveSpeed,0), "zero bonus", ref pass, reasons);
        bool f = MS5B3BonusMath.TryCalculate(10,10,10,10,out full,out problem);
        Verify(++total, f && Close(full.maxHealth,100) && Close(full.damage,.5f) && Close(full.attackSpeed,.3f) && Close(full.moveSpeed,.2f), "max bonus", ref pass, reasons);
        MS5B3BonusValues invalid;
        bool reject = !MS5B3BonusMath.TryCalculate(11,0,0,0,out invalid,out problem);
        Verify(++total, reject, "invalid level rejected", ref pass, reasons);
        MS5C1UnlockDefinition[] unlocks = MS5C1UnlockCatalog.GetAll();
        Verify(++total, unlocks.Length == 7, "seven unlock IDs", ref pass, reasons);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int unlockCost = 0;
        int starter = 0;
        bool unique = true;
        foreach (MS5C1UnlockDefinition entry in unlocks)
        {
            if (entry == null) { unique = false; continue; }
            unique &= ids.Add(entry.id);
            if (entry.starterUnlocked) starter++;
            if (!entry.starterUnlocked && !entry.achievementOnly) unlockCost += entry.price;
        }
        Verify(++total, unique && starter == 1, "unique unlock IDs + 1 starter", ref pass, reasons);
        Verify(++total, unlockCost == 2800, "unlock cost 2800 Gold", ref pass, reasons);
        Verify(++total, unlockCost + cost == 15175, "full progression forecast 15175 Gold", ref pass, reasons);
        return pass + "/" + total + " " + (pass == total ? "PASS" : "FAIL") +
               (reasons.Count == 0 ? "" : " | " + string.Join("; ", reasons.ToArray()));
    }

    private static bool Close(float a, float b) { return Math.Abs(a - b) < .0001f; }
    private static void Verify(int index, bool ok, string label, ref int passed, List<string> failures)
    {
        if (ok) passed++; else failures.Add(index + ": " + label);
    }
}
#endif
