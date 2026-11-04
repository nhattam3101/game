using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 7C.3.2 UI ONLY. Displays 3 legacy + 3 elemental combos, read-only.
/// Requirements come from SkillManager; legacy unlocked/active from PlayerStats;
/// 7C.2 readiness and proc counts are read directly from existing 7C.2 components.
/// </summary>
[DisallowMultipleComponent]
public sealed class MS7C3UnifiedSynergyUI : MonoBehaviour
{
    [Header("Existing gameplay references - never modified")]
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private SkillManager skillManager;

    [Header("Legacy compatibility fields (not used by 7C.4.2)")]
    [Tooltip("Drag the component that exposes bool IsWildfireConduitActive (or change the name below).")]
    [SerializeField] [HideInInspector] private MonoBehaviour wildfireCombatSource;
    [SerializeField] [HideInInspector] private string wildfireActiveMember = "IsWildfireConduitActive";
    [SerializeField] [HideInInspector] private MonoBehaviour glacialCombatSource;
    [SerializeField] [HideInInspector] private string glacialActiveMember = "IsGlacialReboundActive";
    [SerializeField] [HideInInspector] private MonoBehaviour thunderCombatSource;
    [SerializeField] [HideInInspector] private string thunderActiveMember = "IsThunderPrecisionActive";

    [Header("Six sprites, provided by this package")]
    [SerializeField] private Sprite frozenBurstIcon;
    [SerializeField] private Sprite phantomChainIcon;
    [SerializeField] private Sprite rapidBlastIcon;
    [SerializeField] private Sprite wildfireConduitIcon;
    [SerializeField] private Sprite glacialReboundIcon;
    [SerializeField] private Sprite thunderPrecisionIcon;

    [Header("Minimal HUD")]
    [SerializeField] private KeyCode openKey = KeyCode.J;
    [SerializeField] private KeyCode alternativeOpenKey = KeyCode.L;
    [SerializeField, Min(0.1f)] private float refreshSeconds = 0.25f;
    [SerializeField] private bool showSmallButton = true;

    // Editor Installer remembers which old UI behaviours it disabled (not the cores).
    [SerializeField, HideInInspector] private MonoBehaviour[] installerOldPanels = new MonoBehaviour[0];
    [SerializeField, HideInInspector] private bool[] installerOldStates = new bool[0];

    private readonly Color ink = new Color(.94f, .95f, 1f);
    private readonly Color muted = new Color(.62f, .68f, .78f);
    private readonly Color gold = new Color(.98f, .75f, .36f);
    private readonly Color green = new Color(.25f, .86f, .66f);
    private readonly Color purple = new Color(.52f, .42f, .84f);
    private readonly Color gray = new Color(.39f, .45f, .58f);

    private GameObject overlay;
    private RectTransform panel;
    private RectTransform gridViewport;
    private RectTransform gridContent;
    private ScrollRect scroll;
    private TMP_Text subtitle;
    private TMP_Text footer;
    private readonly Card[] cards = new Card[6];
    private float nextRefresh;
    private Vector2 previousCanvasSize = new Vector2(-1, -1);
    private Canvas hostingCanvas;

    // Old 2C/7C.1 panel scripts may create buttons inside Awake even if the
    // behaviour was disabled in Edit Mode. Hide ONLY their generated UI roots,
    // never their logic/core components, during Play Mode.
    private static readonly string[] LegacyUiRoots = {
        "Synergy_Open_Button", "Synergy_Overlay",
        "MS7C1_NewCombosButton", "MS7C1_NewCombosOverlay"
    };
    private float nextLegacyCheck;
    private bool openDiagnosticLogged;

    private sealed class Card
    {
        public RectTransform root;
        public Image backdrop;
        public Image icon;
        public TMP_Text initials;
        public TMP_Text requirements;
        public TMP_Text status;
        public Image statusBackground;
        public TMP_Text info;
    }

