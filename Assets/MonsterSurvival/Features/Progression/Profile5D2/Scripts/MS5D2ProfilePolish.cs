using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 5D.2: Alternative read-only presentation layered over 5D.1.
// 5D.1 and 5A-5C sources are left unchanged. No purchase/save APIs are called.
[DisallowMultipleComponent]
[DefaultExecutionOrder(500)]
public sealed class MS5D2ProfilePolish : MonoBehaviour
{
    [Header("Read-only input sources")]
    public MS5D1ProfileOverview originalOverview;
    public MS5A3ItemRegistry itemRegistry;
    public MS3DEquipmentCatalog equipmentCatalog;

    [Header("Fallback category icons (assigned by 5D.2 Installer)")]
    public Sprite genericItemIcon;
    public Sprite weaponIcon;
    public Sprite armorIcon;
    public Sprite accessoryIcon;
    public Sprite mapIcon;
    public Sprite medalIcon;

    [Header("Presentation")]
    [Range(0.12f, 0.5f)] public float fadeDuration = 0.22f;
    [Range(0.005f, 0.08f)] public float openScaleOffset = 0.025f;
    [Range(640, 1600)] public int twoColumnMinScreenWidth = 1280;

    private Button launcher;
    private GameObject layer;
    private RectTransform panel, viewport, content;
    private CanvasGroup layerGroup, cardsGroup;
    private GridLayoutGroup grid;
    private ScrollRect scroll;
    private TMP_Text header, goldText, saveText, statusText;
    private Button overviewButton, collectionButton;
    private Image overviewBg, collectionBg;
    private bool open, closing, ready;
    private int tab, nextTab = -1, previousProfile = -1, previousRevision = int.MinValue;
    private float nextPoll, tween, pageTween;
    private int cachedWidth, cachedHeight;

    private static readonly Color Backdrop = new Color(.008f, .009f, .027f, .96f);
    private static readonly Color Window = new Color(.058f, .043f, .100f, 1f);
    private static readonly Color Tile = new Color(.115f, .084f, .174f, .98f);
    private static readonly Color TileInner = new Color(.19f, .133f, .264f, .98f);
    private static readonly Color Purple = new Color(.66f, .44f, .89f, 1f);
    private static readonly Color Pale = new Color(.95f, .92f, 1f, 1f);
    private static readonly Color Muted = new Color(.70f, .64f, .80f, 1f);
    private static readonly Color Gold = new Color(.96f, .76f, .43f, 1f);
    private static readonly Color Green = new Color(.45f, .88f, .72f, 1f);
    private static readonly Color Red = new Color(.98f, .47f, .50f, 1f);

    private void Start()
    {
        if (!originalOverview) originalOverview = GetComponent<MS5D1ProfileOverview>();
        if (!originalOverview || !originalOverview.launcher || !GetComponentInParent<Canvas>())
        {
            Debug.LogWarning("[5D.2] Requires installed 5D.1 overview and MainMenu Canvas.", this);
            enabled = false;
            return;
        }
        launcher = originalOverview.launcher;
        BuildUi();
        launcher.onClick.AddListener(Open);
        ready = true;
    }

