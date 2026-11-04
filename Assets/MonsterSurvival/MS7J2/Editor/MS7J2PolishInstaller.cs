#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Additive, reversible visual upgrade: 7J.1 scripts / data are kept untouched.
public sealed class MS7J2PolishInstaller : EditorWindow
{
    private const string HubPath = "Assets/Scenes/PlayerHub.unity";
    private string report = "7J.2 — Audit first, then install. Edit Mode only.";
    private Vector2 scroll;

    [MenuItem("Tools/MonsterSurvival/UI/7J.2 - Open Professional Player Hub Polish")]
    private static void Open()
    {
        var w = GetWindow<MS7J2PolishInstaller>("7J.2 Player Hub Polish");
        w.minSize = new Vector2(710, 490);
    }
    private void OnGUI()
    {
        EditorGUILayout.LabelField("7J.2  /  PLAYER HUB VISUAL POLISH", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Native TMP using MainMenu font; 7 responsive cards; original Save/Quest/Endless buttons untouched. Backup both Scenes before install.", MessageType.Info);
        if (GUILayout.Button("1 - AUDIT EXISTING 7J.1 SCENES (READ ONLY)", GUILayout.Height(32))) Audit();
        if (GUILayout.Button("2 - BACKUP + INSTALL NEW VISUAL SKIN (OFFLINE SCENE)", GUILayout.Height(38))) Install();
        if (GUILayout.Button("3 - EXPORT 7J.2 QA TXT", GUILayout.Height(32))) Export();
        if (GUILayout.Button("4 - BACKUP + RESTORE ORIGINAL 7J.1 LOOK", GUILayout.Height(33))) Rollback();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Last report", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }
    private static bool Paths(out string menuPath, out string reason)
    {
        menuPath = ""; reason = "";
        string[] menuScenes = AssetDatabase.FindAssets("t:Scene").Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith("/MainMenu.unity", StringComparison.OrdinalIgnoreCase)).Distinct().ToArray();
        if (menuScenes.Length != 1) { reason = "Require exactly ONE MainMenu.unity, found " + menuScenes.Length; return false; }
        menuPath = menuScenes[0];
        if (!File.Exists(Path.GetFullPath(HubPath))) { reason = "PlayerHub.unity missing. Install 7J.1 first."; return false; }
        return true;
    }
    private static Scene Load(string path, out bool opened)
    {
        Scene existing = SceneManager.GetSceneByPath(path);
        opened = !existing.IsValid() || !existing.isLoaded;
        return opened ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive) : existing;
    }
    private static bool Find(Scene main, Scene hub, out MMV3MenuController menu,
        out MS7J1PlayerHubScene oldHub, out string why)
    {
        menu = null; oldHub = null; why = "";
        var menus = main.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MMV3MenuController>(true)).ToArray();
        var hubs = hub.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MS7J1PlayerHubScene>(true)).ToArray();
        if (menus.Length != 1 || hubs.Length != 1) { why = "Expected exactly one MMV3MenuController and 7J.1 Hub root"; return false; }
        menu = menus[0]; oldHub = hubs[0];
        if (!menu.GetComponent<MS7J1HubEntry>()) { why = "7J.1 MS7J1HubEntry missing from MainMenu"; return false; }
        if (!menu.homePage || !menu.mapsPage) { why = "MainMenu V3 pages missing"; return false; }
        if (!EditorBuildSettings.scenes.Any(s => s.enabled && s.path == HubPath))
        { why = "PlayerHub.unity must be enabled in Build Profiles"; return false; }
        return true;
    }
    private static string Backup(string menuPath, out string err)
    {
        err = "";
        try
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string target = Path.Combine(project, "MS_Backups", "7J2", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            Directory.CreateDirectory(target);
            File.Copy(Path.Combine(project, menuPath), Path.Combine(target, "MainMenu_BEFORE_7J2.unity"));
            File.Copy(Path.Combine(project, HubPath), Path.Combine(target, "PlayerHub_BEFORE_7J2.unity"));
            File.WriteAllText(Path.Combine(target, "README_BACKUP.txt"),
                "7J.2 backup before visual-only edit.\nKeep 7J.1 source scripts and Save files.\n" +
                "Original scenes: " + menuPath + " and " + HubPath + "\n", Encoding.UTF8);
            return target;
        }
        catch (Exception e) { err = e.Message; return ""; }
    }
    private void WithScenes(bool modifying, bool restore)
    {
        if (EditorApplication.isPlaying) { report = "[BLOCKED] Exit Play Mode first."; return; }
        string menuPath, error;
        if (!Paths(out menuPath, out error)) { report = "[BLOCKED] " + error; return; }
        if (modifying && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        { report = "[CANCELLED] Save or discard existing edits first."; return; }
        bool openedMenu = false, openedHub = false;
        Scene main = default(Scene), hub = default(Scene), previous = SceneManager.GetActiveScene();
        try
        {
            main = Load(menuPath, out openedMenu);
            hub = Load(HubPath, out openedHub);
            MMV3MenuController menu; MS7J1PlayerHubScene old; string reason;
            if (!Find(main, hub, out menu, out old, out reason)) { report = "[BLOCKED] " + reason; return; }
            var existing = old.GetComponent<MS7J2PlayerHubPolish>();
            var styled = menu.GetComponent<MS7J2MenuEntryStyle>();
            string previousState = "7J1=" + old.enabled + " 7J2=" + (existing && existing.enabled) +
                " MainMenuSkin=" + (styled != null) + "\n";
            if (!modifying)
            {
                report = "7J.2 / READ ONLY AUDIT\nMainMenu=" + menuPath + "\nPlayerHub=" + HubPath +
                    "\n" + previousState + "MainMenu TMP source=" + (menu.selectedMapCaption && menu.selectedMapCaption.font ? "YES" : "FALLBACK") +
                    "\nNo Scene or Save changes.";
                return;
            }
            string backup = Backup(menuPath, out error);
            if (string.IsNullOrEmpty(backup)) { report = "[BLOCKED] BACKUP FAILED: " + error; return; }
            if (restore)
            {
                if (existing) Undo.DestroyObjectImmediate(existing);
                if (styled) Undo.DestroyObjectImmediate(styled);
                Undo.RecordObject(old, "7J2 Restore old PlayerHub UI");
                old.enabled = true;
                EditorUtility.SetDirty(old);
                report = "[RESTORED] Original 7J.1 PlayerHub active.\nBackup=" + backup;
            }
            else
            {
                if (!existing) existing = Undo.AddComponent<MS7J2PlayerHubPolish>(old.gameObject);
                Undo.RecordObject(existing, "7J2 Hub Scene source");
                existing.hostSceneName = "MainMenu";
                existing.enabled = true;
                EditorUtility.SetDirty(existing);
                if (!styled) styled = Undo.AddComponent<MS7J2MenuEntryStyle>(menu.gameObject);
                Undo.RecordObject(styled, "7J2 Menu entry visual source");
                styled.menu = menu;
                styled.enabled = true;
                EditorUtility.SetDirty(styled);
                Undo.RecordObject(old, "7J2 Suppress 7J1 runtime appearance");
                old.enabled = false; // Preserve the entire original 7J.1 component for rollback.
                EditorUtility.SetDirty(old);
                report = "[INSTALLED] 7J.2 native TMP PlayerHub + MainMenu button skin.\n" +
                    "7J.1 old Hub disabled but retained; original seven module scripts preserved.\n"+
                    "Backup=" + backup;
            }
            EditorSceneManager.MarkSceneDirty(main);
            EditorSceneManager.MarkSceneDirty(hub);
            if (!EditorSceneManager.SaveScene(hub)) throw new IOException("Could not save PlayerHub.unity");
            if (!EditorSceneManager.SaveScene(main)) throw new IOException("Could not save MainMenu.unity");
            Debug.Log(report);
        }
        catch (Exception e)
        {
            report = "[FAILED] " + e + "\nCheck MS_Backups/7J2 before changes.";
            Debug.LogError(report);
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (openedHub && hub.IsValid()) EditorSceneManager.CloseScene(hub, true);
            if (openedMenu && main.IsValid()) EditorSceneManager.CloseScene(main, true);
        }
    }
    private void Audit() { WithScenes(false, false); }
    private void Install() { WithScenes(true, false); }
    private void Rollback() { WithScenes(true, true); }
    private void Export()
    {
        try
        {
            string folder = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "MS_QAReports", "7J2");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "MS7J2_PlayerHubPolishQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".txt");
            Scene hub = SceneManager.GetSceneByName("PlayerHub");
            bool isLoaded = hub.IsValid() && hub.isLoaded;
            MS7J2PlayerHubPolish rt = isLoaded ?
                hub.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MS7J2PlayerHubPolish>(true)).FirstOrDefault() : null;
            string text = "MONSTERSURVIVAL 7J.2 PLAYER HUB POLISH QA\n" +
                "Time=" + DateTime.Now.ToString("o") + " Unity=" + Application.unityVersion +
                " Play=" + Application.isPlaying + " ActiveScene=" + SceneManager.GetActiveScene().name + "\n" +
                "PlayerHubLoaded=" + isLoaded + " 7J2Status=" + (rt ? rt.State : "NOT LOADED") + "\n" +
                "SelectedProfile=" + MS5A2GoldBank.SelectedSlot + "\n" + report + "\n" +
                "MANUAL PENDING: Seven tab opening/closing, TMP visibility, 1920x1080, 1280x720, 1024x768 scroll, MainMenu return, original Save/Quest/7I4 Claim, Gold+Revision isolation.\n";
            File.WriteAllText(path, text, Encoding.UTF8);
            report = "[EXPORTED] " + path + "\n" + report;
            Debug.Log(report);
        }
        catch (Exception e) { report = "[EXPORT FAILED] " + e.Message; }
    }
}
#endif
