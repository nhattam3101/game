using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// A small MAP SELECT button and modal. No new Gameplay HUD or Home-page clutter.
// Uses Unity UI Text (LegacyRuntime) to avoid the old TMP text-visibility issue.
[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public sealed class MS7I4RecordsMenu : MonoBehaviour
{
    public MMV3MenuController menu;
    public string Status { get; private set; } = "NOT STARTED";
    public bool Built { get { return panel != null; } }

    private GameObject panel;
    private Text details;
    private Text statusText;
    private Button claimButton;
    private int viewedSlot;
    private static readonly Color PanelColor = new Color(.075f, .049f, .125f, .985f);
    private static readonly Color CardColor = new Color(.16f, .11f, .25f, 1f);
    private static readonly Color Gold = new Color(.95f, .76f, .42f, 1f);
    private static readonly Color Pale = new Color(.90f, .86f, .95f, 1f);

    private void Awake()
    {
        if (SceneManager.GetActiveScene().name != "MainMenu") { enabled = false; return; }
        if (!menu) menu = GetComponent<MMV3MenuController>();
        if (!menu || !menu.mapsPage) { Status = "BLOCKED: Map Select page missing"; return; }
        Build();
        Status = "READY: open RECORDS in Map Select";
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        RectTransform r = go.GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = min; r.anchorMax = max;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        return r;
    }

    private static Text TextAt(string name, Transform parent, string value,
        Vector2 min, Vector2 max, int fontSize, TextAnchor align, Color color)
    {
        RectTransform r = Rect(name, parent, min, max);
        Text t = r.gameObject.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = value; t.fontSize = fontSize;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 12; t.resizeTextMaxSize = fontSize;
        t.alignment = align; t.color = color;
        t.raycastTarget = false;
        return t;
    }

    private static Button MakeButton(string name, Transform parent, string label,
        Vector2 min, Vector2 max, Color bg, Color fg)
    {
        RectTransform r = Rect(name, parent, min, max);
        Image img = r.gameObject.AddComponent<Image>(); img.color = bg;
        Button b = r.gameObject.AddComponent<Button>(); b.targetGraphic = img;
        TextAt("Text", r, label, new Vector2(.02f,.02f),new Vector2(.98f,.98f),22,TextAnchor.MiddleCenter,fg);
        return b;
    }

    private void Build()
    {
        RectTransform page = menu.mapsPage.transform as RectTransform;
        if (!page) { Status = "BLOCKED: missing RectTransform"; return; }
        // Small third row above 7I.2 Normal/Endless mode choices (y=.175-.238).
        Button show = MakeButton("BTN_7I4Records", page, "ENDLESS RECORDS / REWARDS",
            new Vector2(.66f,.245f), new Vector2(.94f,.304f), CardColor, Gold);
        show.onClick.AddListener(Open);
        RectTransform modal = Rect("7I4_Records_Modal", page, new Vector2(.18f,.14f), new Vector2(.82f,.87f));
        panel = modal.gameObject;
        Image background = panel.AddComponent<Image>(); background.color = PanelColor;
        TextAt("Heading", modal, "ENDLESS RECORDS", new Vector2(.06f,.86f), new Vector2(.78f,.96f), 32,
            TextAnchor.MiddleLeft, Gold);
        MakeButton("BTN_Close", modal, "X", new Vector2(.88f,.865f),new Vector2(.96f,.95f),CardColor,Gold)
            .onClick.AddListener(Close);
        details = TextAt("Records_Details", modal, "", new Vector2(.06f,.32f),new Vector2(.94f,.83f),24,
            TextAnchor.UpperLeft,Pale);
        claimButton = MakeButton("BTN_ClaimOne", modal, "CLAIM NEXT REWARD",
            new Vector2(.36f,.17f),new Vector2(.91f,.28f),new Color(.39f,.22f,.63f,1f),Gold);
        claimButton.onClick.AddListener(Claim);
        MakeButton("BTN_Refresh", modal, "REFRESH", new Vector2(.06f,.17f),new Vector2(.33f,.28f),CardColor,Pale)
            .onClick.AddListener(Refresh);
        statusText = TextAt("Status", modal, "",new Vector2(.06f,.035f),new Vector2(.94f,.15f),17,
            TextAnchor.MiddleLeft,Pale);
        panel.SetActive(false);
    }

    private void Open()
    {
        if (panel == null) return;
        panel.SetActive(true);
        Refresh();
    }

    private void Close() { if (panel != null) panel.SetActive(false); }

    private void Refresh()
    {
        viewedSlot = MS5A2GoldBank.SelectedSlot;
        MS7I4ProfileRecord record;
        int count, gold;
        string result;
        bool ok = MS7I4RecordService.TrySnapshot(viewedSlot, out record, out count, out gold, out result);
        if (!ok)
        {
            details.text = "PROFILE NOT READY\n\nOpen MANAGE SAVES and verify this slot.\n" + result;
            claimButton.interactable = false;
            Status = "BLOCKED: " + result;
        }
        else
        {
            details.text = "PROFILE " + viewedSlot.ToString("00") + " / PERSONAL BEST\n\n" +
                "Highest wave REACHED: " + record.bestReachedWave + "\n" +
                "Highest wave CLEARED: " + record.bestClearedWave + "\n" +
                "Completed Endless runs: " + record.gamesCompleted + "\n" +
                "Rotating Bosses defeated: " + record.totalEndlessBossesDefeated + "\n\n" +
                "UNCLAIMED REWARDS  /  " + count + " run(s)\n" +
                "Bank Gold available to CLAIM: +" + gold + "\n" +
                "Claim one run at a time. No Endless Victory or Daily/Weekly progress.";
            claimButton.interactable = count > 0;
            Status = "READ PROFILE " + viewedSlot + " PENDING=" + count + " Gold=" + gold;
        }
        statusText.text = Status;
    }

    private void Claim()
    {
        if (MS5A2GoldBank.SelectedSlot != viewedSlot)
        {
            Status = "PROFILE CHANGED. Refresh and review before Claim.";
            statusText.text = Status;
            claimButton.interactable = false;
            return;
        }
        string report;
        bool ok = MS7I4RecordService.TryClaimOne(viewedSlot, out report);
        if (ok) Refresh();
        Status = (ok ? "SUCCESS: " : "BLOCKED: ") + report;
        statusText.text = Status;
        Debug.Log("[7I.4] " + Status);
    }
}