    private void Update()
    {
        if (!ready || !layer || !layer.activeSelf) return;
        if (!launcher || !launcher.gameObject.activeInHierarchy) { CloseImmediate(); return; }
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
#endif
        float dt = Time.unscaledDeltaTime;
        tween = Mathf.MoveTowards(tween, closing ? 0f : 1f, dt / Mathf.Max(.03f, fadeDuration));
        layerGroup.alpha = Mathf.SmoothStep(0f, 1f, tween);
        float size = 1f - openScaleOffset * (1f - tween);
        panel.localScale = new Vector3(size, size, 1f);
        if (closing && tween <= 0f) { CloseImmediate(); return; }

        int w = Screen.width, h = Screen.height;
        if (w != cachedWidth || h != cachedHeight)
        {
            cachedWidth = w; cachedHeight = h;
            UpdateResponsive();
        }
        if (Time.unscaledTime >= nextPoll)
        {
            nextPoll = Time.unscaledTime + 1.0f;
            CheckSaveRevision();
        }
        if (nextTab >= 0)
        {
            pageTween = Mathf.MoveTowards(pageTween, 0f, dt / .12f);
            if (pageTween <= 0f)
            {
                tab = nextTab;
                nextTab = -1;
                RebuildCurrentPage();
                UpdateTabs();
            }
        }
        else pageTween = Mathf.MoveTowards(pageTween, 1f, dt / .20f);
        if (cardsGroup) cardsGroup.alpha = Mathf.SmoothStep(0f, 1f, pageTween);
    }

    public void Open()
    {
        if (!ready || !layer) return;
        // 5D.1's own listener fires first; replace its visual with this polished view.
        originalOverview.Close();
        layer.transform.SetAsLastSibling();
        layer.SetActive(true);
        open = true; closing = false; nextTab = -1; tween = 0f; pageTween = 1f;
        layerGroup.alpha = 0f;
        layerGroup.blocksRaycasts = true;
        layerGroup.interactable = true;
        cachedWidth = -1;
        CheckSaveRevision(true);
        UpdateResponsive();
    }

    public void Close()
    {
        if (!open || closing) return;
        closing = true;
        layerGroup.interactable = false;
        originalOverview.Close();
    }

    private void CloseImmediate()
    {
        open = false; closing = false; nextTab = -1;
        if (layer) layer.SetActive(false);
        if (originalOverview) originalOverview.Close();
    }

    private void SwitchPage(int index)
    {
        if (!open || closing || tab == index || nextTab >= 0) return;
        nextTab = index;
    }

    private void CheckSaveRevision(bool force = false)
    {
        int slot = MS5A2GoldBank.SelectedSlot;
        MS5A1SaveData data;
        bool fromBackup;
        string reason;
        bool ok = MS5A1SaveService.TryLoad(slot, out data, out reason, out fromBackup);
        int revision = ok && data != null ? data.revision : int.MinValue;
        if (!force && slot == previousProfile && revision == previousRevision) return;
        previousProfile = slot; previousRevision = revision;
        RebuildCurrentPage();
    }

    private void UpdateResponsive()
    {
        if (!panel || !viewport || !grid) return;
        bool compact = Screen.width < twoColumnMinScreenWidth || Screen.width < Screen.height * 1.35f;
        panel.anchorMin = compact ? new Vector2(.017f, .018f) : new Vector2(.07f, .055f);
        panel.anchorMax = compact ? new Vector2(.983f, .982f) : new Vector2(.93f, .945f);
        panel.offsetMin = Vector2.zero; panel.offsetMax = Vector2.zero;
        Canvas.ForceUpdateCanvases();
        float width = Mathf.Max(260f, viewport.rect.width);
        int columns = compact ? 1 : 2;
        float spacing = 10f;
        float horizontalPadding = 18f;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.cellSize = new Vector2(Mathf.Max(200f, (width - horizontalPadding * 2f - spacing * (columns - 1)) / columns), compact ? 108f : 122f);
        LayoutContent();
    }

    private void LayoutContent()
    {
        if (!content || !grid) return;
        int count = content.childCount;
        int columns = Mathf.Max(1, grid.constraintCount);
        int rows = (count + columns - 1) / columns;
        float height = 20f + rows * grid.cellSize.y + Mathf.Max(0, rows - 1) * grid.spacing.y;
        content.sizeDelta = new Vector2(0f, height);
        if (scroll) scroll.verticalNormalizedPosition = 1f;
    }

