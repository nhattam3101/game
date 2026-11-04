#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// MS 7H.5: Editor-only, READ-ONLY for real user profiles and Scenes.
// No TrySave, no game-object creation, no scene modifications, no quest Claim calls.
// In-memory pure tests use NEW MS5A1SaveData objects only.
public sealed class MS7H5FinalQuestQAWindow : EditorWindow
{
    private const string MenuPath = "Tools/MonsterSurvival/7H Quests/5 - Open 7H.5 Final Quest Save QA";
    private const string TitleText = "MONSTERSURVIVAL 7H.5 / FINAL QUEST + SAVE QA";
    private const int ManualCount = 16;
    private static readonly string[] ManualSteps =
    {
        "01 | MainMenu: 7H1 QUESTS can open, close, and show all three correct labels.",
        "02 | PROGRESS: three 7H2 progress bars and state labels are readable.",
        "03 | 7H3 DAILY/WEEKLY: ALL text visibly renders (title, tabs, names, Claim, Back, X).",
        "04 | Daily/Weekly tabs, navigation and Close work at target Game View sizes.",
        "05 | Win real Wave 5 and return to MainMenu: 7H1 First Victory progresses.",
        "06 | 7H2 wins/Boss/earned Gold increment ONCE after verified 5A3 Victory.",
        "07 | 7H3 daily/weekly counters increment ONCE after corresponding 7H2 sync.",
        "08 | Game Over, Retry and Quit before Victory do not grant persistent quest progress.",
        "09 | Claim 7H1 reward: Bank Gold increases by exact amount and persists on reload.",
        "10 | Claim 7H2 reward: Bank Gold increases by exact amount and persists on reload.",
        "11 | Claim 7H3 reward: Bank Gold increases by exact amount and persists on reload.",
        "12 | Double-click/reopen Claim: no extra reward or duplicate claim marker.",
        "13 | Three profiles 01/02/03: Gold, counters and claimed state remain independent.",
        "14 | UTC day/week rollover refreshes correct periods; clock rollback blocks writes.",
        "15 | 7H4 toast shown for NEW quest status; no historic spam; no Gold mutation.",
        "16 | Inventory/Equipment/Save recovery, Menu V3, Victory, Boss HUD are unaffected."
    };

    private readonly bool[] confirmed = new bool[ManualCount];
    private readonly Dictionary<int, ProfileReading> baselines = new Dictionary<int, ProfileReading>();
    private string audit = "Run READ-ONLY AUDIT; no actual Save/Scene will be modified.";
    private string tests = "Not run. In-memory rule tests are not Play Mode or persistence proof.";
    private string comparison = "No baseline captured; use CAPTURE BASELINE before a manual Victory/Claim test.";
    private bool auditHadHardError;
    private bool testsPassed;
    private bool auditRan;
    private bool structureVerified;
    private Vector2 scroll;
    private bool actionPending;
    private int selectedSlot = 1;
    private bool showManual = true;
    private string lastExport = "Not exported";

    private sealed class ProfileReading
    {
        public int slot, gold, revision, wins, bosses, earnedGold;
        public int claimCount, readyCount;
        public bool safe;
        public string status;
    }

    [MenuItem(MenuPath)]
    public static void OpenWindow() { GetWindow<MS7H5FinalQuestQAWindow>("7H.5 Final Quest QA"); }

