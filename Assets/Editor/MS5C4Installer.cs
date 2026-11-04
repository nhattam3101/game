#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MS5C4Installer
{
    private const string Menu = "Tools/MonsterSurvival/5C Unlock System/";
    private const string Root = "Assets/MonsterSurvival/Unlock5C4";
    private const string Art = Root + "/Art/";
    private const string Backups = Root + "/Backups";
    private const string Launcher = "MS5C4_BTN_Achievements";
    private const string Gallery = "MS5C4_AchievementGallery";

    [MenuItem(Menu + "9 - INSTALL 5C.4 Achievement UI + Toasts (BOTH scenes)")]
    public static void Install()
    {
        if (Application.isPlaying)
        { EditorUtility.DisplayDialog("5C.4", "Stop Play Mode first.", "OK"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string mainPath = FindScene("MainMenu"),gamePath = FindScene("SampleScene");
        if (mainPath == null || gamePath == null)
        { EditorUtility.DisplayDialog("5C.4", "MainMenu or SampleScene missing.", "OK"); return; }
        Scene m = EditorSceneManager.OpenScene(mainPath,OpenSceneMode.Single);
        GameObject canvas=Find(m,"MMV3_Canvas"),home=Find(m,"PAGE_Home");
        MMV3MenuController controller=FindComponent<MMV3MenuController>(m);
        MS5A2ProfileWidget widget=FindComponent<MS5A2ProfileWidget>(m);
        MS5C3MainMenuGate gate=FindComponent<MS5C3MainMenuGate>(m);
        if (!canvas || !canvas.GetComponent<Canvas>() || !canvas.GetComponent<GraphicRaycaster>() ||
            !home || !controller || !widget || !gate || !home.transform.IsChildOf(canvas.transform))
        { EditorUtility.DisplayDialog("5C.4", "Install 5C.3, 5A.2 and Main Menu V3 first. Nothing changed.", "OK");return; }
        if (Find(m,Launcher) || Find(m,Gallery))
        { EditorUtility.DisplayDialog("5C.4", "5C.4 UI already exists or partially installed. No duplicate created.", "OK");return; }
        Scene g=EditorSceneManager.OpenScene(gamePath,OpenSceneMode.Single);
        MS5C3BossAchievements boss=FindComponent<MS5C3BossAchievements>(g);
        if (!boss || !boss.waveManager)
        { EditorSceneManager.OpenScene(mainPath,OpenSceneMode.Single);
          EditorUtility.DisplayDialog("5C.4", "Missing 5C.3 BossAchievements in SampleScene. No changes made.", "OK");return; }
        if (FindComponent<MS5C4AchievementToast>(g))
        { EditorSceneManager.OpenScene(mainPath,OpenSceneMode.Single);
          EditorUtility.DisplayDialog("5C.4", "Toast component already exists. Check previous installation.", "OK");return; }
        if (!EditorUtility.DisplayDialog("5C.4 - ACHIEVEMENT UI",
            "Back up both Scenes and add achievement gallery + saved-achievement toasts?\n\n"+
            "NO Gold deductions, equipment rewards, or new achievements are granted by installation.\n"+
            "Main Menu V3, Boss AI, WaveManager and existing unlocks stay intact.","INSTALL","CANCEL"))
        {EditorSceneManager.OpenScene(mainPath,OpenSceneMode.Single);return;}
        try
        {
            EnsureFolder(Backups);
            BackupScene(mainPath,Backups+"/MainMenu_BEFORE_5C4.unity");
            BackupScene(gamePath,Backups+"/SampleScene_BEFORE_5C4.unity");
            Sprite titan=Prepare("Badge_Titan.png");
            Sprite warden=Prepare("Badge_Warden.png");
            Sprite gear=Prepare("Badge_Gear.png");
            // Gameplay: attach only one read-only watcher alongside the existing 5C.3 watcher.
            MS5C4AchievementToast toast=Undo.AddComponent<MS5C4AchievementToast>(boss.gameObject);
            toast.unlockSound=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"/Audio/Achievement_Chime.wav");
            if (!toast.unlockSound) throw new FileNotFoundException("Achievement chime missing.");
            EditorUtility.SetDirty(toast);EditorSceneManager.MarkSceneDirty(g);EditorSceneManager.SaveScene(g);
            // MainMenu: one launcher below the existing UPGRADES launcher, one overlay root.
            m=EditorSceneManager.OpenScene(mainPath,OpenSceneMode.Single);
            canvas=Find(m,"MMV3_Canvas");home=Find(m,"PAGE_Home");widget=FindComponent<MS5A2ProfileWidget>(m);
            GameObject buttonGo=new GameObject(Launcher,typeof(RectTransform),typeof(Image),typeof(Button));
            buttonGo.layer=5;buttonGo.transform.SetParent(home.transform,false);
            Undo.RegisterCreatedObjectUndo(buttonGo,"5C.4 launcher");
            RectTransform br=buttonGo.GetComponent<RectTransform>();br.anchorMin=br.anchorMax=new Vector2(1f,1f);
            br.pivot=new Vector2(1f,1f);br.anchoredPosition=new Vector2(-36f,-301f);br.sizeDelta=new Vector2(245f,52f);
            Image bg=buttonGo.GetComponent<Image>();bg.color=new Color(.25f,.16f,.36f,1f);bg.raycastTarget=true;
            Button button=buttonGo.GetComponent<Button>();button.targetGraphic=bg;
            ColorBlock colors=button.colors;colors.normalColor=Color.white;
            colors.highlightedColor=new Color(1f,.84f,1f,1f);colors.pressedColor=new Color(.75f,.64f,.9f,1f);
            button.colors=colors;
            GameObject label=new GameObject("TXT_Achievements",typeof(RectTransform),typeof(TextMeshProUGUI));
            label.layer=5;label.transform.SetParent(buttonGo.transform,false);
            RectTransform lr=label.GetComponent<RectTransform>();lr.anchorMin=Vector2.zero;lr.anchorMax=Vector2.one;
            lr.offsetMin=lr.offsetMax=Vector2.zero;
            TextMeshProUGUI txt=label.GetComponent<TextMeshProUGUI>();txt.text="ACHIEVEMENTS";txt.fontSize=22;
            txt.fontSizeMin=13;txt.fontSizeMax=22;txt.enableAutoSizing=true;
            txt.color=new Color(.95f,.9f,1f,1f);txt.alignment=TextAlignmentOptions.Center;txt.raycastTarget=false;
            if(TMP_Settings.defaultFontAsset)txt.font=TMP_Settings.defaultFontAsset;
            GameObject gallery=new GameObject(Gallery,typeof(RectTransform),typeof(MS5C4AchievementGallery));
            gallery.layer=5;gallery.transform.SetParent(canvas.transform,false);
            Undo.RegisterCreatedObjectUndo(gallery,"5C.4 Gallery");
            RectTransform rt=gallery.GetComponent<RectTransform>();rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;
            rt.offsetMin=rt.offsetMax=Vector2.zero;gallery.transform.SetAsLastSibling();
            MS5C4AchievementGallery component=gallery.GetComponent<MS5C4AchievementGallery>();
            component.openButton=button;component.profileWidget=widget;
            component.titanBadge=titan;component.wardenBadge=warden;component.equipmentBadge=gear;
            EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(m);EditorSceneManager.SaveScene(m);
            Debug.Log("[5C.4] INSTALLED: Gallery and saved-achievement Toast. No profile files modified.");
            EditorUtility.DisplayDialog("5C.4 INSTALLED",
                "MainMenu -> ACHIEVEMENTS (under UPGRADES).\n"+
                "SampleScene: 5C.4 toast is attached to 5C.3 RuntimeGuards.\n"+
                "Both Scenes were backed up. Press Play to test.","OK");
        }
        catch(Exception ex)
        { Debug.LogError("[5C.4] INSTALL FAILED: "+ex);EditorUtility.DisplayDialog("5C.4",ex.Message,"OK"); }
    }

    [MenuItem(Menu + "10 - AUDIT 5C.4 Achievements (READ ONLY)")]
    public static void Audit()
    {
        Scene s=SceneManager.GetActiveScene();
        var ui=FindComponent<MS5C4AchievementGallery>(s);
        var toast=FindComponent<MS5C4AchievementToast>(s);
        Debug.Log("[5C.4 AUDIT] Scene="+s.name+" Gallery="+(ui?"PASS":"NOT IN SCENE")+
            " Launcher="+(ui && ui.openButton?"PASS":"MISSING")+
            " Icons="+(ui && ui.titanBadge && ui.wardenBadge && ui.equipmentBadge?"PASS":"MISSING")+
            " GameplayToast="+(toast?"PASS":"NOT IN SCENE"));
        for(int i=1;i<=3;i++)
        {
            MS5C1UnlockService.Snapshot snapshot;
            string reason;
            bool valid=MS5C1UnlockService.TryGetSnapshot(i,out snapshot,out reason);
            Debug.Log("[5C.4 AUDIT] Profile "+i+" | "+reason+
                (valid ? " | Titan="+snapshot.IsUnlocked("achievement.shadow_titan")+
                    " | Warden="+snapshot.IsUnlocked("achievement.grave_warden")+
                    " | Reaper="+snapshot.IsUnlocked("equipment.reaper_edge")+
                    " | Oath="+snapshot.IsUnlocked("equipment.wardens_oath") : " | UNAVAILABLE"));
        }
        Debug.Log("[5C.4 AUDIT] READ ONLY. No Badge, Gold, Item or Save has been changed.");
    }

    [MenuItem(Menu + "11 - REMOVE 5C.4 UI + Toast ONLY (BOTH scenes)")]
    public static void Remove()
    {
        if(Application.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        if(!EditorUtility.DisplayDialog("REMOVE 5C.4",
            "Remove 5C.4 Gallery, launcher and Toast?\nExisting saved achievements, Gold, equipment and maps are kept.",
            "REMOVE","CANCEL"))return;
        string main=FindScene("MainMenu"),game=FindScene("SampleScene");
        if(main==null || game==null){Debug.LogError("[5C.4] Scenes missing.");return;}
        Scene g=EditorSceneManager.OpenScene(game,OpenSceneMode.Single);
        var toast=FindComponent<MS5C4AchievementToast>(g);if(toast)Undo.DestroyObjectImmediate(toast);
        EditorSceneManager.MarkSceneDirty(g);EditorSceneManager.SaveScene(g);
        Scene m=EditorSceneManager.OpenScene(main,OpenSceneMode.Single);
        GameObject gallery=Find(m,Gallery),launcher=Find(m,Launcher);
        if(gallery)Undo.DestroyObjectImmediate(gallery);
        if(launcher)Undo.DestroyObjectImmediate(launcher);
        EditorSceneManager.MarkSceneDirty(m);EditorSceneManager.SaveScene(m);
        Debug.Log("[5C.4] Removed only 5C.4 UI & Toast. Profiles unchanged.");
    }
    private static Sprite Prepare(string file)
    {
        string path=Art+file;
        TextureImporter imp=AssetImporter.GetAtPath(path) as TextureImporter;
        if(!imp)throw new FileNotFoundException("Missing 5C.4 icon: "+path);
        imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;
        imp.alphaIsTransparency=true;imp.mipmapEnabled=false;imp.filterMode=FilterMode.Bilinear;
        imp.SaveAndReimport();
        Sprite sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if(!sprite)throw new InvalidOperationException("Icon import failed: "+path);
        return sprite;
    }
    private static string FindScene(string name)
    {
        foreach(string id in AssetDatabase.FindAssets("t:Scene "+name))
        {string path=AssetDatabase.GUIDToAssetPath(id);
            if(Path.GetFileNameWithoutExtension(path).Equals(name,StringComparison.OrdinalIgnoreCase) && !path.Contains("/Backups/"))return path;}
        return null;
    }
    private static GameObject Find(Scene scene,string name)
    {foreach(GameObject root in scene.GetRootGameObjects())
        {if(root.name==name)return root;
            foreach(Transform t in root.GetComponentsInChildren<Transform>(true))if(t.name==name)return t.gameObject;}
        return null;}
    private static T FindComponent<T>(Scene scene) where T:Component
    {foreach(GameObject root in scene.GetRootGameObjects()){T c=root.GetComponentInChildren<T>(true);if(c)return c;}return null;}
    private static void EnsureFolder(string path)
    {string[] parts=path.Split('/');string dir=parts[0];
        for(int i=1;i<parts.Length;i++){string next=dir+"/"+parts[i];
            if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(dir,parts[i]);dir=next;}}
    private static void BackupScene(string source,string destination)
    {if(!AssetDatabase.LoadAssetAtPath<SceneAsset>(destination))
        {if(!AssetDatabase.CopyAsset(source,destination))throw new IOException("Backup Scene failed: "+source);
            AssetDatabase.ImportAsset(destination);}}
}
#endif
