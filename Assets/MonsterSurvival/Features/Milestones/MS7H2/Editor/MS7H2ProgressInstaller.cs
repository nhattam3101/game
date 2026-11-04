#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only, additive installer. Does not edit 5A/5C/5D core or MainMenu V3 assets.
public sealed class MS7H2ProgressInstaller : EditorWindow
{
    private Vector2 scroll;
    private string lastReport = "7H.2 Progressive Quests. Stop Play Mode before Install/Remove.";
    private bool pendingAction;
    // Pick the Scene currently open in Editor first, or the one enabled in Build Settings.
    // The previous implementation took the FIRST AssetDatabase match; projects with
    // multiple copies of MainMenu / SampleScene could silently inspect the wrong Scene.
    private static string FindScene(string name)
    {
        var paths = new System.Collections.Generic.List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Scene " + name))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.Equals(Path.GetFileNameWithoutExtension(path), name, StringComparison.OrdinalIgnoreCase) &&
                !path.Contains("/Backups/") && !path.Contains("/MS_Backups/"))
                paths.Add(path);
        }
        if (paths.Count == 0) return null;
        paths.Sort(StringComparer.Ordinal);
        Scene current = SceneManager.GetActiveScene();
        if (current.IsValid() && current.name == name && paths.Contains(current.path))
            return current.path;
        foreach (EditorBuildSettingsScene item in EditorBuildSettings.scenes)
            if (item.enabled && paths.Contains(item.path)) return item.path;
        return paths[0];
    }

    // Read-only validation of the chosen TWO Scenes. If a Scene is not already loaded,
    // open it ADDITIVELY and unload only that temporary instance; never discard changes
    // to the user's open Scene. Never add a 5A.3 save component automatically.
    private static string CheckSceneDependencies(out bool ok)
    {
        ok = false;
        string main = FindScene("MainMenu"), game = FindScene("SampleScene");
        var sb = new StringBuilder();
        sb.AppendLine("7H.2 DEPENDENCY CHECK / READ ONLY");
        sb.AppendLine("Editor Scene=" + SceneManager.GetActiveScene().path);
        sb.AppendLine("MainMenu asset=" + (main ?? "MISSING"));
        sb.AppendLine("SampleScene asset=" + (game ?? "MISSING"));
        if (string.IsNullOrEmpty(main) || string.IsNullOrEmpty(game))
        {
            sb.AppendLine("[BLOCKED] Missing one of the actual Scene assets.");
            return sb.ToString();
        }
        bool hasBoard = false, hasWave = false, hasRun = false;
        string boardName = "", waveName = "", runName = "";
        try
        {
            Scene openedMain = SceneManager.GetSceneByPath(main);
            bool temporaryMain = !openedMain.IsValid() || !openedMain.isLoaded;
            if (temporaryMain) openedMain = EditorSceneManager.OpenScene(main, OpenSceneMode.Additive);
            try
            {
                MS7H1QuestBoard board = FindInScene<MS7H1QuestBoard>(openedMain);
                hasBoard = board != null;
                if (hasBoard) boardName = board.gameObject.name;
                else
                {
                    foreach (GameObject root in openedMain.GetRootGameObjects())
                    {
                        Transform[] trs = root.GetComponentsInChildren<Transform>(true);
                        foreach (Transform tr in trs)
                            if (tr.name.Contains("MS7H1_QuestBoard")) boardName = tr.name + " (component missing / script link broken?)";
                    }
                }
            }
            finally { if (temporaryMain) EditorSceneManager.CloseScene(openedMain, true); }

            Scene openedGame = SceneManager.GetSceneByPath(game);
            bool temporaryGame = !openedGame.IsValid() || !openedGame.isLoaded;
            if (temporaryGame) openedGame = EditorSceneManager.OpenScene(game, OpenSceneMode.Additive);
            try
            {
                WaveManager wave = FindInScene<WaveManager>(openedGame);
                MS5A3PersistentRun run = FindInScene<MS5A3PersistentRun>(openedGame);
                hasWave = wave != null;
                hasRun = run != null;
                waveName = hasWave ? wave.gameObject.name : "";
                runName = hasRun ? run.gameObject.name : "";
            }
            finally { if (temporaryGame) EditorSceneManager.CloseScene(openedGame, true); }
        }
        catch (Exception ex)
        {
            sb.AppendLine("[ERROR] Cannot inspect Scene without changes: " + ex.Message);
            return sb.ToString();
        }
        sb.AppendLine((hasBoard ? "[PASS]" : "[FAIL]") + " MainMenu / MS7H1QuestBoard " + boardName);
        sb.AppendLine((hasWave ? "[PASS]" : "[FAIL]") + " SampleScene / WaveManager " + waveName);
        sb.AppendLine((hasRun ? "[PASS]" : "[FAIL]") + " SampleScene / MS5A3PersistentRun " + runName);
        ok = hasBoard && hasWave && hasRun;
        if (ok) sb.AppendLine("[READY] All 3 dependencies found. Stop Play Mode and install.");
        else
        {
            sb.AppendLine("[BLOCKED] Exact missing component(s) are flagged above. No Scene was modified.");
            sb.AppendLine("If MS5A3PersistentRun is absent, inspect 5A.3 setup in SampleScene. Do NOT add a bare save component: its Inventory/Equipment dependencies must be bound.");
            sb.AppendLine("If a valid component exists in a DIFFERENT Scene copy, inspect the selected asset paths above and Build Profiles.");
        }
        return sb.ToString();
    }
    [MenuItem("Tools/MonsterSurvival/7H Quests/2 - Open 7H.2 Progressive Quests")]
    public static void Open() { GetWindow<MS7H2ProgressInstaller>("7H.2 Progress Quests"); }
    private void Queue(Action action)
    {
        if (pendingAction || action == null) return;
        pendingAction = true;
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            try { action(); }
            catch (Exception ex) { lastReport = "[7H2 ERROR] " + ex; Debug.LogException(ex); }
            finally { pendingAction = false; if (this != null) Repaint(); }
        };
    }
    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        try
        {
            EditorGUILayout.LabelField("7H.2 - PROGRESSIVE QUESTS / UNITY 6.6",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("3 progressive quests: 3 Victories, 2 real Boss kills, 500 earned Gold. Uses the existing 5A Save. No 7H1 UI or Menu V3 replacement.",MessageType.Info);
            if (GUILayout.Button("1 - PREFLIGHT / AUDIT (READ ONLY)",GUILayout.Height(33))) Queue(() => { bool ok; SetReport(CheckSceneDependencies(out ok) + "\n" + Audit()); });
            if (GUILayout.Button("2 - PURE PROGRESSION TESTS (NO DISK WRITES)",GUILayout.Height(33))) Queue(() => SetReport(PureTests()));
            if (GUILayout.Button("3 - BACKUP + INSTALL BOTH SCENES",GUILayout.Height(35))) Queue(Install);
            if (GUILayout.Button("4 - EXPORT 7H.2 QA TXT",GUILayout.Height(33))) Queue(Export);
            if (GUILayout.Button("5 - BACKUP + REMOVE 7H.2 COMPONENTS",GUILayout.Height(32))) Queue(Remove);
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Report",EditorStyles.boldLabel);
            EditorGUILayout.TextArea(lastReport,GUILayout.MinHeight(340),GUILayout.ExpandHeight(true));
        }
        finally { EditorGUILayout.EndScrollView(); }
    }
    private void SetReport(string report) { lastReport = report; Debug.Log(report); }
    private static bool Ready(out string menu, out string game, out string error)
    {
        menu=FindScene("MainMenu"); game=FindScene("SampleScene"); error="";
        if (string.IsNullOrEmpty(menu) || string.IsNullOrEmpty(game))
        { error="Missing MainMenu / SampleScene assets"; return false; }
        if (Application.isPlaying)
        { error="Stop Play Mode before installation or removal"; return false; }
        return true;
    }
    private static string ProjectRoot { get { return Directory.GetParent(Application.dataPath).FullName; } }
    private static string Backup(string assetPath, string tag)
    {
        string dir=Path.Combine(ProjectRoot,"MS_Backups","7H2");
        Directory.CreateDirectory(dir);
        string filename=tag+"_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".unity";
        string result=Path.Combine(dir,filename);
        string source=Path.Combine(ProjectRoot,assetPath);
        if (!File.Exists(source)) throw new IOException("Scene not found for backup: "+source);
        File.Copy(source,result,false);
        if (!File.Exists(result) || new FileInfo(result).Length != new FileInfo(source).Length)
            throw new IOException("Backup verification failed: "+result);
        return result;
    }
    private static T FindInScene<T>(Scene scene) where T:Component
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            T component=go.GetComponentInChildren<T>(true);
            if (component) return component;
        }
        return null;
    }
    private void Install()
    {
        string main,game,error;
        if (!Ready(out main,out game,out error)) { SetReport("[BLOCKED] "+error); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { SetReport("[CANCELLED] Unsaved Scene"); return; }
        // Check both actual Scene paths without replacing the current Editor Scene.
        // Only proceed to the original, backup-first install if all three exist.
        bool valid;
        string diagnosis = CheckSceneDependencies(out valid);
        if (!valid) { SetReport(diagnosis); return; }
        string original=SceneManager.GetActiveScene().path;
        // Back up BOTH current Scenes before the first mutation.
        string bMain=Backup(main,"MainMenu_before_7H2");
        string bGame=Backup(game,"SampleScene_before_7H2");
        Scene mainScene=EditorSceneManager.OpenScene(main,OpenSceneMode.Single);
        if (!FindInScene<MS7H2ProgressBoard>(mainScene))
        {
            GameObject host=new GameObject("MS7H2_ProgressBoard [MainMenu]");
            Undo.RegisterCreatedObjectUndo(host,"Install 7H2 Progress Board");
            Undo.AddComponent<MS7H2ProgressBoard>(host);
            EditorSceneManager.MarkSceneDirty(mainScene);
            EditorSceneManager.SaveScene(mainScene);
        }
        Scene gameScene=EditorSceneManager.OpenScene(game,OpenSceneMode.Single);
        WaveManager wave=FindInScene<WaveManager>(gameScene);
        if (wave.GetComponent<MS7H2VictoryProgressObserver>() == null)
        {
            MS7H2VictoryProgressObserver observer=Undo.AddComponent<MS7H2VictoryProgressObserver>(wave.gameObject);
            observer.waveManager=wave;
            observer.persistentRun=FindInScene<MS5A3PersistentRun>(gameScene);
            observer.progression=FindInScene<PlayerProgression>(gameScene);
            EditorUtility.SetDirty(observer);
            EditorSceneManager.MarkSceneDirty(gameScene);
            EditorSceneManager.SaveScene(gameScene);
        }
        EditorSceneManager.OpenScene(!string.IsNullOrEmpty(original)?original:main,OpenSceneMode.Single);
        SetReport("[INSTALL/REPAIR COMPLETE] 7H2 added without replacing 7H1.1 or 5A Save.\n[BACKUP] "+bMain+"\n[BACKUP] "+bGame+"\n"+Audit());
    }
    private void Remove()
    {
        string main,game,error;
        if (!Ready(out main,out game,out error)) { SetReport("[BLOCKED] "+error); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string original=SceneManager.GetActiveScene().path;
        string bMain=Backup(main,"MainMenu_before_7H2_REMOVE");
        string bGame=Backup(game,"SampleScene_before_7H2_REMOVE");
        Scene scene=EditorSceneManager.OpenScene(main,OpenSceneMode.Single);
        var board=FindInScene<MS7H2ProgressBoard>(scene);
        if (board)
        {
            if (board.gameObject.name == "MS7H2_ProgressBoard [MainMenu]") Undo.DestroyObjectImmediate(board.gameObject);
            else Undo.DestroyObjectImmediate(board);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        scene=EditorSceneManager.OpenScene(game,OpenSceneMode.Single);
        var observer=FindInScene<MS7H2VictoryProgressObserver>(scene);
        if (observer)
        {
            Undo.DestroyObjectImmediate(observer);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        EditorSceneManager.OpenScene(!string.IsNullOrEmpty(original)?original:main,OpenSceneMode.Single);
        SetReport("[REMOVED UI/OBSERVER] 7H2 markers and earned rewards remain in Save.\n[BACKUP] "+bMain+"\n[BACKUP] "+bGame);
    }
    private static string Audit()
    {
        var sb=new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 7H.2 — PROGRESSIVE QUESTS / READ ONLY");
        sb.AppendLine("Unity="+Application.unityVersion+" Scene="+SceneManager.GetActiveScene().name+" PlayMode="+Application.isPlaying);
        string main=FindScene("MainMenu"),game=FindScene("SampleScene");
        sb.AppendLine((main!=null?"[PASS]":"[FAIL]")+" MainMenu="+main);
        sb.AppendLine((game!=null?"[PASS]":"[FAIL]")+" SampleScene="+game);
        sb.AppendLine("[INFO] 7H1 layout and QuestService files are never replaced by this package");
        sb.AppendLine("[NOTE] Audit above reports dependencies in each saved Scene, not just the active Scene.");
        var board=UnityEngine.Object.FindFirstObjectByType<MS7H2ProgressBoard>(FindObjectsInactive.Include);
        var watcher=UnityEngine.Object.FindFirstObjectByType<MS7H2VictoryProgressObserver>(FindObjectsInactive.Include);
        sb.AppendLine("[INFO] Current Scene board="+(board?"PRESENT":"NOT IN ACTIVE SCENE")+" observer="+(watcher?"PRESENT":"NOT IN ACTIVE SCENE"));
        if (watcher) sb.AppendLine("[LIVE] "+watcher.Status+" attempts="+watcher.SyncAttempts+" realBossDefeated="+watcher.BossDefeated+" finished="+watcher.Finished);
        for(int slot=1;slot<=3;slot++)
        {
            var s=MS7H2ProgressService.ReadSnapshot(slot);
            sb.AppendLine("[PROFILE "+slot+"] valid="+s.valid+" backupRecovery="+s.backupRecovery+
                " wins="+s.wins+" bosses="+s.bosses+" earnedGold="+s.earnedGold+" bankGold="+s.bankGold+
                " rev="+s.revision+" status="+s.message);
            foreach(var q in s.quests)
                sb.AppendLine("  "+q.id+" "+q.current+"/"+q.target+" completed="+q.completed+" claimed="+q.claimed);
        }
        sb.AppendLine("NOTE: Pre-existing 5D3 stats are not backfilled; 7H2 counts only new 7H2-synchronized victories.");
        return sb.ToString();
    }
    private static string PureTests()
    {
        int pass=0,fail=0;
        var sb=new StringBuilder("7H.2 PURE RULE TESTS / IN MEMORY / NO DISK WRITES\n");
        Action<bool,string> check=(ok,msg)=>{ if(ok)pass++;else fail++;sb.AppendLine((ok?"[PASS] ":"[FAIL] ")+msg); };
        var data=new MS5A1SaveData();data.bankGold=50;data.lastCommittedRunId="runA";
        string err;
        check(MS7H2ProgressService.TryApplyRunToData(data,"runA",175,true,out err),"Verified Victory sync A");
        check(MS7H2ProgressService.Wins(data)==1 && MS7H2ProgressService.Bosses(data)==1 && MS7H2ProgressService.EarnedGold(data)==175,"Counters correct");
        check(!MS7H2ProgressService.TryApplyRunToData(data,"runA",175,true,out err),"Duplicate same run blocked");
        data.lastCommittedRunId="runB";
        check(MS7H2ProgressService.TryApplyRunToData(data,"runB",190,false,out err),"Run B no Boss count");
        data.lastCommittedRunId="runC";
        check(MS7H2ProgressService.TryApplyRunToData(data,"runC",185,true,out err),"Run C reaches all three targets");
        check(MS7H2ProgressService.Wins(data)==3 && MS7H2ProgressService.Bosses(data)==2 && MS7H2ProgressService.EarnedGold(data)==550,"Total progress after 3 unique runs");
        check(MS7H2ProgressService.TryApplyClaimToData(data,MS7H2QuestId.ThreeVictories,out err) && data.bankGold==200,"Wins reward adds once");
        check(!MS7H2ProgressService.TryApplyClaimToData(data,MS7H2QuestId.ThreeVictories,out err) && data.bankGold==200,"Double Claim blocked");
        check(MS7H2ProgressService.TryApplyClaimToData(data,MS7H2QuestId.TwoBosses,out err) && data.bankGold==420,"Boss reward adds once");
        check(MS7H2ProgressService.TryApplyClaimToData(data,MS7H2QuestId.EarnFiveHundred,out err) && data.bankGold==620,"Gold earned reward adds once");
        var other=new MS5A1SaveData();other.bankGold=40;
        check(MS7H2ProgressService.Wins(other)==0 && !MS7H2ProgressService.IsComplete(other,MS7H2QuestId.ThreeVictories),"Other profile has no invented progress");
        check(!MS7H2ProgressService.TryApplyClaimToData(other,MS7H2QuestId.ThreeVictories,out err) && other.bankGold==40,"Locked quest cannot grant Gold");
        data.bankGold=int.MaxValue;
        check(!MS7H2ProgressService.TryApplyClaimToData(data,MS7H2QuestId.ThreeVictories,out err),"Claimed marker still blocks even on max Gold");
        data.lastCommittedRunId="runD";
        check(!MS7H2ProgressService.TryApplyRunToData(data,"differentRun",1,false,out err),"Uncommitted/mismatched run rejected");
        sb.AppendLine("PURE PASS="+pass+" FAIL="+fail+" / no files or profiles touched");
        return sb.ToString();
    }
    private void Export()
    {
        string dir=Path.Combine(ProjectRoot,"MS_QAReports","7H2");Directory.CreateDirectory(dir);
        string file=Path.Combine(dir,"MS7H2_ProgressQA_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".txt");
        File.WriteAllText(file,Audit()+"\n"+PureTests()+"\n=== MANUAL QA (NOT AUTOMATED) ===\n"+
            "[ ] New Victory adds wins ONCE, even after reload\n"+
            "[ ] Legit WaveManager Boss defeated + Victory adds bosses ONCE\n"+
            "[ ] Retry/GameOver never adds progress\n"+
            "[ ] Earned Gold tracks saved run earnings, NOT Bank Gold / quest rewards\n"+
            "[ ] Progress still correct after re-entering MainMenu and Unity restart\n"+
            "[ ] Profile 01/02/03 independent; legacy progress not backfilled\n"+
            "[ ] Repeated Claim and invalid/backup-only Save cannot award\n"+
            "[ ] 7H1 QUESTS and 7H1.1 Menu Layout unaffected\n"+
            "[ ] Inventory, Equipment, Achievements, 5D3 stats still function\n"+
            "[ ] UI readable at 1920x1080, 1366x768, 1024x768\n"+
            "RELEASE=PENDING UNTIL UNITY RUNTIME + MANUAL QA\n",new UTF8Encoding(false));
        SetReport("[EXPORTED] "+file+"\n"+Audit());
    }
}
#endif
