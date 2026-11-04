#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// 5A.5: READ-ONLY runtime/scene/save inspector. Does not install components,
// change Scene state, modify PlayerPrefs or write any real profile data.
public sealed class MS5A5FinalQAWindow : EditorWindow
{
    private readonly string[] manualNames =
    {
        "01. MainMenu: switch Profile 01/02/03 and see distinct balances",
        "02. Profile selected BEFORE Start is the profile used by the run",
        "03. Start SampleScene; initial Inventory/Equipment restored ONCE",
        "04. Pick items / equip gear; EQUIP, UNEQUIP, USE still work",
        "05. Win: Bank Gold + 20 Inventory + 3 Equipment saved together",
        "06. Return to MainMenu: correct Gold and save timestamp",
        "07. Re-enter same profile: item positions & counts unchanged",
        "08. Re-enter: Equipment bonuses applied ONCE, not doubled",
        "09. Win second run: only NEW earned Gold deposited",
        "10. Lose / Restart before Victory: no new data committed",
        "11. Different profile has independent inventory and equipment",
        "12. MANAGE SAVES: Archive then inspect safe backup status",
        "13. Recovery only on a disposable test profile / backed-up data",
        "14. Close Unity/reopen Project and repeat the load check"
    };
    private bool[] manualChecks = new bool[14];
    private Vector2 scroll;
    private readonly List<string> events = new List<string>();
    private string lastObservation = "";
    private string auditText = "Not audited yet.";
    private string fixtureText = "Not run yet.";
    private int fixturePassed;
    private int fixtureTotal;
    private double lastSampleTime;
    private bool monitoring;
    private double startedAt;
    private int countWarnings;

    [MenuItem("Tools/MonsterSurvival/5A Save System/14 - OPEN 5A.5 Final Integration QA (READ ONLY)")]
    public static void Open()
    {
        MS5A5FinalQAWindow window = GetWindow<MS5A5FinalQAWindow>("5A.5 Final QA");
        window.minSize = new Vector2(620, 510);
        window.Show();
    }

    private void OnEnable() { EditorApplication.update += OnEditorUpdate; }
    private void OnDisable() { EditorApplication.update -= OnEditorUpdate; }

