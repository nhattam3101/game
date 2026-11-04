using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 5B.2 - Pure UI adapter for the verified 5B.1 save transaction service.
// Never calls PlayerStats, GameManager, Inventory, or Equipment.
// Shop is created at runtime, under MMV3_Canvas, to preserve all V3 navigation.
[DisallowMultipleComponent]
public sealed class MS5B2ShopUI : MonoBehaviour
{
    [Header("Installed by 5B.2 Editor tool")]
    public Button openButton;
    public MS5A2ProfileWidget profileWidget;

    private const int CardCount = 4;
    private readonly string[] upgradeIds = { "vitality", "strength", "fury", "agility" };
    private readonly string[] glyphs = { "HP", "DMG", "SPD", "MOV" };
    private static readonly Color[] Accents = {
        new Color(.99f,.48f,.54f), new Color(.92f,.67f,.37f),
        new Color(.68f,.43f,.97f), new Color(.46f,.78f,.96f)
    };
    private static readonly Color White = new Color(.96f,.92f,1f,1f);
    private static readonly Color Muted = new Color(.68f,.64f,.77f,1f);
    private static readonly Color Gold = new Color(.96f,.76f,.45f,1f);

    private GameObject window;
    private CanvasGroup windowGroup;
    private TMP_Text profileTitle;
    private TMP_Text bankTitle;
    private TMP_Text feedback;
    private readonly TMP_Text[] names = new TMP_Text[CardCount];
    private readonly TMP_Text[] bonuses = new TMP_Text[CardCount];
    private readonly TMP_Text[] costs = new TMP_Text[CardCount];
    private readonly TMP_Text[] levels = new TMP_Text[CardCount];
    private readonly Image[] progress = new Image[CardCount];
    private readonly Button[] buyButtons = new Button[CardCount];
    private readonly TMP_Text[] buttonLabels = new TMP_Text[CardCount];
    private MS5B1PermanentUpgrades.Snapshot snapshot;
    private int displayedSlot;
    private float fade;
    private bool ready;
    private bool purchasing;

    private void Awake()
    {
        BuildUI();
        if (openButton) openButton.onClick.AddListener(Open);
        else Debug.LogWarning("[5B.2] Missing Upgrade Shop launcher.", this);
    }

    private void OnDestroy()
    {
        if (openButton) openButton.onClick.RemoveListener(Open);
    }

