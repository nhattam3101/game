#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Installer only edits MainMenu Scene. No player save, prefab, gameplay, or Save Core writes.
public static class MS5B2ShopInstaller
{
    private const string RootMenu = "Tools/MonsterSurvival/5B Permanent Upgrades/";
    private const string BackupFolder = "Assets/MonsterSurvival/Features/Progression/Upgrade5B2/Backups";
    private const string LauncherName = "MS5B2_BTN_Upgrades";
    private const string LayerName = "MS5B2_UpgradeShop";

    [MenuItem(RootMenu + "3 - INSTALL 5B.2 Upgrade Shop (OPEN MainMenu)")]
    public static void Install()
    {
        if (Application.isPlaying) { Debug.LogWarning("[5B.2] Stop Play Mode first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string path=FindMainMenuScene();
        if (string.IsNullOrEmpty(path)) { EditorUtility.DisplayDialog("5B.2", "MainMenu.unity not found.", "OK");return; }
        if (!EditorUtility.DisplayDialog("MonsterSurvival 5B.2",
            "Install a NEW Permanent Upgrades shop in Main Menu V3?\n\n"+
            "This only edits MainMenu Scene, creates one launcher and one independent overlay. " +
            "Profile Gold, JSON saves, 5A.4 Manage Saves and gameplay are NOT modified by installing.",
            "INSTALL", "CANCEL")) return;
        try
        {
            EnsureFolder(BackupFolder);
            string backup = BackupFolder + "/MainMenu_BEFORE_5B2.unity";
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(backup))
            {
                if (!AssetDatabase.CopyAsset(path,backup)) throw new IOException("Could not back up MainMenu Scene.");
                AssetDatabase.ImportAsset(backup);
            }
            Scene scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            GameObject canvas=Find(scene,"MMV3_Canvas");
            GameObject home=Find(scene,"PAGE_Home");
            if (!canvas || !home || !home.transform.IsChildOf(canvas.transform))
                throw new InvalidOperationException("MMV3_Canvas/PAGE_Home missing; install Main Menu V3 first.");
            MS5A2ProfileWidget widget=FindComponent<MS5A2ProfileWidget>(scene);
            if (!widget) throw new InvalidOperationException("5A.2 Profile Widget missing. Install 5A.2 first.");
            if (!canvas.GetComponent<Canvas>() || !canvas.GetComponent<GraphicRaycaster>())
                throw new InvalidOperationException("MMV3_Canvas missing Canvas or GraphicRaycaster.");

            GameObject oldLayer=Find(scene,LayerName),oldButton=Find(scene,LauncherName);
            if (oldLayer || oldButton)
            {
                MS5B2ShopUI existing=oldLayer?oldLayer.GetComponent<MS5B2ShopUI>():null;
                if (existing && existing.openButton && existing.profileWidget && oldButton &&
                    existing.openButton.gameObject==oldButton)
                {
                    Debug.Log("[5B.2] Already installed; no duplicates created.");
                    EditorUtility.DisplayDialog("5B.2", "Already installed; no changes made.", "OK");return;
                }
                throw new InvalidOperationException("Incomplete 5B.2 objects already exist. Inspect Hierarchy before reinstalling.");
            }

            GameObject launch=new GameObject(LauncherName,typeof(RectTransform),typeof(Image),typeof(Button));
            launch.layer=5;launch.transform.SetParent(home.transform,false);
            RectTransform rt=launch.GetComponent<RectTransform>();
            rt.anchorMin=rt.anchorMax=new Vector2(1,1);rt.pivot=new Vector2(1,1);
            // BELOW 5A.4 MANAGE SAVES (-177, height 52), avoiding overlap.
            rt.anchoredPosition=new Vector2(-36,-239);rt.sizeDelta=new Vector2(245,52);
            Image launchImage=launch.GetComponent<Image>();launchImage.color=new Color(.28f,.19f,.40f,.98f);
            launchImage.raycastTarget=true;
            Button launchButton=launch.GetComponent<Button>();launchButton.targetGraphic=launchImage;
            launchButton.transition=Selectable.Transition.None;
            MS5B2ButtonMotion hover=launch.AddComponent<MS5B2ButtonMotion>();
            hover.targetGraphic=launchImage;hover.baseColor=launchImage.color;
            GameObject labelGo=new GameObject("TXT_Upgrades",typeof(RectTransform),typeof(TextMeshProUGUI));
            labelGo.layer=5;labelGo.transform.SetParent(launch.transform,false);
            RectTransform lr=labelGo.GetComponent<RectTransform>();lr.anchorMin=Vector2.zero;lr.anchorMax=Vector2.one;
            lr.offsetMin=lr.offsetMax=Vector2.zero;
            TextMeshProUGUI label=labelGo.GetComponent<TextMeshProUGUI>();
            label.text="UPGRADES";label.fontSize=23;label.fontSizeMin=14;label.fontSizeMax=23;
            label.enableAutoSizing=true;label.color=new Color(.96f,.88f,.99f,1f);label.alignment=TextAlignmentOptions.Center;
            label.raycastTarget=false;
            if (TMP_Settings.defaultFontAsset)label.font=TMP_Settings.defaultFontAsset;

            GameObject overlay=new GameObject(LayerName,typeof(RectTransform),typeof(MS5B2ShopUI));
            overlay.layer=5;overlay.transform.SetParent(canvas.transform,false);
            RectTransform overlayRect=overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin=Vector2.zero;overlayRect.anchorMax=Vector2.one;overlayRect.offsetMin=overlayRect.offsetMax=Vector2.zero;
            overlay.transform.SetAsLastSibling();
            MS5B2ShopUI shop=overlay.GetComponent<MS5B2ShopUI>();
            shop.openButton=launchButton;shop.profileWidget=widget;
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("[5B.2] Installed. No save files were written. Open MainMenu and click UPGRADES.");
            EditorUtility.DisplayDialog("5B.2 INSTALLED",
                "MainMenu: UPGRADES button added below MANAGE SAVES.\n" +
                "Play Mode: opening is read-only; clicking UPGRADE spends Bank Gold and SAVES immediately.\n"+
                "BONUSES DO NOT YET APPLY TO COMBAT (planned 5B.3).", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogError("[5B.2] Install failed: "+ex);
            EditorUtility.DisplayDialog("5B.2 ERROR",ex.Message,"OK");
        }
    }

    [MenuItem(RootMenu + "4 - AUDIT 5B.2 Shop (READ ONLY)")]
    public static void Audit()
    {
        Scene scene=SceneManager.GetActiveScene();
        GameObject canvas=Find(scene,"MMV3_Canvas"),home=Find(scene,"PAGE_Home");
        GameObject root=Find(scene,LayerName),launcher=Find(scene,LauncherName);
        MS5B2ShopUI shop=root?root.GetComponent<MS5B2ShopUI>():null;
        Debug.Log("[5B.2 AUDIT] Scene="+scene.name+
            " Canvas="+(canvas?"PASS":"MISSING")+
            " Home="+(home?"PASS":"MISSING")+
            " Shop="+(shop?"PASS":"MISSING")+
            " Launcher="+(launcher && launcher.GetComponent<Button>()?"PASS":"MISSING")+
            " ProfileRef="+(shop && shop.profileWidget?"PASS":"MISSING")+
            " ButtonRef="+(shop && shop.openButton?"PASS":"MISSING"));
        for (int i=1;i<=3;i++)
        {
            MS5B1PermanentUpgrades.Snapshot data;
            string status;
            bool ok=MS5B1PermanentUpgrades.TryGetSnapshot(i,out data,out status);
            Debug.Log("[5B.2 AUDIT] Profile "+i+" "+(ok ?
                "READ OK | Gold="+data.bankGold+" | Rev="+data.revision : "BLOCKED")+" | "+status);
        }
        Debug.Log("[5B.2 AUDIT] READ ONLY. This audit does not purchase, save or modify Scene.");
    }

    [MenuItem(RootMenu + "5 - REMOVE 5B.2 Shop UI ONLY")]
    public static void Remove()
    {
        if (Application.isPlaying) { Debug.LogWarning("[5B.2] Stop Play Mode first.");return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string path=FindMainMenuScene();
        if (string.IsNullOrEmpty(path))return;
        if (!EditorUtility.DisplayDialog("REMOVE 5B.2 SHOP",
            "Remove only the 5B.2 shop and its UPGRADES launcher from MainMenu?\n"+
            "Already purchased upgrades, Bank Gold and save backups WILL NOT be deleted.","REMOVE UI","CANCEL"))return;
        Scene scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
        GameObject shop=Find(scene,LayerName),launcher=Find(scene,LauncherName);
        if (shop && shop.GetComponent<MS5B2ShopUI>())UnityEngine.Object.DestroyImmediate(shop);
        if (launcher && launcher.GetComponent<MS5B2ButtonMotion>() && launcher.GetComponent<Button>())
            UnityEngine.Object.DestroyImmediate(launcher);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("[5B.2] UI removed; no player save data modified.");
    }

    private static string FindMainMenuScene()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Scene MainMenu"))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path).Equals("MainMenu",StringComparison.OrdinalIgnoreCase)
                && !path.Contains("/Backups/"))return path;
        }
        return null;
    }
    private static T FindComponent<T>(Scene scene) where T:Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component=root.GetComponentInChildren<T>(true);
            if (component)return component;
        }
        return null;
    }
    private static GameObject Find(Scene scene,string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name==name)return root;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name==name)return child.gameObject;
        }
        return null;
    }
    private static void EnsureFolder(string path)
    {
        string[] pieces=path.Split('/');string current=pieces[0];
        for(int i=1;i<pieces.Length;i++)
        {
            string next=current+"/"+pieces[i];
            if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,pieces[i]);
            current=next;
        }
    }
}
#endif
