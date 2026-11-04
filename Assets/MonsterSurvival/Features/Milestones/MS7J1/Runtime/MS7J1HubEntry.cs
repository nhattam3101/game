using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// MainMenu host stays loaded. PlayerHub is a *separate additive Scene* so existing
// MainMenu-only Save/Quest/Endless scripts still run with their correct context.
[DisallowMultipleComponent]
[DefaultExecutionOrder(15000)]
public sealed class MS7J1HubEntry : MonoBehaviour
{
    public MMV3MenuController menu;
    [Tooltip("Build Profile scene path. A full path avoids ambiguity with scenes that share a file name.")]
    public string playerHubSceneName="Assets/MonsterSurvival/Content/Scenes/PlayerHub.unity";
    public bool cleanMainMenu=true;
    public string LastStatus { get; private set; }="NOT INITIALIZED";
    private bool opening;
    private GameObject entry;
    private static readonly string[] HomeButtons={
        "MS5A4_BTN_Open","MS5B2_BTN_Upgrades", "MS5C4_BTN_Achievements", "MS5D1_BTN_ProfileOverview"
    };

    private IEnumerator Start()
    {
        if (gameObject.scene.name!="MainMenu") yield break;
        if (!menu) menu=GetComponent<MMV3MenuController>();
        if (!menu || !menu.homePage) {LastStatus="NO MAIN MENU V3";yield break;}
        // 7H1/7H2 construct launchers during Start. Hide only after they exist.
        yield return null;
        CreateEntry();
        ApplyCleanMenu();
        LastStatus="READY: PLAYER HUB (additive)";
    }
    private void CreateEntry()
    {
        if (entry || !menu.homePage) return;
        var button=MS7J1HubUI.Button("MS7J1_BTN_PlayerHub",menu.homePage.transform,
            "PLAYER HUB",MS7J1HubUI.CardSelected,MS7J1HubUI.Gold,
            new Vector2(0,1),new Vector2(0,1),new Vector2(0,1),new Vector2(225,54),new Vector2(116,-35),OpenHub);
        entry=button.gameObject;
    }
    private void ApplyCleanMenu()
    {
        if (!cleanMainMenu) return;
        foreach(string name in HomeButtons)
        {
            HideLauncher(name);
        }
        HideLauncher("Quest Launcher");
        HideLauncher("Progress Launcher");
        HideLauncher("BTN_7I4Records");
    }
    private void HideLauncher(string name)
    {
        var scene=gameObject.scene;
        foreach(var root in scene.GetRootGameObjects())
        {
            Transform t=FindNamed(root.transform,name);
            if (t)
            {
                // Keep original onClick listeners and service lifecycle alive.
                // 7J.1 used SetActive(false), which broke some dynamic popup openers.
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                CanvasGroup cg=t.GetComponent<CanvasGroup>();
                if (!cg) cg=t.gameObject.AddComponent<CanvasGroup>();
                cg.alpha=0f;
                cg.blocksRaycasts=false;
                cg.interactable=false;
                return;
            }
        }
    }
    private static Transform FindNamed(Transform root,string name)
    {
        if (root.name==name) return root;
        foreach(Transform child in root)
        {
            var found=FindNamed(child,name);if(found) return found;
        }
        return null;
    }
    public void OpenHub()
    {
        if(opening || SceneManager.GetSceneByPath(playerHubSceneName).isLoaded) return;
        if (!Application.CanStreamedLevelBeLoaded(playerHubSceneName))
        {
            LastStatus="BLOCKED: Scene is missing or disabled in the active Build Profile: "+playerHubSceneName;
            Debug.LogError("[7J.1] "+LastStatus, this);
            return;
        }
        StartCoroutine(LoadHub());
    }
    private IEnumerator LoadHub()
    {
        opening=true;
        var op=SceneManager.LoadSceneAsync(playerHubSceneName,LoadSceneMode.Additive);
        if(op==null){opening=false;LastStatus="LoadSceneAsync failed";yield break;}
        while(!op.isDone) yield return null;
        Scene main=gameObject.scene;
        if (main.IsValid() && main.isLoaded) SceneManager.SetActiveScene(main);
        opening=false;
        LastStatus="PlayerHub opened; original MainMenu host preserved";
    }
    public void RestoreHomePage()
    {
        if (!menu) return;
        if(menu.homePage) menu.homePage.SetActive(true);
        if(menu.mapsPage) menu.mapsPage.SetActive(false);
        if(menu.settingsPage) menu.settingsPage.SetActive(false);
        ApplyCleanMenu();
    }
}
