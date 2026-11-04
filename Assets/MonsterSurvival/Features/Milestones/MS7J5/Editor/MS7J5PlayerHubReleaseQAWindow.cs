#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 7J.5 - Player Hub RELEASE QA. EDITOR-ONLY and READ-ONLY with respect to player state.
// Does NOT change Scenes, Prefabs, PlayerPrefs, Save, Gold, Quest, Rewards or UI.
// Only the explicit Export button writes one TXT into <ProjectRoot>/MS_QAReports/7J5.
public sealed class MS7J5PlayerHubReleaseQAWindow : EditorWindow
{
    private const int MaxTrace = 240;
    private const int MaxFiles = 512;
    private const long MaxFileBytes = 5L * 1024L * 1024L;
    private static readonly string[] TabNames = {
        "01 MANAGE SAVES", "02 PERMANENT UPGRADES", "03 ACHIEVEMENTS",
        "04 PROFILE & STATS", "05 LONG-TERM QUESTS", "06 PROGRESS / DAILY / WEEKLY",
        "07 ENDLESS RECORDS / REWARDS"
    };
    private static readonly string[] ManualLabels = {
        "01: Manage Saves opens -> X -> Dashboard, correct Profile",
        "02: Permanent Upgrades opens -> X -> Dashboard",
        "03: Achievements real popup opens -> X -> Dashboard",
        "04: Profile & Stats real popup opens -> X -> Dashboard",
        "05: Long-Term Quests opens -> X -> Dashboard",
        "06: Progress / Daily / Weekly opens -> X -> Dashboard",
        "07: Endless Records real popup opens -> X -> Dashboard",
        "Hover/select color returns to default after moving pointer away",
        "Return to Main Menu restores PLAY/MAP SELECT/SETTINGS/QUIT",
        "Profile 01/02/03 show separate Bank Gold/Quests/Records (no mixing)",
        "Quest CLAIM: correct Gold and revision; second claim blocked (use eligible quest)",
        "Endless CLAIM: pending run paid once, second claim blocked (use eligible run)",
        "Save reloaded after Play/Unity restart: Gold/claims/Inventory/Equipment correct",
        "Idle Hub browsing (no Claim) does not mutate tracked Save files",
        "Normal mode Wave 5 gives genuine Victory; no Endless reward ticket",
        "Endless mode Wave 6+ / Boss rotation works; Game Over correctly recorded",
        "Daily/Weekly not advanced by an Endless defeat or fake Victory",
        "1920x1080: all 7 tabs and BACK readable in Game View Fit",
        "2560x1440: all 7 tabs and BACK readable in Game View Fit",
        "1280x720: all 7 tabs and BACK readable in Game View Fit",
        "1024x768: one column, scroll to tab 07, controls readable",
        "Console: 0 C# compile errors and 0 new runtime exceptions",
        "No stale pink selection, black screen or blocked click after repeated opens",
        "Backup/rollback verified; original UI/Save/Prefab can be restored"
    };

    private readonly List<string> trace = new List<string>();
    private readonly bool[] observedOpen = new bool[7];
    private readonly bool[] observedReturn = new bool[7];
    private readonly bool[] manual = new bool[24];
    private readonly HashSet<string> testedResolutions = new HashSet<string>();
    private Dictionary<string, string> baselineFiles;
    private string baselineFingerprint = "NOT CAPTURED";
    private string compareReport = "Capture a baseline, browse Hub without Claim, then compare. The fingerprint is evidence only, NOT an authoritative Save audit.";
    private bool seenDashboard;
    private bool seenReturnToMenu;
    private int lastSelectedSlot = -1;
    private int lastModule = -1;
    private bool hadOpenModule;
    private bool enableTracking = true;
    private double nextSample;
    private string lastSignature = "";
    private string lastReport = "7J.5 is read-only. Play from MainMenu, open PlayerHub, test tabs and screen sizes, then AUDIT / EXPORT.";
    private Vector2 scroll;
    private int view; // 0 report, 1 manual checklist, 2 trace

    [MenuItem("Tools/MonsterSurvival/UI/7J.5 - Open Player Hub Release QA")]
    public static void OpenWindow()
    {
        MS7J5PlayerHubReleaseQAWindow window = GetWindow<MS7J5PlayerHubReleaseQAWindow>("7J.5 Player Hub QA");
        window.minSize = new Vector2(840f, 610f);
    }

    private void OnEnable() { EditorApplication.update += Sample; }
    private void OnDisable() { EditorApplication.update -= Sample; }

