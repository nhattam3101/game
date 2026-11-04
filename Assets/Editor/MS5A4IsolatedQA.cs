#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

// Every test file lives in its own temporary folder. Real profile JSONs are NEVER opened for writing.
public static class MS5A4IsolatedQA
{
    [Serializable] private sealed class Envelope
    {
        public string magic="MONSTERSURVIVAL_SAVE_5A1";
        public int formatVersion=1;
        public string sha256;
        public string payloadJson;
    }
    [MenuItem("Tools/MonsterSurvival/5A Save System/13 - RUN 5A.4 ISOLATED Recovery QA (SANDBOX ONLY)")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[5A.4 QA] Run in Edit Mode only.");return; }
        string folder = Path.Combine(Application.temporaryCachePath,"MS5A4_RecoverySandbox_"+Guid.NewGuid().ToString("N"));
        int passed=0;
        try
        {
            Directory.CreateDirectory(folder);
            string main=Path.Combine(folder,"qa_slot_01.json");
            string backup=main+".bak";
            CreateFixture(main,10);CreateFixture(backup,9);
            MS5A1SaveData data;string text;
            bool p=MS5A4SaveRecovery.InspectFile(main,"qa_slot_01",out data,out text)==MS5A4SaveRecovery.FileState.Valid && data.revision==10;
            Report("Primary fixture validated",p,ref passed);
            p=MS5A4SaveRecovery.InspectFile(backup,"qa_slot_01",out data,out text)==MS5A4SaveRecovery.FileState.Valid && data.revision==9;
            Report("Backup fixture validated",p,ref passed);
            string msg;
            p=MS5A4SaveRecovery.TryRestoreVerifiedBackup(main,backup,"qa_slot_01",out msg);
            p=p && MS5A4SaveRecovery.InspectFile(main,"qa_slot_01",out data,out text)==MS5A4SaveRecovery.FileState.Valid && data.revision==9;
            Report("Explicit verified rollback rev10 -> rev9",p,ref passed);
            p=MS5A4SaveRecovery.InspectFile(backup,"qa_slot_01",out data,out text)==MS5A4SaveRecovery.FileState.Valid && data.revision==9;
            Report("Backup preserved unchanged",p,ref passed);
            string history=Path.Combine(folder,"5A4_ManualHistory");
            p=Directory.Exists(history) && Directory.GetFiles(history,"*.json").Length>=1;
            Report("Previous primary archived",p,ref passed);
            CreateFixture(main,11);
            File.WriteAllText(backup,"CORRUPT TEST BACKUP",Encoding.UTF8);
            p=!MS5A4SaveRecovery.TryRestoreVerifiedBackup(main,backup,"qa_slot_01",out msg);
            p=p && MS5A4SaveRecovery.InspectFile(main,"qa_slot_01",out data,out text)==MS5A4SaveRecovery.FileState.Valid && data.revision==11;
            Report("Corrupt backup rejected, primary preserved",p,ref passed);
            CreateFixture(backup,10);
            File.WriteAllText(main,"CORRUPT TEST PRIMARY",Encoding.UTF8);
            p=MS5A4SaveRecovery.TryRestoreVerifiedBackup(main,backup,"qa_slot_01",out msg);
            p=p && MS5A4SaveRecovery.InspectFile(main,"qa_slot_01",out data,out text)==MS5A4SaveRecovery.FileState.Valid && data.revision==10;
            Report("Corrupt primary recovered from verified backup",p,ref passed);
            p=MS5A4SaveRecovery.InspectFile(main,"slot_01",out data,out text)==MS5A4SaveRecovery.FileState.Invalid;
            Report("Wrong profile ID rejected",p,ref passed);
            Debug.Log("[5A.4 QA] RESULT: " + passed + "/8. Sandbox: " + folder + " (temporary files only)");
        }
        catch(Exception ex){Debug.LogError("[5A.4 QA] Exception: "+ex);}
        finally
        {
            try{Directory.Delete(folder,true);Debug.Log("[5A.4 QA] Temporary sandbox removed. Real saves untouched.");}
            catch(Exception e){Debug.LogWarning("[5A.4 QA] Temporary directory not deleted: "+folder+" | "+e.Message);}
        }
    }
    private static void Report(string label,bool pass,ref int count)
    {
        if(pass)count++;
        if(pass)Debug.Log("[5A.4 QA] PASS — "+label);
        else Debug.LogError("[5A.4 QA] FAIL — "+label);
    }
    private static void CreateFixture(string path,int revision)
    {
        MS5A1SaveData data=MS5A1SaveService.CreateFresh(1,true);
        data.revision=revision;data.bankGold=revision*10;
        data.lastSavedUtc=DateTime.UtcNow.ToString("o");
        string payload=JsonUtility.ToJson(data);
        Envelope env=new Envelope();env.payloadJson=payload;
        using(SHA256 sha=SHA256.Create())
        {
            byte[] bytes=sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
            StringBuilder hex=new StringBuilder();foreach(byte b in bytes)hex.Append(b.ToString("x2"));env.sha256=hex.ToString();
        }
        File.WriteAllText(path,JsonUtility.ToJson(env,true),new UTF8Encoding(false));
    }
}
#endif
