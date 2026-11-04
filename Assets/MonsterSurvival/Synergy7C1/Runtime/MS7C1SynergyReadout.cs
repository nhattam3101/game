using UnityEngine;
using UnityEngine.UI;
using TMPro;

// OPTIONAL independent readout. Does not replace the existing J = SKILL SYNERGY panel.
// Uses separate button "NEW COMBOS". Never makes gameplay or Save changes.
[DisallowMultipleComponent]
public sealed class MS7C1SynergyReadout : MonoBehaviour
{
    public MS7C1SynergyCore core;
    public KeyCode toggleKey = KeyCode.L;
    private GameObject overlay;
    private TextMeshProUGUI summary;
    private TextMeshProUGUI[] rows;
    private float nextUpdate;
    private readonly Color ink = new Color(.94f,.91f,.98f,1f);
    private readonly Color accent = new Color(.96f,.75f,.42f,1f);

    private void Awake()
    {
        if (core == null) core = FindFirstObjectByType<MS7C1SynergyCore>();
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (canvas == null) { Debug.LogWarning("[7C.1] UI must be attached to an existing gameplay Canvas.", this); enabled = false; return; }
        Build(canvas.transform);
    }

    private void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(toggleKey)) Toggle();
#endif
        if (overlay == null || !overlay.activeSelf) return;
        if (IsLevelUpOpen()) { overlay.SetActive(false); return; }
        if (Time.unscaledTime < nextUpdate) return;
        nextUpdate = Time.unscaledTime + .25f;
        UpdateText();
    }

    private bool IsLevelUpOpen()
    {
        return core != null && core.skillManager != null && core.skillManager.skillPanel != null &&
               core.skillManager.skillPanel.activeInHierarchy;
    }

    public void Toggle()
    {
        if (overlay == null) return;
        bool next = !overlay.activeSelf;
        if (next && IsLevelUpOpen()) return;
        if (next && core == null) core = FindFirstObjectByType<MS7C1SynergyCore>();
        overlay.SetActive(next);
        if (next) { UpdateText(); overlay.transform.SetAsLastSibling(); }
    }

    private void UpdateText()
    {
        if (core == null) { summary.text = "Player / Synergy Core not found"; return; }
        core.RefreshNow(false);
        summary.text = core.UnlockedCount + "/3 NEW COMBOS UNLOCKED  |  OLD 3: VIEW [J]";
        SetRow(0, "WILDFIRE CONDUIT", "Fire Ammo + Lightning Ammo", core.WildfireConduitUnlocked, core.IsPreferredElementEquipped(MS7C1SynergyCore.WildfireId));
        SetRow(1, "GLACIAL REBOUND", "Ice Ammo + Ricochet Shot", core.GlacialReboundUnlocked, core.IsPreferredElementEquipped(MS7C1SynergyCore.GlacialId));
        SetRow(2, "THUNDER PRECISION", "Lightning Ammo + Critical Hit", core.ThunderPrecisionUnlocked, core.IsPreferredElementEquipped(MS7C1SynergyCore.ThunderId));
    }

    private void SetRow(int index, string title, string condition, bool unlocked, bool preferredEquipped)
    {
        if (rows == null || index >= rows.Length || rows[index] == null) return;
        // Do not label as ACTIVE: special combat effects belong to 7C.2.
        string status = unlocked ? "UNLOCKED" : "LOCKED";
        string equipped = unlocked && preferredEquipped ? " | ELEMENT READY" : "";
        rows[index].text = title + "  [" + status + "]" + equipped + "\n" + condition;
        rows[index].color = unlocked ? ink : new Color(.58f,.55f,.66f,1f);
    }

    private void Build(Transform canvas)
    {
        RectTransform buttonRoot = Create(canvas, "MS7C1_NewCombosButton", new Vector2(1,0),new Vector2(1,0),new Vector2(1,0),new Vector2(-24,80),new Vector2(165,40));
        Image bg = buttonRoot.gameObject.AddComponent<Image>(); bg.color = new Color(.28f,.20f,.47f,.97f);
        Button button = buttonRoot.gameObject.AddComponent<Button>(); button.targetGraphic = bg; button.onClick.AddListener(Toggle);
        MakeText(buttonRoot,"ButtonText","NEW COMBOS [L]",14f,TextAlignmentOptions.Center,ink);

        RectTransform full = Create(canvas,"MS7C1_NewCombosOverlay",Vector2.zero,Vector2.one,new Vector2(.5f,.5f),Vector2.zero,Vector2.zero);
        Image dim = full.gameObject.AddComponent<Image>(); dim.color = new Color(.02f,.015f,.05f,.82f);
        Button closeOutside = full.gameObject.AddComponent<Button>(); closeOutside.targetGraphic = dim; closeOutside.onClick.AddListener(Toggle);
        overlay = full.gameObject;
        RectTransform box = Create(full,"Panel",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(650,450));
        Image panel = box.gameObject.AddComponent<Image>(); panel.color = new Color(.09f,.065f,.16f,.99f);
        Outline outline = box.gameObject.AddComponent<Outline>(); outline.effectColor = accent; outline.effectDistance = new Vector2(2,-2);
        TextMeshProUGUI header = MakeText(box,"Title","NEW SKILL SYNERGIES · 7C.1",25,TextAlignmentOptions.Center,accent);
        Position(header.rectTransform,new Vector2(0,1),new Vector2(1,1),new Vector2(.5f,1),new Vector2(0,-16),new Vector2(-40,42));
        summary = MakeText(box,"Summary","",15,TextAlignmentOptions.Center,ink);
        Position(summary.rectTransform,new Vector2(0,1),new Vector2(1,1),new Vector2(.5f,1),new Vector2(0,-67),new Vector2(-40,27));
        rows = new TextMeshProUGUI[3];
        for (int i=0; i<3;i++)
        {
            RectTransform card = Create(box,"Card_"+i,new Vector2(0,1),new Vector2(1,1),new Vector2(.5f,1),new Vector2(0,-112-i*91),new Vector2(-44,79));
            Image cb = card.gameObject.AddComponent<Image>(); cb.color = new Color(.18f,.13f,.27f,.96f);
            TextMeshProUGUI t = MakeText(card,"Requirements","",17,TextAlignmentOptions.Left,ink);
            Position(t.rectTransform,Vector2.zero,Vector2.one,new Vector2(.5f,.5f),new Vector2(16,0),new Vector2(-30,-12));
            rows[i] = t;
        }
        TextMeshProUGUI footer = MakeText(box,"Footer","7C.1 checks unlock conditions only. Combat bonuses will arrive in 7C.2.",12,TextAlignmentOptions.Center,accent);
        Position(footer.rectTransform,new Vector2(0,0),new Vector2(1,0),new Vector2(.5f,0),new Vector2(0,15),new Vector2(-30,33));
        RectTransform close = Create(box,"Close",Vector2.one,Vector2.one,Vector2.one,new Vector2(-10,-10),new Vector2(38,32));
        Image closeBg = close.gameObject.AddComponent<Image>(); closeBg.color = new Color(.34f,.24f,.40f,1);
        Button closeBtn = close.gameObject.AddComponent<Button>(); closeBtn.targetGraphic = closeBg; closeBtn.onClick.AddListener(Toggle);
        MakeText(close,"X","X",18,TextAlignmentOptions.Center,ink);
        overlay.SetActive(false);
    }

    private static RectTransform Create(Transform parent,string name,Vector2 min,Vector2 max,Vector2 pivot,Vector2 position,Vector2 size)
    {
        GameObject go = new GameObject(name,typeof(RectTransform));
        RectTransform r = go.GetComponent<RectTransform>(); r.SetParent(parent,false); Position(r,min,max,pivot,position,size); return r;
    }
    private static void Position(RectTransform r,Vector2 min,Vector2 max,Vector2 pivot,Vector2 pos,Vector2 size)
    { r.anchorMin=min; r.anchorMax=max; r.pivot=pivot; r.anchoredPosition=pos; r.sizeDelta=size; }
    private static TextMeshProUGUI MakeText(Transform parent,string name,string content,float size,TextAlignmentOptions alignment,Color color)
    {
        RectTransform r = Create(parent,name,Vector2.zero,Vector2.one,new Vector2(.5f,.5f),Vector2.zero,Vector2.zero);
        TextMeshProUGUI t=r.gameObject.AddComponent<TextMeshProUGUI>();
        t.text=content;t.fontSize=size;t.color=color;t.alignment=alignment;t.textWrappingMode=TextWrappingModes.Normal;
        t.raycastTarget=false;return t;
    }

    private void OnDestroy()
    {
        // Transient UI created at runtime, destroy only 7C.1-owned children.
        if (overlay != null) Destroy(overlay);
        Transform canvas = GetComponent<Canvas>() != null ? transform : transform.parent;
        if (canvas != null)
        {
            Transform button = canvas.Find("MS7C1_NewCombosButton");
            if (button != null) Destroy(button.gameObject);
        }
    }
}
