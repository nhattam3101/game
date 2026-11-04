using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 7C.3.3 - READ-ONLY level up synergy advice and unlock notification.
/// Uses SkillManager's actual 3 offered SkillData entries, never changes offers,
/// Click listeners, combat damage, upgrades or Save data.
/// Attach to the existing MS7C3_UnifiedSynergyUI GameObject.
/// </summary>
[DisallowMultipleComponent]
public sealed class MS7C33LevelUpGuide : MonoBehaviour
{
    [Header("Auto references")]
    [SerializeField] private SkillManager skillManager;
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private MS7C3UnifiedSynergyUI unifiedUI;

    [Header("Options")]
    [SerializeField] private bool showDuringSkillSelection = true;
    [SerializeField] private bool showUnlockToast = true;
    [SerializeField, Min(0.1f)] private float refreshInterval = 0.20f;

    private Canvas rootCanvas;
    private RectTransform canvasRect;
    private GameObject guide;
    private RectTransform guideRect;
    private TMP_Text guideHeader;
    private readonly Row[] rows = new Row[6];
    private GameObject toast;
    private TMP_Text toastText;
    private float toastUntil;
    private float nextRefresh;
    private bool[] alreadyUnlocked = new bool[6];
    private bool initializedBaseline;
    private bool previousSelection;
    private Vector2 lastCanvasSize = Vector2.zero;

    private readonly Color ink = new Color(.93f, .94f, 1f, 1f);
    private readonly Color soft = new Color(.66f, .7f, .82f, 1f);
    private readonly Color yellow = new Color(1f, .76f, .32f, 1f);
    private readonly Color mint = new Color(.32f, .91f, .70f, 1f);
    private readonly Color blue = new Color(.38f, .72f, 1f, 1f);

    private sealed class Row
    {
        public Image background;
        public Image icon;
        public TMP_Text iconFallback;
        public TMP_Text title;
        public TMP_Text description;
        public TMP_Text chip;
        public RectTransform transform;
    }

