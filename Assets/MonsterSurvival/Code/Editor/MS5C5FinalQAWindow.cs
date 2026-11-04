using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// MonsterSurvival 5C.5 — READ ONLY by design.
// Does NOT call TrySave, TryPurchase, TryGrantAchievement, SceneManager.LoadScene,
// or change any GameObject, SaveData, Time.timeScale or Button state.
public sealed class MS5C5FinalQAWindow : EditorWindow
{
    private enum Severity { Pass, Warn, Error, Info }
    private sealed class Finding
    {
        public Severity level;
        public string message;
        public Finding(Severity s, string m) { level = s; message = m; }
    }

    private readonly List<Finding> findings = new List<Finding>();
    private readonly List<string> timeline = new List<string>();
    private readonly bool[] manual = new bool[16];
    private Vector2 scroll;
    private bool observing;
    private double lastSampleTime;
    private string lastSignature = string.Empty;
    private string lastAuditScene = "(not scanned)";
    private string lastAuditTime = "—";
    private int passCount, warnCount, errorCount, infoCount;
    private int sampleCount;
    private static readonly string[] ManualItems = {
        "01. MainMenu: select Profile 01/02/03; independent permissions and Gold",
        "02. Existing legacy Profile keeps previously playable maps",
        "03. Fresh Profile: Meadow available; locked map Start is blocked",
        "04. Unlock Sand Dunes: confirm price, Gold and revision",
        "05. Duplicate/rapid UNLOCK click never charges Gold twice",
        "06. Missing Gold / Save backup-only prevents unsafe purchase",
        "07. Ancient Ruins unlock persists after restarting Unity",
        "08. Gameplay guard blocks a locked selected map on a fresh Profile",
        "09. Gameplay on an allowed selected map starts correctly",
        "10. Defeat spawned Shadow Titan; achievement saved exactly once",
        "11. Defeat spawned Grave Warden; achievement saved exactly once",
        "12. Test Boss placed directly in Scene gives no achievement",
        "13. Achievement Gallery reads correct Profile / badges",
        "14. Achievement toast triggers only for NEW persisted achievement",
        "15. Equipment license does NOT grant free Item into Inventory",
        "16. Victory, Inventory, Equipment and Permanent Upgrades still work"
    };

    [MenuItem("Tools/MonsterSurvival/5C Unlock System/12 - OPEN 5C.5 Final QA + Balance (READ ONLY)")]
    private static void OpenWindow()
    {
        MS5C5FinalQAWindow window = GetWindow<MS5C5FinalQAWindow>("5C.5 Unlock Final QA");
        window.minSize = new Vector2(650f, 540f);
        window.Show();
    }

    private void OnEnable() { EditorApplication.update += Observe; }
    private void OnDisable() { observing = false; EditorApplication.update -= Observe; }

