#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 7J.4 installation is deliberately additive: one runtime component on PlayerHub.
// The MainMenu scene is audited, not rewritten: its known CanvasScaler is already correct.
public sealed class MS7J4ResponsiveInstaller : EditorWindow
{
    private const string HubPath = "Assets/MonsterSurvival/Content/Scenes/PlayerHub.unity";
    private const string MenuPath = "Assets/MonsterSurvival/Content/Scenes/MainMenu.unity";
    private string report = "7J.4 ready. Run AUDIT before installation.";
    private Vector2 scroll;

    [MenuItem("Tools/MonsterSurvival/UI/7J.4 - Open Responsive Player Hub Installer")]
    public static void Open()
    {
        var window = GetWindow<MS7J4ResponsiveInstaller>("7J.4 Responsive Hub");
        window.minSize = new Vector2(650, 470);
    }
    private void OnGUI()
    {
        EditorGUILayout.LabelField("7J.4  /  RESPONSIVE PLAYER HUB & MULTI-RESOLUTION", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("MainMenu 1.3x screenshot is an Editor Game View zoom issue. This installer adds real responsive layout to PlayerHub only; MainMenu Canvas is audited, not altered. No Save/Profile/Quest writes.", MessageType.Info);
        if (GUILayout.Button("1 - AUDIT MAINMENU / 7J.2.3 / PLAYERHUB (READ ONLY)", GUILayout.Height(34))) Run(false, false);
        if (GUILayout.Button("2 - BACKUP SCENES + INSTALL 7J.4 ADAPTIVE LAYOUT", GUILayout.Height(40))) Run(true, false);
        if (GUILayout.Button("3 - PURE ASPECT / GRID POLICY TESTS", GUILayout.Height(34))) PureTests();
        if (GUILayout.Button("4 - EXPORT 7J.4 QA TXT", GUILayout.Height(34))) Export();
        if (GUILayout.Button("5 - BACKUP + REMOVE ONLY 7J.4 (ROLLBACK)", GUILayout.Height(34))) Run(true, true);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Last report", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }
    private static string ProjectRoot { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); } }
    private static string AbsoluteScene(string path) { return Path.Combine(ProjectRoot, path.Replace('/', Path.DirectorySeparatorChar)); }
    private static bool ExistingScenes(out string error)
    {
        error = "";
        if (!File.Exists(AbsoluteScene(HubPath))) { error = "PlayerHub.unity not found: " + HubPath; return false; }
        if (!File.Exists(AbsoluteScene(MenuPath))) { error = "MainMenu.unity not found: " + MenuPath; return false; }
        return true;
    }
    private static Scene LoadOrFind(string path, out bool opened)
    {
        Scene existing = SceneManager.GetSceneByPath(path);
        opened = !existing.IsValid() || !existing.isLoaded;
        return opened ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive) : existing;
    }
    private static MS7J2PlayerHubPolish FindBaseline(Scene hub)
    {
        foreach (GameObject root in hub.GetRootGameObjects())
        {
            MS7J2PlayerHubPolish[] polishes = root.GetComponentsInChildren<MS7J2PlayerHubPolish>(true);
            if (polishes.Length > 0) return polishes[0];
        }
        return null;
    }
    private static string BackupScenes(out string error)
    {
        error = "";
        try
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string directory = Path.Combine(project, "MS_Backups", "7J4", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            Directory.CreateDirectory(directory);
            File.Copy(AbsoluteScene(MenuPath), Path.Combine(directory, "MainMenu_BEFORE_7J4.unity"));
            File.Copy(AbsoluteScene(HubPath), Path.Combine(directory, "PlayerHub_BEFORE_7J4.unity"));
            File.WriteAllText(Path.Combine(directory, "RESTORE.txt"),
                "7J.4 adds only MS7J4AdaptiveHubLayout to the PlayerHub Scene. Use installer button 5 to remove that component.\n" +
                "For manual recovery, close Unity, back up current Scenes, copy the matching scene files from this backup to Assets/MonsterSurvival/Content/Scenes.\n" +
                "No Save/Profile files are included or modified.\n", Encoding.UTF8);
            return directory;
        }
        catch (Exception ex) { error = ex.Message; return ""; }
    }
    private void Run(bool modifying, bool rollback)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { report = "[BLOCKED] Exit Play Mode before editing Scenes."; return; }
        string error;
        if (!ExistingScenes(out error)) { report = "[BLOCKED] " + error; return; }
        if (modifying && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        { report = "[CANCELLED] Scene changes not saved."; return; }
        Scene previous = SceneManager.GetActiveScene();
        Scene hub = default(Scene), main = default(Scene);
        bool openedHub = false, openedMain = false;
        try
        {
            main = LoadOrFind(MenuPath, out openedMain);
            hub = LoadOrFind(HubPath, out openedHub);
            MS7J2PlayerHubPolish baseline = FindBaseline(hub);
            if (!baseline) { report = "[BLOCKED] 7J.2 PlayerHubPolish component missing in PlayerHub.unity. Install 7J.2/7J.2.3 first."; return; }
            MS7J4AdaptiveHubLayout[] duplicates = baseline.GetComponents<MS7J4AdaptiveHubLayout>();
            if (duplicates.Length > 1) { report = "[BLOCKED] Duplicate 7J.4 components. Manually inspect PlayerHub scene."; return; }
            var text = new StringBuilder();
            text.AppendLine("MONSTERSURVIVAL / 7J.4 RESPONSIVE INSTALLER");
            text.AppendLine("Baseline=" + baseline.gameObject.name + " Enabled=" + baseline.enabled);
            text.AppendLine("Existing 7J4 components=" + duplicates.Length);
            foreach (GameObject root in main.GetRootGameObjects())
            foreach (CanvasScaler scaler in root.GetComponentsInChildren<CanvasScaler>(true))
            {
                if (!scaler.GetComponent<Canvas>()) continue;
                text.AppendLine("MAINMENU CANVAS " + scaler.name + " / " + scaler.uiScaleMode +
                    " / Ref=" + scaler.referenceResolution + " / Match=" + scaler.matchWidthOrHeight.ToString("F2"));
            }
            if (!modifying)
            {
                text.AppendLine("READ ONLY. MainMenu CanvasScaler is NOT altered. Game View preview zoom must be set to Fit.");
                report = text.ToString(); return;
            }
            string backup = BackupScenes(out error);
            if (string.IsNullOrEmpty(backup)) { report = "[BLOCKED] Backup failed: " + error; return; }
            if (rollback)
            {
                if (duplicates.Length == 1)
                {
                    Undo.DestroyObjectImmediate(duplicates[0]);
                    text.AppendLine("REMOVED 7J4 only; 7J2.3 remains unchanged.");
                }
                else text.AppendLine("Already absent; no change.");
            }
            else
            {
                if (duplicates.Length == 0)
                {
                    MS7J4AdaptiveHubLayout added = Undo.AddComponent<MS7J4AdaptiveHubLayout>(baseline.gameObject);
                    added.enabled = true;
                    EditorUtility.SetDirty(added);
                    text.AppendLine("INSTALLED one MS7J4AdaptiveHubLayout beside 7J2.3.");
                }
                else
                {
                    Undo.RecordObject(duplicates[0], "7J4 Enable responsive layout");
                    duplicates[0].enabled = true;
                    EditorUtility.SetDirty(duplicates[0]);
                    text.AppendLine("ALREADY INSTALLED; enabled existing instance, no duplicate.");
                }
            }
            EditorSceneManager.MarkSceneDirty(hub);
            if (!EditorSceneManager.SaveScene(hub)) throw new IOException("PlayerHub Scene save failed.");
            text.AppendLine("BACKUP=" + backup);
            text.AppendLine("NO MainMenu Scene modifications; no Save/Quest/Gold calls.");
            report = text.ToString();
            Debug.Log("[7J4] " + report);
        }
        catch (Exception ex) { report = "[FAILED] " + ex; Debug.LogError(report); }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (openedHub && hub.IsValid() && hub.isLoaded) EditorSceneManager.CloseScene(hub, true);
            if (openedMain && main.IsValid() && main.isLoaded) EditorSceneManager.CloseScene(main, true);
        }
    }
    private void PureTests()
    {
        int pass = 0, fail = 0;
        StringBuilder s = new StringBuilder("7J4 PURE LAYOUT POLICY (no Scene / Save write)\n");
        Action<bool, string> check = (ok, title) => { if (ok) pass++; else fail++; s.AppendLine((ok ? "PASS " : "FAIL ") + title); };
        check(MS7J4AdaptiveHubLayout.ChooseColumns(1920, 1080, 1.48f) == 2, "FHD 16:9 -> two columns");
        check(MS7J4AdaptiveHubLayout.ChooseColumns(2560, 1440, 1.48f) == 2, "QHD 16:9 -> two columns");
        check(MS7J4AdaptiveHubLayout.ChooseColumns(1280, 720, 1.48f) == 2, "HD 16:9 -> two columns");
        check(MS7J4AdaptiveHubLayout.ChooseColumns(1024, 768, 1.48f) == 1, "4:3 -> one scrollable column");
        check(MS7J4AdaptiveHubLayout.ChooseColumns(720, 1280, 1.48f) == 1, "Portrait -> one column");
        check(MS7J4AdaptiveHubLayout.ChooseColumns(3440, 1440, 1.48f) == 2, "Ultrawide -> two centered columns");
        check(Mathf.Approximately(MS7J4AdaptiveHubLayout.FitWidth(1750, 1580), 1580f), "Max content width capped");
        check(MS7J4AdaptiveHubLayout.AvailableCardWidth(1580f, 2, 18f, 4f) > 600f, "Wide card fits title");
        check(MS7J4AdaptiveHubLayout.AvailableCardWidth(700f, 1, 18f, 4f) > 600f, "Single card fits title");
        s.AppendLine("PURE=" + pass + " PASS / " + fail + " FAIL");
        report = s.ToString();
    }
    private void Export()
    {
        try
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string dir = Path.Combine(project, "MS_QAReports", "7J4");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "MS7J4_ResponsiveQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".txt");
            var s = new StringBuilder();
            s.AppendLine("MONSTERSURVIVAL / 7J.4 RESPONSIVE PLAYER HUB QA");
            s.AppendLine("Generated=" + DateTime.Now.ToString("o") + " Unity=" + Application.unityVersion + " Play=" + Application.isPlaying + " Scene=" + SceneManager.GetActiveScene().name);
            s.AppendLine("Game Resolution=" + Screen.width + "x" + Screen.height + " Aspect=" + ((float)Screen.width / Mathf.Max(1, Screen.height)).ToString("F3"));
            s.AppendLine("SafeArea=" + Screen.safeArea);
            Scene hub = SceneManager.GetSceneByName("PlayerHub");
            s.AppendLine("HubLoaded=" + (hub.IsValid() && hub.isLoaded));
            MS7J4AdaptiveHubLayout layout = null;
            MS7J2PlayerHubPolish hubUI = null;
            if (hub.IsValid() && hub.isLoaded)
                foreach (GameObject root in hub.GetRootGameObjects())
                {
                    if (!layout) layout = root.GetComponentInChildren<MS7J4AdaptiveHubLayout>(true);
                    if (!hubUI) hubUI = root.GetComponentInChildren<MS7J2PlayerHubPolish>(true);
                }
            s.AppendLine("ResponsiveComponent=" + (layout ? "FOUND" : "NOT LOADED") + " Enabled=" + (layout && layout.enabled));
            s.AppendLine("Dashboard=" + (hubUI ? hubUI.State : "NOT LOADED") + " Visible=" + (hubUI && hubUI.DashboardVisible));
            if (layout) s.AppendLine("LayoutState=" + layout.Status + " Columns=" + layout.Columns + " LogicalCanvas=" + layout.CanvasWidth + "x" + layout.CanvasHeight + " ViewportW=" + layout.ViewportWidth + " ContentH=" + layout.ContentHeight + " NeedsScroll=" + layout.VerticalScrollRequired);
            s.AppendLine("QA WINDOW REPORT:\n" + report);
            s.AppendLine("MANUAL PENDING: Game View zoom Fit/0.8/0.6, FHD/QHD/1280x720/1024x768, all 7 tabs X, Normal/Endless, Profile Gold Claim, Save isolation.");
            s.AppendLine("QA generates TXT only. No Save/Profile/Quest/Gold writes.");
            File.WriteAllText(path, s.ToString(), Encoding.UTF8);
            report = "[EXPORTED] " + path + "\n" + report;
            Debug.Log("[7J4] " + report);
        }
        catch (Exception ex) { report = "[EXPORT FAILED] " + ex.Message; }
    }
}
#endif
