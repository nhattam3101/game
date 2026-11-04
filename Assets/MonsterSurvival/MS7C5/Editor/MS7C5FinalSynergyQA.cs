#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// MonsterSurvival 7C.5 - read-only release-gate dashboard for Unity 6000.6.
/// No scene modifications, no player/skill mutations and no Save/Gold writes.
/// Observe the original 7C.1/7C.2 core, 7C.3.3 UI and 7C.4.2 truth resolver.
/// </summary>
public sealed class MS7C5FinalSynergyQA : EditorWindow
{
    private const string Menu = "Tools/MonsterSurvival/7C.5/";
    private const int MaxHistory = 600;
    private static readonly string[] ManualLabels =
    {
        "J/L mo cung MOT bang va hien du 6 the / 6 icon",
        "Khong con 2 nut SYNERGY / NEW COMBOS cu de len HUD",
        "3 lua chon level-up: guide hien dung combo va khong chan click",
        "Khi hoc du 2 skill, bang J/L cap nhat dung unlock",
        "Fire + Lightning: test Lightning chain va Wildfire burn tren muc tieu phu",
        "Ice + Ricochet: test slow duoc tang 1 lan / hit",
        "Lightning + Critical: test crit chain va Thunder Precision",
        "Victory / Game Over / Pause / Retry / Profile / Gold / Save van dung",
        "Shadow Titan runtime HP dung moc can bang 500 (kiem tra agent multiplier)"
    };

    private readonly bool[] manual = new bool[9];
    private readonly bool[] everUnlocked = new bool[6];
    private readonly bool[] everReady = new bool[6];
    private readonly int[] maxProcs = new int[3];
    private readonly List<string> history = new List<string>();
    private Vector2 scroll;
    private bool capturing;
    private bool hasRuntimeSample;
    private bool hookAuditCompleted;
    private bool allHooksPresent;
    private string hookDetails = "NOT AUDITED";
    private string structuralDetails = "NOT AUDITED";
    private string lastSignature;
    private double nextSample;
    private int sampleCount;
    private int changes;
    private int lastPass, lastWarn, lastError;
    private string sessionScene = "";

    [MenuItem(Menu + "1 - Open Final Synergy QA Dashboard")]
    public static void OpenDashboard()
    {
        GetWindow<MS7C5FinalSynergyQA>("MS 7C.5 Final QA").minSize = new Vector2(620, 560);
    }

    [MenuItem(Menu + "2 - Run Read-only Scene + Hook Audit")]
    public static void MenuAudit()
    {
        MS7C5FinalSynergyQA window = GetWindow<MS7C5FinalSynergyQA>("MS 7C.5 Final QA");
        window.RunAudit();
    }

    [MenuItem(Menu + "3 - Export QA Report as TXT")]
    public static void MenuExport()
    {
        MS7C5FinalSynergyQA window = GetWindow<MS7C5FinalSynergyQA>("MS 7C.5 Final QA");
        window.ExportReport();
    }

    [MenuItem(Menu + "4 - Run Pure Unlock Formula Checks")]
    public static void MenuPure()
    {
        int count = 0;
        count += Pure(!MS7C1SynergyCore.HasWildfire(1, 0), "Wildfire needs both");
        count += Pure(MS7C1SynergyCore.HasWildfire(1, 1), "Wildfire Fire+Lightning");
        count += Pure(!MS7C1SynergyCore.HasGlacial(1, false), "Glacial needs Ricochet");
        count += Pure(MS7C1SynergyCore.HasGlacial(1, true), "Glacial Ice+Ricochet");
        count += Pure(!MS7C1SynergyCore.HasThunder(0, true), "Thunder needs Lightning");
        count += Pure(MS7C1SynergyCore.HasThunder(1, true), "Thunder Lightning+Crit");
        Debug.Log("[7C.5 PURE QA] " + count + "/6 PASSED. CONDITIONS ONLY. This does not test hit/procs, UI, Save, or Boss.");
    }

    private static int Pure(bool ok, string label)
    {
        if (ok) Debug.Log("[7C.5 PURE QA] PASS " + label);
        else Debug.LogError("[7C.5 PURE QA] FAIL " + label);
        return ok ? 1 : 0;
    }

