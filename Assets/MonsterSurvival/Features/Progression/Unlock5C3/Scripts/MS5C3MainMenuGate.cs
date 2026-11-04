using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Blocks the existing START button rather than adding a second start handler.
// Does not change MMV3MenuController or 5C.2 purchase code.
[DisallowMultipleComponent]
[DefaultExecutionOrder(1200)]
public sealed class MS5C3MainMenuGate : MonoBehaviour
{
    public MMV3MenuController menu;
    public MS5C2MapUnlockUI mapUnlockPreview;
    private Button start;
    private TMP_Text notice;
    private Button legacyButton;
    private GameObject legacyConfirm;
    private float nextRefresh;
    private bool wasOpen;
    private int lastSlot = -1;
    private int lastSelection = -1;
    private bool enrolledThisOpen;
    private static readonly Color Gold = new Color(.94f, .75f, .42f, 1f);
    private static readonly Color Pale = new Color(.95f, .89f, .99f, 1f);

    private void Start()
    {
        if (!menu) menu = GetComponent<MMV3MenuController>();
        if (!menu || !menu.mapsPage || !menu.mapsStartButton || menu.mapCards == null || menu.mapCards.Length < 3)
        { Debug.LogError("[5C.3] Missing MMV3 menu references; enforcement unavailable.", this); enabled = false; return; }
        start = menu.mapsStartButton;
        Build();
        for (int i = 0; i < 3; i++)
            if (menu.mapCards[i]) menu.mapCards[i].onClick.AddListener(RefreshNow);
        RefreshNow();
    }

    private void OnDisable()
    {
        if (start) start.interactable = true;
    }

    private void Update()
    {
        if (!menu || !menu.mapsPage || !start) return;
        bool open = menu.mapsPage.activeInHierarchy;
        if (!open)
        {
            if (wasOpen && legacyConfirm) legacyConfirm.SetActive(false);
            wasOpen = false;
            enrolledThisOpen = false;
            start.interactable = true;
            return;
        }
        int slot = MS5A2GoldBank.SelectedSlot;
        int selected = SelectedIndex();
        if (!wasOpen || slot != lastSlot || selected != lastSelection || Time.unscaledTime >= nextRefresh)
            RefreshNow();
        wasOpen = true;
    }

    private int SelectedIndex()
    {
        if (menu.mapStates != null)
            for (int i = 0; i < Mathf.Min(3, menu.mapStates.Length); i++)
                if (menu.mapStates[i] && menu.mapStates[i].text == "SELECTED") return i;
        // Not-yet-selected UI defaults to the controller's initial Meadow.
        return 0;
    }

    public void RefreshNow()
    {
        if (!menu || !menu.mapsPage || !start) return;
        int slot = MS5A2GoldBank.SelectedSlot;
        int map = SelectedIndex();
        lastSlot = slot;
        lastSelection = map;
        nextRefresh = Time.unscaledTime + .3f;
        if (!menu.mapsPage.activeInHierarchy) return;

        MS5C3MapPolicy.State state;
        if (!MS5C3MapPolicy.TryGet(slot, map, out state))
        {
            start.interactable = false; // Fail closed if save is corrupt.
            if (notice) notice.text = "SAVE NOT VERIFIED · START DISABLED · " + state.status;
            if (legacyButton) legacyButton.gameObject.SetActive(false);
            return;
        }
        if (state.isNew && !enrolledThisOpen)
        {
            enrolledThisOpen = true;
            string result;
            if (!MS5C3MapPolicy.TryInitializeNewProfile(slot, out result) ||
                !MS5C3MapPolicy.TryGet(slot, map, out state) || state.isNew)
            {
                // Never start a NEW profile without a durable access-policy marker.
                start.interactable = false;
                if (notice) notice.text = "CANNOT INITIALIZE NEW PROFILE · CHECK CONSOLE / BACKUP";
                Debug.LogError("[5C.3] Profile enrollment failed: " + result, this);
                return;
            }
        }
        start.interactable = state.valid && state.allowed;
        if (notice)
        {
            notice.text = state.legacy ? "LEGACY PROFILE · ALL MAPS PLAYABLE · OPTIONAL LOCK ACTIVATION" :
                state.allowed ? "MAP ACCESS VERIFIED  /  PROFILE " + slot.ToString("00") :
                "LOCKED MAP · BUY UNLOCK ABOVE TO START  /  PROFILE " + slot.ToString("00");
        }
        if (legacyButton)
        {
            legacyButton.gameObject.SetActive(state.legacy && !state.recovered);
            legacyButton.interactable = state.legacy && !state.recovered;
        }
        // The 5C.2 preview still displays purchase ownership. Legacy access is
        // a play permission, not a fake purchase; no unlock flag is invented.
    }

