using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 7D.3 - Read-only Equipment Set display owned exclusively by the EXISTING 3D.1 inventory window.
/// No Inventory/Equipment/PlayerStats/Save mutation; runtime-created UI is not serialized.
/// </summary>
[DisallowMultipleComponent]
public sealed class MS7D3EquipmentSetsUI : MonoBehaviour
{
    [Header("References assigned by the 7D.3 editor installer")]
    public MS3DEquipmentUI equipmentUI;
    public MS3DPlayerEquipment playerEquipment;
    public MS7D1EquippedItemReader equippedReader;
    public MS7D1EquipmentSetRegistry setRegistry;
    public MS7D2SetBonusApplier bonusApplier;

    [Header("Presentation only")]
    [Range(.10f, 2f)] public float refreshSeconds = .35f;
    public bool useReadableCatalogNames = true;

    private Button openButton;
    private GameObject overlay;
    private RectTransform dialog;
    private RectTransform shadeRect;
    private RectTransform content;
    private TextMeshProUGUI summary;
    private TextMeshProUGUI footer;
    private TextMeshProUGUI emptyMessage;
    private MS3DPlayerEquipment hookedEquipment;
    private float nextRefresh;
    private string builtSignature = null;
    private readonly List<Card> cards = new List<Card>();

    private static readonly Color Bg = new Color(.038f, .027f, .077f, 1f);
    private static readonly Color Panel = new Color(.090f, .067f, .151f, 1f);
    private static readonly Color CardBg = new Color(.135f, .104f, .214f, 1f);
    private static readonly Color Gold = new Color(.94f, .75f, .43f, 1f);
    private static readonly Color White = new Color(.94f, .91f, .98f, 1f);
    private static readonly Color Muted = new Color(.66f, .63f, .76f, 1f);
    private static readonly Color Ready = new Color(.46f, .86f, .67f, 1f);
    private static readonly Color Empty = new Color(.26f, .21f, .37f, 1f);

    private sealed class Card
    {
        public string id;
        public MS7D1EquipmentSetRegistry.Rule rule;
        public TextMeshProUGUI name;
        public TextMeshProUGUI status;
        public TextMeshProUGUI pieces;
        public TextMeshProUGUI bonuses;
        public Image[] progress;
    }

    private void Awake() { ResolveReferences(); }

    private void OnEnable()
    {
        ResolveReferences();
        BindEquipmentEvent();
        EnsureVisuals();
        if (openButton != null) openButton.gameObject.SetActive(true);
    }

    private void Start() { BindEquipmentEvent(); EnsureVisuals(); }

    private void Update()
    {
        if (overlay == null || !overlay.activeSelf) return;
        if (equipmentUI == null || equipmentUI.inventoryUI == null ||
            equipmentUI.inventoryUI.panel == null || !equipmentUI.inventoryUI.panel.activeInHierarchy)
        {
            overlay.SetActive(false);
            return;
        }
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + Mathf.Max(.1f, refreshSeconds);
        RefreshReadOnly();
    }

    private void LateUpdate()
    {
        if (overlay == null || !overlay.activeSelf || dialog == null || shadeRect == null) return;
        float w = Mathf.Max(320f, shadeRect.rect.width - 36f);
        float h = Mathf.Max(270f, shadeRect.rect.height - 36f);
        Vector2 size = new Vector2(Mathf.Min(690f, w), Mathf.Min(630f, h));
        if ((dialog.sizeDelta - size).sqrMagnitude > 1f) dialog.sizeDelta = size;
    }

    private void OnDisable()
    {
        UnbindEquipmentEvent();
        if (overlay != null) overlay.SetActive(false);
        if (openButton != null) openButton.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        UnbindEquipmentEvent();
        // Only delete UI generated and owned by this component, never any 3A/3D windows.
        if (Application.isPlaying)
        {
            if (overlay != null) Destroy(overlay);
            if (openButton != null) Destroy(openButton.gameObject);
        }
    }

    private void ResolveReferences()
    {
        if (equipmentUI == null) equipmentUI = GetComponent<MS3DEquipmentUI>();
        if (playerEquipment == null && equipmentUI != null) playerEquipment = equipmentUI.equipment;
        if (equippedReader == null && playerEquipment != null)
            equippedReader = playerEquipment.GetComponent<MS7D1EquippedItemReader>();
        if (setRegistry == null && playerEquipment != null)
            setRegistry = playerEquipment.GetComponent<MS7D1EquipmentSetRegistry>();
        if (bonusApplier == null && playerEquipment != null)
            bonusApplier = playerEquipment.GetComponent<MS7D2SetBonusApplier>();
    }

