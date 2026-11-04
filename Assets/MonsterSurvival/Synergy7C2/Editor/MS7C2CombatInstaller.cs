#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ONLY one copy in Assets/MonsterSurvival/Synergy7C2/Editor.
// 7C.2 requires 7B.1, 7B.2, 7B.3 and 7C.1. Edit Mode install only.
public sealed class MS7C2CombatInstaller : EditorWindow
{
    private const string BulletBase = "MS7B1ElementalHitBridge.AfterBaseHit(this, target, actualDamage);";
    private const string BulletNew = "MS7C2CombatBridge.RecordCritical(this, isCritical); // MS7C2_CRIT_HOOK\n        " + BulletBase;

    private const string SlowBase = "slow.ApplySlow(settings.slowPercent, settings.slowDuration);";
    private const string SlowNew = "slow.ApplySlow(MS7C2CombatBridge.GlacialSlow(this, settings.slowPercent), settings.slowDuration);";

    private const string ChainBase = "float arcDamage = Mathf.Max(0f, actualDamage * settings.chainDamageRatio);";
    private const string ChainNew = "float arcDamage = Mathf.Max(0f, MS7C2CombatBridge.AmplifyLightningChain(this, actualDamage * settings.chainDamageRatio));";

    private const string BurnBase = "best.TakeDamage(arcDamage); // primary damage is NEVER repeated";
    private const string BurnNew = BurnBase + "\n            MS7C2CombatBridge.AfterLightningChainHit(this, best, settings); // MS7C2_WILDFIRE_HOOK";

    private MS7C1SynergyCore core;
    private Vector2 scroll;

    [MenuItem("Tools/MonsterSurvival/7C Skill Synergy/4 - OPEN 7C.2 Combat Installer")]
    public static void Open() { GetWindow<MS7C2CombatInstaller>("7C.2 Combat Effects").minSize = new Vector2(570, 390); }

    [MenuItem("Tools/MonsterSurvival/7C Skill Synergy/5 - AUDIT 7C.2 (READ ONLY)")]
    public static void MenuAudit() { Open(); GetWindow<MS7C2CombatInstaller>().Audit(); }

    [MenuItem("Tools/MonsterSurvival/7C Skill Synergy/6 - RUN 7C.2 PURE FORMULA QA")]
    public static void MenuQA() { PureQA(); }

    private void OnEnable() { Discover(); }
    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("MONSTERSURVIVAL 7C.2 — COMBAT EFFECTS", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("NEW synergies only. Legacy Frozen Burst / Phantom Chain / Rapid Blast remain untouched. Exact source anchors and backup required before patching. Installs 1 Scene Player component and 4 reversible hook sites (1 Bullet + 3 elemental).", MessageType.Info);
        core = (MS7C1SynergyCore)EditorGUILayout.ObjectField("SCENE 7C.1 Synergy Core", core, typeof(MS7C1SynergyCore), true);
        if (GUILayout.Button("FIND CURRENT SCENE SYNERGY CORE")) Discover();
        GUILayout.Space(8);
        GUI.enabled = !EditorApplication.isPlayingOrWillChangePlaymode;
        if (GUILayout.Button("INSTALL / UPDATE 7C.2 (BACKUP SOURCE + SCENE)", GUILayout.Height(37))) SafeInstall();
        GUI.enabled = true;
        if (GUILayout.Button("AUDIT HOOKS & REFERENCES (READ ONLY)", GUILayout.Height(30))) Audit();
        if (GUILayout.Button("RUN 12 PURE FORMULA TESTS (READ ONLY)", GUILayout.Height(30))) PureQA();
        if (EditorApplication.isPlaying && GUILayout.Button("SHOW LIVE PROC COUNTERS IN CONSOLE", GUILayout.Height(30)))
        {
            MS7C2SynergyCombat live = FindFirstObjectByType<MS7C2SynergyCombat>();
            if (live != null) Debug.Log("[7C.2 LIVE] Wildfire=" + live.WildfireProcs +
                " Glacial=" + live.GlacialHitBonuses + " Thunder=" + live.ThunderCriticalChains);
            else Debug.LogWarning("[7C.2 LIVE] No enabled combat component in this Scene.");
        }
        GUI.enabled = !EditorApplication.isPlayingOrWillChangePlaymode;
        if (GUILayout.Button("REMOVE 7C.2 ONLY (REVERSE ALL 4 HOOKS)", GUILayout.Height(30))) SafeRemove();
        GUI.enabled = true;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("READ ME: Install AFTER 7B.3 and 7C.1. Run 7C.2 Remove BEFORE uninstalling 7B.3 or 7B.1. Do not copy scripts to Assets/Editor or duplicate existing Player controller. Pause and Save systems are never edited.", MessageType.Warning);
        EditorGUILayout.EndScrollView();
    }

