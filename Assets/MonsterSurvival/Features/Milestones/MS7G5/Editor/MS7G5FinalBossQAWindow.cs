#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 7G.5 - Consolidated read-only Boss QA. Only Install/Remove changes the Scene observer.
public sealed class MS7G5FinalBossQAWindow : EditorWindow
{
    private const string KeyPrefix = "MS7G5_Manual_";
    private const float TitanExpectedHP = 500f;
    private const float GraveExpectedHP = 750f;
    private GameObject titanPrefab, gravePrefab;
    private Vector2 scroll;
    private string output = "Open SampleScene, audit, and install ONE read-only observer.";
    private bool queued;
    private int selectedBoss; // 0=Titan, 1=Grave
    private readonly string[] titanManual = {
        "Rift telegraph visible, readable, three fair lanes, safe escape path",
        "Phase 2 Rift used and only one hit per cast; outside never damaged",
        "PlayerShield correctly blocks a Rift hit without duplicate damage",
        "Shockwave and Void Burst continue normally without unfair overlap",
        "7G.3 VFX follows source line geometry; clears on cancellation / death",
        "Pause/Resume freezes warning, impact visuals and cooldown",
        "Red Boss HP bar still follows head; Victory/GameOver/Retry work",
        "7G.4 restore test + Save/Profile/Gold/Skills/Equipment unaffected"
    };
    private readonly string[] graveManual = {
        "Soul Reap purple-gold arc readable; near-inner safe circle + side dodge",
        "Inside arc: only one Health/Shield check; outside/wall avoids damage",
        "Summon/TombWall/SpiritOrbit/Reanimate still run normally",
        "Old AI cancels Soul Reap safely if old cast begins",
        "7G.3 VFX follows real arc and cleans up on interrupt / death",
        "Pause/Resume freezes warning, VFX and cooldown",
        "Purple-gold Boss HP bar follows head; Victory/GameOver/Retry work",
        "7G.4 restore + Save/Profile/Gold/Skills/Equipment remain correct"
    };