    private void Queue(Action action)
    {
        if (actionPending || action == null) return;
        actionPending = true;
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            try { action(); }
            catch (Exception e) { auditHadHardError = true; audit = "[ERROR] " + e; Debug.LogException(e); }
            finally { actionPending = false; if (this != null) Repaint(); }
        };
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        try
        {
            EditorGUILayout.LabelField("7H.5 - FINAL QUEST + SAVE QA", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("READ ONLY for real Profile 01-03 and Unity Scenes. NO install is required. " +
                "Pure tests change disposable in-memory SaveData ONLY. Manual QA is separate from automated evidence.", MessageType.Info);
            if (GUILayout.Button("1 - READ ONLY AUDIT TWO SCENES + THREE PROFILES", GUILayout.Height(36)))
                Queue(() => { audit = BuildAudit(); Debug.Log("[7H5] " + audit); });
            if (GUILayout.Button("2 - IN-MEMORY PURE QUEST / REWARD TESTS", GUILayout.Height(36)))
                Queue(() => { tests = RunPureTests(); testsPassed = tests.Contains(" PURE_FAIL=0 |"); Debug.Log("[7H5] " + tests); });
            EditorGUILayout.Space(6);
            selectedSlot = EditorGUILayout.IntPopup("Profile for comparison", selectedSlot,
                new[] { "Profile 01", "Profile 02", "Profile 03" }, new[] { 1, 2, 3 });
            if (GUILayout.Button("3 - CAPTURE PROFILE BASELINE (READ ONLY)", GUILayout.Height(32)))
                Queue(CaptureBaseline);
            if (GUILayout.Button("4 - COMPARE CURRENT VS BASELINE (READ ONLY)", GUILayout.Height(32)))
                Queue(CompareBaseline);
            if (GUILayout.Button("5 - EXPORT FINAL 7H.5 QA TXT", GUILayout.Height(36)))
                Queue(ExportReport);
            EditorGUILayout.Space(6);
            showManual = EditorGUILayout.Foldout(showManual, "MANUAL GAMEPLAY CHECKLIST (user-confirmed only)", true);
            if (showManual)
            {
                for (int i = 0; i < confirmed.Length; i++)
                    confirmed[i] = EditorGUILayout.ToggleLeft(ManualSteps[i], confirmed[i]);
                EditorGUILayout.HelpBox("Tick only after you actually tested it in Game View / Save. " +
                    "This editor checkbox is NOT automated evidence.", MessageType.Warning);
            }
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Last comparison", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(comparison, GUILayout.MinHeight(76));
            EditorGUILayout.LabelField("Audit report", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(audit, GUILayout.MinHeight(280), GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField("Pure rule tests", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(tests, GUILayout.MinHeight(130));
            EditorGUILayout.LabelField("Last export", lastExport);
        }
        finally { EditorGUILayout.EndScrollView(); }
    }

    private static string ProjectRoot
    {
        get { return Directory.GetParent(Application.dataPath).FullName; }
    }

    private static string FindScenePath(string name)
    {
        List<string> matches = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Scene " + name))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.Equals(Path.GetFileNameWithoutExtension(path), name, StringComparison.OrdinalIgnoreCase) &&
                !path.Contains("/Backups/") && !path.Contains("/MS_Backups/"))
                matches.Add(path);
        }
        if (matches.Count == 0) return null;
        matches.Sort(StringComparer.Ordinal);
        Scene current = SceneManager.GetActiveScene();
        if (current.IsValid() && current.name == name && matches.Contains(current.path)) return current.path;
        foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
            if (entry.enabled && matches.Contains(entry.path)) return entry.path;
        return matches[0];
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T item = root.GetComponentInChildren<T>(true);
            if (item != null) return item;
        }
        return null;
    }

