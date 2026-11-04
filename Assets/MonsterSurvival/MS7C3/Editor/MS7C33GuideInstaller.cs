#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 7C.3.3 opt-in installer. Never changes 7C1 core, 7C2 combat,
/// SkillManager, SkillData assets, player stats, prefabs or Saves.
/// </summary>
public static class MS7C33GuideInstaller
{
    [MenuItem("Tools/MonsterSurvival/7C.3/4 - Install or Repair 7C.3.3 Choice Guide")]
    private static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("7C.3.3", "Thoat Play Mode truoc khi cai dat.", "OK");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        MS7C3UnifiedSynergyUI ui = FindInScene<MS7C3UnifiedSynergyUI>(scene);
        SkillManager manager = FindInScene<SkillManager>(scene);
        PlayerStats stats = FindInScene<PlayerStats>(scene);
        if (ui == null || manager == null || stats == null || manager.skillPanel == null)
        {
            Debug.LogError("[7C.3.3] Need ONE installed 7C.3 unified UI, SkillManager, PlayerStats and skillPanel in the active scene. Run 7C.3 menu 1 first only if no unified UI exists.");
            EditorUtility.DisplayDialog("7C.3.3 Missing refs",
                "Can mo SampleScene va kiem tra: MS7C3_UnifiedSynergyUI, SkillManager, PlayerStats, skillPanel. Console co chi tiet.", "OK");
            return;
        }
        MS7C33LevelUpGuide existing = ui.GetComponent<MS7C33LevelUpGuide>();
        if (existing == null)
        {
            if (!EditorUtility.DisplayDialog("Install 7C.3.3",
                "Add a READ-ONLY level-up combo guide to existing MS7C3_UnifiedSynergyUI?\nNo gameplay, Save, or Prefab changes.",
                "Install", "Cancel")) return;
            existing = Undo.AddComponent<MS7C33LevelUpGuide>(ui.gameObject);
        }
        Undo.RecordObject(existing, "7C.3.3 assign gameplay refs");
        existing.EditorAssign(manager,stats,ui);
        EditorUtility.SetDirty(existing);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject=ui.gameObject;
        Debug.Log("[7C.3.3] Choice Guide ready (on existing unified UI). Save the Scene with Ctrl+S. Open Level Up in Play Mode to verify 6 cards.",ui);
    }

    [MenuItem("Tools/MonsterSurvival/7C.3/5 - Audit 7C.3.3 (Read Only)")]
    private static void Audit()
    {
        Scene scene = SceneManager.GetActiveScene();
        MS7C3UnifiedSynergyUI ui = FindInScene<MS7C3UnifiedSynergyUI>(scene);
        MS7C33LevelUpGuide[] guides = Object.FindObjectsByType<MS7C33LevelUpGuide>(FindObjectsInactive.Include);
        int count = 0;
        MS7C33LevelUpGuide guide = null;
        foreach (var component in guides)
        {
            if (component != null && component.gameObject.scene == scene)
            { count++; guide = component; }
        }
        int errors = 0;
        if (ui == null) { errors++; Debug.LogError("[7C.3.3 AUDIT] Missing unified UI"); }
        if (count != 1) { errors++; Debug.LogError("[7C.3.3 AUDIT] Expected exactly one Choice Guide; found="+count); }
        if (guide != null && !guide.EditorValid) { errors++; Debug.LogError("[7C.3.3 AUDIT] Missing guide refs"); }
        SkillManager manager = FindInScene<SkillManager>(scene);
        if (manager == null || manager.skillPanel == null || manager.skillButtons == null || manager.skillButtons.Length < 3)
        { errors++; Debug.LogError("[7C.3.3 AUDIT] SkillManager 3-card UI unavailable"); }
        if (errors == 0) Debug.Log("[7C.3.3 AUDIT] 5/5 structure checks PASS. Not Play Mode verified. Test Level Up and after choosing combo.");
        else Debug.LogWarning("[7C.3.3 AUDIT] " + errors + " structural ERROR(s). No project files modified by audit.");
    }

    [MenuItem("Tools/MonsterSurvival/7C.3/6 - Remove Only 7C.3.3 Choice Guide")]
    private static void RemoveGuide()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        MS7C33LevelUpGuide[] guides = Object.FindObjectsByType<MS7C33LevelUpGuide>(FindObjectsInactive.Include);
        if (!EditorUtility.DisplayDialog("Remove 7C.3.3",
            "Remove only Choice Guide components? Existing J/L synergy UI and all combat/save systems stay.",
            "Remove", "Cancel")) return;
        foreach (var guide in guides)
            if (guide != null && guide.gameObject.scene == scene)
                Undo.DestroyObjectImmediate(guide);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[7C.3.3] Choice Guide removed; unified 7C.3 UI preserved.");
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        T[] all = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        foreach (T item in all)
            if (item != null && item.gameObject.scene == scene) return item;
        return null;
    }
}
#endif
