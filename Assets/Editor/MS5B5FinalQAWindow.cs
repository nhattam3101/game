#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 5B.5 - READ-ONLY audit. Never writes a profile, scene, PlayerPrefs, or gameplay object.
/// No automatic purchase, no fake Gameplay PASS. Export only with explicit user action.
/// </summary>
public sealed class MS5B5FinalQAWindow : EditorWindow
{
    private const string Menu = "Tools/MonsterSurvival/5B Permanent Upgrades/";
    private static readonly string[] Keys = { "vitality", "strength", "fury", "agility" };
    private static readonly string[] Names = {
        "01. Main Menu shop opens/closes; legacy menus still work",
        "02. Shop shows correct Bank Gold and selected Profile",
        "03. Affordable UPGRADE deducts Gold exactly once",
        "04. Double-click / stale purchase does not charge twice",
        "05. Insufficient Gold / MAX prevents purchase",
        "06. Purchases survive close + reopen Unity",
        "07. Profile 02/03 has independent upgrades",
        "08. Vitality Max HP and current HP match expected bonus",
        "09. Strength / Fury / Agility bonuses apply ONCE",
        "10. Equip/Unequip does not remove permanent bonuses",
        "11. Level-up Skill does not remove permanent bonuses",
        "12. Restart, Game Over and Victory preserve purchased levels",
        "13. VFX and audio follow successful purchase, no fake success",
        "14. Inventory/Equipment and Gold Save 5A remain functional"
    };

    [Serializable]
    private sealed class CheckRow
    {
        public string label;
        public string severity;
        public string details;
        public CheckRow(string severity, string label, string details)
        { this.severity = severity; this.label = label; this.details = details; }
    }

    private readonly List<CheckRow> checks = new List<CheckRow>();
    private readonly bool[] manual = new bool[14];
    private readonly List<string> runtimeEvents = new List<string>();
    private Vector2 scroll;
    private bool monitor;
    private bool showManual = true;
    private bool showBalance = true;
    private double nextPoll;
    private string lastRuntimeLine = "";
    private string lastAuditScene = "";
    private string lastAuditTime = "Never";
    private int passCount, warnCount, failCount;

    [MenuItem(Menu + "14 - OPEN 5B.5 FINAL QA + BALANCE (READ ONLY)")]
    public static void Open()
    {
        MS5B5FinalQAWindow window = GetWindow<MS5B5FinalQAWindow>();
        window.titleContent = new GUIContent("5B.5 Final QA");
        window.minSize = new Vector2(680, 510);
        window.Show();
        window.RunAudit();
    }

    private void OnEnable() { EditorApplication.update += Poll; }
    private void OnDisable() { EditorApplication.update -= Poll; monitor = false; }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("MONSTERSURVIVAL / 5B.5 — FINAL QA & BALANCE", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("READ ONLY. All profile snapshots and runtime component values are observed; no saves or scenes are changed. Manual confirmations are NOT automatic PASS results.", MessageType.Info);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("AUDIT OPEN SCENE + 3 PROFILES", GUILayout.Height(27))) RunAudit();
        if (GUILayout.Button(monitor ? "STOP PLAY MODE MONITOR" : "START PLAY MODE MONITOR", GUILayout.Height(27)))
        {
            monitor = !monitor;
            if (monitor) { runtimeEvents.Clear(); lastRuntimeLine = ""; nextPoll = 0; Record("Monitor started (read only)."); }
            else Record("Monitor stopped.");
        }
        if (GUILayout.Button("EXPORT TXT", GUILayout.Height(27))) ExportReport();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("Last audit: " + lastAuditTime + " | Scene: " + lastAuditScene);
        EditorGUILayout.LabelField("Auto audit: " + passCount + " PASS / " + warnCount + " WARN / " + failCount + " ERROR");
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("SCENE / PROFILE AUDIT", EditorStyles.boldLabel);
        foreach (CheckRow r in checks)
        {
            Color previous = GUI.contentColor;
            GUI.contentColor = r.severity == "ERROR" ? new Color(1f,.42f,.42f) :
                               r.severity == "WARN" ? new Color(1f,.77f,.35f) :
                               r.severity == "PASS" ? new Color(.58f,.91f,.67f) : Color.white;
            EditorGUILayout.LabelField("[" + r.severity + "] " + r.label, EditorStyles.boldLabel);
            GUI.contentColor = previous;
            if (!string.IsNullOrEmpty(r.details)) EditorGUILayout.LabelField(r.details, EditorStyles.wordWrappedMiniLabel);
        }
        EditorGUILayout.Space(10);
        showBalance = EditorGUILayout.Foldout(showBalance, "BALANCE FORECAST (PROPOSED PRICES, NO CHANGES)", true);
        if (showBalance) DrawBalance();
        EditorGUILayout.Space(8);
        showManual = EditorGUILayout.Foldout(showManual, "MANUAL PLAY MODE CONFIRMATIONS", true);
        if (showManual)
        {
            int done = 0; for (int i=0;i<manual.Length;i++) if (manual[i]) done++;
            EditorGUILayout.LabelField("User-confirmed: " + done + "/" + manual.Length + " (not automatic)");
            for (int i = 0; i < Names.Length; i++) manual[i] = EditorGUILayout.ToggleLeft(Names[i], manual[i]);
            if (GUILayout.Button("CLEAR MANUAL CHECKMARKS")) Array.Clear(manual, 0, manual.Length);
        }
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("RUNTIME TRACE (latest 75 state transitions)", EditorStyles.boldLabel);
        if (runtimeEvents.Count == 0) EditorGUILayout.LabelField("Start Play Mode monitor, then use MainMenu / Shop / Gameplay.", EditorStyles.wordWrappedMiniLabel);
        for (int i=Math.Max(0,runtimeEvents.Count-35);i<runtimeEvents.Count;i++)
            EditorGUILayout.LabelField(runtimeEvents[i], EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndScrollView();
    }