    private void Awake()
    {
        if (skillManager == null) skillManager = FindAnyObjectByType<SkillManager>();
        if (playerStats == null) playerStats = FindAnyObjectByType<PlayerStats>();
        if (unifiedUI == null) unifiedUI = GetComponent<MS7C3UnifiedSynergyUI>();
        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null) rootCanvas = rootCanvas.rootCanvas;
        if (rootCanvas == null)
        {
            Debug.LogWarning("[7C.3.3] Missing Canvas. Guide disabled; gameplay unaffected.", this);
            enabled = false;
            return;
        }
        canvasRect = rootCanvas.transform as RectTransform;
        CreateGuide();
        CreateToast();
        guide.SetActive(false);
        toast.SetActive(false);
    }

    private void Update()
    {
        if (skillManager == null) skillManager = FindAnyObjectByType<SkillManager>();
        if (playerStats == null) playerStats = FindAnyObjectByType<PlayerStats>();
        bool selecting = showDuringSkillSelection && skillManager != null &&
            skillManager.skillPanel != null && skillManager.skillPanel.activeInHierarchy;

        if (guide != null && guide.activeSelf != selecting)
        {
            guide.SetActive(selecting);
            if (selecting)
            {
                // The original Level Up UI remains fully clickable.
                guide.transform.SetAsLastSibling();
                LayoutGuide();
                RefreshGuide();
                Debug.Log("[7C.3.3] Level-up combo guide OPEN (6 cards, 3 live choices).", this);
            }
        }
        if (selecting && canvasRect != null && canvasRect.rect.size != lastCanvasSize)
            LayoutGuide();

        if (Time.unscaledTime >= nextRefresh)
        {
            if (selecting) RefreshGuide();
            CheckNewUnlocks();
            nextRefresh = Time.unscaledTime + refreshInterval;
        }
        if (toast != null && toast.activeSelf && Time.unscaledTime >= toastUntil)
            toast.SetActive(false);
        previousSelection = selecting;
    }

    private void CheckNewUnlocks()
    {
        if (skillManager == null) return;
        if (!initializedBaseline)
        {
            for (int i = 0; i < 6; i++)
                alreadyUnlocked[i] = MS7C33SynergyRules.CountsAsUnlocked(skillManager, playerStats, i);
            initializedBaseline = true;
            return;
        }
        for (int i = 0; i < 6; i++)
        {
            bool now = MS7C33SynergyRules.CountsAsUnlocked(skillManager, playerStats, i);
            if (now && !alreadyUnlocked[i] && showUnlockToast)
            {
                // For 7C.1 new combos this confirms TWO owned skills, NOT 7C.2 combat.
                string label = (i >= 3 ? "DU DIEU KIEN GHEP: " : "DA MO COMBO: ") +
                    MS7C33SynergyRules.Combos[i].Title;
                ShowToast(label);
                Debug.Log("[7C.3.3] " + label +
                    "; New elemental effects require separate 7C.2 confirmation.", this);
            }
            alreadyUnlocked[i] = now;
        }
    }

    private void RefreshGuide()
    {
        if (skillManager == null) return;
        SkillData[] choices = new SkillData[3];
        bool anyOffered = false;
        for (int j = 0; j < 3; j++)
        {
            if (skillManager.skillButtons != null && j < skillManager.skillButtons.Length &&
                skillManager.skillButtons[j] != null &&
                skillManager.skillButtons[j].gameObject.activeInHierarchy)
            {
                choices[j] = MS7C33SynergyRules.GetOffered(skillManager, j);
                if (choices[j] != null) anyOffered = true;
            }
        }
        if (guideHeader != null)
            guideHeader.text = anyOffered ? "6 COMBOS   |   CHON 1 TRONG 3" :
                "6 COMBOS   |   DANG DOC 3 LUA CHON";
        for (int i = 0; i < 6; i++)
        {
            Row row = rows[i];
            if (row == null) continue;
            int progress = MS7C33SynergyRules.Progress(skillManager, i);
            bool unlocked = MS7C33SynergyRules.CountsAsUnlocked(skillManager, playerStats, i);
            bool coreWait = i < 3 && progress == 2 && !unlocked;
            string best = "";
            bool completes = false;
            int choiceIndex = -1;
            for (int j = 0; j < 3; ++j)
            {
                bool willComplete;
                bool helps = MS7C33SynergyRules.OfferedHelps(skillManager, choices[j], i, out willComplete);
                if (helps && (choiceIndex < 0 || (willComplete && !completes)))
                { choiceIndex = j; completes = willComplete; }
            }
            string detail;
            if (unlocked) detail = i < 3 ? "DA MO (CORE 2C)" : "DU 2 KY NANG (7C.1)";
            else if (coreWait) detail = "DU 2 KY NANG - CORE 2C CHUA XAC NHAN";
            else detail = progress + "/2  " +
                MS7C33SynergyRules.Combos[i].First + " + " +
                MS7C33SynergyRules.Combos[i].Second;
            if (!unlocked && !coreWait && choiceIndex >= 0)
                best = "CHON " + (choiceIndex + 1) + (completes ? " -> HOAN THANH" : " -> +1 MANH");
            if (row.title != null) row.title.text = MS7C33SynergyRules.Combos[i].Title;
            if (row.description != null) row.description.text = detail;
            if (row.chip != null) row.chip.text = unlocked ? "DONE" : coreWait ? "CORE ?" :
                choiceIndex < 0 ? progress + "/2" : best;
            if (row.background != null)
                row.background.color = completes ? new Color(.17f,.27f,.23f,1f) :
                    choiceIndex >= 0 ? new Color(.19f,.175f,.28f,1f) :
                    unlocked ? new Color(.14f,.2f,.22f,1f) :
                    new Color(.12f,.125f,.18f,1f);
            if (row.chip != null)
                row.chip.color = completes ? mint : choiceIndex >= 0 ? yellow :
                    unlocked ? mint : soft;
        }
    }

    private void ShowToast(string message)
    {
        if (toast == null) return;
        toastText.text = message;
        toast.SetActive(true);
        toast.transform.SetAsLastSibling();
        toastUntil = Time.unscaledTime + 3.25f;
    }

    private void LayoutGuide()
    {
        if (guideRect == null || canvasRect == null) return;
        Rect cr = canvasRect.rect;
        lastCanvasSize = cr.size;
        float width = Mathf.Clamp(cr.width * .27f, 185f, 292f);
        width = Mathf.Min(width, cr.width * .38f);
        float height = Mathf.Clamp(cr.height - 36f, 320f, 552f);
        guideRect.sizeDelta = new Vector2(width, height);

        // Compare candidate positions against actual three buttons in canvas
        // local space; prefer the side with the smaller overlap area.
        Vector2 centerR = new Vector2(cr.xMax - width*.5f - 8f, cr.center.y);
        Vector2 centerL = new Vector2(cr.xMin + width*.5f + 8f, cr.center.y);
        float overlapR = OverlapWithChoices(centerR,width,height);
        float overlapL = OverlapWithChoices(centerL,width,height);
        Vector2 target = overlapR <= overlapL ? centerR : centerL;
        guideRect.anchorMin = guideRect.anchorMax = new Vector2(.5f,.5f);
        guideRect.pivot = new Vector2(.5f,.5f);
        guideRect.anchoredPosition = target - cr.center;
        if (overlapR > 0 && overlapL > 0)
            Debug.LogWarning("[7C.3.3] Level-up buttons occupy both sides. Guide remains non-interactive; consider a larger Game View resolution.", this);

        float usable = height - 74f;
        float rowHeight = Mathf.Max(36f, (usable - 5f*5f)/6f);
        for (int i = 0; i < rows.Length; ++i)
        {
            if (rows[i] == null) continue;
            RectTransform rect = rows[i].transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f,1f);
            rect.pivot = new Vector2(.5f,1f);
            rect.sizeDelta = new Vector2(width - 16f, rowHeight);
            rect.anchoredPosition = new Vector2(0, -62f - i * (rowHeight+5f));
            // On a very short Game View the 3rd text line would collide;
            // preserve combo name + PICK hint as the essential information.
            if (rows[i].description != null)
                rows[i].description.enabled = rowHeight >= 55f;
        }
    }

    private float OverlapWithChoices(Vector2 center, float width, float height)
    {
        if (skillManager == null || skillManager.skillButtons == null || canvasRect == null) return 0f;
        Rect candidate = new Rect(center.x-width*.5f, center.y-height*.5f,width,height);
        float area = 0f;
        for (int i=0;i<skillManager.skillButtons.Length && i<3;i++)
        {
            Button button = skillManager.skillButtons[i];
            if (button == null) continue;
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, button.transform);
            Rect rect = new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
            float x = Mathf.Max(0f,Mathf.Min(candidate.xMax,rect.xMax)-Mathf.Max(candidate.xMin,rect.xMin));
            float y = Mathf.Max(0f,Mathf.Min(candidate.yMax,rect.yMax)-Mathf.Max(candidate.yMin,rect.yMin));
            area += x*y;
        }
        return area;
    }

    private void CreateGuide()
    {
        guideRect = NewRect(rootCanvas.transform,"MS7C33_LevelUp_Combo_Guide");
        guide = guideRect.gameObject;
        CanvasGroup group = guide.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false; // Critical: does NOT block any skill choice.
        Image background = AddImage(guideRect,new Color(.065f,.06f,.11f,.98f));
        background.raycastTarget = false;
        Outline edge = guide.AddComponent<Outline>();
        edge.effectColor = new Color(.41f,.32f,.67f,.8f);
        edge.effectDistance = new Vector2(1,-1);
        Text(guideRect,"Title","SYNERGY GUIDE",18,ink,
            new Vector2(.5f,1f),new Vector2(.5f,1f),new Vector2(0,-10),new Vector2(-16,26),TextAlignmentOptions.Center);
        guideHeader = Text(guideRect,"Summary","6 COMBOS",11,yellow,
            new Vector2(.5f,1f),new Vector2(.5f,1f),new Vector2(0,-36),new Vector2(-12,20),TextAlignmentOptions.Center);
        for (int i = 0; i < 6; i++)
        {
            RectTransform slot = NewRect(guideRect,"Combo_Card_"+i);
            Row row = rows[i] = new Row();
            row.transform = slot;
            row.background=AddImage(slot,new Color(.12f,.125f,.18f,1f));
            RectTransform iconRect = NewRect(slot,"Icon");
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f,.5f);
            iconRect.pivot = new Vector2(0f,.5f);
            iconRect.sizeDelta = new Vector2(34,34);
            iconRect.anchoredPosition = new Vector2(7f,0f);
            Sprite icon = unifiedUI != null ? unifiedUI.GetComboIcon(i) : null;
            if (icon != null)
            {
                row.icon=AddImage(iconRect,Color.white);
                row.icon.sprite=icon;
                row.icon.preserveAspect=true;
            }
            else
            {
                AddImage(iconRect,new Color(.21f,.19f,.33f,1f));
                row.iconFallback=Text(iconRect,"Fallback",MS7C33SynergyRules.Combos[i].Short,12,ink,
                    Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,TextAlignmentOptions.Center);
            }
            row.title=Text(slot,"Name",MS7C33SynergyRules.Combos[i].Title,12,ink,
                new Vector2(0f,1f),new Vector2(1f,1f),new Vector2(48,-13),new Vector2(-55,22),TextAlignmentOptions.Left);
            row.description=Text(slot,"Progress","0/2",10,soft,
                new Vector2(0f,1f),new Vector2(1f,1f),new Vector2(48,-33),new Vector2(-55,19),TextAlignmentOptions.Left);
            row.chip=Text(slot,"ChoiceHint","",10,yellow,
                new Vector2(0f,0f),new Vector2(1f,0f),new Vector2(48,11),new Vector2(-55,18),TextAlignmentOptions.Left);
        }
        LayoutGuide();
    }

    private void CreateToast()
    {
        RectTransform rect = NewRect(rootCanvas.transform,"MS7C33_Combo_Toast");
        toast=rect.gameObject;
        rect.anchorMin=rect.anchorMax=new Vector2(.5f,1f);
        rect.pivot=new Vector2(.5f,1f);
        rect.anchoredPosition=new Vector2(0,-12);
        rect.sizeDelta=new Vector2(440,38);
        AddImage(rect,new Color(.11f,.15f,.2f,.97f));
        CanvasGroup group=rect.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts=false;
        group.interactable=false;
        toastText=Text(rect,"Text","",15,mint,
            Vector2.zero,Vector2.one,new Vector2(8,0),new Vector2(-16,0),TextAlignmentOptions.Center);
    }

    private static RectTransform NewRect(Transform parent,string name)
    {
        GameObject go=new GameObject(name,typeof(RectTransform));
        RectTransform rect=go.GetComponent<RectTransform>();
        rect.SetParent(parent,false);
        return rect;
    }

    private static Image AddImage(RectTransform rect,Color color)
    {
        Image image=rect.gameObject.AddComponent<Image>();
        image.color=color;
        image.raycastTarget=false;
        return image;
    }

    private static TMP_Text Text(RectTransform parent,string name,string value,float size,Color color,
        Vector2 anchorMin,Vector2 anchorMax,Vector2 position,Vector2 dimensions,TextAlignmentOptions align)
    {
        RectTransform rect=NewRect(parent,name);
        rect.anchorMin=anchorMin;
        rect.anchorMax=anchorMax;
        rect.pivot=anchorMin==anchorMax ? anchorMin : new Vector2(0,.5f);
        rect.anchoredPosition=position;
        rect.sizeDelta=dimensions;
        TextMeshProUGUI label=rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text=value;
        label.fontSize=size;
        label.alignment=align;
        label.color=color;
        label.raycastTarget=false;
        label.textWrappingMode=TextWrappingModes.NoWrap;
        label.overflowMode=TextOverflowModes.Ellipsis;
        return label;
    }

#if UNITY_EDITOR
    public void EditorAssign(SkillManager manager,PlayerStats stats,MS7C3UnifiedSynergyUI ui)
    {
        skillManager=manager;
        playerStats=stats;
        unifiedUI=ui;
    }
    public bool EditorValid { get { return skillManager!=null && playerStats!=null && unifiedUI!=null; } }
#endif

    private void OnDestroy()
    {
        if (guide != null) Destroy(guide);
        if (toast != null) Destroy(toast);
    }
}