    [MenuItem("Tools/MonsterSurvival/7G Boss Skills/5 - Open 7G.5 Final Boss QA")]
    public static void OpenWindow()
    {
        var w = GetWindow<MS7G5FinalBossQAWindow>(false, "7G.5 Final Boss QA");
        w.minSize = new Vector2(720f, 590f);
        w.Show();
    }
    private void OnEnable()
    {
        if (titanPrefab == null) titanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MonsterSurvival/Content/Prefabs/ShadowTitan.prefab");
        if (gravePrefab == null) gravePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MonsterSurvival/Content/Prefabs/GraveWardenBoss.prefab");
    }
    private void Queue(Action action)
    {
        if (queued || action == null) return;
        queued = true;
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            try { action(); }
            catch (Exception ex) { output = "[EXCEPTION] " + ex; Debug.LogException(ex); }
            finally { queued = false; if (this != null) Repaint(); }
        };
    }
    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        try
        {
            GUILayout.Label("7G.5 — FINAL BOSS QA / RELEASE GATE", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "READ-ONLY gameplay inspection for 7G.1–7G.4. No Boss HP correction, no Save/Profile, AI, VFX or combat writes.\n" +
                "IMPORTANT: prior live QA reported Titan 550 HP vs 500 prefab and Grave 825 HP vs 750 prefab. " +
                "The gate BLOCKS release until the source is identified and acceptable runtime HP verified.", MessageType.Warning);
            titanPrefab = (GameObject)EditorGUILayout.ObjectField("Shadow Titan prefab", titanPrefab, typeof(GameObject), false);
            gravePrefab = (GameObject)EditorGUILayout.ObjectField("Grave Warden prefab", gravePrefab, typeof(GameObject), false);
            selectedBoss = GUILayout.Toolbar(selectedBoss, new[] { "Shadow Titan", "Grave Warden" });
            GUILayout.Space(8);
            using (new EditorGUI.DisabledScope(queued))
            {
                if (GUILayout.Button("1 - PREFLIGHT BOTH PREFABS (READ ONLY)", GUILayout.Height(31)))
                    Queue(() => { output = PrefabAudit(); Debug.Log(output); });
                if (GUILayout.Button("2 - BACKUP + INSTALL READ-ONLY OBSERVER", GUILayout.Height(32))) Queue(Install);
                if (GUILayout.Button("3 - PURE SAFETY / GEOMETRY TESTS", GUILayout.Height(31)))
                    Queue(() => { output = PureTests(); Debug.Log(output); });
                if (GUILayout.Button("4 - EXPORT THIS BOSS QA IN PLAY MODE", GUILayout.Height(33))) Queue(Export);
                if (GUILayout.Button("5 - AGGREGATE TWO BOSS REPORTS", GUILayout.Height(32))) Queue(Aggregate);
                if (GUILayout.Button("6 - FIND POSSIBLE HP WRITERS (READ ONLY)", GUILayout.Height(32))) Queue(ScanHpSources);
                if (GUILayout.Button("7 - BACKUP + REMOVE ONLY 7G.5 OBSERVER", GUILayout.Height(29))) Queue(Remove);
            }
            EditorGUILayout.HelpBox(
                "Workflow: Install in SampleScene EDIT mode, then start from MainMenu. Export Titan and Grave " +
                "in their own Wave-5 Phase-2 encounters (or after observed kill if recorder was running). " +
                "For a different boss, begin a NEW Play session. One encounter is not proof for both bosses.", MessageType.Info);
            GUILayout.Space(7);
            var checks = selectedBoss == 0 ? titanManual : graveManual;
            GUILayout.Label("MANUAL QA / " + (selectedBoss == 0 ? "SHADOW TITAN" : "GRAVE WARDEN") +
                            " - do not check without actually testing", EditorStyles.boldLabel);
            int done = 0;
            for (int i = 0; i < checks.Length; i++)
            {
                string key = ManualKey(selectedBoss, i);
                bool current = EditorPrefs.GetBool(key, false);
                bool next = EditorGUILayout.ToggleLeft((i + 1) + " - " + checks[i], current);
                if (next != current) EditorPrefs.SetBool(key, next);
                if (next) done++;
            }
            GUILayout.Label("Manual verified: " + done + "/" + checks.Length + " (local editor confirmation only)");
            if (GUILayout.Button("Reset selected Boss manual checks (confirm)")) Queue(ResetManual);
            GUILayout.Space(7);
            GUILayout.Label("Latest report", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(output, GUILayout.MinHeight(280));
        }
        finally { EditorGUILayout.EndScrollView(); }
    }

    private static string ManualKey(int boss, int id) { return KeyPrefix + boss + "_" + id; }
    private static int ManualCount(int boss)
    {
        int c = 0;
        for (int i = 0; i < 8; i++) if (EditorPrefs.GetBool(ManualKey(boss, i), false)) c++;
        return c;
    }
    private void ResetManual()
    {
        int boss = selectedBoss;
        if (!EditorUtility.DisplayDialog("7G.5 Reset", "Clear eight MANUAL ticks for " +
                 (boss == 0 ? "Shadow Titan" : "Grave Warden") + "?", "Reset", "Cancel")) return;
        for (int i = 0; i < 8; i++) EditorPrefs.DeleteKey(ManualKey(boss, i));
    }
    private static WaveManager FindWave(out string reason)
    {
        Scene scene = SceneManager.GetActiveScene();
        var all = UnityEngine.Object.FindObjectsByType<WaveManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        WaveManager result = null; int count = 0;
        foreach (var w in all) if (w != null && w.gameObject.scene == scene) { result = w; count++; }
        reason = count == 1 ? "WaveManager found" : "Expected one WaveManager in gameplay Scene; found " + count;
        return count == 1 ? result : null;
    }
    private static string Root() { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); }
    private static string QaFolder() { return Path.Combine(Root(), "MS_QAReports", "7G5"); }
    private static void SaveReport(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, text, new UTF8Encoding(true));
        Debug.Log("[7G.5] Report saved: " + path);
    }
    private static bool ValidPrefab(GameObject prefab, bool titan, out string reason)
    {
        string display = titan ? "TITAN" : "GRAVE";
        if (prefab == null) { reason = "[FAIL] " + display + " prefab missing"; return false; }
        string path = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
            !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
            PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.NotAPrefab)
        { reason = "[FAIL] " + display + ": pick the real Prefab ASSET, not Scene object"; return false; }
        if (prefab.GetComponent<Health>() == null) { reason = "[FAIL] " + display + " missing Health"; return false; }
        bool core = titan ? prefab.GetComponent<MS7G1TitanRiftLanes>() != null &&
            prefab.GetComponent<BossPhase2Controller>() != null :
            prefab.GetComponent<MS7G2GraveSoulReap>() != null && prefab.GetComponent<GraveWardenBossAI>() != null;
        reason = (core ? "[PASS] " : "[FAIL] ") + display + " " + path +
                 (core ? "" : " missing existing 7G.1/7G.2 or Phase/AI");
        return core;
    }
    private string PrefabAudit()
    {
        var b = new StringBuilder();
        b.AppendLine("MONSTERSURVIVAL 7G.5 — PREFLIGHT / READ ONLY");
        b.AppendLine("Unity=" + Application.unityVersion + " Scene=" + SceneManager.GetActiveScene().name +
                     " PlayMode=" + EditorApplication.isPlaying);
        string t, g;
        bool tok = ValidPrefab(titanPrefab, true, out t), gok = ValidPrefab(gravePrefab, false, out g);
        b.AppendLine(t); b.AppendLine(g);
        if (tok)
        {
            var hp = titanPrefab.GetComponent<Health>().maxHealth;
            b.AppendLine("[" + (Mathf.Approximately(hp, TitanExpectedHP) ? "PASS" : "FAIL") +
                         "] Titan prefab HP=" + hp + " TARGET=" + TitanExpectedHP);
            b.AppendLine("[" + (titanPrefab.GetComponent<BossSpecialAttack>() && titanPrefab.GetComponent<BossVoidBurstAttack>() ? "PASS" : "PENDING") +
                         "] Existing Shockwave/Void Burst components");
            b.AppendLine("Titan 7G3 VFX=" + (titanPrefab.GetComponent<MS7G3BossSkillVFX>() != null) +
                         " 7G4 Balance=" + (titanPrefab.GetComponent<MS7G4BossBalance>() != null));
        }
        if (gok)
        {
            var hp = gravePrefab.GetComponent<Health>().maxHealth;
            b.AppendLine("[" + (Mathf.Approximately(hp, GraveExpectedHP) ? "PASS" : "FAIL") +
                         "] Grave prefab HP=" + hp + " TARGET=" + GraveExpectedHP);
            b.AppendLine("Grave 7G3 VFX=" + (gravePrefab.GetComponent<MS7G3BossSkillVFX>() != null) +
                         " 7G4 Balance=" + (gravePrefab.GetComponent<MS7G4BossBalance>() != null));
        }
        string why; var wave = FindWave(out why);
        b.AppendLine("GAMEPLAY_SCENE_WAVE=" + (wave != null ? "PASS" : "PENDING") + " " + why);
        if (wave != null) b.AppendLine("7G5_RECORDER=" + (wave.GetComponent<MS7G5BossEncounterRecorder>() != null));
        b.AppendLine("PREFLIGHT=" + (tok && gok ? "PASS" : "FAIL"));
        b.AppendLine("NOTE: prefab HP is NOT live HP; prior 550/825 requires runtime investigation.");
        return b.ToString();
    }

    private static string BackUpScene(Scene scene)
    {
        if (string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("Save gameplay Scene before installing.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return null;
        if (!EditorSceneManager.SaveScene(scene)) return null;
        var folder = Path.Combine(Root(), "MS_Backups", "7G5");
        Directory.CreateDirectory(folder);
        string source = Path.GetFullPath(Path.Combine(Root(), scene.path));
        string target = Path.Combine(folder, Path.GetFileNameWithoutExtension(scene.path) +
                         "_BEFORE_7G5_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity");
        File.Copy(source, target, false);
        return target;
    }
    private void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorUtility.DisplayDialog("7G.5", "Stop Play Mode before installing.", "OK"); return; }
        string why; var wave = FindWave(out why);
        if (wave == null) { EditorUtility.DisplayDialog("7G.5", why + ". Open SampleScene first.", "OK"); return; }
        string a, b;
        // Run BOTH prefab checks before showing errors.  || short-circuiting could
        // skip the Grave check and leave local variable 'b' unassigned (CS0165).
        bool titanValid = ValidPrefab(titanPrefab, true, out a);
        bool graveValid = ValidPrefab(gravePrefab, false, out b);
        if (!titanValid || !graveValid)
        { EditorUtility.DisplayDialog("7G.5", "Check the two real boss prefabs first.\n" + a + "\n" + b, "OK"); return; }
        var existing = wave.GetComponents<MS7G5BossEncounterRecorder>();
        if (existing.Length > 1) { EditorUtility.DisplayDialog("7G.5", "Multiple recorders found. Remove duplicates manually after backup.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("7G.5 read-only recorder", "Back up gameplay Scene and attach ONE read-only 7G.5 observer to WaveManager? Boss prefabs and gameplay code remain unchanged.", "Install", "Cancel")) return;
        var backup = BackUpScene(wave.gameObject.scene);
        if (backup == null) return;
        var recorder = existing.Length == 1 ? existing[0] : Undo.AddComponent<MS7G5BossEncounterRecorder>(wave.gameObject);
        Undo.RecordObject(recorder, "7G.5 enable observer");
        recorder.enableObservation = true;
        EditorUtility.SetDirty(recorder);
        EditorSceneManager.MarkSceneDirty(wave.gameObject.scene);
        EditorSceneManager.SaveScene(wave.gameObject.scene);
        output = "[INSTALL] 7G.5 read-only recorder active on existing WaveManager.\nBackup=" + backup + "\n" + PrefabAudit();
        Debug.Log(output);
    }
    private void Remove()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorUtility.DisplayDialog("7G.5", "Stop Play Mode before removing.", "OK"); return; }
        string why; var wave = FindWave(out why);
        if (wave == null) { EditorUtility.DisplayDialog("7G.5", why, "OK"); return; }
        var recorder = wave.GetComponent<MS7G5BossEncounterRecorder>();
        if (recorder == null) { output = "[INFO] 7G.5 observer not installed."; return; }
        if (!EditorUtility.DisplayDialog("Remove 7G.5 only", "Back up Scene and remove only the 7G.5 recorder?", "Remove", "Cancel")) return;
        string backup = BackUpScene(wave.gameObject.scene);
        if (backup == null) return;
        Undo.DestroyObjectImmediate(recorder);
        EditorSceneManager.MarkSceneDirty(wave.gameObject.scene);
        EditorSceneManager.SaveScene(wave.gameObject.scene);
        output = "[REMOVE] Only 7G.5 observer removed. All Boss skills and data unchanged.\nBackup=" + backup;
    }

    private static void Check(StringBuilder b, ref int pass, ref int fail, bool ok, string label)
    {
        if (ok) { pass++; b.AppendLine("[PASS] " + label); }
        else { fail++; b.AppendLine("[FAIL] " + label); }
    }
    private static string PureTests()
    {
        int pass = 0, fail = 0; var b = new StringBuilder("7G.5 PURE TESTS / NO SCENE CHANGES\n");
        Check(b, ref pass, ref fail, MS7G2GraveSoulReap.IsInsideSector(
            new Vector2(2f,0f), Vector2.zero, Vector2.right,1.6f,4.15f,43f), "Grave arc contains forward target");
        Check(b, ref pass, ref fail, !MS7G2GraveSoulReap.IsInsideSector(
            new Vector2(.5f,0f), Vector2.zero, Vector2.right,1.6f,4.15f,43f), "Grave near boss safe zone");
        Check(b, ref pass, ref fail, !MS7G2GraveSoulReap.IsInsideSector(
            new Vector2(0f,3f), Vector2.zero, Vector2.right,1.6f,4.15f,43f), "Grave side dodge");
        Check(b, ref pass, ref fail, !MS7G2GraveSoulReap.IsInsideSector(
            new Vector2(5f,0f), Vector2.zero, Vector2.right,1.6f,4.15f,43f), "Grave outside radius safe");
        Check(b, ref pass, ref fail, Math.Abs((550f/500f-1f)*100f-10f)<.001f, "HP diagnostic detects Titan +10% example");
        Check(b, ref pass, ref fail, Math.Abs((825f/750f-1f)*100f-10f)<.001f, "HP diagnostic detects Grave +10% example");
        Check(b, ref pass, ref fail, !Mathf.Approximately(550f, TitanExpectedHP), "550 never passes 500HP gate");
        Check(b, ref pass, ref fail, !Mathf.Approximately(825f, GraveExpectedHP), "825 never passes 750HP gate");
        Check(b, ref pass, ref fail, Mathf.Approximately(500f, TitanExpectedHP), "Target Titan 500HP accepted");
        Check(b, ref pass, ref fail, Mathf.Approximately(750f, GraveExpectedHP), "Target Grave 750HP accepted");
        b.AppendLine("PURE_PASS="+pass+" PURE_FAIL="+fail);
        b.AppendLine("Pure tests are not Play Mode, visual, AI-overlap, Save/Profile or Build validation.");
        return b.ToString();
    }

    private string BuildBossReport(int bossId)
    {
        var b = new StringBuilder();
        string bossKind = bossId == 0 ? "Titan" : "Grave";
        float target = bossId == 0 ? TitanExpectedHP : GraveExpectedHP;
        GameObject prefab = bossId == 0 ? titanPrefab : gravePrefab;
        b.AppendLine("MONSTERSURVIVAL 7G.5 — FINAL BOSS RELEASE GATE / READ ONLY");
        b.AppendLine("Created=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " Unity=" + Application.unityVersion +
                     " Scene=" + SceneManager.GetActiveScene().name + " PlayMode=" + EditorApplication.isPlaying);
        b.AppendLine("BOSS_KIND=" + bossKind);
        b.AppendLine("No gameplay, Health, Save/Profile, Prefab or Balance values were written by this audit.");
        string reason; bool prefabValid = ValidPrefab(prefab, bossId == 0, out reason);
        b.AppendLine(reason);
        b.AppendLine("STRUCTURE_GATE=" + (prefabValid ? "PASS" : "FAIL"));
        float prefabHp = prefabValid ? prefab.GetComponent<Health>().maxHealth : 0f;
        b.AppendLine("HP_PREFAB=" + prefabHp.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                     " HP_TARGET=" + target.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        var wave = FindWave(out string waveReason);
        b.AppendLine("WAVE_MANAGER=" + (wave != null ? "PASS" : "PENDING") + " (" + waveReason + ")");
        var recorder = wave != null ? wave.GetComponent<MS7G5BossEncounterRecorder>() : null;
        var capture = recorder != null ? (bossId == 0 ? recorder.Titan : recorder.Grave) : null;
        bool hasLive = EditorApplication.isPlaying && capture != null && capture.hasSample;
        b.AppendLine("OBSERVER=" + (recorder != null ? "INSTALLED" : "MISSING") +
                     " OBSERVED_FRAMES=" + (recorder != null ? recorder.ObservedFrames : 0));
        float hp = hasLive ? capture.runtimeMaxHealth : 0f;
        string hpGate = !hasLive || !prefabValid ? "PENDING" :
            (Mathf.Approximately(hp, target) && Mathf.Approximately(prefabHp, target) ? "PASS" : "FAIL");
        b.AppendLine("HP_RUNTIME=" + hp.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        b.AppendLine("HP_DELTA_PERCENT=" + (hasLive && prefabHp > 0f ?
                     ((hp / prefabHp - 1f)*100f).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : "UNKNOWN"));
        b.AppendLine("HP_GATE=" + hpGate);
        if (hpGate == "FAIL") b.AppendLine("[BLOCKER] Runtime HP mismatches target. INVESTIGATE potential Wave / 7A.3 scaling first. No auto HP correction.");
        if (hasLive) b.Append(capture.ToReport());
        else b.AppendLine("[PENDING] No observed " + bossKind + " boss. Start MainMenu -> Boss map -> Wave 5 -> Phase 2.");
        string castGate = hasLive && capture.phaseTwo && capture.warnings > 0 && capture.impacts > 0 ? "PASS" : "PENDING";
        string vfxGate = hasLive && capture.vfxEnabled && capture.vfxWarnings > 0 && capture.vfxImpacts > 0 ? "PASS" : "PENDING";
        string balanceGate = hasLive && capture.balanceApplies > 0 ? "PASS" : "PENDING";
        string restoreGate = hasLive && capture.balanceRestores > 0 ? "PASS" : "PENDING";
        string pauseGate = hasLive && (capture.sawPause || recorder.SawAnyPause) ? "OBSERVED" : "PENDING";
        b.AppendLine("CAST_GATE=" + castGate + " VFX_GATE=" + vfxGate + " BALANCE_GATE=" + balanceGate);
        b.AppendLine("RESTORE_GATE=" + restoreGate + " PAUSE_GATE=" + pauseGate);
        int done = ManualCount(bossId); var checks = bossId == 0 ? titanManual : graveManual;
        b.AppendLine("=== MANUAL QA / EDITOR USER TICKS ===");
        for (int i = 0; i < checks.Length; i++) b.AppendLine((EditorPrefs.GetBool(ManualKey(bossId,i),false) ? "[CONFIRMED] " : "[NOT CONFIRMED] ") + (i+1) + " " + checks[i]);
        b.AppendLine("MANUAL_CONFIRMED=" + done + "/8");
        bool tech = prefabValid && hpGate == "PASS" && castGate == "PASS" &&
                    vfxGate == "PASS" && balanceGate == "PASS" && restoreGate == "PASS" &&
                    pauseGate == "OBSERVED" && hasLive && capture.existingBossAiDetected && capture.encounters == 1;
        string release = hpGate == "FAIL" ? "BLOCKED_HP" : tech && done == 8 ? "READY_FOR_USER_FINAL_REVIEW" : "PENDING";
        b.AppendLine("RELEASE_GATE=" + release);
        b.AppendLine("NOTE: counters do NOT certify visual quality, fair walls, all old skills, Win/Lose, or persistent Save data.");
        return b.ToString();
    }
    private void Export()
    {
        if (!EditorApplication.isPlaying) { output = "[PENDING] Enter Play Mode from MainMenu. Export in SampleScene."; return; }
        string why; if (FindWave(out why) == null) { output = "[PENDING] " + why; return; }
        var content = BuildBossReport(selectedBoss);
        string boss = selectedBoss == 0 ? "Titan" : "Grave";
        string file = Path.Combine(QaFolder(), "MS7G5_" + boss + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".txt");
        SaveReport(file, content);
        output = "[EXPORTED] " + file + "\n" + content;
    }
    private static string Extract(string content, string key)
    {
        var m = Regex.Match(content, @"(?m)^" + Regex.Escape(key) + @"=([^\r\n ]+)");
        return m.Success ? m.Groups[1].Value.Trim() : "MISSING";
    }
    private void Aggregate()
    {
        var directory = QaFolder(); Directory.CreateDirectory(directory);
        string first = EditorUtility.OpenFilePanel("7G5 report #1 (Titan or Grave)", directory, "txt");
        if (string.IsNullOrEmpty(first)) return;
        string second = EditorUtility.OpenFilePanel("7G5 report #2 (other Boss)", directory, "txt");
        if (string.IsNullOrEmpty(second)) return;
        if (Path.GetFullPath(first) == Path.GetFullPath(second)) { output = "[FAIL] Two distinct report files required."; return; }
        string a = File.ReadAllText(first), c = File.ReadAllText(second);
        if (!a.Contains("MONSTERSURVIVAL 7G.5") || !c.Contains("MONSTERSURVIVAL 7G.5"))
        { output = "[FAIL] Select two 7G.5 exported reports, not 7G.4 reports."; return; }
        string typeA = Extract(a,"BOSS_KIND"), typeC = Extract(c,"BOSS_KIND");
        if (!((typeA == "Titan" && typeC == "Grave") || (typeA == "Grave" && typeC == "Titan")))
        { output = "[FAIL] Need one Titan and one Grave report; found " + typeA + " / " + typeC; return; }
        string titan = typeA == "Titan" ? a : c, grave = typeA == "Grave" ? a : c;
        var b = new StringBuilder();
        b.AppendLine("MONSTERSURVIVAL 7G.5 — TWO BOSS FINAL AGGREGATE / READ ONLY");
        b.AppendLine("Created=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        b.AppendLine("TitanSource=" + (typeA == "Titan" ? first : second));
        b.AppendLine("GraveSource=" + (typeA == "Grave" ? first : second));
        foreach (string key in new[] { "STRUCTURE_GATE","HP_PREFAB","HP_RUNTIME","HP_DELTA_PERCENT","HP_GATE","CAST_GATE","VFX_GATE","BALANCE_GATE","RESTORE_GATE","PAUSE_GATE","MANUAL_CONFIRMED","RELEASE_GATE" })
        {
            b.AppendLine(key + ": Titan=" + Extract(titan,key) + " Grave=" + Extract(grave,key));
        }
        bool bothReady = Extract(titan,"RELEASE_GATE") == "READY_FOR_USER_FINAL_REVIEW" &&
                         Extract(grave,"RELEASE_GATE") == "READY_FOR_USER_FINAL_REVIEW";
        string final = Extract(titan,"HP_GATE") == "FAIL" || Extract(grave,"HP_GATE") == "FAIL" ?
                       "BLOCKED_HP" : bothReady ? "READY_FOR_USER_FINAL_REVIEW" : "PENDING";
        b.AppendLine("FINAL_7G_RELEASE_GATE=" + final);
        b.AppendLine("IMPORTANT: 7G.1 Titan live QA, 7G.3 animation quality, 7G.4 restore, old Boss skills and Profile are MANUAL checks.");
        b.AppendLine("Do not claim complete until the above claims are actually tested and runtime HP is resolved.");
        string path = Path.Combine(directory,"MS7G5_Aggregate_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".txt");
        SaveReport(path,b.ToString());
        output = "[AGGREGATED] " + path + "\n" + b;
    }
    private void ScanHpSources()
    {
        var b = new StringBuilder();
        b.AppendLine("MONSTERSURVIVAL 7G.5 — POSSIBLE HP WRITERS / STATIC FILE SCAN");
        b.AppendLine("READ ONLY. These are CANDIDATES, NOT proof a particular script caused the 10% HP increase.");
        b.AppendLine("Prior reports: Shadow Titan 500 prefab -> 550 runtime, Grave 750 -> 825 (+10% each).");
        b.AppendLine("Searches text of existing C# files under Assets/ for assignments to maxHealth.");
        var guids = AssetDatabase.FindAssets("t:MonoScript", new [] { "Assets" });
        var pattern = new Regex(@"\bmaxHealth\s*(?:\*=|/=|\+=|-=|=(?!=))", RegexOptions.Compiled);
        int found = 0;
        foreach (var id in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(id);
            if (!path.EndsWith(".cs",StringComparison.OrdinalIgnoreCase)) continue;
            string absolute = Path.Combine(Root(), path);
            if (!File.Exists(absolute)) continue;
            string[] lines;
            try { lines = File.ReadAllLines(absolute); }
            catch { continue; }
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (!pattern.IsMatch(line) || line.StartsWith("//")) continue;
                // Avoid confusing declaration initialization (public float maxHealth = 100f) with live modification.
                bool declaration = Regex.IsMatch(line, @"\b(?:public|private|protected|internal)\s+(?:float|int|double)\s+maxHealth\s*=");
                if (declaration) continue;
                b.AppendLine("[CANDIDATE] " + path + ":" + (i+1) + " " + line);
                found++;
                if (found >= 80) break;
            }
            if (found >= 80) break;
        }
        b.AppendLine("MATCHED_LINES=" + found + (found >= 80 ? " (TRUNCATED)" : ""));
        b.AppendLine("If none: HP may be changed by serialized values, reflection, an indirect method, a package, or an unscanned file.");
        b.AppendLine("Next: inspect runtime Wave / 7A.3 / other Boss scaling components, disable suspected module only in a backup test.");
        string filename = Path.Combine(QaFolder(),"MS7G5_HPSourceCandidates_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".txt");
        SaveReport(filename,b.ToString());
        output = "[EXPORTED] " + filename + "\n" + b;
    }
}
#endif
