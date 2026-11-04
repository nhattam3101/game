using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Optional independent V3 overlay. Does not alter Main Menu navigation, gameplay, or SaveService.
[DisallowMultipleComponent]
public sealed class MS5A4SaveSlotsUI : MonoBehaviour
{
    public Button openButton;
    public MS5A2ProfileWidget profileWidget;

    private GameObject modal;
    private GameObject confirmation;
    private TMP_Text confirmText;
    private TMP_Text feedback;
    private readonly TMP_Text[] details = new TMP_Text[3];
    private readonly Image[] rowBackgrounds = new Image[3];
    private readonly Button[] chooseButtons = new Button[3];
    private readonly Button[] archiveButtons = new Button[3];
    private readonly Button[] restoreButtons = new Button[3];
    private int pendingRestoreSlot;

    private static readonly Color Bg = new Color(.035f,.035f,.085f,.985f);
    private static readonly Color Purple = new Color(.50f,.34f,.71f,1f);
    private static readonly Color Gold = new Color(.96f,.73f,.40f,1f);
    private static readonly Color White = new Color(.94f,.91f,1f,1f);
    private static readonly Color Muted = new Color(.72f,.70f,.81f,1f);

    private void Awake()
    {
        BuildVisuals();
        if (openButton) openButton.onClick.AddListener(Open);
        else Debug.LogWarning("[5A.4] No Open Save Manager button linked.", this);
    }
    private void OnDestroy()
    {
        if (openButton) openButton.onClick.RemoveListener(Open);
    }
    private void Update()
    {
        if (modal != null && modal.activeSelf && Input.GetKeyDown(KeyCode.Escape)) Close();
    }
    public void Open()
    {
        if (modal == null) BuildVisuals();
        if (modal == null) return;
        modal.SetActive(true);
        confirmation.SetActive(false);
        feedback.text = "Read-only inspection. Archive and restore require explicit clicks.";
        Refresh();
    }
    public void Close()
    {
        if (confirmation) confirmation.SetActive(false);
        if (modal) modal.SetActive(false);
    }
    private void Refresh()
    {
        for (int i = 0; i < 3; i++)
        {
            int slot = i + 1;
            MS5A4SaveRecovery.Report report = MS5A4SaveRecovery.InspectSlot(slot);
            bool primaryValid = report.primaryState == MS5A4SaveRecovery.FileState.Valid;
            bool newSlot = report.primaryState == MS5A4SaveRecovery.FileState.Missing &&
                           report.backupState == MS5A4SaveRecovery.FileState.Missing;
            bool selected = MS5A2GoldBank.SelectedSlot == slot;
            rowBackgrounds[i].color = selected ? new Color(.16f,.115f,.24f,1f) : new Color(.09f,.075f,.15f,1f);
            MS5A1SaveData data = primaryValid ? report.primary : report.backup;
            string state = primaryValid ? "SAVED" : newSlot ? "NEW PROFILE" :
                report.CanRestore ? "BACKUP AVAILABLE" : "SAVE INVALID";
            string backupState = report.backupState == MS5A4SaveRecovery.FileState.Valid
                ? "Backup OK · rev " + report.backup.revision : "Backup: " + report.backupState;
            string gold = data != null ? data.bankGold.ToString("N0") : "0";
            int nItems = MS5A4SaveRecovery.ItemCount(data);
            int nGear = MS5A4SaveRecovery.GearCount(data);
            string items = nItems < 0 ? "--" : nItems.ToString();
            string gear = nGear < 0 ? "--" : nGear.ToString();
            details[i].text = "<b>PROFILE " + slot.ToString("00") + "</b>  " + state + (selected ? "  <color=#F3C778>● ACTIVE</color>" : "") +
                "\nGold: " + gold + "   |   Inv: " + items + "/20   |   Gear: " + gear + "/3" +
                "\nLast save: " + (data == null ? "Never" : MS5A4SaveRecovery.FormatTime(data.lastSavedUtc)) +
                "   |   Rev: " + (data == null ? "0" : data.revision.ToString()) +
                "\n" + backupState;
            chooseButtons[i].interactable = primaryValid || newSlot;
            archiveButtons[i].interactable = primaryValid;
            restoreButtons[i].interactable = report.CanRestore;
        }
    }
    private void Choose(int slot)
    {
        MS5A4SaveRecovery.Report r = MS5A4SaveRecovery.InspectSlot(slot);
        if (r.primaryState != MS5A4SaveRecovery.FileState.Valid &&
            !(r.primaryState == MS5A4SaveRecovery.FileState.Missing && r.backupState == MS5A4SaveRecovery.FileState.Missing))
        { feedback.text = "Invalid profile: restore a verified backup first."; Refresh(); return; }
        MS5A2GoldBank.SelectSlot(slot);
        if (profileWidget) profileWidget.Refresh();
        feedback.text = "Selected Profile " + slot.ToString("00") + ".";
        Refresh();
    }
    private void Archive(int slot)
    {
        if (SceneManager.GetActiveScene().name != "MainMenu") { feedback.text = "Available only in MainMenu."; return; }
        string message;
        bool ok = MS5A4SaveRecovery.TryArchiveSlot(slot, out message);
        feedback.text = ok ? "Manual archive created (see Console for full path)." : message;
        if (ok) Debug.Log("[5A.4] " + message);
        Refresh();
    }
    private void AskRestore(int slot)
    {
        if (SceneManager.GetActiveScene().name != "MainMenu") { feedback.text = "Available only in MainMenu."; return; }
        MS5A4SaveRecovery.Report report = MS5A4SaveRecovery.InspectSlot(slot);
        if (!report.CanRestore) { feedback.text = "No verified backup available."; Refresh(); return; }
        pendingRestoreSlot = slot;
        confirmText.text = "RESTORE PROFILE " + slot.ToString("00") + "?\n\n" +
            "Verified backup revision " + report.backup.revision + " will replace the active save. " +
            "Recent progress may be LOST. Current primary file will be archived first. " +
            "Existing .bak will not be overwritten.\n\nThis cannot be undone in-game.";
        confirmation.SetActive(true);
    }
    private void ConfirmRestore()
    {
        int slot = pendingRestoreSlot;
        confirmation.SetActive(false);
        if (SceneManager.GetActiveScene().name != "MainMenu") { feedback.text = "Only available in MainMenu."; return; }
        string message;
        bool ok = MS5A4SaveRecovery.TryRestoreSlotBackup(slot, out message);
        feedback.text = ok ? "Profile " + slot + " restored. See Console for archive path." : message;
        if (ok) Debug.Log("[5A.4] " + message); else Debug.LogWarning("[5A.4] " + message);
        if (profileWidget) profileWidget.Refresh();
        Refresh();
    }
    private void BuildVisuals()
    {
        if (modal != null) return;
        RectTransform root = transform as RectTransform;
        if (!root) { Debug.LogError("[5A.4] Attach to UI RectTransform."); return; }
        RectTransform shade = Rect("MS5A4_Modal",root,Vector2.zero,Vector2.one);
        modal = shade.gameObject;
        Image dim = shade.gameObject.AddComponent<Image>(); dim.color = new Color(.012f,.008f,.028f,.88f); dim.raycastTarget = true;
        RectTransform panel = Rect("Window",shade,new Vector2(.15f,.09f),new Vector2(.85f,.91f));
        Image panelImage = panel.gameObject.AddComponent<Image>(); panelImage.color = Bg; panelImage.raycastTarget = true;
        MakeImage("TopLine",panel,new Vector2(.015f,.965f),new Vector2(.985f,.971f),Gold);
        Label("SAVE PROFILES",panel,new Vector2(.055f,.88f),new Vector2(.75f,.965f),42,White,TextAlignmentOptions.Left);
        Label("Verified JSON / SHA-256 · Restore requires confirmation",panel,new Vector2(.055f,.835f),new Vector2(.85f,.885f),20,Muted,TextAlignmentOptions.Left);
        Button close = MakeButton("BTN_Close",panel,"X",new Vector2(.91f,.895f),new Vector2(.97f,.96f),new Color(.24f,.15f,.33f,1f));
        close.onClick.AddListener(Close);
        for (int i = 0; i < 3; i++)
        {
            int capturedSlot = i+1;
            float top = .815f-i*.205f;
            RectTransform row = Rect("Profile_"+capturedSlot,panel,new Vector2(.045f,top-.187f),new Vector2(.955f,top));
            Image rowImage = row.gameObject.AddComponent<Image>();rowImage.color = new Color(.09f,.075f,.15f,1f);rowImage.raycastTarget=false;
            rowBackgrounds[i] = rowImage;
            details[i] = Label("",row,new Vector2(.025f,.07f),new Vector2(.55f,.93f),21,White,TextAlignmentOptions.Left);
            chooseButtons[i] = MakeButton("BTN_Select",row,"SELECT",new Vector2(.555f,.30f),new Vector2(.675f,.69f),new Color(.35f,.22f,.53f,1f));
            archiveButtons[i] = MakeButton("BTN_Archive",row,"ARCHIVE",new Vector2(.686f,.30f),new Vector2(.83f,.69f),new Color(.17f,.26f,.35f,1f));
            restoreButtons[i] = MakeButton("BTN_Restore",row,"RESTORE",new Vector2(.84f,.30f),new Vector2(.982f,.69f),new Color(.40f,.25f,.22f,1f));
            chooseButtons[i].onClick.AddListener(()=>Choose(capturedSlot));
            archiveButtons[i].onClick.AddListener(()=>Archive(capturedSlot));
            restoreButtons[i].onClick.AddListener(()=>AskRestore(capturedSlot));
        }
        feedback = Label("",panel,new Vector2(.05f,.07f),new Vector2(.95f,.19f),20,Gold,TextAlignmentOptions.Center);
        Label("Manual history: Application.persistentDataPath/MonsterSurvival/SaveData/5A4_ManualHistory",
            panel,new Vector2(.05f,.014f),new Vector2(.95f,.08f),16,Muted,TextAlignmentOptions.Center);
        RectTransform confirm = Rect("ConfirmRestore",panel,Vector2.zero,Vector2.one);
        confirmation = confirm.gameObject;
        Image confirmBg = confirm.gameObject.AddComponent<Image>();confirmBg.color = new Color(.01f,.012f,.03f,.97f);confirmBg.raycastTarget = true;
        RectTransform confirmCard = Rect("ConfirmCard",confirm,new Vector2(.19f,.29f),new Vector2(.81f,.76f));
        MakeImage("ConfirmPanel",confirmCard,Vector2.zero,Vector2.one,new Color(.11f,.07f,.19f,1f));
        confirmText = Label("",confirmCard,new Vector2(.07f,.28f),new Vector2(.93f,.92f),24,White,TextAlignmentOptions.Center);
        Button cancel = MakeButton("BTN_Cancel",confirmCard,"CANCEL",new Vector2(.09f,.07f),new Vector2(.47f,.26f),new Color(.29f,.26f,.36f,1f));
        Button approve = MakeButton("BTN_Confirm",confirmCard,"RESTORE NOW",new Vector2(.53f,.07f),new Vector2(.91f,.26f),new Color(.55f,.28f,.19f,1f));
        cancel.onClick.AddListener(()=>confirmation.SetActive(false));
        approve.onClick.AddListener(ConfirmRestore);
        confirmation.SetActive(false);
        modal.SetActive(false);
    }
    private static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
    {
        GameObject go = new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);
        RectTransform rt=go.GetComponent<RectTransform>();rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=rt.offsetMax=Vector2.zero;return rt;
    }
    private static Image MakeImage(string name,Transform parent,Vector2 min,Vector2 max,Color color)
    {
        RectTransform rt=Rect(name,parent,min,max);Image im=rt.gameObject.AddComponent<Image>();im.color=color;im.raycastTarget=false;return im;
    }
    private static TMP_Text Label(string content,Transform parent,Vector2 min,Vector2 max,int fontSize,Color color,TextAlignmentOptions alignment)
    {
        RectTransform rt=Rect("Text",parent,min,max);TextMeshProUGUI t=rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text=content;t.fontSize=fontSize;t.color=color;t.alignment=alignment;t.raycastTarget=false;
        t.enableAutoSizing=true;t.fontSizeMin=Mathf.Max(12,fontSize/2);t.fontSizeMax=fontSize;
        if(TMP_Settings.defaultFontAsset)t.font=TMP_Settings.defaultFontAsset;return t;
    }
    private static Button MakeButton(string name,Transform parent,string content,Vector2 min,Vector2 max,Color color)
    {
        RectTransform rt=Rect(name,parent,min,max);Image im=rt.gameObject.AddComponent<Image>();im.color=color;im.raycastTarget=true;
        Button button=rt.gameObject.AddComponent<Button>();button.targetGraphic=im;
        var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1f,.88f,1f,1f);
        colors.pressedColor=new Color(.68f,.53f,.8f,1f);colors.disabledColor=new Color(.52f,.48f,.55f,.38f);button.colors=colors;
        Label(content,rt,Vector2.zero,Vector2.one,22,White,TextAlignmentOptions.Center);
        return button;
    }
}