    private static T FindInScene<T>(string sceneName) where T : Component
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null) return component;
        }
        return null;
    }
    private static Transform FindChild(Transform parent, string name)
    {
        if (!parent) return null;
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindChild(child, name);
            if (found) return found;
        }
        return null;
    }
    private static GameObject FindNamed(string sceneName, string name)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindChild(root.transform, name);
            if (found) return found.gameObject;
        }
        return null;
    }
    private void Trace(string message)
    {
        trace.Add(DateTime.Now.ToString("HH:mm:ss.fff") + " " + message);
        if (trace.Count > MaxTrace) trace.RemoveAt(0);
    }
    private void Sample()
    {
        if (!enableTracking || EditorApplication.timeSinceStartup < nextSample) return;
        nextSample = EditorApplication.timeSinceStartup + 0.24d;
        if (!Application.isPlaying)
        {
            if (lastSignature != "EDIT") { Trace("EDIT MODE (runtime tracking paused)"); lastSignature = "EDIT"; }
            return;
        }
        MS7J2PlayerHubPolish hub = FindInScene<MS7J2PlayerHubPolish>("PlayerHub");
        MS7J4AdaptiveHubLayout responsive = FindInScene<MS7J4AdaptiveHubLayout>("PlayerHub");
        MS7J1HubEntry entry = FindInScene<MS7J1HubEntry>("MainMenu");
        int slot = MS5A2GoldBank.SelectedSlot;
        int tab = hub ? hub.ActiveModuleIndex : -1;
        bool dashboard = hub && hub.DashboardVisible;
        string popup = hub ? hub.TrackedPopupName : "NONE";
        string state = hub ? hub.State : "NOT LOADED";
        string res = Screen.width + "x" + Screen.height;
        string signature = "slot=" + slot + "|hub=" + state + "|tab=" + tab + "|home=" + dashboard +
            "|popup=" + popup + "|res=" + res + "|cols=" + (responsive ? responsive.Columns.ToString() : "-") +
            "|entry=" + (entry ? entry.LastStatus : "MISSING");
        if (signature != lastSignature)
        {
            lastSignature = signature;
            Trace(signature);
        }
        if (lastSelectedSlot != slot)
        {
            if (lastSelectedSlot > 0) Trace("PROFILE SWITCH " + lastSelectedSlot + " -> " + slot);
            lastSelectedSlot = slot;
        }
        if (dashboard && responsive && responsive.enabled && responsive.Columns > 0)
        {
            seenDashboard = true;
            string resolution = Screen.width + "x" + Screen.height;
            testedResolutions.Add(resolution);
        }
        if (tab >= 0 && tab < 7 && popup != "NONE")
        {
            observedOpen[tab] = true;
            lastModule = tab;
            hadOpenModule = true;
        }
        if (dashboard && tab == -1 && hadOpenModule && lastModule >= 0)
        {
            observedReturn[lastModule] = true;
            Trace("RETURN TO DASHBOARD after tab " + (lastModule + 1).ToString("00"));
            hadOpenModule = false;
            lastModule = -1;
        }
        if (seenDashboard && !hub && entry && entry.LastStatus.Contains("opened")) seenReturnToMenu = true;
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("7J.5 / FINAL PLAYER HUB QA + RELEASE GATE", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("READ-ONLY: no Scene changes, no Save/Claim calls. Capture baseline/compare only READ existing files; Export writes a TXT outside Assets. Tick manual items ONLY after visual verification.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            enableTracking = GUILayout.Toggle(enableTracking, "Live trace", "Button", GUILayout.Width(110), GUILayout.Height(31));
            if (GUILayout.Button("1 - AUDIT CURRENT SESSION", GUILayout.Height(31))) { lastReport = BuildReport(); view = 0; }
            if (GUILayout.Button("2 - RUN PURE RULE TESTS", GUILayout.Height(31))) { lastReport = PureTests(); view = 0; }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("3 - CAPTURE SAVE FILE BASELINE (READ ONLY)", GUILayout.Height(32))) CaptureBaseline();
            if (GUILayout.Button("4 - COMPARE SAVE FILES (READ ONLY)", GUILayout.Height(32))) CompareBaseline();
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("5 - EXPORT 7J.5 QA TXT", GUILayout.Height(32))) Export();
            if (GUILayout.Button("RESET SESSION CHECKS (NO SAVE WRITE)", GUILayout.Height(32))) ResetEvidence();
        }
        GUILayout.Space(6);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Toggle(view == 0, "QA REPORT", "Button")) view = 0;
            if (GUILayout.Toggle(view == 1, "MANUAL CHECKLIST", "Button")) view = 1;
            if (GUILayout.Toggle(view == 2, "LIVE TRACE", "Button")) view = 2;
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (view == 0)
        {
            EditorGUILayout.TextArea(lastReport, GUILayout.MinHeight(370f), GUILayout.ExpandHeight(true));
        }
        else if (view == 1)
        {
            EditorGUILayout.HelpBox("24 manual checks. Automatic tracking does NOT certify rewards, Save consistency or visual quality.", MessageType.Warning);
            for (int i = 0; i < manual.Length; i++) manual[i] = EditorGUILayout.ToggleLeft(ManualLabels[i], manual[i]);
            GUILayout.Space(8);
            for (int i = 0; i < 7; i++)
                EditorGUILayout.LabelField(TabNames[i] + " | Open=" + observedOpen[i] + " Return=" + observedReturn[i]);
        }
        else EditorGUILayout.TextArea(trace.Count == 0 ? "No events yet" : string.Join("\n", trace.ToArray()), GUILayout.MinHeight(370), GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
        EditorGUILayout.LabelField("RELEASE GATE: " + Gate(), EditorStyles.boldLabel);
    }

    private void ResetEvidence()
    {
        Array.Clear(manual, 0, manual.Length);
        Array.Clear(observedOpen, 0, observedOpen.Length);
        Array.Clear(observedReturn, 0, observedReturn.Length);
        trace.Clear();
        testedResolutions.Clear();
        seenDashboard = false;
        seenReturnToMenu = false;
        lastSelectedSlot = -1;
        hadOpenModule = false;
        lastModule = -1;
        lastSignature = "";
        lastReport = "Manual and trace evidence reset. Existing Save baseline is unchanged; no Save files modified.";
    }

    private static Dictionary<string, string> ReadFileFingerprints(out string warning)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        string root = Application.persistentDataPath;
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            warning = "persistentDataPath missing; fingerprint is unavailable";
            return result;
        }
        string[] paths;
        try { paths = Directory.GetFiles(root, "*", SearchOption.AllDirectories); }
        catch (Exception ex) { warning = "List files error: " + ex.Message; return result; }
        Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
        int skipped = 0;
        using (SHA256 sha = SHA256.Create())
        {
            foreach (string path in paths)
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext != ".json" && ext != ".sav" && ext != ".dat" && ext != ".bak" &&
                    ext != ".bin" && ext != ".bytes" && ext != ".txt" && ext != ".save") continue;
                if (result.Count >= MaxFiles) { skipped++; continue; }
                try
                {
                    FileInfo info = new FileInfo(path);
                    if (info.Length > MaxFileBytes) { skipped++; continue; }
                    byte[] hash;
                    using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        hash = sha.ComputeHash(stream);
                    string relative = path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    result[relative.Replace('\\', '/')] = BitConverter.ToString(hash).Replace("-", "") + " (" + info.Length + " B)";
                }
                catch (Exception ex) { errors.Add(Path.GetFileName(path) + "=" + ex.GetType().Name); }
            }
        }
        warning = "Tracked=" + result.Count + " Skipped=" + skipped + " ReadErrors=" + errors.Count +
            (errors.Count > 0 ? " (" + string.Join(",", errors.Take(4).ToArray()) + ")" : "") +
            ". Coverage: supported extensions <=5MB within persistentDataPath only; other storage NOT covered.";
        return result;
    }

    private void CaptureBaseline()
    {
        string message;
        baselineFiles = ReadFileFingerprints(out message);
        baselineFingerprint = DateTime.Now.ToString("o") + " / " + message;
        compareReport = "BASELINE: " + baselineFingerprint;
        Trace("SAVE FILE BASELINE CAPTURED " + message);
        lastReport = BuildReport();
        view = 0;
    }
    private void CompareBaseline()
    {
        if (baselineFiles == null) { compareReport = "NO BASELINE. Click 3 first."; lastReport = BuildReport(); return; }
        string message;
        Dictionary<string, string> now = ReadFileFingerprints(out message);
        string[] added = now.Keys.Except(baselineFiles.Keys, StringComparer.OrdinalIgnoreCase).ToArray();
        string[] missing = baselineFiles.Keys.Except(now.Keys, StringComparer.OrdinalIgnoreCase).ToArray();
        string[] changed = baselineFiles.Keys.Intersect(now.Keys, StringComparer.OrdinalIgnoreCase)
            .Where(k => baselineFiles[k] != now[k]).ToArray();
        compareReport = "COMPARE " + DateTime.Now.ToString("o") + " / " + message +
            " / ADDED=" + added.Length + " MISSING=" + missing.Length + " CHANGED=" + changed.Length +
            " / " + ((added.Length + missing.Length + changed.Length) == 0 ? "UNCHANGED in tracked subset" : "DIFFERENCE - review expected vs unexpected writes");
        // Don't export sensitive filenames / file bytes; only counts and hash-based comparison summary.
        Trace("SAVE FILES " + compareReport);
        lastReport = BuildReport();
        view = 0;
    }

    private string Gate()
    {
        if (!Application.isPlaying) return "PENDING - Play Mode evidence required";
        if (!seenDashboard) return "PENDING - open PlayerHub with adaptive Dashboard";
        if (manual.Any(x => !x)) return "PENDING - manual " + manual.Count(x => x) + "/" + manual.Length;
        if (observedOpen.Any(x => !x) || observedReturn.Any(x => !x)) return "PENDING - all 7 open/return transitions required";
        if (baselineFiles == null || baselineFiles.Count == 0) return "PENDING - no tracked Save fingerprint baseline";
        if (compareReport.StartsWith("BASELINE") || compareReport.StartsWith("NO BASELINE")) return "PENDING - Save comparison required";
        return "READY FOR USER REVIEW (manual + tracked evidence; not automatic release)";
    }

    private string BuildReport()
    {
        var b = new StringBuilder();
        b.AppendLine("MONSTERSURVIVAL / 7J.5 FINAL PLAYER HUB QA + RELEASE GATE");
        b.AppendLine("Generated=" + DateTime.Now.ToString("o") + " Unity=" + Application.unityVersion +
            " Play=" + Application.isPlaying + " Scene=" + SceneManager.GetActiveScene().name);
        b.AppendLine("SOURCE BASELINE=7J.2.3 + 7J.4.1 (TMP compile hotfix), 7J.4 Adaptive Hub");
        b.AppendLine("SELECTED PROFILE=" + MS5A2GoldBank.SelectedSlot.ToString("00"));
        b.AppendLine("SCENE FILE PlayerHub=" + (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/MonsterSurvival/Content/Scenes/PlayerHub.unity") ? "PRESENT" : "MISSING"));
        b.AppendLine("BUILD PROFILE PlayerHub loadable=" + Application.CanStreamedLevelBeLoaded("PlayerHub"));
        Scene mainScene = SceneManager.GetSceneByName("MainMenu");
        Scene hubScene = SceneManager.GetSceneByName("PlayerHub");
        b.AppendLine("SCENE MainMenu loaded=" + (mainScene.IsValid() && mainScene.isLoaded) + " PlayerHub loaded=" + (hubScene.IsValid() && hubScene.isLoaded));
        MS7J1HubEntry entry = FindInScene<MS7J1HubEntry>("MainMenu");
        MS7J2PlayerHubPolish hub = FindInScene<MS7J2PlayerHubPolish>("PlayerHub");
        MS7J4AdaptiveHubLayout responsive = FindInScene<MS7J4AdaptiveHubLayout>("PlayerHub");
        MMV3MenuController menu = FindInScene<MMV3MenuController>("MainMenu");
        b.AppendLine("7J1 ENTRY=" + (entry ? entry.LastStatus : "MISSING/NOT PLAY"));
        b.AppendLine("7J2 DASHBOARD=" + (hub ? hub.DashboardVisible.ToString() : "NOT LOADED") +
            " ActiveTab=" + (hub ? (hub.ActiveModuleIndex + 1).ToString("00") : "NA") +
            " Popup=" + (hub ? hub.TrackedPopupName : "NA") +
            " SyncSummary=" + (hub ? hub.SyncSummary : "NA"));
        b.AppendLine("7J4 RESPONSIVE=" + (responsive ? (responsive.enabled ? "ENABLED" : "DISABLED") + " / " + responsive.Status +
            " / Columns=" + responsive.Columns + " / Canvas=" + responsive.CanvasWidth.ToString("F0") + "x" + responsive.CanvasHeight.ToString("F0") +
            " / ViewportWidth=" + responsive.ViewportWidth.ToString("F0") + " / ContentHeight=" + responsive.ContentHeight.ToString("F0") +
            " / ScrollRequired=" + responsive.VerticalScrollRequired : "NOT LOADED"));
        b.AppendLine("RENDER RESOLUTION=" + Screen.width + "x" + Screen.height + " SafeArea=" + Screen.safeArea +
            " (Game View preview zoom is an Editor setting, not Canvas scale)");
        b.AppendLine("RESOLUTIONS OBSERVED WHILE HUB OPEN=" + (testedResolutions.Count == 0 ? "NONE" : string.Join(",", testedResolutions.OrderBy(x=>x).ToArray())));
        b.AppendLine("MAIN MENU=" + (menu ? "Home=" + (menu.homePage ? menu.homePage.activeInHierarchy.ToString() : "MISSING") +
            " Maps=" + (menu.mapsPage ? menu.mapsPage.activeInHierarchy.ToString() : "MISSING") : "MISSING"));
        b.AppendLine("EVENT SYSTEM SELECTED=" + (EventSystem.current && EventSystem.current.currentSelectedGameObject ? EventSystem.current.currentSelectedGameObject.name : "NONE"));
        b.AppendLine("TABS:");
        for (int i = 0; i < 7; i++)
        {
            GameObject card = FindNamed("PlayerHub", "Category_" + (i + 1));
            b.AppendLine("  " + TabNames[i] + " Card=" + (card ? "PRESENT" : "NOT LOADED") +
                " OpenObserved=" + observedOpen[i] + " ReturnedObserved=" + observedReturn[i] + " Manual=" + manual[i]);
        }
        b.AppendLine("OBSERVED HUB=" + seenDashboard + " RETURNED MAIN MENU=" + seenReturnToMenu);
        b.AppendLine("SAVE FINGERPRINT BASELINE=" + baselineFingerprint);
        b.AppendLine("SAVE FINGERPRINT COMPARE=" + compareReport);
        b.AppendLine("MANUAL CHECKLIST=" + manual.Count(x => x) + "/" + manual.Length);
        b.AppendLine("RELEASE GATE=" + Gate());
        b.AppendLine("NO GAME STATE WRITES by this QA; no Save/Quest/Claim calls. Export TXT only.");
        b.AppendLine("=== TRACE (last " + MaxTrace + ") ===");
        foreach (string line in trace) b.AppendLine(line);
        return b.ToString();
    }

    private static string PureTests()
    {
        var b = new StringBuilder("7J.5 PURE QA TESTS (no Scene/Save/Gold/Quest writes)\n");
        int pass = 0, fail = 0;
        Action<bool, string> test = (ok, label) => { b.AppendLine((ok ? "PASS " : "FAIL ") + label); if (ok) pass++; else fail++; };
        test(TabNames.Length == 7, "Exactly 7 tabs");
        test(ManualLabels.Length == 24, "24 manual release gates");
        test(TabNames.Distinct().Count() == 7, "No duplicate tab IDs");
        test(TabNames[2].Contains("ACHIEVEMENTS"), "Critical tab 03 represented");
        test(TabNames[3].Contains("PROFILE"), "Critical tab 04 represented");
        test(TabNames[6].Contains("ENDLESS"), "Critical tab 07 represented");
        test(ManualLabels.Any(x=>x.Contains("Hover")), "Hover regression represented");
        test(ManualLabels.Any(x=>x.Contains("1024x768")), "4:3 scroll represented");
        test(ManualLabels.Any(x=>x.Contains("2560x1440")), "QHD represented");
        test(ManualLabels.Any(x=>x.Contains("CLAIM")), "Claim safety represented");
        test(ManualLabels.Any(x=>x.Contains("Normal")), "Normal/Endless isolation represented");
        test(MaxTrace >= 200 && MaxFiles <= 512, "Bounded tracking buffers");
        test(MS7J4AdaptiveHubLayout.ChooseColumns(1920f,1080f,1.48f)==2, "FHD layout policy 2 columns");
        test(MS7J4AdaptiveHubLayout.ChooseColumns(1024f,768f,1.48f)==1, "4:3 layout policy 1 column");
        test(MS7J4AdaptiveHubLayout.AvailableCardWidth(1500f,2,18f,4f)>500f, "Grid cell width positive");
        b.AppendLine("PURE RESULT PASS=" + pass + " FAIL=" + fail);
        b.AppendLine("Pure tests do not certify real popup clickability, scene load, Save mutation absence, rewards or UI readability.");
        return b.ToString();
    }
    private void Export()
    {
        try
        {
            string report = BuildReport();
            string folder = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "MS_QAReports", "7J5");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "MS7J5_PlayerHubReleaseQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".txt");
            File.WriteAllText(path, report, new UTF8Encoding(false));
            lastReport = "[EXPORTED] " + path + "\n" + report;
            view = 0;
            Debug.Log("[7J.5] READ-ONLY QA report exported: " + path);
        }
        catch (Exception ex)
        {
            lastReport = "[7J.5] EXPORT FAILED: " + ex;
            Debug.LogError(lastReport);
        }
    }
}
#endif