    private void Add(string severity, string label, string details = "")
    {
        checks.Add(new CheckRow(severity, label, details));
        if (severity == "PASS") passCount++;
        else if (severity == "ERROR") failCount++;
        else if (severity == "WARN") warnCount++;
    }

    private void RunAudit()
    {
        checks.Clear(); passCount = 0; warnCount = 0; failCount = 0;
        lastAuditScene = SceneManager.GetActiveScene().name;
        lastAuditTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        try
        {
            if (lastAuditScene == "MainMenu") AuditMainMenu();
            else if (lastAuditScene == "SampleScene") AuditGameplay();
            else Add("WARN", "Open MainMenu or SampleScene to audit UI/gameplay references.", "Current scene: " + lastAuditScene);
            AuditProfiles();
            AuditCatalogMath();
        }
        catch (Exception ex) { Add("ERROR", "Audit exception", ex.GetType().Name + ": " + ex.Message); }
        Repaint();
    }

    private void AuditMainMenu()
    {
        MS5B2ShopUI[] shops = UnityEngine.Object.FindObjectsByType<MS5B2ShopUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (shops.Length == 1) Add("PASS", "5B.2 Shop UI: exactly one component");
        else Add("ERROR", "5B.2 Shop UI count: " + shops.Length, "Expected one MS5B2ShopUI in MainMenu scene.");
        MS5B4PurchaseFeedback[] fx = UnityEngine.Object.FindObjectsByType<MS5B4PurchaseFeedback>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (fx.Length == 1 && shops.Length == 1 && fx[0].gameObject == shops[0].gameObject)
        {
            bool clipsPresent = fx[0].successClip && fx[0].blockedClip;
            Add(clipsPresent ? "PASS" : "WARN", "5B.4 VFX attached to 5B.2 Shop", "Success and blocked clips: " +
                (fx[0].successClip ? "success OK" : "success MISSING") + ", " + (fx[0].blockedClip ? "blocked OK" : "blocked MISSING"));
        }
        else Add("WARN", "5B.4 VFX missing or attached elsewhere", "Observed " + fx.Length + " MS5B4PurchaseFeedback components.");
        if (shops.Length == 1 && shops[0].openButton != null)
            Add("PASS", "UPGRADES launcher Button reference assigned");
        else Add("WARN", "UPGRADES launcher reference missing");
        if (shops.Length == 1 && shops[0].profileWidget != null)
            Add("PASS", "5A.2 Profile widget reference assigned");
        else Add("WARN", "5A.2 Profile widget not connected to Shop");
    }

    private void AuditGameplay()
    {
        MS5B3PermanentBonusApplier[] appliers = UnityEngine.Object.FindObjectsByType<MS5B3PermanentBonusApplier>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (appliers.Length != 1) { Add("ERROR", "5B.3 Applier count=" + appliers.Length, "Exactly one component expected on Player."); return; }
        MS5B3PermanentBonusApplier applier = appliers[0];
        if (applier.playerStats && applier.playerHealth && applier.playerStats.gameObject == applier.gameObject && applier.playerHealth.gameObject == applier.gameObject)
            Add("PASS", "5B.3 PlayerStats + Health references connected");
        else Add("ERROR", "5B.3 Player component references missing/mismatched");
        MS5A3PersistentRun[] runs = UnityEngine.Object.FindObjectsByType<MS5A3PersistentRun>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (runs.Length == 1 && runs[0].enabled) Add("PASS", "Unified 5A.3 Save watcher active");
        else Add("WARN", "5A.3 Save watcher count/enabled: " + runs.Length);
        if (EditorApplication.isPlaying)
        {
            int slot = applier.LockedProfileSlot;
            if (applier.ProfileReadSuccessfully && applier.IsApplied)
                Add("PASS", "5B.3 runtime applied, locked profile " + slot, applier.Status);
            else Add("WARN", "5B.3 has not applied", applier.Status);
            MS5B1PermanentUpgrades.Snapshot snapshot;
            string msg;
            if (slot >= 1 && slot <= 3 && MS5B1PermanentUpgrades.TryGetSnapshot(slot, out snapshot, out msg))
            {
                MS5B3BonusValues expected; string error;
                if (MS5B3BonusMath.TryFromSnapshot(snapshot, out expected, out error) && applier.IsApplied)
                {
                    MS5B3BonusValues actual = applier.AppliedBonuses;
                    bool same = EqualBonus(actual, expected);
                    Add(same ? "PASS" : "WARN", "5B.3 applied bonus snapshot comparison", same ?
                        "Applied values match saved upgrade levels." : "Mismatch; expected/current may differ if profile was bought AFTER run started.");
                }
            }
        }
        else Add("INFO", "Runtime bonus comparison requires Play Mode");
    }