    private static string CheckScene(string path, bool menu)
    {
        if (string.IsNullOrEmpty(path)) return "[FAIL] " + (menu ? "MainMenu" : "SampleScene") + " Scene missing";
        Scene scene = SceneManager.GetSceneByPath(path);
        bool loaded = scene.IsValid() && scene.isLoaded;
        bool openedForAudit = false;
        if (!loaded && !Application.isPlaying)
        {
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            loaded = true;
            openedForAudit = true;
        }
        try
        {
            if (!loaded)
                return "[PENDING] " + path + " is not loaded during Play Mode; no scene opened automatically.";
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[SCENE] " + path);
            if (menu)
            {
                sb.AppendLine(Found<MS7H1QuestBoard>(scene, "7H1 Quest Board"));
                sb.AppendLine(Found<MS7H2ProgressBoard>(scene, "7H2 Progress Board"));
                sb.AppendLine(Found<MS7H3PeriodicQuestPanel>(scene, "7H3 Periodic UI"));
                if (Application.isPlaying && MS7H4QuestToastController.Instance != null)
                    sb.AppendLine("[PASS] 7H4 persistent Toast Controller (DontDestroyOnLoad)");
                else sb.AppendLine(Found<MS7H4QuestToastController>(scene, "7H4 Toast Controller"));
                if (Application.isPlaying && scene == SceneManager.GetActiveScene())
                    sb.AppendLine(InspectPeriodicText(scene));
            }
            else
            {
                sb.AppendLine(Found<WaveManager>(scene, "Existing WaveManager"));
                sb.AppendLine(Found<MS5A3PersistentRun>(scene, "5A3 PersistentRun"));
                sb.AppendLine(Found<MS7H2VictoryProgressObserver>(scene, "7H2 Victory observer"));
                sb.AppendLine(Found<MS7H3PeriodicVictoryObserver>(scene, "7H3 Periodic observer"));
                if (Application.isPlaying && scene == SceneManager.GetActiveScene())
                {
                    MS7H2VictoryProgressObserver a = FindInScene<MS7H2VictoryProgressObserver>(scene);
                    MS7H3PeriodicVictoryObserver b = FindInScene<MS7H3PeriodicVictoryObserver>(scene);
                    if (a != null) sb.AppendLine("[LIVE] 7H2 finished=" + a.Finished + " bossObserved=" + a.BossDefeated +
                        " syncAttempts=" + a.SyncAttempts + " status=" + a.Status);
                    if (b != null) sb.AppendLine("[LIVE] 7H3 finished=" + b.Finished + " attempts=" + b.Attempts +
                        " status=" + b.Status);
                }
            }
            return sb.ToString();
        }
        finally
        {
            if (openedForAudit) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static string Found<T>(Scene scene, string label) where T : Component
    {
        T c = FindInScene<T>(scene);
        return (c != null ? "[PASS] " : "[FAIL] ") + label + (c != null ? " / " + c.gameObject.name : " MISSING");
    }

    private static string InspectPeriodicText(Scene scene)
    {
        MS7H3PeriodicQuestPanel panel = FindInScene<MS7H3PeriodicQuestPanel>(scene);
        if (panel == null) return "[FAIL] 7H3 UI component missing";
        // The 7H3.2 hotfix may use legacy UnityEngine.UI.Text instead of TMP.
        Text[] legacy = panel.GetComponentsInChildren<Text>(true);
        TMP_Text[] tmp = panel.GetComponentsInChildren<TMP_Text>(true);
        int legacyNonempty = 0, tmpNonempty = 0, activeTexts = 0;
        for (int i = 0; i < legacy.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(legacy[i].text)) legacyNonempty++;
            if (legacy[i].gameObject.activeInHierarchy) activeTexts++;
        }
        for (int i = 0; i < tmp.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(tmp[i].text)) tmpNonempty++;
            if (tmp[i].gameObject.activeInHierarchy) activeTexts++;
        }
        return "[UI DIAGNOSTIC] 7H3 text components: legacy=" + legacy.Length + " (nonempty=" + legacyNonempty +
            "), TMP=" + tmp.Length + " (nonempty=" + tmpNonempty + "), active=" + activeTexts +
            ". [PENDING PIXEL PROOF] Text objects do NOT prove Game View glyph visibility.";
    }

