#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 7C.4.2 non-destructive repair menu. It NEVER calls the 7C.2 source patcher.
/// Reuses the original observer + UI; edits no skills, combat, prefab or Save.
/// </summary>
public static class MS7C42SynergyRepair
{
    private const string Menu = "Tools/MonsterSurvival/7C.4.2/";

    [MenuItem(Menu + "1 - Repair UI and Observer references (EDIT MODE)")]
    private static void Repair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogError("[7C.4.2] Stop Play Mode before Repair."); return; }
        Scene scene = SceneManager.GetActiveScene();
        var manager = InScene<SkillManager>(scene);
        var stats = InScene<PlayerStats>(scene);
        var ui = InScene<MS7C3UnifiedSynergyUI>(scene);
        var guide = InScene<MS7C33LevelUpGuide>(scene);
        var observer = InScene<MS7C4SynergyObserver>(scene);
        if (manager == null || stats == null || ui == null || guide == null || observer == null)
        {
            Debug.LogError("[7C.4.2] Missing a scene component. Use 7C.3.3 and 7C.4.1 installers first. Nothing changed.");
            return;
        }
        // UI's legacy 7C2 bool fields are intentionally not used by the new resolver.
        // Preserve Sprite and old view toggle settings by reusing the same component.
        Undo.RecordObject(observer, "7C.4.2 Repair Observer References");
        observer.EditorAssign(manager, stats, ui, guide);
        EditorUtility.SetDirty(observer);
        // Original 7C.2 scene installation is NOT performed automatically.
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[7C.4.2] Observer wired; UI reads 7C.1/7C.2 on Player at runtime. Save Scene (Ctrl+S). No combat or Save modifications.");
        Audit();
    }

    [MenuItem(Menu + "2 - Audit integration (READ ONLY)")]
    private static void Audit()
    {
        Scene scene = SceneManager.GetActiveScene();
        var manager = InScene<SkillManager>(scene);
        var stats = InScene<PlayerStats>(scene);
        var ui = InScene<MS7C3UnifiedSynergyUI>(scene);
        var guide = InScene<MS7C33LevelUpGuide>(scene);
        var observer = InScene<MS7C4SynergyObserver>(scene);
        var core = stats != null ? stats.GetComponent<MS7C1SynergyCore>() : null;
        var combat = core != null ? core.GetComponent<MS7C2SynergyCombat>() : null;
        var coord = manager != null ? manager.GetComponent<MS7B2ElementalSkillCoordinator>() : null;
        int pass = 0, warn = 0, error = 0;
        Check(manager != null, "SkillManager", ref pass, ref error);
        Check(stats != null, "PlayerStats", ref pass, ref error);
        Check(ui != null, "7C.3 Unified J/L UI", ref pass, ref error);
        Check(guide != null, "7C.3.3 Choice Guide", ref pass, ref error);
        Check(observer != null, "7C.4 Observer", ref pass, ref error);
        Check(core != null, "7C.1 core attached to Player", ref pass, ref error);
        if (coord == null || coord.fireSkill == null || coord.iceSkill == null || coord.lightningSkill == null)
        { error++; Debug.LogError("[7C.4.2 AUDIT] Missing 7B.2 coordinator/elemental SkillData"); }
        else { pass++; Debug.Log("[7C.4.2 AUDIT] PASS exact 7B.2 Fire/Ice/Lightning SkillData refs (SkillType.Heal is expected)"); }
        if (combat == null)
        { warn++; Debug.LogWarning("[7C.4.2 AUDIT] 7C.2 Combat NOT INSTALLED on Player. Run original 7C.2 install + hook audit (menu 5). UI will show 7C2 ?."); }
        else if (combat.core != core)
        { error++; Debug.LogError("[7C.4.2 AUDIT] 7C.2 Combat exists but core ref is missing/different. Wire it on Player's Inspector."); }
        else { pass++; Debug.Log("[7C.4.2 AUDIT] PASS 7C.2 combat component reference; NOTE hook sites still require original 7C.2 audit"); }
        if (observer != null && !observer.EditorReferencesValid)
        { warn++; Debug.LogWarning("[7C.4.2 AUDIT] Observer references incomplete: run Repair menu 1"); }
        if (manager != null && manager.allSkills != null && coord != null)
        {
            bool found = manager.allSkills.Contains(coord.fireSkill) && manager.allSkills.Contains(coord.iceSkill) && manager.allSkills.Contains(coord.lightningSkill);
            if (found) pass++; else { error++; Debug.LogError("[7C.4.2 AUDIT] Elemental skills missing from allSkills"); }
        }
        Debug.Log("[7C.4.2 AUDIT] PASS="+pass+" WARN="+warn+" ERROR="+error+
            ". Structure only. 7C.2's original 4-hook audit and Play Mode proc counters remain mandatory.");
    }

    [MenuItem(Menu + "3 - Export truth snapshot as TXT (PLAY MODE)")]
    private static void Export()
    {
        if (!EditorApplication.isPlaying)
        { Debug.LogWarning("[7C.4.2] Enter Play Mode and learn combos before exporting."); return; }
        var stats = UnityEngine.Object.FindFirstObjectByType<PlayerStats>();
        var manager = UnityEngine.Object.FindFirstObjectByType<SkillManager>();
        if (stats == null || manager == null) { Debug.LogError("[7C.4.2] No runtime Player/SkillManager"); return; }
        var report = new StringBuilder();
        report.AppendLine("MONSTERSURVIVAL 7C.4.2 - LIVE SYNERGY TRUTH (READ ONLY)");
        report.Append("Unity ").Append(Application.unityVersion).Append(" | Scene ").Append(SceneManager.GetActiveScene().name)
            .Append(" | Time ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).AppendLine();
        report.AppendLine("READY is equipment-armed, NOT an observed proc; procs come from 7C.2 runtime counters.");
        for (int i=0;i<6;i++)
        {
            var combo=MS7C33SynergyRules.Combos[i];
            int a=MS7C33SynergyRules.OwnedLevel(manager,combo.First);
            int b=MS7C33SynergyRules.OwnedLevel(manager,combo.Second);
            report.Append((i+1)+". "+combo.Title+" | "+combo.First+" Lv"+a+" + "+combo.Second+" Lv"+b);
            if (i<3) report.Append(" | Legacy unlock=").Append(MS7C33SynergyRules.IsActualLegacyUnlocked(stats,i));
            else
            {
                var s=MS7C42SynergyStatus.Read(manager,stats,i);
                report.Append(" | 7C1="+s.coreFound+" unlocked="+s.coreUnlocked+" 7C2="+s.combatFound+
                    " READY="+s.ready+" PROCS="+s.procs+" | "+s.note);
            }
            report.AppendLine();
        }
        var core=MS7C42SynergyStatus.FindCore(stats);
        var combat=MS7C42SynergyStatus.FindCombat(core);
        if(combat == null) report.AppendLine("ACTION: Run original 7C.2 installer/audit; 7C.4.2 does NOT add combat hooks.");
        else report.AppendLine("ACTION: Run original 7C.2 READ ONLY audit to confirm all four hooks, then manually trigger combat.");
        string path=EditorUtility.SaveFilePanel("Export 7C.4.2 runtime truth", Application.dataPath,
            "MS7C42_SynergyTruth_"+DateTime.Now.ToString("yyyyMMdd_HHmmss"),"txt");
        if (string.IsNullOrEmpty(path)) return;
        File.WriteAllText(path,report.ToString(),new UTF8Encoding(false));
        Debug.Log("[7C.4.2] Exported runtime report: "+path+"\n"+report);
    }

    private static void Check(bool ok,string name,ref int pass,ref int error)
    {
        if(ok){pass++;Debug.Log("[7C.4.2 AUDIT] PASS "+name);}
        else {error++;Debug.LogError("[7C.4.2 AUDIT] MISSING "+name);}
    }

    private static T InScene<T>(Scene scene) where T:Component
    {
        foreach(T item in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(item!=null && item.gameObject.scene==scene) return item;
        return null;
    }
}
#endif
