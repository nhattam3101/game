using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// MonsterSurvival 5C.2: non-destructive Map Unlock preview over Main Menu V3.
// IMPORTANT: This stage is intentionally SOFT GATING. It does not disable map selection
// or modify MMV3MenuController. Actual gameplay enforcement is a separate future step.
[DisallowMultipleComponent]
public sealed class MS5C2MapUnlockUI : MonoBehaviour
{
    [Header("Scene references - assigned by 5C.2 Installer")]
    public MMV3MenuController menu;
    public MS5A2ProfileWidget profileWidget;
    public Sprite lockIcon;

    private static readonly string[] MapIds = {
        "map.green_meadow", "map.sand_dunes", "map.ancient_ruins"
    };
    private static readonly int[] MapCosts = { 0, 250, 650 };
    private static readonly Color Gold = new Color(.96f, .74f, .42f, 1f);
    private static readonly Color Pale = new Color(.96f, .91f, .99f, 1f);
    private static readonly Color Blue = new Color(.51f, .86f, .79f, 1f);
    private static readonly Color Muted = new Color(.70f, .64f, .80f, 1f);

    private readonly GameObject[] accents = new GameObject[3];
    private readonly Image[] locks = new Image[3];
    private readonly TMP_Text[] states = new TMP_Text[3];
    private readonly Button[] actions = new Button[3];
    private readonly TMP_Text[] actionLabels = new TMP_Text[3];
    private GameObject confirmation;
    private TMP_Text confirmMessage;
    private TMP_Text overview;
    private Button confirmBuy;
    private int pendingIndex = -1;
    private int pendingSlot = -1;
    private int pendingRevision = -1;
    private int lastSlot = -1;
    private float nextRead;
    private bool wasOpen;
    private bool purchasing;
    private bool available;
    private bool writable;
    private MS5C1UnlockService.Snapshot snapshot;

    private void Start()
    {
        if (!menu) menu = GetComponent<MMV3MenuController>();
        if (!menu || menu.mapCards == null || menu.mapCards.Length < 3 || !menu.mapsPage)
        {
            Debug.LogError("[5C.2] Missing Menu V3 references; UI not created.", this);
            enabled = false;
            return;
        }
        Build();
        Refresh();
    }

    private void Update()
    {
        if (!menu || !menu.mapsPage) return;
        bool open = menu.mapsPage.activeInHierarchy;
        if (!open)
        {
            if (wasOpen) CloseConfirm();
            wasOpen = false;
            return;
        }
        int slot = MS5A2GoldBank.SelectedSlot;
        if (!wasOpen || slot != lastSlot || Time.unscaledTime >= nextRead)
        {
            if (slot != lastSlot) CloseConfirm();
            Refresh();
            nextRead = Time.unscaledTime + .6f;
        }
        wasOpen = true;
        if (confirmation && confirmation.activeSelf && Input.GetKeyDown(KeyCode.Escape)) CloseConfirm();
    }

    public void Refresh()
    {
        lastSlot = MS5A2GoldBank.SelectedSlot;
        string reason;
        MS5C1UnlockService.Snapshot current;
        available = MS5C1UnlockService.TryGetSnapshot(lastSlot, out current, out reason);
        snapshot = available ? current : null;
        writable = available && !snapshot.isNew && !snapshot.fromBackup &&
                   string.Equals(reason, "SAVED_PROFILE", StringComparison.Ordinal);
        if (overview)
        {
            overview.text = !available ? "PROFILE " + lastSlot + "  /  SAVE UNAVAILABLE - " + reason :
                "PROFILE " + lastSlot + "  /  BANK GOLD " + snapshot.bankGold.ToString("N0") +
                "  /  MAP ACCESS PREVIEW";
        }

        for (int i = 0; i < 3; i++)
        {
            bool owned = i == 0 || (available && snapshot.IsUnlocked(MapIds[i]));
            if (states[i])
            {
                states[i].text = owned ? "UNLOCKED" : "LOCKED";
                states[i].color = owned ? Blue : Gold;
            }
            if (locks[i]) locks[i].gameObject.SetActive(!owned && lockIcon != null);
            if (actions[i])
            {
                actions[i].gameObject.SetActive(i > 0 && !owned);
                actions[i].interactable = !owned && writable && snapshot.bankGold >= MapCosts[i];
            }
            if (actionLabels[i])
            {
                actionLabels[i].text = !available ? "SAVE ERROR" :
                    !writable ? "SAVE READ ONLY" :
                    snapshot.bankGold < MapCosts[i] ? "NEED " + MapCosts[i].ToString("N0") + " GOLD" :
                    "UNLOCK  ·  " + MapCosts[i].ToString("N0") + " GOLD";
            }
        }
        // Keep Menu V3's selection / START button unchanged until hard gating is separately tested.
        if (profileWidget) profileWidget.Refresh();
    }

