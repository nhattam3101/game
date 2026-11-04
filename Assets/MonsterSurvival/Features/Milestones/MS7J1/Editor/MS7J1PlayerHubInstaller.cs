#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Install a real PlayerHub.unity scene and a single entry point on the EXISTING MainMenu.
// No legacy Save/Profile/Quest/Endless module is moved, copied or rewritten.
public sealed class MS7J1PlayerHubInstaller : EditorWindow
{
    private string report="7J.1 Player Hub: run AUDIT, then INSTALL in Edit Mode.";
    private Vector2 scroll;
    private const string HubFile="Assets/MonsterSurvival/Content/Scenes/PlayerHub.unity";
    private const string MenuName="MainMenu";

    [MenuItem("Tools/MonsterSurvival/UI/7J.1 - Open Player Hub Installer")]
    private static void Open(){var window=GetWindow<MS7J1PlayerHubInstaller>("7J.1 Player Hub");window.minSize=new Vector2(660,470);}

    private void OnGUI()
    {
        EditorGUILayout.LabelField("7J.1  /  PLAYER HUB SCENE",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("One NEW additive PlayerHub Scene; MainMenu remains the active host so existing MainMenu-only Quest/Save panels still work. No save/reward writes.",MessageType.Info);
        if(GUILayout.Button("1 - AUDIT MAINMENU + INFORMATION TABS (READ ONLY)",GUILayout.Height(31)))Audit();
        if(GUILayout.Button("2 - BACKUP + CREATE PLAYERHUB SCENE + CLEAN MAINMENU",GUILayout.Height(37)))Install();
        if(GUILayout.Button("3 - EXPORT 7J.1 QA TXT",GUILayout.Height(31)))Export();
        if(GUILayout.Button("4 - BACKUP + REMOVE MAINMENU HUB ENTRY (ROLLBACK)",GUILayout.Height(31)))Rollback();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Latest report",EditorStyles.boldLabel);
        scroll=EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report,GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }
    private static bool FindMenuScene(out string path,out string why)
    {
        path="";why="";
        string[] all=AssetDatabase.FindAssets("t:Scene").Select(AssetDatabase.GUIDToAssetPath)
            .Where(p=>p.EndsWith("/MainMenu.unity",StringComparison.OrdinalIgnoreCase)).Distinct().ToArray();
        if(all.Length!=1){why="Require exactly one MainMenu.unity in Assets; found "+all.Length;return false;}
        path=all[0];return true;
    }
    private static bool FindMenuInScene(Scene scene,out MMV3MenuController menu,out string why)
    {
        menu=null; why="";
        var list=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MMV3MenuController>(true)).ToArray();
        if(list.Length!=1||!list[0].homePage||!list[0].mapsPage){why="Missing UNIQUE MMV3MenuController with Home/Map pages";return false;}
        menu=list[0];return true;
    }
    private static Scene LoadScene(string path,out bool opened)
    {
        Scene existing=SceneManager.GetSceneByPath(path);
        opened=!existing.IsValid()||!existing.isLoaded;
        return opened?EditorSceneManager.OpenScene(path,OpenSceneMode.Additive):existing;
    }
    private static string SceneInfo(Scene scene)
    {
        string[] names={"MS5A4_BTN_Open","MS5B2_BTN_Upgrades","MS5C4_BTN_Achievements","MS5D1_BTN_ProfileOverview"};
        var sb=new StringBuilder();
        foreach(string n in names)
        {
            int count=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Button>(true))
                .Count(b=>b.name==n);
            sb.AppendLine("["+(count==1?"PASS":"WARN")+"] "+n+" buttons="+count);
        }
        sb.AppendLine("7H1 QuestBoard="+scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MS7H1QuestBoard>(true)).Count());
        sb.AppendLine("7H2 ProgressBoard="+scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MS7H2ProgressBoard>(true)).Count());
        sb.AppendLine("7H3 PeriodicPanel="+scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MS7H3PeriodicQuestPanel>(true)).Count());
        sb.AppendLine("7I4 RecordsMenu="+scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MS7I4RecordsMenu>(true)).Count());
        return sb.ToString();
    }
    private void Audit()
    {
        if(EditorApplication.isPlaying){report="[BLOCKED] Exit Play Mode first to audit/edit Scenes.";return;}
        string path,reason;
        if(!FindMenuScene(out path,out reason)){report="[BLOCKED] "+reason;return;}
        bool opened=false;Scene m=default(Scene);
        try
        {
            m=LoadScene(path,out opened);
            MMV3MenuController menu;
            bool ok=FindMenuInScene(m,out menu,out reason);
            string hub=HubFile;
            bool exists=File.Exists(Path.GetFullPath(hub));
            bool inBuild=EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path==hub);
            report="7J.1 / READ-ONLY AUDIT\nMainMenu="+path+"\n"+
                (ok?"[PASS] ":"[BLOCKED] ")+reason+"\n"+
                (ok?SceneInfo(m):"")+"PlayerHub scene exists="+exists+" in Build Profiles="+inBuild+"\n"+
                (ok?"Hub Entry="+(menu.GetComponent<MS7J1HubEntry>()?"INSTALLED":"MISSING"):"")+"\nNo Save/Profile or Scene writes in Audit.";
            Debug.Log(report);
        }
        catch(Exception e){report="AUDIT EXCEPTION: "+e;}
        finally{if(opened&&m.IsValid())EditorSceneManager.CloseScene(m,true);}
    }
    private static bool Backup(string source,bool hubExists,out string folder,out string error)
    {
        folder="";error="";
        try
        {
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            string main=Path.Combine(project,source);
            if(!File.Exists(main)) {error="MainMenu.unity not on disk";return false;}
            folder=Path.Combine(project,"MS_Backups","7J1",DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            Directory.CreateDirectory(folder);
            File.Copy(main,Path.Combine(folder,"MainMenu_BEFORE_7J1.unity"));
            if(hubExists)File.Copy(Path.Combine(project,HubFile),Path.Combine(folder,"PlayerHub_BEFORE_7J1.unity"));
            File.WriteAllLines(Path.Combine(folder,"BuildScenes_BEFORE_7J1.txt"),
                EditorBuildSettings.scenes.Select(s=>(s.enabled?"ON":"OFF")+" "+s.path).ToArray());
            return true;
        }
        catch(Exception e){error=e.Message;return false;}
    }
    private static bool BuildHubScene(out string message)
    {
        message="";
        if(File.Exists(Path.GetFullPath(HubFile)))
        {
            bool opened=false;Scene existing=default(Scene);
            try
            {
                existing=LoadScene(HubFile,out opened);
                int count=existing.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MS7J1PlayerHubScene>(true)).Count();
                if(count!=1){message="Existing PlayerHub.unity has no unique MS7J1PlayerHubScene; refusing overwrite";return false;}
                message="Existing PlayerHub.unity accepted (idempotent)";return true;
            }
            finally{if(opened&&existing.IsValid())EditorSceneManager.CloseScene(existing,true);}
        }
        if(!Directory.Exists("Assets/MonsterSurvival/Content/Scenes"))Directory.CreateDirectory("Assets/MonsterSurvival/Content/Scenes");
        Scene created=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        try
        {
            var root=new GameObject("MS7J1_PlayerHubSceneRoot");
            if(root.scene!=created)SceneManager.MoveGameObjectToScene(root,created);
            root.AddComponent<MS7J1PlayerHubScene>();
            if(!EditorSceneManager.SaveScene(created,HubFile)){message="Cannot save PlayerHub.unity";return false;}
            message="Created "+HubFile;
            return true;
        }
        finally{if(created.IsValid())EditorSceneManager.CloseScene(created,true);}
    }
    private static void BuildSettingsEnable(string menuPath)
    {
        var list=EditorBuildSettings.scenes.ToList();
        if(!list.Any(s=>s.path==menuPath&&s.enabled))
            list.Insert(0,new EditorBuildSettingsScene(menuPath,true));
        if(!list.Any(s=>s.path==HubFile))
        {
            int index=list.FindIndex(s=>s.path==menuPath);
            list.Insert(index<0?list.Count:index+1,new EditorBuildSettingsScene(HubFile,true));
        }
        else for(int i=0;i<list.Count;i++)if(list[i].path==HubFile)
            list[i]=new EditorBuildSettingsScene(HubFile,true);
        EditorBuildSettings.scenes=list.ToArray();
    }
    private void Install()
    {
        if(EditorApplication.isPlaying){report="[BLOCKED] Exit Play Mode";return;}
        string path,reason;
        if(!FindMenuScene(out path,out reason)){report="[BLOCKED] "+reason;return;}
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {report="[CANCELLED] Save your Scene first";return;}
        bool opened=false;Scene main=default(Scene);
        try
        {
            main=LoadScene(path,out opened);
            MMV3MenuController menu;
            if(!FindMenuInScene(main,out menu,out reason)){report="[BLOCKED] "+reason;return;}
            string backup;
            if(!Backup(path,File.Exists(Path.GetFullPath(HubFile)),out backup,out reason))
            {report="[BLOCKED] BACKUP FAILED: "+reason;return;}
            string sceneResult;
            if(!BuildHubScene(out sceneResult))
            {report="[BLOCKED] "+sceneResult+"\nBackup="+backup;return;}
            MS7J1HubEntry entry=menu.GetComponent<MS7J1HubEntry>();
            if(!entry)entry=Undo.AddComponent<MS7J1HubEntry>(menu.gameObject);
            Undo.RecordObject(entry,"7J1 Hub binding");
            entry.menu=menu;entry.cleanMainMenu=true;entry.playerHubSceneName=HubFile;
            EditorUtility.SetDirty(entry);
            EditorSceneManager.MarkSceneDirty(main);
            if(!EditorSceneManager.SaveScene(main))throw new IOException("Failed to save MainMenu");
            AssetDatabase.Refresh();
            BuildSettingsEnable(path);
            report="[INSTALLED] 7J.1 PLAYER HUB\n"+sceneResult+"\nMainMenu entry added.\n"+
                "Old information button references retained; clean-up is visual only at runtime.\n"+
                "MainMenu + PlayerHub enabled in Build Profiles.\nBackup="+backup+"\n"+
                "NO changes to WaveManager, SampleScene, existing Player UI scripts, Save, rewards, or quest records.";
            Debug.Log(report);
        }
        catch(Exception e){report="[FAILED] "+e+"\nCheck backup in MS_Backups/7J1";Debug.LogError(report);}
        finally{if(opened&&main.IsValid())EditorSceneManager.CloseScene(main,true);}
    }
    private void Rollback()
    {
        if(EditorApplication.isPlaying){report="[BLOCKED] Exit Play Mode";return;}
        string path,reason;
        if(!FindMenuScene(out path,out reason)){report="[BLOCKED] "+reason;return;}
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        bool opened=false;Scene main=default(Scene);
        try
        {
            main=LoadScene(path,out opened);
            MMV3MenuController menu;
            if(!FindMenuInScene(main,out menu,out reason)){report="[BLOCKED] "+reason;return;}
            string backup;
            if(!Backup(path,File.Exists(Path.GetFullPath(HubFile)),out backup,out reason))
            {report="[BLOCKED] Backup failed: "+reason;return;}
            var entry=menu.GetComponent<MS7J1HubEntry>();
            if(entry)Undo.DestroyObjectImmediate(entry);
            EditorSceneManager.MarkSceneDirty(main);
            EditorSceneManager.SaveScene(main);
            EditorBuildSettings.scenes=EditorBuildSettings.scenes.Where(s=>s.path!=HubFile).ToArray();
            report="[ROLLED BACK] Removed only 7J1 MainMenu component and PlayerHub build entry.\n"+
                "PlayerHub.unity was NOT deleted; no Save/Profile/Quest touched.\nBackup="+backup;
            Debug.Log(report);
        }
        catch(Exception e){report="[ROLLBACK FAILED] "+e;Debug.LogError(report);}
        finally{if(opened&&main.IsValid())EditorSceneManager.CloseScene(main,true);}
    }
    private void Export()
    {
        try
        {
            string directory=Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath,"..")),"MS_QAReports","7J1");
            Directory.CreateDirectory(directory);
            string file=Path.Combine(directory,"MS7J1_PlayerHubQA_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".txt");
            Scene hubScene=SceneManager.GetSceneByName("PlayerHub");
            bool hubLoaded=hubScene.IsValid() && hubScene.isLoaded;
            MS7J1PlayerHubScene hub=null;
            if(hubLoaded)
                hub=hubScene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MS7J1PlayerHubScene>(true)).FirstOrDefault();
            Scene mainScene=SceneManager.GetSceneByName("MainMenu");
            MS7J1HubEntry entry=null;
            if(mainScene.IsValid() && mainScene.isLoaded)
                entry=mainScene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MS7J1HubEntry>(true)).FirstOrDefault();
            string capture="MONSTERSURVIVAL 7J.1 PLAYER HUB QA\nTime="+DateTime.Now.ToString("o")+
                " Unity="+Application.unityVersion+" Play="+Application.isPlaying+
                " ActiveScene="+SceneManager.GetActiveScene().name+"\n"+
                "PlayerHubLoaded="+hubLoaded+" HubState="+(hub?hub.State:"N/A")+
                " MainMenuEntry="+(entry?entry.LastStatus:"N/A")+"\n"+report+"\n"+
                "MANUAL PENDING: MainMenu single PLAYER HUB button; all 7 modules; original claims; Profile isolation; back/unload; Normal/Endless; UI at 1920x1080,1280x720,1024x768; Save Gold/revision unchanged until actual claim.\n";
            File.WriteAllText(file,capture,Encoding.UTF8);
            report="[EXPORTED] "+file+"\n"+report;
            Debug.Log(report);
        }
        catch(Exception e){report="[EXPORT FAILED] "+e.Message;}
    }
}
#endif
