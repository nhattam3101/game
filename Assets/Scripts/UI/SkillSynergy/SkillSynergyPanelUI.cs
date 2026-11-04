using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MonsterSurvival v1.2 - Skill Synergy UI 2D.
/// READ-ONLY view of PlayerStats 2C: never changes skills or the projectile type.
/// Attach to an empty child GameObject under the EXISTING HUD Canvas.
/// A small bottom-right button and the panel are created automatically.
/// </summary>
public class SkillSynergyPanelUI : MonoBehaviour
{
    [Header("Existing game components")]
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private SkillManager skillManager;

    [Header("Icon sprites - optional (PNG -> Sprite 2D and UI)")]
    [SerializeField] private Sprite frozenBurstIcon;
    [SerializeField] private Sprite phantomChainIcon;
    [SerializeField] private Sprite rapidBlastIcon;

    [Header("Opening the panel")]
    [SerializeField] private KeyCode toggleKey = KeyCode.J;
    [SerializeField] private bool createOpenButton = true;
    [SerializeField] private Button existingOpenButton;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.2f;

    private GameObject overlay;
    private TMP_Text counterText;
    private TMP_Text currentEffectText;
    private CardView frozenCard;
    private CardView phantomCard;
    private CardView rapidCard;
    private float nextRefresh;

    private readonly Color ink = new Color(0.91f, 0.94f, 1f, 1f);
    private readonly Color subtle = new Color(0.62f, 0.70f, 0.84f, 1f);
    private readonly Color muted = new Color(0.36f, 0.43f, 0.58f, 1f);
    private readonly Color green = new Color(0.25f, 0.87f, 0.60f, 1f);
    private readonly Color yellow = new Color(1f, 0.76f, 0.37f, 1f);

    private sealed class CardView
    {
        public Image background;
        public Image icon;
        public TMP_Text fallbackIcon;
        public TMP_Text requirements;
        public TMP_Text status;
        public Image statusBackground;
    }

    private void Awake()
    {
        if (playerStats == null)
            playerStats = FindFirstObjectByType<PlayerStats>();

        if (skillManager == null)
            skillManager = FindFirstObjectByType<SkillManager>();

        // Attach to an existing Canvas, not to the Player or a Prefab asset.
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("SkillSynergyPanelUI: Put this component on a child of your HUD Canvas.", this);
            enabled = false;
            return;
        }

        BuildInterface(canvas.transform);

        if (existingOpenButton != null)
            existingOpenButton.onClick.AddListener(TogglePanel);