    private static string FormatProfile(int slot, out ProfileReading reading, out bool problem)
    {
        problem = false;
        reading = new ProfileReading { slot = slot, status = "NOT READY" };
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("--- PROFILE " + slot.ToString("00") + " ---");
        try
        {
            MS7H1QuestSnapshot a = MS7H1QuestService.ReadSnapshot(slot);
            MS7H2Snapshot b = MS7H2ProgressService.ReadSnapshot(slot);
            MS7H3Snapshot c = MS7H3PeriodicQuestService.ReadSnapshot(slot);
            reading.safe = a.valid && b.valid && c.valid && !a.backupRecovery &&
                !b.backupRecovery && !c.backupRecovery && !c.clockRollback;
            if (!reading.safe)
            {
                sb.AppendLine("[PENDING / BLOCKED] Save not fully verified: 7H1=" + a.message +
                    " 7H2=" + b.message + " 7H3=" + c.message);
                // Missing/backup-only profiles are not automatically corrupt; do NOT overwrite them.
                reading.status = "PENDING Save validation";
            }
            else
            {
                reading.gold = a.gold; reading.revision = a.revision;
                reading.wins = b.wins; reading.bosses = b.bosses; reading.earnedGold = b.earnedGold;
                sb.AppendLine("[PASS] Save readable; Gold=" + a.gold + " Revision=" + a.revision);
                sb.AppendLine("[PROGRESS] 7H2 wins=" + b.wins + " bosses=" + b.bosses + " earnedGold=" + b.earnedGold);
                if (a.gold != b.bankGold || b.bankGold != c.bankGold || a.revision != b.revision || b.revision != c.revision)
                {
                    sb.AppendLine("[FAIL] Services disagree on Gold/Revision in current reads; inspect Save before Claim.");
                    problem = true;
                }
                reading.status = problem ? "CONFLICT" : "VALID";
            }
            if (a.quests != null)
            {
                for (int i = 0; i < a.quests.Length; i++)
                {
                    MS7H1QuestView q = a.quests[i];
                    if (q == null) { problem = true; sb.AppendLine("[FAIL] 7H1 null quest"); continue; }
                    sb.AppendLine("7H1: " + q.title + " completed=" + q.completed + " claimed=" + q.claimed + " +" + q.reward);
                    if (q.claimed) reading.claimCount++;
                    if (q.completed && !q.claimed) reading.readyCount++;
                    if (reading.safe && q.claimed && !q.completed)
                    { problem = true; sb.AppendLine("[FAIL] 7H1 claim without current completion: " + q.title); }
                }
            }
            if (b.quests != null)
            {
                for (int i = 0; i < b.quests.Length; i++)
                {
                    MS7H2QuestView q = b.quests[i];
                    if (q == null) { problem = true; sb.AppendLine("[FAIL] 7H2 null quest"); continue; }
                    sb.AppendLine("7H2: " + q.title + " " + q.current + "/" + q.target +
                        " ready=" + q.completed + " claimed=" + q.claimed + " +" + q.reward);
                    if (q.claimed) reading.claimCount++;
                    if (q.completed && !q.claimed) reading.readyCount++;
                    if (reading.safe && q.claimed && !q.completed)
                    { problem = true; sb.AppendLine("[FAIL] 7H2 claim without progress: " + q.title); }
                }
            }
            if (c.quests != null)
            {
                for (int i = 0; i < c.quests.Length; i++)
                {
                    MS7H3QuestView q = c.quests[i];
                    if (q == null) { problem = true; sb.AppendLine("[FAIL] 7H3 null quest"); continue; }
                    sb.AppendLine("7H3: " + q.title + " " + q.current + "/" + q.target +
                        " ready=" + q.completed + " claimed=" + q.claimed + " +" + q.reward);
                    if (q.claimed) reading.claimCount++;
                    if (q.completed && !q.claimed) reading.readyCount++;
                    // A period rollover may reset CURRENT progress after an earlier claimed reward.
                    // Do NOT infer Save corruption just because a periodic claim and current progress differ.
                }
            }
            sb.AppendLine("[UTC] Daily resets " + c.dailyUntilUtc + ", weekly resets " + c.weeklyUntilUtc);
            if (c.clockRollback) { problem = true; sb.AppendLine("[BLOCKED] UTC rollback detected: no Claims"); }
        }
        catch (Exception ex)
        {
            problem = true; reading.status = "READ ERROR";
            sb.AppendLine("[ERROR] Profile read threw exception: " + ex.Message);
        }
        return sb.ToString();
    }

