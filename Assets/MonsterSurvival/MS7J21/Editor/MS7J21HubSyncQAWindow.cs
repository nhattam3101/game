#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Read-only QA for PlayerHub 7J.2.1. No Scene/Save/Rewards mutation.
public sealed class MS7J21HubSyncQAWindow : EditorWindow
{
    private string report = "7J.2.1: Play MainMenu -> PLAYER HUB, test X and return, then AUDIT.";
    private Vector2 scroll;

    [MenuItem("Tools/MonsterSurvival/UI/7J.2.1 - Open Hub Sync & Navigation QA")]
    private static void Open()
    {
        var win = GetWindow<MS7J21HubSyncQAWindow>("7J.2.1 Hub QA");
        win.minSize = new Vector2(700, 430);
    }
    private void OnGUI()
    {
        EditorGUILayout.LabelField("7J.2.1 / HUB NAVIGATION & READ-ONLY DATA SYNC", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Fixes black screen after X; snapshots are READ-ONLY. No Save repair or retroactive quest progress.", MessageType.Info);
        if (GUILayout.Button("1 - AUDIT CURRENT PLAYER HUB / PROFILE (READ ONLY)", GUILayout.Height(37))) report = BuildReport();
        if (GUILayout.Button("2 - EXPORT 7J.2.1 QA TXT", GUILayout.Height(37))) Export();
        EditorGUILayout.Space();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }
    private static T FindInScene<T>(string name) where T : Component
    {
        Scene s = SceneManager.GetSceneByName(name);
        if (!s.IsValid() || !s.isLoaded) return null;
        foreach (GameObject root in s.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found) return found;
        }
        return null;
    }
    private static int Ready1(MS7H1QuestView[] arr)
    {
        return arr == null ? 0 : arr.Count(q => q != null && q.completed && !q.claimed);
    }
    private static int Ready2(MS7H2QuestView[] arr)
    {
        return arr == null ? 0 : arr.Count(q => q != null && q.completed && !q.claimed);
    }
    private static int Ready3(MS7H3QuestView[] arr)
    {
        return arr == null ? 0 : arr.Count(q => q != null && q.completed && !q.claimed);
    }
    private static string BuildReport()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL / 7J.2.1 HUB SYNC & NAVIGATION QA");
        sb.AppendLine("Time=" + DateTime.Now.ToString("o") + " Unity=" + Application.unityVersion +
                      " Play=" + Application.isPlaying + " ActiveScene=" + SceneManager.GetActiveScene().name);
        var hub = FindInScene<MS7J2PlayerHubPolish>("PlayerHub");
        var entry = FindInScene<MS7J1HubEntry>("MainMenu");
        sb.AppendLine("MainMenuHubEntry=" + (entry ? entry.LastStatus : "MISSING OR NOT LOADED"));
        sb.AppendLine("PlayerHub7J2=" + (hub ? hub.State : "NOT LOADED"));
        if (hub) sb.AppendLine("DashboardVisible=" + hub.DashboardVisible + " ActiveModule=" + hub.ActiveModuleIndex +
                               " LiveSync=" + hub.SyncSummary);
        int slot = MS5A2GoldBank.SelectedSlot;
        sb.AppendLine("SelectedProfile=" + slot);
        if (Application.isPlaying && slot >= 1 && slot <= 3)
        {
            try
            {
                MS5A1SaveData data = null;
                string msg; bool backupOnly;
                bool ok = MS5A1SaveService.TryLoad(slot, out data, out msg, out backupOnly) && data != null;
                sb.AppendLine("Save=" + (ok ? "VALID" : "MISSING OR INVALID") + " BackupOnly=" + backupOnly +
                              " Status=" + msg);
                if (ok)
                {
                    sb.AppendLine("BankGold=" + data.bankGold + " Revision=" + data.revision);
                    var q1 = MS7H1QuestService.ReadSnapshot(slot);
                    var q2 = MS7H2ProgressService.ReadSnapshot(slot);
                    var q3 = MS7H3PeriodicQuestService.ReadSnapshot(slot);
                    sb.AppendLine("7H1 valid=" + q1.valid + " Ready=" + Ready1(q1.quests));
                    sb.AppendLine("7H2 valid=" + q2.valid + " Ready=" + Ready2(q2.quests) +
                                  " Wins=" + q2.wins + " Bosses=" + q2.bosses + " EarnedGold=" + q2.earnedGold);
                    sb.AppendLine("7H3 valid=" + q3.valid + " BackupOnly=" + q3.backupRecovery +
                                  " ClockRollback=" + q3.clockRollback + " Ready=" + Ready3(q3.quests));
                    MS7I4ProfileRecord record; int pending, gold; string result;
                    bool recordOK = MS7I4RecordService.TrySnapshot(slot, out record, out pending, out gold, out result);
                    sb.AppendLine("7I4 valid=" + recordOK + " PendingTickets=" + pending +
                                  " PendingGold=" + gold + " Status=" + result);
                }
            }
            catch (Exception ex) { sb.AppendLine("READ EXCEPTION: " + ex); }
        }
        sb.AppendLine("NO WRITES: This QA window never calls TrySave, TryClaim or recovery.");
        sb.AppendLine("MANUAL PENDING: close X each of seven tabs -> Hub home; BACK -> MainMenu; switch Profile and reopen; claim Gold once; Normal/Endless unaffected.");
        return sb.ToString();
    }
    private void Export()
    {
        try
        {
            report = BuildReport();
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string folder = Path.Combine(projectRoot, "MS_QAReports", "7J21");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "MS7J21_HubSyncQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".txt");
            File.WriteAllText(path, report, Encoding.UTF8);
            report = "[EXPORTED] " + path + "\n" + report;
            Debug.Log("[7J.2.1] " + path);
        }
        catch (Exception ex) { report = "[EXPORT FAILED] " + ex; Debug.LogError(report); }
    }
}
#endif
