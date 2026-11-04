using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Independent Menu V3. Does not click or depend on any old UI buttons.
[DisallowMultipleComponent]
public sealed class MMV3MenuController : MonoBehaviour
{
    [Header("Scenes")]
    public string gameplaySceneName = "SampleScene";

    [Header("Pages")]
    public GameObject homePage;
    public GameObject mapsPage;
    public GameObject settingsPage;

    [Header("Buttons")]
    public Button playButton;
    public Button mapsButton;
    public Button settingsButton;
    public Button quitButton;
    public Button mapsBackButton;
    public Button mapsStartButton;
    public Button settingsBackButton;
    public Button[] mapCards = new Button[3];

    [Header("Map widgets")]
    public Image[] mapCardBorders = new Image[3];
    public TMP_Text[] mapStates = new TMP_Text[3];
    public TMP_Text selectedMapCaption;

    [Header("Sound")]
    public Slider musicSlider;
    public Slider sfxSlider;
    public TMP_Text musicPercent;
    public TMP_Text sfxPercent;

    [Header("Messages")]
    public TMP_Text footerMessage;

    private static readonly string[] Names = { "GREEN MEADOW", "SAND DUNES", "ANCIENT RUINS" };
    private int selectedMap;
    private bool loading;

    private void Awake()
    {
        if (playButton) playButton.onClick.AddListener(ShowMaps);
        if (mapsButton) mapsButton.onClick.AddListener(ShowMaps);
        if (settingsButton) settingsButton.onClick.AddListener(ShowSettings);
        if (quitButton) quitButton.onClick.AddListener(Quit);
        if (mapsBackButton) mapsBackButton.onClick.AddListener(ShowHome);
        if (settingsBackButton) settingsBackButton.onClick.AddListener(ShowHome);
        if (mapsStartButton) mapsStartButton.onClick.AddListener(StartSelectedMap);
        if (mapCards != null)
            for (int i = 0; i < mapCards.Length && i < 3; i++)
            {
                int mapIndex = i;
                if (mapCards[i]) mapCards[i].onClick.AddListener(() => SelectMap(mapIndex));
            }
        if (musicSlider) musicSlider.onValueChanged.AddListener(SetMusic);
        if (sfxSlider) sfxSlider.onValueChanged.AddListener(SetSFX);
    }

    private void Start()
    {
        Time.timeScale = 1f;
        SelectMap(0);
        ShowHome();
    }

    private void ShowHome()
    {
        if (loading) return;
        if (homePage) homePage.SetActive(true);
        if (mapsPage) mapsPage.SetActive(false);
        if (settingsPage) settingsPage.SetActive(false);
        Message("SELECT YOUR DESTINY");
    }

    private void ShowMaps()
    {
        if (loading) return;
        if (homePage) homePage.SetActive(false);
        if (mapsPage) mapsPage.SetActive(true);
        if (settingsPage) settingsPage.SetActive(false);
        SelectMap(selectedMap);
    }

    private void ShowSettings()
    {
        if (loading) return;
        if (homePage) homePage.SetActive(false);
        if (mapsPage) mapsPage.SetActive(false);
        if (settingsPage) settingsPage.SetActive(true);
        var audio = AudioManager.Instance;
        float music = audio ? audio.GetMusicVolume() : 0.6f;
        float sfx = audio ? audio.GetSFXVolume() : 0.8f;
        if (musicSlider) musicSlider.SetValueWithoutNotify(music);
        if (sfxSlider) sfxSlider.SetValueWithoutNotify(sfx);
        UpdateSoundLabels(music, sfx);
    }

    private void SelectMap(int index)
    {
        if (index < 0 || index >= 3) return;
        selectedMap = index;
        for (int i = 0; i < 3; i++)
        {
            bool on = i == selectedMap;
            if (mapCardBorders != null && i < mapCardBorders.Length && mapCardBorders[i])
                mapCardBorders[i].color = on ? new Color(0.75f, 0.47f, 0.94f, 1f)
                                               : new Color(0.20f, 0.17f, 0.30f, 1f);
            if (mapStates != null && i < mapStates.Length && mapStates[i])
            {
                mapStates[i].text = on ? "SELECTED" : "CLICK TO SELECT";
                mapStates[i].color = on ? new Color(0.95f, 0.78f, 0.47f) : new Color(0.67f, 0.67f, 0.75f);
            }
        }
        if (selectedMapCaption) selectedMapCaption.text = "SELECTED MAP  /  " + Names[selectedMap];
    }

    private void StartSelectedMap()
    {
        if (loading) return;
        if (string.IsNullOrEmpty(gameplaySceneName) || !Application.CanStreamedLevelBeLoaded(gameplaySceneName))
        {
            Debug.LogError("[Menu V3] Scene '" + gameplaySceneName + "' is not enabled in Build Profiles.");
            Message("SCENE MISSING FROM BUILD PROFILES");
            return;
        }
        loading = true;
        MMV3MapSession.Set(selectedMap);
        Message("LOADING MAP...");
        MMV3TransitionCurtain.BeginLoad(gameplaySceneName);
    }

    private void SetMusic(float value)
    {
        if (AudioManager.Instance) AudioManager.Instance.SetMusicVolume(value);
        UpdateSoundLabels(value, sfxSlider ? sfxSlider.value : 0.8f);
    }

    private void SetSFX(float value)
    {
        if (AudioManager.Instance) AudioManager.Instance.SetSFXVolume(value);
        UpdateSoundLabels(musicSlider ? musicSlider.value : 0.6f, value);
    }

    private void UpdateSoundLabels(float music, float sfx)
    {
        if (musicPercent) musicPercent.text = Mathf.RoundToInt(music * 100f) + "%";
        if (sfxPercent) sfxPercent.text = Mathf.RoundToInt(sfx * 100f) + "%";
    }

    private void Message(string value) { if (footerMessage) footerMessage.text = value; }

    private void Quit()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
