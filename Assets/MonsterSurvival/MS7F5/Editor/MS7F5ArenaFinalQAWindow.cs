#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 7F.5 release gate: read-only checks/reporting, and one explicit reversible recorder install.
// All time-consuming Editor actions are deferred OUTSIDE OnGUI to prevent IMGUI layout errors.
public sealed class MS7F5ArenaFinalQAWindow : EditorWindow
{
    private static readonly string[] manual =
    {
        "01  Green Meadow: select from MainMenu, actual scene map matches, Wave2/3/4 playable",
        "02  Sand Dunes: select from MainMenu; auto ID/profile and Wave2/3/4 correct",
        "03  Ancient Ruins: select from MainMenu; auto ID/profile and Wave2/3/4 correct",
        "04  Twin Strike two visible circles; only one hit attempt in overlapping area",
        "05  Crossfire lanes align with collision geometry and can be dodged",
        "06  Hollow Ring center visibly safe; inner/outer boundaries are readable",
        "07  VFX: warning -> glow/charge -> impact; no extra damage introduced by 7F.3",
        "08  Pause during a live hazard: countdown, VFX and Waves freeze; resume works",
        "09  Turn 7F.2 OFF during Play, verify original 7F.1 flag restores; turn 7F.2 ON again",
        "10  After each wave and on Boss Wave: all 11 7F.4 settings restored",
        "11  Wave 5 boss remains unchanged: Shadow Titan base 500 HP and head-follow bar",
        "12  Elite 7E / Healer / enemy caps / safe spawn / Dynamic Waves still work",
        "13  EXP / Gold / shield / skill 7C / equipment sets 7D / 5B upgrades unaffected",
        "14  Victory banks gold; defeat/retry does not; Save/Profile slots remain independent",
        "15  1920x1080, 1280x720, 1024x768: minimal HUD and arena warning legible"
    };
    private static string PrefKey { get { return "MS7F5_ARENA_MANUAL_" + Application.dataPath.Replace('/', '_').Replace('\\', '_') + "_"; } }
    private bool[] checkedManual = new bool[15];
    private bool queued;
    private Vector2 scroll;
    private string output = "Open SampleScene, run AUDIT, then install the read-only recorder.";

