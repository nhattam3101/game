using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// PURE CATALOG CHECKS ONLY. Does not read or write any Profile Save.
// It intentionally does not claim to test a real transaction or gameplay achievement.
public static class MS5C5CatalogSelfTest
{
    [MenuItem("Tools/MonsterSurvival/5C Unlock System/13 - RUN 5C.5 Catalog Self-Test (NO SAVE WRITES)")]
    public static void Run()
    {
        MS5C1UnlockDefinition[] definitions = MS5C1UnlockCatalog.GetAll();
        int pass = 0;
        int fail = 0;
        StringBuilder report = new StringBuilder("[5C.5 Catalog Self-Test] NO SAVE READ/WRITE\n");
        HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
        int maps = 0, equipment = 0, achievements = 0, starters = 0, purchaseGold = 0;
        bool allNonNull = true;
        bool allIdsUnique = true;
        bool pricesNonnegative = true;
        bool achieveOnlyNotSold = true;
        bool starterFree = true;
        bool itemSemantics = true;
        for (int i = 0; i < definitions.Length; i++)
        {
            MS5C1UnlockDefinition d = definitions[i];
            if (d == null) { allNonNull = false; continue; }
            if (string.IsNullOrEmpty(d.id)) { allNonNull = false; continue; }
            if (!unique.Add(d.id)) allIdsUnique = false;
            if (d.price < 0) pricesNonnegative = false;
            if (d.starterUnlocked)
            {
                starters++;
                if (d.price != 0) starterFree = false;
            }
            if (d.achievementOnly && d.price != 0) achieveOnlyNotSold = false;
            if (d.category == MS5C1UnlockCategory.Map) maps++;
            else if (d.category == MS5C1UnlockCategory.Equipment) equipment++;
            else if (d.category == MS5C1UnlockCategory.Achievement) achievements++;
            if (d.category == MS5C1UnlockCategory.Achievement && !d.achievementOnly) itemSemantics = false;
            if (d.achievementOnly && d.category != MS5C1UnlockCategory.Achievement) itemSemantics = false;
            if (!d.achievementOnly && !d.starterUnlocked && d.price > 0) purchaseGold += d.price;
        }
        Check("Seven definitions", definitions.Length == 7, ref pass, ref fail, report);
        Check("All definitions and IDs are non-null", allNonNull, ref pass, ref fail, report);
        Check("IDs are unique", allIdsUnique, ref pass, ref fail, report);
        Check("Exactly three maps", maps == 3, ref pass, ref fail, report);
        Check("Exactly two equipment licenses", equipment == 2, ref pass, ref fail, report);
        Check("Exactly two Boss achievements", achievements == 2, ref pass, ref fail, report);
        Check("Exactly one free starter", starters == 1 && starterFree, ref pass, ref fail, report);
        Check("All prices nonnegative", pricesNonnegative, ref pass, ref fail, report);
        Check("Boss achievements are never purchasable", achieveOnlyNotSold && itemSemantics, ref pass, ref fail, report);
        Check("Meadow starter", Is("map.green_meadow", 0, MS5C1UnlockCategory.Map, true, false), ref pass, ref fail, report);
        Check("Sand Dunes price", Is("map.sand_dunes", 250, MS5C1UnlockCategory.Map, false, false), ref pass, ref fail, report);
        Check("Ancient Ruins price", Is("map.ancient_ruins", 650, MS5C1UnlockCategory.Map, false, false), ref pass, ref fail, report);
        Check("Reaper Edge license price", Is("equipment.reaper_edge", 800, MS5C1UnlockCategory.Equipment, false, false), ref pass, ref fail, report);
        Check("Warden's Oath license price", Is("equipment.wardens_oath", 1100, MS5C1UnlockCategory.Equipment, false, false), ref pass, ref fail, report);
        Check("Boss achievement IDs", Is("achievement.shadow_titan", 0, MS5C1UnlockCategory.Achievement, false, true)
              && Is("achievement.grave_warden", 0, MS5C1UnlockCategory.Achievement, false, true), ref pass, ref fail, report);
        Check("All unlock purchases total 2,800 Gold", purchaseGold == 2800, ref pass, ref fail, report);
        report.AppendLine("RESULT " + pass + "/" + (pass + fail) + " checks passed.");
        report.AppendLine("IMPORTANT: These are pure catalog checks, NOT tests of Save writes, UI clicks or gameplay.");
        if (fail == 0) Debug.Log(report.ToString());
        else Debug.LogError(report.ToString());
        EditorUtility.DisplayDialog("5C.5 Catalog Self-Test", pass + "/" + (pass + fail) + " catalog checks passed.\nNo Save files opened or modified.\nSee Console for details.", "OK");
    }

    private static bool Is(string id, int price, MS5C1UnlockCategory category, bool starter, bool achievement)
    {
        MS5C1UnlockDefinition def;
        return MS5C1UnlockCatalog.TryGet(id, out def) && def != null && def.price == price &&
               def.category == category && def.starterUnlocked == starter && def.achievementOnly == achievement;
    }

    private static void Check(string title, bool success, ref int pass, ref int fail, StringBuilder log)
    {
        if (success) pass++;
        else fail++;
        log.AppendLine("[" + (success ? "PASS" : "FAIL") + "] " + title);
    }
}
