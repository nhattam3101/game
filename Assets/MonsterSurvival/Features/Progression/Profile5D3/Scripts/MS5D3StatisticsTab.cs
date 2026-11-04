using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Adds a third, read-only tab to the 5D.2 polished PROFILE panel.
// Keeps existing 5D.2 tabs, their listeners, and 5A/5B/5C Save data untouched.
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public sealed class MS5D3StatisticsTab : MonoBehaviour
{
    public MS5D2ProfilePolish profilePolish;

    [Header("5D.3.2 Statistics icons (linked by 5D.3.2 Installer)")]
    public Sprite victoriesIcon;
    public Sprite victoryGoldIcon;
    public Sprite shadowTitanIcon;
    public Sprite graveWardenIcon;
    public Sprite mapsIcon;
    public Sprite bossBadgesIcon;
    public Sprite upgradesIcon;
    public Sprite licensesIcon;

    private Transform window;
    private GameObject originalViewport;
    private GameObject statisticsViewport;
    private RectTransform statisticsRows;
    private Button statisticsTab;
    private Image overviewTabBg;
    private Image collectionTabBg;
    private Image statisticsTabBg;
    private TMP_Text wins, gold, titan, warden, maps, badges, upgrades, equipmentLicenses, status;
    private int previousSlot = -1;
    private float nextRefresh;
    private bool showing;
    private readonly Color pale = new Color(.94f,.90f,.99f,1f);
    private readonly Color goldColor = new Color(.96f,.76f,.43f,1f);
    private readonly Color muted = new Color(.76f,.70f,.83f,1f);

    private void Start()
    {
        if (!profilePolish) profilePolish = GetComponent<MS5D2ProfilePolish>();
        Transform runtime = transform.Find("MS5D2_PolishedOverlay/ProfileWindow");
        if (!profilePolish || !runtime)
        {
            Debug.LogWarning("[5D.3] Requires 5D.2 on the same Profile root. No UI was changed.", this);
            enabled = false;
            return;
        }
        window = runtime;
        Transform existing = window.Find("ScrollViewport");
        Button overview = FindButton("OverviewTab");
        Button collection = FindButton("CollectionTab");
        if (!existing || !overview || !collection)
        {
            Debug.LogError("[5D.3] 5D.2 UI layout changed; Statistics tab cannot be attached safely.", this);
            enabled = false;
            return;
        }
        originalViewport = existing.gameObject;
        statisticsTab = MakeButton(window, "MS5D3_StatisticsTab", "STATISTICS", new Vector2(.76f,.745f), new Vector2(.955f,.82f));
        statisticsTabBg = statisticsTab.GetComponent<Image>();
        overviewTabBg = overview.GetComponent<Image>();
        collectionTabBg = collection.GetComponent<Image>();
        statisticsTab.onClick.AddListener(ShowStatistics);
        overview.onClick.AddListener(ShowOverview);
        collection.onClick.AddListener(ShowCollection);
        BuildStatisticsContent();
        statisticsViewport.SetActive(false);
        Debug.Log("[5D.3] Statistics tab attached to Profile 5D.2 (read-only).", this);
    }

    private Button FindButton(string name)
    {
        Transform child = window ? window.Find(name) : null;
        return child ? child.GetComponent<Button>() : null;
    }

    private void Update()
    {
        if (!showing || !statisticsViewport || !statisticsViewport.activeInHierarchy) return;
        if (previousSlot != MS5A2GoldBank.SelectedSlot || Time.unscaledTime >= nextRefresh)
            Refresh();
    }

    private void ShowStatistics()
    {
        if (!statisticsViewport || !originalViewport) return;
        // 5D.2 is still the owner of the open/close animation and tab bar.
        originalViewport.SetActive(false);
        statisticsViewport.SetActive(true);
        statisticsViewport.transform.SetAsLastSibling();
        // Build layout after enabling the previously hidden viewport.
        Canvas.ForceUpdateCanvases();
        if (statisticsRows) LayoutRebuilder.ForceRebuildLayoutImmediate(statisticsRows);
        showing = true;
        if (statisticsTabBg) statisticsTabBg.color = new Color(.66f,.44f,.89f,1f);
        if (overviewTabBg) overviewTabBg.color = new Color(.19f,.13f,.27f,1f);
        if (collectionTabBg) collectionTabBg.color = new Color(.19f,.13f,.27f,1f);
        Refresh();
    }

    private void ShowOverview()
    {
        HideStatistics();
        if (overviewTabBg) overviewTabBg.color = new Color(.66f,.44f,.89f,1f);
        if (collectionTabBg) collectionTabBg.color = new Color(.19f,.13f,.27f,1f);
    }

    private void ShowCollection()
    {
        HideStatistics();
        if (overviewTabBg) overviewTabBg.color = new Color(.19f,.13f,.27f,1f);
        if (collectionTabBg) collectionTabBg.color = new Color(.66f,.44f,.89f,1f);
    }

    private void HideStatistics()
    {
        showing = false;
        if (statisticsViewport) statisticsViewport.SetActive(false);
        if (originalViewport) originalViewport.SetActive(true);
        if (statisticsTabBg) statisticsTabBg.color = new Color(.19f,.13f,.27f,1f);
    }

    private void Refresh()
    {
        int slot = MS5A2GoldBank.SelectedSlot;
        previousSlot = slot;
        nextRefresh = Time.unscaledTime + 1f;
        MS5D3StatsData data;
        string message;
        bool backup;
        bool validStats = MS5D3StatsStore.TryRead(slot, out data, out message, out backup);
        bool noStats = !validStats && message.StartsWith("NO_STATS", StringComparison.Ordinal);
        if (!validStats)
        {
            string unknown = noStats ? "—" : "!";
            wins.text = unknown;
            gold.text = unknown;
            titan.text = unknown;
            warden.text = unknown;
            status.text = noStats ? "Chưa có thống kê 5D.3: số liệu cũ không được suy đoán." :
                "STATS INVALID — cần kiểm tra file và backup trong công cụ Audit.";
        }
        else
        {
            wins.text = data.victories.ToString("N0");
            gold.text = data.totalVictoryGold.ToString("N0");
            titan.text = data.shadowTitanKills.ToString("N0");
            warden.text = data.graveWardenKills.ToString("N0");
            status.text = backup ? "BACKUP ONLY — thống kê có thể cũ; ghi mới bị khóa để bảo vệ dữ liệu." :
                "Tracked since 5D.3 was installed  •  rev " + data.revision;
        }
        MS5A1SaveData save;
        bool recovered;
        string reason;
        bool hasSave = MS5A1SaveService.TryLoad(slot, out save, out reason, out recovered);
        bool fresh = !hasSave && !string.IsNullOrEmpty(reason) && reason.StartsWith("NO_SAVE", StringComparison.Ordinal);
        if (!hasSave && !fresh)
        {
            maps.text = badges.text = upgrades.text = equipmentLicenses.text = "UNAVAILABLE";
            status.text += "\nProfile Save unavailable: " + reason;
            return;
        }
        if (fresh) save = MS5A1SaveService.CreateFresh(slot);
        int unlockedMaps = 1;
        if (Has(save,"map.sand_dunes")) unlockedMaps++;
        if (Has(save,"map.ancient_ruins")) unlockedMaps++;
        maps.text = unlockedMaps + " / 3 purchased or starter";
        int badgeCount = (Has(save,"achievement.shadow_titan")?1:0) + (Has(save,"achievement.grave_warden")?1:0);
        badges.text = badgeCount + " / 2 achievements";
        int licenseCount = (Has(save,"equipment.reaper_edge")?1:0) + (Has(save,"equipment.wardens_oath")?1:0);
        equipmentLicenses.text = licenseCount + " / 2 licenses";
        int total = 0;
        if (save.permanentUpgrades != null)
            foreach (MS5A1UpgradeEntry e in save.permanentUpgrades)
                if (e != null && (e.id == "vitality" || e.id == "strength" || e.id == "fury" || e.id == "agility"))
                    total += Mathf.Clamp(e.level,0,10);
        upgrades.text = Mathf.Clamp(total,0,40) + " / 40 levels";
        if (recovered) status.text += "\nPROFILE SAVE FROM BACKUP — may contain older unlock progress.";
    }

    private static bool Has(MS5A1SaveData data, string id)
    {
        return data != null && data.unlockedContentIds != null && data.unlockedContentIds.Contains(id);
    }

    private void BuildStatisticsContent()
    {
        RectTransform viewport = CreateRect("MS5D3_StatsViewport", window,
            new Vector2(.04f,.115f), new Vector2(.96f,.724f));
        statisticsViewport = viewport.gameObject;
        Image surface = statisticsViewport.AddComponent<Image>();
        surface.color = new Color(.085f,.062f,.14f,.97f);
        surface.raycastTarget = true;
        statisticsViewport.AddComponent<RectMask2D>();
        RectTransform content = CreateRect("MS5D3_StatsRows", viewport, new Vector2(0f,1f),new Vector2(1f,1f));
        statisticsRows = content;
        content.pivot = new Vector2(.5f,1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f,830f);
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12,12,14,14);
        layout.spacing = 9f;
        // FIX 5D.3.1: VerticalLayoutGroup must assign row heights from LayoutElement.
        // With false, each stretched child starts at zero height and all labels overlap.
        layout.childControlHeight = true;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        wins = Row(content,"VICTORIES  /  CHIẾN THẮNG",goldColor, Pick(victoriesIcon, profilePolish.medalIcon));
        gold = Row(content,"TOTAL GOLD BANKED BY VICTORY  /  GOLD TÍCH LŨY",goldColor, Pick(victoryGoldIcon, profilePolish.genericItemIcon));
        titan = Row(content,"SHADOW TITAN  /  SỐ LẦN HẠ",new Color(.80f,.60f,1f),Pick(shadowTitanIcon,profilePolish.medalIcon));
        warden = Row(content,"GRAVE WARDEN  /  SỐ LẦN HẠ",new Color(.80f,.60f,1f),Pick(graveWardenIcon,profilePolish.medalIcon));
        maps = Row(content,"MAPS UNLOCKED  /  BẢN ĐỒ",pale,Pick(mapsIcon,profilePolish.mapIcon));
        badges = Row(content,"BOSS ACHIEVEMENTS  /  HUY HIỆU",pale,Pick(bossBadgesIcon,profilePolish.medalIcon));
        upgrades = Row(content,"PERMANENT UPGRADES  /  NÂNG CẤP",pale,Pick(upgradesIcon,profilePolish.weaponIcon));
        equipmentLicenses = Row(content,"EQUIPMENT LICENSES  /  QUYỀN TRANG BỊ",pale,Pick(licensesIcon,profilePolish.armorIcon));
        status = MakeLabel("TXT_Status",content,13,muted);
        status.alignment = TextAlignmentOptions.MidlineLeft;
        LayoutElement statusLayout = status.gameObject.AddComponent<LayoutElement>();
        statusLayout.preferredHeight = 80;
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 42f;
    }

    private static Sprite Pick(Sprite preferred, Sprite fallback)
    {
        return preferred ? preferred : fallback;
    }

    private TMP_Text Row(Transform parent, string label, Color valueColor, Sprite rowIcon)
    {
        RectTransform row = CreateRect("Row_" + label.Split(' ')[0],parent,Vector2.zero,Vector2.one);
        Image bg = row.gameObject.AddComponent<Image>();
        bg.color = new Color(.15f,.105f,.21f,.95f);
        bg.raycastTarget = false;
        LayoutElement size = row.gameObject.AddComponent<LayoutElement>();
        size.minHeight = 72f;
        size.preferredHeight = 72f;
        // Non-interactive icon in each statistics row; does not block scrolling or buttons.
        RectTransform iconRect = CreateRect("ICON_Statistics", row, new Vector2(0f,.5f),new Vector2(0f,.5f));
        iconRect.sizeDelta = new Vector2(44f,44f);
        iconRect.anchoredPosition = new Vector2(39f,0f);
        Image iconGraphic = iconRect.gameObject.AddComponent<Image>();
        iconGraphic.sprite = rowIcon;
        iconGraphic.color = rowIcon ? Color.white : new Color(.75f,.60f,.88f,.5f);
        iconGraphic.preserveAspect = true;
        iconGraphic.raycastTarget = false;
        TMP_Text title = MakeLabel("TXT_Label",row,15,muted);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(.025f,0f); titleRect.anchorMax = new Vector2(.69f,1f);
        titleRect.offsetMin = new Vector2(77f,0f);
        titleRect.offsetMax = Vector2.zero;
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.fontSizeMin = 12f;
        title.text = label;
        TMP_Text value = MakeLabel("TXT_Value",row,22,valueColor);
        RectTransform valueRect = value.rectTransform;
        valueRect.anchorMin = new Vector2(.69f,0f); valueRect.anchorMax = new Vector2(.985f,1f);
        valueRect.offsetMin = valueRect.offsetMax = Vector2.zero;
        value.alignment = TextAlignmentOptions.MidlineRight;
        value.text = "—";
        return value;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 a, Vector2 b)
    {
        GameObject obj = new GameObject(name,typeof(RectTransform));
        obj.layer = 5;
        obj.transform.SetParent(parent,false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = a;
        rect.anchorMax = b;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }
    private static TMP_Text MakeLabel(string name,Transform parent,float fontSize,Color color)
    {
        RectTransform rect = CreateRect(name,parent,Vector2.zero,Vector2.one);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10;
        text.fontSizeMax = fontSize;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.color = color;
        text.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset) text.font = TMP_Settings.defaultFontAsset;
        return text;
    }
    private Button MakeButton(Transform parent, string name, string caption,Vector2 a,Vector2 b)
    {
        RectTransform rect = CreateRect(name,parent,a,b);
        Image bg = rect.gameObject.AddComponent<Image>();
        bg.color = new Color(.19f,.13f,.27f,1f);
        bg.raycastTarget = true;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(.9f,.78f,1f,1f);
        colors.pressedColor = goldColor;
        button.colors = colors;
        TMP_Text label = MakeLabel("TXT_Label",rect,14,pale);
        label.text = caption;
        label.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private void OnDestroy()
    {
        if (statisticsTab) statisticsTab.onClick.RemoveListener(ShowStatistics);
        if (window)
        {
            Button a = FindButton("OverviewTab"), b = FindButton("CollectionTab");
            if (a) a.onClick.RemoveListener(ShowOverview);
            if (b) b.onClick.RemoveListener(ShowCollection);
        }
    }
}
