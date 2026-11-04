using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// One hub Scene, seven gateways to the EXISTING battle-tested player management UIs.
// It contains presentation only: never directly writes 5A Save, Quests, or Endless rewards.
[DisallowMultipleComponent]
public sealed class MS7J1PlayerHubScene : MonoBehaviour
{
    public string hostSceneName="MainMenu";
    public string State { get; private set; }="STARTING";
    private MS7J1HubEntry host;
    private Canvas dashboard, shield, returnBar;
    private Text status, profile;
    private bool busy;
    private readonly List<Canvas> promotedCanvases=new List<Canvas>();
    private readonly List<GraphicRaycaster> promotedRaycasters=new List<GraphicRaycaster>();

    private IEnumerator Start()
    {
        if(gameObject.scene.name!="PlayerHub") { State="WRONG SCENE";yield break; }
        // Direct Play on PlayerHub.unity is unsupported: MainMenu-only Quest scripts
        // check the active Scene at Start. Enter ONLY via MainMenu's PLAYER HUB button.
        if(!SceneManager.GetSceneByName(hostSceneName).isLoaded)
        {
            State="BLOCKED: Press Play in MainMenu, then open PLAYER HUB";
            Debug.LogError("[7J.1] "+State);
            yield break;
        }
        var scene=SceneManager.GetSceneByName(hostSceneName);
        foreach(var root in scene.GetRootGameObjects())
        {
            host=root.GetComponentInChildren<MS7J1HubEntry>(true);
            if(host)break;
        }
        if(!host){State="BLOCKED: MainMenu needs MS7J1HubEntry installer";Debug.LogError("[7J.1] "+State);yield break;}
        Build(); State="READY";
    }
    private void Build()
    {
        shield=MS7J1HubUI.Canvas("PlayerHub_QuestBackdrop",transform,240);
        MS7J1HubUI.Fill(MS7J1HubUI.Stretch("Backdrop",shield.transform).gameObject,MS7J1HubUI.Ink);
        shield.gameObject.SetActive(false);
        dashboard=MS7J1HubUI.Canvas("PlayerHub_Dashboard",transform,310);
        MS7J1HubUI.Fill(MS7J1HubUI.Stretch("Opaque Background",dashboard.transform).gameObject,MS7J1HubUI.Ink);
        MS7J1HubUI.Label("Title",dashboard.transform,"PLAYER HUB",58,TextAnchor.MiddleLeft,MS7J1HubUI.Gold,
            new Vector2(.07f,.865f),new Vector2(.71f,.95f),new Vector2(0,.5f),Vector2.zero,Vector2.zero);
        MS7J1HubUI.Label("Subtitle",dashboard.transform,"PROFILE  /  UPGRADES  /  ACHIEVEMENTS  /  QUESTS  /  RECORDS",22,
            TextAnchor.MiddleLeft,MS7J1HubUI.Muted,new Vector2(.07f,.805f),new Vector2(.93f,.865f),new Vector2(0,.5f),Vector2.zero,Vector2.zero);
        profile=MS7J1HubUI.Label("Profile",dashboard.transform,"",27,TextAnchor.MiddleRight,MS7J1HubUI.Gold,
            new Vector2(.74f,.89f),new Vector2(.93f,.95f),new Vector2(1,.5f),Vector2.zero,Vector2.zero);
        MS7J1HubUI.Button("BackToMainMenu",dashboard.transform,"BACK TO MAIN MENU",MS7J1HubUI.Card,MS7J1HubUI.Pale,
            new Vector2(.72f,.075f),new Vector2(.93f,.145f),new Vector2(.5f,.5f),Vector2.zero,Vector2.zero,CloseHub);
        string[] titles={"MANAGE SAVES","PERMANENT UPGRADES","ACHIEVEMENTS","PROFILE & STATS",
            "LONG-TERM QUESTS","PROGRESS / DAILY / WEEKLY","ENDLESS RECORDS / REWARDS"};
        string[] descriptions={"Profiles / backup / recovery","Vitality / Strength / Fury / Agility",
            "Boss achievements / unlocks","Wins / statistics / personal progress",
            "One-time quests and claims","Milestones / daily / weekly claims","Highest wave / bosses / Gold claims"};
        for(int i=0;i<7;i++)
        {
            int selected=i;
            int col=i%2,row=i/2;
            float minX=col==0?.08f:.52f, maxX=col==0?.48f:.92f;
            float top=.775f-row*.151f;
            var b=MS7J1HubUI.Button("Tab_"+i,dashboard.transform,
                "0"+(i+1)+"   "+titles[i],MS7J1HubUI.CardSelected,MS7J1HubUI.Gold,
                new Vector2(minX,top-.112f),new Vector2(maxX,top),new Vector2(.5f,.5f),Vector2.zero,Vector2.zero,()=>OpenModule(selected));
            var label=b.GetComponentInChildren<Text>();
            if(label){label.alignment=TextAnchor.UpperLeft;label.fontSize=27;label.resizeTextMaxSize=27;}
            MS7J1HubUI.Label("Description",b.transform,descriptions[i],18,TextAnchor.MiddleLeft,MS7J1HubUI.Pale,
                new Vector2(.06f,.10f),new Vector2(.94f,.43f),new Vector2(.5f,.5f),Vector2.zero,Vector2.zero);
        }
        status=MS7J1HubUI.Label("Status",dashboard.transform,"Select an information tab. No Save changes from this hub.",19,
            TextAnchor.MiddleLeft,MS7J1HubUI.Muted,new Vector2(.08f,.05f),new Vector2(.67f,.14f),new Vector2(0,.5f),Vector2.zero,Vector2.zero);
        returnBar=MS7J1HubUI.Canvas("PlayerHub_ReturnBar",transform,400);
        MS7J1HubUI.Button("ReturnToHub",returnBar.transform,"<  PLAYER HUB",MS7J1HubUI.CardSelected,MS7J1HubUI.Gold,
            new Vector2(0,0),new Vector2(0,0),new Vector2(0,0),new Vector2(245,59),new Vector2(23,23),ReturnToHub);
        returnBar.gameObject.SetActive(false);
        RefreshProfile();
    }
    private void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if(Input.GetKeyDown(KeyCode.Escape) && !busy && dashboard && !dashboard.gameObject.activeSelf) ReturnToHub();
#endif
    }
    private void RefreshProfile()
    {
        if(profile)profile.text="PROFILE " + MS5A2GoldBank.SelectedSlot.ToString("00");
    }
    // Operates on the loaded MainMenu Scene; inactive legacy launchers retain their onClick events.
    private Button FindButton(string name)
    {
        Scene s=SceneManager.GetSceneByName(hostSceneName);
        if(!s.IsValid()||!s.isLoaded)return null;
        foreach(var root in s.GetRootGameObjects())
            foreach(var b in root.GetComponentsInChildren<Button>(true))
                if(b.gameObject.name==name)return b;
        return null;
    }
    private GameObject FindObject(string name)
    {
        Scene s=SceneManager.GetSceneByName(hostSceneName);
        if(!s.IsValid() || !s.isLoaded)return null;
        foreach(var root in s.GetRootGameObjects())
            foreach(var t in root.GetComponentsInChildren<RectTransform>(true))
                if(t.name==name)return t.gameObject;
        return null;
    }
    private void PromoteOriginalPopup(int index)
    {
        string[] popupNames={"MS5A4_SaveManager","MS5B2_UpgradeShop",
            "MS5C4_AchievementGallery","MS5D1_ProfileOverview","","", "7I4_Records_Modal"};
        string popup=popupNames[index];
        if(string.IsNullOrEmpty(popup))return;
        GameObject go=FindObject(popup);
        if(!go)return;
        // Nested Canvas renders the exact original panel ABOVE the PlayerHub's
        // opaque backdrop; original scripts and Unity UI components are untouched.
        Canvas canvas=go.GetComponent<Canvas>();
        if(!canvas){canvas=go.AddComponent<Canvas>();promotedCanvases.Add(canvas);}
        canvas.overrideSorting=true;
        canvas.sortingOrder=260;
        GraphicRaycaster ray=go.GetComponent<GraphicRaycaster>();
        if(!ray){ray=go.AddComponent<GraphicRaycaster>();promotedRaycasters.Add(ray);}
    }
    private void RestorePopupCanvases()
    {
        foreach(var r in promotedRaycasters)if(r)Destroy(r);
        promotedRaycasters.Clear();
        foreach(var c in promotedCanvases)if(c)Destroy(c);
        promotedCanvases.Clear();
    }
    private void OnDestroy(){RestorePopupCanvases();}
    private void OpenModule(int index)
    {
        if(busy||!dashboard||!dashboard.gameObject.activeSelf)return;
        string[] named={"MS5A4_BTN_Open","MS5B2_BTN_Upgrades","MS5C4_BTN_Achievements",
            "MS5D1_BTN_ProfileOverview","Quest Launcher","Progress Launcher","BTN_7I4Records"};
        if(index<0||index>=named.Length)return;
        var button=FindButton(named[index]);
        if(!button){status.text="MISSING: "+named[index]+". Check the original module installation.";State="MISSING MODULE";return;}
        // 7I4 Records panel is parented to Map Select and needs that page active.
        if(index==6 && host && host.menu)
        {
            if(host.menu.homePage)host.menu.homePage.SetActive(false);
            if(host.menu.mapsPage)host.menu.mapsPage.SetActive(true);
            if(host.menu.settingsPage)host.menu.settingsPage.SetActive(false);
        }
        PromoteOriginalPopup(index);
        // All modules get the PlayerHub's clean opaque backdrop. Quest panels already
        // render at 249/250; original legacy panels/7I4 are raised to sorting 260.
        shield.gameObject.SetActive(true);
        dashboard.gameObject.SetActive(false);
        returnBar.gameObject.SetActive(true);
        button.onClick.Invoke();
        State="OPEN: "+named[index];
    }
    public void ReturnToHub()
    {
        if(!dashboard||!host)return;
        returnBar.gameObject.SetActive(false);
        shield.gameObject.SetActive(false);
        RestorePopupCanvases();
        host.RestoreHomePage();
        dashboard.gameObject.SetActive(true);
        RefreshProfile();
        State="READY";
        if(status)status.text="Select another tab. Close the previous popup with X if needed.";
    }
    public void CloseHub()
    {
        if(busy)return;
        StartCoroutine(Unload());
    }
    private IEnumerator Unload()
    {
        busy=true;
        RestorePopupCanvases();
        if(host)host.RestoreHomePage();
        var op=SceneManager.UnloadSceneAsync(gameObject.scene);
        if(op!=null)while(!op.isDone)yield return null;
        busy=false;
    }
}