    private static string ScanMarkerIntegrity(int slot, out bool bad)
    {
        bad = false;
        MS5A1SaveData data = null;
        bool recovered = false;
        string result = "";
        try
        {
            if (!MS5A1SaveService.TryLoad(slot, out data, out result, out recovered) || data == null || recovered)
                return "[PENDING] Profile " + slot + " marker audit skipped (missing/backup-only).";
            List<string> ids = data.unlockedContentIds;
            if (ids == null) return "[INFO] Profile " + slot + ": no quest markers yet.";
            // Unique singleton prefixes; a repeated marker makes counters ambiguous.
            string[] singleton = { "quest.7h2.progress.last.", "quest.7h2.progress.wins.",
                "quest.7h2.progress.bosses.", "quest.7h2.progress.gold.", "quest.7h3.v1." };
            foreach (string prefix in singleton)
            {
                int count = ids.Count(s => s != null && s.StartsWith(prefix, StringComparison.Ordinal));
                if (count > 1) { bad = true; return "[FAIL] Profile " + slot + ": duplicate marker prefix " + prefix + " count=" + count; }
            }
            HashSet<string> claimIds = new HashSet<string>();
            foreach (string id in ids)
            {
                if (id != null && (id.StartsWith("quest.7h1.claimed.", StringComparison.Ordinal) ||
                    id.StartsWith("quest.7h2.claimed.", StringComparison.Ordinal)) && !claimIds.Add(id))
                { bad = true; return "[FAIL] Profile " + slot + ": duplicate claim marker (do not claim again blindly)."; }
            }
            return "[PASS] Profile " + slot + ": quest marker uniqueness (7H1,7H2,7H3)";
        }
        catch (Exception ex) { bad = true; return "[ERROR] Profile " + slot + " marker audit: " + ex.Message; }
    }

