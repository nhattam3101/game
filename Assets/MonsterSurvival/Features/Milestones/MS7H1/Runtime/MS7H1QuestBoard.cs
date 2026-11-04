using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Runtime-generated UI, only in MainMenu; does not replace existing Menu V3.
[DisallowMultipleComponent]
public sealed class MS7H1QuestBoard : MonoBehaviour
{
    private Canvas canvas;
    private GameObject overlay;
    private TMP_Text subtitle;
    private TMP_Text feedback;
    private TMP_Text[] progress = new TMP_Text[3];
    private Button[] claims = new Button[3];
    private int capturedSlot;
    private float nextRefresh;
    private bool opening;

    // 7H.1.1: only adjust the FOUR existing Scene buttons; do not create duplicates.
    // Quest Board/Claim and Save/Profile logic below are preserved.
    private readonly RectTransform[] mainMenuActions = new RectTransform[4];
    private RectTransform homeRect;
    private RectTransform profileRect;
    private RectTransform launcherRect;
    private float lastMeasuredHomeWidth = -1f;

    private static readonly Color Purple = new Color(0.09f, 0.065f, 0.145f, 0.97f);
    private static readonly Color Card = new Color(0.17f, 0.12f, 0.25f, 1f);
    private static readonly Color Gold = new Color(0.91f, 0.73f, 0.43f, 1f);
    private static readonly Color Lilac = new Color(0.83f, 0.76f, 0.93f, 1f);

