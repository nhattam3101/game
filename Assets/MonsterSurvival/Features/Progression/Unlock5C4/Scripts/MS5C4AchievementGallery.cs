using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 5C.4: Read-only gallery for unlocks already saved by 5C.1 / 5C.3.
// NO purchases, achievements, gold or inventory edits are performed here.
[DisallowMultipleComponent]
public sealed class MS5C4AchievementGallery : MonoBehaviour
{
    [Header("Assigned by 5C.4 Installer")]
    public Button openButton;
    public MS5A2ProfileWidget profileWidget;
    public Sprite titanBadge;
    public Sprite wardenBadge;
    public Sprite equipmentBadge;

    private GameObject page;
    private CanvasGroup fade;
    private TMP_Text summary;
    private TMP_Text status;
    private TMP_Text[] owned = new TMP_Text[4];
    private Image[] accents = new Image[4];
    private bool visible;
    private int previousSlot = -1;
    private float nextRefresh;
    private static readonly Color Gold = new Color(.94f,.73f,.42f,1f);
    private static readonly Color Pale = new Color(.94f,.91f,.99f,1f);
    private static readonly Color Muted = new Color(.72f,.67f,.81f,1f);
    private static readonly Color Teal = new Color(.49f,.91f,.76f,1f);

    private void Start()
    {
        if (!openButton || !GetComponentInParent<Canvas>())
        {
            Debug.LogError("[5C.4] Missing launcher or Canvas. Install from Editor menu.", this);
            enabled = false;
            return;
        }
        Build();
        openButton.onClick.AddListener(Open);
    }

    private void Update()
    {
        if (!page) return;
        if (visible && Input.GetKeyDown(KeyCode.Escape)) Close();
        if (visible && openButton && !openButton.gameObject.activeInHierarchy) Close();
        if (visible && (previousSlot != MS5A2GoldBank.SelectedSlot || Time.unscaledTime >= nextRefresh))
            Refresh();
        if (page.activeSelf)
        {
            fade.alpha = Mathf.MoveTowards(fade.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * 7f);
            if (!visible && fade.alpha <= .001f) page.SetActive(false);
            for (int i = 0; i < accents.Length; i++)
            {
                if (!accents[i]) continue;
                Color c = accents[i].color;
                if (owned[i] && owned[i].text == "UNLOCKED") c.a = .7f + .25f * Mathf.Sin(Time.unscaledTime * 2.2f + i);
                else c.a = .35f;
                accents[i].color = c;
            }
        }
    }

    private void Open()
    {
        if (!page) return;
        page.transform.SetAsLastSibling();
        page.SetActive(true);
        fade.alpha = 0f;
        visible = true;
        Refresh();
    }
    private void Close() { visible = false; }

    public void Refresh()
    {
        int slot = MS5A2GoldBank.SelectedSlot;
        previousSlot = slot;
        nextRefresh = Time.unscaledTime + .6f;
        MS5C1UnlockService.Snapshot snapshot;
        string reason;
        if (!MS5C1UnlockService.TryGetSnapshot(slot, out snapshot, out reason))
        {
            if (summary) summary.text = "PROFILE " + slot.ToString("00") + "   /   SAVE UNAVAILABLE";
            if (status) status.text = "READ ONLY  ·  " + reason;
            for (int i = 0; i < owned.Length; i++) SetOwned(i, false);
            return;
        }
        string[] ids = {
            "achievement.shadow_titan", "achievement.grave_warden",
            "equipment.reaper_edge", "equipment.wardens_oath"
        };
        int achievementCount = 0, licenseCount = 0;
        for (int i = 0; i < ids.Length; i++)
        {
            bool has = snapshot.IsUnlocked(ids[i]);
            SetOwned(i, has);
            if (has) { if (i < 2) achievementCount++; else licenseCount++; }
        }
        int maps = 0;
        if (snapshot.IsUnlocked("map.green_meadow")) maps++;
        if (snapshot.IsUnlocked("map.sand_dunes")) maps++;
        if (snapshot.IsUnlocked("map.ancient_ruins")) maps++;
        if (summary) summary.text = "PROFILE " + slot.ToString("00") +
            "   ·   BOSS " + achievementCount + "/2   ·   MAPS " + maps + "/3   ·   GEAR LICENSES " + licenseCount + "/2";
        if (status) status.text = snapshot.fromBackup ? "BACKUP ONLY  ·  DISPLAY MODE" :
            snapshot.isNew ? "NEW PROFILE  ·  NO SAVED ACHIEVEMENTS" :
            "READ ONLY  ·  BADGES SHOW SAVED PROGRESS";
    }

    private void SetOwned(int index, bool has)
    {
        if (!owned[index]) return;
        owned[index].text = has ? "UNLOCKED" : "LOCKED";
        owned[index].color = has ? Teal : Muted;
        if (accents[index]) accents[index].color = new Color(
            has ? .78f : .32f, has ? .52f : .22f, has ? .98f : .48f, .5f);
    }

