#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 7J.2.3: READ ONLY runtime QA. Never invokes a module, Claim, Save or Restore.
public sealed class MS7J23HubTabsQAWindow : EditorWindow
{
    private string report = "Enter Play Mode from MainMenu, open PLAYER HUB, click tab 03/04/07, then AUDIT.";
    private Vector2 scroll;

    [MenuItem("Tools/MonsterSurvival/UI/7J.2.3 - Open Hub Tabs Fix QA")]
    public static void Open()
    {
        var window = GetWindow<MS7J23HubTabsQAWindow>("7J.2.3 Hub Tabs QA");
        window.minSize = new Vector2(760, 460);
    }
    private void OnGUI()
    {
        EditorGUILayout.LabelField("7J.2.3 / ACHIEVEMENTS - PROFILE - ENDLESS RECORDS", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Read-only module navigation audit. Use Play Mode. It will not touch 5A Save, Quest, Claims, Scene files or Prefabs.", MessageType.Info);
        if (GUILayout.Button("1 - AUDIT CURRENT PLAYER HUB (READ ONLY)", GUILayout.Height(35))) report = BuildReport();
        if (GUILayout.Button("2 - EXPORT 7J.2.3 QA TXT", GUILayout.Height(35))) Export();
        EditorGUILayout.Space();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }
    private static T FindInScene<T>(string sceneName) where T : Component
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T item = root.GetComponentInChildren<T>(true);
            if (item) return item;
        }
        return null;
    }
    private static GameObject FindNamed(string sceneName, string objectName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
            if (tr.name == objectName) return tr.gameObject;
        return null;
    }
    private static string Info(GameObject go)
    {
        if (!go) return "MISSING";
        CanvasGroup group = go.GetComponent<CanvasGroup>();
        int graphics = go.GetComponentsInChildren<Graphic>(true).Count(g => g && g.isActiveAndEnabled && g.gameObject.activeInHierarchy);
        return "exists=True self=" + go.activeSelf + " hierarchy=" + go.activeInHierarchy +
            " graphicsActive=" + graphics + (group ? " alpha=" + group.alpha.ToString("0.00") : "");
    }
    private static string BuildReport()
    {
        StringBuilder b = new StringBuilder();
        b.AppendLine("MONSTERSURVIVAL / 7J.2.3 PLAYER HUB TAB OPEN / HOVER QA");
        b.AppendLine("Time=" + DateTime.Now.ToString("o") + " Unity=" + Application.unityVersion +
                     " Play=" + Application.isPlaying + " ActiveScene=" + SceneManager.GetActiveScene().name);
        var hub = FindInScene<MS7J2PlayerHubPolish>("PlayerHub");
        var entry = FindInScene<MS7J1HubEntry>("MainMenu");
        b.AppendLine("Hub=" + (hub ? hub.State : "NOT LOADED") +
                     " Dashboard=" + (hub ? hub.DashboardVisible.ToString() : "N/A") +
                     " ActiveTab=" + (hub ? (hub.ActiveModuleIndex + 1).ToString("00") : "N/A"));
        if (hub)
        {
            b.AppendLine("LastLaunch=" + hub.LastLaunchDiagnostic);
            b.AppendLine("TrackedPopup=" + hub.TrackedPopupName);
        }
        b.AppendLine("MainMenu Entry=" + (entry ? entry.LastStatus : "MISSING"));
        b.AppendLine("SelectedProfile=" + MS5A2GoldBank.SelectedSlot);
        string[] buttons = { "MS5C4_BTN_Achievements", "MS5D1_BTN_ProfileOverview", "BTN_7I4Records" };
        string[] services = { "MS5C4_AchievementGallery", "MS5D1_ProfileOverview", "7I4_Records_Modal" };
        for (int i = 0; i < buttons.Length; i++)
        {
            int tab = i == 0 ? 3 : i == 1 ? 4 : 7;
            b.AppendLine("TAB " + tab.ToString("00") + " launcher: " + Info(FindNamed("MainMenu", buttons[i])));
            b.AppendLine("TAB " + tab.ToString("00") + " service/modal: " + Info(FindNamed("MainMenu", services[i])));
        }
        MS7I4RecordsMenu records = FindInScene<MS7I4RecordsMenu>("MainMenu");
        b.AppendLine("7I4 RecordsMenu=" + (records ? "EXISTS enabled=" + records.enabled +
            " Built=" + records.Built + " Status=" + records.Status : "MISSING"));
        GameObject selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        b.AppendLine("Unity Selected UI=" + (selected ? selected.name : "NONE"));
        b.AppendLine("NO WRITES: Only visual inspection and optional QA TXT export.");
        b.AppendLine("MANUAL: 03/04/07 should show original panel; click X returns Dashboard; none remains pink; test 01/02/05/06 again.");
        return b.ToString();
    }
    private void Export()
    {
        try
        {
            report = BuildReport();
            string folder = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "MS_QAReports", "7J23");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "MS7J23_HubTabsQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".txt");
            File.WriteAllText(path, report, Encoding.UTF8);
            report = "[EXPORTED] " + path + "\n" + report;
            Debug.Log("[7J.2.3] " + path);
        }
        catch (Exception e) { report = "[EXPORT FAILED] " + e; Debug.LogError(report); }
    }
}
#endif