    private string BuildAudit()
    {
        bool hard = false;
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(TitleText);
        sb.AppendLine("Generated=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz") +
            " Unity=" + Application.unityVersion + " Scene=" + SceneManager.GetActiveScene().name +
            " Play=" + Application.isPlaying + " SelectedProfile=" + MS5A2GoldBank.SelectedSlot);
        sb.AppendLine("[SAFETY] READ ONLY: no TrySave / Claim / Scene install / Save rewrite used.");
        string main = FindScenePath("MainMenu"), game = FindScenePath("SampleScene");
        string mainCheck = CheckScene(main, true);
        string gameCheck = CheckScene(game, false);
        sb.AppendLine(mainCheck);
        sb.AppendLine(gameCheck);
        structureVerified = !mainCheck.Contains("[FAIL]") && !gameCheck.Contains("[FAIL]") &&
            !mainCheck.Contains("[PENDING]") && !gameCheck.Contains("[PENDING]") &&
            !mainCheck.Contains("[ERROR]") && !gameCheck.Contains("[ERROR]");
        if (mainCheck.Contains("[FAIL]") || gameCheck.Contains("[FAIL]") ||
            mainCheck.Contains("[ERROR]") || gameCheck.Contains("[ERROR]")) hard = true;
        if (main == null || game == null) hard = true;
        MS7H4QuestToastController live = MS7H4QuestToastController.Instance;
        if (Application.isPlaying && live != null)
            sb.AppendLine("[LIVE 7H4] BaselineProfiles=" + live.BaselineProfiles + " completion=" + live.CompletionEvents +
                " claim=" + live.ClaimEvents + " preview=" + live.PreviewCount + " queued=" + live.QueuedCount);
        for (int slot = 1; slot <= 3; slot++)
        {
            ProfileReading p;
            bool issue, marker;
            sb.AppendLine(FormatProfile(slot, out p, out issue));
            sb.AppendLine(ScanMarkerIntegrity(slot, out marker));
            hard |= issue || marker;
        }
        sb.AppendLine("[PENDING] Verify 7H3 Daily/Weekly glyph visibility in Game View; the last uploaded runtime screenshot showed blank text.");
        sb.AppendLine("[NOTE] Snapshots cannot prove wallet delta, save durability, UI pixels or cross-profile isolation by themselves.");
        auditHadHardError = hard;
        auditRan = true;
        sb.AppendLine("STRUCTURE=" + (structureVerified ? "PASS" : "PENDING_OR_FAIL") +
            " PROFILE_HARD_ERROR=" + hard + ". Missing profiles remain PENDING, not auto-created.");
        return sb.ToString();
    }

    private static string RunPureTests()
    {
        int pass = 0, fail = 0;
        StringBuilder sb = new StringBuilder("7H5 IN-MEMORY PURE RULE TESTS (NO REAL FILE WRITES)\n");
        Action<bool, string> test = (condition, label) =>
        {
            if (condition) pass++; else fail++;
            sb.AppendLine((condition ? "[PASS] " : "[FAIL] ") + label);
        };
        try
        {
            MS5A1SaveData a = new MS5A1SaveData();
            a.bankGold = 200;
            a.unlockedContentIds = new List<string>();
            string info;
            test(!MS7H1QuestService.TryApplyToData(a, MS7H1QuestId.FirstVictory, out info) && a.bankGold == 200,
                "7H1 rejects Claim before first saved Victory");
            a.lastCommittedRunId = "QA_RUN_001";
            test(MS7H1QuestService.TryApplyToData(a, MS7H1QuestId.FirstVictory, out info) && a.bankGold == 260 &&
                 a.unlockedContentIds.Contains(MS7H1QuestService.Marker(MS7H1QuestId.FirstVictory)),
                "7H1 applies +60 Gold and one claim marker together in memory");
            test(!MS7H1QuestService.TryApplyToData(a, MS7H1QuestId.FirstVictory, out info) && a.bankGold == 260,
                "7H1 blocks double Claim");
            test(!MS7H1QuestService.TryApplyToData(a, MS7H1QuestId.DefeatTitan, out info),
                "7H1 rejects Boss reward without achievement");
            a.unlockedContentIds.Add("achievement.shadow_titan");
            test(MS7H1QuestService.TryApplyToData(a, MS7H1QuestId.DefeatTitan, out info) && a.bankGold == 380,
                "7H1 Boss achievement reward +120 once");
            MS5A1SaveData profileB = new MS5A1SaveData();
            profileB.bankGold = 11;
            profileB.unlockedContentIds = new List<string>();
            test(profileB.bankGold == 11 && !MS7H1QuestService.HasId(profileB, MS7H1QuestService.Marker(MS7H1QuestId.FirstVictory)),
                "7H1 in-memory Profile B isolated from Profile A");
            MS5A1SaveData progress = new MS5A1SaveData();
            progress.bankGold = 10;
            progress.unlockedContentIds = new List<string>();
            progress.lastCommittedRunId = "QA_RUN_1";
            test(MS7H2ProgressService.TryApplyRunToData(progress, "QA_RUN_1", 100, true, out info) &&
                MS7H2ProgressService.Wins(progress) == 1 && MS7H2ProgressService.Bosses(progress) == 1 &&
                MS7H2ProgressService.EarnedGold(progress) == 100,
                "7H2 records one committed run's wins, Boss and earned Gold");
            test(!MS7H2ProgressService.TryApplyRunToData(progress, "QA_RUN_1", 100, true, out info) &&
                MS7H2ProgressService.Wins(progress) == 1,
                "7H2 blocks repeated run ID");
            test(!MS7H2ProgressService.TryApplyRunToData(progress, "OTHER_RUN", 100, true, out info),
                "7H2 requires exact saved committed run ID");
            test(!MS7H2ProgressService.TryApplyClaimToData(progress, MS7H2QuestId.ThreeVictories, out info),
                "7H2 rejects premature reward");
            progress.lastCommittedRunId = "QA_RUN_2";
            test(MS7H2ProgressService.TryApplyRunToData(progress, "QA_RUN_2", 200, true, out info),
                "7H2 increments second distinct committed run");
            progress.lastCommittedRunId = "QA_RUN_3";
            test(MS7H2ProgressService.TryApplyRunToData(progress, "QA_RUN_3", 250, false, out info),
                "7H2 increments third distinct committed run");
            test(MS7H2ProgressService.TryApplyClaimToData(progress, MS7H2QuestId.ThreeVictories, out info) &&
                progress.bankGold == 160 && MS7H2ProgressService.IsClaimed(progress, MS7H2QuestId.ThreeVictories),
                "7H2 grants 3-Victory +150 Gold + claim marker in memory");
            test(!MS7H2ProgressService.TryApplyClaimToData(progress, MS7H2QuestId.ThreeVictories, out info) && progress.bankGold == 160,
                "7H2 blocks double Claim");
            test(MS7H2ProgressService.Wins(progress) == 3 && MS7H2ProgressService.Bosses(progress) == 2 &&
                 MS7H2ProgressService.EarnedGold(progress) == 550,
                "7H2 Gold earned distinct from Bank balance; 3 wins / 2 Bosses / 550 earned");
            MS5A1SaveData overflow = new MS5A1SaveData();
            overflow.bankGold = int.MaxValue;
            overflow.unlockedContentIds = new List<string>();
            overflow.lastCommittedRunId = "QA_OVERFLOW";
            test(!MS7H1QuestService.TryApplyToData(overflow, MS7H1QuestId.FirstVictory, out info) &&
                overflow.bankGold == int.MaxValue,
                "7H1 blocks reward overflow");
            string periodic = MS7H3PeriodicQuestService.PureRuleTests();
            sb.AppendLine("--- Original 7H3 isolated pure scenarios ---");
            sb.AppendLine(periodic.TrimEnd());
            bool periodicPassed = periodic.Contains("PURE PASS=") && periodic.Contains("FAIL=0");
            test(periodicPassed, "7H3 built-in daily/weekly rollover, repeat, claim and UTC tests report zero failures");
            string toasts = MS7H4QuestToastController.PureTests();
            sb.AppendLine("--- Original 7H4 in-memory transition scenarios ---");
            sb.AppendLine(toasts.TrimEnd());
            bool toastPassed = toasts.Contains("6/6 PASS");
            test(toastPassed, "7H4 built-in notification transition tests report zero failures");
        }
        catch (Exception e)
        {
            fail++;
            sb.AppendLine("[ERROR] Pure QA exception: " + e);
        }
        sb.AppendLine("RESULT: PURE_PASS=" + pass + " PURE_FAIL=" + fail +
            " | No production Save, Profile or Scene modified.");
        return sb.ToString();
    }

    private void CaptureBaseline()
    {
        ProfileReading current;
        bool problem;
        string details = FormatProfile(selectedSlot, out current, out problem);
        if (problem || !current.safe)
        {
            comparison = "[BLOCKED] Could not capture verified baseline for Profile " + selectedSlot + ".\n" + details;
            return;
        }
        baselines[selectedSlot] = current;
        comparison = "[CAPTURED] Profile " + selectedSlot + " | Bank Gold=" + current.gold +
            " | Revision=" + current.revision + " | Wins=" + current.wins + " Bosses=" + current.bosses +
            " EarnedGold=" + current.earnedGold + " Claimed=" + current.claimCount +
            "\nNext: perform a real Victory or Claim, then click COMPARE. This snapshot is only in Editor memory.";
        Debug.Log("[7H5] Captured read-only baseline for slot " + selectedSlot);
    }

    private void CompareBaseline()
    {
        ProfileReading before;
        if (!baselines.TryGetValue(selectedSlot, out before))
        {
            comparison = "[PENDING] Capture baseline for Profile " + selectedSlot + " first.";
            return;
        }
        ProfileReading current;
        bool problem;
        FormatProfile(selectedSlot, out current, out problem);
        if (problem || !current.safe)
        {
            comparison = "[BLOCKED] Save not safe to compare. Do not Claim until investigated.";
            return;
        }
        int goldDelta = current.gold - before.gold;
        int claimDelta = current.claimCount - before.claimCount;
        comparison = "[COMPARE] Profile " + selectedSlot + "\n" +
            "Bank Gold: " + before.gold + " -> " + current.gold + " (Delta=" + goldDelta + ")\n" +
            "Revision: " + before.revision + " -> " + current.revision + "\n" +
            "Wins: " + before.wins + " -> " + current.wins + " | Bosses: " + before.bosses + " -> " + current.bosses +
            " | EarnedGold: " + before.earnedGold + " -> " + current.earnedGold + "\n" +
            "Claimed flags: " + before.claimCount + " -> " + current.claimCount + " (Delta=" + claimDelta + ")\n" +
            "[PENDING MANUAL] Compare exact expected reward and ensure there were NO other shop/Save writes between snapshots. " +
            "Gold delta alone does not prove atomic durability. Reopen Unity and audit the same slot.";
        Debug.Log("[7H5] " + comparison);
    }

    private int ManualCompleted() { int count = 0; foreach (bool pass in confirmed) if (pass) count++; return count; }

    private void ExportReport()
    {
        audit = BuildAudit(); // Always refresh current profile state; avoid exporting stale audit after Claim.
        string reportFolder = Path.Combine(ProjectRoot, "MS_QAReports", "7H5");
        Directory.CreateDirectory(reportFolder);
        string path = Path.Combine(reportFolder, "MS7H5_FinalQuestSaveQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".txt");
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(TitleText);
        sb.AppendLine("Report exported " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
        sb.AppendLine("Mode=" + (Application.isPlaying ? "PLAY" : "EDIT") + "; selected slot " + MS5A2GoldBank.SelectedSlot);
        sb.AppendLine("RELEASE=" + (ManualCompleted() == ManualCount && testsPassed && auditRan && structureVerified && !auditHadHardError ?
            "MANUAL_REPORTED_READY_FOR_REVIEW (NOT CERTIFIED AUTOMATICALLY)" : "PENDING"));
        sb.AppendLine("[NOTE] Last uploaded 7H3 screenshot still had blank text. Confirm manual item 03 in Game View before release.");
        sb.AppendLine("\n=== STRUCTURE + PROFILE AUDIT (READ ONLY) ===\n" + audit);
        sb.AppendLine("\n=== PURE RULE TESTS (NO SAVE FILES WRITTEN) ===\n" + tests);
        sb.AppendLine("\n=== SELECTED PROFILE BASELINE COMPARISON ===\n" + comparison);
        sb.AppendLine("\n=== MANUAL QA ===");
        for (int i = 0; i < ManualCount; i++) sb.AppendLine((confirmed[i] ? "[USER CONFIRMED] " : "[NOT CONFIRMED] ") + ManualSteps[i]);
        sb.AppendLine("MANUAL=" + ManualCompleted() + "/" + ManualCount);
        sb.AppendLine("FINAL WARNING: automated counters and read-only snapshots cannot prove visual glyphs, real persisted Claim, or Profile isolation without manual checks.");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        lastExport = path;
        Debug.Log("[7H5] Exported read-only QA: " + path);
        EditorUtility.RevealInFinder(path);
    }
}
#endif
