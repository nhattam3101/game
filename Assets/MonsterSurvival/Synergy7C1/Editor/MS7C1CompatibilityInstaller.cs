#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Unique editor class in a unique folder. Do NOT duplicate in Assets/Editor.
public sealed class MS7C1CompatibilityInstaller : EditorWindow
{
    private SkillManager manager;
    private PlayerStats stats;
    private MS7B2ElementalSkillCoordinator elemental;
    private MS7B1ElementLoadout loadout;
    private Canvas canvas;
    private Vector2 scroll;

    [MenuItem("Tools/MonsterSurvival/7C Skill Synergy/1 - OPEN 7C.1 Compatibility Installer")]
    public static void Open()
    {
        MS7C1CompatibilityInstaller window = GetWindow<MS7C1CompatibilityInstaller>("MS 7C.1 Synergy");
        window.minSize = new Vector2(550, 460);
        window.FindSceneReferences();
    }

    [MenuItem("Tools/MonsterSurvival/7C Skill Synergy/2 - AUDIT 7C.1 (READ ONLY)")]
    public static void MenuAudit() { Open(); GetWindow<MS7C1CompatibilityInstaller>().Audit(); }

    [MenuItem("Tools/MonsterSurvival/7C Skill Synergy/3 - RUN PURE UNLOCK TESTS (NO SCENE WRITES)")]
    public static void MenuFormulaQA() { RunPureUnlockTests(); }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("7C.1 — NEW SYNERGY UNLOCK CORE", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Preserves Frozen Burst / Phantom Chain / Rapid Blast (older 2C). New synergies only detect owned skills; NO extra damage, buffs or Save writes.", MessageType.Info);
        EditorGUILayout.LabelField("Scene: " + SceneManager.GetActiveScene().name + " | Edit Mode: " + (!EditorApplication.isPlaying));
        GUILayout.Space(8);
        manager = (SkillManager)EditorGUILayout.ObjectField("Scene SkillManager", manager, typeof(SkillManager), true);
        stats = (PlayerStats)EditorGUILayout.ObjectField("Scene PlayerStats", stats, typeof(PlayerStats), true);
        elemental = (MS7B2ElementalSkillCoordinator)EditorGUILayout.ObjectField("7B.2 Coordinator", elemental, typeof(MS7B2ElementalSkillCoordinator), true);
        loadout = (MS7B1ElementLoadout)EditorGUILayout.ObjectField("7B.1 Loadout", loadout, typeof(MS7B1ElementLoadout), true);
        canvas = (Canvas)EditorGUILayout.ObjectField("Gameplay Canvas", canvas, typeof(Canvas), true);
        GUILayout.Space(9);
        if (GUILayout.Button("FIND CURRENT SCENE REFERENCES", GUILayout.Height(31))) FindSceneReferences();
        if (GUILayout.Button("AUDIT CURRENT SCENE + OLD COMBO PRESENCE (READ ONLY)", GUILayout.Height(34))) Audit();
        if (GUILayout.Button("RUN 10 PURE UNLOCK TESTS (NO SCENE WRITES)", GUILayout.Height(30))) RunPureUnlockTests();
        GUI.enabled = !EditorApplication.isPlaying;
        if (GUILayout.Button("INSTALL / UPDATE 7C.1 (SCENE-ONLY; IDEMPOTENT)", GUILayout.Height(38))) Install();
        if (GUILayout.Button("REMOVE 7C.1 COMPONENTS ONLY", GUILayout.Height(31))) Remove();
        GUI.enabled = true;
        GUILayout.Space(8);
        EditorGUILayout.HelpBox("Use existing SKILL SYNERGY [J] for the 3 original combos. A separate NEW COMBOS [L] button displays only the 3 new unlocks. 7C.2 will implement combat effects.", MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    private void FindSceneReferences()
    {
        manager = UnityEngine.Object.FindFirstObjectByType<SkillManager>(FindObjectsInactive.Include);
        stats = UnityEngine.Object.FindFirstObjectByType<PlayerStats>(FindObjectsInactive.Include);
        elemental = manager != null ? manager.GetComponent<MS7B2ElementalSkillCoordinator>() :
            UnityEngine.Object.FindFirstObjectByType<MS7B2ElementalSkillCoordinator>(FindObjectsInactive.Include);
        loadout = stats != null ? stats.GetComponent<MS7B1ElementLoadout>() :
            UnityEngine.Object.FindFirstObjectByType<MS7B1ElementLoadout>(FindObjectsInactive.Include);
        canvas = manager != null && manager.skillPanel != null ? manager.skillPanel.GetComponentInParent<Canvas>(true) : null;
        if (canvas == null)
        {
            Canvas[] all = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Canvas c in all)
            {
                if (c != null && c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded)
                { canvas = c; break; }
            }
        }
        Repaint();
    }

    private static bool IsSceneObject(Component component)
    {
        return component != null && !EditorUtility.IsPersistent(component) &&
               component.gameObject.scene.IsValid() && component.gameObject.scene.isLoaded;
    }

    private bool CanInstall()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[7C.1] Exit Play Mode before installing."); return false; }
        if (!IsSceneObject(manager) || !IsSceneObject(stats) || !IsSceneObject(elemental) || !IsSceneObject(loadout))
        { Debug.LogError("[7C.1] Need SCENE SkillManager, PlayerStats, 7B.2 Coordinator and 7B.1 Loadout. Open gameplay scene and click FIND."); return false; }
        if (manager.GetComponent<MS7B2ElementalSkillCoordinator>() != elemental)
        { Debug.LogError("[7C.1] 7B.2 Coordinator must be on the same object as SkillManager."); return false; }
        if (!IsSceneObject(canvas)) { Debug.LogError("[7C.1] Assign existing gameplay Canvas (not a prefab asset)."); return false; }
        if (elemental.fireSkill == null || elemental.iceSkill == null || elemental.lightningSkill == null)
        { Debug.LogError("[7C.1] 7B.2 elemental SkillData references are missing."); return false; }
        if (manager.allSkills == null || !manager.allSkills.Contains(elemental.fireSkill) ||
            !manager.allSkills.Contains(elemental.iceSkill) || !manager.allSkills.Contains(elemental.lightningSkill))
        { Debug.LogError("[7C.1] 7B.2 elemental skills not registered in SkillManager.allSkills."); return false; }
        return true;
    }