    private void OnGUI()
    {
        GUILayout.Label("MONSTERSURVIVAL / 5C.5 FINAL QA + BALANCE", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("READ ONLY: Only the EXPORT button writes a TXT report. It does not change Profile Save files.\n"
            + "Static checks do not replace manual gameplay testing.", MessageType.Info);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("AUDIT OPEN SCENE + 3 PROFILES", GUILayout.Height(30))) Audit();
        if (GUILayout.Button(observing ? "STOP LIVE OBSERVATION" : "START LIVE OBSERVATION", GUILayout.Height(30)))
        {
            observing = !observing;
            if (observing) { lastSampleTime = 0; lastSignature = string.Empty; timeline.Add("--- Observe start (Play Mode required) ---"); }
            else timeline.Add("--- Observe stop ---");
        }
        if (GUILayout.Button("EXPORT TXT", GUILayout.Height(30))) Export();
        EditorGUILayout.EndHorizontal();
        GUILayout.Label("Last audit: " + lastAuditTime + " | Scene: " + lastAuditScene);
        EditorGUILayout.LabelField("AUTO: " + passCount + " PASS / " + warnCount + " WARN / " + errorCount + " ERROR / " + infoCount + " INFO");
        EditorGUILayout.LabelField("Live observation: " + sampleCount + " samples; " + timeline.Count + " event lines.");
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.Space(7);
        GUILayout.Label("READ-ONLY AUDIT", EditorStyles.boldLabel);
        if (findings.Count == 0) EditorGUILayout.HelpBox("Press AUDIT to scan current scene and all three Profile slots.", MessageType.None);
        foreach (Finding f in findings)
        {
            Color old = GUI.contentColor;
            GUI.contentColor = f.level == Severity.Error ? new Color(1f,.48f,.48f) :
                               f.level == Severity.Warn ? new Color(1f,.79f,.35f) :
                               f.level == Severity.Pass ? new Color(.65f,.92f,.70f) : Color.white;
            EditorGUILayout.LabelField("[" + f.level.ToString().ToUpperInvariant() + "] " + f.message,
                EditorStyles.wordWrappedLabel);
            GUI.contentColor = old;
        }
        EditorGUILayout.Space(12);
        GUILayout.Label("PLAY MODE CONFIRMATIONS (manually checked by you)", EditorStyles.boldLabel);
        int marked = 0;
        for (int i = 0; i < manual.Length; i++)
        {
            manual[i] = EditorGUILayout.ToggleLeft(ManualItems[i], manual[i]);
            if (manual[i]) marked++;
        }
        EditorGUILayout.LabelField("Manually confirmed: " + marked + "/" + manual.Length);
        EditorGUILayout.Space(7);
        GUILayout.Label("LIVE TRACE (most recent 25 events)", EditorStyles.boldLabel);
        if (!EditorApplication.isPlaying) EditorGUILayout.HelpBox("Enter Play Mode and press START LIVE OBSERVATION. A read-only sample cannot confirm that buttons were clicked.", MessageType.None);
        for (int i = Mathf.Max(0, timeline.Count - 25); i < timeline.Count; i++)
            EditorGUILayout.LabelField(timeline[i], EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndScrollView();
    }

    private void Add(Severity s, string message)
    {
        findings.Add(new Finding(s, message));
        switch (s)
        {
            case Severity.Pass: passCount++; break;
            case Severity.Warn: warnCount++; break;
            case Severity.Error: errorCount++; break;
            default: infoCount++; break;
        }
    }

    private static T FindInScene<T>(Scene scene) where T : UnityEngine.Component
    {
        // Includes inactive GameObjects, excludes Prefab assets and unrelated loaded Scenes.
        T[] all = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < all.Length; i++)
            if (all[i] && all[i].gameObject.scene == scene) return all[i];
        return null;
    }

    private void Audit()
    {
        findings.Clear(); passCount = warnCount = errorCount = infoCount = 0;
        Scene scene = SceneManager.GetActiveScene();
        lastAuditScene = scene.IsValid() ? scene.name : "(no scene)";
        lastAuditTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        AuditCatalog();
        for (int slot = 1; slot <= 3; slot++) AuditSlot(slot);
        AuditScene(scene);
        Add(Severity.Info, "Static audit cannot prove purchased Gold, actual Boss defeat, Scene transitions or UI clicks. Use manual Play Mode checks.");
        Repaint();
    }

    private void AuditCatalog()
    {
        MS5C1UnlockDefinition[] all = MS5C1UnlockCatalog.GetAll();
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        int maps = 0, gear = 0, achievements = 0, purchasableCost = 0, starter = 0;
        foreach (MS5C1UnlockDefinition d in all)
        {
            if (d == null || string.IsNullOrEmpty(d.id)) { Add(Severity.Error, "Catalog has a null/empty ID."); continue; }
            if (!ids.Add(d.id)) Add(Severity.Error, "Duplicate Catalog ID: " + d.id);
            if (d.price < 0) Add(Severity.Error, "Negative price: " + d.id);
            if (d.starterUnlocked) { starter++; if (d.price != 0) Add(Severity.Error, "Starter has a nonzero price: " + d.id); }
            if (d.achievementOnly && d.price != 0) Add(Severity.Error, "Boss achievement has a price: " + d.id);
            if (d.category == MS5C1UnlockCategory.Map) maps++;
            else if (d.category == MS5C1UnlockCategory.Equipment) gear++;
            else if (d.category == MS5C1UnlockCategory.Achievement) achievements++;
            if (!d.starterUnlocked && !d.achievementOnly && d.price > 0) purchasableCost += d.price;
            Add(Severity.Info, "CATALOG " + d.id + " / " + d.title + " / " + d.category +
                " / cost " + d.price + (d.starterUnlocked ? " / STARTER" : "") + (d.achievementOnly ? " / ACHIEVEMENT" : ""));
        }
        if (ids.Count == 7 && maps == 3 && gear == 2 && achievements == 2 && starter == 1)
            Add(Severity.Pass, "Unlock Catalog: 7 unique items / 3 maps / 2 gear licenses / 2 achievements / 1 starter.");
        else Add(Severity.Error, "Unexpected catalog counts: ids=" + ids.Count + " maps=" + maps + " gear=" + gear + " achievements=" + achievements + " starter=" + starter);
        if (ids.Contains("map.green_meadow") && ids.Contains("map.sand_dunes") && ids.Contains("map.ancient_ruins"))
            Add(Severity.Pass, "All three map unlock IDs present.");
        else Add(Severity.Error, "Missing a mapped Map Selection unlock ID.");
        Add(Severity.Info, "BALANCE: all non-starter purchasable content = " + purchasableCost + " Bank Gold (at current catalog prices). No Gold is modified.");
        Add(Severity.Info, "Economic estimate ONLY: at 50/100/200 Gold per Victory, clearing the current purchasable catalog takes >= " +
            CeilDiv(purchasableCost,50) + "/" + CeilDiv(purchasableCost,100) + "/" + CeilDiv(purchasableCost,200) + " winning runs (before other spending).");
    }

