#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;

// 7J.2.2 - Safe repair for PlayerHub missing from Unity 6 Build Profiles.
// Editor-only: never modifies scenes, PlayerPrefs, ScriptableObject game saves, or rewards.
public sealed class MS7J22BuildProfileRepair : EditorWindow
{
    private const string MenuScene = "Assets/Scenes/MainMenu.unity";
    private const string GameScene = "Assets/Scenes/SampleScene.unity";
    private const string HubScene = "Assets/Scenes/PlayerHub.unity";
    private static readonly string[] RequiredScenes = { MenuScene, GameScene, HubScene };

    private string report = "7J.2.2: AUDIT, then BACKUP + REPAIR in EDIT Mode.";
    private Vector2 scroll;

    [MenuItem("Tools/MonsterSurvival/UI/7J.2.2 - Fix PlayerHub Build Profiles")]
    private static void Open()
    {
        var w = GetWindow<MS7J22BuildProfileRepair>("7J.2.2 Hub Scene Fix");
        w.minSize = new Vector2(660, 390);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("7J.2.2 / PLAYER HUB BUILD PROFILES REPAIR", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Fixes [7J1] BLOCKED: Add PlayerHub.unity to Build Profiles. Adds/enables required scenes in the active and global scene lists. Does not edit Scene assets, scripts or SaveData.", MessageType.Info);
        if (GUILayout.Button("1 - AUDIT ACTIVE + GLOBAL SCENE LISTS (READ ONLY)", GUILayout.Height(34))) Audit();
        if (GUILayout.Button("2 - BACKUP + REPAIR ACTIVE / GLOBAL SCENE LISTS", GUILayout.Height(39))) Repair();
        if (GUILayout.Button("3 - EXPORT 7J.2.2 QA TXT", GUILayout.Height(34))) Export();
        EditorGUILayout.HelpBox("After repair, EXIT and RE-ENTER Play Mode from MainMenu, then click PLAYER HUB. If an independent build profile overrides its own Scene List later, audit/repair that profile too.", MessageType.None);
        EditorGUILayout.LabelField("Last report", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private static bool Same(string a, string b)
    {
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static bool AllAssetsExist(out string reason)
    {
        var missing = RequiredScenes.Where(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null).ToArray();
        reason = missing.Length == 0 ? "All 3 Scene assets found." : "MISSING Scene assets: " + string.Join(", ", missing);
        return missing.Length == 0;
    }

    private static EditorBuildSettingsScene[] EnsureEnabled(EditorBuildSettingsScene[] input)
    {
        var list = new List<EditorBuildSettingsScene>();
        if (input != null)
        {
            // Preserve the original order and all unrelated scenes.
            foreach (var item in input)
            {
                if (item == null || string.IsNullOrEmpty(item.path)) continue;
                bool required = RequiredScenes.Any(p => Same(p, item.path));
                if (required)
                {
                    // Avoid duplicate references to the same required scene.
                    if (list.Any(s => Same(s.path, item.path))) continue;
                    string canonical = RequiredScenes.First(p => Same(p, item.path));
                    list.Add(new EditorBuildSettingsScene(canonical, true));
                }
                else list.Add(new EditorBuildSettingsScene(item.path, item.enabled));
            }
        }
        // MainMenu stays the entry scene in an existing list; never reorder existing ones.
        foreach (string path in RequiredScenes)
        {
            if (list.Any(s => Same(s.path, path))) continue;
            if (Same(path, MenuScene)) list.Insert(0, new EditorBuildSettingsScene(path, true));
            else list.Add(new EditorBuildSettingsScene(path, true));
        }
        return list.ToArray();
    }

    private static bool HasEnabled(EditorBuildSettingsScene[] scenes, string path)
    {
        return scenes != null && scenes.Any(s => s != null && s.enabled && Same(s.path, path));
    }

    private static string Summary(string label, EditorBuildSettingsScene[] scenes)
    {
        var sb = new StringBuilder();
        sb.AppendLine(label + " (total=" + (scenes == null ? 0 : scenes.Length) + ")");
        foreach (string path in RequiredScenes)
            sb.AppendLine((HasEnabled(scenes, path) ? "[PASS] " : "[MISSING/DISABLED] ") + path);
        return sb.ToString();
    }

    private static string ActiveProfileName()
    {
        BuildProfile profile = BuildProfile.GetActiveBuildProfile();
        return profile == null ? "Platform profile / global scenes" :
            "BuildProfile=" + profile.name + " overrideGlobalScenes=" + profile.overrideGlobalScenes;
    }

    private static string CurrentAudit()
    {
        var active = EditorBuildSettings.scenes;
        var global = EditorBuildSettings.globalScenes;
        string assets;
        bool exists = AllAssetsExist(out assets);
        bool ready = exists && RequiredScenes.All(s => HasEnabled(active, s));
        return "MONSTERSURVIVAL 7J.2.2 - READ-ONLY AUDIT\n" +
            "Unity=" + Application.unityVersion + " Playing=" + EditorApplication.isPlaying + "\n" +
            "ActiveProfile: " + ActiveProfileName() + "\n" +
            "Scene assets: " + assets + "\n" +
            Summary("ACTIVE SCENE LIST", active) + Summary("GLOBAL SCENE LIST", global) +
            "READY (active list only)=" + ready + "\n" +
            "Application.CanStreamedLevelBeLoaded(PlayerHub)=" + Application.CanStreamedLevelBeLoaded("PlayerHub") + "\n" +
            "QA READ ONLY: No changes to scenes, Quest, Save/Profile/Gold/Records.\n";
    }

    private void Audit()
    {
        try { report = CurrentAudit(); Debug.Log(report); }
        catch (Exception e) { report = "[AUDIT ERROR] " + e; Debug.LogError(report); }
    }

    private static string ProjectRoot()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }

    private static string DescribeScenes(EditorBuildSettingsScene[] scenes)
    {
        return string.Join("\n", (scenes ?? new EditorBuildSettingsScene[0])
            .Where(s => s != null)
            .Select(s => (s.enabled ? "ON  " : "OFF ") + s.path).ToArray());
    }

    private void Repair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { report = "[BLOCKED] Exit Play Mode before modifying Build Profiles."; return; }
        string reason;
        if (!AllAssetsExist(out reason))
        { report = "[BLOCKED] " + reason + "\nRun 7J.1 Installer only if PlayerHub.unity truly does not exist."; return; }
        if (!EditorUtility.DisplayDialog("7J.2.2 - Repair Scene List",
            "Add/enable MainMenu, SampleScene and PlayerHub in the ACTIVE and GLOBAL Scene Lists?\n\nA text backup of both lists is created first. No Scene asset or SaveData is modified.",
            "Backup + Repair", "Cancel")) return;

        try
        {
            EditorBuildSettingsScene[] activeBefore = EditorBuildSettings.scenes;
            EditorBuildSettingsScene[] globalBefore = EditorBuildSettings.globalScenes;
            string backup = Path.Combine(ProjectRoot(), "MS_Backups", "7J22", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            Directory.CreateDirectory(backup);
            File.WriteAllText(Path.Combine(backup, "ActiveScenes_BEFORE.txt"),
                ActiveProfileName() + "\n" + DescribeScenes(activeBefore) + "\n", Encoding.UTF8);
            File.WriteAllText(Path.Combine(backup, "GlobalScenes_BEFORE.txt"),
                DescribeScenes(globalBefore) + "\n", Encoding.UTF8);

            // Note: Unity 6.6 EditorBuildSettings.scenes is the *active profile's* list,
            // while globalScenes is the shared list. Repair both without disabling other scenes.
            EditorBuildSettings.globalScenes = EnsureEnabled(globalBefore);
            EditorBuildSettings.scenes = EnsureEnabled(activeBefore);
            AssetDatabase.SaveAssets();

            bool activeReady = RequiredScenes.All(s => HasEnabled(EditorBuildSettings.scenes, s));
            bool globalReady = RequiredScenes.All(s => HasEnabled(EditorBuildSettings.globalScenes, s));
            report = "[REPAIR " + (activeReady && globalReady ? "PASS" : "CHECK REQUIRED") + "]\n" +
                "Backup: " + backup + "\n" + CurrentAudit() +
                "NEXT: Stop Play (if needed), run MainMenu; click PLAYER HUB; verify dashboard and close X.\n" +
                "NOTE: Other custom Build Profiles that override scenes are not modified.";
            if (activeReady && globalReady) Debug.Log(report); else Debug.LogWarning(report);
        }
        catch (Exception e)
        {
            report = "[REPAIR FAILED] " + e + "\nPrevious Scene Lists are in MS_Backups/7J22 if backup completed.";
            Debug.LogError(report);
        }
    }

    private void Export()
    {
        try
        {
            string folder = Path.Combine(ProjectRoot(), "MS_QAReports", "7J22");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "MS7J22_BuildProfileQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".txt");
            File.WriteAllText(path, CurrentAudit() + "\nLAST ACTION:\n" + report +
                "\nMANUAL PENDING: Play MainMenu, click PLAYER HUB, visit a tab, press X, return Hub; normal Save/Quest unaffected.\n", Encoding.UTF8);
            report = "[EXPORTED] " + path + "\n" + report;
            Debug.Log(report);
        }
        catch (Exception e) { report = "[EXPORT FAILED] " + e.Message; Debug.LogError(report); }
    }
}
#endif
