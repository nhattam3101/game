#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// 5E: ALL checks are read-only. No TrySave, TryPurchase, scene edits or PlayerPrefs writes.
public sealed class MS5EProgressionFinalQAWindow : EditorWindow
{
    private const string Menu = "Tools/MonsterSurvival/5E Progression Final QA/";
    private readonly List<string> report = new List<string>();
    private readonly bool[] manual = new bool[15];
    private readonly string[] manualItems = {
        "01. PROFILE opens / closes and all three tabs work",
        "02. 8 Statistics icons and Progress Summary display",
        "03. 1920x1080 / 1280x720 / 1024x768 / 738x723 readable and scrollable",
        "04. Switching Profile 01/02/03 shows independent values",
        "05. Purchase Upgrade subtracts Gold once, correct revision, survives restart",
        "06. Max/insufficient Gold blocks Upgrade; SFX matches the result",
        "07. Permanent HP/Damage/ASPD/Movement bonuses apply ONCE",
        "08. Equip/Unequip + Skill level-up do not overwrite permanent bonuses",
        "09. New Profile cannot START locked map; purchased map can START",
        "10. Legacy Profile policy does not revoke access unexpectedly",
        "11. UNLOCK subtracts Gold once; no charge on CANCEL / duplicate click",
        "12. Boss spawned by WaveManager grants correct achievement ONCE",
        "13. Boss test prefab placed in scene gives no unintended achievement",
        "14. Victory commits Gold + 20 Inventory + 3 Equipment once; defeat does not",
        "15. Save/Stats survive Unity restart and backup recovery tested on DISPOSABLE data"
    };
    private Vector2 scroll;
    private string auditedAt = "Not audited";
    private int pass, warn, error;
    private bool watching;
    private double nextSample;
    private string lastSignature = "";
    private int observedSamples;
    private string lastSelfTest = "NOT RUN";

    [MenuItem(Menu + "1 - OPEN Final Cross-System QA (READ ONLY)")]
    public static void OpenWindow()
    {
        MS5EProgressionFinalQAWindow w = GetWindow<MS5EProgressionFinalQAWindow>("5E Progression QA");
        w.minSize = new Vector2(730, 520);
        w.Show();
    }

    private void OnEnable() { EditorApplication.update += ObserveUpdate; }
    private void OnDisable() { EditorApplication.update -= ObserveUpdate; watching = false; }