    [MenuItem("Tools/MonsterSurvival/7F Arena Events/19 - Open 7F.5 Final Arena QA")]
    public static void Open()
    {
        MS7F5ArenaFinalQAWindow w = GetWindow<MS7F5ArenaFinalQAWindow>(false, "7F.5 Final Arena QA");
        w.minSize = new Vector2(680, 580);
        w.Show();
    }
    private void OnEnable()
    {
        checkedManual = new bool[manual.Length];
        for (int i = 0; i < manual.Length; i++) checkedManual[i] = EditorPrefs.GetBool(PrefKey + i, false);
    }
    private void Defer(Action action)
    {
        if (queued || action == null) return;
        queued = true;
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            try { action(); }
            catch (Exception e) { output = "[EXCEPTION] " + e; Debug.LogException(e); }
            finally { queued = false; if (this != null) Repaint(); }
        };
    }
    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        try
        {
            GUILayout.Label("7F.5 - FINAL ARENA RELEASE GATE (Unity 6.6)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "READ-ONLY runtime observer for 7F.1/2/3/4/4.1; not a gameplay patch.\n" +
                "Each run covers ONE selected map. Export inside SampleScene Play Mode, then repeat for 3 maps.\n" +
                "Never mark 7F complete solely from event counters or a previous 7E report.", MessageType.Info);
            using (new EditorGUI.DisabledScope(queued))
            {
                if (GUILayout.Button("1 - PREFLIGHT / STRUCTURE AUDIT (READ ONLY)", GUILayout.Height(32)))
                    Defer(() => { output = BuildReport(false); Debug.Log(output); });
                if (GUILayout.Button("2 - BACKUP + INSTALL / REPAIR READ-ONLY RECORDER", GUILayout.Height(33)))
                    Defer(Install);
                if (GUILayout.Button("3 - PURE RULE TESTS (NO SCENE CHANGES)", GUILayout.Height(31)))
                    Defer(() => { output = PureTests(); Debug.Log(output); });
                if (GUILayout.Button("4 - EXPORT THIS MAP QA TXT (WHILE PLAYING)", GUILayout.Height(34)))
                    Defer(Export);
                if (GUILayout.Button("5 - AGGREGATE THREE MAP QA REPORTS", GUILayout.Height(32)))
                    Defer(Aggregate);
            }
            EditorGUILayout.HelpBox(
                "Optional 7F.1 restore experiment: while in Play Mode, turn MS7F2.enableMultipleEvents OFF, wait one frame, check 7F.1 flag + Legacy7F1Restores, then ON. Do not save Play Mode Inspector changes.\n" +
                "Map ID and 7F.4 balance must be enabled; Boss Wave must remain excluded.", MessageType.Warning);
            GUILayout.Space(8);
            GUILayout.Label("MANUAL QA - check ONLY after you personally confirmed it", EditorStyles.boldLabel);
            for (int i = 0; i < manual.Length; i++)
            {
                bool v = EditorGUILayout.ToggleLeft(manual[i], checkedManual[i]);
                if (v == checkedManual[i]) continue;
                checkedManual[i] = v;
                EditorPrefs.SetBool(PrefKey + i, v);
            }
            GUILayout.Label("Manual verified: " + checkedManual.Count(x => x) + "/" + manual.Length);
            if (GUILayout.Button("Reset manual confirmations (requires confirmation)")) Defer(ResetManual);
            GUILayout.Space(6);
            GUILayout.Label("Latest report", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(output, GUILayout.MinHeight(260));
        }
        finally { EditorGUILayout.EndScrollView(); }
    }

    private static WaveManager FindWave(out string reason)
    {
        int count = 0; WaveManager found = null;
        Scene current = SceneManager.GetActiveScene();
        foreach (WaveManager w in UnityEngine.Object.FindObjectsByType<WaveManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (w == null || w.gameObject.scene != current) continue;
            count++; found = w;
        }
        reason = count == 1 ? "Exactly one WaveManager" : "Expected ONE WaveManager in active Scene; found " + count;
        return count == 1 ? found : null;
    }
    private static string ReportFolder()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.Combine(root, "MS_QAReports", "7F5");
    }
    private void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorUtility.DisplayDialog("7F.5", "Stop Play Mode before installing the observer.", "OK"); return; }
        string reason; WaveManager wave = FindWave(out reason);
        if (wave == null) { EditorUtility.DisplayDialog("7F.5", reason + ". Open SampleScene first.", "OK"); return; }
        if (wave.GetComponent<MS7F2ArenaEventDirector>() == null ||
            wave.GetComponent<MS7F4MapBalanceDirector>() == null)
        { EditorUtility.DisplayDialog("7F.5", "Existing 7F.2 / 7F.4 missing; repair prerequisite before installing recorder.", "OK"); return; }
        Scene scene = wave.gameObject.scene;
        if (string.IsNullOrEmpty(scene.path)) { EditorUtility.DisplayDialog("7F.5", "Save SampleScene first.", "OK"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!EditorSceneManager.SaveScene(scene)) return;
        if (!EditorUtility.DisplayDialog("7F.5 - READ ONLY INSTALL", "Back up current gameplay Scene outside Assets and add/repair ONE 7F.5 observer on existing WaveManager? No gameplay code or prefabs will be replaced.", "Install", "Cancel")) return;
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string backupFolder = Path.Combine(root, "MS_Backups", "7F5");
        Directory.CreateDirectory(backupFolder);
        string source = Path.GetFullPath(Path.Combine(root, scene.path));
        string backupPath = Path.Combine(backupFolder, Path.GetFileNameWithoutExtension(scene.path) + "_BEFORE_7F5_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity");
        File.Copy(source, backupPath, false);
        MS7F5ArenaFinalRecorder rec = wave.GetComponent<MS7F5ArenaFinalRecorder>();
        if (rec == null) rec = Undo.AddComponent<MS7F5ArenaFinalRecorder>(wave.gameObject);
        Undo.RecordObject(rec, "7F.5 observer references");
        rec.waveManager = wave;
        rec.mapSwitcher = wave.GetComponent<WaveMapSwitcher>();
        rec.legacy7F1 = wave.GetComponent<MS7F1ArenaHazardDirector>();
        rec.arena7F2 = wave.GetComponent<MS7F2ArenaEventDirector>();
        rec.arena7F3 = wave.GetComponent<MS7F3ArenaVFXDirector>();
        rec.mapBalance7F4 = wave.GetComponent<MS7F4MapBalanceDirector>();
        rec.mapBinder7F41 = wave.GetComponent<MS7F41AutoMapIdBinder>();
        rec.enableObservation = true;
        EditorUtility.SetDirty(rec);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        output = "[INSTALL] READ ONLY observer attached to existing WaveManager.\nBackup=" + backupPath + "\n" + BuildReport(false);
        Debug.Log(output);
        EditorUtility.DisplayDialog("7F.5 Installed", "Observer ready. Enter MainMenu -> select map -> SampleScene. Export in Play Mode for each map.", "OK");
    }
    private static void Check(StringBuilder b, ref int pass, ref int fail, bool condition, string label)
    {
        if (condition) { pass++; b.AppendLine("[PASS] " + label); }
        else { fail++; b.AppendLine("[FAIL] " + label); }
    }
    private static string PureTests()
    {
        int pass = 0, fail = 0; StringBuilder b = new StringBuilder("7F.5 PURE RULE QA - NO SCENE MODIFICATION\n");
        Check(b, ref pass, ref fail, MS7F2ArenaEventDirector.ForWave(2,2) == MS7F2ArenaEvent.EventKind.TwinStrike, "Wave2 Twin Strike");
        Check(b, ref pass, ref fail, MS7F2ArenaEventDirector.ForWave(3,2) == MS7F2ArenaEvent.EventKind.Crossfire, "Wave3 Crossfire");
        Check(b, ref pass, ref fail, MS7F2ArenaEventDirector.ForWave(4,2) == MS7F2ArenaEvent.EventKind.HollowRing, "Wave4 Hollow Ring");
        Check(b, ref pass, ref fail, !MS7F2ArenaEventDirector.Eligible(true,5,2,5,5,true,false,false), "7F.2 excludes Boss 5");
        Check(b, ref pass, ref fail, !MS7F2ArenaEventDirector.Eligible(false,2,2,5,5,true,false,false), "7F.2 opt out");
        Check(b, ref pass, ref fail, !MS7F2ArenaEventDirector.Eligible(true,3,2,5,5,false,false,false), "7F.2 break excluded");
        Check(b, ref pass, ref fail, !MS7F2ArenaEventDirector.Eligible(true,3,2,5,5,true,false,true), "7F.2 paused excluded");
        Check(b, ref pass, ref fail, !MS7F4MapBalanceDirector.WaveEligible(true,true,5,2,5,5,true,false), "7F.4 excludes Boss 5");
        Check(b, ref pass, ref fail, !MS7F1ArenaHazardDirector.IsEligible(true,5,2,5,5,true,true,false,false), "7F.1 boss exclusion");
        Check(b, ref pass, ref fail, MS7F4MapBalanceDirector.SafeDamage(6f,2f,8f) <= 8f, "7F.4 damage cap");
        Check(b, ref pass, ref fail, MS7F4MapBalanceDirector.SafeWarning(0.5f,0.65f,0.9f) >= 0.9f, "7F.4 warning floor");
        Check(b, ref pass, ref fail, MS7F4MapBalanceDirector.SameMap("Green Meadow", " green meadow "), "Map ID normalization");
        b.AppendLine("PURE PASS=" + pass + " FAIL=" + fail);
        b.AppendLine("NOTE: Pure tests cannot verify Game View, Save/Profile, Physics, builds or all Unity runtime behaviors.");
        return b.ToString();
    }

    private string BuildReport(bool exportMode)
    {
        StringBuilder b = new StringBuilder();
        b.AppendLine("MONSTERSURVIVAL 7F.5 — FINAL ARENA RELEASE GATE / READ ONLY");
        b.AppendLine("Generated=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " Unity=" + Application.unityVersion +
            " Scene=" + SceneManager.GetActiveScene().name + " PlayMode=" + EditorApplication.isPlaying);
        b.AppendLine("No gameplay, Save/Profile or asset mutation during this audit.");
        string issue; WaveManager wave = FindWave(out issue);
        if (wave == null)
        {
            b.AppendLine("[PENDING] " + issue + ". Open SampleScene (Play Mode) for runtime QA, not MainMenu.");
            b.AppendLine("STRUCTURE=PENDING RUNTIME=PENDING RESULT=PENDING");
            return b.ToString();
        }
        var legacy = wave.GetComponent<MS7F1ArenaHazardDirector>();
        var arena = wave.GetComponent<MS7F2ArenaEventDirector>();
        var vfx = wave.GetComponent<MS7F3ArenaVFXDirector>();
        var balance = wave.GetComponent<MS7F4MapBalanceDirector>();
        var binder = wave.GetComponent<MS7F41AutoMapIdBinder>();
        var switcher = wave.GetComponent<WaveMapSwitcher>();
        var recorder = wave.GetComponent<MS7F5ArenaFinalRecorder>();
        int pass = 0, fail = 0;
        b.AppendLine("=== STRUCTURAL PREFLIGHT ===");
        Check(b,ref pass,ref fail, wave.enemySpawner != null, "Original WaveManager.enemySpawner present");
        Check(b,ref pass,ref fail, legacy != null && legacy.waveManager == wave && legacy.enemySpawner == wave.enemySpawner, "7F.1 exists with matching references");
        Check(b,ref pass,ref fail, arena != null && arena.waveManager == wave && arena.enemySpawner == wave.enemySpawner, "7F.2 exists with matching references");
        Check(b,ref pass,ref fail, arena != null && arena.legacy7F1 == legacy && arena.suspend7F1WhileActive, "7F.2 legacy 7F.1 suspension connected");
        Check(b,ref pass,ref fail, vfx != null && vfx.arenaEvents == arena, "7F.3 VFX source is exact 7F.2");
        Check(b,ref pass,ref fail, balance != null && balance.waveManager == wave && balance.arena7F2 == arena, "7F.4 references original Wave and 7F.2");
        Check(b,ref pass,ref fail, binder != null && binder.mapSwitcher == switcher && binder.mapBalance == balance, "7F.4.1 uses actual WaveMapSwitcher and 7F.4");
        Check(b,ref pass,ref fail, recorder != null && recorder.waveManager == wave && recorder.arena7F2 == arena && recorder.arena7F3 == vfx && recorder.mapBalance7F4 == balance, "7F.5 recorder attached once with original references");
        Check(b,ref pass,ref fail, arena != null && wave.bossWave == 5 && arena.firstAffectedWave == 2 && MS7F2ArenaEventDirector.ForWave(4,2) == MS7F2ArenaEvent.EventKind.HollowRing, "Wave2-4 mapping and Boss5 known");
        Check(b,ref pass,ref fail, arena != null && arena.warningSeconds >= 0.9f && arena.impactDamage <= 8f && arena.maxEventsPerWave <= 2, "Current 7F.2 warning/damage/event-count within safety caps");
        Check(b,ref pass,ref fail, balance != null && balance.minimumWarningSeconds >= 0.9f && balance.maximumImpactDamage <= 8f && balance.maximumEventsPerWave <= 1, "7F.4 safety caps maintained");
        int mapCount = switcher != null && switcher.maps != null ? switcher.maps.Count : 0;
        Check(b,ref pass,ref fail, mapCount >= 3, "Three or more maps configured in WaveMapSwitcher");
        if (switcher != null && binder != null && balance != null && switcher.maps != null)
        {
            for (int i = 0; i < switcher.maps.Count; ++i)
            {
                string mapId = MS7F41AutoMapIdBinder.GetIdForIndex(switcher,i);
                bool matching = MS7F41AutoMapIdBinder.HasEnabledProfile(balance,mapId);
                bool allWaves = false;
                if (matching && balance.mapProfiles != null)
                {
                    foreach (var profile in balance.mapProfiles)
                    {
                        if (profile == null || !profile.enabled || !MS7F4MapBalanceDirector.SameMap(profile.mapId,mapId) || profile.waves == null) continue;
                        allWaves = profile.waves.Any(x => x != null && x.wave == 2) &&
                                   profile.waves.Any(x => x != null && x.wave == 3) &&
                                   profile.waves.Any(x => x != null && x.wave == 4);
                        if (allWaves) break;
                    }
                }
                Check(b,ref pass,ref fail, matching && allWaves, "Map["+i+"] profile covers Waves2/3/4: '"+mapId+"'");
            }
        }
        b.AppendLine("STRUCTURAL PASS=" + pass + " FAIL=" + fail);
        b.AppendLine("[CONFIG] 7F2Enabled=" + (arena!=null&&arena.enableMultipleEvents) +
            " 7F3Enabled=" + (vfx!=null&&vfx.enableArenaVFX) + " 7F4Enabled=" + (balance!=null&&balance.enableMapBalancing) +
            " AutoIDEnabled=" + (binder!=null&&binder.enableAutoBinding));
        b.AppendLine("=== MAP / RUNTIME EVIDENCE ===");
        string map = "";
        if (binder != null && binder.TryGetActualMap(out int index, out string actualId)) map = actualId;
        b.AppendLine("[MAP_ID]='" + map + "'");
        if (!EditorApplication.isPlaying || recorder == null || recorder.Samples < 1)
        {
            b.AppendLine("[PENDING] Recorder requires active SampleScene Play Mode with >0 samples. Do not export from MainMenu or after stopping.");
            b.AppendLine("[SESSION_GATE] PENDING");
        }
        else
        {
            b.AppendLine(recorder.GetRuntimeReport());
            if (arena != null) b.AppendLine(arena.GetRuntimeReport());
            if (vfx != null) b.AppendLine(vfx.GetRuntimeReport());
            if (balance != null) b.AppendLine(balance.GetRuntimeReport());
            if (binder != null) b.AppendLine(binder.GetReport());
            bool eventOk = recorder.SawTwinAt2 && recorder.SawCrossfireAt3 && recorder.SawRingAt4;
            bool vfxOk = vfx != null && recorder.SawAllThreeVFX && vfx.ImpactVisuals >= 3 && vfx.VisualsRemoved >= 3;
            bool balanceOk = balance != null && recorder.Saw7F4ApplyAt2 && recorder.Saw7F4ApplyAt3 && recorder.Saw7F4ApplyAt4 && recorder.Saw7F4Restore && balance.RestoreCount >= 3;
            bool bossOk = recorder.SawBossWave && recorder.BossNo7F2Events && recorder.BossNo7F3Visuals && recorder.Boss7F4NotTuning && arena != null && arena.BossSkips > 0;
            bool mapOk = recorder.MapMatchedSeen && !recorder.MapMismatchSeen && binder != null && binder.ProfileMatched &&
                balance != null && MS7F4MapBalanceDirector.SameMap(balance.ActiveMapId,map);
            bool noOverlap = !recorder.OverlapDetected && arena != null && arena.LegacySuspensions > 0;
            bool pause = recorder.SawPause && recorder.PauseCountersStable;
            b.AppendLine("--- PER-SESSION GATES ---");
            b.AppendLine("["+(eventOk?"PASS":"PENDING")+"] 7F2 event types start in correct waves");
            b.AppendLine("["+(vfxOk?"PASS":"PENDING")+"] 7F3 all VFX types + impacts + cleanup");
            b.AppendLine("["+(balanceOk?"PASS":"PENDING")+"] 7F4 all Wave rules applied and all restored");
            b.AppendLine("["+(bossOk?"PASS":"PENDING")+"] Boss Wave 5 exclusion observed; still manually verify boss HP/HUD");
            b.AppendLine("["+(mapOk?"PASS":"PENDING")+"] Auto Map ID/Profile consistency");
            b.AppendLine("["+(noOverlap?"PASS":"PENDING")+"] 7F2 avoids 7F1 overlap");
            b.AppendLine("["+(pause?"PASS":"PENDING")+"] Pause observed; counters stable >=0.5s (not proof of visuals)");
            b.AppendLine("["+(arena!=null&&arena.LegacyRestores>0?"OBSERVED":"PENDING")+"] 7F1 original state restore after 7F2 disabled (verify original value manually)");
            bool session = fail == 0 && eventOk && vfxOk && balanceOk && bossOk && mapOk && noOverlap && pause;
            b.AppendLine("[SESSION_GATE] " + (session ? "PASS" : "PENDING"));
        }
        b.AppendLine("=== MANUAL GAMEPLAY QA (SELF-REPORTED) ===");
        for (int i=0;i<manual.Length;i++) b.AppendLine("["+(checkedManual[i]?"CONFIRMED":"NOT CONFIRMED")+"] "+manual[i]);
        b.AppendLine("MANUAL="+checkedManual.Count(x=>x)+"/"+manual.Length);
        b.AppendLine("[RELEASE] PENDING - Requires three distinct map sessions + manual 15/15 + integration/build evidence.");
        b.AppendLine("NOTE: Counter observations cannot prove pixels, attack physics, Save/Profile persistence, actual boss HP, performance or a Unity player build.");
        return b.ToString();
    }
    private void Export()
    {
        if (!EditorApplication.isPlaying)
        { EditorUtility.DisplayDialog("7F.5", "Enter Play Mode in SampleScene before exporting runtime QA.", "OK"); return; }
        string reason; WaveManager w = FindWave(out reason);
        if (w == null) { EditorUtility.DisplayDialog("7F.5", "Need SampleScene with ONE WaveManager, not MainMenu.\n"+reason, "OK"); return; }
        Directory.CreateDirectory(ReportFolder());
        string report = BuildReport(true);
        string mapId = w.GetComponent<MS7F41AutoMapIdBinder>()?.ResolvedMapId ?? "Unknown";
        string safeName = string.Concat(mapId.Where(x => char.IsLetterOrDigit(x) || x=='-' || x=='_'));
        if (string.IsNullOrEmpty(safeName)) safeName = "Unknown";
        string path = Path.Combine(ReportFolder(), "MS7F5_FinalArenaQA_" + safeName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
        File.WriteAllText(path, report, new UTF8Encoding(false));
        output = "[EXPORTED] " + path + "\n" + report;
        Debug.Log("[7F.5] Exported " + path);
        EditorUtility.RevealInFinder(path);
    }
    private void Aggregate()
    {
        string reason; WaveManager w = FindWave(out reason);
        if (w == null) { output = "[PENDING] Open SampleScene in Edit Mode or Play Mode to aggregate existing reports. " + reason; return; }
        var switcher = w.GetComponent<WaveMapSwitcher>();
        if (switcher == null || switcher.maps == null) { output = "[FAIL] WaveMapSwitcher not found"; return; }
        string[] all = Directory.Exists(ReportFolder()) ? Directory.GetFiles(ReportFolder(), "MS7F5_FinalArenaQA_*.txt") : new string[0];
        Array.Sort(all, (x,y) => File.GetLastWriteTimeUtc(y).CompareTo(File.GetLastWriteTimeUtc(x)));
        Dictionary<string,string> latest = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string,string> paths = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach (string f in all)
        {
            string t = File.ReadAllText(f);
            foreach(var entry in switcher.maps)
            {
                if(entry == null || string.IsNullOrWhiteSpace(entry.mapName)) continue;
                string id = entry.mapName.Trim();
                if (!latest.ContainsKey(id) && t.Contains("[MAP_ID]='"+id+"'")) { latest[id] = t; paths[id] = f; }
            }
        }
        StringBuilder b = new StringBuilder("MONSTERSURVIVAL 7F.5 — 3 MAP RUNTIME AGGREGATION (READ ONLY)\n");
        b.AppendLine("Generated="+DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+" ReportsAvailable="+all.Length);
        int covered=0, passed=0, total=0;
        foreach(var entry in switcher.maps)
        {
            if(entry == null || string.IsNullOrWhiteSpace(entry.mapName)) continue;
            total++;
            string id = entry.mapName.Trim();
            if (!latest.ContainsKey(id)) { b.AppendLine("[PENDING] '"+id+"': no map report"); continue; }
            covered++;
            bool ok = latest[id].Contains("[SESSION_GATE] PASS");
            if(ok)passed++;
            b.AppendLine("["+(ok?"PASS":"PENDING")+"] '"+id+"': " + Path.GetFileName(paths[id]) + " (latest report for this map)");
        }
        b.AppendLine("MAP_REPORTS="+covered+"/"+total+" SESSION_PASS="+passed+"/"+total);
        b.AppendLine("MANUAL="+checkedManual.Count(x=>x)+"/"+manual.Length+" (Editor-only self reported, not verified automatically)");
        b.AppendLine("AGGREGATE="+(total>=3&&passed==total ? "RUNTIME MAP COVERAGE PASS" : "PENDING"));
        b.AppendLine("RELEASE=PENDING: visual/Save/Profile/Unity-build and earlier 7E pending evidence require human verification.");
        b.AppendLine("NOTE: Old report files are historic evidence. Re-run all maps if gameplay scripts change.");
        Directory.CreateDirectory(ReportFolder());
        string path = Path.Combine(ReportFolder(), "MS7F5_Aggregate_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".txt");
        File.WriteAllText(path, b.ToString(), new UTF8Encoding(false));
        output = "[AGGREGATE] "+path+"\n"+b;
        Debug.Log(output);
        EditorUtility.RevealInFinder(path);
    }
    private void ResetManual()
    {
        if (!EditorUtility.DisplayDialog("7F.5", "Clear ALL manual confirmations? This does not modify any gameplay or reports already exported.", "Reset", "Cancel")) return;
        for(int i=0;i<manual.Length;i++) { checkedManual[i]=false; EditorPrefs.SetBool(PrefKey+i,false); }
        output = "[RESET] manual flags are now 0/"+manual.Length;
    }
}
#endif
