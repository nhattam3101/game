using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menu-only addon: two compact buttons ABOVE the existing Start Adventure button.
// Does not edit MMV3MenuController or interfere with its map selection logic.
[DefaultExecutionOrder(-1000)] // Register START handler before MMV3MenuController.Awake.
[DisallowMultipleComponent]
public sealed class MS7I2ModeMenu : MonoBehaviour
{
    public MMV3MenuController menu;
    public MS7I2GameMode selectedMode = MS7I2GameMode.Normal;

    private GameObject panel;
    private Button normalButton;
    private Button endlessButton;
    private Image normalBackground;
    private Image endlessBackground;
    private Text normalLabel;
    private Text endlessLabel;
    private bool listening;

    private static readonly Color Back = new Color(0.12f, 0.075f, 0.20f, 0.97f);
    private static readonly Color Active = new Color(0.40f, 0.24f, 0.64f, 0.98f);
    private static readonly Color Gold = new Color(0.95f, 0.77f, 0.45f, 1f);
    private static readonly Color Lilac = new Color(0.86f, 0.81f, 0.94f, 1f);

    public string Status { get; private set; } = "NOT INITIALIZED";
    public bool UiBuilt { get { return panel != null; } }

    private void Awake()
    {
        if (SceneManager.GetActiveScene().name != "MainMenu") { enabled = false; return; }
        MS7I2ModeSession.EnterMainMenu();
        if (menu == null) menu = GetComponent<MMV3MenuController>();
        if (menu == null || menu.mapsPage == null || menu.mapsStartButton == null)
        {
            Status = "BLOCKED: Missing MMV3 map page / Start button";
            Debug.LogError("[7I.2] " + Status, this);
            enabled = false;
            return;
        }
        selectedMode = MS7I2GameMode.Normal;
        // MMV3MenuController adds its own StartSelectedMap listener later in Awake (execution order 0).
        menu.mapsStartButton.onClick.AddListener(CommitModeBeforeMapStart);
        listening = true;
        BuildButtons();
        Status = "READY: NORMAL selected; choose ENDLESS before Start Adventure";
    }

    private static RectTransform MakeRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        RectTransform r = go.GetComponent<RectTransform>();
        r.SetParent(parent, false);
        return r;
    }

    private void BuildButtons()
    {
        RectTransform mapRect = menu.mapsPage.transform as RectTransform;
        if (mapRect == null) { Status = "BLOCKED: Maps Page is not a RectTransform"; return; }
        RectTransform root = MakeRect("7I2 Mode Choice [runtime]", mapRect);
        panel = root.gameObject;
        // Actual BTN_StartAdventure in MainMenu.unity: x=0.66..0.94, y=0.072..0.16.
        // This row sits directly above it: x=0.66..0.94, y=0.175..0.238.
        root.anchorMin = new Vector2(0.66f, 0.175f);
        root.anchorMax = new Vector2(0.94f, 0.238f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        // The root is in PAGE_SelectMap; it is hidden automatically on Home/Settings.
        normalButton = CreateModeButton(root, "BTN_Normal", "NORMAL", 0.0f, 0.487f, out normalBackground, out normalLabel);
        endlessButton = CreateModeButton(root, "BTN_Endless", "ENDLESS", 0.513f, 1f, out endlessBackground, out endlessLabel);
        normalButton.onClick.AddListener(() => Choose(MS7I2GameMode.Normal));
        endlessButton.onClick.AddListener(() => Choose(MS7I2GameMode.Endless));
        RefreshColors();
    }

    private Button CreateModeButton(RectTransform root, string name, string label,
        float minX, float maxX, out Image background, out Text text)
    {
        RectTransform r = MakeRect(name, root);
        r.anchorMin = new Vector2(minX, 0f);
        r.anchorMax = new Vector2(maxX, 1f);
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        background = r.gameObject.AddComponent<Image>();
        background.color = Back;
        Button button = r.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, .92f, 1f, 1f);
        colors.pressedColor = new Color(.78f, .70f, .90f, 1f);
        button.colors = colors;
        RectTransform labelRect = MakeRect("Label", r);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(5f, 2f);
        labelRect.offsetMax = new Vector2(-5f, -2f);
        // 7H.3 had an intermittent TMP rendering issue; use Unity 6.6 legacy UI font here.
        text = labelRect.gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 21;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 13;
        text.resizeTextMaxSize = 21;
        text.raycastTarget = false;
        text.color = Lilac;
        return button;
    }

    private void Choose(MS7I2GameMode mode)
    {
        selectedMode = mode;
        RefreshColors();
        Status = "SELECTED: " + mode + " (not saved to Profile)";
    }

    private void RefreshColors()
    {
        if (normalBackground) normalBackground.color = selectedMode == MS7I2GameMode.Normal ? Active : Back;
        if (endlessBackground) endlessBackground.color = selectedMode == MS7I2GameMode.Endless ? Active : Back;
        if (normalLabel) normalLabel.color = selectedMode == MS7I2GameMode.Normal ? Gold : Lilac;
        if (endlessLabel) endlessLabel.color = selectedMode == MS7I2GameMode.Endless ? Gold : Lilac;
    }

    private void CommitModeBeforeMapStart()
    {
        if (menu == null || string.IsNullOrEmpty(menu.gameplaySceneName) ||
            !Application.CanStreamedLevelBeLoaded(menu.gameplaySceneName))
        {
            Status = "BLOCKED: gameplay scene is missing from Build Profiles";
            return;
        }
        MS7I2ModeSession.CommitForNextGameplay(selectedMode);
        Status = "COMMITTED: " + selectedMode + " / Start Adventure pressed";
        Debug.Log("[7I.2] " + Status, this);
    }

    private void OnDestroy()
    {
        if (listening && menu != null && menu.mapsStartButton != null)
            menu.mapsStartButton.onClick.RemoveListener(CommitModeBeforeMapStart);
    }
}