    private void OnEnable()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayState;
    }

    private void OnDisable()
    {
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= OnPlayState;
    }

    private void OnPlayState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
        {
            capturing = false;
            lastSignature = null;
        }
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("7C.5 | FINAL SYNERGY RELEASE GATE", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("READ-ONLY. This dashboard observes six combos and the four combat hook sites. It never awards skills, sets READY, changes Scene/Prefab/Save/Gold, or changes Boss HP.", MessageType.Info);
        EditorGUILayout.LabelField("Scene", SceneManager.GetActiveScene().name);
        EditorGUILayout.LabelField("Play Mode", EditorApplication.isPlaying ? "YES" : "NO");
        EditorGUILayout.LabelField("Last scene audit", "PASS=" + lastPass + " WARN=" + lastWarn + " ERROR=" + lastError);
        EditorGUILayout.LabelField("7C.2 hook audit", hookDetails);
        if (GUILayout.Button("1) AUDIT SCENE + 4 HOOK SITES (READ ONLY)", GUILayout.Height(33))) RunAudit();
        if (GUILayout.Button("2) PURE UNLOCK TESTS 6/6 (CONDITIONS ONLY)", GUILayout.Height(28))) MenuPure();
        GUILayout.Space(9);
        EditorGUILayout.LabelField("PLAY MODE CAPTURE", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = EditorApplication.isPlaying && !capturing;
        if (GUILayout.Button("START LIVE CAPTURE", GUILayout.Height(35))) StartCapture();
        GUI.enabled = capturing;
        if (GUILayout.Button("STOP CAPTURE", GUILayout.Height(35))) StopCapture();
        GUI.enabled = true;
        if (GUILayout.Button("RESET CAPTURE", GUILayout.Height(35))) ResetCapture();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("Capture", capturing ? "RECORDING" : "STOPPED");
        EditorGUILayout.LabelField("Samples / changes", sampleCount + " / " + changes);
        EditorGUILayout.LabelField("Max observed 7C.2 procs", "Wildfire=" + maxProcs[0] + " | Glacial=" + maxProcs[1] + " | Thunder=" + maxProcs[2]);
        GUILayout.Space(9);
        EditorGUILayout.LabelField("MANUAL QA - user observations (not automatic passes)", EditorStyles.boldLabel);
        for (int i = 0; i < manual.Length; ++i)
            manual[i] = EditorGUILayout.ToggleLeft((i + 1) + ". " + ManualLabels[i], manual[i]);
        EditorGUILayout.HelpBox("The nine manual checkboxes are NOT inferred from Editor Audit. Confirm them only after testing. If you cannot verify one, leave it unchecked.", MessageType.Warning);
        GUILayout.Space(8);
        if (GUILayout.Button("3) EXPORT FULL TXT REPORT", GUILayout.Height(36))) ExportReport();
        if (history.Count > 0)
        {
            EditorGUILayout.LabelField("Recent observed changes (last 8)", EditorStyles.boldLabel);
            int start = Mathf.Max(0, history.Count - 8);
            for (int i = start; i < history.Count; i++) EditorGUILayout.SelectableLabel(history[i], GUILayout.Height(30));
        }
        EditorGUILayout.EndScrollView();
    }

    private static T FindInActiveScene<T>() where T : Component
    {
        Scene scene = SceneManager.GetActiveScene();
        foreach (T item in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (item != null && item.gameObject.scene == scene) return item;
        }
        return null;
    }

    private static int CountInActiveScene<T>() where T : Component
    {
        Scene scene = SceneManager.GetActiveScene();
        int count = 0;
        foreach (T item in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (item != null && item.gameObject.scene == scene) count++;
        return count;
    }

    private void RunAudit()
    {
        StringBuilder result = new StringBuilder();
        lastPass = lastWarn = lastError = 0;
        SkillManager manager = FindInActiveScene<SkillManager>();
        PlayerStats player = FindInActiveScene<PlayerStats>();
        MS7C3UnifiedSynergyUI ui = FindInActiveScene<MS7C3UnifiedSynergyUI>();
        MS7C33LevelUpGuide guide = FindInActiveScene<MS7C33LevelUpGuide>();
        MS7C4SynergyObserver observer = FindInActiveScene<MS7C4SynergyObserver>();
        MS7C1SynergyCore core = player != null ? player.GetComponent<MS7C1SynergyCore>() : null;
        MS7C2SynergyCombat combat = core != null ? core.GetComponent<MS7C2SynergyCombat>() : null;
        MS7B2ElementalSkillCoordinator coordinator = manager != null ? manager.GetComponent<MS7B2ElementalSkillCoordinator>() : null;
        CheckCount(result, "SkillManager exactly one", CountInActiveScene<SkillManager>(), 1);
        CheckCount(result, "PlayerStats exactly one", CountInActiveScene<PlayerStats>(), 1);
        CheckCount(result, "Unified UI exactly one", CountInActiveScene<MS7C3UnifiedSynergyUI>(), 1);
        CheckCount(result, "Choice Guide exactly one", CountInActiveScene<MS7C33LevelUpGuide>(), 1);
        CheckCount(result, "7C.4 Observer exactly one", CountInActiveScene<MS7C4SynergyObserver>(), 1);
        CheckCount(result, "7C.1 Core exactly one", CountInActiveScene<MS7C1SynergyCore>(), 1);
        CheckCount(result, "7C.2 Combat exactly one", CountInActiveScene<MS7C2SynergyCombat>(), 1);
        Check(result, "7C.1 core on same PlayerStats object", player != null && core != null && core.isActiveAndEnabled);
        Check(result, "7C.2 controller linked to THIS core", combat != null && combat.isActiveAndEnabled && combat.core == core);
        Check(result, "7B.2 Coordinator on SkillManager", coordinator != null);
        Check(result, "Fire, Ice and Lightning have distinct ScriptableObject references", coordinator != null &&
            coordinator.fireSkill != null && coordinator.iceSkill != null && coordinator.lightningSkill != null &&
            coordinator.fireSkill != coordinator.iceSkill && coordinator.fireSkill != coordinator.lightningSkill &&
            coordinator.iceSkill != coordinator.lightningSkill);
        Check(result, "Elemental SkillData registered in allSkills", manager != null && manager.allSkills != null && coordinator != null &&
            manager.allSkills.Contains(coordinator.fireSkill) && manager.allSkills.Contains(coordinator.iceSkill) &&
            manager.allSkills.Contains(coordinator.lightningSkill));
        Check(result, "Three choice buttons configured", manager != null && manager.skillPanel != null && manager.skillButtons != null &&
            manager.skillButtons.Length >= 3 && manager.skillNames != null && manager.skillNames.Length >= 3);
        Check(result, "Six combo definitions", MS7C33SynergyRules.Combos != null && MS7C33SynergyRules.Combos.Length == 6);
        if (manager != null && MS7C33SynergyRules.Combos != null)
        {
            for (int i = 0; i < MS7C33SynergyRules.Combos.Length; ++i)
            {
                var c = MS7C33SynergyRules.Combos[i];
                bool found1 = FindCanonicalSkill(manager, c.First) != null;
                bool found2 = FindCanonicalSkill(manager, c.Second) != null;
                Check(result, c.Title + " SkillData refs", found1 && found2);
            }
        }
        Check(result, "Observer references valid", observer != null && observer.EditorReferencesValid);
        if (ui != null && !ui.isActiveAndEnabled) Warn(result, "Unified UI component disabled; J/L cannot open");
        if (guide != null && !guide.isActiveAndEnabled) Warn(result, "Level-up guide disabled");
        AuditHooks(result);
        structuralDetails = result.ToString();
        string header = "[7C.5 EDITOR AUDIT] PASS=" + lastPass + " WARN=" + lastWarn + " ERROR=" + lastError;
        if (lastError > 0) Debug.LogError(header + "\n" + structuralDetails);
        else if (lastWarn > 0) Debug.LogWarning(header + "\n" + structuralDetails);
        else Debug.Log(header + "\n" + structuralDetails);
        Repaint();
    }

    private static SkillData FindCanonicalSkill(SkillManager manager, string canonical)
    {
        if (manager == null || manager.allSkills == null) return null;
        SkillData element = MS7C33SynergyRules.ElementalAsset(manager, canonical);
        if (element != null) return element;
        foreach (SkillData skill in manager.allSkills)
            if (skill != null && MS7C33SynergyRules.Matches(skill, canonical)) return skill;
        return null;
    }

    private void Check(StringBuilder sb, string label, bool ok)
    {
        if (ok) { lastPass++; sb.AppendLine("[PASS] " + label); }
        else { lastError++; sb.AppendLine("[ERROR] " + label); }
    }

    private void CheckCount(StringBuilder sb, string label, int found, int expected)
    {
        Check(sb, label + " (found=" + found + ")", found == expected);
    }

    private void Warn(StringBuilder sb, string text)
    {
        lastWarn++;
        sb.AppendLine("[WARN] " + text);
    }

    private static int Occurrences(string text, string token)
    {
        int count = 0, start = 0;
        while (true)
        {
            int index = text.IndexOf(token, start, StringComparison.Ordinal);
            if (index < 0) return count;
            count++;
            start = index + token.Length;
        }
    }

    private static string ReadExactAssetScript(string name, out string note)
    {
        string path = null;
        string[] guids = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(name) + " t:MonoScript");
        foreach (string guid in guids)
        {
            string candidate = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileName(candidate) != name) continue;
            if (path != null)
            {
                note = "DUPLICATE " + name + " at " + path + " and " + candidate;
                return null;
            }
            path = candidate;
        }
        if (path == null) { note = "MISSING " + name; return null; }
        string full = Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path));
        if (!File.Exists(full)) { note = "FILE NOT FOUND " + full; return null; }
        note = path;
        return File.ReadAllText(full);
    }

    private void AuditHooks(StringBuilder sb)
    {
        string bulletNote, elementNote;
        string bullet = ReadExactAssetScript("Bullet.cs", out bulletNote);
        string elemental = ReadExactAssetScript("MS7B1ElementalProjectile.cs", out elementNote);
        hookAuditCompleted = true;
        allHooksPresent = false;
        if (bullet == null || elemental == null)
        {
            hookDetails = "CHECK source files / duplicates";
            Check(sb, "Source Bullet.cs / MS7B1ElementalProjectile.cs uniquely found (" + bulletNote + "; " + elementNote + ")", false);
            return;
        }
        string[] tokens =
        {
            "MS7C2CombatBridge.RecordCritical(",
            "MS7C2CombatBridge.GlacialSlow(",
            "MS7C2CombatBridge.AmplifyLightningChain(",
            "MS7C2CombatBridge.AfterLightningChainHit("
        };
        int[] count =
        {
            Occurrences(bullet, tokens[0]),
            Occurrences(elemental, tokens[1]),
            Occurrences(elemental, tokens[2]),
            Occurrences(elemental, tokens[3])
        };
        int okay = 0;
        for (int i = 0; i < count.Length; i++)
        {
            bool valid = count[i] == 1;
            if (valid) okay++;
            Check(sb, "7C.2 hook #" + (i + 1) + " exactly once (found=" + count[i] + ")", valid);
        }
        allHooksPresent = okay == 4;
        hookDetails = okay + "/4 source hooks";
        sb.AppendLine("[INFO] Bullet source: " + bulletNote + " | Elemental source: " + elementNote);
        sb.AppendLine("[INFO] Hook source presence does NOT prove damage, chance, VFX or runtime proc.");
    }

    private void StartCapture()
    {
        if (!EditorApplication.isPlaying)
        { Debug.LogWarning("[7C.5] Enter Play Mode before capture."); return; }
        ResetCapture();
        capturing = true;
        sessionScene = SceneManager.GetActiveScene().name;
        nextSample = 0;
        LogEvent("START capture in " + sessionScene);
    }

    private void StopCapture()
    {
        if (!capturing) return;
        capturing = false;
        LogEvent("STOP capture; samples=" + sampleCount + " changes=" + changes);
    }

    private void ResetCapture()
    {
        capturing = false;
        hasRuntimeSample = false;
        lastSignature = null;
        sampleCount = 0;
        changes = 0;
        history.Clear();
        Array.Clear(everUnlocked, 0, everUnlocked.Length);
        Array.Clear(everReady, 0, everReady.Length);
        Array.Clear(maxProcs, 0, maxProcs.Length);
    }

    private void Tick()
    {
        if (!capturing || !EditorApplication.isPlaying) return;
        double now = EditorApplication.timeSinceStartup;
        if (now < nextSample) return;
        nextSample = now + 0.25d;
        SkillManager manager = FindInActiveScene<SkillManager>();
        PlayerStats stats = FindInActiveScene<PlayerStats>();
        if (manager == null || stats == null) return;
        sampleCount++;
        hasRuntimeSample = true;
        StringBuilder signature = new StringBuilder();
        signature.Append(SceneManager.GetActiveScene().name);
        bool selecting = manager.skillPanel != null && manager.skillPanel.activeInHierarchy;
        signature.Append(" | LEVEL_UP=").Append(selecting);
        if (selecting)
            for (int i = 0; i < 3; i++)
            {
                SkillData option = MS7C33SynergyRules.GetOffered(manager, i);
                signature.Append(" | ").Append(i + 1).Append(":").Append(option != null ? option.name : "none");
            }
        for (int i = 0; i < 6; i++)
        {
            int progress = MS7C33SynergyRules.Progress(manager, i);
            bool unlocked;
            bool ready = false;
            int procs = 0;
            if (i < 3) unlocked = MS7C33SynergyRules.IsActualLegacyUnlocked(stats, i);
            else
            {
                MS7C42SynergyStatus.Snapshot s = MS7C42SynergyStatus.Read(manager, stats, i);
                unlocked = s.coreUnlocked;
                ready = s.ready;
                procs = s.procs;
                maxProcs[i - 3] = Mathf.Max(maxProcs[i - 3], procs);
            }
            everUnlocked[i] |= unlocked;
            everReady[i] |= ready;
            signature.Append(" | ").Append(i + 1).Append(":").Append(progress).Append("/2").Append(unlocked ? "U" : "-")
                .Append(ready ? "R" : "-").Append("p").Append(procs);
        }
        string current = signature.ToString();
        if (!string.Equals(lastSignature, current, StringComparison.Ordinal))
        {
            if (lastSignature != null) changes++;
            lastSignature = current;
            LogEvent(current);
            Repaint();
        }
    }

    private void LogEvent(string text)
    {
        string line = DateTime.Now.ToString("HH:mm:ss") + " | " + text;
        history.Add(line);
        if (history.Count > MaxHistory) history.RemoveAt(0);
    }

    private void ExportReport()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 7C.5 - FINAL SYNERGY QA / READ ONLY");
        sb.AppendLine("Generated=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | Unity=" + Application.unityVersion);
        sb.AppendLine("Scene=" + SceneManager.GetActiveScene().name + " | PlayMode=" + EditorApplication.isPlaying);
        sb.AppendLine("This report does not prove unobserved gameplay. READY is not an actual proc.");
        sb.AppendLine();
        sb.AppendLine("=== EDITOR/STRUCTURAL AUDIT ===");
        sb.AppendLine("PASS=" + lastPass + " WARN=" + lastWarn + " ERROR=" + lastError + " | HOOKS=" + hookDetails);
        sb.AppendLine(structuralDetails);
        sb.AppendLine("=== LIVE CAPTURE ===");
        sb.AppendLine("Capture scene=" + sessionScene + " | Samples=" + sampleCount + " | Changes=" + changes + " | HasRuntimeSample=" + hasRuntimeSample);
        for (int i = 0; i < 6; i++)
            sb.AppendLine((i + 1) + ". " + MS7C33SynergyRules.Combos[i].Title + " | observedUnlocked=" + everUnlocked[i] + " | observedReady=" + everReady[i] +
                (i >= 3 ? " | maxObservedProcs=" + maxProcs[i - 3] : " | legacy has no 7C2 proc counter"));
        sb.AppendLine("=== MANUAL TEST RESULTS (USER CONFIRMED) ===");
        int done = 0;
        for (int i = 0; i < manual.Length; i++)
        {
            sb.AppendLine((manual[i] ? "[USER CONFIRMED] " : "[NOT CONFIRMED] ") + (i + 1) + ". " + ManualLabels[i]);
            if (manual[i]) done++;
        }
        sb.AppendLine("Manual confirmed=" + done + "/" + manual.Length);
        sb.AppendLine("=== RELEASE GATE ===");
        bool autoGate = lastError == 0 && lastWarn == 0 && hookAuditCompleted && allHooksPresent && hasRuntimeSample;
        bool procsGate = maxProcs[0] > 0 && maxProcs[1] > 0 && maxProcs[2] > 0;
        bool sixCombosGate = true;
        for (int i = 0; i < everUnlocked.Length; i++) if (!everUnlocked[i]) sixCombosGate = false;
        bool manualGate = done == manual.Length;
        sb.AppendLine("STRUCTURE_AND_LIVE_SAMPLE=" + (autoGate ? "PASS" : "NOT CONFIRMED"));
        sb.AppendLine("ALL_6_COMBOS_UNLOCK_OBSERVED=" + (sixCombosGate ? "PASS" : "NOT CONFIRMED"));
        sb.AppendLine("ALL_3_ELEMENTAL_PROCS_OBSERVED=" + (procsGate ? "PASS" : "NOT CONFIRMED"));
        sb.AppendLine("ALL_MANUAL_ITEMS_CONFIRMED=" + (manualGate ? "PASS" : "NOT CONFIRMED"));
        sb.AppendLine("RESULT=" + ((autoGate && sixCombosGate && procsGate && manualGate) ? "GATE CRITERIA MET (user confirmation still requires review)" : "PENDING - DO NOT MARK FINAL QA COMPLETE"));
        sb.AppendLine("=== OBSERVED EVENT TRACE ===");
        foreach (string line in history) sb.AppendLine(line);
        string path = EditorUtility.SaveFilePanel("Save MonsterSurvival 7C.5 QA Report", Application.dataPath,
            "MS7C5_FinalQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"), "txt");
        if (string.IsNullOrEmpty(path)) return;
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        Debug.Log("[7C.5] QA report exported: " + path);
    }
}
#endif
