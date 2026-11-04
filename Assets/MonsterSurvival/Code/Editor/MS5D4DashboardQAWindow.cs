#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// 5D.4 final audit: never calls TrySave, TryPurchase or any gameplay-changing method.
public sealed class MS5D4DashboardQAWindow : EditorWindow
{
    private readonly List<string> results = new List<string>();
    private Vector2 scroll;
    private int pass, warn, error;
    private string stamp = "Not audited yet";

    public static void ShowWindow()
    {
        var window = GetWindow<MS5D4DashboardQAWindow>("5D.4 Profile Final QA");
        window.minSize = new Vector2(650,410);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("MONSTERSURVIVAL 5D.4 | FINAL DASHBOARD QA", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("READ ONLY: checks MainMenu wiring, statistics sprites, and all 3 Save/Stats files. No save writes. Play Mode visual and button tests must still be done manually.",MessageType.Info);
        EditorGUILayout.LabelField("Last audit: " + stamp);
        EditorGUILayout.LabelField("PASS " + pass + " / WARN " + warn + " / ERROR " + error, EditorStyles.boldLabel);
        using(new EditorGUILayout.HorizontalScope())
        {
            if(GUILayout.Button("AUDIT OPEN SCENE + 3 PROFILES", GUILayout.Height(28))) Audit();
            if(GUILayout.Button("EXPORT TXT REPORT",GUILayout.Height(28))) Export();
            if(GUILayout.Button("CLEAR VIEW",GUILayout.Height(28))) { results.Clear();pass=warn=error=0; }
        }
        scroll=EditorGUILayout.BeginScrollView(scroll);
        foreach(string line in results) EditorGUILayout.SelectableLabel(line,EditorStyles.wordWrappedLabel,GUILayout.MinHeight(24));
        EditorGUILayout.EndScrollView();
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Manual checks: PROFILE opens, 3 tabs work, scroll usable, icons visible, 16:9/4:3/738x723, old buttons remain clickable.",EditorStyles.wordWrappedMiniLabel);
    }

    private void Check(bool ok,string title,string detail = "")
    {
        if(ok)pass++;else error++;
        results.Add((ok ? "PASS " : "ERROR ")+title+(string.IsNullOrEmpty(detail)?"":" — "+detail));
    }
    private void Warning(string text) { warn++;results.Add("WARN "+text); }
    private void Info(string text) { results.Add("INFO "+text); }

    private void Audit()
    {
        results.Clear();pass=warn=error=0;
        Scene scene=SceneManager.GetActiveScene();
        stamp=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        Info("SCENE "+scene.name+(EditorApplication.isPlaying ? " [PLAY MODE]" : " [EDIT MODE]"));
        var a=MS5D4DashboardInstaller.FindInScene<MS5D1ProfileOverview>(scene);
        var b=MS5D4DashboardInstaller.FindInScene<MS5D2ProfilePolish>(scene);
        var c=MS5D4DashboardInstaller.FindInScene<MS5D3StatisticsTab>(scene);
        var d=MS5D4DashboardInstaller.FindInScene<MS5D4ProgressRibbon>(scene);
        if(scene.name=="MainMenu")
        {
            Check(a && a.launcher && a.existingProfileWidget,"5D.1 Profile launcher and widget");
            Check(b && a && b.gameObject==a.gameObject && b.originalOverview==a,"5D.2 Polish attached to same Profile root");
            Check(c && b && c.gameObject==b.gameObject && c.profilePolish==b,"5D.3 Statistics tab attached to same Profile root");
            Check(d && b && c && d.gameObject==b.gameObject && d.profilePolish==b && d.statisticsTab==c,"5D.4 Progress Summary attached");
            if(c)
            {
                UnityEngine.Sprite[] icons={ c.victoriesIcon,c.victoryGoldIcon,c.shadowTitanIcon,c.graveWardenIcon,c.mapsIcon,c.bossBadgesIcon,c.upgradesIcon,c.licensesIcon };
                int spriteCount=0;foreach(var icon in icons) if(icon) spriteCount++;
                Check(spriteCount==8,"5D.3.2 eight Statistics icons","icons="+spriteCount+"/8");
            }
            if(b)
            {
                if(!b.itemRegistry) Warning("5D.2 Item Registry missing: optional generic icon fallback active.");
                if(!b.equipmentCatalog) Warning("5D.2 Equipment Catalog missing: rarity display may use fallback.");
            }
            if(EditorApplication.isPlaying && d)
            {
                Transform runtime = d.transform.Find("MS5D2_PolishedOverlay/ProfileWindow/MS5D3_StatsViewport/MS5D3_StatsRows/MS5D4_ProgressSummary");
                Check(runtime!=null,"5D.4 runtime Progress Summary constructed");
            }
        }
        else Info("Open MainMenu to run the UI component checks; Save/Stats files are checked in any Scene.");

        for(int slot=1;slot<=3;slot++)
        {
            MS5A1SaveData profile;
            string message;
            bool recovered;
            bool loaded=MS5A1SaveService.TryLoad(slot,out profile,out message,out recovered);
            bool fresh=!loaded && !string.IsNullOrEmpty(message) && message.StartsWith("NO_SAVE",StringComparison.Ordinal);
            if(fresh) Info("PROFILE " + slot + ": NEW / no file created by audit.");
            else if(!loaded) Warning("PROFILE "+slot+": SAVE UNAVAILABLE — "+message);
            else
            {
                Check(profile!=null,"Profile "+slot+" save can be read", "revision="+profile.revision+"; Bank Gold="+profile.bankGold);
                if(recovered) Warning("Profile "+slot+" backup-only: potential stale progress. Audit does not restore it.");
                int maps = 1+(Has(profile,"map.sand_dunes")?1:0)+(Has(profile,"map.ancient_ruins")?1:0);
                int bosses = (Has(profile,"achievement.shadow_titan")?1:0)+(Has(profile,"achievement.grave_warden")?1:0);
                int ups=0;
                string[] ids={"vitality","strength","fury","agility"};
                foreach(string id in ids)
                {
                    if(profile.permanentUpgrades==null)continue;
                    foreach(MS5A1UpgradeEntry entry in profile.permanentUpgrades)
                        if(entry!=null && entry.id==id){ ups+=Mathf.Clamp(entry.level,0,10);break; }
                }
                Info("PROFILE "+slot+" progress: maps "+maps+"/3 (purchased/starter), boss badges "+bosses+"/2, upgrades "+ups+"/40");
                if(profile.bankGold<0) Warning("Profile "+slot+": negative Bank Gold; investigate original Save (no fixes attempted).");
            }
            MS5D3StatsData statistics;
            string status;
            bool fromBackup;
            bool statsOk=MS5D3StatsStore.TryRead(slot,out statistics,out status,out fromBackup);
            if(statsOk)
            {
                Check(statistics!=null,"Profile "+slot+" statistics valid","wins="+statistics.victories+" GoldBanked="+statistics.totalVictoryGold+" Titan="+statistics.shadowTitanKills+" Warden="+statistics.graveWardenKills);
                if(fromBackup)Warning("Profile "+slot+" statistics from backup only.");
            }
            else if(!string.IsNullOrEmpty(status) && status.StartsWith("NO_STATS",StringComparison.Ordinal))
                Info("Profile "+slot+" has no 5D.3 statistics yet; historic matches are not guessed.");
            else Warning("Profile "+slot+" Statistics unavailable: "+status);
        }
        Info("MANUAL: Verify 1920x1080, 1280x720, 1024x768 and 738x723 Game View layouts, tab switching and scrollbar.");
        Info("MANUAL: Confirm icon visibility, save read-only behavior, profile switching, old Play/Map/Settings buttons.");
        Info("AUDIT TOTAL: "+pass+" PASS / "+warn+" WARN / "+error+" ERROR. Runtime gameplay is NOT certified by static checks.");
        Repaint();
    }

    private static bool Has(MS5A1SaveData data,string id)
    { return data != null && data.unlockedContentIds != null && data.unlockedContentIds.Contains(id); }

    private void Export()
    {
        if(results.Count==0){EditorUtility.DisplayDialog("5D.4","Run AUDIT first.","OK");return;}
        string path=EditorUtility.SaveFilePanel("Export 5D.4 report",Directory.GetParent(Application.dataPath).FullName,
            "MS5D4_ProfileQA_"+DateTime.Now.ToString("yyyyMMdd_HHmmss"),"txt");
        if(string.IsNullOrEmpty(path))return;
        try
        {
            StringBuilder sb=new StringBuilder();
            sb.AppendLine("MONSTERSURVIVAL 5D.4 FINAL DASHBOARD QA (READ ONLY)");
            sb.AppendLine("Created: "+stamp+" | Unity "+Application.unityVersion);
            sb.AppendLine("PASS "+pass+" / WARN "+warn+" / ERROR "+error);
            foreach(string item in results)sb.AppendLine(item);
            File.WriteAllText(path,sb.ToString(),new UTF8Encoding(false));
            Debug.Log("[5D.4] QA report exported to "+path);
            EditorUtility.RevealInFinder(path);
        }
        catch(Exception ex){Debug.LogError("[5D.4] Export failed: "+ex);}
    }
}
#endif
