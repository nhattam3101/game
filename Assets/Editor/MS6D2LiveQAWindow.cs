#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// MonsterSurvival 6D.2 - play-mode UI recorder. READ ONLY.
/// Does NOT modify GameObjects, components, buttons, Time.timeScale or gameplay.
/// Window must be open while testing. Can export observations to project-root UI_QA_Reports.
/// </summary>
public sealed class MS6D2LiveQAWindow : EditorWindow
{
    private const double PollSeconds = 0.25;
    private const int MaxEvents = 1200;
    private static readonly string[] ManualLabels = {
        "MainMenu V3: Play -> Map Select -> Start", 
        "Map Back, Settings, Music/SFX and Back", 
        "Gameplay start: no unexpected modal shown", 
        "ESC -> Settings -> Back -> Resume", 
        "Level-up: three skill choices and successful selection", 
        "Victory and Game Over screens in separate runs", 
        "Restart and return to Main Menu", 
        "Check 16:9, 16:10, 4:3 and 21:9 visually"
    };

    private readonly List<string> events = new List<string>();
    private readonly HashSet<string> screensObserved = new HashSet<string>();
    private readonly HashSet<string> sceneObserved = new HashSet<string>();
    private readonly bool[] manuallyPassed = new bool[8];
    private Vector2 eventScroll;
    private Vector2 guideScroll;
    private double nextSample;
    private double sessionStart;
    private string lastFingerprint = "";
    private string lastProblem = "";
    private string lastStatus = "Chua Play";
    private int samples;
    private int warningTransitions;
    private int warningSamples;
    private bool lastWasPlaying;
    private string lastExportPath = "";

    [MenuItem("Tools/MonsterSurvival/6D UI QA/6 - 6D.2 LIVE Play Mode QA (READ ONLY)")]
    public static void Open()
    {
        MS6D2LiveQAWindow w = GetWindow<MS6D2LiveQAWindow>("6D.2 Live UI QA");
        w.minSize = new Vector2(590, 480);
        w.Show();
    }