    private static int CeilDiv(int a, int b) { return b <= 0 ? 0 : (a + b - 1) / b; }

    private void AuditSlot(int slot)
    {
        MS5A1SaveData data;
        bool backup;
        string reason;
        bool loaded = MS5A1SaveService.TryLoad(slot, out data, out reason, out backup);
        if (!loaded)
        {
            if (!string.IsNullOrEmpty(reason) && reason.StartsWith("NO_SAVE", StringComparison.Ordinal))
            {
                Add(Severity.Info, "Profile " + slot.ToString("00") + ": NEW / no save file. No file created.");
                MS5C3MapPolicy.State newState;
                if (MS5C3MapPolicy.TryGet(slot, 1, out newState) && newState.gated && !newState.allowed)
                    Add(Severity.Pass, "Profile " + slot.ToString("00") + ": new profile policy blocks Sand Dunes before unlock.");
                else Add(Severity.Warn, "Profile " + slot.ToString("00") + ": new profile map policy was not verified.");
            }
            else Add(Severity.Error, "Profile " + slot.ToString("00") + " could not be verified: " + reason);
            return;
        }
        if (data == null) { Add(Severity.Error, "Profile " + slot + ": load returned null data."); return; }
        if (backup) Add(Severity.Warn, "Profile " + slot.ToString("00") + " loaded from valid BACKUP; restore it safely before any purchases.");
        else Add(Severity.Pass, "Profile " + slot.ToString("00") + " primary verified: Gold=" + data.bankGold + " rev=" + data.revision + ".");
        if (data.bankGold < 0) Add(Severity.Error, "Profile " + slot + ": negative Bank Gold.");
        if (data.unlockedContentIds == null)
        { Add(Severity.Error, "Profile " + slot + ": null unlock ID list."); return; }
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in data.unlockedContentIds)
        {
            if (string.IsNullOrEmpty(id)) { Add(Severity.Warn, "Profile " + slot + ": empty unlock ID."); continue; }
            if (!seen.Add(id)) Add(Severity.Warn, "Profile " + slot + ": repeated unlock ID " + id);
            MS5C1UnlockDefinition def;
            if (id != MS5C3MapPolicy.Marker && !MS5C1UnlockCatalog.TryGet(id, out def))
                Add(Severity.Warn, "Profile " + slot + ": unknown/future unlock ID " + id + " (not removed).");
        }
        MS5C3MapPolicy.State meadow, sand, ruins;
        // Call each TryGet separately: a short-circuited && would leave
        // sand/ruins potentially unassigned (CS0170 on their fields).
        bool meadowOk = MS5C3MapPolicy.TryGet(slot, 0, out meadow);
        bool sandOk   = MS5C3MapPolicy.TryGet(slot, 1, out sand);
        bool ruinsOk  = MS5C3MapPolicy.TryGet(slot, 2, out ruins);
        bool ok = meadowOk && sandOk && ruinsOk;
        if (!ok) Add(Severity.Error, "Profile " + slot + ": map access policy read failed.");
        else
        {
            if (!meadow.allowed) Add(Severity.Error, "Profile " + slot + ": Green Meadow must remain accessible.");
            else Add(Severity.Pass, "Profile " + slot + ": Meadow allowed; mode=" + meadow.status + ".");
            if (sand.legacy)
                Add(Severity.Info, "Profile " + slot + ": LEGACY ACCESS, Sand/Ruins playable without purchased flags (intentional compatibility).");
            else
            {
                bool sandPurchased = seen.Contains("map.sand_dunes");
                bool ruinsPurchased = seen.Contains("map.ancient_ruins");
                if (sand.allowed == sandPurchased && ruins.allowed == ruinsPurchased)
                    Add(Severity.Pass, "Profile " + slot + ": enforced map access matches saved unlocks.");
                else Add(Severity.Error, "Profile " + slot + ": map access does not match purchased flags.");
            }
            Add(Severity.Info, "Profile " + slot + " ACCESS: Sand=" + sand.allowed + " / Ruins=" + ruins.allowed +
                " | purchased: Sand=" + seen.Contains("map.sand_dunes") + " / Ruins=" + seen.Contains("map.ancient_ruins") +
                " | bosses: Titan=" + seen.Contains("achievement.shadow_titan") + " / Warden=" + seen.Contains("achievement.grave_warden") +
                " | gear licenses: Reaper=" + seen.Contains("equipment.reaper_edge") + " / Oath=" + seen.Contains("equipment.wardens_oath"));
        }
    }

    private void AuditScene(Scene scene)
    {
        if (!scene.IsValid()) { Add(Severity.Warn, "No valid active Scene to inspect."); return; }
        if (scene.name == "MainMenu")
        {
            MS5C2MapUnlockUI mapUI = FindInScene<MS5C2MapUnlockUI>(scene);
            MS5C3MainMenuGate gate = FindInScene<MS5C3MainMenuGate>(scene);
            MS5C4AchievementGallery gallery = FindInScene<MS5C4AchievementGallery>(scene);
            if (!mapUI) Add(Severity.Error, "MainMenu is missing MS5C2MapUnlockUI.");
            else if (!mapUI.menu || !mapUI.profileWidget) Add(Severity.Warn, "5C.2 Map Unlock UI references are incomplete.");
            else Add(Severity.Pass, "MainMenu 5C.2 Map Unlock UI connected.");
            if (!gate) Add(Severity.Error, "MainMenu is missing MS5C3MainMenuGate.");
            else if (!gate.menu || !gate.menu.mapsStartButton) Add(Severity.Warn, "5C.3 Main Menu Gate / START reference not assigned.");
            else Add(Severity.Pass, "MainMenu 5C.3 START access gate connected.");
            if (!gallery) Add(Severity.Error, "MainMenu is missing MS5C4AchievementGallery.");
            else if (!gallery.openButton || !gallery.profileWidget || !gallery.titanBadge || !gallery.wardenBadge || !gallery.equipmentBadge)
                Add(Severity.Warn, "5C.4 Gallery launcher, badges or Profile references missing.");
            else Add(Severity.Pass, "MainMenu 5C.4 Achievement Gallery connected.");
            if (mapUI && gate && gallery && (!mapUI.enabled || !gate.enabled || !gallery.enabled))
                Add(Severity.Warn, "A 5C runtime UI component is disabled in MainMenu.");
        }
        else if (scene.name == "SampleScene")
        {
            MS5C3GameplayGate gate = FindInScene<MS5C3GameplayGate>(scene);
            MS5C3BossAchievements bosses = FindInScene<MS5C3BossAchievements>(scene);
            MS5C4AchievementToast toast = FindInScene<MS5C4AchievementToast>(scene);
            if (!gate) Add(Severity.Error, "Gameplay is missing MS5C3GameplayGate.");
            else if (!gate.mapSwitcher || !gate.waveManager) Add(Severity.Warn, "Gameplay Map Gate missing MapSwitcher/WaveManager reference.");
            else Add(Severity.Pass, "Gameplay map access guard attached.");
            if (!bosses) Add(Severity.Error, "Gameplay is missing MS5C3BossAchievements.");
            else if (!bosses.waveManager) Add(Severity.Warn, "Boss achievement watcher missing WaveManager reference.");
            else Add(Severity.Pass, "WaveManager Boss Achievement watcher attached.");
            if (!toast) Add(Severity.Warn, "Gameplay missing 5C.4 Achievement Toast.");
            else if (!toast.unlockSound) Add(Severity.Warn, "Achievement Toast audio clip not assigned.");
            else Add(Severity.Pass, "5C.4 Achievement Toast and Audio assigned.");
        }
        else Add(Severity.Info, "Current Scene is " + scene.name + ". Open MainMenu or SampleScene to check 5C integration.");
    }

    private void Observe()
    {
        if (!observing) return;
        if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
        double now = EditorApplication.timeSinceStartup;
        if (now - lastSampleTime < .5) return;
        lastSampleTime = now;
        sampleCount++;
        Scene scene = SceneManager.GetActiveScene();
        int slot = MS5A2GoldBank.SelectedSlot;
        string status = scene.name + " Profile=" + slot + " TimeScale=" + Time.timeScale.ToString("0.00");
        if (scene.name == "SampleScene")
        {
            WaveMapSwitcher sw = FindInScene<WaveMapSwitcher>(scene);
            if (sw) status += " SelectedMap=" + sw.SelectedMapIndex + " ActiveMap=" + sw.ActiveMapIndex;
            if (sw && sw.SelectedMapIndex >= 0 && sw.SelectedMapIndex <= 2)
            {
                MS5C3MapPolicy.State state;
                if (MS5C3MapPolicy.TryGet(slot, sw.SelectedMapIndex, out state))
                {
                    status += " Access=" + (state.allowed ? "ALLOWED" : "DENIED") + "/" + state.status;
                    if (!state.allowed) status += " [WARNING: LOCKED MAP ACTIVE]";
                }
                else status += " [WARNING: POLICY READ FAILURE]";
            }
        }
        if (scene.name == "MainMenu")
        {
            MMV3MenuController menu = FindInScene<MMV3MenuController>(scene);
            if (menu && menu.mapsPage) status += " MapPage=" + menu.mapsPage.activeInHierarchy;
        }
        MS5C1UnlockService.Snapshot snapshot;
        string message;
        if (slot >= 1 && slot <= 3 && MS5C1UnlockService.TryGetSnapshot(slot, out snapshot, out message))
            status += " Gold=" + snapshot.bankGold + " Rev=" + snapshot.revision +
                " Titan=" + snapshot.IsUnlocked("achievement.shadow_titan") +
                " Warden=" + snapshot.IsUnlocked("achievement.grave_warden");
        if (!string.Equals(status, lastSignature, StringComparison.Ordinal))
        {
            timeline.Add("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + status);
            lastSignature = status;
            if (timeline.Count > 600) timeline.RemoveAt(0);
            Repaint();
        }
    }

    private void Export()
    {
        string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "UI_QA_Reports");
        string file = Path.Combine(directory, "MS5C5_FinalQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
        try
        {
            Directory.CreateDirectory(directory);
            StringBuilder b = new StringBuilder();
            b.AppendLine("MONSTERSURVIVAL / 5C.5 FINAL UNLOCK QA (READ ONLY)");
            b.AppendLine("Created: " + DateTime.Now.ToString("o") + " | Unity: " + Application.unityVersion);
            b.AppendLine("Scene audited: " + lastAuditScene + " at " + lastAuditTime);
            b.AppendLine("AUDIT: " + passCount + " PASS / " + warnCount + " WARN / " + errorCount + " ERROR / " + infoCount + " INFO");
            foreach (Finding f in findings) b.AppendLine("[" + f.level.ToString().ToUpperInvariant() + "] " + f.message);
            b.AppendLine("----- MANUAL CONFIRMATIONS (USER ONLY) -----");
            for (int i = 0; i < manual.Length; i++) b.AppendLine((manual[i] ? "[CONFIRMED] " : "[NOT CONFIRMED] ") + ManualItems[i]);
            b.AppendLine("----- LIVE TIMELINE -----");
            b.AppendLine("Samples: " + sampleCount);
            foreach (string evt in timeline) b.AppendLine(evt);
            b.AppendLine("IMPORTANT: Static audit and observer do not verify purchases, button clicks, actual Boss kills, or disk writes.");
            File.WriteAllText(file, b.ToString(), new UTF8Encoding(false));
            Debug.Log("[5C.5] Final QA TXT exported: " + file);
            EditorUtility.DisplayDialog("5C.5 QA", "Report saved (no Profile modified):\n" + file, "OK");
        }
        catch (Exception ex) { Debug.LogError("[5C.5] Export failed: " + ex.Message); }
    }
}