    private void Start()
    {
        if (SceneManager.GetActiveScene().name != "MainMenu") return;
        BuildUI();
        CacheExistingMainMenuButtons();
        ApplyResponsiveHeaderLayout();
    }
    private void Update()
    {
        if (!canvas) return;
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.75f;
        ApplyResponsiveHeaderLayout();
        int slot = MS5A2GoldBank.SelectedSlot;
        if (slot != capturedSlot && overlay && overlay.activeSelf)
        {
            capturedSlot = slot;
            feedback.text = "Profile changed. Check this slot before claiming.";
        }
        if (overlay && overlay.activeSelf) Refresh();
    }
    private static GameObject Make(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }
    private static RectTransform Rect(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchored, Vector2 size)
    {
        var r = (RectTransform)go.transform;
        r.anchorMin = anchorMin; r.anchorMax = anchorMax; r.pivot = pivot;
        r.anchoredPosition = anchored; r.sizeDelta = size;
        return r;
    }
    private static Image Panel(GameObject go, Color color)
    {
        var image = go.AddComponent<Image>(); image.color = color; return image;
    }
    private static TMP_Text Text(GameObject go, string value, float fontSize, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = value; t.fontSize = fontSize; t.color = color; t.alignment = alignment;
        t.raycastTarget = false;
        return t;
    }
    private static Button AddButton(Transform parent, string name, string label, Color bg, Color foreground,
        Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        var go = Make(name, parent); Rect(go, min, max, pivot, position, size);
        Panel(go, bg); var button = go.AddComponent<Button>(); button.targetGraphic = go.GetComponent<Image>();
        button.onClick.AddListener(onClick);
        var textObj = Make("Label", go.transform);
        Rect(textObj, Vector2.zero, Vector2.one, new Vector2(.5f,.5f), Vector2.zero, Vector2.zero);
        Text(textObj, label, 23, foreground, TextAlignmentOptions.Center);
        return button;
    }
    private void BuildUI()
    {
        var go = Make("7H1 Quest Canvas [runtime]", transform);
        canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 250;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
        go.AddComponent<GraphicRaycaster>();
        if (EventSystem.current == null)
            Debug.LogWarning("[7H.1] No EventSystem found. UI buttons require the project's existing EventSystem.", this);

        // 7H.1.1: bottom-right Quest launcher, below the magic circle.
        // The four existing system buttons are positioned by Scene + responsive code.
        // Stay BELOW the "THE SHADOWS AWAKEN" caption and the magic circle.
        Button launch = AddButton(go.transform, "Quest Launcher", "QUESTS", Card, Gold,
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
            new Vector2(-36, 26), new Vector2(176, 48), Toggle);
        launcherRect = launch.GetComponent<RectTransform>();

        overlay = Make("Quest Board Overlay", go.transform);
        Rect(overlay, Vector2.zero, Vector2.one, new Vector2(.5f,.5f), Vector2.zero, Vector2.zero);
        Panel(overlay, new Color(0f,0f,0f, .77f));
        var box = Make("Quest Board", overlay.transform);
        Rect(box, new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(.5f,.5f), Vector2.zero, new Vector2(940,710));
        Panel(box, Purple);
        var heading = Make("Title", box.transform);
        Rect(heading, new Vector2(.5f,1), new Vector2(.5f,1), new Vector2(.5f,1), new Vector2(0,-35), new Vector2(740,60));
        Text(heading, "LONG-TERM QUESTS", 41, Gold, TextAlignmentOptions.Center);
        var sub = Make("Profile", box.transform);
        Rect(sub, new Vector2(.5f,1), new Vector2(.5f,1), new Vector2(.5f,1), new Vector2(0,-109), new Vector2(830,46));
        subtitle = Text(sub, "Loading profile...", 23, Lilac, TextAlignmentOptions.Center);
        AddButton(box.transform, "Close", "X", Card, Gold,
            new Vector2(1,1), new Vector2(1,1), new Vector2(1,1), new Vector2(-20,-16), new Vector2(55,50), Toggle);
        for (int i=0; i<3; i++)
        {
            int index=i;
            var card = Make("Quest " + i, box.transform);
            Rect(card, new Vector2(.5f,1), new Vector2(.5f,1), new Vector2(.5f,1), new Vector2(0,-175-i*145), new Vector2(842,126));
            Panel(card, Card);
            var title = Make("Title",card.transform);
            Rect(title, new Vector2(0,1),new Vector2(0,1),new Vector2(0,1),new Vector2(20,-10),new Vector2(620,35));
            Text(title, MS7H1QuestService.Title((MS7H1QuestId)i), 26, Gold);
            var desc = Make("Description",card.transform);
            Rect(desc, new Vector2(0,1),new Vector2(0,1),new Vector2(0,1),new Vector2(20,-47),new Vector2(620,40));
            Text(desc, MS7H1QuestService.Description((MS7H1QuestId)i), 20, Lilac);
            var status = Make("Status",card.transform);
            Rect(status, new Vector2(0,0),new Vector2(0,0),new Vector2(0,0),new Vector2(20,8),new Vector2(630,32));
            progress[i] = Text(status, "PENDING", 20, Color.white);
            claims[i] = AddButton(card.transform, "Claim", "CLAIM", Gold, Purple,
                new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-19,0),new Vector2(170,52), () => Claim(index));
        }
        var footer=Make("Feedback",box.transform);
        Rect(footer,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,23),new Vector2(840,42));
        feedback=Text(footer,"Rewards are added to the existing 5A Bank Gold save.",19,Lilac,TextAlignmentOptions.Center);
        overlay.SetActive(false);
    }
    // Uses the real MainMenu hierarchy from the supplied MainMenu.unity file.
    // No changes to the original Button components, OnClick or service references.
    private void CacheExistingMainMenuButtons()
    {
        GameObject menuCanvas = GameObject.Find("MMV3_Canvas");
        if (menuCanvas == null) return;

        Transform page = menuCanvas.transform.Find("MMV3_Root/PAGE_Home");
        if (page == null) return;

        homeRect = page as RectTransform;
        Transform status = page.Find("MS5A2_ProfileCard");
        profileRect = status as RectTransform;

        string[] names = {
            "MS5A4_BTN_Open", "MS5B2_BTN_Upgrades",
            "MS5C4_BTN_Achievements", "MS5D1_BTN_ProfileOverview"
        };
        for (int i = 0; i < names.Length; i++)
        {
            Transform child = page.Find(names[i]);
            mainMenuActions[i] = child as RectTransform;
        }
    }

    private void ApplyResponsiveHeaderLayout()
    {
        if (homeRect == null) CacheExistingMainMenuButtons();
        if (homeRect == null) return;

        float width = homeRect.rect.width;
        if (width <= 1f || Mathf.Abs(width - lastMeasuredHomeWidth) < 1f) return;
        lastMeasuredHomeWidth = width;

        // Leave space for the original Profile/Bank Gold card at upper-right.
        float statusWidth = profileRect != null ? profileRect.rect.width : 392f;
        float rightReserve = statusWidth + 36f + 28f;
        float left = Mathf.Max(46f, width * 0.075f);
        float usable = Mathf.Max(0f, width - rightReserve - left);
        const float gap = 12f;
        bool compact = usable < (4f * 142f + 3f * gap);
        float itemWidth = compact
            ? Mathf.Clamp((usable - gap) * .5f, 110f, 220f)
            : Mathf.Clamp((usable - 3f * gap) * .25f, 142f, 220f);

        for (int i = 0; i < mainMenuActions.Length; i++)
        {
            RectTransform action = mainMenuActions[i];
            if (action == null) continue;
            int column = compact ? i % 2 : i;
            int row = compact ? i / 2 : 0;
            action.anchorMin = new Vector2(0f, 1f);
            action.anchorMax = new Vector2(0f, 1f);
            action.pivot = new Vector2(0f, 1f);
            action.sizeDelta = new Vector2(itemWidth, compact ? 43f : 49f);
            action.anchoredPosition = new Vector2(left + column * (itemWidth + gap),
                -34f - row * 49f);

            TMP_Text label = action.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.enableAutoSizing = true;
                label.fontSizeMin = 12f;
                label.fontSizeMax = 20f;
                label.fontSize = 20f;
            }
        }

        if (launcherRect != null)
        {
            launcherRect.anchorMin = new Vector2(1f, 0f);
            launcherRect.anchorMax = new Vector2(1f, 0f);
            launcherRect.pivot = new Vector2(1f, 0f);
            launcherRect.anchoredPosition = new Vector2(-36f, 26f);
            launcherRect.sizeDelta = new Vector2(width < 1100f ? 154f : 176f, 48f);
        }
    }

    private void Toggle()
    {
        if (!overlay) return;
        opening=!overlay.activeSelf;
        overlay.SetActive(opening);
        if (opening)
        {
            capturedSlot=MS5A2GoldBank.SelectedSlot;
            feedback.text="Only completed quests can be claimed; each reward is one-time per Profile.";
            Refresh();
        }
    }
    private void Claim(int i)
    {
        if (MS5A2GoldBank.SelectedSlot != capturedSlot)
        { feedback.text="Profile changed. Reopen the board to confirm the new slot."; return; }
        string msg;
        MS7H1QuestService.TryClaim(capturedSlot, (MS7H1QuestId)i, out msg);
        feedback.text=msg;
        Refresh();
    }
    private void Refresh()
    {
        if (!overlay || !overlay.activeSelf) return;
        MS7H1QuestSnapshot snapshot=MS7H1QuestService.ReadSnapshot(capturedSlot);
        subtitle.text="PROFILE " + capturedSlot.ToString("00") + "     Bank Gold: " + (snapshot.valid ? snapshot.gold.ToString() : "—") +
            "     Save: " + snapshot.message;
        for (int i=0;i<3;i++)
        {
            var q=snapshot.quests[i];
            string status= q.claimed ? "CLAIMED" : q.completed ? "READY  +"+q.reward+" Gold" : "LOCKED  +"+q.reward+" Gold";
            progress[i].text=status;
            progress[i].color=q.claimed ? new Color(.6f,.85f,.67f,1f) : q.completed ? Gold : Lilac;
            claims[i].interactable=snapshot.valid && !snapshot.backupRecovery && q.completed && !q.claimed;
        }
    }
}
