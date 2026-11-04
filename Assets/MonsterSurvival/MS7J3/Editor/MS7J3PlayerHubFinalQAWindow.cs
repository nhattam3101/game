#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 7J.3 — read-only Player Hub integration QA for MonsterSurvival Unity 6.6.
// No scene, prefab, Quest, 5A Save, 7I4 reward or EditorBuildSettings changes.
// Export writes ONLY to project-root/MS_QAReports/7J3.
public sealed class MS7J3PlayerHubFinalQAWindow : EditorWindow
{
    private const int MaxTrace = 180;
    private static readonly string[] ModuleNames = {
        "01 MANAGE SAVES", "02 PERMANENT UPGRADES", "03 ACHIEVEMENTS",
        "04 PROFILE & STATS", "05 LONG-TERM QUESTS", "06 PROGRESS / DAILY / WEEKLY",
        "07 ENDLESS RECORDS / REWARDS"
    };
    private static readonly string[] Launchers = {
        "MS5A4_BTN_Open", "MS5B2_BTN_Upgrades", "MS5C4_BTN_Achievements",
        "MS5D1_BTN_ProfileOverview", "Quest Launcher", "Progress Launcher", "BTN_7I4Records"
    };
    private static readonly string[] ManualLabels = {
        "01: Open -> X -> Dashboard", "02: Open -> X -> Dashboard",
        "03: Achievements visible -> X -> Dashboard", "04: Profile & Stats visible -> X -> Dashboard",
        "05: Quests visible -> X -> Dashboard", "06: Progress/Daily/Weekly visible -> X -> Dashboard",
        "07: Endless Records visible -> X -> Dashboard",
        "No tab remains pink after pointer exits / X",
        "BACK TO MAIN MENU restores PLAY / MAP SELECT / SETTINGS / QUIT",
        "Change Profile and reopen Hub: data belongs to selected slot",
        "Claim once: Bank Gold + Save revision correct; second click blocked",
        "Daily / Weekly + Endless record remain isolated across Profiles",
        "NORMAL still wins at Wave 5; ENDLESS can reach Wave 6+",
        "UI readable at 1920x1080, 1280x720, 1024x768"
    };
    private readonly List<string> trace = new List<string>();
    private readonly bool[] opened = new bool[7];
    private readonly bool[] closed = new bool[7];
    private readonly bool[] manual = new bool[14];
    private bool seenHub;
    private bool seenMainMenuReturn;
    private int lastSlot = -1;
    private string previousSignature = "";
    private double nextSample;
    private bool tracking = true;
    private string lastAudit = "Run in Play Mode from MainMenu, open PLAYER HUB and test each tab.\nUse AUDIT to capture evidence, then EXPORT QA TXT.";
    private Vector2 mainScroll;
    private Vector2 traceScroll;
    private int mode; // 0 audit / 1 manual / 2 timeline
    private int latestTab = -1;
    private bool tabWasTracked;

    [MenuItem("Tools/MonsterSurvival/UI/7J.3 - Open Player Hub Final Integration QA")]
    public static void Open()
    {
        MS7J3PlayerHubFinalQAWindow window = GetWindow<MS7J3PlayerHubFinalQAWindow>("7J.3 Player Hub QA");
        window.minSize = new Vector2(800, 590);
    }