    private const string WildfireId = "7c1.wildfire_conduit";
    private const string GlacialId = "7c1.glacial_rebound";
    private const string ThunderId = "7c1.thunder_precision";

    private void Awake()
    {
        if (playerStats == null) playerStats = FindAnyObjectByType<PlayerStats>();
        if (skillManager == null) skillManager = FindAnyObjectByType<SkillManager>();
        hostingCanvas = GetComponentInParent<Canvas>();
        if (hostingCanvas == null)
        {
            Debug.LogError("[7C.3] Attach to an existing gameplay Canvas child; no new Canvas is created.", this);
            enabled = false;
            return;
        }
        hostingCanvas = hostingCanvas.rootCanvas;
        CreateUI();
        overlay.SetActive(false);
        HideOldPanelRoots();
    }

    private void Update()
    {
        // View scripts can run Awake in unpredictable order. Recheck for their
        // generated UI roots periodically, including when unified panel is closed.
        if (Time.unscaledTime >= nextLegacyCheck)
        {
            HideOldPanelRoots();
            nextLegacyCheck = Time.unscaledTime + 0.35f;
        }
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(openKey) ||
            (alternativeOpenKey != openKey && Input.GetKeyDown(alternativeOpenKey)) ||
            (overlay != null && overlay.activeSelf && Input.GetKeyDown(KeyCode.Escape)))
            Toggle();
#endif
        if (overlay == null || !overlay.activeSelf) return;
        if (IsSkillSelectionVisible())
        {
            Close();
            return;
        }
        if (hostingCanvas != null)
        {
            RectTransform canvasRect = hostingCanvas.transform as RectTransform;
            if (canvasRect != null && canvasRect.rect.size != previousCanvasSize)
                FitAndLayout();
        }
        if (Time.unscaledTime >= nextRefresh)
        {
            Refresh();
            nextRefresh = Time.unscaledTime + refreshSeconds;
        }
    }

    public void Toggle()
    {
        if (overlay == null) return;
        if (overlay.activeSelf) Close();
        else Open();
    }

    public void Open()
    {
        if (overlay == null || IsSkillSelectionVisible()) return;
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        FitAndLayout();
        HideOldPanelRoots();
        Refresh();
        if (!openDiagnosticLogged)
        {
            openDiagnosticLogged = true;
            int cardCount = 0;
            for (int i = 0; i < cards.Length; i++) if (cards[i] != null) cardCount++;
            Debug.Log("[7C.3.2] Unified panel OPEN; generated cards=" + cardCount +
                "/6; viewport=" + (gridViewport != null ? gridViewport.rect.size.ToString() : "missing") +
                "; content=" + (gridContent != null ? gridContent.rect.size.ToString() : "missing") +
                "; viewport RectMask2D=" + (gridViewport != null && gridViewport.GetComponent<RectMask2D>() != null), this);
            if (gridViewport == null || gridViewport.rect.width < 20f || gridViewport.rect.height < 20f)
                Debug.LogWarning("[7C.3.2] Viewport has no usable size. Check Canvas Scaler and RectTransform.", this);
        }
        nextRefresh = Time.unscaledTime + refreshSeconds;
    }

    public void Close()
    {
        if (overlay != null) overlay.SetActive(false);
    }

    private bool IsSkillSelectionVisible()
    {
        return skillManager != null && skillManager.skillPanel != null &&
               skillManager.skillPanel.activeInHierarchy;
    }

    private void HideOldPanelRoots()
    {
        if (hostingCanvas == null) return;
        Transform canvasRoot = hostingCanvas.rootCanvas.transform;
        for (int i = 0; i < LegacyUiRoots.Length; i++)
        {
            // Exact names from legacy UI; do not disable owner objects that may
            // contain MS7C1 unlock state, 7C2 combat or other gameplay scripts.
            Transform oldRoot = canvasRoot.Find(LegacyUiRoots[i]);
            if (oldRoot != null && oldRoot.gameObject.activeSelf)
            {
                oldRoot.gameObject.SetActive(false);
                Debug.Log("[7C.3.2] Hidden old generated UI root: " + oldRoot.name, this);
            }
        }
    }

    private void Refresh()
    {
        if (playerStats == null) playerStats = FindAnyObjectByType<PlayerStats>();
        if (skillManager == null) skillManager = FindAnyObjectByType<SkillManager>();

        bool[] met = new bool[6];
        bool[] active = new bool[6];
        bool[] known = new bool[6];
        bool[] readyButNotCore = new bool[6];
        string[] requirement = new string[6];

        // Legacy: original real unlock and active flags, never infer from prerequisites.
        if (playerStats != null)
        {
            met[0] = playerStats.FrozenBurstUnlocked;
            met[1] = playerStats.PhantomChainUnlocked;
            met[2] = playerStats.RapidBlastUnlocked;
            ProjectileEffectData equipped = playerStats.activeProjectileEffect;
            bool explosion = equipped != null && equipped.effectType == ProjectileEffectType.Explosion;
            active[0] = met[0] && explosion;
            active[1] = met[1] && playerStats.IsPhantomChainActive;
            active[2] = met[2] && playerStats.IsRapidBlastActive;
            known[0] = known[1] = known[2] = true;
        }

        bool slow = Owned("SlowShot", "FrostShot");
        bool explosive = Owned("ExplosiveShot");
        bool piercing = Owned("PiercingShot");
        bool ricochet = Owned("RicochetShot");
        bool speed = Owned("AttackSpeed");
        bool fire = Owned("FireAmmo", "FireBullet");
        bool ice = Owned("IceAmmo", "IceBullet");
        bool lightning = Owned("LightningAmmo", "LightningBullet");
        bool critical = Owned("CriticalHit", "CriticalStrike");

        requirement[0] = Mark(slow) + " Frost Shot  +  " + Mark(explosive) + " Explosive Shot";
        requirement[1] = Mark(piercing) + " Piercing Shot  +  " + Mark(ricochet) + " Ricochet Shot";
        requirement[2] = Mark(speed) + " Attack Speed  +  " + Mark(explosive) + " Explosive Shot";
        requirement[3] = Mark(fire) + " Fire Ammo  +  " + Mark(lightning) + " Lightning Ammo";
        requirement[4] = Mark(ice) + " Ice Ammo  +  " + Mark(ricochet) + " Ricochet Shot";
        requirement[5] = Mark(lightning) + " Lightning Ammo  +  " + Mark(critical) + " Critical Hit";

        // 7C.4.2: NEW combos must be confirmed by real 7C.1 core. 7C.2
        // readiness is NOT the same as a triggered per-bullet combat proc.
        MS7C42SynergyStatus.Snapshot[] real = new MS7C42SynergyStatus.Snapshot[3];
        int readyCount = 0;
        for (int i = 3; i < 6; i++)
        {
            real[i - 3] = MS7C42SynergyStatus.Read(skillManager, playerStats, i);
            met[i] = real[i - 3].coreUnlocked;
            known[i] = real[i - 3].combatFound;
            if (met[i] && real[i - 3].ready) readyCount++;
        }
        // Legacy: level possession alone does not prove original 2C unlock.
        readyButNotCore[0] = slow && explosive && !met[0];
        readyButNotCore[1] = piercing && ricochet && !met[1];
        readyButNotCore[2] = speed && explosive && !met[2];

        int unlockedTotal = 0;
        int legacyActive = 0;
        for (int i = 0; i < cards.Length; i++)
        {
            if (met[i]) unlockedTotal++;
            if (i < 3)
            {
                if (met[i] && active[i]) legacyActive++;
                SetCard(cards[i], met[i], known[i], met[i] && active[i],
                    requirement[i], readyButNotCore[i]);
            }
            else
                SetElementalCard(cards[i], real[i - 3], requirement[i]);
        }
        if (subtitle != null)
            subtitle.text = skillManager == null
                ? "SKILL DATA NOT LINKED - CHECK INSPECTOR"
                : unlockedTotal + "/6 UNLOCKED  |  " + readyCount + " ELEMENTAL READY  |  " +
                  legacyActive + " LEGACY ACTIVE";
        if (footer != null)
            footer.text = "[J]/[L] OPEN  |  ESC CLOSE  |  READY = waiting for qualifying hit, not guaranteed proc";
    }

    private void SetElementalCard(Card card, MS7C42SynergyStatus.Snapshot value, string condition)
    {
        if (card == null) return;
        card.requirements.text = condition;
        if (!value.coreFound)
        {
            card.status.text = value.ownsBoth ? "7C1 ?" : "LOCKED";
            card.statusBackground.color = value.ownsBoth ? gold : gray;
            card.backdrop.color = new Color(.17f,.14f,.2f,1f);
        }
        else if (!value.coreUnlocked)
        {
            card.status.text = value.ownsBoth ? "CORE ?" : "LOCKED";
            card.statusBackground.color = value.ownsBoth ? gold : gray;
            card.backdrop.color = new Color(.105f,.115f,.16f,1f);
        }
        else if (!value.combatFound)
        {
            card.status.text = "7C2 ?";
            card.statusBackground.color = purple;
            card.backdrop.color = new Color(.16f,.14f,.24f,1f);
        }
        else if (value.ready)
        {
            card.status.text = "READY";
            card.statusBackground.color = green;
            card.backdrop.color = new Color(.12f,.20f,.22f,1f);
        }
        else
        {
            card.status.text = "UNLOCKED";
            card.statusBackground.color = gold;
            card.backdrop.color = new Color(.16f,.155f,.215f,1f);
        }
        card.info.text = (value.combatFound ? "PROCS " + value.procs + " | " : "") + value.note;
        if (card.icon != null) card.icon.color = value.coreUnlocked ? Color.white : new Color(.6f,.62f,.7f);
        if (card.initials != null) card.initials.color = value.coreUnlocked ? ink : muted;
    }

    // 7C.3.3: Use the same tolerant SkillData matching as the level-up guide.
    // SkillManager's runtime levels are the source of truth for possession.
    private bool Owned(params string[] names)
    {
        for (int i = 0; i < names.Length; ++i)
            if (MS7C33SynergyRules.Owned(skillManager, names[i])) return true;
        return false;
    }

    private static string Mark(bool owned) { return owned ? "[OK]" : "[  ]"; }

    private bool ReadCombatActive(string id, MonoBehaviour source, string memberName, out bool active)
    {
        // Preferred if 7C.2 publishes status explicitly. A false value is valid.
        if (MS7C3CombatStatusBus.TryGet(id, out active)) return true;
        active = false;
        if (source == null || string.IsNullOrEmpty(memberName)) return false;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        Type type = source.GetType();
        try
        {
            PropertyInfo property = type.GetProperty(memberName, flags);
            if (property != null && property.PropertyType == typeof(bool) && property.GetIndexParameters().Length == 0)
            {
                active = (bool)property.GetValue(source, null);
                return true;
            }
            FieldInfo field = type.GetField(memberName, flags);
            if (field != null && field.FieldType == typeof(bool))
            {
                active = (bool)field.GetValue(source);
                return true;
            }
            MethodInfo method = type.GetMethod(memberName, flags, null, Type.EmptyTypes, null);
            if (method != null && method.ReturnType == typeof(bool))
            {
                active = (bool)method.Invoke(source, null);
                return true;
            }
        }
        catch (Exception)
        {
            // A transient source can disappear during wave transitions.
            // The UI refuses to assert ACTIVE if status is unavailable.
        }
        return false;
    }

    private void SetCard(Card card, bool unlocked, bool statusKnown, bool active, string condition,
        bool readyButCoreMissing)
    {
        if (card == null) return;
        card.requirements.text = condition;
        if (readyButCoreMissing)
        {
            card.status.text = "CORE ?";
            card.statusBackground.color = gold;
            card.backdrop.color = new Color(.17f, .14f, .20f, 1f);
            card.info.text = "Both skills owned; 2C core unlock NOT confirmed";
        }
        else if (!unlocked)
        {
            card.status.text = "LOCKED";
            card.statusBackground.color = gray;
            card.backdrop.color = new Color(.105f, .115f, .16f, 1f);
            card.info.text = "Learn both required skills";
        }
        else if (active)
        {
            card.status.text = "ACTIVE";
            card.statusBackground.color = green;
            card.backdrop.color = new Color(.12f, .20f, .22f, 1f);
            card.info.text = "Combat effect enabled";
        }
        else if (!statusKnown)
        {
            card.status.text = "7C2 ?";
            card.statusBackground.color = purple;
            card.backdrop.color = new Color(.16f, .14f, .24f, 1f);
            card.info.text = "Both skills owned; 7C.2 ACTIVE not verified";
        }
        else
        {
            card.status.text = "UNLOCKED";
            card.statusBackground.color = gold;
            card.backdrop.color = new Color(.16f, .155f, .215f, 1f);
            card.info.text = "Learned • effect currently inactive";
        }
        if (card.icon != null) card.icon.color = (unlocked || readyButCoreMissing) ? Color.white : new Color(.6f, .62f, .7f);
        if (card.initials != null) card.initials.color = (unlocked || readyButCoreMissing) ? ink : muted;
    }

    // Read-only access for the 7C.3.3 level-up guide. No combat changes.
    public Sprite GetComboIcon(int index)
    {
        switch (index)
        {
            case 0: return frozenBurstIcon;
            case 1: return phantomChainIcon;
            case 2: return rapidBlastIcon;
            case 3: return wildfireConduitIcon;
            case 4: return glacialReboundIcon;
            case 5: return thunderPrecisionIcon;
            default: return null;
        }
    }

    private void CreateUI()
    {
        Transform parent = hostingCanvas.transform;
        RectTransform toggle = NewRect(parent, "MS7C3_Minimal_Synergy_Button");
        SetAnchor(toggle, Vector2.one, Vector2.one, Vector2.one,
            new Vector2(-22f, -120f), new Vector2(43f, 43f));
        Image toggleFill = AddImage(toggle, new Color(.25f, .19f, .39f, .94f), true);
        Button toggleButton = toggle.gameObject.AddComponent<Button>();
        toggleButton.targetGraphic = toggleFill;
        toggleButton.onClick.AddListener(Toggle);
        MakeText(toggle, "S", "S", 19, FontStyles.Bold, TextAlignmentOptions.Center, ink,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        toggle.gameObject.SetActive(showSmallButton);

        RectTransform shade = NewRect(parent, "MS7C3_Unified_Overlay");
        shade.anchorMin = Vector2.zero;
        shade.anchorMax = Vector2.one;
        shade.offsetMin = Vector2.zero;
        shade.offsetMax = Vector2.zero;
        Image dim = AddImage(shade, new Color(.012f, .012f, .033f, .82f), true);
        Button outside = shade.gameObject.AddComponent<Button>();
        outside.targetGraphic = dim;
        outside.onClick.AddListener(Close);
        overlay = shade.gameObject;

        panel = NewRect(shade, "Unified_Skill_Synergy_Panel");
        SetAnchor(panel, new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(.5f,.5f),
            Vector2.zero, new Vector2(960,690));
        AddImage(panel, new Color(.065f,.068f,.115f,1), true);
        Outline edge = panel.gameObject.AddComponent<Outline>();
        edge.effectColor = new Color(.51f,.39f,.76f,.8f);
        edge.effectDistance = new Vector2(2,-2);

        MakeText(panel, "Title", "SKILL SYNERGIES", 31, FontStyles.Bold,
            TextAlignmentOptions.Left, ink,
            new Vector2(0,1), new Vector2(1,1), new Vector2(26,-24), new Vector2(-150,44));
        subtitle = MakeText(panel, "Subtitle", "0/6 UNLOCKED  |  0 ACTIVE", 17,
            FontStyles.Bold, TextAlignmentOptions.Left, gold,
            new Vector2(0,1), new Vector2(1,1), new Vector2(28,-75), new Vector2(-85,24));

        RectTransform close = NewRect(panel,"Close_Button");
        SetAnchor(close, Vector2.one, Vector2.one, Vector2.one, new Vector2(-21,-24), new Vector2(42,42));
        Image closeFill = AddImage(close, new Color(.24f,.22f,.33f,1), true);
        Button closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeFill;
        closeButton.onClick.AddListener(Close);
        MakeText(close,"X","X",22,FontStyles.Bold,TextAlignmentOptions.Center,ink,
            Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);

        RectTransform scrollArea = NewRect(panel,"Combo_ScrollArea");
        scrollArea.anchorMin = Vector2.zero;
        scrollArea.anchorMax = Vector2.one;
        scrollArea.offsetMin = new Vector2(20,64);
        scrollArea.offsetMax = new Vector2(-20,-122);
        scroll = scrollArea.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        gridViewport = NewRect(scrollArea,"Viewport");
        gridViewport.anchorMin = Vector2.zero;
        gridViewport.anchorMax = Vector2.one;
        gridViewport.offsetMin = gridViewport.offsetMax = Vector2.zero;
        // 7C.3.2: The old transparent Image + stencil Mask could clip EVERY card.
        // RectMask2D clips by the viewport rectangle and does not depend on image alpha.
        Image viewportHitArea = AddImage(gridViewport, new Color(1f, 1f, 1f, 0f), true);
        viewportHitArea.raycastTarget = true; // ScrollRect can still receive pointer drags.
        gridViewport.gameObject.AddComponent<RectMask2D>();

        gridContent = NewRect(gridViewport,"Six_Combo_Cards");
        gridContent.anchorMin = new Vector2(0,1);
        gridContent.anchorMax = new Vector2(1,1);
        gridContent.pivot = new Vector2(.5f,1);
        gridContent.anchoredPosition = Vector2.zero;
        scroll.viewport = gridViewport;
        scroll.content = gridContent;

        cards[0] = CreateCard(0,"FROZEN BURST","FROST + EXPLOSIVE","LEGACY",frozenBurstIcon,"FB",new Color(.3f,.76f,.93f));
        cards[1] = CreateCard(1,"PHANTOM CHAIN","PIERCE + RICOCHET","LEGACY",phantomChainIcon,"PC",new Color(.65f,.48f,.97f));
        cards[2] = CreateCard(2,"RAPID BLAST","ATTACK SPEED + EXPLOSIVE","LEGACY",rapidBlastIcon,"RB",new Color(.98f,.57f,.33f));
        cards[3] = CreateCard(3,"WILDFIRE CONDUIT","FIRE + LIGHTNING","ELEMENTAL",wildfireConduitIcon,"WC",new Color(.97f,.58f,.3f));
        cards[4] = CreateCard(4,"GLACIAL REBOUND","ICE + RICOCHET","ELEMENTAL",glacialReboundIcon,"GR",new Color(.42f,.79f,.95f));
        cards[5] = CreateCard(5,"THUNDER PRECISION","LIGHTNING + CRITICAL","ELEMENTAL",thunderPrecisionIcon,"TP",new Color(.9f,.77f,.39f));
        footer = MakeText(panel, "Footer", "[J] / [L] OPEN   •   ESC CLOSE   •   READ-ONLY DISPLAY", 13,
            FontStyles.Normal, TextAlignmentOptions.Center, muted,
            new Vector2(0,0), new Vector2(1,0), new Vector2(16,10), new Vector2(-32,36));
    }

    private Card CreateCard(int index, string displayName, string desc, string group, Sprite sprite,
        string abbreviation, Color accent)
    {
        Card card = new Card();
        card.root = NewRect(gridContent,"Card_" + index + "_" + abbreviation);
        card.backdrop = AddImage(card.root, new Color(.105f,.115f,.16f,1), true);
        Outline edge = card.root.gameObject.AddComponent<Outline>();
        edge.effectColor = new Color(accent.r*.32f,accent.g*.32f,accent.b*.32f,.9f);
        edge.effectDistance = new Vector2(1,-1);
        RectTransform iconBox = NewRect(card.root,"Icon_Accent");
        SetAnchor(iconBox, new Vector2(0,1), new Vector2(0,1), new Vector2(0,1),
            new Vector2(13,-13),new Vector2(62,62));
        AddImage(iconBox,new Color(accent.r*.22f,accent.g*.22f,accent.b*.22f,1),false);
        if (sprite != null)
        {
            RectTransform iconRect = NewRect(iconBox,"Icon");
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(5,5);
            iconRect.offsetMax = new Vector2(-5,-5);
            card.icon = AddImage(iconRect,Color.white,false);
            card.icon.sprite = sprite;
            card.icon.preserveAspect = true;
        }
        else
        {
            card.initials = MakeText(iconBox,"Initials",abbreviation,20,FontStyles.Bold,
                TextAlignmentOptions.Center,ink,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        }
        MakeText(card.root,"Group",group,11,FontStyles.Bold,TextAlignmentOptions.Left,accent,
            new Vector2(0,1),new Vector2(1,1),new Vector2(86,-10),new Vector2(-96,18));
        MakeText(card.root,"Name",displayName,19,FontStyles.Bold,TextAlignmentOptions.Left,ink,
            new Vector2(0,1),new Vector2(1,1),new Vector2(86,-30),new Vector2(-98,29));
        MakeText(card.root,"Short_Info",desc,12,FontStyles.Normal,TextAlignmentOptions.Left,muted,
            new Vector2(0,1),new Vector2(1,1),new Vector2(86,-63),new Vector2(-98,22));
        card.requirements = MakeText(card.root,"Requirements","-",13,FontStyles.Bold,
            TextAlignmentOptions.Left,gold,
            new Vector2(0,0),new Vector2(1,0),new Vector2(14,48),new Vector2(-28,35));
        card.info = MakeText(card.root,"Detail","-",12,FontStyles.Normal,
            TextAlignmentOptions.Left,muted,
            new Vector2(0,0),new Vector2(1,0),new Vector2(14,12),new Vector2(-152,28));
        RectTransform badge = NewRect(card.root,"Status_Chip");
        SetAnchor(badge,new Vector2(1,0),new Vector2(1,0),new Vector2(1,0),
            new Vector2(-12,14),new Vector2(115,28));
        card.statusBackground = AddImage(badge,gray,false);
        card.status = MakeText(badge,"Status","LOCKED",12,FontStyles.Bold,
            TextAlignmentOptions.Center,new Color(.06f,.06f,.1f),
            Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        return card;
    }

    private void FitAndLayout()
    {
        if (hostingCanvas == null || panel == null || gridViewport == null) return;
        RectTransform canvas = hostingCanvas.transform as RectTransform;
        if (canvas == null) return;
        Vector2 cs = canvas.rect.size;
        previousCanvasSize = cs;
        float width = Mathf.Max(240,Mathf.Min(990,cs.x-24));
        float height = Mathf.Max(270,Mathf.Min(720,cs.y-24));
        panel.sizeDelta = new Vector2(width,height);
        Canvas.ForceUpdateCanvases();
        float viewportWidth = Mathf.Max(180,gridViewport.rect.width);
        int columns = viewportWidth >= 740 ? 2 : 1;
        float gap = 12;
        float pad = 4;
        float cellWidth = (viewportWidth-2*pad-gap*(columns-1))/columns;
        float cellHeight = 157f;
        int rows = (cards.Length+columns-1)/columns;
        gridContent.sizeDelta = new Vector2(0,2*pad + rows*cellHeight + (rows-1)*gap);
        for (int i=0;i<cards.Length;i++)
        {
            if (cards[i] == null) continue;
            RectTransform rect = cards[i].root;
            rect.anchorMin = rect.anchorMax = new Vector2(0,1);
            rect.pivot = new Vector2(0,1);
            rect.sizeDelta = new Vector2(cellWidth,cellHeight);
            rect.anchoredPosition = new Vector2(pad+(i%columns)*(cellWidth+gap),
                -pad-(i/columns)*(cellHeight+gap));
        }
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
    }

    private static RectTransform NewRect(Transform parent, string name)
    {
        GameObject go = new GameObject(name,typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent,false);
        return rect;
    }

    private static void SetAnchor(RectTransform rect,Vector2 min,Vector2 max,Vector2 pivot,
        Vector2 pos,Vector2 size)
    {
        rect.anchorMin=min;
        rect.anchorMax=max;
        rect.pivot=pivot;
        rect.anchoredPosition=pos;
        rect.sizeDelta=size;
    }

    private static Image AddImage(RectTransform rect,Color color,bool raycast)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color=color;
        image.raycastTarget=raycast;
        return image;
    }

    private static TMP_Text MakeText(RectTransform parent,string name,string text,float size,
        FontStyles style,TextAlignmentOptions align,Color color,Vector2 anchorMin,
        Vector2 anchorMax,Vector2 offsetMin,Vector2 sizeDelta)
    {
        RectTransform rect = NewRect(parent,name);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = anchorMin == anchorMax ? anchorMin :
            new Vector2(anchorMin.x == anchorMax.x ? anchorMin.x : 0f,
                        anchorMin.y == anchorMax.y ? anchorMin.y : .5f);
        rect.anchoredPosition = offsetMin;
        rect.sizeDelta = sizeDelta;
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text=text;
        label.fontSize=size;
        label.fontStyle=style;
        label.alignment=align;
        label.color=color;
        label.raycastTarget=false;
        label.textWrappingMode=TextWrappingModes.NoWrap;
        label.overflowMode=TextOverflowModes.Ellipsis;
        return label;
    }

#if UNITY_EDITOR
    public void EditorAssign(PlayerStats stats,SkillManager skills,Sprite[] sprites)
    {
        playerStats=stats;
        skillManager=skills;
        if(sprites == null || sprites.Length != 6) return;
        frozenBurstIcon=sprites[0];
        phantomChainIcon=sprites[1];
        rapidBlastIcon=sprites[2];
        wildfireConduitIcon=sprites[3];
        glacialReboundIcon=sprites[4];
        thunderPrecisionIcon=sprites[5];
    }

    public void EditorRememberLegacy(MonoBehaviour[] panels,bool[] enabledStates)
    {
        installerOldPanels=panels;
        installerOldStates=enabledStates;
    }

    public MonoBehaviour[] EditorPreviousPanels { get { return installerOldPanels; } }
    public bool[] EditorPreviousStates { get { return installerOldStates; } }
    public void EditorBindCombatSources(MonoBehaviour wildfire, MonoBehaviour glacial, MonoBehaviour thunder)
    {
        if (wildfireCombatSource == null && wildfire != null) wildfireCombatSource = wildfire;
        if (glacialCombatSource == null && glacial != null) glacialCombatSource = glacial;
        if (thunderCombatSource == null && thunder != null) thunderCombatSource = thunder;
    }

    public bool EditorIconComplete
    {
        get
        {
            return frozenBurstIcon!=null && phantomChainIcon!=null && rapidBlastIcon!=null &&
                   wildfireConduitIcon!=null && glacialReboundIcon!=null && thunderPrecisionIcon!=null;
        }
    }
    public bool EditorReferencesComplete { get { return skillManager!=null && playerStats!=null; } }
#endif
}
