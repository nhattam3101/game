#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 7C.4 QA-only tools. The installer adds a scene-local observer with Undo.
/// Audit does not alter Scene/Save; export writes ONLY a user-chosen .txt.
/// </summary>
public static class MS7C4SynergyQAInstaller
{
    private const string Root = "Tools/MonsterSurvival/7C.4/";

    [MenuItem(Root + "1 - Install or Repair Read-only Observer")]
    private static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("7C.4", "Stop Play Mode before installing.", "OK");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        var unified = FindInScene<MS7C3UnifiedSynergyUI>(scene);
        var guide = FindInScene<MS7C33LevelUpGuide>(scene);
        var manager = FindInScene<SkillManager>(scene);
        var stats = FindInScene<PlayerStats>(scene);
        if (unified == null || guide == null || manager == null || stats == null)
        {
            Debug.LogError("[7C.4] Cannot install. Need existing 7C.3.3 UnifiedUI + LevelUpGuide + SkillManager + PlayerStats in active SampleScene.");
            EditorUtility.DisplayDialog("7C.4 Missing Dependencies",
                "Install/repair 7C.3.3 first. Check Console for missing dependencies. No changes made.", "OK");
            return;
        }
        var observers = InScene<MS7C4SynergyObserver>(scene);
        if (observers.Count > 1)
        {
            Debug.LogError("[7C.4] Multiple observers detected. Remove duplicates before installing; no changes made.");
            return;
        }
        if (!EditorUtility.DisplayDialog("7C.4 QA Only",
            "Install/repair ONE read-only observer on the existing unified synergy UI?\n\nNo edits to Save/Gold/EXP/PlayerStats/SkillManager/7C2 combat or prefab assets.",
            "Install", "Cancel")) return;
        MS7C4SynergyObserver observer = observers.Count == 1 ? observers[0] :
            Undo.AddComponent<MS7C4SynergyObserver>(unified.gameObject);
        if (observer.gameObject != unified.gameObject)
        {
            Debug.LogError("[7C.4] Observer already exists on another object. Remove it using menu 4, then reinstall. No new component added.");
            return;
        }
        Undo.RecordObject(observer, "7C.4 assign QA references");
        observer.EditorAssign(manager, stats, unified, guide);
        EditorUtility.SetDirty(observer);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = unified.gameObject;
        Debug.Log("[7C.4] Observer installed/repaired. Ctrl+S the Scene; Play Mode and Export Snapshot to inspect 6 combo states.", observer);
    }

    [MenuItem(Root + "2 - Audit Active Scene (Read Only)")]
    private static void Audit()
    {
        string report = BuildSceneReport();
        Debug.Log(report);
    }

    [MenuItem(Root + "3 - Export Snapshot or Audit as TXT")]
    private static void Export()
    {
        Scene scene = SceneManager.GetActiveScene();
        MS7C4SynergyObserver observer = FindInScene<MS7C4SynergyObserver>(scene);
        string report = BuildSceneReport();
        if (EditorApplication.isPlaying && observer != null)
            report += "\n\n" + observer.BuildReport();
        else
            report += "\n[NOT RUNTIME VERIFIED] Enter Play Mode to record actual skill levels and combat-active states.\n";
        string filename = "MS7C4_SynergyQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
        string path = EditorUtility.SaveFilePanel("Save 7C.4 read-only QA report",
            Path.GetDirectoryName(Application.dataPath), filename, "txt");
        if (string.IsNullOrEmpty(path)) return;
        // Never silently write into Unity's persistentDataPath / save directory.
        File.WriteAllText(path, report, new UTF8Encoding(false));
        Debug.Log("[7C.4] Diagnostic report exported to: " + path + " (No Save data modified)");
        EditorUtility.RevealInFinder(path);
    }

    [MenuItem(Root + "4 - Remove Only 7C.4 QA Observer")]
    private static void Remove()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        List<MS7C4SynergyObserver> found = InScene<MS7C4SynergyObserver>(scene);
        if (found.Count == 0)
        {
            Debug.Log("[7C.4] No observer to remove."); return;
        }
        if (!EditorUtility.DisplayDialog("7C.4 Rollback",
            "Remove ONLY 7C.4 QA Observer component(s) from active Scene?\nExisting UI and gameplay stay intact.",
            "Remove", "Cancel")) return;
        foreach (var observer in found) Undo.DestroyObjectImmediate(observer);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[7C.4] Read-only Observer removed. Save Scene with Ctrl+S.");
    }

    public static string BuildSceneReport()
    {
        Scene scene = SceneManager.GetActiveScene();
        var b = new StringBuilder(4600);
        int pass = 0, warn = 0, error = 0;
        b.AppendLine("MONSTERSURVIVAL 7C.4 - EDITOR STRUCTURAL AUDIT (READ ONLY)");
        b.Append("Unity ").Append(Application.unityVersion).Append(" | Scene ")
         .Append(scene.name).Append(" | Date ")
         .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).AppendLine();
        var uiList = InScene<MS7C3UnifiedSynergyUI>(scene);
        var guideList = InScene<MS7C33LevelUpGuide>(scene);
        var observerList = InScene<MS7C4SynergyObserver>(scene);
        var managers = InScene<SkillManager>(scene);
        var stats = InScene<PlayerStats>(scene);
        if (uiList.Count == 1) { pass++; b.AppendLine("[PASS] 1 unified J/L UI"); }
        else { error++; b.AppendLine("[ERROR] Unified UI count=" + uiList.Count + " (expected 1)"); }
        if (guideList.Count == 1) { pass++; b.AppendLine("[PASS] 1 level-up choice guide"); }
        else { error++; b.AppendLine("[ERROR] Guide count=" + guideList.Count + " (expected 1)"); }
        if (observerList.Count == 1 && observerList[0].EditorReferencesValid)
        { pass++; b.AppendLine("[PASS] 1 observer with valid scene references"); }
        else { error++; b.AppendLine("[ERROR] Observer missing/duplicate/missing references. Count=" + observerList.Count); }
        if (managers.Count == 1 && managers[0].skillPanel != null &&
            managers[0].skillButtons != null && managers[0].skillButtons.Length >= 3 &&
            managers[0].skillNames != null && managers[0].skillNames.Length >= 3)
        { pass++; b.AppendLine("[PASS] 1 SkillManager with 3 cards and skillPanel"); }
        else { error++; b.AppendLine("[ERROR] SkillManager count/panel/card refs wrong. Count=" + managers.Count); }
        if (stats.Count == 1) { pass++; b.AppendLine("[PASS] 1 PlayerStats"); }
        else { error++; b.AppendLine("[ERROR] PlayerStats count=" + stats.Count); }

        if (managers.Count == 1)
        {
            SkillManager manager = managers[0];
            int skillCount = manager.allSkills == null ? 0 : manager.allSkills.Count;
            b.AppendLine("[INFO] SkillData entries in SkillManager.allSkills = " + skillCount);
            var seen = new HashSet<SkillData>();
            if (manager.allSkills != null)
                foreach (SkillData entry in manager.allSkills)
                    if (entry != null && !seen.Add(entry))
                    { warn++; b.AppendLine("[WARN] Duplicate SkillData asset reference: " + entry.name); }
            for (int i = 0; i < MS7C33SynergyRules.Combos.Length; i++)
            {
                var combo = MS7C33SynergyRules.Combos[i];
                bool one = TryFindSkill(manager, combo.First, out string firstDetail);
                bool two = TryFindSkill(manager, combo.Second, out string secondDetail);
                if (one && two) { pass++; b.Append("[PASS] "); }
                else { warn++; b.Append("[WARN] "); }
                b.Append(combo.Title).Append(" | ")
                 .Append(combo.First).Append(" => ").Append(firstDetail).Append(" | ")
                 .Append(combo.Second).Append(" => ").Append(secondDetail).AppendLine();
            }
        }
        if (uiList.Count == 1)
        {
            for (int i = 0; i < 6; ++i)
            {
                if (uiList[0].GetComboIcon(i) != null) pass++;
                else { warn++; b.AppendLine("[WARN] Combo sprite index " + i + " is missing"); }
            }
        }
        b.AppendLine("[INFO] ACTIVE on new combos requires a real 7C.2 status bus or configured public bool member; asset ownership alone is not combat proof.");
        b.AppendLine("[INFO] EDITOR AUDIT IS NOT A PLAY MODE TEST. No Scene, Asset, Save or gameplay state written by audit.");
        b.Append("RESULT: PASS=").Append(pass).Append(" WARN=").Append(warn).Append(" ERROR=").Append(error).AppendLine();
        return b.ToString();
    }

    private static bool TryFindSkill(SkillManager manager, string target, out string detail)
    {
        if (manager.allSkills != null)
            foreach (SkillData skill in manager.allSkills)
                if (skill != null && MS7C33SynergyRules.Matches(skill, target))
                {
                    detail = "FOUND " + skill.name + " (type " + skill.skillType + ")";
                    return true;
                }
        detail = "NOT FOUND in allSkills";
        return false;
    }

    private static List<T> InScene<T>(Scene scene) where T : Component
    {
        var matches = new List<T>();
        T[] all = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (T item in all)
            if (item != null && item.gameObject.scene == scene) matches.Add(item);
        return matches;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        var found = InScene<T>(scene);
        return found.Count == 1 ? found[0] : null;
    }
}
#endif