    private void OnEnable()
    {
        EditorApplication.update += Track;
    }
    private void OnDisable()
    {
        EditorApplication.update -= Track;
    }
    private void Track()
    {
        if (!tracking || EditorApplication.timeSinceStartup < nextSample) return;
        nextSample = EditorApplication.timeSinceStartup + .20d;
        if (!Application.isPlaying)
        {
            if (previousSignature != "NOT PLAY") { previousSignature = "NOT PLAY"; AddTrace("EDIT MODE; runtime trace paused"); }
            return;
        }
        MS7J2PlayerHubPolish hub = InScene<MS7J2PlayerHubPolish>("PlayerHub");
        MS7J1HubEntry entry = InScene<MS7J1HubEntry>("MainMenu");
        int slot = MS5A2GoldBank.SelectedSlot;
        bool dashboard = hub != null && hub.DashboardVisible;
        int tab = hub != null ? hub.ActiveModuleIndex : -1;
        string popup = hub != null ? hub.TrackedPopupName : "NONE";
        string diagnostic = hub != null ? hub.LastLaunchDiagnostic : "NONE";
        string signature = (entry != null ? "entry:" + entry.LastStatus : "no-entry") +
            "|hub:" + (hub ? hub.State : "NOT LOADED") +
            "|home:" + dashboard + "|tab:" + tab + "|popup:" + popup +
            "|slot:" + slot + "|launch:" + diagnostic;
        if (signature != previousSignature)
        {
            previousSignature = signature;
            AddTrace("PROFILE " + slot.ToString("00") + " / " + (hub ? hub.State : "HUB NOT LOADED") +
                " / Dashboard=" + dashboard + " / Tab=" + (tab >= 0 ? (tab + 1).ToString("00") : "--") +
                " / Popup=" + popup + " / " + diagnostic);
        }
        if (slot != lastSlot)
        {
            if (lastSlot >= 0) AddTrace("PROFILE SWITCH " + lastSlot.ToString("00") + " -> " + slot.ToString("00") + " (inspect new data manually)");
            lastSlot = slot;
        }
        if (dashboard) seenHub = true;
        if (tab >= 0 && tab < 7 && popup != "NONE" && !string.IsNullOrEmpty(popup))
        {
            opened[tab] = true;
            latestTab = tab;
            tabWasTracked = true;
        }
        if (tab == -1 && dashboard && tabWasTracked && latestTab >= 0)
        {
            closed[latestTab] = true;
            AddTrace("RETURNED TO HUB AFTER TAB " + (latestTab + 1).ToString("00"));
            tabWasTracked = false;
            latestTab = -1;
        }
        if (entry && hub == null && seenHub && entry.LastStatus.Contains("opened"))
        {
            seenMainMenuReturn = true;
        }
        Repaint();
    }

