#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 7H.4 -- installer only touches a NEW root in MainMenu, after verified Scene backup.
// Existing Quest UI, Boss, Save, Prefab, SampleScene and MainMenu V3 remain unchanged.
public sealed class MS7H4QuestNotificationInstaller : EditorWindow
{
    private const string RootName = "MS7H4_QuestNotifications [MainMenu]";
    private Vector2 scroll;
    private string report = "7H.4 Quest Notifications. Begin with AUDIT.";
    private bool actionPending;

    [MenuItem("Tools/MonsterSurvival/7H Quests/4 - Open 7H.4 Notifications")]
    private static void Open() { GetWindow<MS7H4QuestNotificationInstaller>("7H.4 Quest Toasts"); }

    private void Queue(Action action)
    {
        if (actionPending || action == null) return;
        actionPending = true;
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            try { action(); }
            catch (Exception ex) { Report("[ERROR] " + ex); Debug.LogException(ex); }
            finally { actionPending = false; if (this != null) Repaint(); }
        };
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        try
        {
            EditorGUILayout.LabelField("7H.4 - QUEST NOTIFICATIONS / POLISH", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Additive read-only toast notifications. Installs one new root in MainMenu. " +
                "Never writes Save/Profile, Gold, quest markers, 7H.3 UI, or gameplay. " +
                "Stop Play Mode before Install/Remove.", MessageType.Info);
            if (GUILayout.Button("1 - AUDIT REAL MAINMENU (READ ONLY)", GUILayout.Height(34)))
                Queue(() => Report(Audit()));
            if (GUILayout.Button("2 - PURE NOTIFICATION TESTS (NO DISK WRITES)", GUILayout.Height(34)))
                Queue(() => Report(MS7H4QuestToastController.PureTests()));
            if (GUILayout.Button("3 - BACKUP + INSTALL 7H.4 ON MAINMENU", GUILayout.Height(38)))
                Queue(Install);
            if (GUILayout.Button("4 - PLAYMODE PREVIEW TOAST (NO REWARD)", GUILayout.Height(34)))
                Queue(Preview);
            if (GUILayout.Button("5 - EXPORT 7H.4 QA TXT", GUILayout.Height(34)))
                Queue(Export);
            if (GUILayout.Button("6 - BACKUP + REMOVE 7H.4 COMPONENT", GUILayout.Height(34)))
                Queue(Remove);
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Report", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(report, GUILayout.MinHeight(295), GUILayout.ExpandHeight(true));
        }
        finally { EditorGUILayout.EndScrollView(); }
    }

    private void Report(string value) { report = value; Debug.Log("[7H.4] " + value); }
    private static string ProjectRoot { get { return Directory.GetParent(Application.dataPath).FullName; } }

    private static string FindMainMenu()
    {
        var matches = new System.Collections.Generic.List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Scene MainMenu"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (string.Equals(Path.GetFileNameWithoutExtension(p), "MainMenu", StringComparison.OrdinalIgnoreCase) &&
                !p.Contains("/Backups/") && !p.Contains("/MS_Backups/")) matches.Add(p);
        }
        if (matches.Count == 0) return null;
        matches.Sort(StringComparer.Ordinal);
        Scene current = SceneManager.GetActiveScene();
        if (current.IsValid() && current.name == "MainMenu" && matches.Contains(current.path))
            return current.path;
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            if (s.enabled && matches.Contains(s.path)) return s.path;
        return matches[0];
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T result = root.GetComponentInChildren<T>(true);
            if (result != null) return result;
        }
        return null;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }

    private static string SafeBackup(string scenePath, string suffix)
    {
        string source = Path.Combine(ProjectRoot, scenePath);
        if (!File.Exists(source)) throw new FileNotFoundException("Missing MainMenu Scene on disk", source);
        string dir = Path.Combine(ProjectRoot, "MS_Backups", "7H4");
        Directory.CreateDirectory(dir);
        string target = Path.Combine(dir,
            "MainMenu_" + suffix + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity");
        File.Copy(source, target, false);
        if (!File.Exists(target) || new FileInfo(source).Length != new FileInfo(target).Length)
            throw new IOException("Backup failed integrity check: " + target);
        return target;
    }

    private static string Audit()
    {
        StringBuilder b = new StringBuilder("MONSTERSURVIVAL 7H.4 / READ-ONLY AUDIT\n");
        b.AppendLine("Unity=" + Application.unityVersion + " Scene=" + SceneManager.GetActiveScene().name +
            " Play=" + Application.isPlaying);
        string path = FindMainMenu();
        b.AppendLine("MainMenu=" + (path ?? "MISSING"));
        if (path == null) return b.AppendLine("[FAIL] MainMenu Scene asset missing").ToString();
        if (Application.isPlaying)
        {
            var live = MS7H4QuestToastController.Instance;
            b.AppendLine("[INFO] PlayMode Scene audit skipped to avoid modifying loaded scenes.");
            b.AppendLine(live == null ? "[PENDING] 7H.4 persistent notifier not running" :
                "[LIVE] BaselineProfiles=" + live.BaselineProfiles + " CompletionEvents=" + live.CompletionEvents +
                " ClaimEvents=" + live.ClaimEvents + " PreviewCount=" + live.PreviewCount +
                " Queued=" + live.QueuedCount + " LastEvent=" + live.LastEvent);
            return b.ToString();
        }
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            b.AppendLine((FindInScene<MS7H1QuestBoard>(scene) != null ? "[PASS] " : "[FAIL] ") + "7H.1 Quest Board");
            b.AppendLine((FindInScene<MS7H2ProgressBoard>(scene) != null ? "[PASS] " : "[FAIL] ") + "7H.2 Progress Board");
            b.AppendLine((FindInScene<MS7H3PeriodicQuestPanel>(scene) != null ? "[PASS] " : "[FAIL] ") + "7H.3 Daily / Weekly panel");
            GameObject root = FindRoot(scene, RootName);
            b.AppendLine(root == null ? "[NOT INSTALLED] 7H.4 root" :
                (root.GetComponent<MS7H4QuestToastController>() != null ? "[PASS] " : "[FAIL] ") + RootName);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        b.AppendLine("[NOTE] UI visibility, real victories, Save durability and Claim still require Play Mode manual QA.");
        b.AppendLine("[NOTE] Existing 7H.3.2 blank-text issue is not certified by this package.");
        return b.ToString();
    }

    private void Install()
    {
        if (Application.isPlaying) { Report("[BLOCKED] Stop Play Mode before installing."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string path = FindMainMenu();
        if (path == null) { Report("[BLOCKED] MainMenu Scene not found."); return; }
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        bool ready;
        try
        {
            ready = FindInScene<MS7H1QuestBoard>(scene) != null &&
                FindInScene<MS7H2ProgressBoard>(scene) != null &&
                FindInScene<MS7H3PeriodicQuestPanel>(scene) != null;
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        if (!ready) { Report("[BLOCKED] MainMenu needs 7H.1, 7H.2 and 7H.3 components. No Scene modified.\n" + Audit()); return; }

        string backup = SafeBackup(path, "BEFORE_7H4_INSTALL");
        string original = SceneManager.GetActiveScene().path;
        scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        GameObject root = FindRoot(scene, RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "7H4 notifier root");
            if (root.scene != scene) SceneManager.MoveGameObjectToScene(root, scene);
        }
        if (root.GetComponent<MS7H4QuestToastController>() == null)
            Undo.AddComponent<MS7H4QuestToastController>(root);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unable to save MainMenu");
        if (!string.IsNullOrEmpty(original) && original != path && File.Exists(Path.Combine(ProjectRoot, original)))
            EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
        Report("[INSTALLED] 7H.4 root added / repaired; only MainMenu modified.\n[BACKUP] " + backup + "\n" + Audit());
    }

    private void Remove()
    {
        if (Application.isPlaying) { Report("[BLOCKED] Stop Play Mode before removing."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string path = FindMainMenu();
        if (path == null) { Report("[BLOCKED] MainMenu missing"); return; }
        string backup = SafeBackup(path, "BEFORE_7H4_REMOVE");
        string original = SceneManager.GetActiveScene().path;
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        GameObject root = FindRoot(scene, RootName);
        if (root != null)
        {
            Undo.DestroyObjectImmediate(root);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unable to save MainMenu");
        }
        if (!string.IsNullOrEmpty(original) && original != path && File.Exists(Path.Combine(ProjectRoot, original)))
            EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
        Report("[REMOVED] Only 7H4 notifier GameObject. Quest Save & existing UI preserved.\n[BACKUP] " + backup);
    }

    private void Preview()
    {
        MS7H4QuestToastController live = MS7H4QuestToastController.Instance;
        if (!Application.isPlaying || SceneManager.GetActiveScene().name != "MainMenu" || live == null)
        {
            Report("[PENDING] Enter Play Mode in MainMenu with 7H4 installed before preview.");
            return;
        }
        live.PreviewOnly();
        Report("[PREVIEW] Visual toast queued. Does NOT complete a quest, give Gold or write Save.\n" + Audit());
    }

    private void Export()
    {
        string dir = Path.Combine(ProjectRoot, "MS_QAReports", "7H4");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "MS7H4_QuestNotificationsQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
        string data = Audit() + "\n" + MS7H4QuestToastController.PureTests() + "\n" +
            "=== MANUAL QA: NOT AUTOMATICALLY VERIFIED ===\n" +
            "[ ] Preview toast appears and does not block button clicks\n" +
            "[ ] Existing QUESTS/PROGRESS/DAILY-WEEKLY UI still works\n" +
            "[ ] Victory -> MainMenu shows only new eligible quest-completion toast\n" +
            "[ ] Claim new quest shows exactly one reward toast; no double reward\n" +
            "[ ] Switching Profiles never shows quests from another Profile\n" +
            "[ ] Existing completed quests at startup cause no notification flood\n" +
            "[ ] Resetting a daily/weekly period does not cause a false completion toast\n" +
            "[ ] Save invalid/backup-only/clock rollback shows no toast, writes no Save\n" +
            "[ ] 1920x1080 / 1366x768 / 1024x768 toast stays legible\n" +
            "[ ] Quest VFX does not obstruct magic circle, Profile, main navigation or Claim\n" +
            "[ ] Disable / Remove 7H4 leaves all quest functions and Save untouched\n" +
            "[ ] 7H.3.2 missing-text issue independently verified or still tracked as PENDING\n" +
            "RESULT=PENDING UNTIL UNITY COMPILE + PLAY MODE + MANUAL QA\n";
        File.WriteAllText(path, data, new UTF8Encoding(false));
        Report("[EXPORTED] " + path + "\n" + Audit());
    }
}
#endif