    private static bool Near(float a, float b) { return Mathf.Abs(a-b) <= .0005f; }
    private static bool EqualBonus(MS5B3BonusValues a, MS5B3BonusValues b)
    {
        return Near(a.maxHealth,b.maxHealth) && Near(a.damage,b.damage) &&
            Near(a.attackSpeed,b.attackSpeed) && Near(a.moveSpeed,b.moveSpeed) &&
            a.vitalityLevel==b.vitalityLevel && a.strengthLevel==b.strengthLevel &&
            a.furyLevel==b.furyLevel && a.agilityLevel==b.agilityLevel;
    }

    private void AuditProfiles()
    {
        for (int slot=1; slot<=3; slot++)
        {
            MS5B1PermanentUpgrades.Snapshot snap; string msg;
            bool ok = MS5B1PermanentUpgrades.TryGetSnapshot(slot, out snap, out msg);
            if (!ok) { Add("ERROR", "Profile " + slot + " unavailable", msg); continue; }
            if (snap.isNewProfile) { Add("INFO", "Profile " + slot + " new (no save yet)", "No file will be created by this audit."); continue; }
            int[] levels = {snap.LevelOf("vitality"),snap.LevelOf("strength"),snap.LevelOf("fury"),snap.LevelOf("agility")};
            string info = "Gold=" + snap.bankGold + " rev=" + snap.revision +
                " | Vitality="+levels[0]+" Strength="+levels[1]+" Fury="+levels[2]+" Agility="+levels[3];
            MS5A1SaveData raw; bool recovered; string readMessage;
            bool loaded = MS5A1SaveService.TryLoad(slot, out raw, out readMessage, out recovered);
            if (loaded && !recovered && snap.bankGold>=0) Add("PASS", "Profile " + slot + " valid", info);
            else Add("WARN", "Profile " + slot + " loaded from backup / invalid Bank Gold", info + " | " + readMessage);
        }
    }

    private void AuditCatalogMath()
    {
        MS5B1UpgradeDefinition[] catalog = MS5B1UpgradeDefinitions.GetAll();
        if (catalog.Length != 4) Add("ERROR", "Catalog expected 4 upgrade definitions", "Actual " + catalog.Length);
        else Add("PASS", "Catalog has four upgrades");
        int total = 0;
        HashSet<string> ids = new HashSet<string>();
        for (int i=0;i<catalog.Length;i++)
        {
            MS5B1UpgradeDefinition def = catalog[i];
            if (def == null) { Add("ERROR", "Null upgrade definition index " + i); continue; }
            bool valid = ids.Add(def.id) && def.maxLevel==10 && def.baseCost>0 &&
                def.PriceForNextLevel(0)==def.baseCost &&
                def.PriceForNextLevel(9)==10*def.baseCost &&
                def.PriceForNextLevel(10)==-1;
            Add(valid?"PASS":"ERROR", "Price curve " + def.id, "Initial Gold=" + def.baseCost + " | Lv.10 purchase="+def.baseCost*10);
            total += def.baseCost * 55;
        }
        Add(total==12375?"PASS":"WARN", "Full max-level Bank Gold forecast: " + total, "The current catalog requires 55 x sum(base price) = 12,375 Gold to max all upgrades.");
        MS5B3BonusValues zero, max; string error0,error1;
        bool z = MS5B3BonusMath.TryCalculate(0,0,0,0,out zero,out error0);
        bool m = MS5B3BonusMath.TryCalculate(10,10,10,10,out max,out error1);
        bool good = z && m && Near(zero.maxHealth,0f) && Near(max.maxHealth,100f) &&
            Near(max.damage,.5f) && Near(max.attackSpeed,.3f) && Near(max.moveSpeed,.2f);
        Add(good?"PASS":"ERROR", "Bonus mathematical extremes (level 0 / 10)",
            "Max target: +100 HP / +0.50 Damage / +0.30 AttackSpeed / +0.20 MoveSpeed");
    }