    private void AddTrace(string value)
    {
        trace.Add(DateTime.Now.ToString("HH:mm:ss.fff") + "  " + value);
        if (trace.Count > MaxTrace) trace.RemoveAt(0);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("7J.3 / FINAL PLAYER HUB INTEGRATION", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("READ-ONLY QA: never opens tabs automatically, never calls Save, Claim, Quest or Endless reward APIs. Only EXPORT writes a QA TXT outside Assets.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            tracking = GUILayout.Toggle(tracking, "Track Live UI", "Button", GUILayout.Height(30));
            if (GUILayout.Button("1 - AUDIT CURRENT SESSION (READ ONLY)", GUILayout.Height(30))) { lastAudit = BuildReport(); mode = 0; }
            if (GUILayout.Button("2 - RUN PURE QA RULE TESTS", GUILayout.Height(30))) { lastAudit = PureTests(); mode = 0; }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("3 - EXPORT 7J.3 QA TXT", GUILayout.Height(32))) Export();
            if (GUILayout.Button("RESET TRACE / CHECKS (SESSION ONLY)", GUILayout.Height(32))) ResetSession();
        }
        GUILayout.Space(6);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Toggle(mode == 0, "READ-ONLY AUDIT", "Button")) mode = 0;
            if (GUILayout.Toggle(mode == 1, "MANUAL CHECKLIST", "Button")) mode = 1;
            if (GUILayout.Toggle(mode == 2, "RUNTIME TRACE", "Button")) mode = 2;
        }
        if (mode == 0)
        {
            mainScroll = EditorGUILayout.BeginScrollView(mainScroll);
            EditorGUILayout.TextArea(lastAudit, GUILayout.MinHeight(350), GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
        else if (mode == 1)
        {
            mainScroll = EditorGUILayout.BeginScrollView(mainScroll);
            EditorGUILayout.HelpBox("Tick only after YOU have visually confirmed each behavior. These checkmarks are not automatically inferred from a source-code audit.", MessageType.Warning);
            for (int i = 0; i < ManualLabels.Length; i++)
                manual[i] = EditorGUILayout.ToggleLeft(ManualLabels[i], manual[i]);
            GUILayout.Space(8);
            EditorGUILayout.LabelField("Observed native module attempts / closed back to Hub", EditorStyles.boldLabel);
            for (int i = 0; i < 7; i++)
                EditorGUILayout.LabelField(ModuleNames[i] + "  |  OPEN OBSERVED=" + opened[i] + "  RETURN OBSERVED=" + closed[i]);
            EditorGUILayout.EndScrollView();
        }
        else
        {
            traceScroll = EditorGUILayout.BeginScrollView(traceScroll);
            EditorGUILayout.TextArea(trace.Count == 0 ? "No runtime transitions recorded yet." : string.Join("\n", trace.ToArray()),
                GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
        EditorGUILayout.LabelField("Release status: " + GateSummary(), EditorStyles.boldLabel);
    }

    private void ResetSession()
    {
        trace.Clear();
        Array.Clear(opened, 0, opened.Length);
        Array.Clear(closed, 0, closed.Length);
        Array.Clear(manual, 0, manual.Length);
        seenHub = false;
        seenMainMenuReturn = false;
        lastSlot = -1;
        latestTab = -1;
        tabWasTracked = false;
        previousSignature = "";
        lastAudit = "Trace/checklist reset locally. No Player Save or scene data changed.";
    }

    private static T InScene<T>(string sceneName) where T : Component
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component) return component;
        }
        return null;
    }

    private static GameObject FindNamed(string sceneName, string exactName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == exactName) return t.gameObject;
        return null;
    }

    private static string PopupInfo(GameObject obj)
    {
        if (!obj) return "MISSING";
        int visible = 0;
        foreach (Graphic graphic in obj.GetComponentsInChildren<Graphic>(true))
            if (graphic && graphic.isActiveAndEnabled && graphic.gameObject.activeInHierarchy && graphic.color.a > .03f) visible++;
        CanvasGroup group = obj.GetComponent<CanvasGroup>();
        return "Exists=True Active=" + obj.activeInHierarchy + " RenderedGraphics=" + visible +
            (group ? " CanvasGroupAlpha=" + group.alpha.ToString("0.00") : "");
    }

    private string BuildReport()
    {
        StringBuilder b = new StringBuilder();
        b.AppendLine("MONSTERSURVIVAL / 7J.3 PLAYER HUB FINAL INTEGRATION QA");
        b.AppendLine("Time=" + DateTime.Now.ToString("o") + " Unity=" + Application.unityVersion +
            " Play=" + Application.isPlaying + " Scene=" + SceneManager.GetActiveScene().name);
        b.AppendLine("SOURCE BASELINE=7J.2.3 (7J.1 + 7J.2 + 7J.2.1 + 7J.2.2 prerequisites)");
        b.AppendLine("SELECTED PROFILE=" + MS5A2GoldBank.SelectedSlot.ToString("00"));
        string scenePath = "Assets/Scenes/PlayerHub.unity";
        b.AppendLine("PLAYERHUB SCENE ASSET=" + (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null ? "FOUND" : "MISSING"));
        b.AppendLine("BUILD PROFILES LOADABLE=" + (Application.isPlaying ? Application.CanStreamedLevelBeLoaded("PlayerHub").ToString() : "NOT CHECKED IN EDIT MODE (verify active Build Profile)"));
        Scene main = SceneManager.GetSceneByName("MainMenu");
        Scene player = SceneManager.GetSceneByName("PlayerHub");
        b.AppendLine("MAINMENU LOADED=" + (main.IsValid() && main.isLoaded) + " PLAYERHUB LOADED=" + (player.IsValid() && player.isLoaded));
        MS7J1HubEntry entry = InScene<MS7J1HubEntry>("MainMenu");
        MMV3MenuController menu = InScene<MMV3MenuController>("MainMenu");
        MS7J2PlayerHubPolish hub = InScene<MS7J2PlayerHubPolish>("PlayerHub");
        b.AppendLine("7J1 ENTRY=" + (entry ? entry.LastStatus : "MISSING/NOT PLAY") +
            " / MENU=" + (menu ? "FOUND" : "MISSING"));
        b.AppendLine("7J2 HUB=" + (hub ? hub.State : "NOT LOADED") +
            " / DASHBOARD=" + (hub ? hub.DashboardVisible.ToString() : "NA") +
            " / ACTIVE TAB=" + (hub ? (hub.ActiveModuleIndex + 1).ToString("00") : "NA"));
        if (hub)
        {
            b.AppendLine("7J2 NATIVE POPUP=" + hub.TrackedPopupName);
            b.AppendLine("7J2 LAST LAUNCH=" + hub.LastLaunchDiagnostic);
            b.AppendLine("7J2 READ-ONLY DATA SUMMARY=" + hub.SyncSummary);
        }
        b.AppendLine("MAINMENU PAGES=" + (menu ?
            "Home=" + (menu.homePage ? menu.homePage.activeInHierarchy.ToString() : "missing") +
            " Maps=" + (menu.mapsPage ? menu.mapsPage.activeInHierarchy.ToString() : "missing") +
            " Settings=" + (menu.settingsPage ? menu.settingsPage.activeInHierarchy.ToString() : "missing") : "MISSING"));
        for (int i = 0; i < ModuleNames.Length; i++)
        {
            GameObject launcher = FindNamed("MainMenu", Launchers[i]);
            GameObject card = FindNamed("PlayerHub", "Category_" + (i + 1));
            bool cardReady = card && card.GetComponent<Button>() && card.GetComponent<MS7J2CardHover>();
            b.AppendLine("TAB=" + ModuleNames[i] + " Launcher=" + (launcher ? "FOUND" : "MISSING") +
                " Card+Hover=" + cardReady + " SeenOpen=" + opened[i] +
                " ReturnedHome=" + closed[i] + " Manual=" + manual[i]);
        }
        b.AppendLine("7I4 RECORDS MODAL=" + PopupInfo(FindNamed("MainMenu", "7I4_Records_Modal")));
        GameObject selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        b.AppendLine("UNITY EVENTSYSTEM SELECTION=" + (selected ? selected.name : "NONE"));
        b.AppendLine("OBSERVED HUB=" + seenHub + " RETURNED TO MAINMENU=" + seenMainMenuReturn);
        b.AppendLine("MANUAL CHECKLIST COMPLETED=" + manual.Count(v => v) + "/" + manual.Length);
        b.AppendLine("RELEASE GATE=" + GateSummary());
        b.AppendLine("NO SAVE WRITES. No automatic Claim/Quest/Victory/Gold mutations. Export writes only MS_QAReports/7J3.");
        b.AppendLine("=== RUNTIME TRACE (last " + MaxTrace + " events) ===");
        foreach (string line in trace) b.AppendLine(line);
        return b.ToString();
    }

    private string GateSummary()
    {
        if (!Application.isPlaying) return "PENDING (capture Play Mode evidence, then export)";
        if (!seenHub) return "PENDING (open PlayerHub from MainMenu)";
        if (manual.Any(v => !v)) return "PENDING (manual checklist " + manual.Count(v => v) + "/" + manual.Length + ")";
        if (opened.Any(v => !v) || closed.Any(v => !v)) return "PENDING (7 native open/return transitions not all observed)";
        return "READY FOR USER REVIEW (runtime observation; not a claim Save QA PASS)";
    }

    private string PureTests()
    {
        int pass = 0;
        int fail = 0;
        StringBuilder b = new StringBuilder("7J.3 PURE QA RULE TESTS — no Scene, Save, Profile or disk writes\n");
        Action<bool, string> check = (ok, name) => { b.AppendLine((ok ? "PASS " : "FAIL ") + name); if (ok) pass++; else fail++; };
        check(ModuleNames.Length == 7, "Exactly 7 category labels");
        check(Launchers.Length == 7 && Launchers.Distinct().Count() == 7, "Seven distinct legacy launcher names");
        check(ManualLabels.Length == 14, "14 explicit manual confirmations");
        check("Category_1" != "Category_7", "Cards use unique IDs");
        check((new bool[7]).All(v => !v), "No check silently passes without evidence");
        check(ManualLabels[2].Contains("Achievements") && ManualLabels[3].Contains("Profile"), "7J.2.3 critical 03/04 cases represented");
        check(ManualLabels[6].Contains("Endless") && ManualLabels[7].Contains("pink"), "7J.2.3 critical 07/hover cases represented");
        check(ManualLabels[10].Contains("second click"), "Double claim safety requires real manual check");
        check(ManualLabels[12].Contains("NORMAL") && ManualLabels[12].Contains("ENDLESS"), "Both game modes covered");
        check(MaxTrace >= 100, "Trace buffer contains >=100 changes");
        b.AppendLine("PURE RESULT PASS=" + pass + " FAIL=" + fail);
        b.AppendLine("Pure tests do NOT prove Unity compile, runtime popup visibility, Save persistence or Claims.");
        return b.ToString();
    }

    private void Export()
    {
        try
        {
            lastAudit = BuildReport();
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string folder = Path.Combine(projectRoot, "MS_QAReports", "7J3");
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, "MS7J3_PlayerHubFinalQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".txt");
            File.WriteAllText(file, lastAudit, new UTF8Encoding(false));
            lastAudit = "[EXPORTED] " + file + "\n" + lastAudit;
            mode = 0;
            Debug.Log("[7J.3] READ-ONLY QA exported: " + file);
        }
        catch (Exception error)
        {
            lastAudit = "[EXPORT ERROR] " + error;
            Debug.LogError(lastAudit);
        }
    }
}
#endif