    private void Install()
    {
        FindSceneReferences();
        if (!CanInstall()) return;
        // Backup actual scene into Assets sibling (not a duplicate script).
        Scene scene = stats.gameObject.scene;
        if (string.IsNullOrEmpty(scene.path)) { Debug.LogError("[7C.1] Save the gameplay Scene before installing."); return; }
        EditorSceneManager.SaveOpenScenes();
        string backupDir = Path.Combine(Directory.GetCurrentDirectory(), "MS_Backups", "7C1");
        Directory.CreateDirectory(backupDir);
        string backup = Path.Combine(backupDir, Path.GetFileNameWithoutExtension(scene.path) + "_before_7C1_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
        File.Copy(Path.GetFullPath(scene.path), backup, false);

        MS7C1SynergyCore[] existingCores = UnityEngine.Object.FindObjectsByType<MS7C1SynergyCore>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (MS7C1SynergyCore existing in existingCores)
            if (existing != null && existing.gameObject != stats.gameObject && IsSceneObject(existing))
            { Debug.LogError("[7C.1] Another SynergyCore already exists in the open scene: " + existing.name + ". Remove duplicate manually before installing."); return; }
        MS7C1SynergyCore core = stats.GetComponent<MS7C1SynergyCore>();
        if (core == null) core = Undo.AddComponent<MS7C1SynergyCore>(stats.gameObject);
        core.skillManager = manager;
        core.elementalCards = elemental;
        core.loadout = loadout;
        EditorUtility.SetDirty(core);

        MS7C1SynergyReadout[] existingReadouts = UnityEngine.Object.FindObjectsByType<MS7C1SynergyReadout>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (MS7C1SynergyReadout existing in existingReadouts)
            if (existing != null && existing.gameObject != canvas.gameObject && IsSceneObject(existing))
            { Debug.LogError("[7C.1] Another Readout is already attached to a different Canvas. Remove duplicate manually."); return; }
        MS7C1SynergyReadout readout = canvas.GetComponent<MS7C1SynergyReadout>();
        if (readout == null) readout = Undo.AddComponent<MS7C1SynergyReadout>(canvas.gameObject);
        readout.core = core;
        EditorUtility.SetDirty(readout);
        EditorSceneManager.MarkSceneDirty(scene);
        if (canvas.gameObject.scene != scene) EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[7C.1 INSTALL] SUCCESS: 1 SynergyCore on Player + 1 Readout on gameplay Canvas. Old 3 combos untouched. Backup: " + backup);
        Audit();
    }

    private void Audit()
    {
        FindSceneReferences();
        int pass = 0, warn = 0, error = 0;
        if (IsSceneObject(manager)) pass++; else error++;
        if (IsSceneObject(stats)) pass++; else error++;
        if (IsSceneObject(elemental)) pass++; else error++;
        if (IsSceneObject(loadout)) pass++; else error++;
        if (IsSceneObject(canvas)) pass++; else warn++;
        if (elemental != null)
        {
            if (elemental.fireSkill != null && elemental.iceSkill != null && elemental.lightningSkill != null &&
                elemental.fireSkill != elemental.iceSkill && elemental.fireSkill != elemental.lightningSkill && elemental.iceSkill != elemental.lightningSkill) pass++; else error++;
            if (manager != null && manager.allSkills != null && manager.allSkills.Contains(elemental.fireSkill) &&
                manager.allSkills.Contains(elemental.iceSkill) && manager.allSkills.Contains(elemental.lightningSkill)) pass++; else error++;
        }
        int coreCount = UnityEngine.Object.FindObjectsByType<MS7C1SynergyCore>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        int uiCount = UnityEngine.Object.FindObjectsByType<MS7C1SynergyReadout>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        if (coreCount == 1) pass++; else if (coreCount == 0) warn++; else error++;
        if (uiCount == 1) pass++; else if (uiCount == 0) warn++; else error++;
        int oldPanelCount = 0;
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (MonoBehaviour b in behaviours)
            if (b != null && b.GetType().Name == "SkillSynergyPanelUI") oldPanelCount++;
        if (oldPanelCount == 1) pass++; else if (oldPanelCount == 0) warn++; else error++;
        Debug.Log("[7C.1 AUDIT] PASS="+pass+" WARN="+warn+" ERROR="+error+" | New core="+coreCount+" readout="+uiCount+" | OLD SkillSynergyPanelUI="+oldPanelCount+
            "\nOld: Frozen Burst / Phantom Chain / Rapid Blast — no modifications or replacements.\nNew: Wildfire Conduit / Glacial Rebound / Thunder Precision — unlock detection only.\nNo gameplay bonuses, no Save changes, no Boss HP edits.");
    }

    private static void RunPureUnlockTests()
    {
        int pass = 0;
        Action<bool,string> check = (ok,name) =>
        {
            if (ok) { pass++; Debug.Log("[7C.1 PURE TEST] PASS " + name); }
            else Debug.LogError("[7C.1 PURE TEST] FAIL " + name);
        };
        check(!MS7C1SynergyCore.HasWildfire(0,0), "Wildfire none => locked");
        check(!MS7C1SynergyCore.HasWildfire(1,0), "Wildfire Fire only => locked");
        check(!MS7C1SynergyCore.HasWildfire(0,1), "Wildfire Lightning only => locked");
        check(MS7C1SynergyCore.HasWildfire(1,1), "Wildfire both => unlocked");
        check(!MS7C1SynergyCore.HasGlacial(1,false), "Glacial Ice only => locked");
        check(!MS7C1SynergyCore.HasGlacial(0,true), "Glacial Ricochet only => locked");
        check(MS7C1SynergyCore.HasGlacial(1,true), "Glacial both => unlocked");
        check(!MS7C1SynergyCore.HasThunder(1,false), "Thunder Lightning only => locked");
        check(!MS7C1SynergyCore.HasThunder(0,true), "Thunder Critical only => locked");
        check(MS7C1SynergyCore.HasThunder(3,true), "Thunder high level + Critical => unlocked");
        Debug.Log("[7C.1 PURE UNLOCK TEST] " + pass + "/10 PASS. These check CONDITIONS only, not gameplay effects, Save or Play Mode.");
    }

    private void Remove()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[7C.1] Exit Play Mode first."); return; }
        MS7C1SynergyReadout[] readouts = UnityEngine.Object.FindObjectsByType<MS7C1SynergyReadout>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        MS7C1SynergyCore[] cores = UnityEngine.Object.FindObjectsByType<MS7C1SynergyCore>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int count = 0;
        foreach (MS7C1SynergyReadout r in readouts) if (IsSceneObject(r)) { Undo.DestroyObjectImmediate(r); count++; }
        foreach (MS7C1SynergyCore c in cores) if (IsSceneObject(c)) { Undo.DestroyObjectImmediate(c); count++; }
        if (count > 0)
        {
            foreach (Scene sc in new List<Scene>(GetLoadedScenes())) if (sc.IsValid() && sc.isLoaded) EditorSceneManager.MarkSceneDirty(sc);
            EditorSceneManager.SaveOpenScenes();
        }
        Debug.Log("[7C.1 REMOVE] Removed "+count+" 7C.1 components. Existing 2C synergy + 7B skills unchanged.");
    }

    private static IEnumerable<Scene> GetLoadedScenes()
    {
        for (int i=0;i<SceneManager.sceneCount;i++) yield return SceneManager.GetSceneAt(i);
    }
}
#endif