    private void BindEquipmentEvent()
    {
        ResolveReferences();
        if (hookedEquipment == playerEquipment) return;
        UnbindEquipmentEvent();
        if (playerEquipment != null)
        {
            playerEquipment.EquipmentChanged += MarkForRefresh;
            hookedEquipment = playerEquipment;
        }
    }

    private void UnbindEquipmentEvent()
    {
        if (hookedEquipment != null) hookedEquipment.EquipmentChanged -= MarkForRefresh;
        hookedEquipment = null;
    }

    private void MarkForRefresh()
    {
        nextRefresh = 0f;
        if (overlay != null && overlay.activeSelf) RefreshReadOnly();
    }

    private void EnsureVisuals()
    {
        if (openButton != null && overlay != null) return;
        ResolveReferences();
        if (equipmentUI == null || equipmentUI.equipmentWindow == null ||
            equipmentUI.inventoryUI == null || equipmentUI.inventoryUI.panel == null) return;
        Transform shade = equipmentUI.inventoryUI.panel.transform;
        shadeRect = shade as RectTransform;
        if (shadeRect == null) return;

        // Small launcher in the TOP RIGHT of the Equipment window; no change to Equip/Unequip.
        RectTransform launcher = NewRect("MS7D3_SetsButton", equipmentUI.equipmentWindow,
            new Vector2(.77f, .913f), new Vector2(.963f, .970f));
        Image launchBg = launcher.gameObject.AddComponent<Image>(); launchBg.color = new Color(.29f, .195f, .45f, 1f);
        openButton = launcher.gameObject.AddComponent<Button>(); openButton.targetGraphic = launchBg;
        openButton.onClick.AddListener(Toggle);
        MakeText(launcher, "SETS", "SETS", 12, Gold, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one);

        RectTransform backdrop = NewRect("MS7D3_SetsOverlay", shadeRect, Vector2.zero, Vector2.one);
        overlay = backdrop.gameObject;
        Image dim = backdrop.gameObject.AddComponent<Image>();
        dim.color = new Color(.017f, .009f, .039f, .84f);
        Button outside = backdrop.gameObject.AddComponent<Button>(); outside.targetGraphic = dim;
        outside.onClick.AddListener(Close);

        dialog = NewRect("Dialog", backdrop, new Vector2(.5f, .5f), new Vector2(.5f, .5f));
        dialog.pivot = new Vector2(.5f, .5f);
        dialog.sizeDelta = new Vector2(690f, 630f);
        Image dialogBg = dialog.gameObject.AddComponent<Image>(); dialogBg.color = Bg;
        Outline frame = dialog.gameObject.AddComponent<Outline>();
        frame.effectColor = new Color(.60f, .39f, .78f, .75f);
        frame.effectDistance = new Vector2(1.5f, -1.5f);

        MakeText(dialog, "Title", "EQUIPMENT SETS", 28, Gold, TextAlignmentOptions.Left,
            new Vector2(.055f, .885f), new Vector2(.81f, .965f));
        RectTransform x = NewRect("Close", dialog, new Vector2(.88f, .90f), new Vector2(.965f, .965f));
        Image xb = x.gameObject.AddComponent<Image>(); xb.color = new Color(.26f, .17f, .35f, 1f);
        Button close = x.gameObject.AddComponent<Button>(); close.targetGraphic = xb; close.onClick.AddListener(Close);
        MakeText(x, "X", "X", 20, White, TextAlignmentOptions.Center, Vector2.zero, Vector2.one);

        summary = MakeText(dialog, "Summary", "", 15, White, TextAlignmentOptions.Left,
            new Vector2(.056f, .805f), new Vector2(.96f, .89f));
        RectTransform listBg = NewRect("ListBg", dialog, new Vector2(.045f, .17f), new Vector2(.955f, .805f));
        Image listBgImage = listBg.gameObject.AddComponent<Image>(); listBgImage.color = Panel;
        RectTransform viewport = NewRect("Viewport", listBg, new Vector2(.012f, .015f), new Vector2(.985f, .985f));
        Image viewportImage = viewport.gameObject.AddComponent<Image>(); viewportImage.color = new Color(1f, 1f, 1f, .001f);
        viewport.gameObject.AddComponent<RectMask2D>();
        content = NewRect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f));
        content.pivot = new Vector2(.5f, 1f);
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 9f;
        layout.padding = new RectOffset(7, 7, 7, 7);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroller = listBg.gameObject.AddComponent<ScrollRect>();
        scroller.viewport = viewport;
        scroller.content = content;
        scroller.horizontal = false;
        scroller.vertical = true;
        scroller.movementType = ScrollRect.MovementType.Clamped;
        scroller.scrollSensitivity = 34f;
        emptyMessage = MakeText(listBg, "Empty", "", 17, Muted, TextAlignmentOptions.Center,
            new Vector2(.08f, .20f), new Vector2(.92f, .78f));
        footer = MakeText(dialog, "Footer", "", 13, Muted, TextAlignmentOptions.Left,
            new Vector2(.058f, .037f), new Vector2(.94f, .158f));
        overlay.SetActive(false);
    }

    public void Toggle()
    {
        EnsureVisuals();
        if (overlay == null) return;
        if (overlay.activeSelf) Close();
        else Open();
    }

    public void Open()
    {
        EnsureVisuals();
        if (overlay == null || equipmentUI == null || equipmentUI.inventoryUI == null ||
            equipmentUI.inventoryUI.panel == null || !equipmentUI.inventoryUI.panel.activeInHierarchy) return;
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
        nextRefresh = 0f;
        builtSignature = null;
        RefreshReadOnly();
    }

    public void Close() { if (overlay != null) overlay.SetActive(false); }

    public void RefreshReadOnly()
    {
        if (overlay == null || !overlay.activeSelf || summary == null) return;
        ResolveReferences();
        if (setRegistry == null || equippedReader == null || playerEquipment == null)
        {
            summary.text = "MISSING EQUIPMENT / 7D.1 REFERENCES";
            ClearCards();
            emptyMessage.gameObject.SetActive(true);
            emptyMessage.text = "Run Tools > MonsterSurvival > 7D Equipment Sets > 13 (INSTALL).";
            footer.text = "Read-only display. No gameplay state changed.";
            return;
        }
        List<MS7D1EquipmentSetRegistry.Status> statuses;
        string diagnostic;
        bool valid = setRegistry.TryPreview(out statuses, out diagnostic);
        if (!valid)
        {
            summary.text = "EQUIPMENT READER NOT READY";
            ClearCards();
            emptyMessage.gameObject.SetActive(true);
            emptyMessage.text = diagnostic;
            footer.text = "No guessed item IDs. Configure 7D.1 first.";
            return;
        }
        string[] equipped;
        if (!equippedReader.TryRead(out equipped, out diagnostic)) equipped = new string[3];
        int equippedCount = 0;
        foreach (string id in equipped) if (!string.IsNullOrWhiteSpace(id)) equippedCount++;
        bool bonusEnabled = bonusApplier != null && bonusApplier.isActiveAndEnabled &&
            bonusApplier.mappingVerifiedByUser && bonusApplier.enableActualBonuses;
        string applied = bonusApplier == null ? "7D.2 NOT FOUND" :
            bonusEnabled ? (bonusApplier.CurrentlyApplying ? "LIVE +" : "GATES ON / CURRENT +") +
                bonusApplier.AppliedDamageBonus.ToString("0.##") + " DMG  |  +" +
                bonusApplier.AppliedAttackSpeedBonus.ToString("0.##") + " ASPD" : "PREVIEW ONLY (BONUS OFF)";
        summary.text = "EQUIPPED " + equippedCount + "/3   |   " + applied;
        footer.text = bonusEnabled ?
            "LIVE totals from 7D.2; per-set lines are eligibility, not proof of extra damage." :
            "2-piece / 3-piece values are PREVIEW only until 7D.2 is verified and enabled.";

        string sig = Signature(statuses);
        if (builtSignature != sig) RebuildCards(statuses, sig);
        emptyMessage.gameObject.SetActive(cards.Count == 0);
        if (cards.Count == 0)
            emptyMessage.text = "NO ENABLED EQUIPMENT SETS\n\nConfigure real piece IDs in 7D.1 Registry.\n7D.2.1 installer also offers an optional demo set.";
        var byId = new Dictionary<string, MS7D1EquipmentSetRegistry.Status>(StringComparer.OrdinalIgnoreCase);
        foreach (var status in statuses) if (status != null && !byId.ContainsKey(status.id)) byId.Add(status.id, status);
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string id in equipped) if (!string.IsNullOrWhiteSpace(id)) present.Add(id.Trim());
        foreach (Card card in cards)
        {
            MS7D1EquipmentSetRegistry.Status s;
            if (!byId.TryGetValue(card.id, out s)) continue;
            card.name.text = s.name.ToUpperInvariant();
            card.status.text = s.matched + "/" + s.total + "  " + (s.complete ? "COMPLETE" : s.twoPieceReady ? "2-PIECE READY" : "LOCKED");
            card.status.color = s.complete ? Gold : s.twoPieceReady ? Ready : Muted;
            for (int p = 0; p < card.progress.Length; p++)
            {
                bool exists = p < s.total;
                card.progress[p].gameObject.SetActive(exists);
                if (exists) card.progress[p].color = p < s.matched ? Ready : Empty;
            }
            var labels = new List<string>();
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (card.rule.pieceIds != null)
            {
                foreach (string raw in card.rule.pieceIds)
                {
                    string id = raw == null ? "" : raw.Trim();
                    if (id.Length == 0 || !unique.Add(id)) continue;
                    labels.Add((present.Contains(id) ? "[X] " : "[ ] ") + FriendlyName(id));
                }
            }
            card.pieces.text = string.Join("    |    ", labels.ToArray());
            card.bonuses.text = "2 PCS: +" + s.previewTwoPieceDamage.ToString("0.##") + " DMG MULT" +
                (s.total == 3 ? "     3 PCS: +" + s.previewThreePieceAttackSpeed.ToString("0.##") + " ASPD MULT" : "");
        }
    }

    private string FriendlyName(string id)
    {
        if (!useReadableCatalogNames || playerEquipment == null || playerEquipment.catalog == null ||
            playerEquipment.catalog.entries == null) return id;
        foreach (MS3DEquipmentDefinition row in playerEquipment.catalog.entries)
        {
            if (row == null || row.item == null) continue;
            if (string.Equals(row.item.itemId, id, StringComparison.OrdinalIgnoreCase))
                return string.IsNullOrWhiteSpace(row.item.displayName) ? id : row.item.displayName;
        }
        return id;
    }

    private static string Signature(List<MS7D1EquipmentSetRegistry.Status> statuses)
    {
        var sb = new System.Text.StringBuilder();
        foreach (MS7D1EquipmentSetRegistry.Status s in statuses)
            sb.Append(s.id).Append(':').Append(s.total).Append('|');
        return sb.ToString();
    }

    private void ClearCards()
    {
        cards.Clear();
        builtSignature = null;
        if (content == null) return;
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
    }

    private void RebuildCards(List<MS7D1EquipmentSetRegistry.Status> statuses, string signature)
    {
        ClearCards();
        if (setRegistry == null || setRegistry.sets == null) return;
        foreach (MS7D1EquipmentSetRegistry.Status status in statuses)
        {
            MS7D1EquipmentSetRegistry.Rule found = null;
            foreach (var rule in setRegistry.sets)
                if (rule != null && rule.enabledRule && string.Equals(rule.setId, status.id, StringComparison.OrdinalIgnoreCase))
                { found = rule; break; }
            if (found == null) continue;
            cards.Add(CreateCard(found, status));
        }
        builtSignature = signature;
    }

    private Card CreateCard(MS7D1EquipmentSetRegistry.Rule rule, MS7D1EquipmentSetRegistry.Status status)
    {
        RectTransform root = NewRect("Set_" + status.id, content, Vector2.zero, Vector2.one);
        Image bg = root.gameObject.AddComponent<Image>(); bg.color = CardBg;
        LayoutElement layout = root.gameObject.AddComponent<LayoutElement>(); layout.preferredHeight = 156f;
        Card card = new Card { id = status.id, rule = rule };
        card.name = MakeText(root, "Name", "", 19, Gold, TextAlignmentOptions.Left,
            new Vector2(.035f, .78f), new Vector2(.72f, .97f));
        card.status = MakeText(root, "Status", "", 13, Muted, TextAlignmentOptions.Right,
            new Vector2(.70f, .78f), new Vector2(.975f, .97f));
        card.progress = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            RectTransform p = NewRect("Progress_" + i, root,
                new Vector2(.035f + i * .317f, .65f), new Vector2(.331f + i * .317f, .697f));
            Image img = p.gameObject.AddComponent<Image>(); img.color = Empty; img.raycastTarget = false;
            card.progress[i] = img;
        }
        card.pieces = MakeText(root, "Pieces", "", 13, White, TextAlignmentOptions.Left,
            new Vector2(.038f, .24f), new Vector2(.98f, .64f));
        card.bonuses = MakeText(root, "Bonuses", "", 13, Gold, TextAlignmentOptions.Left,
            new Vector2(.038f, .035f), new Vector2(.98f, .28f));
        return card;
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static TextMeshProUGUI MakeText(Transform parent, string name, string text,
        int fontSize, Color color, TextAlignmentOptions align, Vector2 min, Vector2 max)
    {
        RectTransform rect = NewRect(name, parent, min, max);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = align;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }
}