    private void RequestUnlock(int index)
    {
        if (purchasing || index < 1 || index > 2) return;
        Refresh();
        if (!available || !writable || snapshot == null || snapshot.IsUnlocked(MapIds[index]) ||
            snapshot.bankGold < MapCosts[index]) return;
        pendingIndex = index;
        pendingSlot = snapshot.slot;
        pendingRevision = snapshot.revision;
        if (confirmMessage)
        {
            string label = index == 1 ? "SAND DUNES" : "ANCIENT RUINS";
            confirmMessage.text = "UNLOCK " + label + "?\n\nCOST: " + MapCosts[index].ToString("N0") +
                " BANK GOLD\nAFTER: " + (snapshot.bankGold - MapCosts[index]).ToString("N0") +
                " BANK GOLD\n\nPurchase is saved immediately to Profile " + snapshot.slot + ".";
        }
        if (confirmation)
        {
            confirmation.SetActive(true);
            confirmation.transform.SetAsLastSibling();
        }
    }

    private void ConfirmUnlock()
    {
        if (purchasing || pendingIndex < 1 || pendingIndex > 2) return;
        int index = pendingIndex;
        int slot = pendingSlot;
        int revision = pendingRevision;
        CloseConfirm();
        if (MS5A2GoldBank.SelectedSlot != slot)
        {
            Refresh();
            Notify("PROFILE CHANGED - purchase cancelled.");
            return;
        }
        purchasing = true;
        try
        {
            MS5C1UnlockService.UnlockReceipt receipt;
            string result;
            bool ok = MS5C1UnlockService.TryPurchase(slot, MapIds[index], revision,
                out receipt, out result);
            Refresh();
            if (!ok)
            {
                Notify("UNLOCK BLOCKED: " + result);
                return;
            }
            Notify("UNLOCKED: " + (index == 1 ? "SAND DUNES" : "ANCIENT RUINS") +
                "  /  -" + receipt.goldSpent + " GOLD  /  REV " + receipt.revision);
        }
        catch (Exception ex)
        {
            Debug.LogError("[5C.2] Purchase exception: " + ex, this);
            Refresh();
            Notify("UNLOCK ERROR - check Console.");
        }
        finally { purchasing = false; }
    }

    private void Notify(string text)
    {
        if (overview) overview.text = text;
        Debug.Log("[5C.2] " + text);
    }

    private void CloseConfirm()
    {
        pendingIndex = -1;
        pendingSlot = -1;
        pendingRevision = -1;
        if (confirmation) confirmation.SetActive(false);
    }

    private void Build()
    {
        for (int i = 0; i < 3; i++)
        {
            Button map = menu.mapCards[i];
            if (!map) continue;
            RectTransform card = map.transform as RectTransform;
            RectTransform banner = Rect("MS5C2_RarityBanner", card,
                new Vector2(.035f, .895f), new Vector2(.965f, .977f));
            Image bg = banner.gameObject.AddComponent<Image>();
            bg.color = new Color(.035f, .025f, .074f, .92f);
            bg.raycastTarget = false;
            accents[i] = banner.gameObject;
            RectTransform mark = Rect("Lock", banner,
                new Vector2(.035f, .13f), new Vector2(.135f, .88f));
            Image lockImage = mark.gameObject.AddComponent<Image>();
            lockImage.sprite = lockIcon;
            lockImage.preserveAspect = true;
            lockImage.raycastTarget = false;
            locks[i] = lockImage;
            states[i] = Text("UNLOCKED", banner, new Vector2(.15f, .06f),
                new Vector2(.95f, .94f), 23, Gold, TextAlignmentOptions.Center);
            if (i == 0) continue;
            RectTransform actionRect = Rect("MS5C2_BTN_Unlock", card,
                new Vector2(.15f, .788f), new Vector2(.85f, .89f));
            Image buttonImage = actionRect.gameObject.AddComponent<Image>();
            buttonImage.color = new Color(.29f, .18f, .41f, .96f);
            buttonImage.raycastTarget = true;
            Button button = actionRect.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.transition = Selectable.Transition.None;
            MS5C2ButtonHover hover = actionRect.gameObject.AddComponent<MS5C2ButtonHover>();
            hover.background = buttonImage;
            hover.idle = buttonImage.color;
            hover.highlighted = new Color(.51f, .32f, .67f, 1f);
            int captured = i;
            button.onClick.AddListener(() => RequestUnlock(captured));
            actions[i] = button;
            actionLabels[i] = Text("UNLOCK", actionRect, Vector2.zero, Vector2.one,
                22, Pale, TextAlignmentOptions.Center);
        }
        RectTransform page = menu.mapsPage.transform as RectTransform;
        RectTransform bar = Rect("MS5C2_StatusBar", page,
            new Vector2(.18f, .171f), new Vector2(.82f, .215f));
        overview = Text("MAP ACCESS PREVIEW", bar, Vector2.zero, Vector2.one,
            20, Gold, TextAlignmentOptions.Center);
        RectTransform warningBar = Rect("MS5C2_PreviewInfo", page,
            new Vector2(.11f, .015f), new Vector2(.89f, .063f));
        Text("5C.2 PREVIEW: Map access remains playable until hard-gating validation.",
            warningBar, Vector2.zero, Vector2.one, 17, Muted, TextAlignmentOptions.Center);
        BuildConfirmation(page);
    }