    private void RebuildCurrentPage()
    {
        if (!content) return;
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject child = content.GetChild(i).gameObject;
            child.SetActive(false);
            child.transform.SetParent(null, false);
            Destroy(child);
        }
        MS5A1SaveData data;
        bool fromBackup;
        string reason;
        int profile = MS5A2GoldBank.SelectedSlot;
        bool saved = MS5A1SaveService.TryLoad(profile, out data, out reason, out fromBackup);
        bool fresh = !saved && !string.IsNullOrEmpty(reason) && reason.StartsWith("NO_SAVE", StringComparison.Ordinal);
        header.text = "PLAYER PROFILE  " + profile.ToString("00") + " / 03";
        if (!saved && !fresh)
        {
            goldText.text = "BANK GOLD  —";
            saveText.text = "SAVE UNAVAILABLE";
            statusText.text = "READ ONLY  •  Save unavailable; no data guessed or changed.";
            statusText.color = Red;
            MakeCard("SAVE ERROR", reason ?? "Cannot verify profile data", null, Red, "!", null);
            LayoutContent();
            return;
        }
        if (fresh) data = MS5A1SaveService.CreateFresh(profile);
        if (data == null) return;
        goldText.text = "BANK GOLD  " + data.bankGold.ToString("N0");
        saveText.text = fresh ? "NEW PROFILE  •  NO SAVE FILE" : "REV " + data.revision + "  •  " + DisplayTimestamp(data.lastSavedUtc);
        statusText.text = fromBackup ? "BACKUP-ONLY SAVE: the verified backup may contain older progress." :
                          fresh ? "NEW PROFILE: no save file was created by this viewer." :
                          "READ ONLY  •  Verified data from the selected profile.";
        statusText.color = fromBackup ? Red : Muted;
        if (tab == 0) BuildOverview(data);
        else BuildCollection(data);
        LayoutContent();
        UpdateTabs();
    }

    private void BuildOverview(MS5A1SaveData data)
    {
        MakeCard("BANK GOLD", data.bankGold.ToString("N0") + " stored Gold", genericItemIcon, Gold, "GOLD", "Profile's bank balance");
        MS5B1UpgradeDefinition[] definitions = MS5B1UpgradeDefinitions.GetAll();
        for (int i = 0; i < definitions.Length; i++)
        {
            MS5B1UpgradeDefinition d = definitions[i];
            if (d == null) continue;
            int level = 0;
            if (data.permanentUpgrades != null)
                foreach (MS5A1UpgradeEntry upgrade in data.permanentUpgrades)
                    if (upgrade != null && upgrade.id == d.id) { level = Mathf.Clamp(upgrade.level, 0, d.maxLevel); break; }
            MakeCard(d.title.ToUpperInvariant(), "LV " + level + "/" + d.maxLevel,
                d.id == "vitality" ? armorIcon : d.id == "strength" ? weaponIcon : accessoryIcon,
                Purple, "UPGRADE", Bonus(d.id, level));
        }
        string[] ids = { "map.green_meadow", "map.sand_dunes", "map.ancient_ruins" };
        string[] names = { "GREEN MEADOW", "SAND DUNES", "ANCIENT RUINS" };
        for (int i = 0; i < 3; i++)
        {
            bool purchased = i == 0 || Contains(data.unlockedContentIds, ids[i]);
            MS5C3MapPolicy.State state;
            bool policyOk = MS5C3MapPolicy.TryGet(MS5A2GoldBank.SelectedSlot, i, out state);
            string access = !policyOk ? "ACCESS UNKNOWN" : purchased ? "UNLOCKED" :
                state.allowed && state.legacy ? "LEGACY ACCESS" : state.allowed ? "ACCESS ALLOWED" : "LOCKED";
            MakeCard(names[i], access, mapIcon, policyOk && state.allowed ? Green : Muted,
                "MAP", purchased ? "Purchased/default unlock" : "Access follows current profile policy");
        }
        string[] bossIds = { "achievement.shadow_titan", "achievement.grave_warden" };
        string[] bossNames = { "TITAN SLAYER", "WARDEN BREAKER" };
        for (int i = 0; i < 2; i++)
        {
            bool earned = Contains(data.unlockedContentIds, bossIds[i]);
            MakeCard(bossNames[i], earned ? "UNLOCKED" : "LOCKED", medalIcon,
                earned ? Gold : Muted, "ACHIEVEMENT", earned ? "Boss achievement saved" : "Defeat the spawned Boss to unlock");
        }
        string[] licenseIds = { "equipment.reaper_edge", "equipment.wardens_oath" };
        string[] licenseNames = { "REAPER EDGE LICENSE", "WARDEN'S OATH LICENSE" };
        for (int i = 0; i < 2; i++)
        {
            bool unlocked = Contains(data.unlockedContentIds, licenseIds[i]);
            MakeCard(licenseNames[i], unlocked ? "LICENSED" : "LOCKED", weaponIcon,
                unlocked ? Green : Muted, "EQUIPMENT ACCESS", "License does NOT grant a free item");
        }
    }

    private void BuildCollection(MS5A1SaveData data)
    {
        bool snapshot = data.inventorySnapshotPresent;
        string[] types = { "WEAPON", "ARMOR", "ACCESSORY" };
        Sprite[] icons = { weaponIcon, armorIcon, accessoryIcon };
        for (int i = 0; i < 3; i++)
        {
            string itemId = "";
            if (snapshot && data.equippedItems != null)
                foreach (MS5A3SavedEquipped equipped in data.equippedItems)
                    if (equipped != null && equipped.type == i && !string.IsNullOrEmpty(equipped.itemId))
                    { itemId = equipped.itemId; break; }
            MS3AItemData item = ResolveItem(itemId);
            string name = !snapshot ? "NOT SAVED" : string.IsNullOrEmpty(itemId) ? "EMPTY" :
                item != null && !string.IsNullOrEmpty(item.displayName) ? item.displayName : ReadableName(itemId);
            MakeCard(types[i], name, item != null && item.icon ? item.icon : icons[i],
                string.IsNullOrEmpty(itemId) ? Muted : EquipmentColor(item), "EQUIPPED", !string.IsNullOrEmpty(itemId) ? itemId : "Read-only equipment slot");
        }
        for (int i = 0; i < 20; i++)
        {
            MS5A3SavedStack stack = snapshot && data.inventorySlots != null && i < data.inventorySlots.Count ? data.inventorySlots[i] : null;
            string itemId = stack != null && stack.quantity > 0 ? stack.itemId : "";
            MS3AItemData item = ResolveItem(itemId);
            string displayName = string.IsNullOrEmpty(itemId) ? snapshot ? "EMPTY" : "NO SNAPSHOT" :
                item != null && !string.IsNullOrEmpty(item.displayName) ? item.displayName : ReadableName(itemId);
            string sub = string.IsNullOrEmpty(itemId) ? "Inventory slot " + (i+1).ToString("00") :
                "QTY  " + stack.quantity + "  •  " + itemId;
            MakeCard("SLOT " + (i+1).ToString("00"), displayName,
                item != null && item.icon ? item.icon : genericItemIcon,
                string.IsNullOrEmpty(itemId) ? Muted : EquipmentColor(item), "INVENTORY", sub);
        }
    }

    private MS3AItemData ResolveItem(string id)
    {
        if (string.IsNullOrEmpty(id) || !itemRegistry || itemRegistry.items == null) return null;
        foreach (MS3AItemData item in itemRegistry.items)
            if (item && item.itemId == id) return item;
        return null;
    }

    private Color EquipmentColor(MS3AItemData item)
    {
        if (!item || !equipmentCatalog) return Purple;
        MS3DEquipmentDefinition definition;
        if (!equipmentCatalog.TryGet(item, out definition) || definition == null) return Purple;
        return MS3DEquipmentCatalog.QualityColor(definition.quality);
    }

    private void MakeCard(string name, string value, Sprite icon, Color accent, string kind, string detail)
    {
        RectTransform card = MakeRect("Card_" + name.Replace(' ', '_'), content, Vector2.zero, Vector2.one);
        Image bg = card.gameObject.AddComponent<Image>(); bg.color = Tile; bg.raycastTarget = false;
        MakeImage("Accent", card, new Vector2(0f, 0f), new Vector2(.012f, 1f), accent);
        RectTransform iconFrame = MakeRect("IconFrame", card, new Vector2(.035f, .16f), new Vector2(.195f, .83f));
        Image frame = iconFrame.gameObject.AddComponent<Image>(); frame.color = TileInner; frame.raycastTarget = false;
        Image graphic = MakeImage("Icon", iconFrame, new Vector2(.12f,.12f), new Vector2(.88f,.88f), icon ? Color.white : accent);
        if (icon) { graphic.sprite = icon; graphic.preserveAspect = true; }
        else { TMP_Text mark = MakeText("MARK", iconFrame, Vector2.zero, Vector2.one, 24, accent);
               mark.text = kind.Length > 0 ? kind.Substring(0,1) : "•"; mark.alignment = TextAlignmentOptions.Center; }
        TMP_Text heading = MakeText("Heading", card, new Vector2(.22f,.66f), new Vector2(.96f,.93f), 15, Gold);
        heading.text = name;
        TMP_Text label = MakeText("Value", card, new Vector2(.22f,.37f), new Vector2(.96f,.69f), 20, accent);
        label.text = value;
        TMP_Text foot = MakeText("Detail", card, new Vector2(.22f,.06f), new Vector2(.96f,.36f), 12, Muted);
        foot.text = string.IsNullOrEmpty(detail) ? kind : detail;
    }

    private void BuildUi()
    {
        Transform canvas = originalOverview.transform;
        layer = MakeRect("MS5D2_PolishedOverlay", canvas, Vector2.zero, Vector2.one).gameObject;
        layer.transform.SetAsLastSibling();
        layerGroup = layer.AddComponent<CanvasGroup>();
        MakeImage("Scrim", layer.transform, Vector2.zero, Vector2.one, Backdrop, true);
        panel = MakeRect("ProfileWindow", layer.transform, new Vector2(.07f,.055f), new Vector2(.93f,.945f));
        MakeImage("WindowBG", panel, Vector2.zero, Vector2.one, Window);
        MakeImage("GoldRule", panel, new Vector2(.025f,.958f), new Vector2(.975f,.965f), Gold);
        header = MakeText("PlayerProfile", panel, new Vector2(.045f,.865f), new Vector2(.58f,.949f), 36, Pale);
        goldText = MakeText("Gold", panel, new Vector2(.55f,.88f), new Vector2(.86f,.934f), 24, Gold);
        goldText.alignment = TextAlignmentOptions.MidlineRight;
        saveText = MakeText("Save", panel, new Vector2(.54f,.838f), new Vector2(.86f,.884f), 13, Muted);
        saveText.alignment = TextAlignmentOptions.MidlineRight;
        Button close = MakeButton("Close", panel, "X", new Vector2(.9f,.872f), new Vector2(.968f,.944f), out _);
        close.onClick.AddListener(Close);
        overviewButton = MakeButton("OverviewTab", panel, "OVERVIEW", new Vector2(.045f,.745f), new Vector2(.35f,.82f), out overviewBg);
        collectionButton = MakeButton("CollectionTab", panel, "INVENTORY & EQUIPMENT", new Vector2(.365f,.745f), new Vector2(.75f,.82f), out collectionBg);
        overviewButton.onClick.AddListener(() => SwitchPage(0));
        collectionButton.onClick.AddListener(() => SwitchPage(1));
        MakeImage("TabSeparator", panel, new Vector2(.04f,.738f), new Vector2(.96f,.741f), TileInner);
        viewport = MakeRect("ScrollViewport", panel, new Vector2(.04f,.115f), new Vector2(.96f,.724f));
        Image viewportGraphic = viewport.gameObject.AddComponent<Image>();
        viewportGraphic.color = new Color(0f,0f,0f,.001f); viewportGraphic.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();
        content = MakeRect("ScrollContent", viewport, new Vector2(0f,1f), new Vector2(1f,1f));
        content.pivot = new Vector2(.5f,1f);
        content.anchoredPosition = Vector2.zero;
        grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.spacing = new Vector2(10f,10f);
        grid.padding = new RectOffset(9,9,10,10);
        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = content;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 36f;
        cardsGroup = viewport.gameObject.AddComponent<CanvasGroup>();
        statusText = MakeText("Status", panel, new Vector2(.048f,.027f), new Vector2(.955f,.098f), 14, Muted);
        statusText.text = "READ ONLY";
        layer.SetActive(false);
        UpdateTabs();
    }

    private void UpdateTabs()
    {
        if (overviewBg) overviewBg.color = tab == 0 ? Purple : TileInner;
        if (collectionBg) collectionBg.color = tab == 1 ? Purple : TileInner;
    }

    private static RectTransform MakeRect(string label, Transform parent, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(label, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = min; r.anchorMax = max;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        return r;
    }

    private static Image MakeImage(string name, Transform parent, Vector2 min, Vector2 max, Color color, bool raycast = false)
    {
        RectTransform r = MakeRect(name,parent,min,max);
        Image img = r.gameObject.AddComponent<Image>(); img.color = color; img.raycastTarget = raycast;
        return img;
    }

    private static TMP_Text MakeText(string name, Transform parent, Vector2 min, Vector2 max, float fontSize, Color color)
    {
        RectTransform r = MakeRect("TXT_" + name, parent, min, max);
        TextMeshProUGUI text = r.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize; text.fontSizeMin = Mathf.Max(9f, fontSize * .57f);
        text.fontSizeMax = fontSize; text.enableAutoSizing = true;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = color; text.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset) text.font = TMP_Settings.defaultFontAsset;
        return text;
    }

    private static Button MakeButton(string name, Transform parent, string caption, Vector2 min, Vector2 max, out Image graphic)
    {
        RectTransform r = MakeRect(name, parent, min, max);
        graphic = r.gameObject.AddComponent<Image>(); graphic.raycastTarget = true; graphic.color = TileInner;
        Button button = r.gameObject.AddComponent<Button>(); button.targetGraphic = graphic;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f,.85f,1f,1f);
        colors.pressedColor = Gold;
        colors.fadeDuration = .12f;
        button.colors = colors;
        TMP_Text text = MakeText("Label", r, Vector2.zero, Vector2.one, 18, Pale);
        text.text = caption; text.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static bool Contains(List<string> list, string id) { return list != null && list.Contains(id); }
    private static string Bonus(string id, int level)
    {
        if (id == "vitality") return "+" + level * 10 + " HP";
        if (id == "strength") return "+" + level * 5 + "% Damage";
        if (id == "fury") return "+" + level * 3 + "% Attack Speed";
        if (id == "agility") return "+" + level * 2 + "% Move Speed";
        return "—";
    }
    private static string DisplayTimestamp(string timestamp)
    {
        DateTime dt;
        return DateTime.TryParse(timestamp, out dt) ? dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "UNKNOWN SAVE TIME";
    }
    private static string ReadableName(string id)
    {
        if (string.IsNullOrEmpty(id)) return "EMPTY";
        int dot = id.LastIndexOf('.');
        string name = dot >= 0 ? id.Substring(dot+1) : id;
        return name.Replace('_',' ').Replace('-',' ').ToUpperInvariant();
    }
    private void OnDestroy()
    {
        if (launcher) launcher.onClick.RemoveListener(Open);
    }
}