    private void Update()
    {
        if (window == null || !window.activeSelf) return;
        if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
        fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime * 5f);
        if (windowGroup) windowGroup.alpha = fade;
    }

    public void Open()
    {
        if (window == null) BuildUI();
        if (window == null) return;
        window.SetActive(true);
        window.transform.SetAsLastSibling();
        fade = 0f;
        if (windowGroup) windowGroup.alpha = 0f;
        Refresh("Choose an upgrade. Purchases are saved immediately to the selected profile.");
    }

    public void Close()
    {
        if (window) window.SetActive(false);
        if (profileWidget) profileWidget.Refresh();
    }

    // Read-only refresh; no new profile save is created just by opening this page.
    public void Refresh(string status = null)
    {
        displayedSlot = MS5A2GoldBank.SelectedSlot;
        string message;
        MS5B1PermanentUpgrades.Snapshot fresh;
        ready = MS5B1PermanentUpgrades.TryGetSnapshot(displayedSlot, out fresh, out message);
        snapshot = ready ? fresh : null;
        bool safeToBuy = ready && string.Equals(message, "SAVED PROFILE", StringComparison.Ordinal);

        if (profileTitle) profileTitle.text = "PROFILE " + displayedSlot.ToString("00") + " / 03";
        if (bankTitle) bankTitle.text = ready ? (snapshot.bankGold.ToString("N0") + " GOLD") : "GOLD --";

        for (int i = 0; i < CardCount; ++i)
        {
            MS5B1PermanentUpgrades.Row row = FindRow(upgradeIds[i]);
            if (names[i]) names[i].text = row != null ? row.title : upgradeIds[i].ToUpperInvariant();
            if (bonuses[i]) bonuses[i].text = row != null ? row.description : "Profile unavailable";
            if (levels[i]) levels[i].text = row != null ? "LV " + row.level + " / " + row.maxLevel : "LV --";
            if (progress[i]) progress[i].fillAmount = row != null && row.maxLevel > 0 ?
                Mathf.Clamp01((float)row.level / row.maxLevel) : 0f;
            bool maxed = row != null && row.level >= row.maxLevel;
            int price = row != null ? row.nextCost : -1;
            if (costs[i]) costs[i].text = row == null ? "UNAVAILABLE" :
                maxed ? "MAX LEVEL REACHED" : "NEXT LEVEL  ·  " + price.ToString("N0") + " GOLD";
            if (buyButtons[i]) buyButtons[i].interactable = safeToBuy && row != null &&
                !maxed && price > 0 && snapshot.bankGold >= price;
            if (buttonLabels[i]) buttonLabels[i].text = row == null || !safeToBuy ? "LOCKED" :
                maxed ? "MAX" : snapshot.bankGold < price ? "NEED GOLD" : "UPGRADE";
        }

        if (feedback) feedback.text = !ready ? "SAVE READ BLOCKED: " + message :
            !string.IsNullOrEmpty(status) ? status :
            snapshot.isNewProfile ? "NEW PROFILE - Win a run to earn Bank Gold." :
            "Bank Gold and upgrades are stored in this profile only.";
        if (profileWidget) profileWidget.Refresh();
    }

    private MS5B1PermanentUpgrades.Row FindRow(string id)
    {
        if (snapshot == null) return null;
        foreach (MS5B1PermanentUpgrades.Row row in snapshot.rows)
            if (string.Equals(row.id, id, StringComparison.Ordinal)) return row;
        return null;
    }

    private void Purchase(int index)
    {
        if (purchasing || index < 0 || index >= CardCount) return;
        if (MS5A2GoldBank.SelectedSlot != displayedSlot)
        {
            Refresh("Profile changed. Please review the prices before buying.");
            return;
        }
        if (!ready || snapshot == null) { Refresh("Profile save cannot be read safely."); return; }
        if (snapshot.isNewProfile) { Refresh("New profile: earn Gold before buying."); return; }
        MS5B1PermanentUpgrades.Row row = FindRow(upgradeIds[index]);
        if (row == null || row.nextCost < 0 || row.level >= row.maxLevel ||
            snapshot.bankGold < row.nextCost) { Refresh("Not enough Bank Gold or max level reached."); return; }

        purchasing = true;
        try
        {
            MS5B1PermanentUpgrades.PurchaseReceipt receipt;
            string result;
            // expectedRevision is from the last verified read. Service rejects stale/double clicks.
            bool ok = MS5B1PermanentUpgrades.TryPurchase(displayedSlot, upgradeIds[index],
                snapshot.revision, out receipt, out result);
            if (!ok)
            {
                Refresh("PURCHASE BLOCKED: " + result);
                return;
            }
            Refresh("SUCCESS: " + row.title + " LV " + receipt.newLevel +
                    "  (-" + receipt.goldSpent + " GOLD). Saved revision " + receipt.savedRevision + ".");
        }
        catch (Exception ex)
        {
            Debug.LogError("[5B.2] Purchase failed: " + ex, this);
            Refresh("Purchase error - see Console. No further action was requested.");
        }
        finally { purchasing = false; }
    }

    private void BuildUI()
    {
        if (window != null) return;
        RectTransform canvasRect = transform as RectTransform;
        if (!canvasRect) { Debug.LogError("[5B.2] Attach to UI RectTransform.", this); return; }

        RectTransform shade = Rect("MS5B2_Window", canvasRect, Vector2.zero, Vector2.one);
        window = shade.gameObject;
        Image shadeImage = shade.gameObject.AddComponent<Image>();
        shadeImage.color = new Color(.01f,.009f,.025f,.94f); shadeImage.raycastTarget = true;
        windowGroup = shade.gameObject.AddComponent<CanvasGroup>();
        windowGroup.blocksRaycasts = true;
        windowGroup.interactable = true;

        RectTransform panel = Rect("ShopPanel", shade, new Vector2(.135f,.09f), new Vector2(.865f,.91f));
        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(.055f,.041f,.095f,1f);
        panelImage.raycastTarget = true;
        Surface("PanelInner", panel, new Vector2(.012f,.018f), new Vector2(.988f,.982f), new Color(.08f,.062f,.13f,.95f));
        Surface("TopGoldLine",panel,new Vector2(.02f,.963f),new Vector2(.98f,.97f),Gold);
        Label("PERMANENT UPGRADES",panel,new Vector2(.048f,.868f),new Vector2(.66f,.958f),44,White,TextAlignmentOptions.Left);
        Label("LEGACY POWER  /  SAVE SLOT PROGRESSION",panel,
              new Vector2(.05f,.826f),new Vector2(.72f,.877f),17,Muted,TextAlignmentOptions.Left);
        profileTitle=Label("PROFILE 01 / 03",panel,new Vector2(.68f,.905f),new Vector2(.89f,.958f),18,Gold,TextAlignmentOptions.Right);
        bankTitle=Label("0 GOLD",panel,new Vector2(.68f,.845f),new Vector2(.89f,.915f),29,Gold,TextAlignmentOptions.Right);
        Button close = MakeButton("BTN_Close",panel,"X",new Vector2(.913f,.89f),new Vector2(.967f,.953f),new Color(.24f,.16f,.34f),19);
        close.onClick.AddListener(Close);

        for (int i=0;i<CardCount;i++)
        {
            int captured = i;
            float x0 = i % 2 == 0 ? .046f : .51f;
            float x1 = i % 2 == 0 ? .49f : .954f;
            float y0 = i < 2 ? .526f : .232f;
            float y1 = i < 2 ? .80f : .506f;
            RectTransform card=Rect("Upgrade_"+upgradeIds[i],panel,new Vector2(x0,y0),new Vector2(x1,y1));
            Image image=card.gameObject.AddComponent<Image>();image.color=new Color(.12f,.09f,.19f,1f);image.raycastTarget=false;
            Surface("RarityBar",card,new Vector2(0,0),new Vector2(.011f,1),Accents[i]);
            Surface("IconBg",card,new Vector2(.045f,.67f),new Vector2(.18f,.93f),new Color(.2f,.15f,.31f));
            Label(glyphs[i],card,new Vector2(.05f,.68f),new Vector2(.175f,.93f),27,Accents[i],TextAlignmentOptions.Center);
            names[i]=Label(upgradeIds[i].ToUpperInvariant(),card,new Vector2(.205f,.74f),new Vector2(.95f,.95f),31,White,TextAlignmentOptions.Left);
            bonuses[i]=Label("",card,new Vector2(.205f,.59f),new Vector2(.96f,.77f),20,Muted,TextAlignmentOptions.Left);
            levels[i]=Label("LV 0 / 10",card,new Vector2(.055f,.40f),new Vector2(.43f,.59f),20,White,TextAlignmentOptions.Left);
            costs[i]=Label("",card,new Vector2(.43f,.40f),new Vector2(.957f,.59f),19,Gold,TextAlignmentOptions.Right);
            RectTransform track=Rect("ProgressTrack",card,new Vector2(.055f,.31f),new Vector2(.945f,.355f));
            Image bg=track.gameObject.AddComponent<Image>();bg.color=new Color(.24f,.2f,.32f);bg.raycastTarget=false;
            RectTransform fillRect=Rect("ProgressFill",track,Vector2.zero,Vector2.one);
            Image fill=fillRect.gameObject.AddComponent<Image>();fill.color=Accents[i];fill.raycastTarget=false;
            fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Horizontal;fill.fillAmount=0;
            progress[i]=fill;
            Button buy=MakeButton("BTN_Upgrade",card,"UPGRADE",new Vector2(.26f,.035f),new Vector2(.74f,.265f),
                new Color(.34f,.23f,.49f),20);
            buy.onClick.AddListener(()=>Purchase(captured));
            buyButtons[i]=buy;
            buttonLabels[i]=buy.GetComponentInChildren<TextMeshProUGUI>(true);
        }
        feedback=Label("",panel,new Vector2(.055f,.106f),new Vector2(.945f,.20f),22,Gold,TextAlignmentOptions.Center);
        Label("Saved immediately after purchase  ·  Bonuses apply in gameplay with 5B.3",
              panel,new Vector2(.055f,.034f),new Vector2(.945f,.105f),18,Muted,TextAlignmentOptions.Center);
        window.SetActive(false);
    }

    private static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
    {
        GameObject go = new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);
        RectTransform rt=go.GetComponent<RectTransform>();rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=rt.offsetMax=Vector2.zero;
        return rt;
    }
    private static void Surface(string name,Transform parent,Vector2 min,Vector2 max,Color color)
    {
        RectTransform rt=Rect(name,parent,min,max);
        Image i=rt.gameObject.AddComponent<Image>();i.color=color;i.raycastTarget=false;
    }
    private static TMP_Text Label(string value,Transform parent,Vector2 min,Vector2 max,int size,Color color,TextAlignmentOptions align)
    {
        RectTransform rt=Rect("TXT",parent,min,max);
        TextMeshProUGUI label=rt.gameObject.AddComponent<TextMeshProUGUI>();
        label.text=value;label.fontSize=size;label.color=color;label.alignment=align;
        label.raycastTarget=false;label.enableAutoSizing=true;label.fontSizeMin=Mathf.Max(11,size/2);label.fontSizeMax=size;
        label.textWrappingMode=TextWrappingModes.NoWrap;
        if(TMP_Settings.defaultFontAsset)label.font=TMP_Settings.defaultFontAsset;
        return label;
    }
    private static Button MakeButton(string name,Transform parent,string value,Vector2 min,Vector2 max,Color color,int size)
    {
        RectTransform rect=Rect(name,parent,min,max);
        Image image=rect.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=true;
        Button button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
        button.transition=Selectable.Transition.None;
        MS5B2ButtonMotion motion=rect.gameObject.AddComponent<MS5B2ButtonMotion>();motion.baseColor=color;motion.targetGraphic=image;
        Label(value,rect,Vector2.zero,Vector2.one,size,White,TextAlignmentOptions.Center);
        return button;
    }
}