    private void Build()
    {
        RectTransform root = transform as RectTransform;
        page = Rect("MS5C4_GalleryScreen", root, Vector2.zero, Vector2.one).gameObject;
        fade = page.AddComponent<CanvasGroup>();
        fade.alpha = 0f;
        Surface("Dim", page.transform, Vector2.zero, Vector2.one, new Color(.014f,.012f,.035f,.96f), true);
        RectTransform panel = Rect("GalleryPanel", page.transform, new Vector2(.145f,.105f), new Vector2(.855f,.895f));
        Surface("PanelBg",panel,Vector2.zero,Vector2.one,new Color(.073f,.054f,.135f,.98f));
        Surface("GoldTop",panel,new Vector2(.022f,.97f),new Vector2(.978f,.977f),Gold);
        Label("ACHIEVEMENTS",panel,new Vector2(.045f,.873f),new Vector2(.79f,.963f),52,Pale,TextAlignmentOptions.Left);
        Label("BOSS TROPHIES  /  UNLOCKED CONTENT",panel,new Vector2(.047f,.82f),new Vector2(.79f,.88f),20,Gold,TextAlignmentOptions.Left);
        Button close = MakeButton("Close",panel,"X",new Vector2(.915f,.887f),new Vector2(.967f,.953f));
        close.onClick.AddListener(Close);
        summary = Label("PROFILE",panel,new Vector2(.04f,.752f),new Vector2(.96f,.82f),21,Pale,TextAlignmentOptions.Center);
        string[] titles={"SHADOW TITAN", "GRAVE WARDEN", "REAPER EDGE", "WARDEN'S OATH"};
        string[] subtitles={"Boss trophy  ·  Defeat Shadow Titan", "Boss trophy  ·  Defeat Grave Warden",
                           "Equipment license  ·  800 Bank Gold", "Equipment license  ·  1,100 Bank Gold"};
        string[] benefits={"Reward: TITAN SLAYER badge", "Reward: WARDEN BREAKER badge",
                           "Unlocks access; does not grant an item", "Unlocks access; does not grant an item"};
        Sprite[] sprites={titanBadge,wardenBadge,equipmentBadge,equipmentBadge};
        for(int i=0;i<4;i++)
        {
            float x0 = (i % 2 == 0) ? .045f : .515f;float x1=x0+.44f;
            float y0 = (i < 2) ? .435f : .135f;float y1=y0+.282f;
            RectTransform card=Rect("Card_"+i,panel,new Vector2(x0,y0),new Vector2(x1,y1));
            Surface("CardBg",card,Vector2.zero,Vector2.one,new Color(.14f,.103f,.226f,.97f));
            accents[i]=Surface("Accent",card,new Vector2(0,0),new Vector2(.012f,1f),new Color(.6f,.4f,.85f,.6f));
            RectTransform icon=Rect("BadgeIcon",card,new Vector2(.045f,.26f),new Vector2(.235f,.85f));
            Image image=icon.gameObject.AddComponent<Image>();image.sprite=sprites[i];image.preserveAspect=true;image.raycastTarget=false;
            Label(titles[i],card,new Vector2(.255f,.69f),new Vector2(.98f,.92f),27,Pale,TextAlignmentOptions.Left);
            Label(subtitles[i],card,new Vector2(.255f,.46f),new Vector2(.98f,.70f),16,Muted,TextAlignmentOptions.Left);
            Label(benefits[i],card,new Vector2(.255f,.22f),new Vector2(.97f,.47f),16,Gold,TextAlignmentOptions.Left);
            owned[i]=Label("LOCKED",card,new Vector2(.255f,.03f),new Vector2(.96f,.24f),17,Muted,TextAlignmentOptions.Left);
        }
        status=Label("READ ONLY",panel,new Vector2(.04f,.047f),new Vector2(.96f,.117f),19,Muted,TextAlignmentOptions.Center);
        page.SetActive(false);
    }

    private static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
    {
        GameObject go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);
        RectTransform rt=go.GetComponent<RectTransform>();rt.anchorMin=min;rt.anchorMax=max;
        rt.offsetMin=rt.offsetMax=Vector2.zero;return rt;
    }
    private static Image Surface(string name,Transform parent,Vector2 min,Vector2 max,Color color,bool blocksRaycast=false)
    {var rt=Rect(name,parent,min,max);var im=rt.gameObject.AddComponent<Image>();im.color=color;im.raycastTarget=blocksRaycast;return im;}
    private static TMP_Text Label(string value,Transform parent,Vector2 min,Vector2 max,int size,Color color,TextAlignmentOptions alignment)
    {
        var rt=Rect("TXT_"+value.Substring(0,Mathf.Min(16,value.Length)).Replace(' ','_'),parent,min,max);
        TextMeshProUGUI t=rt.gameObject.AddComponent<TextMeshProUGUI>();t.text=value;t.color=color;t.fontSize=size;
        t.fontSizeMin=12;t.fontSizeMax=size;t.enableAutoSizing=true;t.alignment=alignment;t.raycastTarget=false;
        t.textWrappingMode=TextWrappingModes.Normal;
        if(TMP_Settings.defaultFontAsset)t.font=TMP_Settings.defaultFontAsset;return t;
    }
    private static Button MakeButton(string name,Transform parent,string label,Vector2 min,Vector2 max)
    {
        RectTransform rt=Rect(name,parent,min,max);Image image=rt.gameObject.AddComponent<Image>();
        image.color=new Color(.29f,.19f,.43f,1f);image.raycastTarget=true;
        Button b=rt.gameObject.AddComponent<Button>();b.targetGraphic=image;
        ColorBlock colors=b.colors;colors.highlightedColor=new Color(1f,.81f,.96f,1f);colors.pressedColor=Gold;b.colors=colors;
        Label(label,rt,Vector2.zero,Vector2.one,20,Pale,TextAlignmentOptions.Center);
        return b;
    }
    private void OnDestroy() { if(openButton) openButton.onClick.RemoveListener(Open); }
}
