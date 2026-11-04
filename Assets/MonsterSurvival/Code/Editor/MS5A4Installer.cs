#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MS5A4Installer
{
    private const string BackupFolder = "Assets/MonsterSurvival/Features/Progression/Save5A4/Backups";
    [MenuItem("Tools/MonsterSurvival/5A Save System/10 - INSTALL 5A.4 Save Slots UI (OPEN MainMenu)")]
    public static void Install()
    {
        if (Application.isPlaying) { Debug.LogWarning("[5A.4] Stop Play Mode before installation."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string scenePath = FindMainMenuScene();
        if (string.IsNullOrEmpty(scenePath)) { EditorUtility.DisplayDialog("5A.4", "MainMenu Scene not found.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("MonsterSurvival 5A.4", "Add an independent three-slot Save Manager overlay to Main Menu V3?\n\nOnly MainMenu Scene changes. Real JSON save files, .bak, Inventory, Equipment, and GameManager are untouched.", "INSTALL", "CANCEL")) return;
        try
        {
            EnsureFolder(BackupFolder);
            string backupPath = BackupFolder + "/MainMenu_before_5A4.unity";
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(backupPath))
            {
                if (!AssetDatabase.CopyAsset(scenePath, backupPath)) throw new IOException("Could not back up MainMenu Scene");
                AssetDatabase.ImportAsset(backupPath);
            }
            Scene scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Single);
            GameObject canvas=FindObject(scene,"MMV3_Canvas");
            GameObject home=FindObject(scene,"PAGE_Home");
            if (!canvas || !home || !home.transform.IsChildOf(canvas.transform))
                throw new InvalidOperationException("Expected PAGE_Home inside MMV3_Canvas. MainMenu V3 is required.");
            MS5A2ProfileWidget profile=FindComponent<MS5A2ProfileWidget>(scene);
            if (!profile) throw new InvalidOperationException("5A.2 Profile Widget missing. Install 5A.2 before 5A.4.");
            Transform found = canvas.transform.Find("MS5A4_SaveManager");
            if (found)
            {
                MS5A4SaveSlotsUI existing = found.GetComponent<MS5A4SaveSlotsUI>();
                if (existing && existing.openButton && existing.profileWidget)
                {
                    Debug.Log("[5A.4] Already installed; no duplicate created.");
                    EditorUtility.DisplayDialog("5A.4", "Already installed. No changes made.", "OK");
                    return;
                }
                throw new InvalidOperationException("Incomplete MS5A4_SaveManager exists. Inspect before reinstalling.");
            }
            Transform existingButton = home.transform.Find("MS5A4_BTN_Open");
            if (existingButton) throw new InvalidOperationException("An unexpected MS5A4_BTN_Open already exists.");
            GameObject buttonGo = new GameObject("MS5A4_BTN_Open", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.layer=5;buttonGo.transform.SetParent(home.transform,false);
            RectTransform br=buttonGo.GetComponent<RectTransform>();
            br.anchorMin=br.anchorMax=new Vector2(1f,1f);br.pivot=new Vector2(1f,1f);
            br.anchoredPosition=new Vector2(-36f,-177f);br.sizeDelta=new Vector2(245f,52f);
            Image img=buttonGo.GetComponent<Image>();img.color=new Color(.21f,.14f,.31f,.96f);img.raycastTarget=true;
            Button open=buttonGo.GetComponent<Button>();open.targetGraphic=img;
            ColorBlock colors=open.colors;colors.normalColor=Color.white;
            colors.highlightedColor=new Color(1f,.86f,.96f,1f);colors.pressedColor=new Color(.7f,.58f,.85f,1f);open.colors=colors;
            GameObject labelGo = new GameObject("TXT_ManageSaves",typeof(RectTransform),typeof(TextMeshProUGUI));
            labelGo.layer=5;labelGo.transform.SetParent(buttonGo.transform,false);
            RectTransform lr=labelGo.GetComponent<RectTransform>();lr.anchorMin=Vector2.zero;lr.anchorMax=Vector2.one;lr.offsetMin=lr.offsetMax=Vector2.zero;
            TextMeshProUGUI label=labelGo.GetComponent<TextMeshProUGUI>();label.text="MANAGE SAVES";
            label.alignment=TextAlignmentOptions.Center;label.fontSize=22;label.color=new Color(.94f,.91f,1f,1f);
            label.raycastTarget=false;label.enableAutoSizing=true;label.fontSizeMin=14;label.fontSizeMax=22;
            if (TMP_Settings.defaultFontAsset)label.font=TMP_Settings.defaultFontAsset;
            GameObject layer = new GameObject("MS5A4_SaveManager",typeof(RectTransform),typeof(MS5A4SaveSlotsUI));
            layer.layer=5;layer.transform.SetParent(canvas.transform,false);
            RectTransform rect=layer.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
            rect.offsetMin=rect.offsetMax=Vector2.zero;layer.transform.SetAsLastSibling();
            MS5A4SaveSlotsUI component=layer.GetComponent<MS5A4SaveSlotsUI>();
            component.openButton=open;component.profileWidget=profile;
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("[5A.4] Installed Save UI. Existing save files left untouched.");
            EditorUtility.DisplayDialog("5A.4 INSTALLED", "Open MainMenu, Play, click MANAGE SAVES. Three profiles and verified backups will be displayed. Scene saved.", "OK");
        }
        catch (Exception e) { Debug.LogError("[5A.4] INSTALL FAILED: " + e);EditorUtility.DisplayDialog("5A.4",e.Message,"OK"); }
    }

    [MenuItem("Tools/MonsterSurvival/5A Save System/11 - AUDIT 5A.4 (READ ONLY)")]
    public static void Audit()
    {
        Scene s=SceneManager.GetActiveScene();
        GameObject layer=FindObject(s,"MS5A4_SaveManager");
        MS5A4SaveSlotsUI ui=layer?layer.GetComponent<MS5A4SaveSlotsUI>():null;
        Debug.Log("[5A.4 Audit] Scene="+s.name+" | UI="+(ui?"PASS":"MISSING")+
                  " | Launcher="+(ui && ui.openButton?"PASS":"MISSING")+
                  " | 5A.2 Widget="+(ui && ui.profileWidget?"PASS":"MISSING"));
        for(int i=1;i<=3;i++)
        {
            MS5A4SaveRecovery.Report r=MS5A4SaveRecovery.InspectSlot(i);
            Debug.Log("[5A.4 Audit] Slot "+i+" | primary="+r.primaryState+" ("+r.primaryDetail+")"+
                " | backup="+r.backupState+" ("+r.backupDetail+")"+
                " | restore="+(r.CanRestore?"AVAILABLE":"DISABLED")+
                " | NO FILE CHANGES");
        }
    }

    [MenuItem("Tools/MonsterSurvival/5A Save System/12 - REMOVE 5A.4 Scene UI ONLY")]
    public static void Remove()
    {
        if(Application.isPlaying) { Debug.LogWarning("[5A.4] Stop Play Mode.");return; }
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        string scenePath=FindMainMenuScene();if(string.IsNullOrEmpty(scenePath))return;
        if(!EditorUtility.DisplayDialog("REMOVE 5A.4", "Remove only Manage Saves UI from MainMenu?\nNo save files, archives or backups will be deleted.","REMOVE","CANCEL"))return;
        Scene s=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Single);
        GameObject panel=FindObject(s,"MS5A4_SaveManager"),button=FindObject(s,"MS5A4_BTN_Open");
        if(panel && panel.GetComponent<MS5A4SaveSlotsUI>())UnityEngine.Object.DestroyImmediate(panel);
        if(button && button.GetComponent<Button>())UnityEngine.Object.DestroyImmediate(button);
        EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);
        Debug.Log("[5A.4] UI removed. Saved JSON, .bak, manual history untouched.");
    }
    private static string FindMainMenuScene()
    {
        foreach(string guid in AssetDatabase.FindAssets("t:Scene MainMenu"))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            if(Path.GetFileNameWithoutExtension(path).Equals("MainMenu",StringComparison.OrdinalIgnoreCase) &&
               !path.Contains("/Backups/"))return path;
        }
        return null;
    }
    private static GameObject FindObject(Scene scene,string name)
    {
        foreach(GameObject root in scene.GetRootGameObjects())
        {
            if(root.name==name)return root;
            foreach(Transform child in root.GetComponentsInChildren<Transform>(true))
                if(child.name==name)return child.gameObject;
        }
        return null;
    }
    private static T FindComponent<T>(Scene scene) where T:Component
    {
        foreach(GameObject root in scene.GetRootGameObjects())
        {
            T component=root.GetComponentInChildren<T>(true);
            if(component)return component;
        }
        return null;
    }
    private static void EnsureFolder(string path)
    {
        string[] parts=path.Split('/');string current=parts[0];
        for(int i=1;i<parts.Length;i++)
        {
            string next=current+"/"+parts[i];
            if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);
            current=next;
        }
    }
}
#endif
