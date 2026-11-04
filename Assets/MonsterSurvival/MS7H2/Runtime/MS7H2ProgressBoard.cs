using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Separate, additive UI. Does NOT replace the customized 7H.1.1 Quest Board or Menu V3.
[DisallowMultipleComponent]
public sealed class MS7H2ProgressBoard : MonoBehaviour
{
    private Canvas canvas;
    private GameObject overlay;
    private TMP_Text subtitle;
    private TMP_Text feedback;
    private TMP_Text[] labels = new TMP_Text[3];
    private Image[] fills = new Image[3];
    private Button[] claimButtons = new Button[3];
    private TMP_Text[] claimCaptions = new TMP_Text[3];
    private RectTransform canvasRect;
    private RectTransform panelRect;
    private float layoutWidth = -1f;
    private float layoutHeight = -1f;
    private int capturedSlot;
    private float nextRefresh;
    private static readonly Color Ink = new Color(.085f,.057f,.145f,.98f);
    private static readonly Color Card = new Color(.17f,.12f,.24f,1f);
    private static readonly Color Gold = new Color(.92f,.74f,.43f,1f);
    private static readonly Color Lilac = new Color(.86f,.79f,.95f,1f);

    private void Start()
    {
        if (SceneManager.GetActiveScene().name != "MainMenu") { enabled = false; return; }
        BuildUI();
    }
    private void Update()
    {
        if (!canvas) return;
        ApplyResponsivePanelLayout();
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .65f;
        if (overlay && overlay.activeSelf)
        {
            if (capturedSlot != MS5A2GoldBank.SelectedSlot)
            {
                feedback.text = "Profile changed; close and reopen PROGRESS before claiming.";
                for (int i = 0; i < claimButtons.Length; i++) claimButtons[i].interactable = false;
            }
            else Refresh();
        }
    }
    private static GameObject Make(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = 5;
        return go;
    }
    private static RectTransform Rect(GameObject go, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var r = (RectTransform)go.transform;
        r.anchorMin = anchor; r.anchorMax = anchor; r.pivot = pivot;
        r.anchoredPosition = pos; r.sizeDelta = size;
        return r;
    }
    private static void Stretch(GameObject go)
    {
        var r = (RectTransform)go.transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.pivot = new Vector2(.5f,.5f); r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }
    private static Image Background(GameObject go, Color color, bool clickable)
    {
        var im = go.AddComponent<Image>(); im.color = color; im.raycastTarget = clickable; return im;
    }
    private static TMP_Text Write(GameObject go, string value, float size, Color color, TextAlignmentOptions align)
    {
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = value; t.fontSize = size; t.fontSizeMin = size * .7f;
        t.enableAutoSizing = true; t.color = color; t.alignment = align;
        t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }
    private Button Button(Transform parent, string name, string caption, Vector2 anchor, Vector2 pivot,
        Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        var go = Make(name,parent); Rect(go,anchor,pivot,pos,size);
        var im = Background(go,Card,true);
        var b = go.AddComponent<Button>(); b.targetGraphic=im; b.onClick.AddListener(action);
        var label = Make("Label",go.transform); Stretch(label);
        Write(label,caption,caption == "X" ? 22f : 19f,Gold,TextAlignmentOptions.Center);
        return b;
    }
    private void BuildUI()
    {
        var root = Make("7H2 Progressive Quest Canvas [runtime]", transform);
        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasRect = root.GetComponent<RectTransform>();
        canvas.sortingOrder = 249; // 7H1 overlay (250) wins when user opens old QUESTS
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;
        root.AddComponent<GraphicRaycaster>();

        // The existing QUESTS launcher sits at right -36, bottom 26 (width 176).
        // PROGRESS sits immediately to its left, both BELOW the magic circle.
        Button(root.transform,"Progress Launcher","PROGRESS",new Vector2(1,0),new Vector2(1,0),
            new Vector2(-226,26),new Vector2(178,48),Toggle);

        overlay = Make("Progress Overlay",root.transform);
        Stretch(overlay); Background(overlay,new Color(0f,0f,0f,.65f),true);
        var panel = Make("Progress Panel",overlay.transform);
        panelRect = Rect(panel,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(780,560));
        Background(panel,Ink,true);
        var title = Make("Title",panel.transform);
        Rect(title,new Vector2(.5f,1f),new Vector2(.5f,1f),new Vector2(0,-17),new Vector2(675,48));
        Write(title,"PROGRESSIVE QUESTS",34,Gold,TextAlignmentOptions.Center);
        var sub = Make("Profile",panel.transform);
        Rect(sub,new Vector2(.5f,1f),new Vector2(.5f,1f),new Vector2(0,-75),new Vector2(690,38));
        subtitle = Write(sub,"Loading profile...",19,Lilac,TextAlignmentOptions.Center);
        Button(panel.transform,"Close","X",new Vector2(1,1),new Vector2(1,1),
            new Vector2(-12,-11),new Vector2(40,38),Toggle);
        for (int i=0; i<3; i++)
        {
            int index=i;
            var card=Make("Milestone_"+i,panel.transform);
            Rect(card,new Vector2(.5f,1f),new Vector2(.5f,1f),new Vector2(0,-132-i*113),new Vector2(704,100));
            Background(card,Card,false);
            var heading=Make("Heading",card.transform);
            Rect(heading,new Vector2(0,1),new Vector2(0,1),new Vector2(18,-8),new Vector2(500,28));
            Write(heading,MS7H2ProgressService.Title((MS7H2QuestId)i),22,Gold,TextAlignmentOptions.MidlineLeft);
            var desc=Make("Description",card.transform);
            Rect(desc,new Vector2(0,1),new Vector2(0,1),new Vector2(18,-37),new Vector2(504,27));
            Write(desc,MS7H2ProgressService.Description((MS7H2QuestId)i),17,Lilac,TextAlignmentOptions.MidlineLeft);
            var bar=Make("Progress Track",card.transform);
            Rect(bar,new Vector2(0,0),new Vector2(0,0),new Vector2(18,6),new Vector2(496,7));
            Background(bar,new Color(.27f,.22f,.35f,1f),false);
            var fill=Make("Progress Fill",bar.transform); Stretch(fill);
            fills[i]=Background(fill,Gold,false);
            var status=Make("Status",card.transform);
            Rect(status,new Vector2(0,0),new Vector2(0,0),new Vector2(18,17),new Vector2(495,18));
            labels[i]=Write(status,"0 / ...",16,Lilac,TextAlignmentOptions.MidlineLeft);
            claimButtons[i]=Button(card.transform,"Claim","CLAIM",new Vector2(1,.5f),new Vector2(1,.5f),
                new Vector2(-12,0),new Vector2(144,44),()=>Claim(index));
            claimCaptions[i] = claimButtons[i].GetComponentInChildren<TMP_Text>(true);
        }
        var foot=Make("Feedback",panel.transform);
        Rect(foot,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,17),new Vector2(700,32));
        feedback=Write(foot,"Progress counts saved Victories, not Gold spending or quest claims.",16,Lilac,TextAlignmentOptions.Center);
        ApplyResponsivePanelLayout();
        overlay.SetActive(false);
    }
    // Only fit the visual panel. All 7H.2 progress/reward code stays untouched.
    // CanvasScaler converts the screen into reference-resolution units.
    private void ApplyResponsivePanelLayout()
    {
        if (canvasRect == null || panelRect == null) return;
        float width = canvasRect.rect.width;
        float height = canvasRect.rect.height;
        if (width <= 0f || height <= 0f) return;
        if (Mathf.Abs(layoutWidth - width) < .5f && Mathf.Abs(layoutHeight - height) < .5f) return;
        layoutWidth = width;
        layoutHeight = height;
        const float panelWidth = 780f;
        const float panelHeight = 560f;
        float fit = Mathf.Min(1f,
            Mathf.Min(Mathf.Max(0.1f, (width - 48f) / panelWidth),
                      Mathf.Max(0.1f, (height - 44f) / panelHeight)));
        panelRect.localScale = Vector3.one * fit;
    }

    private void Toggle()
    {
        if (!overlay) return;
        bool opening = !overlay.activeSelf;
        overlay.SetActive(opening);
        if (opening)
        {
            capturedSlot = MS5A2GoldBank.SelectedSlot;
            feedback.text = "Only fully completed milestones can be claimed once per Profile.";
            Refresh();
        }
    }
    private void Claim(int index)
    {
        if (capturedSlot != MS5A2GoldBank.SelectedSlot)
        { feedback.text = "Profile changed; reopen board."; return; }
        string message;
        MS7H2ProgressService.TryClaim(capturedSlot,(MS7H2QuestId)index,out message);
        feedback.text=message;
        Refresh();
    }
    private void Refresh()
    {
        if (!overlay || !overlay.activeSelf || capturedSlot != MS5A2GoldBank.SelectedSlot) return;
        var snap=MS7H2ProgressService.ReadSnapshot(capturedSlot);
        subtitle.text="PROFILE "+capturedSlot.ToString("00")+"   BANK GOLD "+(snap.valid ? snap.bankGold.ToString():"—")+"   "+snap.message;
        for (int i=0;i<3;i++)
        {
            var q=snap.quests[i];
            labels[i].text=q.claimed ? "CLAIMED  "+q.current+" / "+q.target :
                q.completed ? "READY  +"+q.reward+" Gold" :
                q.current+" / "+q.target+"    •    +"+q.reward+" Gold";
            labels[i].color=q.completed ? Gold : Lilac;
            var rect=fills[i].rectTransform;
            rect.anchorMin=Vector2.zero;
            rect.anchorMax=new Vector2(Mathf.Clamp01((float)q.current/q.target),1f);
            rect.offsetMin=Vector2.zero; rect.offsetMax=Vector2.zero;
            bool canClaim = snap.valid && !snap.backupRecovery && q.completed && !q.claimed;
            claimButtons[i].interactable = canClaim;
            // Clear disabled labels avoid looking like available rewards.
            if (claimCaptions[i] != null)
            {
                claimCaptions[i].text = q.claimed ? "CLAIMED" : canClaim ? "CLAIM" : "LOCKED";
                claimCaptions[i].color = canClaim ? Gold : new Color(.62f,.57f,.69f,1f);
            }
        }
    }
}
