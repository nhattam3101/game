#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Read-only diagnosis + explicitly opted-in, backed-up recovery of one VERIFIED last run.
// Does not install components, modify scenes or grant Bank Gold.
public sealed class MS7H33VictorySyncRepairWindow : EditorWindow
{
    private Vector2 scroll;
    private string report = "Click AUDIT. For recovery, first back up real SaveData.";
    private bool queued;

    [MenuItem("Tools/MonsterSurvival/7H Quests/3.3 - Open Victory Sync Repair")]
    private static void Open() { GetWindow<MS7H33VictorySyncRepairWindow>("7H3.3 Sync Repair"); }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        try
        {
            GUILayout.Label("7H.3.3 - DAILY / WEEKLY VICTORY SYNC", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Do not create a fresh Save, re-install quests or alter Gold to fix missing progress. AUDIT is read-only. Recovery is an explicit one-time operation on the verified latest Victory only.", MessageType.Info);
            if (GUILayout.Button("1 - AUDIT CURRENT PROFILE (READ ONLY)", GUILayout.Height(35)))
                Queue(Audit);
            if (GUILayout.Button("2 - BACKUP + RECOVER LATEST VERIFIED VICTORY (PLAY MODE MAINMENU)", GUILayout.Height(40)))
                Queue(Recover);
            if (GUILayout.Button("3 - EXPORT 7H3.3 QA TXT", GUILayout.Height(35)))
                Queue(Export);
            EditorGUILayout.LabelField("Last Report", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(report, GUILayout.MinHeight(340));
        }
        finally { EditorGUILayout.EndScrollView(); }
    }

    private void Queue(Action action)
    {
        if (queued) return;
        queued=true;
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            try { action(); }
            catch (Exception ex) { report="ERROR: "+ex; Debug.LogException(ex); }
            finally { queued=false; if (this != null) Repaint(); }
        };
    }

    private void Audit()
    {
        int slot=MS5A2GoldBank.SelectedSlot;
        var sb=new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 7H.3.3 / READ ONLY AUDIT");
        sb.AppendLine("Unity="+Application.unityVersion+" Scene="+SceneManager.GetActiveScene().name+
                      " Play="+Application.isPlaying+" Profile="+slot);
        MS5A1SaveData data=null; string info=""; bool recovered=false;
        if (!MS5A1SaveService.TryLoad(slot,out data,out info,out recovered) || data==null)
        { sb.AppendLine("[BLOCKED] Save unreadable: "+info);report=sb.ToString();Debug.Log(report);return; }
        sb.AppendLine("BankGold="+data.bankGold+" Revision="+data.revision+" BackupRecovery="+recovered);
        sb.AppendLine("LastCommittedRunId="+data.lastCommittedRunId);
        bool h2=data.unlockedContentIds!=null && !string.IsNullOrEmpty(data.lastCommittedRunId) &&
                data.unlockedContentIds.Contains("quest.7h2.progress.last."+data.lastCommittedRunId);
        sb.AppendLine("7H2 latest-run marker="+(h2?"VERIFIED":"MISSING"));
        sb.AppendLine("7H2 wins="+MS7H2ProgressService.Wins(data)+
                      " Bosses="+MS7H2ProgressService.Bosses(data)+
                      " EarnedGold="+MS7H2ProgressService.EarnedGold(data));
        MS7H3Snapshot snap=MS7H3PeriodicQuestService.ReadSnapshot(slot);
        sb.AppendLine("7H3 Save="+snap.message+" Daily="+snap.quests[0].current+"/1"+
                      " WeeklyWins="+snap.quests[2].current+"/5"+
                      " DailyGold="+snap.quests[1].current+"/150"+
                      " WeeklyBoss="+snap.quests[3].current+"/3");
        sb.AppendLine("[IMPORTANT] Missing progress may be caused by 7H2 saving on scene unload after 7H3's last Update.");
        sb.AppendLine("[SAFETY] Recovery needs a recent, user-confirmed missed Victory. It does not guess historic bosses or Gold.");
        report=sb.ToString();Debug.Log(report);
    }

    private static bool BackupRealSave(out string path, out string reason)
    {
        path="";reason="";
        string source=Path.Combine(Application.persistentDataPath,"MonsterSurvival/SaveData");
        if (!Directory.Exists(source))
        { reason="Real SaveData folder not found at "+source+". No Save written.";return false; }
        string root=Directory.GetParent(Application.dataPath).FullName;
        path=Path.Combine(root,"MS_Backups/7H33/SaveData_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
        try
        {
            string[] files=Directory.GetFiles(source,"*",SearchOption.AllDirectories);
            if (files.Length==0 || files.Length>1000)
            { reason="Unusual SaveData contents (files="+files.Length+"). Backup manually; no Save written.";return false; }
            Directory.CreateDirectory(path);
            foreach (string original in files)
            {
                string relative=original.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
                string dest=Path.Combine(path,relative);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(original,dest,false);
            }
            reason="Backup OK: "+files.Length+" files at "+path;
            return true;
        }
        catch (Exception ex) { reason="Backup failed: "+ex.Message;return false; }
    }

    private void Recover()
    {
        if (!Application.isPlaying || SceneManager.GetActiveScene().name!="MainMenu")
        { report="[BLOCKED] Enter Play Mode on MainMenu before recovery. No changes made.";Debug.LogWarning(report);return; }
        int slot=MS5A2GoldBank.SelectedSlot;
        if (!EditorUtility.DisplayDialog("7H.3.3 - RECOVER ONE LATEST VICTORY",
            "Confirm: You recently won ONE match in this exact Profile while 7H3 was installed, and its Daily/Weekly did not increase.\n\n"+
            "The recovery verifies the latest 7H2 saved Run ID, backs up ALL real SaveData, and counts no earlier matches. When no exact prior 7H3 checkpoint exists, it counts only 1 win (not unverified Boss/earned Gold). It does NOT grant Bank Gold.\n\nProfile: "+slot,
            "Confirm + Back Up", "Cancel")) return;
        string backup,why;
        if (!BackupRealSave(out backup,out why))
        { report="[BLOCKED] "+why;Debug.LogError(report);return; }
        string outcome;
        bool success=MS7H3PeriodicQuestService.TryRecoverLatestCommittedVictory(slot,out outcome);
        report="[7H3.3] "+(success?"RECOVERED":"NO WRITE / BLOCKED")+"\n"+why+"\n"+outcome;
        Debug.Log(report);
        if (success) Audit();
    }

    private void Export()
    {
        string root=Directory.GetParent(Application.dataPath).FullName;
        string dir=Path.Combine(root,"MS_QAReports/7H33");
        Directory.CreateDirectory(dir);
        string path=Path.Combine(dir,"MS7H33_VictorySyncQA_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".txt");
        File.WriteAllText(path,report,Encoding.UTF8);
        Debug.Log("[7H3.3] QA exported: "+path);
    }
}
#endif