    private void OnGUI()
    {
        GUILayout.Label("MONSTERSURVIVAL 5E — FINAL PROGRESSION QA", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("READ ONLY. Scan the OPEN Scene and 3 Profiles. Does not buy, save, restore, create QA profiles, change Scene or edit Player components. Gameplay tests are manual.", MessageType.Info);
        EditorGUILayout.LabelField("Last audit: " + auditedAt + "   |   PASS " + pass + " / WARN " + warn + " / ERROR " + error, EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("AUDIT OPEN SCENE + 3 PROFILES", GUILayout.Height(32))) RunAudit();
            if (GUILayout.Button("PURE MATH SELF-TEST", GUILayout.Height(32))) RunMathTest();
            if (GUILayout.Button("EXPORT REPORT TXT", GUILayout.Height(32))) Export();
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = EditorApplication.isPlaying;
            if (GUILayout.Button(watching ? "STOP PLAY MODE OBSERVER" : "START PLAY MODE OBSERVER", GUILayout.Height(26)))
            {
                watching = !watching;
                if (watching) { lastSignature = ""; nextSample = 0d; observedSamples = 0; Info("LIVE OBSERVER STARTED — no scene or save writes"); }
                else Info("LIVE OBSERVER STOPPED");
            }
            GUI.enabled = true;
            if (GUILayout.Button("RESET MANUAL CHECKMARKS", GUILayout.Height(26))) Array.Clear(manual, 0, manual.Length);
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        GUILayout.Label("Automated observations", EditorStyles.boldLabel);
        foreach (string line in report) EditorGUILayout.SelectableLabel(line, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(20));
        EditorGUILayout.Space(12);
        GUILayout.Label("Manual Play Mode confirmation (NOT automatically verified)", EditorStyles.boldLabel);
        for (int i = 0; i < manual.Length; i++) manual[i] = EditorGUILayout.ToggleLeft(manualItems[i], manual[i]);
        EditorGUILayout.EndScrollView();
        EditorGUILayout.LabelField("Self test: " + lastSelfTest + " | Live samples: " + observedSamples + " | Checked manually: " + ManualCount() + "/" + manual.Length);
    }

    private int ManualCount()
    {
        int count = 0;
        foreach (bool b in manual) if (b) count++;
        return count;
    }
    private void Pass(bool good, string item, string detail = "")
    {
        if (good) pass++; else error++;
        report.Add((good ? "[PASS] " : "[ERROR] ") + item + (string.IsNullOrEmpty(detail) ? "" : " — " + detail));
    }
    private void Warning(string item) { warn++; report.Add("[WARN] " + item); }
    private void Info(string item) { report.Add("[INFO] " + item); Repaint(); }

    private static T FindScene<T>(Scene scene) where T : Component
    {
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found) return found;
        }
        return null;
    }
    private static int CountScene<T>(Scene scene) where T : Component
    {
        if (!scene.IsValid() || !scene.isLoaded) return 0;
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
            count += root.GetComponentsInChildren<T>(true).Length;
        return count;
    }
    private static int UpgradeLevel(MS5A1SaveData data, string id)
    {
        if (data == null || data.permanentUpgrades == null) return 0;
        foreach (MS5A1UpgradeEntry e in data.permanentUpgrades)
            if (e != null && e.id == id) return e.level;
        return 0;
    }
    private static bool Has(MS5A1SaveData data, string id)
    {
        return data != null && data.unlockedContentIds != null && data.unlockedContentIds.Contains(id);
    }
    private void RunAudit()
    {
        report.Clear(); pass = warn = error = 0;
        Scene scene = SceneManager.GetActiveScene();
        auditedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        Info("Unity " + Application.unityVersion + " | Scene " + scene.name + " | " + (EditorApplication.isPlaying ? "PLAY" : "EDIT") + " | Profile selected " + MS5A2GoldBank.SelectedSlot);
        if (scene.name == "MainMenu") AuditMenu(scene);
        else if (scene.name == "SampleScene") AuditGameplay(scene);
        else Warning("Current Scene is not MainMenu or SampleScene. Open each separately for complete wiring checks.");
        AuditCatalog();
        for (int slot = 1; slot <= 3; slot++) AuditSlot(slot);
        Info("RESULT " + pass + " PASS / " + warn + " WARN / " + error + " ERROR. Manual confirmations " + ManualCount() + "/15 are NOT auto-verified.");
        Info("NOTE: Stats5D3 is an independent file: absence is not evidence of zero historical wins. Backup-only saves must not be silently written.");
        Repaint();
    }

    private void AuditMenu(Scene scene)
    {
        var profile = FindScene<MS5D1ProfileOverview>(scene);
        var polish = FindScene<MS5D2ProfilePolish>(scene);
        var statistics = FindScene<MS5D3StatisticsTab>(scene);
        var ribbon = FindScene<MS5D4ProgressRibbon>(scene);
        Pass(profile && profile.launcher && profile.existingProfileWidget, "5D.1 Profile launcher / widget references");
        Pass(polish && profile && polish.gameObject == profile.gameObject && polish.originalOverview == profile, "5D.2 responsive overlay attached to Profile root");
        Pass(statistics && polish && statistics.gameObject == polish.gameObject && statistics.profilePolish == polish, "5D.3 Stats tab wiring");
        Pass(ribbon && polish && statistics && ribbon.profilePolish == polish && ribbon.statisticsTab == statistics, "5D.4 Progress Summary wiring");
        if (statistics)
        {
            Sprite[] sprites = { statistics.victoriesIcon, statistics.victoryGoldIcon, statistics.shadowTitanIcon,
                statistics.graveWardenIcon, statistics.mapsIcon, statistics.bossBadgesIcon,
                statistics.upgradesIcon, statistics.licensesIcon };
            int ready = 0; foreach (Sprite sprite in sprites) if (sprite) ready++;
            Pass(ready == 8, "5D.3.2 Statistics icons", ready + "/8");
        }
        Pass(CountScene<MS5D1ProfileOverview>(scene) == 1, "No duplicate ProfileOverview component");
        Pass(CountScene<MS5D4ProgressRibbon>(scene) == 1, "No duplicate ProgressRibbon component");
        var menu = FindScene<MMV3MenuController>(scene);
        Pass(menu && menu.homePage && menu.mapsPage && menu.settingsPage && menu.mapsStartButton, "Main Menu V3 pages / Start references");
        Pass(CountScene<MS5C3MainMenuGate>(scene) == 1, "5C.3 START gate attached once");
        Pass(CountScene<MS5C2MapUnlockUI>(scene) == 1, "5C.2 map unlock UI attached once");
        Pass(CountScene<MS5C4AchievementGallery>(scene) == 1, "5C.4 achievement gallery attached once");
    }

    private void AuditGameplay(Scene scene)
    {
        MS5A3PersistentRun run = FindScene<MS5A3PersistentRun>(scene);
        MS5B3PermanentBonusApplier applier = FindScene<MS5B3PermanentBonusApplier>(scene);
        Pass(CountScene<MS5A3PersistentRun>(scene) == 1, "5A.3 unified Victory save controller exactly once");
        Pass(run && run.gameManager && run.progression && run.inventory && run.equipment && run.itemRegistry, "5A.3 Victory save dependencies wired");
        if (run && run.legacyGoldWatcher && run.legacyGoldWatcher.enabled)
            Warning("5A.2 LEGACY GOLD WATCHER ENABLED: risk of duplicate Gold deposits. Do not auto-fix; check original installer.");
        else Pass(run != null, "Legacy Gold watcher not active (when unified save exists)");
        Pass(CountScene<MS5B3PermanentBonusApplier>(scene) == 1, "5B.3 permanent-bonus applier exactly once");
        Pass(applier && applier.playerStats && applier.playerHealth && applier.playerStats.gameObject == applier.playerHealth.gameObject, "5B.3 Health and PlayerStats references consistent");
        Pass(CountScene<MS5C3GameplayGate>(scene) == 1, "5C.3 gameplay gate exactly once");
        Pass(CountScene<MS5D3RunTracker>(scene) == 1, "5D.3 game statistics tracker exactly once");
        if (EditorApplication.isPlaying && run)
            Info("5A.3 CURRENT RUN " + run.Status + " | committed=" + run.Committed);
        if (EditorApplication.isPlaying && applier)
            Info("5B.3 CURRENT RUN " + applier.Status + " | applied=" + applier.IsApplied + " | profile=" + applier.LockedProfileSlot);
    }

    private void AuditCatalog()
    {
        MS5B1UpgradeDefinition[] upgrades = MS5B1UpgradeDefinitions.GetAll();
        var upgradeIds = new HashSet<string>(StringComparer.Ordinal);
        int goldToMax = 0;
        foreach (MS5B1UpgradeDefinition item in upgrades)
        {
            if (item == null) continue;
            if (!upgradeIds.Add(item.id)) Warning("Duplicate upgrade ID: " + item.id);
            for (int i = 0; i < item.maxLevel; i++) goldToMax += item.PriceForNextLevel(i);
        }
        Pass(upgrades.Length == 4 && upgradeIds.Count == 4, "5B Permanent Upgrade Catalog: four unique IDs");
        MS5C1UnlockDefinition[] unlocks = MS5C1UnlockCatalog.GetAll();
        var unlockIds = new HashSet<string>(StringComparer.Ordinal);
        int unlockCost = 0;
        int starters = 0;
        foreach (MS5C1UnlockDefinition item in unlocks)
        {
            if (item == null) continue;
            if (!unlockIds.Add(item.id)) Warning("Duplicate unlock ID: " + item.id);
            if (item.starterUnlocked) starters++;
            if (!item.starterUnlocked && !item.achievementOnly) unlockCost += item.price;
        }
        Pass(unlocks.Length == 7 && unlockIds.Count == 7 && starters == 1, "5C Unlock Catalog: 7 unique IDs / 1 starter");
        Info("BALANCE FORECAST: Upgrade max cost=" + goldToMax + "; all unlock purchases=" + unlockCost + "; combined=" + (goldToMax + unlockCost) + ". No Gold changed.");
    }

    private void AuditSlot(int slot)
    {
        MS5A1SaveData data;
        string message;
        bool recovered;
        bool loaded = MS5A1SaveService.TryLoad(slot, out data, out message, out recovered);
        if (!loaded)
        {
            if (!string.IsNullOrEmpty(message) && message.StartsWith("NO_SAVE", StringComparison.Ordinal))
                Info("PROFILE " + slot + ": new / no Save file. No files created.");
            else Warning("PROFILE " + slot + ": Save unavailable. " + message);
            AuditStats(slot);
            return;
        }
        Pass(data != null && data.bankGold >= 0 && data.revision >= 0, "Profile " + slot + " verified 5A Save", "Gold=" + data.bankGold + " revision=" + data.revision);
        if (recovered) Warning("Profile " + slot + " was read from .bak only; treat as READ ONLY, may be older than last successful save.");
        if (data.inventorySnapshotPresent)
        {
            bool shapeOk = data.inventorySlots != null && data.inventorySlots.Count == 20 &&
                           data.equippedItems != null && data.equippedItems.Count == 3;
            Pass(shapeOk, "Profile " + slot + " 5A.3 20 Inventory + 3 Equipment shape");
            if (shapeOk)
            {
                int used = 0, worn = 0;
                bool valid = true;
                for (int i = 0; i < 20; i++)
                {
                    MS5A3SavedStack entry = data.inventorySlots[i];
                    if (entry == null || entry.quantity < 0 ||
                        (entry.quantity == 0 && !string.IsNullOrEmpty(entry.itemId)) ||
                        (entry.quantity > 0 && string.IsNullOrEmpty(entry.itemId))) valid = false;
                    if (entry != null && entry.quantity > 0) used++;
                }
                for (int i = 0; i < 3; i++)
                {
                    MS5A3SavedEquipped gear = data.equippedItems[i];
                    if (gear == null || gear.type != i) valid = false;
                    if (gear != null && !string.IsNullOrEmpty(gear.itemId)) worn++;
                }
                Pass(valid, "Profile " + slot + " inventory quantities / equipment slot orders", "used=" + used + "/20, gear=" + worn + "/3");
            }
        }
        else Info("Profile " + slot + " has no Inventory snapshot (legacy profile; not automatically an error).");
        bool upgradeRangeOk = true;
        var upgradeNames = new HashSet<string>(StringComparer.Ordinal);
        if (data.permanentUpgrades != null)
        {
            foreach (MS5A1UpgradeEntry entry in data.permanentUpgrades)
            {
                if (entry == null) { upgradeRangeOk = false; continue; }
                MS5B1UpgradeDefinition def;
                if (!MS5B1UpgradeDefinitions.TryGet(entry.id, out def) || def == null || entry.level > def.maxLevel || entry.level < 0 || !upgradeNames.Add(entry.id)) upgradeRangeOk = false;
            }
        }
        Pass(upgradeRangeOk, "Profile " + slot + " 5B upgrade levels valid (0..10)");
        int vitality = UpgradeLevel(data, "vitality");
        int strength = UpgradeLevel(data, "strength");
        int fury = UpgradeLevel(data, "fury");
        int agility = UpgradeLevel(data, "agility");
        MS5B3BonusValues bonus; string bonusError;
        bool bonusOk = MS5B3BonusMath.TryCalculate(vitality, strength, fury, agility, out bonus, out bonusError);
        Pass(bonusOk, "Profile " + slot + " 5B bonus math", bonusOk ? "HP+" + bonus.maxHealth + " DMG+" + bonus.damage + " ASPD+" + bonus.attackSpeed + " MOV+" + bonus.moveSpeed : bonusError);
        MS5C3MapPolicy.State meadow, sand, ruins;
        bool m0 = MS5C3MapPolicy.TryGet(slot, 0, out meadow);
        bool m1 = MS5C3MapPolicy.TryGet(slot, 1, out sand);
        bool m2 = MS5C3MapPolicy.TryGet(slot, 2, out ruins);
        Pass(m0 && m1 && m2, "Profile " + slot + " map policy reads 3 maps");
        if (m0 && m1 && m2)
        {
            Pass(meadow.allowed, "Profile " + slot + " Meadow accessible");
            Info("Profile " + slot + " Sand=" + sand.allowed + " Ruins=" + ruins.allowed + " | Legacy=" + meadow.legacy + " | Purchased Sand=" + Has(data, "map.sand_dunes") + " Ruins=" + Has(data, "map.ancient_ruins"));
        }
        Info("Profile " + slot + " achievements: Titan=" + Has(data, "achievement.shadow_titan") + " Warden=" + Has(data, "achievement.grave_warden") + " | gear licenses Reaper=" + Has(data, "equipment.reaper_edge") + " Oath=" + Has(data, "equipment.wardens_oath"));
        AuditStats(slot);
    }

    private void AuditStats(int slot)
    {
        MS5D3StatsData stats; string result; bool backupOnly;
        bool ok = MS5D3StatsStore.TryRead(slot, out stats, out result, out backupOnly);
        if (!ok)
        {
            if (!string.IsNullOrEmpty(result) && result.StartsWith("NO_STATS", StringComparison.Ordinal)) Info("Profile " + slot + ": no 5D.3 stats yet, history not reconstructed.");
            else Warning("Profile " + slot + ": stats invalid/unreadable: " + result);
            return;
        }
        Pass(stats != null && stats.slot == slot && stats.victories >= 0 && stats.totalVictoryGold >= 0 && stats.shadowTitanKills >= 0 && stats.graveWardenKills >= 0,
             "Profile " + slot + " verified 5D.3 statistics", "wins=" + stats.victories + " totalGold=" + stats.totalVictoryGold + " Titan=" + stats.shadowTitanKills + " Warden=" + stats.graveWardenKills);
        if (backupOnly) Warning("Profile " + slot + ": stats read from backup only. Do not overwrite.");
        if (stats != null && stats.committedEventIds != null)
        {
            var uniq = new HashSet<string>(stats.committedEventIds, StringComparer.Ordinal);
            Pass(uniq.Count == stats.committedEventIds.Count, "Profile " + slot + " stats idempotency event IDs unique");
        }
    }

    private void RunMathTest()
    {
        var lines = MS5EPureSelfTest.Run();
        lastSelfTest = lines;
        Info("PURE TEST: " + lines);
    }

    private void ObserveUpdate()
    {
        if (!watching) return;
        if (!EditorApplication.isPlaying) { watching = false; lastSignature = ""; Repaint(); return; }
        double now = EditorApplication.timeSinceStartup;
        if (now < nextSample) return;
        nextSample = now + .5d;
        observedSamples++;
        Scene scene = SceneManager.GetActiveScene();
        int slot = MS5A2GoldBank.SelectedSlot;
        MS5A1SaveData data; string reason; bool recovered;
        bool valid = MS5A1SaveService.TryLoad(slot, out data, out reason, out recovered);
        int gold = valid ? data.bankGold : -1;
        int rev = valid ? data.revision : -1;
        MS5A3PersistentRun run = FindScene<MS5A3PersistentRun>(scene);
        MS5B3PermanentBonusApplier bonus = FindScene<MS5B3PermanentBonusApplier>(scene);
        GameManager gm = FindScene<GameManager>(scene);
        string signature = "Scene=" + scene.name + " Slot=" + slot + " Rev=" + rev + " Gold=" + gold +
            " Save=" + (valid ? (recovered ? "BACKUP" : "VALID") : reason) +
            " Run=" + (run ? run.Status : "n/a") + " Bonus=" + (bonus ? bonus.Status : "n/a") +
            " GameOver=" + (gm ? gm.IsGameOver.ToString() : "n/a") + " tScale=" + Time.timeScale.ToString("0.00");
        if (signature == lastSignature) return;
        lastSignature = signature;
        report.Add("[LIVE " + DateTime.Now.ToString("HH:mm:ss") + "] " + signature);
        // Cap log to avoid Editor UI overload on long sessions.
        if (report.Count > 700) report.RemoveRange(0, 100);
        Repaint();
    }

    private void Export()
    {
        string suggested = "MS5E_FinalQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
        string path = EditorUtility.SaveFilePanel("Export 5E Final QA report", Path.GetDirectoryName(Application.dataPath), suggested, "txt");
        if (string.IsNullOrEmpty(path)) return;
        var sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 5E - FINAL PROGRESSION QA / READ-ONLY");
        sb.AppendLine("Generated " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | Unity " + Application.unityVersion);
        sb.AppendLine("Last audited " + auditedAt + " | PASS=" + pass + " WARN=" + warn + " ERROR=" + error);
        sb.AppendLine("Pure math self test: " + lastSelfTest);
        sb.AppendLine("Observed samples " + observedSamples);
        sb.AppendLine("--- AUTOMATED AUDIT / LIVE EVENTS ---");
        foreach (string entry in report) sb.AppendLine(entry);
        sb.AppendLine("--- MANUAL CONFIRMATIONS (only checked by user) ---");
        for (int i = 0; i < manual.Length; i++) sb.AppendLine((manual[i] ? "[USER CONFIRMED] " : "[NOT CONFIRMED] ") + manualItems[i]);
        sb.AppendLine("Manual checks cannot be automatically verified. No file saves or purchases were performed by this audit.");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        Debug.Log("[5E] Exported report: " + path);
        EditorUtility.RevealInFinder(path);
    }
}
#endif