    private void Discover()
    {
        core = FindFirstObjectByType<MS7C1SynergyCore>();
        if (core != null && !SceneObject(core)) core = null;
    }
    private static bool SceneObject(Component c)
    {
        return c != null && !EditorUtility.IsPersistent(c) &&
            c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded;
    }
    private static void EditOnly()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before Install/Remove.");
    }
    private static string FindUniqueScript(string filename)
    {
        string[] found = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(filename) + " t:MonoScript");
        string unique = null;
        foreach (string guid in found)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileName(path) != filename) continue;
            if (unique != null) throw new InvalidOperationException("Duplicate " + filename + ": " + unique + " and " + path);
            unique = path;
        }
        if (unique == null) throw new InvalidOperationException("Missing script " + filename + ". Check installed 7B.1 and current Bullet.cs.");
        return unique;
    }
    private static string Abs(string asset)
    {
        return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, asset));
    }
    private static string LF(string text) { return text.Replace("\r\n", "\n"); }
    private static string Newlines(string text, bool crlf) { return crlf ? text.Replace("\n", "\r\n") : text; }
    private static int Count(string text, string term)
    {
        int count=0, pos=0;
        while (true)
        {
            int idx=text.IndexOf(term,pos,StringComparison.Ordinal);
            if (idx<0) return count;
            count++; pos=idx+term.Length;
        }
    }
    private static string ReplaceUnique(string source, string oldText, string newText)
    {
        if (Count(source, oldText)!=1)
            throw new InvalidOperationException("Expected exactly ONE source anchor: " + oldText.Substring(0,Math.Min(oldText.Length,85)) + " ... No files changed.");
        return source.Replace(oldText, newText);
    }
    private static bool HasHooks(string bullet, string elemental)
    {
        return Count(bullet,"MS7C2CombatBridge.RecordCritical(")==1 &&
            Count(elemental,"MS7C2CombatBridge.GlacialSlow(")==1 &&
            Count(elemental,"MS7C2CombatBridge.AmplifyLightningChain(")==1 &&
            Count(elemental,"MS7C2CombatBridge.AfterLightningChainHit(")==1;
    }
    private static int HookCount(string bullet, string elemental)
    {
        return Count(bullet,"MS7C2CombatBridge.RecordCritical(") +
            Count(elemental,"MS7C2CombatBridge.GlacialSlow(") +
            Count(elemental,"MS7C2CombatBridge.AmplifyLightningChain(") +
            Count(elemental,"MS7C2CombatBridge.AfterLightningChainHit(");
    }

    private static void Ensure7B3Ready(string bullet, string elemental)
    {
        if (Count(bullet,BulletBase)!=1 || !bullet.Contains("MS7B1_ELEMENT_HOOK"))
            throw new InvalidOperationException("7B.1 Bullet Hook missing/changed. Fix 7B.1 first.");
        if (!elemental.Contains("MS7B3VisualBridge.AfterFire(") ||
            !elemental.Contains("MS7B3VisualBridge.AfterIce(") ||
            !elemental.Contains("MS7B3VisualBridge.AfterLightningJump(") ||
            !elemental.Contains("hasLegacySlow"))
            throw new InvalidOperationException("7B.3 VFX / Frost precedence guard missing. Install/Audit 7B.3 first.");
    }
    private static string PatchBullet(string text)
    {
        if (text.Contains("MS7C2CombatBridge.RecordCritical(")) return text;
        return ReplaceUnique(text,BulletBase,BulletNew);
    }
    private static string PatchElemental(string text)
    {
        if (text.Contains("MS7C2CombatBridge.")) return text;
        text=ReplaceUnique(text,SlowBase,SlowNew);
        text=ReplaceUnique(text,ChainBase,ChainNew);
        text=ReplaceUnique(text,BurnBase,BurnNew);
        return text;
    }
    private static string UnpatchBullet(string text)
    {
        if (!text.Contains("MS7C2CombatBridge.RecordCritical(")) return text;
        return ReplaceUnique(text,BulletNew,BulletBase);
    }
    private static string UnpatchElemental(string text)
    {
        if (!text.Contains("MS7C2CombatBridge.")) return text;
        text=ReplaceUnique(text,BurnNew,BurnBase);
        text=ReplaceUnique(text,ChainNew,ChainBase);
        text=ReplaceUnique(text,SlowNew,SlowBase);
        return text;
    }
    private static string Backup(string bulletAsset, string elementAsset, string label)
    {
        string project=Directory.GetParent(Application.dataPath).FullName;
        string path=Path.Combine(project,"MS_Backups","7C2",
            DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+"_"+label);
        Directory.CreateDirectory(path);
        File.Copy(Abs(bulletAsset),Path.Combine(path,"Bullet.cs"),false);
        File.Copy(Abs(elementAsset),Path.Combine(path,"MS7B1ElementalProjectile.cs"),false);
        var sc=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (sc.IsValid() && !string.IsNullOrEmpty(sc.path) && File.Exists(Abs(sc.path)))
            File.Copy(Abs(sc.path),Path.Combine(path,Path.GetFileName(sc.path)),false);
        return path;
    }
    private static void WriteSource(string asset, string normalized, bool crlf)
    {
        File.WriteAllText(Abs(asset),Newlines(normalized,crlf),new UTF8Encoding(false));
    }
    private void SafeInstall()
    {
        try { Install(); } catch(Exception e)
        { Debug.LogError("[7C.2 INSTALL ABORT] " + e.Message); EditorUtility.DisplayDialog("7C.2 Install stopped",e.Message,"OK"); }
    }
    private void Install()
    {
        EditOnly(); Discover();
        if (!SceneObject(core) || core.skillManager==null || core.elementalCards==null || core.loadout==null)
            throw new InvalidOperationException("Open SampleScene, install 7C.1 and assign SCENE Player SynergyCore + SkillManager + 7B.2 + Loadout.");
        if (core.GetComponent<MS7C2SynergyCombat>()==null &&
            FindObjectsByType<MS7C2SynergyCombat>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length>0)
            throw new InvalidOperationException("Found 7C.2 Combat on ANOTHER object. Remove duplicates first.");
        string bulletAsset=FindUniqueScript("Bullet.cs");
        string elementAsset=FindUniqueScript("MS7B1ElementalProjectile.cs");
        string bulletRaw=File.ReadAllText(Abs(bulletAsset)), elementRaw=File.ReadAllText(Abs(elementAsset));
        bool bulletCrlf=bulletRaw.Contains("\r\n"), elementCrlf=elementRaw.Contains("\r\n");
        string b=LF(bulletRaw), e=LF(elementRaw);
        Ensure7B3Ready(b,e);
        int count=HookCount(b,e);
        if (count!=0 && !HasHooks(b,e))
            throw new InvalidOperationException("Partial 7C.2 hooks ("+count+"/4); do NOT write. Compare with original backup.");
        string nextB= count==0 ? PatchBullet(b):b;
        string nextE= count==0 ? PatchElemental(e):e;
        if (!HasHooks(nextB,nextE)) throw new InvalidOperationException("7C.2 patch validation failed; no files changed.");
        EditorSceneManager.SaveOpenScenes();
        string backup=Backup(bulletAsset,elementAsset,"install");
        MS7C2SynergyCombat combat=core.GetComponent<MS7C2SynergyCombat>();
        if (combat==null) combat=Undo.AddComponent<MS7C2SynergyCombat>(core.gameObject);
        Undo.RecordObject(combat,"Install 7C.2 combat"); combat.core=core;
        EditorUtility.SetDirty(combat);
        EditorSceneManager.MarkSceneDirty(core.gameObject.scene);
        if (!EditorSceneManager.SaveScene(core.gameObject.scene))
            throw new InvalidOperationException("Cannot save Scene. No code modified. Backup: "+backup);
        try
        {
            if (nextB!=b) WriteSource(bulletAsset,nextB,bulletCrlf);
            if (nextE!=e) WriteSource(elementAsset,nextE,elementCrlf);
        }
        catch
        {
            // Both backup files were completed before either source write.
            File.Copy(Path.Combine(backup,"Bullet.cs"),Abs(bulletAsset),true);
            File.Copy(Path.Combine(backup,"MS7B1ElementalProjectile.cs"),Abs(elementAsset),true);
            throw;
        }
        Debug.Log("[7C.2 INSTALL] 4 hooks verified. Combat controller on Player. Source/Scene backups: "+backup+
                  " | Old 3 synergies unchanged. Recompile then run AUDIT.");
        if (nextB!=b || nextE!=e) AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
    }
    private void SafeRemove()
    {
        try { Remove(); } catch (Exception e)
        { Debug.LogError("[7C.2 REMOVE ABORT] "+e.Message); EditorUtility.DisplayDialog("7C.2 Remove stopped",e.Message,"OK"); }
    }
    private void Remove()
    {
        EditOnly();
        string bulletAsset=FindUniqueScript("Bullet.cs");
        string elementAsset=FindUniqueScript("MS7B1ElementalProjectile.cs");
        string bulletRaw=File.ReadAllText(Abs(bulletAsset)),elementRaw=File.ReadAllText(Abs(elementAsset));
        bool bulletCrlf=bulletRaw.Contains("\r\n"),elementCrlf=elementRaw.Contains("\r\n");
        string b=LF(bulletRaw),e=LF(elementRaw);
        int count=HookCount(b,e);
        if (count!=0 && !HasHooks(b,e)) throw new InvalidOperationException("Partial/edited 7C.2 hooks; restore manually from backup. Nothing removed.");
        string nextB=count==0?b:UnpatchBullet(b);
        string nextE=count==0?e:UnpatchElemental(e);
        if (HookCount(nextB,nextE)!=0) throw new InvalidOperationException("Remaining hooks after proposed remove. Nothing removed.");
        EditorSceneManager.SaveOpenScenes();
        string backup=Backup(bulletAsset,elementAsset,"remove");
        foreach (var addon in FindObjectsByType<MS7C2SynergyCombat>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if (!SceneObject(addon)) continue;
            Undo.DestroyObjectImmediate(addon);
        }
        EditorSceneManager.SaveOpenScenes();
        try
        {
            if(nextB!=b) WriteSource(bulletAsset,nextB,bulletCrlf);
            if(nextE!=e) WriteSource(elementAsset,nextE,elementCrlf);
        }
        catch
        {
            File.Copy(Path.Combine(backup,"Bullet.cs"),Abs(bulletAsset),true);
            File.Copy(Path.Combine(backup,"MS7B1ElementalProjectile.cs"),Abs(elementAsset),true);
            throw;
        }
        Debug.Log("[7C.2 REMOVE] 4 hooks and 7C.2 scene addon removed. 7B.1/7B.3/7C.1 and legacy combos preserved. Backup: "+backup);
        if(nextB!=b || nextE!=e) AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
    }
    private void Audit()
    {
        try
        {
            string b=LF(File.ReadAllText(Abs(FindUniqueScript("Bullet.cs"))));
            string e=LF(File.ReadAllText(Abs(FindUniqueScript("MS7B1ElementalProjectile.cs"))));
            int count=HookCount(b,e);
            int sceneAddons=FindObjectsByType<MS7C2SynergyCombat>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            int oldPanel=0;
            foreach(MonoBehaviour mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(mb!=null && mb.GetType().Name=="SkillSynergyPanelUI") oldPanel++;
            Discover();
            bool valid=HasHooks(b,e) && sceneAddons==1 && SceneObject(core) &&
                core.GetComponent<MS7C2SynergyCombat>()!=null &&
                core.GetComponent<MS7C2SynergyCombat>().core==core && e.Contains("hasLegacySlow");
            string report="[7C.2 AUDIT] "+(valid?"PASS":"CHECK")+" | hook sites="+count+"/4 | combat components="+sceneAddons+
                " | 7C.1 core scene="+SceneObject(core)+" | 7B.3 Frost guard="+e.Contains("hasLegacySlow")+
                " | old [J] SkillSynergyPanelUI="+oldPanel+
                "\nWildfire: 35% Burn secondary chain only; Glacial: +10 pp single Slow on Ice Ricochet; Thunder: +35% chain on critical bullet."+
                "\nAudit does NOT prove damage, sound, Save or old-combo behavior. Manual Play QA required.";
            if(valid) Debug.Log(report); else Debug.LogWarning(report);
        }
        catch(Exception ex) {Debug.LogError("[7C.2 AUDIT] "+ex.Message);}
    }
    private static void PureQA()
    {
        int total=0,pass=0;
        Action<bool,string> check=(ok,name)=>
        {total++;if(ok){pass++;Debug.Log("[7C.2 PURE] PASS "+name);}else Debug.LogError("[7C.2 PURE] FAIL "+name);};
        check(Mathf.Approximately(MS7C2SynergyCombat.FormulaGlacialSlow(.30f,.10f,.75f),.40f),"Ice Lv1 +10pp => 40% slow");
        check(Mathf.Approximately(MS7C2SynergyCombat.FormulaGlacialSlow(.50f,.10f,.75f),.60f),"Ice Lv3 +10pp => 60% slow");
        check(Mathf.Approximately(MS7C2SynergyCombat.FormulaGlacialSlow(.80f,.10f,.75f),.75f),"Glacial cap at 75%");
        check(Mathf.Approximately(MS7C2SynergyCombat.FormulaGlacialSlow(.30f,0f,.75f),.30f),"No bonus stays unchanged");
        check(Mathf.Approximately(MS7C2SynergyCombat.FormulaThunderDamage(4.5f,1.35f),6.075f),"Thunder crit 4.5 -> 6.075");
        check(Mathf.Approximately(MS7C2SynergyCombat.FormulaThunderDamage(0f,1.35f),0f),"No damage stays zero");
        check(Mathf.Approximately(MS7C2SynergyCombat.FormulaThunderDamage(4.5f,5f),9f),"Thunder max multiplier capped at 2x");
        check(Mathf.Approximately(MS7C2SynergyCombat.FormulaWildfireDps(2f,.3f,1,1f),2f),"Wildfire Fire Lv1 2 DPS");
        check(Mathf.Approximately(MS7C2SynergyCombat.FormulaWildfireDps(2f,.3f,2,1f),2.6f),"Wildfire Fire Lv2 2.6 DPS");
        check(Mathf.Approximately(MS7C2SynergyCombat.FormulaWildfireDps(2f,.3f,3,1f),3.2f),"Wildfire Fire Lv3 3.2 DPS");
        check(MS7C1SynergyCore.HasWildfire(1,1) && !MS7C1SynergyCore.HasWildfire(0,1),"Wildfire unlock prerequisite");
        check(MS7C1SynergyCore.HasGlacial(1,true) && MS7C1SynergyCore.HasThunder(1,true),"Glacial/Thunder unlock prerequisites");
        Debug.Log("[7C.2 PURE RESULT] "+pass+"/"+total+" PASS (math/conditions only; manual gameplay still required).");
    }
}
#endif