    private void OnEditorUpdate()
    {
        if (!monitoring || !EditorApplication.isPlaying) return;
        if (EditorApplication.timeSinceStartup - lastSampleTime < 0.35) return;
        lastSampleTime = EditorApplication.timeSinceStartup;
        string snapshot = GetRuntimeObservation();
        if (snapshot == lastObservation) return;
        lastObservation = snapshot;
        string entry = "[+" + (EditorApplication.timeSinceStartup - startedAt).ToString("F1") + "s] " + snapshot;
        if (events.Count >= 1000) events.RemoveAt(0);
        events.Add(entry);
        if (snapshot.Contains("WARNING")) countWarnings++;
        Repaint();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        GUILayout.Space(6);
        EditorGUILayout.LabelField("MONSTERSURVIVAL • 5A.5 FINAL INTEGRATION QA", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("READ ONLY: this tool NEVER edits real slot_01/02/03 saves, Inventory, Equipment, TimeScale, Scene objects or listeners. Isolated fixture test uses a unique temporary directory.", MessageType.Info);
        GUILayout.Space(6);
        EditorGUILayout.LabelField("A · Audit current Scene + real save status", EditorStyles.boldLabel);
        if (GUILayout.Button("AUDIT CURRENT SCENE & ALL THREE SLOTS (READ ONLY)", GUILayout.Height(29)))
            auditText = BuildAuditReport();
        EditorGUILayout.TextArea(auditText, GUILayout.MinHeight(122));
        GUILayout.Space(8);
        EditorGUILayout.LabelField("B · Isolated integration fixtures", EditorStyles.boldLabel);
        if (GUILayout.Button("RUN ISOLATED INTEGRATION QA (TEMP FOLDER ONLY)", GUILayout.Height(29)))
        {
            fixtureText = MS5A5IsolatedIntegrationQA.Run(out fixturePassed, out fixtureTotal);
            Debug.Log("[5A.5 QA] " + fixturePassed + "/" + fixtureTotal + " checks.\n" + fixtureText);
        }
        EditorGUILayout.LabelField("Fixture result: " + (fixtureTotal == 0 ? "Not run" : fixturePassed + "/" + fixtureTotal), EditorStyles.miniBoldLabel);
        EditorGUILayout.TextArea(fixtureText, GUILayout.MinHeight(65));
        GUILayout.Space(8);
        EditorGUILayout.LabelField("C · Live Play Mode observation", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = EditorApplication.isPlaying && !monitoring;
            if (GUILayout.Button("START OBSERVING"))
            {
                monitoring = true;
                startedAt = EditorApplication.timeSinceStartup;
                lastSampleTime = 0;
                lastObservation = "";
                events.Clear();
                countWarnings = 0;
                events.Add("[0.0s] Observing started. No gameplay controls are modified.");
            }
            GUI.enabled = monitoring;
            if (GUILayout.Button("STOP OBSERVING"))
            {
                monitoring = false;
                events.Add("[END] Monitoring stopped.");
            }
            GUI.enabled = true;
        }
        EditorGUILayout.LabelField("Observed changes: " + events.Count + " · Warning transitions: " + countWarnings);
        string logView = events.Count == 0 ? "Enter Play Mode and press START OBSERVING." :
            string.Join("\n", events.GetRange(Mathf.Max(0, events.Count - 22), Mathf.Min(22, events.Count)).ToArray());
        EditorGUILayout.TextArea(logView, GUILayout.MinHeight(130));
        GUILayout.Space(8);
        EditorGUILayout.LabelField("D · Manual end-to-end checklist (not automatic PASS)", EditorStyles.boldLabel);
        int done = 0;
        for (int i = 0; i < manualNames.Length; i++)
        {
            manualChecks[i] = EditorGUILayout.ToggleLeft(manualNames[i], manualChecks[i]);
            if (manualChecks[i]) done++;
        }
        EditorGUILayout.LabelField("Manually confirmed: " + done + "/" + manualNames.Length);
        GUILayout.Space(10);
        if (GUILayout.Button("EXPORT QA REPORT (.TXT in project root / UI_QA_Reports)", GUILayout.Height(31)))
            ExportReport(done);
        EditorGUILayout.HelpBox("Important: passing fixture/scene checks does not prove real gameplay flow. Do not mark 5A complete until Victory, loss, restart, profile switch and reload have been manually verified.", MessageType.Warning);
        EditorGUILayout.EndScrollView();
    }

    private static T FindScene<T>(Scene scene) where T : Component
    {
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component) return component;
        }
        return null;
    }

    private string BuildAuditReport()
    {
        StringBuilder sb = new StringBuilder();
        Scene scene = SceneManager.GetActiveScene();
        sb.AppendLine("Scene: " + scene.name + (EditorApplication.isPlaying ? " [PLAY MODE]" : " [EDIT MODE]"));
        if (scene.name == "MainMenu")
        {
            MS5A2ProfileWidget widget = FindScene<MS5A2ProfileWidget>(scene);
            MS5A4SaveSlotsUI manager = FindScene<MS5A4SaveSlotsUI>(scene);
            sb.AppendLine((widget ? "PASS" : "WARN") + " — 5A.2 Profile Widget");
            sb.AppendLine((manager ? "PASS" : "WARN") + " — 5A.4 Manage Saves UI");
            if (manager)
                sb.AppendLine((manager.openButton && manager.profileWidget ? "PASS" : "WARN") + " — Manage Saves button / profile widget references");
        }
        else if (scene.name == "SampleScene")
        {
            MS5A3PersistentRun run = FindScene<MS5A3PersistentRun>(scene);
            MS5A2BankOnVictory old = FindScene<MS5A2BankOnVictory>(scene);
            MS3APlayerInventory inventory = FindScene<MS3APlayerInventory>(scene);
            MS3DPlayerEquipment equipment = FindScene<MS3DPlayerEquipment>(scene);
            GameManager gm = FindScene<GameManager>(scene);
            PlayerProgression progression = FindScene<PlayerProgression>(scene);
            sb.AppendLine((run ? "PASS" : "ERROR") + " — 5A.3 unified Victory watcher");
            sb.AppendLine((old && !old.enabled ? "PASS" : "ERROR") + " — legacy 5A.2 watcher must be present & DISABLED");
            sb.AppendLine((run && run.enabled ? "PASS" : "ERROR") + " — 5A.3 watcher enabled");
            sb.AppendLine((gm && progression ? "PASS" : "ERROR") + " — GameManager + PlayerProgression");
            sb.AppendLine((inventory && inventory.Slots != null && inventory.Slots.Count == 20 ? "PASS" : "ERROR") + " — Inventory 20 slots");
            sb.AppendLine((equipment && equipment.catalog ? "PASS" : "ERROR") + " — Equipment catalog");
            if (run)
            {
                sb.AppendLine((run.gameManager == gm && run.progression == progression &&
                               run.inventory == inventory && run.equipment == equipment && run.itemRegistry ?
                               "PASS" : "ERROR") + " — 5A.3 watcher linked references");
                sb.AppendLine((!old || run.legacyGoldWatcher == old ? "PASS" : "WARN") + " — legacy watcher reference");
                if (EditorApplication.isPlaying)
                    sb.AppendLine("Runtime status: " + run.Status + " | committed=" + run.Committed);
                if (run.itemRegistry)
                {
                    Dictionary<string, MS3AItemData> lookup;
                    string reason;
                    bool registryGood = run.itemRegistry.TryBuild(out lookup, out reason);
                    sb.AppendLine((registryGood ? "PASS" : "ERROR") + " — Item registry: " + reason);
                }
            }
        }
        else sb.AppendLine("INFO — Open MainMenu or SampleScene for scene wiring checks.");
        sb.AppendLine("Selected Profile: " + MS5A2GoldBank.SelectedSlot);
        for (int slot = 1; slot <= 3; slot++)
        {
            try
            {
                MS5A4SaveRecovery.Report r = MS5A4SaveRecovery.InspectSlot(slot);
                MS5A1SaveData data = r.primaryState == MS5A4SaveRecovery.FileState.Valid ? r.primary :
                                     r.backupState == MS5A4SaveRecovery.FileState.Valid ? r.backup : null;
                sb.AppendLine("Slot " + slot.ToString("00") + " primary=" + r.primaryState +
                    " backup=" + r.backupState + " | rev=" + (data == null ? "—" : data.revision.ToString()) +
                    " | gold=" + (data == null ? "—" : data.bankGold.ToString()) +
                    " | itemSlots=" + (data == null ? "—" : MS5A4SaveRecovery.ItemCount(data).ToString()) +
                    " | equipped=" + (data == null ? "—" : MS5A4SaveRecovery.GearCount(data).ToString()) +
                    " | save=" + (data == null ? "—" : MS5A4SaveRecovery.FormatTime(data.lastSavedUtc)) +
                    " | " + (r.primaryState == MS5A4SaveRecovery.FileState.Valid ? "verified primary" :
                            r.backupState == MS5A4SaveRecovery.FileState.Valid ? "BACKUP ONLY — needs attention" : "no usable save"));
            }
            catch (Exception ex) { sb.AppendLine("Slot " + slot + ": ERROR reading status — " + ex.Message); }
        }
        sb.AppendLine("Read-only audit; primary/backup files unchanged.");
        return sb.ToString();
    }

    private static string GetRuntimeObservation()
    {
        Scene scene = SceneManager.GetActiveScene();
        StringBuilder b = new StringBuilder();
        b.Append(scene.name).Append(" | Slot ").Append(MS5A2GoldBank.SelectedSlot)
         .Append(" | TimeScale ").Append(Time.timeScale.ToString("F1"));
        if (scene.name == "SampleScene")
        {
            MS5A3PersistentRun run = FindScene<MS5A3PersistentRun>(scene);
            MS5A2BankOnVictory old = FindScene<MS5A2BankOnVictory>(scene);
            GameManager gm = FindScene<GameManager>(scene);
            PlayerProgression p = FindScene<PlayerProgression>(scene);
            MS3APlayerInventory inv = FindScene<MS3APlayerInventory>(scene);
            MS3DPlayerEquipment eq = FindScene<MS3DPlayerEquipment>(scene);
            b.Append(" | Coins ").Append(p ? p.coins : -1);
            if (inv && inv.Slots != null)
            {
                int nonEmpty = 0, totalItems = 0;
                foreach (var s in inv.Slots) if (s != null && !s.IsEmpty) { nonEmpty++; totalItems += s.quantity; }
                b.Append(" | Bag ").Append(nonEmpty).Append("/20 stacks, ").Append(totalItems).Append(" items");
            }
            if (eq)
            {
                int n = 0;
                for (int i = 0; i < 3; i++) if (eq.GetEquipped((MS3DEquipmentType)i)) n++;
                b.Append(" | Gear ").Append(n).Append("/3");
            }
            b.Append(" | Victory ").Append(gm && gm.victoryPanel && gm.victoryPanel.activeSelf)
             .Append(" | GameOver ").Append(gm && gm.gameOverPanel && gm.gameOverPanel.activeSelf);
            b.Append(" | Save ").Append(run ? run.Status : "NO 5A.3 RUN");
            if (old && old.enabled) b.Append(" | WARNING: OLD BANK WATCHER ENABLED");
            if (run && !run.enabled) b.Append(" | WARNING: UNIFIED WATCHER DISABLED");
        }
        else if (scene.name == "MainMenu")
        {
            MS5A4SaveSlotsUI ui = FindScene<MS5A4SaveSlotsUI>(scene);
            b.Append(" | ManageSaves ").Append(ui ? "present" : "missing");
        }
        return b.ToString();
    }

    private void ExportReport(int confirmed)
    {
        try
        {
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "UI_QA_Reports");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "MS5A5_FinalQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            StringBuilder b = new StringBuilder();
            b.AppendLine("MONSTERSURVIVAL / 5A.5 FINAL INTEGRATION REPORT");
            b.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            b.AppendLine("Unity: " + Application.unityVersion);
            b.AppendLine("Read-only inspector; tests described below are NOT automatically gameplay-verified.");
            b.AppendLine("===== SCENE / SAVE AUDIT =====").AppendLine(auditText);
            b.AppendLine("===== ISOLATED FIXTURES: " + fixturePassed + "/" + fixtureTotal + " =====").AppendLine(fixtureText);
            b.AppendLine("===== RUNTIME EVENT TRACE =====");
            foreach (string entry in events) b.AppendLine(entry);
            b.AppendLine("===== MANUAL CONFIRMATIONS: " + confirmed + "/" + manualNames.Length + " =====");
            for (int i = 0; i < manualNames.Length; i++)
                b.AppendLine((manualChecks[i] ? "[CONFIRMED] " : "[NOT CONFIRMED] ") + manualNames[i]);
            b.AppendLine("Do not mark project final until manual gameplay checks pass.");
            File.WriteAllText(path, b.ToString(), new UTF8Encoding(false));
            Debug.Log("[5A.5] Exported report: " + path);
            EditorUtility.RevealInFinder(path);
        }
        catch (Exception ex) { Debug.LogError("[5A.5] Export failed: " + ex); }
    }
}
#endif
