#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Install/remove ONE runtime dashboard component. No Save/Gold/Inventory writes.
public static class MS5D4DashboardInstaller
{
    private const string Menu = "Tools/MonsterSurvival/5D Profile Overview/";

    [MenuItem(Menu + "14 - INSTALL 5D.4 Dashboard Final Polish (OPEN MainMenu)")]
    public static void Install()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != "MainMenu" || string.IsNullOrEmpty(scene.path))
        { EditorUtility.DisplayDialog("5D.4", "Stop Play Mode and open the saved MainMenu Scene.", "OK"); return; }
        MS5D1ProfileOverview original = FindInScene<MS5D1ProfileOverview>(scene);
        MS5D2ProfilePolish polish = FindInScene<MS5D2ProfilePolish>(scene);
        MS5D3StatisticsTab stats = FindInScene<MS5D3StatisticsTab>(scene);
        if (!original || !polish || !stats || original.gameObject != polish.gameObject || polish.gameObject != stats.gameObject)
        { EditorUtility.DisplayDialog("5D.4 Prerequisites", "5D.1, 5D.2 and 5D.3.2 must exist on the same Profile root in MainMenu. No changes made.", "OK"); return; }
        if (!original.launcher || !original.existingProfileWidget || !polish.originalOverview || !stats.profilePolish)
        { EditorUtility.DisplayDialog("5D.4 Prerequisites", "One or more existing Profile references are missing. Run the 5D.1 / 5D.2 / 5D.3 audit first.", "OK"); return; }
        if (stats.GetComponent<MS5D4ProgressRibbon>())
        { EditorUtility.DisplayDialog("5D.4", "5D.4 is already installed; nothing duplicated.", "OK"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!EditorUtility.DisplayDialog("Install 5D.4", "Add a visual-only progress summary and responsive labels to the existing PROFILE / STATISTICS tab?\n\nMainMenu will be backed up before changes. No Save, Gold, Skill, Boss or Item values are written.", "INSTALL", "CANCEL")) return;
        try
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string backupDir = Path.Combine(project,"MS_Backups","5D4");
            Directory.CreateDirectory(backupDir);
            string backup = Path.Combine(backupDir,"MainMenu_BEFORE_5D4_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
            File.Copy(Path.GetFullPath(scene.path),backup,false);
            MS5D4ProgressRibbon ribbon = Undo.AddComponent<MS5D4ProgressRibbon>(stats.gameObject);
            ribbon.profilePolish=polish;
            ribbon.statisticsTab=stats;
            EditorUtility.SetDirty(ribbon);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[5D.4] Installed on " + stats.name + ". Scene backup: " + backup + ". Save data untouched.");
            EditorUtility.DisplayDialog("5D.4 installed", "MainMenu saved. Open Play Mode > PROFILE > STATISTICS and scroll down to PROGRESSION SUMMARY.\nBackup: " + backup, "OK");
        }
        catch(Exception ex)
        {
            Debug.LogError("[5D.4] Install exception: " + ex);
            EditorUtility.DisplayDialog("5D.4 Install failed", ex.Message, "OK");
        }
    }

    [MenuItem(Menu + "15 - OPEN 5D.4 Final Dashboard QA (READ ONLY)")]
    public static void OpenQA() { MS5D4DashboardQAWindow.ShowWindow(); }

    [MenuItem(Menu + "16 - REMOVE 5D.4 Component ONLY (OPEN MainMenu)")]
    public static void Remove()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != "MainMenu")
        { EditorUtility.DisplayDialog("5D.4", "Stop Play Mode and open MainMenu.","OK");return; }
        MS5D4ProgressRibbon ribbon = FindInScene<MS5D4ProgressRibbon>(scene);
        if (!ribbon) { EditorUtility.DisplayDialog("5D.4","Already absent.","OK");return; }
        if (!EditorUtility.DisplayDialog("Remove 5D.4", "Remove only the extra progress summary/responsive polish? 5D.1–5D.3.2 and all Save data remain intact.","REMOVE","CANCEL")) return;
        Undo.DestroyObjectImmediate(ribbon);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[5D.4] Removed visual-only component. Existing profile UI and Save unchanged.");
    }

    internal static T FindInScene<T>(Scene scene) where T:Component
    {
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach(GameObject root in scene.GetRootGameObjects())
        {
            T c=root.GetComponentInChildren<T>(true);
            if(c) return c;
        }
        return null;
    }
}
#endif