    private void OnEnable()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayChange;
        lastWasPlaying = EditorApplication.isPlaying;
        nextSample = EditorApplication.timeSinceStartup;
        if (sessionStart <= 0) sessionStart = nextSample;
    }

    private void OnDisable()
    {
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= OnPlayChange;
    }

    private void OnPlayChange(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            ResetSession();
            lastWasPlaying = true;
            AddEvent("PLAY MODE START");
        }
        else if (change == PlayModeStateChange.ExitingPlayMode)
        {
            if (events.Count > 0)
                AddEvent("PLAY MODE EXIT. Export report while or after Play Mode.");
            lastWasPlaying = false;
        }
        Repaint();
    }

    private void ResetSession()
    {
        events.Clear(); screensObserved.Clear(); sceneObserved.Clear();
        lastFingerprint = ""; lastProblem = ""; lastStatus = "Cho du lieu...";
        samples = 0; warningTransitions = 0; warningSamples = 0;
        sessionStart = EditorApplication.timeSinceStartup;
        nextSample = sessionStart;
    }

    private void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            if (lastWasPlaying)
            {
                lastWasPlaying = false;
                lastStatus = "Da thoat Play Mode — co the Export bao cao.";
                Repaint();
            }
            return;
        }
        lastWasPlaying = true;
        double now = EditorApplication.timeSinceStartup;
        if (now < nextSample) return;
        nextSample = now + PollSeconds;
        try { Sample(); }
        catch (Exception e) { AddEvent("QA TOOL EXCEPTION (not gameplay): " + e.GetType().Name + " " + e.Message); }
        Repaint();
    }

    private void Sample()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return;
        samples++;
        sceneObserved.Add(scene.name);
        screensObserved.Add(Screen.width + "x" + Screen.height);
        string sceneName = scene.name;
        StringBuilder problem = new StringBuilder(240);
        List<string> visible = new List<string>();
        List<string> details = new List<string>();
        // Scan RectTransforms only ONCE per sample to avoid repeated allocations.
        Dictionary<string, GameObject> named = IndexSceneRects(scene);

        if (sceneName.Equals("MainMenu", StringComparison.OrdinalIgnoreCase))
        {
            GameObject home = FindNamed(named, "PAGE_Home");
            GameObject maps = FindNamed(named, "PAGE_SelectMap");
            GameObject settings = FindNamed(named, "PAGE_Settings");
            if (Visible(home)) visible.Add("Home");
            if (Visible(maps)) visible.Add("MapSelect");
            if (Visible(settings)) visible.Add("MenuSettings");
            if (visible.Count > 1) problem.Append("Several Main Menu pages visible; ");
            if (visible.Count == 0) details.Add("No active V3 page; may be scene transition");
        }
        else if (sceneName.Equals("SampleScene", StringComparison.OrdinalIgnoreCase))
        {
            bool pause = Visible(FindNamed(named, "PausePanel"));
            bool skill = Visible(FindNamed(named, "SkillPanel"));
            bool victory = Visible(FindNamed(named, "VictoryPanel"));
            bool gameover = Visible(FindNamed(named, "GameOverPanel"));
            bool settings = Visible(FindNamed(named, "SettingsWindow", "SettingsPanel", "SettingsWindow(Clone)"));
            bool pauseWindow = Visible(FindNamed(named, "PauseWindow"));
            if (pause) visible.Add("Pause");
            if (skill) visible.Add("Skill");
            if (victory) visible.Add("Victory");
            if (gameover) visible.Add("GameOver");
            if (settings) visible.Add("Settings");
            if (victory && gameover) problem.Append("Victory + GameOver overlap; ");
            if (skill && (pause || victory || gameover || settings)) problem.Append("Skill overlaps another modal; ");
            if (pause && (victory || gameover)) problem.Append("Pause overlaps result; ");
            if (settings && (victory || gameover)) problem.Append("Settings overlaps result; ");
            if (pause && settings && pauseWindow) problem.Append("PauseWindow visible behind Settings; ");

            GameObject hud = FindNamed(named, "HUD6A2_Minimal");
            if (hud == null) details.Add("HUD6A2_Minimal MISSING");
            else
            {
                GameObject player = FindChild(hud, "PlayerStatus");
                GameObject status = FindChild(hud, "GameStatus_6A3");
                if (player != null && status != null && Visible(player) && Visible(status))
                {
                    Rect a, b;
                    if (TryScreenRect(player, out a) && TryScreenRect(status, out b))
                    {
                        Rect view = new Rect(0, 0, Screen.width, Screen.height);
                        if (!FullyInside(a, view) || !FullyInside(b, view))
                            problem.Append("HUD corner out of screen bounds; ");
                        if (a.Overlaps(b)) problem.Append("PlayerStatus / GameStatus_6A3 overlap; ");
                    }
                }
            }
            if (FindInScene<EventSystem>(scene) == null) problem.Append("EventSystem missing; ");
        }
        else details.Add("Other scene, read-only monitoring only");

        string screen = Screen.width + "x" + Screen.height;
        string modal = visible.Count > 0 ? string.Join(" + ", visible.ToArray()) : "none";
        string nowProblem = problem.ToString();
        string fingerprint = sceneName + "|" + screen + "|" + modal + "|" + Time.timeScale.ToString("F2") + "|" + nowProblem;
        lastStatus = sceneName + "   " + screen + "   TimeScale " + Time.timeScale.ToString("F2") + "   UI: " + modal;
        if (fingerprint != lastFingerprint)
        {
            string line = lastStatus + (details.Count > 0 ? "   INFO: " + string.Join(", ", details.ToArray()) : "");
            AddEvent(line);
            lastFingerprint = fingerprint;
        }
        if (nowProblem.Length > 0)
        {
            warningSamples++;
            if (nowProblem != lastProblem)
            {
                warningTransitions++;
                AddEvent("[WARN] " + nowProblem + " Scene=" + sceneName + " Screen=" + screen);
            }
        }
        else if (lastProblem.Length > 0) AddEvent("[RESOLVED] Previous runtime UI warning no longer detected");
        lastProblem = nowProblem;
    }

    private static bool FullyInside(Rect r, Rect view)
    {
        const float margin = 3f;
        return r.xMin >= view.xMin - margin && r.yMin >= view.yMin - margin &&
               r.xMax <= view.xMax + margin && r.yMax <= view.yMax + margin;
    }

    private static bool TryScreenRect(GameObject go, out Rect rect)
    {
        rect = new Rect();
        RectTransform rt = go != null ? go.GetComponent<RectTransform>() : null;
        if (!rt) return false;
        Canvas canvas = rt.GetComponentInParent<Canvas>();
        if (!canvas) return false;
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null :
                     (canvas.worldCamera != null ? canvas.worldCamera : Camera.main);
        Vector3[] corners = new Vector3[4]; rt.GetWorldCorners(corners);
        Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
        rect = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                               Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        return rect.width > 0.5f && rect.height > 0.5f;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component) return component;
        }
        return null;
    }

    private static Dictionary<string, GameObject> IndexSceneRects(Scene scene)
    {
        var result = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        var all = UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (RectTransform rt in all)
        {
            if (!rt || rt.gameObject.scene != scene) continue;
            GameObject previous;
            // Prefer active dynamic panels over inactive templates with the same name.
            if (!result.TryGetValue(rt.name, out previous) ||
                (!previous.activeInHierarchy && rt.gameObject.activeInHierarchy))
                result[rt.name] = rt.gameObject;
        }
        return result;
    }

    private static GameObject FindNamed(Dictionary<string, GameObject> objects, params string[] names)
    {
        GameObject inactive = null;
        foreach (string name in names)
        {
            GameObject go;
            if (!objects.TryGetValue(name, out go) || !go) continue;
            if (go.activeInHierarchy) return go;
            if (!inactive) inactive = go;
        }
        return inactive;
    }

    private static GameObject FindChild(GameObject root, string name)
    {
        if (!root) return null;
        foreach (RectTransform rt in root.GetComponentsInChildren<RectTransform>(true))
            if (string.Equals(rt.name, name, StringComparison.OrdinalIgnoreCase)) return rt.gameObject;
        return null;
    }

    private static bool Visible(GameObject go)
    {
        if (!go || !go.activeInHierarchy) return false;
        for (Transform t = go.transform; t != null; t = t.parent)
        {
            CanvasGroup group = t.GetComponent<CanvasGroup>();
            if (group && group.alpha <= 0.05f) return false;
        }
        return true;
    }

    private void AddEvent(string s)
    {
        if (events.Count >= MaxEvents) events.RemoveAt(0);
        double elapsed = Math.Max(0d, EditorApplication.timeSinceStartup - sessionStart);
        events.Add("[" + elapsed.ToString("F1") + "s] " + s);
    }

    private void Export()
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../UI_QA_Reports"));
        try
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "MonsterSurvival_6D2_Runtime_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            StringBuilder b = new StringBuilder(8000);
            b.AppendLine("MONSTERSURVIVAL 6D.2 - LIVE PLAY MODE UI QA");
            b.AppendLine("Report created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            b.AppendLine("Read only observations; do not interpret as automatic gameplay test.");
            b.AppendLine("Samples: " + samples + " / warning transitions: " + warningTransitions + " / warning samples: " + warningSamples);
            b.AppendLine("Scene(s): " + string.Join(", ", new List<string>(sceneObserved).ToArray()));
            b.AppendLine("Resolution(s): " + string.Join(", ", new List<string>(screensObserved).ToArray()));
            b.AppendLine("UNITY: " + Application.unityVersion);
            b.AppendLine("-------------- MANUAL TEST CONFIRMATIONS --------------");
            for (int i = 0; i < ManualLabels.Length; i++)
                b.AppendLine("[" + (manuallyPassed[i] ? "CONFIRMED BY USER" : "NOT CONFIRMED") + "] " + ManualLabels[i]);
            b.AppendLine("-------------- EVENT TIMELINE --------------");
            foreach (string e in events) b.AppendLine(e);
            b.AppendLine("-------------- NOTES --------------");
            b.AppendLine("Unobserved tests are not PASS. Exporting is not evidence that every button actually worked.");
            b.AppendLine("For dynamic UI issues, attach console errors or a screenshot alongside this report.");
            File.WriteAllText(path, b.ToString(), new UTF8Encoding(true));
            lastExportPath = path;
            Debug.Log("[6D.2 UI QA] Report saved to: " + path);
            EditorUtility.RevealInFinder(path);
        }
        catch (Exception e)
        {
            Debug.LogError("[6D.2 UI QA] Cannot write report: " + e.Message);
            EditorUtility.DisplayDialog("6D.2 export failed", e.Message, "OK");
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("MONSTERSURVIVAL 6D.2  ·  LIVE UI QUALITY ASSURANCE", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("READ ONLY. Mo cua so nay truoc khi bam Play. Tu thao tac game; tool chi quan sat. Khong tu nhan PASS neu chua test.", MessageType.Info);
        EditorGUILayout.LabelField("Unity", Application.unityVersion);
        EditorGUILayout.LabelField("Status", EditorApplication.isPlaying ? "DANG THEO DOI PLAY MODE" : "STOP - hay bam Play trong Unity");
        EditorGUILayout.LabelField("Latest", lastStatus, EditorStyles.wordWrappedLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Samples: " + samples, GUILayout.Width(120));
            EditorGUILayout.LabelField("WARN changes: " + warningTransitions, GUILayout.Width(155));
            EditorGUILayout.LabelField("WARN samples: " + warningSamples);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("EXPORT TXT REPORT", GUILayout.Height(29))) Export();
            if (GUILayout.Button("RESET SESSION LOG", GUILayout.Height(29)))
            {
                if (EditorUtility.DisplayDialog("Reset 6D.2 log?", "Clear observations and checkboxes for this session? Gameplay is not changed.", "Reset", "Cancel"))
                {
                    ResetSession();
                    for (int i = 0; i < manuallyPassed.Length; ++i) manuallyPassed[i] = false;
                }
            }
        }
        if (!string.IsNullOrEmpty(lastExportPath))
            EditorGUILayout.SelectableLabel("Saved: " + lastExportPath, EditorStyles.wordWrappedLabel, GUILayout.Height(35));
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("MANUAL TESTS — tick ONLY after real Play Mode confirmation", EditorStyles.boldLabel);
        guideScroll = EditorGUILayout.BeginScrollView(guideScroll, GUILayout.Height(186));
        for (int i = 0; i < manuallyPassed.Length; i++)
            manuallyPassed[i] = EditorGUILayout.ToggleLeft(ManualLabels[i], manuallyPassed[i]);
        EditorGUILayout.EndScrollView();
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("RUNTIME EVENT TIMELINE", EditorStyles.boldLabel);
        eventScroll = EditorGUILayout.BeginScrollView(eventScroll);
        if (events.Count == 0) EditorGUILayout.LabelField("Chua co mau do. Mo MainMenu -> Play, sau do thu UI.");
        else
        {
            for (int i = events.Count - 1; i >= 0; i--)
                EditorGUILayout.LabelField(events[i], EditorStyles.wordWrappedLabel);
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.HelpBox("Khong tu fix modal overlap / khong sua Scene. Neu co WARN, hay Export TXT roi gui de sua dung luong UI bi loi.", MessageType.None);
    }
}
#endif