    private void BuildConfirmation(RectTransform page)
    {
        RectTransform shade = Rect("MS5C2_UnlockConfirm", page, Vector2.zero, Vector2.one);
        confirmation = shade.gameObject;
        Image shadeImage = shade.gameObject.AddComponent<Image>();
        shadeImage.color = new Color(.008f, .008f, .025f, .88f);
        shadeImage.raycastTarget = true; // Blocks underlying map clicks during confirmation.
        RectTransform panel = Rect("ConfirmPanel", shade, new Vector2(.325f, .31f), new Vector2(.675f, .69f));
        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(.10f, .075f, .17f, 1f);
        panelImage.raycastTarget = true;
        Surface("GoldAccent", panel, new Vector2(.015f, .964f), new Vector2(.985f, .98f), Gold);
        Text("CONFIRM UNLOCK", panel, new Vector2(.075f, .80f), new Vector2(.925f, .94f),
            32, Pale, TextAlignmentOptions.Center);
        confirmMessage = Text("", panel, new Vector2(.065f, .28f), new Vector2(.935f, .81f),
            20, Pale, TextAlignmentOptions.Center);
        confirmMessage.textWrappingMode = TextWrappingModes.Normal;
        confirmBuy = MakeButton("MS5C2_BTN_Confirm", panel, "UNLOCK NOW",
            new Vector2(.08f, .065f), new Vector2(.48f, .245f), Gold, new Color(.19f, .12f, .31f));
        confirmBuy.onClick.AddListener(ConfirmUnlock);
        Button cancel = MakeButton("MS5C2_BTN_Cancel", panel, "CANCEL",
            new Vector2(.52f, .065f), new Vector2(.92f, .245f), new Color(.31f, .25f, .43f), new Color(.46f, .33f, .56f));
        cancel.onClick.AddListener(CloseConfirm);
        confirmation.SetActive(false);
    }

    private static Button MakeButton(string name, Transform parent, string label,
                                     Vector2 min, Vector2 max, Color baseColor, Color hoverColor)
    {
        RectTransform rt = Rect(name, parent, min, max);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = baseColor;
        img.raycastTarget = true;
        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        button.transition = Selectable.Transition.None;
        MS5C2ButtonHover motion = rt.gameObject.AddComponent<MS5C2ButtonHover>();
        motion.background = img;
        motion.idle = baseColor;
        motion.highlighted = hoverColor;
        Text(label, rt, Vector2.zero, Vector2.one, 20, Pale, TextAlignmentOptions.Center);
        return button;
    }

    private static void Surface(string name, Transform parent, Vector2 min, Vector2 max, Color color)
    {
        RectTransform rt = Rect(name, parent, min, max);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    private static TMP_Text Text(string value, Transform parent, Vector2 min, Vector2 max,
                                 int size, Color color, TextAlignmentOptions alignment)
    {
        RectTransform rt = Rect("TXT_" + value.Replace(' ', '_').Substring(0, Mathf.Min(18, value.Length)), parent, min, max);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = value;
        t.fontSize = size;
        t.color = color;
        t.alignment = alignment;
        t.raycastTarget = false;
        t.enableAutoSizing = true;
        t.fontSizeMin = Mathf.Max(11, size / 2);
        t.fontSizeMax = size;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        if (TMP_Settings.defaultFontAsset) t.font = TMP_Settings.defaultFontAsset;
        return t;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }
}