    private void Build()
    {
        RectTransform page = menu.mapsPage.transform as RectTransform;
        RectTransform line = Rect("MS5C3_AccessNotice", page, new Vector2(.15f,.783f), new Vector2(.78f,.813f));
        notice = Text(line, "MAP ACCESS", 17);
        RectTransform buttonRect = Rect("MS5C3_LegacyOptIn", page, new Vector2(.79f,.785f), new Vector2(.945f,.83f));
        legacyButton = MakeButton(buttonRect, "ACTIVATE MAP LOCKS");
        legacyButton.onClick.AddListener(() => { if (legacyConfirm) legacyConfirm.SetActive(true); });
        RectTransform shade = Rect("MS5C3_OptInConfirm", page, Vector2.zero, Vector2.one);
        legacyConfirm = shade.gameObject;
        Image shadeImg = shade.gameObject.AddComponent<Image>(); shadeImg.color = new Color(.015f,.01f,.04f,.93f);
        shadeImg.raycastTarget = true;
        RectTransform panel = Rect("Panel", shade, new Vector2(.29f,.29f),new Vector2(.71f,.71f));
        Image panelImg = panel.gameObject.AddComponent<Image>(); panelImg.color = new Color(.12f,.07f,.23f,1f);
        panelImg.raycastTarget = true;
        RectTransform label = Rect("Message", panel,new Vector2(.08f,.35f),new Vector2(.92f,.90f));
        Text(label,"ACTIVATE MAP LOCKS?\n\nPreviously playable maps without a purchased unlock will become locked for this profile. This action cannot be automatically reversed. Your Gold and purchased unlocks stay unchanged.",22).textWrappingMode = TextWrappingModes.Normal;
        Button accept = MakeButton(Rect("Enable",panel,new Vector2(.08f,.08f),new Vector2(.48f,.27f)), "ACTIVATE");
        accept.onClick.AddListener(() => { legacyConfirm.SetActive(false); string msg; if (!MS5C3MapPolicy.TryActivateLegacy(MS5A2GoldBank.SelectedSlot,out msg)) Debug.LogWarning("[5C.3] " + msg); RefreshNow(); });
        Button cancel = MakeButton(Rect("Cancel",panel,new Vector2(.52f,.08f),new Vector2(.92f,.27f)), "CANCEL");
        cancel.onClick.AddListener(() => legacyConfirm.SetActive(false));
        legacyConfirm.SetActive(false);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(name,typeof(RectTransform));go.layer=5;
        go.transform.SetParent(parent,false); RectTransform rt=(RectTransform)go.transform;
        rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=rt.offsetMax=Vector2.zero;return rt;
    }
    private static TMP_Text Text(Transform parent,string text,int fontSize)
    {
        var t=parent.gameObject.AddComponent<TextMeshProUGUI>(); t.text=text;
        t.fontSize=fontSize;t.color=Pale;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;
        t.enableAutoSizing=true;t.fontSizeMin=12;t.fontSizeMax=fontSize;
        if(TMP_Settings.defaultFontAsset)t.font=TMP_Settings.defaultFontAsset;return t;
    }
    private static Button MakeButton(RectTransform rt,string title)
    {
        Image bg=rt.gameObject.AddComponent<Image>();bg.color=new Color(.32f,.18f,.48f,1f);bg.raycastTarget=true;
        Button b=rt.gameObject.AddComponent<Button>(); b.targetGraphic=bg;
        var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=Gold;colors.pressedColor=new Color(.7f,.56f,.8f);b.colors=colors;
        RectTransform label=Rect("Label",rt,Vector2.zero,Vector2.one);Text(label,title,17);return b;
    }
}
