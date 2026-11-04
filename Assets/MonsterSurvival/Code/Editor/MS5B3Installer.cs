#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MS5B3Installer
{
    private const string Menu = "Tools/MonsterSurvival/5B Permanent Upgrades/";
    private const string BackupFolder = "Assets/MonsterSurvival/Features/Progression/Upgrade5B3/Backups";

    [MenuItem(Menu + "6 - INSTALL 5B.3 Permanent Combat Bonuses (OPEN SampleScene)")]
    public static void Install()
    {
        if (Application.isPlaying) { Debug.LogWarning("[5B.3] Stop Play Mode first."); return; }
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "SampleScene")
        { EditorUtility.DisplayDialog("5B.3", "Please open SampleScene first. No changes made.", "OK"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!EditorUtility.DisplayDialog("5B.3 INSTALL",
            "Add a single read-only permanent upgrade bonus layer to the Player in SampleScene?\n\n" +
            "This does not edit PlayerStats/Health source, profile JSON, Boss, Wave, or Equipment.",
            "INSTALL", "CANCEL")) return;
        try
        {
            PlayerStats player = FindPlayerStats(scene);
            if (!player) throw new InvalidOperationException("Player with PlayerStats and tag Player not found.");
            Health health = player.GetComponent<Health>();
            if (!health) throw new InvalidOperationException("Player Health component missing.");
            MS5A3PersistentRun run = FindSceneComponent<MS5A3PersistentRun>(scene);
            if (!run || !run.enabled)
                throw new InvalidOperationException("5A.3 persistent run component is missing or disabled. Install/fix 5A.3 before 5B.3.");
            MS5B3PermanentBonusApplier[] attached = player.GetComponents<MS5B3PermanentBonusApplier>();
            if (attached.Length > 0)
            {
                Debug.Log("[5B.3] Already installed on Player; no duplicate created.");
                EditorUtility.DisplayDialog("5B.3", "Already installed. No changes made.", "OK");
                return;
            }
            MS5B3PermanentBonusApplier existing = FindSceneComponent<MS5B3PermanentBonusApplier>(scene);
            if (existing)
                throw new InvalidOperationException("An applier is already present elsewhere in the scene. Remove duplicate manually before installing.");
            EnsureFolder(BackupFolder);
            string backup = BackupFolder + "/SampleScene_BEFORE_5B3.unity";
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(backup))
            {
                if (!AssetDatabase.CopyAsset(scene.path, backup)) throw new IOException("Failed to back up SampleScene.");
                AssetDatabase.ImportAsset(backup);
            }
            MS5B3PermanentBonusApplier component = Undo.AddComponent<MS5B3PermanentBonusApplier>(player.gameObject);
            component.playerStats = player;
            component.playerHealth = health;
            EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[5B.3] INSTALLED on Player. Backup: " + backup);
            EditorUtility.DisplayDialog("5B.3", "Installed. Open MainMenu, select a profile, start SampleScene and check PlayerStats + Health.", "OK");
        }
        catch (Exception ex) { Debug.LogError("[5B.3] INSTALL FAILED: " + ex); EditorUtility.DisplayDialog("5B.3 ERROR", ex.Message, "OK"); }
    }

    [MenuItem(Menu + "7 - AUDIT 5B.3 Player & Profiles (READ ONLY)")]
    public static void Audit()
    {
        Scene scene = SceneManager.GetActiveScene();
        PlayerStats player = FindPlayerStats(scene);
        Health health = player ? player.GetComponent<Health>() : null;
        MS5B3PermanentBonusApplier applier = player ? player.GetComponent<MS5B3PermanentBonusApplier>() : null;
        var run = FindSceneComponent<MS5A3PersistentRun>(scene);
        Debug.Log("[5B.3 AUDIT] Scene="+scene.name+
                  " | Player="+(player?"PASS":"MISSING")+
                  " | Health="+(health?"PASS":"MISSING")+
                  " | 5A.3="+(run && run.enabled?"PASS":"MISSING/DISABLED")+
                  " | 5B.3="+(applier?"PASS":"MISSING")+
                  " | 5B3-count="+CountSceneComponents<MS5B3PermanentBonusApplier>(scene));
        for (int slot=1;slot<=3;slot++)
        {
            MS5B1PermanentUpgrades.Snapshot snapshot;
            string msg;
            if (!MS5B1PermanentUpgrades.TryGetSnapshot(slot, out snapshot, out msg))
            {
                Debug.LogWarning("[5B.3 AUDIT] Profile "+slot+" BLOCKED: "+msg);continue;
            }
            MS5B3BonusValues bonus;
            string error;
            if (!MS5B3BonusMath.TryFromSnapshot(snapshot,out bonus,out error))
            {
                Debug.LogWarning("[5B.3 AUDIT] Profile "+slot+" INVALID BONUS: "+error);continue;
            }
            Debug.Log("[5B.3 AUDIT] Profile "+slot+" (read-only) | Gold="+snapshot.bankGold+
                      " Rev="+snapshot.revision+" | V="+bonus.vitalityLevel+" S="+bonus.strengthLevel+
                      " F="+bonus.furyLevel+" A="+bonus.agilityLevel+
                      " | HP+"+bonus.maxHealth+" Damage+"+bonus.damage+
                      " AtkSpeed+"+bonus.attackSpeed+" Move+"+bonus.moveSpeed);
        }
        if (Application.isPlaying && applier)
            Debug.Log("[5B.3 LIVE] LockedProfile="+applier.LockedProfileSlot+
                      " ReadOK="+applier.ProfileReadSuccessfully+
                      " Applied="+applier.IsApplied+
                      " Status="+applier.Status+
                      " | Current HP="+(health?health.CurrentHealth.ToString("F2"):"n/a")+
                      " Max HP="+(health?health.maxHealth.ToString("F2"):"n/a"));
        Debug.Log("[5B.3 AUDIT] READ ONLY: no purchases, bonus recalculation, or save writes.");
    }

    [MenuItem(Menu + "8 - RUN 5B.3 Bonus Math QA (READ ONLY)")]
    public static void TestBonusMath()
    {
        int pass=0, total=0;
        Check(ref pass,ref total,0,0,0,0,0f,0f,0f,0f);
        Check(ref pass,ref total,1,0,0,0,10f,0f,0f,0f);
        Check(ref pass,ref total,0,1,0,0,0f,.05f,0f,0f);
        Check(ref pass,ref total,0,0,1,0,0f,0f,.03f,0f);
        Check(ref pass,ref total,0,0,0,1,0f,0f,0f,.02f);
        Check(ref pass,ref total,10,10,10,10,100f,.5f,.3f,.2f);
        Check(ref pass,ref total,3,2,4,5,30f,.1f,.12f,.1f);
        CheckInvalid(ref pass,ref total,-1,0,0,0);
        CheckInvalid(ref pass,ref total,0,11,0,0);
        CheckInvalid(ref pass,ref total,0,0,0,99);
        Debug.Log("[5B.3 QA] BONUS MATH: "+pass+"/"+total+
            " PASS. No GameObject, Profile or file was changed. Runtime equip/unequip needs Play Mode test.");
    }

    [MenuItem(Menu + "9 - REMOVE 5B.3 Component ONLY (OPEN SampleScene)")]
    public static void Remove()
    {
        if (Application.isPlaying) { Debug.LogWarning("[5B.3] Stop Play Mode first.");return; }
        Scene scene=SceneManager.GetActiveScene();
        if (scene.name!="SampleScene") { Debug.LogWarning("[5B.3] Open SampleScene first.");return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        if (!EditorUtility.DisplayDialog("REMOVE 5B.3", "Remove only the permanent bonus component from Player?\nPurchased upgrades and Save files will not be changed.","REMOVE", "CANCEL"))return;
        PlayerStats player=FindPlayerStats(scene);
        MS5B3PermanentBonusApplier applier=player?player.GetComponent<MS5B3PermanentBonusApplier>():null;
        if (!applier){Debug.LogWarning("[5B.3] Component not installed.");return;}
        Undo.DestroyObjectImmediate(applier);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[5B.3] Component removed. Purchased upgrades remain in Save files.");
    }

    private static bool Near(float a,float b){return Mathf.Abs(a-b)<.0001f;}
    private static void Check(ref int pass,ref int total,int v,int s,int f,int a,float hp,float dmg,float atk,float mov)
    {
        total++;
        MS5B3BonusValues b;string err;
        bool ok=MS5B3BonusMath.TryCalculate(v,s,f,a,out b,out err) &&
                Near(b.maxHealth,hp) && Near(b.damage,dmg) &&
                Near(b.attackSpeed,atk) && Near(b.moveSpeed,mov);
        if(ok)pass++;else Debug.LogError("[5B.3 QA] FAIL levels "+v+","+s+","+f+","+a+" "+err);
    }
    private static void CheckInvalid(ref int pass,ref int total,int v,int s,int f,int a)
    {
        total++;
        MS5B3BonusValues b;string error;
        if(!MS5B3BonusMath.TryCalculate(v,s,f,a,out b,out error))pass++;
        else Debug.LogError("[5B.3 QA] FAIL expected rejection: "+v+","+s+","+f+","+a);
    }
    private static PlayerStats FindPlayerStats(Scene scene)
    {
        foreach(GameObject root in scene.GetRootGameObjects())
        {
            foreach(PlayerStats stats in root.GetComponentsInChildren<PlayerStats>(true))
                if(stats && stats.CompareTag("Player"))return stats;
        }
        return null;
    }
    private static T FindSceneComponent<T>(Scene scene) where T:Component
    {
        foreach(GameObject root in scene.GetRootGameObjects())
        {
            T comp=root.GetComponentInChildren<T>(true);
            if(comp)return comp;
        }
        return null;
    }
    private static int CountSceneComponents<T>(Scene scene) where T:Component
    {
        int count=0;
        foreach(GameObject root in scene.GetRootGameObjects())count+=root.GetComponentsInChildren<T>(true).Length;
        return count;
    }
    private static void EnsureFolder(string folder)
    {
        string[] parts=folder.Split('/');string current=parts[0];
        for(int i=1;i<parts.Length;i++)
        {
            string next=current+"/"+parts[i];
            if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);
            current=next;
        }
    }
}
#endif
