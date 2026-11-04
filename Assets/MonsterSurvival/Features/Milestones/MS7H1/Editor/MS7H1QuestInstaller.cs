#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MS7H1QuestInstaller : EditorWindow
{
    private string report = "7H.1 QUESTS / MainMenu only. Run AUDIT, PURE TESTS, then INSTALL.";
    private Vector2 scroll;
    private bool actionPending;

    [MenuItem("Tools/MonsterSurvival/7H Quests/1 - Open 7H.1 Long-Term Quest Board")]
    public static void Open() { GetWindow<MS7H1QuestInstaller>("7H.1 Quest Board").minSize = new Vector2(650,470); }

    private void Queue(Action action)
    {
        if (actionPending || action == null) return;
        actionPending = true;
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            try { action(); }
            catch (Exception ex) { report = "[ERROR] " + ex; Debug.LogException(ex); }
            finally { actionPending = false; if (this != null) Repaint(); }
        };
    }
    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        try
        {
            EditorGUILayout.LabelField("7H.1 / LONG-TERM QUESTS", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Separate Profile 01-03. Rewards are committed through existing 5A.1 atomic SaveData, not PlayerPrefs. Install on MainMenu scene only. 7G.5 remains separately pending.", MessageType.Info);
            if (GUILayout.Button("1 - AUDIT PROFILES (READ ONLY)", GUILayout.Height(34))) Queue(() => SetReport(Audit()));
            if (GUILayout.Button("2 - PURE RULE TESTS (NO FILE WRITES)", GUILayout.Height(34))) Queue(() => SetReport(RuleTests()));
            if (GUILayout.Button("3 - BACKUP + INSTALL ON OPEN MAINMENU", GUILayout.Height(34))) Queue(Install);
            if (GUILayout.Button("4 - EXPORT 7H.1 QA TXT", GUILayout.Height(34))) Queue(Export);
            if (GUILayout.Button("5 - BACKUP + REMOVE SCENE UI ONLY", GUILayout.Height(34))) Queue(Remove);
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Last report", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(report, GUILayout.MinHeight(270), GUILayout.ExpandHeight(true));
        }
        finally { EditorGUILayout.EndScrollView(); }
    }
    private void SetReport(string s) { report = s; Debug.Log("[7H.1] " + report); }

    private static string Audit()
    {
        var sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 7H.1 — QUEST AUDIT (READ ONLY)");
        sb.AppendLine("Unity=" + Application.unityVersion + " Scene=" + SceneManager.GetActiveScene().name + " PlayMode=" + Application.isPlaying);
        sb.AppendLine("[SOURCE] MS5A2GoldBank.SelectedSlot / MS5A1SaveService.TryLoad / lastCommittedRunId / unlockedContentIds");
        int selected = MS5A2GoldBank.SelectedSlot;
        sb.AppendLine("SelectedProfile=" + selected);
        var found = UnityEngine.Object.FindObjectsByType<MS7H1QuestBoard>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine("QuestBoard components in loaded scenes=" + found.Length);
        foreach (var instance in found) if (instance != null) sb.AppendLine("  " + instance.gameObject.scene.name + "/" + instance.name);
        for (int slot = 1; slot <= 3; ++slot)
        {
            var s = MS7H1QuestService.ReadSnapshot(slot);
            sb.AppendLine("[PROFILE " + slot + "] valid=" + s.valid + " recovery=" + s.backupRecovery + " gold=" + (s.valid ? s.gold.ToString() : "UNKNOWN") + " rev=" + s.revision + " status=" + s.message);
            foreach (var q in s.quests)
                sb.AppendLine("  " + q.id + " completed=" + q.completed + " claimed=" + q.claimed + " reward=" + q.reward);
        }
        sb.AppendLine("READ ONLY — does not prove claim, Gold persist/reload, GameView UI or Achievement event.");
        return sb.ToString();
    }

    private static string RuleTests()
    {
        var lines = new StringBuilder("MONSTERSURVIVAL 7H.1 — PURE IN-MEMORY QUEST RULES\n");
        int pass=0, fail=0;
        Action<bool,string> test=(okay,name)=> { lines.AppendLine((okay?"[PASS] ":"[FAIL] ")+name); if(okay)pass++;else fail++; };
        var a = new MS5A1SaveData();
        a.bankGold=100;
        a.lastCommittedRunId="qa-run";
        a.unlockedContentIds = new List<string>();
        string error;
        test(MS7H1QuestService.IsCompleted(a,MS7H1QuestId.FirstVictory), "Victory evidence from committed run ID");
        test(MS7H1QuestService.TryApplyToData(a,MS7H1QuestId.FirstVictory,out error), "Claim first victory in memory");
        test(a.bankGold==160 && MS7H1QuestService.HasId(a,MS7H1QuestService.Marker(MS7H1QuestId.FirstVictory)), "Gold and marker updated TOGETHER");
        test(!MS7H1QuestService.TryApplyToData(a,MS7H1QuestId.FirstVictory,out error) && a.bankGold==160, "Repeat claim blocked without extra Gold");
        test(!MS7H1QuestService.TryApplyToData(a,MS7H1QuestId.DefeatTitan,out error), "Boss quest locked without 5C achievement");
        a.unlockedContentIds.Add("achievement.shadow_titan");
        test(MS7H1QuestService.TryApplyToData(a,MS7H1QuestId.DefeatTitan,out error) && a.bankGold==280, "Boss unlock allows reward exactly once");
        test(!MS7H1QuestService.TryApplyToData(a,MS7H1QuestId.DefeatTitan,out error) && a.bankGold==280, "Boss reward idempotent");
        var b=new MS5A1SaveData(); b.bankGold=25; b.unlockedContentIds=new List<string>();
        test(!MS7H1QuestService.IsCompleted(b,MS7H1QuestId.FirstVictory), "Fresh Profile has no invented Victory");
        test(b.bankGold==25 && !MS7H1QuestService.HasId(b,MS7H1QuestService.Marker(MS7H1QuestId.FirstVictory)), "Other Profile remains isolated");
        var c=new MS5A1SaveData(); c.lastCommittedRunId="x"; c.bankGold=int.MaxValue; c.unlockedContentIds=new List<string>();
        test(!MS7H1QuestService.TryApplyToData(c,MS7H1QuestId.FirstVictory,out error) && c.bankGold==int.MaxValue, "Overflow blocks Gold write");
        test(!MS7H1QuestService.TryApplyToData(null,MS7H1QuestId.FirstVictory,out error), "Missing SaveData fail closed");
        test(!MS7H1QuestService.TryApplyToData(b,(MS7H1QuestId)999,out error), "Unknown quest ID rejected");
        test(MS7H1QuestService.Reward(MS7H1QuestId.FirstVictory)==60 && MS7H1QuestService.Reward(MS7H1QuestId.DefeatWarden)==160,"Reward catalog stable");
        lines.AppendLine("PURE PASS="+pass+" FAIL="+fail+" (no disk IO)");
        return lines.ToString();
    }

    private static bool IsMainMenuScene(out string msg)
    {
        var scene=SceneManager.GetActiveScene();
        msg="";
        if (Application.isPlaying) { msg="STOP Play Mode first"; return false; }
        if (!scene.IsValid() || scene.name!="MainMenu" || string.IsNullOrEmpty(scene.path))
        { msg="Open the existing MainMenu scene (not SampleScene) first."; return false; }
        if (!File.Exists(scene.path)) { msg="Saved scene file not found: " +scene.path; return false; }
        return true;
    }
    private static string BackupScene()
    {
        var scene=SceneManager.GetActiveScene();
        string root=Directory.GetParent(Application.dataPath).FullName;
        string dir=Path.Combine(root,"MS_Backups","7H1");
        Directory.CreateDirectory(dir);
        string target=Path.Combine(dir,"MainMenu_before_7H1_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".unity");
        File.Copy(scene.path,target,false);
        return target;
    }
    private void Install()
    {
        string error;
        if(!IsMainMenuScene(out error)){ SetReport("[BLOCKED] "+error);return; }
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { SetReport("[CANCELLED] Scene not saved; no files modified"); return; }
        var existing=UnityEngine.Object.FindObjectsByType<MS7H1QuestBoard>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        foreach(var item in existing) if(item.gameObject.scene==SceneManager.GetActiveScene())
        { SetReport("[NO OP] 7H.1 already installed in MainMenu. No duplicates or data edits.\n"+Audit()); return; }
        string backup=BackupScene(); // MUST finish before scene modification
        var go=new GameObject("MS7H1_QuestBoard [MainMenu]");
        Undo.RegisterCreatedObjectUndo(go,"Install 7H1 Quest Board");
        Undo.AddComponent<MS7H1QuestBoard>(go);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        SetReport("[INSTALLED] Added Quest Board to MainMenu.\n[BACKUP] " + backup + "\nOpen MainMenu in Play Mode and click QUESTS at top-right.\n"+Audit());
    }
    private void Remove()
    {
        string error;
        if(!IsMainMenuScene(out error)) { SetReport("[BLOCKED] "+error);return; }
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { SetReport("[CANCELLED] No change"); return; }
        var existing=UnityEngine.Object.FindObjectsByType<MS7H1QuestBoard>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        var hits=new List<MS7H1QuestBoard>();
        foreach(var item in existing) if(item!=null && item.gameObject.scene==SceneManager.GetActiveScene()) hits.Add(item);
        if(hits.Count==0){ SetReport("[NO OP] No 7H.1 Quest Board in MainMenu");return; }
        string backup=BackupScene();
        foreach(var item in hits)
        {
            if (item.gameObject.name == "MS7H1_QuestBoard [MainMenu]")
                Undo.DestroyObjectImmediate(item.gameObject);
            else Undo.DestroyObjectImmediate(item); // never destroy a pre-existing host GameObject
        }
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        SetReport("[REMOVED UI] " + hits.Count + " Quest Board object(s). Existing 5A SaveData and earned Quest Gold unchanged.\n[BACKUP] " + backup);
    }
    private void Export()
    {
        string root=Directory.GetParent(Application.dataPath).FullName;
        string dir=Path.Combine(root,"MS_QAReports","7H1");
        Directory.CreateDirectory(dir);
        string path=Path.Combine(dir,"MS7H1_QuestQA_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".txt");
        string output=Audit()+"\n"+RuleTests()+"\n=== MANUAL QA (NOT AUTO-CERTIFIED) ===\n"+
            "[ ] Claim First Victory once; Bank Gold +60, revision +1\n"+
            "[ ] Close and reopen Unity: claimed marker persists\n"+
            "[ ] Repeated / rapid Claim does not award twice\n"+
            "[ ] Boss achievements via legitimate Wave 5 unlock corresponding quest\n"+
            "[ ] Switching Profile 01/02/03 shows separate quests and Gold\n"+
            "[ ] Backup-only / invalid Profile never auto-writes save\n"+
            "[ ] Existing 5A Victory Gold and Inventory/Equipment unchanged\n"+
            "[ ] 5C Achievement Gallery still works, no false achievements\n"+
            "[ ] MainMenu Map Select/Start/Settings/Shop/Profiles work\n"+
            "RELEASE=PENDING UNTIL MANUAL QA; 7G.5 BLOCKERS TRACKED SEPARATELY\n";
        File.WriteAllText(path,output,new UTF8Encoding(false));
        SetReport("[EXPORTED] " +path+"\n\n"+output);
        EditorUtility.RevealInFinder(path);
    }
}
#endif