    private void DrawBalance()
    {
        EditorGUILayout.HelpBox("These values are a read-only forecast from the 5B.1 catalog, NOT recommended automatic economy changes. Enemy Gold income varies by actual gameplay.", MessageType.None);
        MS5B1UpgradeDefinition[] catalog = MS5B1UpgradeDefinitions.GetAll();
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Upgrade", GUILayout.Width(125));
        GUILayout.Label("L1", GUILayout.Width(65));
        GUILayout.Label("L5", GUILayout.Width(65));
        GUILayout.Label("L10", GUILayout.Width(65));
        GUILayout.Label("All 10", GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();
        foreach (MS5B1UpgradeDefinition d in catalog)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(d.title, GUILayout.Width(125));
            GUILayout.Label(d.PriceForNextLevel(0).ToString(), GUILayout.Width(65));
            GUILayout.Label(d.PriceForNextLevel(4).ToString(), GUILayout.Width(65));
            GUILayout.Label(d.PriceForNextLevel(9).ToString(), GUILayout.Width(65));
            GUILayout.Label((55*d.baseCost).ToString("N0"),GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.LabelField("Total all 40 purchases: 12,375 Bank Gold");
        EditorGUILayout.LabelField("No cost or balance change is performed by 5B.5.", EditorStyles.miniLabel);
    }

    private void Poll()
    {
        if (!monitor) return;
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 0.3;
        if (!EditorApplication.isPlaying) return;
        try
        {
            Scene scene = SceneManager.GetActiveScene();
            string key = scene.name + " | profile=" + MS5A2GoldBank.SelectedSlot;
            if (scene.name == "MainMenu")
            {
                MS5B2ShopUI[] s = UnityEngine.Object.FindObjectsByType<MS5B2ShopUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (s.Length == 1)
                {
                    Transform w = s[0].transform.Find("MS5B2_Window");
                    key += " | shop=" + (w && w.gameObject.activeInHierarchy ? "OPEN" : "CLOSED");
                }
            }
            if (scene.name == "SampleScene")
            {
                MS5B3PermanentBonusApplier[] appliers = UnityEngine.Object.FindObjectsByType<MS5B3PermanentBonusApplier>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                key += " | 5B.3=" + (appliers.Length==1 ? (appliers[0].IsApplied ? "APPLIED" : "NOT_APPLIED") : "COUNT_"+appliers.Length);
                if (appliers.Length == 1) key += " | lockedSlot=" + appliers[0].LockedProfileSlot;
            }
            if (lastRuntimeLine != key)
            {
                lastRuntimeLine = key;
                Record(key);
            }
        }
        catch (Exception ex) { Record("Monitor read failed: " + ex.Message); monitor = false; }
    }

    private void Record(string line)
    {
        runtimeEvents.Add("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + line);
        if (runtimeEvents.Count > 75) runtimeEvents.RemoveAt(0);
        Repaint();
    }

    private void ExportReport()
    {
        string path = EditorUtility.SaveFilePanel("Export 5B.5 Read-only QA Report", "", "MS5B5_FinalQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"), "txt");
        if (string.IsNullOrEmpty(path)) return;
        StringBuilder s = new StringBuilder();
        s.AppendLine("MONSTERSURVIVAL - 5B.5 FINAL QA / BALANCE (READ-ONLY)");
        s.AppendLine("Generated: " + DateTime.Now.ToString("o") + " | Unity " + Application.unityVersion);
        s.AppendLine("Scene: " + lastAuditScene + " | Audit at " + lastAuditTime);
        s.AppendLine("AUTO: " + passCount + " PASS / " + warnCount + " WARN / " + failCount + " ERROR");
        foreach (CheckRow r in checks) s.AppendLine("[" + r.severity + "] " + r.label + " - " + r.details);
        s.AppendLine("BALANCE: all max upgrades cost 12,375 Bank Gold at current prices.");
        s.AppendLine("MANUAL CONFIRMATIONS (user checkmarks are not auto-verified):");
        for (int i=0;i<Names.Length;i++) s.AppendLine((manual[i] ? "[USER CONFIRMED] " : "[NOT CONFIRMED] ") + Names[i]);
        s.AppendLine("RUNTIME TRACE:");
        foreach (string row in runtimeEvents) s.AppendLine(row);
        s.AppendLine("Notes: Audit does NOT automatically verify clicked buttons, save writes, or combat effects.");
        try { File.WriteAllText(path,s.ToString(),new UTF8Encoding(false)); Debug.Log("[5B.5] Exported report: " + path); }
        catch (Exception ex) { Debug.LogError("[5B.5] Report export failed: " + ex.Message); }
    }
}
#endif