        overlay.SetActive(false);
    }

    private void Update()
    {
        // Use unscaled input/time: interface also works while the game is paused.
        if (Input.GetKeyDown(toggleKey))
            TogglePanel();

        if (overlay == null || !overlay.activeSelf)
            return;

        // The level-up panel must not be hidden behind the Synergy interface.
        if (IsLevelUpOpen())
        {
            ClosePanel();
            return;
        }

        if (Time.unscaledTime >= nextRefresh)
        {
            RefreshPanel();
            nextRefresh = Time.unscaledTime + refreshInterval;
        }
    }

    private bool IsLevelUpOpen()
    {
        return skillManager != null && skillManager.skillPanel != null &&
               skillManager.skillPanel.activeInHierarchy;
    }

    public void TogglePanel()
    {
        if (overlay == null)
            return;

        if (overlay.activeSelf)
            ClosePanel();
        else
            OpenPanel();
    }

    public void OpenPanel()
    {
        if (overlay == null || IsLevelUpOpen())
            return;

        if (playerStats == null)
            playerStats = FindFirstObjectByType<PlayerStats>();
        if (skillManager == null)
            skillManager = FindFirstObjectByType<SkillManager>();

        RefreshPanel();
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
        nextRefresh = Time.unscaledTime + refreshInterval;
    }

    public void ClosePanel()
    {
        if (overlay != null)
            overlay.SetActive(false);
    }

    private void RefreshPanel()
    {
        if (playerStats == null)
        {
            if (counterText != null) counterText.text = "PLAYER NOT FOUND";
            return;
        }

        bool frozen = playerStats.FrozenBurstUnlocked;
        bool phantom = playerStats.PhantomChainUnlocked;
        bool rapid = playerStats.RapidBlastUnlocked;

        ProjectileEffectData equipped = playerStats.activeProjectileEffect;
        bool explosionEquipped = equipped != null &&
                                 equipped.effectType == ProjectileEffectType.Explosion;
        bool frozenActive = frozen && explosionEquipped;
        bool phantomActive = playerStats.IsPhantomChainActive;
        bool rapidActive = playerStats.IsRapidBlastActive;

        SetCard(frozenCard, frozen, frozenActive,
            "FROST " + Mark(SkillType.SlowShot) +
            "   +   EXPLOSIVE " + Mark(SkillType.ExplosiveShot));

        SetCard(phantomCard, phantom, phantomActive,
            "PIERCING " + Mark(SkillType.PiercingShot) +
            "   +   RICOCHET " + Mark(SkillType.RicochetShot));

        SetCard(rapidCard, rapid, rapidActive,
            "ATTACK SPEED " + Mark(SkillType.AttackSpeed) +
            "   +   EXPLOSIVE " + Mark(SkillType.ExplosiveShot));

        int count = (frozen ? 1 : 0) + (phantom ? 1 : 0) + (rapid ? 1 : 0);
        counterText.text = count + "/3  DA MO KHOA";

        if (equipped == null)
            currentEffectText.text = "DAN HIEN TAI:  Dan thuong";
        else
            currentEffectText.text = "DAN HIEN TAI:  " + GetEffectLabel(equipped.effectType);
    }

    private string Mark(SkillType type)
    {
        if (skillManager == null || skillManager.allSkills == null)
            return "?"; // SkillManager reference not available.

        foreach (SkillData skill in skillManager.allSkills)
        {
            if (skill != null && skill.skillType == type &&
                skillManager.GetSkillLevel(skill) > 0)
                return "[OK]";
        }
        return "[  ]";
    }

    private static string GetEffectLabel(ProjectileEffectType type)
    {
        switch (type)
        {
            case ProjectileEffectType.Pierce: return "Piercing";
            case ProjectileEffectType.Ricochet: return "Ricochet";
            case ProjectileEffectType.Explosion: return "Explosive";
            case ProjectileEffectType.Slow: return "Frost";
            default: return "Normal";
        }
    }

    private void SetCard(CardView view, bool unlocked, bool active, string requirements)
    {
        if (view == null) return;
        view.requirements.text = requirements;
        view.background.color = active
            ? new Color(0.14f, 0.26f, 0.32f, 1f)
            : unlocked
                ? new Color(0.18f, 0.20f, 0.35f, 1f)
                : new Color(0.13f, 0.17f, 0.26f, 1f);

        view.status.text = active ? "DANG DUNG" : unlocked ? "DA MO" : "CHUA MO";
        view.status.color = active ? new Color(0.03f, 0.14f, 0.12f, 1f) :
            unlocked ? new Color(0.18f, 0.12f, 0.04f, 1f) : ink;
        view.statusBackground.color = active ? green : unlocked ? yellow : muted;

        if (view.icon != null)
            view.icon.color = unlocked ? Color.white : new Color(0.59f, 0.63f, 0.72f, 1f);
        if (view.fallbackIcon != null)
            view.fallbackIcon.color = unlocked ? ink : subtle;
    }

    private void BuildInterface(Transform canvas)
    {
        if (createOpenButton)
        {
            RectTransform toggleRect = NewRect(canvas, "Synergy_Open_Button");
            SetAnchor(toggleRect, new Vector2(1f, 0f),
                new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-24f, 24f), new Vector2(165f, 45f));
            Image toggleImage = AddImage(toggleRect, new Color(0.34f, 0.23f, 0.67f, 0.97f));
            Button toggle = toggleRect.gameObject.AddComponent<Button>();
            toggle.targetGraphic = toggleImage;
            toggle.onClick.AddListener(TogglePanel);
            MakeText(toggleRect, "Open_Text", "SYNERGY  [J]", 21f, FontStyles.Bold,
                TextAlignmentOptions.Center, ink, Stretch());
        }

        RectTransform shade = NewRect(canvas, "Synergy_Overlay");
        shade.anchorMin = Vector2.zero;
        shade.anchorMax = Vector2.one;
        shade.offsetMin = Vector2.zero;
        shade.offsetMax = Vector2.zero;
        Image dim = AddImage(shade, new Color(0.015f, 0.025f, 0.07f, 0.86f));
        dim.raycastTarget = true;
        Button outsideClose = shade.gameObject.AddComponent<Button>();
        outsideClose.targetGraphic = dim;
        outsideClose.onClick.AddListener(ClosePanel);
        overlay = shade.gameObject;

        RectTransform panel = NewRect(shade, "Synergy_Panel");
        SetAnchor(panel, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
            new Vector2(.5f, .5f), Vector2.zero, new Vector2(770f, 595f));
        Image panelBackground = AddImage(panel, new Color(0.067f, 0.093f, 0.16f, 1f));
        panelBackground.raycastTarget = true;
        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(.34f, .27f, .59f, .95f);
        outline.effectDistance = new Vector2(2f, -2f);

        MakeText(panel, "Panel_Title", "SKILL SYNERGY", 35f, FontStyles.Bold,
            TextAlignmentOptions.Left, ink,
            TopLeft(26f, -13f, 460f, 45f));
        MakeText(panel, "Panel_Subtitle", "TO HOP KY NANG  |  MONSTERSURVIVAL v1.2", 16f,
            FontStyles.Normal, subtle, TopLeft(26f, -63f, 600f, 25f), TextAlignmentOptions.Left);

        RectTransform closeRect = NewRect(panel, "Close_Button");
        SetAnchor(closeRect, Vector2.one, Vector2.one,
            new Vector2(1f, 1f), new Vector2(-17f, -17f), new Vector2(49f, 42f));
        Image closeImage = AddImage(closeRect, new Color(.23f, .26f, .38f, 1f));
        Button closeButton = closeRect.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        closeButton.onClick.AddListener(ClosePanel);
        MakeText(closeRect, "X", "X", 24f, FontStyles.Bold,
            TextAlignmentOptions.Center, ink, Stretch());

        frozenCard = MakeCard(panel, 0, "FROZEN BURST", "NO DIEN RONG + LAM CHAM",
            frozenBurstIcon, "FB", new Color(.15f, .55f, .77f, 1f));
        phantomCard = MakeCard(panel, 1, "PHANTOM CHAIN", "DAN XUYEN ROI NAY",
            phantomChainIcon, "PC", new Color(.63f, .37f, .87f, 1f));
        rapidCard = MakeCard(panel, 2, "RAPID BLAST", "MOI DOT BAN TAO THEM 2 DAN NO",
            rapidBlastIcon, "RB", new Color(.93f, .44f, .27f, 1f));

        counterText = MakeText(panel, "Unlocked_Count", "0/3  DA MO KHOA", 18f,
            FontStyles.Bold, TextAlignmentOptions.Left, yellow,
            TopLeft(28f, -539f, 270f, 33f));
        currentEffectText = MakeText(panel, "Equipped_Effect", "DAN HIEN TAI: -", 18f,
            FontStyles.Bold, TextAlignmentOptions.Right, subtle,
            TopRight(-27f, -539f, 380f, 33f));
    }

    private CardView MakeCard(RectTransform panel, int index, string title,
        string desc, Sprite iconSprite, string fallback, Color iconColor)
    {
        RectTransform rect = NewRect(panel, title.Replace(' ', '_'));
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -108f - index * 139f);
        rect.sizeDelta = new Vector2(-44f, 125f);

        CardView view = new CardView();
        view.background = AddImage(rect, new Color(.13f, .17f, .26f, 1f));

        RectTransform iconBorder = NewRect(rect, "Icon_Box");
        SetAnchor(iconBorder, new Vector2(0f, .5f), new Vector2(0f, .5f),
            new Vector2(.5f, .5f), new Vector2(61f, 0f), new Vector2(94f, 94f));
        AddImage(iconBorder, new Color(iconColor.r * .30f, iconColor.g * .30f,
            iconColor.b * .30f, 1f));

        if (iconSprite != null)
        {
            RectTransform iconRect = NewRect(iconBorder, "Icon");
            SetAnchor(iconRect, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(.5f, .5f), Vector2.zero, new Vector2(76f, 76f));
            view.icon = AddImage(iconRect, Color.white);
            view.icon.sprite = iconSprite;
            view.icon.preserveAspect = true;
        }
        else
        {
            view.fallbackIcon = MakeText(iconBorder, "Fallback_Icon", fallback,
                25f, FontStyles.Bold, TextAlignmentOptions.Center, ink, Stretch());
        }

        MakeText(rect, "Name", title, 24f, FontStyles.Bold,
            TextAlignmentOptions.Left, ink, LeftMid(124f, 32f, 365f, 29f));
        MakeText(rect, "Description", desc, 15f, FontStyles.Normal,
            TextAlignmentOptions.Left, subtle, LeftMid(124f, 1f, 405f, 23f));
        view.requirements = MakeText(rect, "Requirements", "-", 15f,
            FontStyles.Bold, TextAlignmentOptions.Left, yellow,
            LeftMid(124f, -32f, 445f, 27f));

        RectTransform badge = NewRect(rect, "Status_Badge");
        SetAnchor(badge, new Vector2(1f, .5f), new Vector2(1f, .5f),
            new Vector2(1f, .5f), new Vector2(-15f, 0f), new Vector2(132f, 42f));
        view.statusBackground = AddImage(badge, muted);
        view.status = MakeText(badge, "Status_Label", "CHUA MO", 17f,
            FontStyles.Bold, TextAlignmentOptions.Center, ink, Stretch());
        return view;
    }

    private static RectTransform NewRect(Transform parent, string name)
    {
        GameObject element = new GameObject(name, typeof(RectTransform));
        RectTransform rect = element.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static Image AddImage(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text MakeText(RectTransform parent, string name, string content,
        float size, FontStyles style, TextAlignmentOptions align, Color color,
        RectTransformConfiguration placement)
    {
        RectTransform rect = NewRect(parent, name);
        SetAnchor(rect, placement.min, placement.max, placement.pivot,
            placement.position, placement.size);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = align;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    // Overload with a more natural argument order for subtitle creation.
    private static TMP_Text MakeText(RectTransform parent, string name, string content,
        float size, FontStyles style, Color color,
        RectTransformConfiguration placement, TextAlignmentOptions align)
    {
        return MakeText(parent, name, content, size, style, align, color, placement);
    }

    private struct RectTransformConfiguration
    {
        public Vector2 min, max, pivot, position, size;
        public RectTransformConfiguration(Vector2 a, Vector2 b, Vector2 c,
            Vector2 d, Vector2 e)
        { min = a; max = b; pivot = c; position = d; size = e; }
    }

    private static RectTransformConfiguration Stretch() =>
        new RectTransformConfiguration(Vector2.zero, Vector2.one,
            new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);

    private static RectTransformConfiguration TopLeft(float x, float y, float w, float h) =>
        new RectTransformConfiguration(new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(x, y), new Vector2(w, h));

    private static RectTransformConfiguration TopRight(float x, float y, float w, float h) =>
        new RectTransformConfiguration(new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(x, y), new Vector2(w, h));

    private static RectTransformConfiguration LeftMid(float x, float y, float w, float h) =>
        new RectTransformConfiguration(new Vector2(0f, .5f), new Vector2(0f, .5f),
            new Vector2(0f, .5f), new Vector2(x, y), new Vector2(w, h));

    private static void SetAnchor(RectTransform rect, Vector2 min, Vector2 max,
        Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void OnDestroy()
    {
        if (existingOpenButton != null)
            existingOpenButton.onClick.RemoveListener(TogglePanel);
    }
}
