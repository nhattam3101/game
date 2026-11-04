#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Additive, backup-first installer. No gameplay/Save core or 7H1/7H2 file overwritten.
public sealed class MS7H3PeriodicQuestInstaller : EditorWindow
{
    private string report="7H.3 Daily / Weekly Quests. Start with Audit.";
    private Vector2 scroll;
    private bool pending;
    [MenuItem("Tools/MonsterSurvival/7H Quests/3 - Open 7H.3 Daily Weekly Quests")]
    private static void Open() { GetWindow<MS7H3PeriodicQuestInstaller>("7H.3 Daily Weekly"); }
    private void Queue(Action action)
    {
        if (pending || action==null) return;
        pending=true;
        EditorApplication.delayCall += () =>
        {
            if(this==null)return;
            try{action();}catch(Exception ex){ report="[ERROR] "+ex; Debug.LogException(ex); }
            finally{pending=false;if(this!=null)Repaint();}
        };
    }
    private void OnGUI()
    {
        scroll=EditorGUILayout.BeginScrollView(scroll);
        try
        {
            EditorGUILayout.LabelField("7H.3 - DAILY / WEEKLY QUESTS",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Adds two Daily + two Weekly quests inside 7H2 PROGRESS. UTC resets; Save/profile remains 5A1. Stop Play Mode before editing scenes.", MessageType.Info);
            if(GUILayout.Button("1 - PREFLIGHT BOTH SCENES (READ ONLY)",GUILayout.Height(34)))Queue(()=>Set(Audit()));
            if(GUILayout.Button("2 - PURE RULE TESTS (NO SAVE WRITES)",GUILayout.Height(34)))Queue(()=>Set(MS7H3PeriodicQuestService.PureRuleTests()));
            if(GUILayout.Button("3 - BACKUP + INSTALL 7H3",GUILayout.Height(36)))Queue(Install);
            if(GUILayout.Button("4 - EXPORT 7H3 QA TXT",GUILayout.Height(34)))Queue(Export);
            if(GUILayout.Button("5 - BACKUP + REMOVE 7H3 COMPONENTS",GUILayout.Height(34)))Queue(Remove);
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Last Report",EditorStyles.boldLabel);
            EditorGUILayout.TextArea(report,GUILayout.MinHeight(300),GUILayout.ExpandHeight(true));
        }
        finally{EditorGUILayout.EndScrollView();}
    }
    private void Set(string s){report=s;Debug.Log(s);}
    private static string Root{get{return Directory.GetParent(Application.dataPath).FullName;}}
    private static string FindScene(string name)
    {
        var paths=new System.Collections.Generic.List<string>();
        foreach(string guid in AssetDatabase.FindAssets("t:Scene "+name))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            if(string.Equals(Path.GetFileNameWithoutExtension(path),name,StringComparison.OrdinalIgnoreCase) &&
                !path.Contains("/Backups/") && !path.Contains("/MS_Backups/")) paths.Add(path);
        }
        if(paths.Count==0)return null;
        paths.Sort(StringComparer.Ordinal);
        Scene current=SceneManager.GetActiveScene();
        if(current.IsValid() && current.name==name && paths.Contains(current.path))return current.path;
        foreach(EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            if(s.enabled && paths.Contains(s.path))return s.path;
        return paths[0];
    }
    private static T Find<T>(Scene s) where T:Component
    {
        foreach(GameObject g in s.GetRootGameObjects())
        {T c=g.GetComponentInChildren<T>(true);if(c!=null)return c;}
        return null;
    }
    private static string Backup(string scenePath,string tag)
    {
        string targetDir=Path.Combine(Root,"MS_Backups","7H3");Directory.CreateDirectory(targetDir);
        string source=Path.Combine(Root,scenePath);
        string dest=Path.Combine(targetDir,tag+"_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".unity");
        File.Copy(source,dest,false);
        if(!File.Exists(dest) || new FileInfo(dest).Length!=new FileInfo(source).Length)
            throw new IOException("Backup mismatch "+dest);
        return dest;
    }
    private static string Audit()
    {
        var sb=new StringBuilder("MONSTERSURVIVAL 7H.3 - DAILY WEEKLY AUDIT / READ ONLY\n");
        sb.AppendLine("Unity="+Application.unityVersion+" Scene="+SceneManager.GetActiveScene().name+" Play="+Application.isPlaying);
        string main=FindScene("MainMenu"),game=FindScene("SampleScene");
        sb.AppendLine("MainMenu="+(main??"MISSING"));sb.AppendLine("SampleScene="+(game??"MISSING"));
        if(main==null || game==null){sb.AppendLine("[FAIL] Missing Scene asset");return sb.ToString();}
        try
        {
            string a=AuditScene<MS7H2ProgressBoard>(main,"7H2 ProgressBoard");
            string b=AuditScene<MS7H2VictoryProgressObserver>(game,"7H2 VictoryObserver");
            string c=AuditScene<MS5A3PersistentRun>(game,"5A3 PersistentRun");
            string d=AuditScene<WaveManager>(game,"WaveManager");
            sb.AppendLine(a);sb.AppendLine(b);sb.AppendLine(c);sb.AppendLine(d);
        }
        catch(Exception ex){sb.AppendLine("[ERROR] Scene inspection: "+ex.Message);}
        var watcher=UnityEngine.Object.FindFirstObjectByType<MS7H3PeriodicVictoryObserver>(FindObjectsInactive.Include);
        if (watcher) sb.AppendLine("[RUNTIME] 7H3 observer="+watcher.Status+" attempts="+watcher.Attempts+" finished="+watcher.Finished);
        for(int i=1;i<=3;i++)
        {
            var s=MS7H3PeriodicQuestService.ReadSnapshot(i);
            sb.AppendLine("[PROFILE "+i+"] valid="+s.valid+" backup="+s.backupRecovery+" rollback="+s.clockRollback+
                " rev="+s.revision+" gold="+s.bankGold+" status="+s.message);
            foreach(var q in s.quests)sb.AppendLine("  "+q.title+" "+q.current+"/"+q.target+" claimed="+q.claimed+" ready="+q.completed);
        }
        sb.AppendLine("[NOTE] Read-only counters do NOT verify buttons, daily/weekly rollover across actual days, Save durability or UI pixels.");
        return sb.ToString();
    }
    private static string AuditScene<T>(string path,string label) where T:Component
    {
        Scene s=SceneManager.GetSceneByPath(path);
        bool temp=!s.IsValid() || !s.isLoaded;
        if(temp)s=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        try
        {
            T c=Find<T>(s);
            return (c!=null?"[PASS] ":"[FAIL] ")+label+" @ "+path+(c!=null?" / "+c.gameObject.name:"");
        }
        finally{if(temp)EditorSceneManager.CloseScene(s,true);}
    }
    private static bool Ready(out string menu,out string game,out string message)
    {
        menu=FindScene("MainMenu");game=FindScene("SampleScene");message="";
        if(Application.isPlaying){message="Stop Play Mode";return false;}
        if(menu==null||game==null){message="Missing actual MainMenu/SampleScene";return false;}
        return true;
    }
    private static bool Dependencies(string menu,string game,out string message)
    {
        message="";
        Scene m=SceneManager.GetSceneByPath(menu),g=SceneManager.GetSceneByPath(game);
        bool tm=!m.IsValid()||!m.isLoaded, tg=!g.IsValid()||!g.isLoaded;
        if(tm)m=EditorSceneManager.OpenScene(menu,OpenSceneMode.Additive);
        if(tg)g=EditorSceneManager.OpenScene(game,OpenSceneMode.Additive);
        try
        {
            bool board=Find<MS7H2ProgressBoard>(m)!=null;
            bool watcher=Find<MS7H2VictoryProgressObserver>(g)!=null;
            bool persistent=Find<MS5A3PersistentRun>(g)!=null;
            bool wave=Find<WaveManager>(g)!=null;
            message="MainMenu 7H2Board="+board+" / SampleScene 7H2Observer="+watcher+
                " 5A3PersistentRun="+persistent+" WaveManager="+wave;
            return board && watcher && persistent && wave;
        }
        finally{if(tg)EditorSceneManager.CloseScene(g,true);if(tm)EditorSceneManager.CloseScene(m,true);}
    }
    private void Install()
    {
        string main,game,err;
        if(!Ready(out main,out game,out err)){Set("[BLOCKED] "+err);return;}
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        if(!Dependencies(main,game,out err)){Set("[BLOCKED] "+err+". No scenes modified.");return;}
        string original=SceneManager.GetActiveScene().path;
        string backupMain=Backup(main,"MainMenu_before_7H3");
        string backupGame=Backup(game,"SampleScene_before_7H3");
        Scene s=EditorSceneManager.OpenScene(main,OpenSceneMode.Single);
        MS7H2ProgressBoard p=Find<MS7H2ProgressBoard>(s);
        if(p.GetComponent<MS7H3PeriodicQuestPanel>()==null)
        {
            Undo.AddComponent<MS7H3PeriodicQuestPanel>(p.gameObject);
            EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);
        }
        s=EditorSceneManager.OpenScene(game,OpenSceneMode.Single);
        WaveManager wave=Find<WaveManager>(s);
        if(wave.GetComponent<MS7H3PeriodicVictoryObserver>()==null)
        {
            var o=Undo.AddComponent<MS7H3PeriodicVictoryObserver>(wave.gameObject);
            o.progressObserver=wave.GetComponent<MS7H2VictoryProgressObserver>();
            o.persistentRun=Find<MS5A3PersistentRun>(s);
            EditorUtility.SetDirty(o);EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);
        }
        EditorSceneManager.OpenScene(string.IsNullOrEmpty(original)?main:original,OpenSceneMode.Single);
        Set("[INSTALLED] 7H3 UI inside PROGRESS / 7H3 observer on existing WaveManager.\n[BACKUP] "+backupMain+"\n[BACKUP] "+backupGame+"\n"+Audit());
    }
    private void Remove()
    {
        string main,game,err;
        if(!Ready(out main,out game,out err)){Set("[BLOCKED] "+err);return;}
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        string original=SceneManager.GetActiveScene().path;
        string bm=Backup(main,"MainMenu_before_7H3_REMOVE"), bg=Backup(game,"SampleScene_before_7H3_REMOVE");
        Scene s=EditorSceneManager.OpenScene(main,OpenSceneMode.Single);
        var ui=Find<MS7H3PeriodicQuestPanel>(s);
        if(ui!=null){Undo.DestroyObjectImmediate(ui);EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);}
        s=EditorSceneManager.OpenScene(game,OpenSceneMode.Single);
        var o=Find<MS7H3PeriodicVictoryObserver>(s);
        if(o!=null){Undo.DestroyObjectImmediate(o);EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);}
        EditorSceneManager.OpenScene(string.IsNullOrEmpty(original)?main:original,OpenSceneMode.Single);
        Set("[REMOVED] Only 7H3 components removed. Saved rewards remain in Profile.\n"+bm+"\n"+bg);
    }
    private void Export()
    {
        string dir=Path.Combine(Root,"MS_QAReports","7H3");Directory.CreateDirectory(dir);
        string path=Path.Combine(dir,"MS7H3_DailyWeeklyQA_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".txt");
        string output=Audit()+"\n"+MS7H3PeriodicQuestService.PureRuleTests()+"\n=== MANUAL QA (PENDING UNTIL USER TESTS) ===\n"+
            "[ ] 7H2 saved Victory increments 7H3 once, re-enter MainMenu does not repeat\n"+
            "[ ] GameOver / Retry increments neither period\n"+
            "[ ] Daily/weekly reset actually observed at UTC boundaries, preserving correct claims\n"+
            "[ ] UTC clock moves backward => no Claim / progress sync\n"+
            "[ ] Two daily and two weekly quests show progress and CLAIM/LOCKED/CLAIMED\n"+
            "[ ] Claim saves Gold + marker once; repeated click/restart cannot claim twice in period\n"+
            "[ ] Profile 01/02/03 independent and backup-only Save rejects writes\n"+
            "[ ] 7H1 QUESTS / 7H2 PROGRESS / MainMenu layout, WaveManager/7F/7G unchanged\n"+
            "[ ] UI 1920x1080, 1366x768, 1024x768 readable; no magic-circle occlusion when closed\n"+
            "[ ] 5A Inventory/Equipment, 5C Achievements, 5D Stats persistence\n"+
            "RELEASE=PENDING UNTIL REAL UNITY PLAYMODE + MANUAL QA\n";
        File.WriteAllText(path,output,new UTF8Encoding(false));Set("[EXPORTED] "+path+"\n"+Audit());
    }
}
#endif
