using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Extends the existing 7H2 PROGRESS pop-up IN PLACE, without any MainMenu launcher.
[DisallowMultipleComponent]
public sealed class MS7H3PeriodicQuestPanel : MonoBehaviour
{
    private Transform progressOverlay;
    private GameObject progressPanel, periodPanel;
    private RectTransform progressPanelRect, periodPanelRect;
    private Canvas rootCanvas;
    private UnityEngine.UI.Text summary, feedback;
    private UnityEngine.UI.Text[] names = new UnityEngine.UI.Text[2];
    private UnityEngine.UI.Text[] descriptions = new UnityEngine.UI.Text[2];
    private UnityEngine.UI.Text[] statusTexts = new UnityEngine.UI.Text[2];
    private Image[] progressFill = new Image[2];
    private Button[] claims = new Button[2];
    private UnityEngine.UI.Text[] claimCaptions = new UnityEngine.UI.Text[2];
    private int capturedSlot, tab;
    private float nextRefresh;
    private bool wasOpen;
    // 7H.3.2: this popup only uses the standard uGUI Text renderer as a
    // conservative fallback for Unity 6000.6 installations where runtime
    // TextMeshProUGUI produces zero visible glyphs despite a valid TMP font.
    // Original 7H.1 / 7H.2 TextMeshPro components are never touched.
    private Font uiFallbackFont;
    private readonly System.Collections.Generic.List<UnityEngine.UI.Text> generatedText =
        new System.Collections.Generic.List<UnityEngine.UI.Text>();
    private bool visibilityLogged;
    private static readonly Color Ink=new Color(.085f,.057f,.145f,.99f);
    private static readonly Color Card=new Color(.17f,.12f,.24f,1f);
    private static readonly Color Gold=new Color(.92f,.74f,.43f,1f);
    private static readonly Color Lilac=new Color(.86f,.79f,.95f,1f);

    private void Start()
    {
        if (SceneManager.GetActiveScene().name!="MainMenu") {enabled=false;return;}
        // 7H2 constructs UI in its own Start; our Update finds it once.
    }
    private void Update()
    {
        if (periodPanel==null) { TryMount();return; }
        if (progressOverlay==null || progressPanel==null) return;
        periodPanelRect.localScale=progressPanelRect.localScale; // inherit 7H2.1 responsive fit
        bool overlayOpen=progressOverlay.gameObject.activeSelf;
        if (!overlayOpen)
        {
            if (wasOpen) { periodPanel.SetActive(false);progressPanel.SetActive(true);wasOpen=false; }
            return;
        }
        wasOpen=true;
        if (!periodPanel.activeSelf) return;
        if (Time.unscaledTime<nextRefresh) return;
        nextRefresh=Time.unscaledTime+.65f;
        Refresh();
    }
    private static GameObject Make(string name,Transform parent)
    {
        var o=new GameObject(name,typeof(RectTransform));o.transform.SetParent(parent,false);o.layer=5;return o;
    }
    private static RectTransform Rect(GameObject o,Vector2 anchor,Vector2 pivot,Vector2 pos,Vector2 size)
    {
        RectTransform r=(RectTransform)o.transform;r.anchorMin=anchor;r.anchorMax=anchor;r.pivot=pivot;
        r.anchoredPosition=pos;r.sizeDelta=size;return r;
    }
    private static Image BG(GameObject o,Color color,bool rays)
    {Image img=o.AddComponent<Image>();img.color=color;img.raycastTarget=rays;return img;}
    private Font ResolveFallbackFont()
    {
        if (uiFallbackFont != null) return uiFallbackFont;
        // Unity 6 renamed the old built-in Arial font to LegacyRuntime.ttf.
        uiFallbackFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (uiFallbackFont == null)
        {
            // Fallback is only used if this editor/player has no built-in font.
            uiFallbackFont = Font.CreateDynamicFontFromOSFont("Arial", 20);
        }
        if (uiFallbackFont == null)
            Debug.LogError("[7H.3 UI] No legacy UI font available; cannot display Daily/Weekly text.",this);
        return uiFallbackFont;
    }

    private UnityEngine.UI.Text Label(GameObject o,string text,int size,Color color,TextAlignmentOptions align)
    {
        var t=o.AddComponent<UnityEngine.UI.Text>();
        t.font = ResolveFallbackFont();
        t.text = text;
        t.fontSize = size;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = Mathf.Max(11, size - 5);
        t.resizeTextMaxSize = size;
        t.supportRichText = false;
        color.a = 1f;
        t.color = color;
        t.alignment = align == TextAlignmentOptions.Center ? TextAnchor.MiddleCenter :
            align == TextAlignmentOptions.MidlineRight ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        generatedText.Add(t);
        return t;
    }
    private Button MakeButton(Transform parent,string id,string label,Vector2 anchor,Vector2 pivot,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction clicked)
    {
        GameObject o=Make(id,parent);Rect(o,anchor,pivot,pos,size);Image im=BG(o,Card,true);
        Button b=o.AddComponent<Button>();b.targetGraphic=im;b.onClick.AddListener(clicked);
        GameObject labelObj=Make("Label",o.transform);RectTransform lr=(RectTransform)labelObj.transform;
        lr.anchorMin=Vector2.zero;lr.anchorMax=Vector2.one;lr.offsetMin=Vector2.zero;lr.offsetMax=Vector2.zero;
        Label(labelObj,label,18,Gold,TextAlignmentOptions.Center);return b;
    }
    private void TryMount()
    {
        MS7H2ProgressBoard original=GetComponent<MS7H2ProgressBoard>();
        if (original==null) {enabled=false;Debug.LogWarning("[7H3] Missing 7H2 ProgressBoard",this);return;}
        Transform root=transform.Find("7H2 Progressive Quest Canvas [runtime]");
        if (!root) return;
        rootCanvas=root.GetComponent<Canvas>();
        Transform ov=root.Find("Progress Overlay");if (!ov) return;
        Transform p=ov.Find("Progress Panel");if (!p) return;
        progressOverlay=ov;progressPanel=p.gameObject;progressPanelRect=(RectTransform)p;
        // Use an independent uGUI Text renderer, since copying the 7H.2 TMP
        // font and forcing its mesh did not display any glyphs in this scene.
        ResolveFallbackFont();
        // Small tab INSIDE PROGRESS; does not change any existing MainMenu layout.
        MakeButton(p,"Daily Weekly Tab","DAILY / WEEKLY",new Vector2(1,0),new Vector2(1,0),
            new Vector2(-14,13),new Vector2(162,44),ShowPeriodic);
        // Make the existing feedback fit beside the new button.
        Transform oldFeedback=p.Find("Feedback");
        if(oldFeedback)
        { RectTransform r=(RectTransform)oldFeedback; r.sizeDelta=new Vector2(486,32);r.anchoredPosition=new Vector2(-93,17); }
        periodPanel=Make("7H3 Periodic Panel",ov);
        periodPanelRect=Rect(periodPanel,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(780,560));
        BG(periodPanel,Ink,true);
        MakeButton(periodPanel.transform,"Back","BACK TO PROGRESS",new Vector2(0,1),new Vector2(0,1),
            new Vector2(18,-14),new Vector2(193,40),BackToProgress);
        MakeButton(periodPanel.transform,"Close","X",new Vector2(1,1),new Vector2(1,1),
            new Vector2(-12,-11),new Vector2(40,38),CloseAll);
        var title=Make("Title",periodPanel.transform);Rect(title,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-55),new Vector2(670,48));
        Label(title,"DAILY / WEEKLY QUESTS",32,Gold,TextAlignmentOptions.Center);
        var sub=Make("Profile Status",periodPanel.transform);
        Rect(sub,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-114),new Vector2(704,34));
        summary=Label(sub,"Loading...",17,Lilac,TextAlignmentOptions.Center);
        MakeButton(periodPanel.transform,"Daily Tab","DAILY",new Vector2(.5f,1),new Vector2(.5f,1),
            new Vector2(-98,-161),new Vector2(170,39),()=>SwitchTab(0));
        MakeButton(periodPanel.transform,"Weekly Tab","WEEKLY",new Vector2(.5f,1),new Vector2(.5f,1),
            new Vector2(98,-161),new Vector2(170,39),()=>SwitchTab(1));
        for(int i=0;i<2;i++)
        {
            int idx=i;
            var card=Make("Period Quest "+i,periodPanel.transform);
            Rect(card,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-212-i*123),new Vector2(704,108));
            BG(card,Card,false);
            var name=Make("Quest Name",card.transform);Rect(name,new Vector2(0,1),new Vector2(0,1),new Vector2(16,-8),new Vector2(515,29));
            names[i]=Label(name,"Quest",23,Gold,TextAlignmentOptions.MidlineLeft);
            var desc=Make("Description",card.transform);Rect(desc,new Vector2(0,1),new Vector2(0,1),new Vector2(16,-36),new Vector2(515,31));
            descriptions[i]=Label(desc,"...",17,Lilac,TextAlignmentOptions.MidlineLeft);
            var bar=Make("Track",card.transform);Rect(bar,new Vector2(0,0),new Vector2(0,0),new Vector2(16,7),new Vector2(494,8));BG(bar,new Color(.26f,.21f,.34f,1f),false);
            var fill=Make("Fill",bar.transform);RectTransform fr=(RectTransform)fill.transform;
            fr.anchorMin=Vector2.zero;fr.anchorMax=Vector2.one;fr.offsetMin=fr.offsetMax=Vector2.zero;
            progressFill[i]=BG(fill,Gold,false);
            var status=Make("Status",card.transform);Rect(status,new Vector2(0,0),new Vector2(0,0),new Vector2(16,20),new Vector2(490,21));
            statusTexts[i]=Label(status,"0 / 0",15,Lilac,TextAlignmentOptions.MidlineLeft);
            claims[i]=MakeButton(card.transform,"Claim","CLAIM",new Vector2(1,.5f),new Vector2(1,.5f),
                new Vector2(-14,0),new Vector2(145,43),()=>Claim(idx));
            claimCaptions[i]=claims[i].GetComponentInChildren<UnityEngine.UI.Text>(true);
        }
        var foot=Make("Feedback",periodPanel.transform);Rect(foot,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,22),new Vector2(708,35));
        feedback=Label(foot,"Resets at 00:00 UTC; claims only in MainMenu.",16,Lilac,TextAlignmentOptions.Center);
        periodPanel.SetActive(false);
    }
    private void ShowPeriodic()
    {
        capturedSlot=MS5A2GoldBank.SelectedSlot;tab=0;
        progressPanel.SetActive(false);periodPanel.SetActive(true);
        feedback.text="Daily / weekly progress is tied to committed Victory saves.";
        Refresh();
        RefreshTextMeshes();
    }
    // Force a text redraw after enabling the previously inactive overlay.
    private void RefreshTextMeshes()
    {
        Canvas.ForceUpdateCanvases();
        foreach (UnityEngine.UI.Text t in generatedText)
        {
            if (t == null) continue;
            t.SetAllDirty();
        }
        Canvas.ForceUpdateCanvases();
        if (!visibilityLogged)
        {
            visibilityLogged = true;
            UnityEngine.UI.Text probe = names[0];
            Debug.Log("[7H.3 UI] Legacy UI Text active=" + (probe != null && probe.isActiveAndEnabled) +
                " font=" + (probe != null && probe.font != null ? probe.font.name : "MISSING") +
                " chars=" + (probe != null ? probe.text.Length : 0) +
                " rendererCull=" + (probe != null && probe.canvasRenderer != null && probe.canvasRenderer.cull) +
                " rect=" + (probe != null ? probe.rectTransform.rect.ToString() : "NULL"), this);
        }
    }
    private void BackToProgress() {periodPanel.SetActive(false);progressPanel.SetActive(true);}
    private void CloseAll() {periodPanel.SetActive(false);progressPanel.SetActive(true);progressOverlay.gameObject.SetActive(false);wasOpen=false;}
    private void SwitchTab(int newTab) {tab=newTab;Refresh();RefreshTextMeshes();}
    private void Claim(int index)
    {
        if (MS5A2GoldBank.SelectedSlot!=capturedSlot)
        { feedback.text="Profile changed. Close and reopen PROGRESS.";return; }
        string result;MS7H3PeriodicQuestService.TryClaim(capturedSlot,(MS7H3QuestId)(tab*2+index),out result);
        feedback.text=result;Refresh();
    }
    private void Refresh()
    {
        if (!periodPanel || !periodPanel.activeSelf) return;
        var s=MS7H3PeriodicQuestService.ReadSnapshot(capturedSlot);
        bool slotOk=capturedSlot==MS5A2GoldBank.SelectedSlot;
        summary.text="PROFILE "+capturedSlot.ToString("00")+"   GOLD "+s.bankGold+"   "+s.message+
            "    RESET "+(tab==0?s.dailyUntilUtc:s.weeklyUntilUtc);
        for(int i=0;i<2;i++)
        {
            var q=s.quests[tab*2+i];names[i].text=q.title;descriptions[i].text=q.description;
            statusTexts[i].text=q.current+" / "+q.target+"    +"+q.reward+" Gold";
            RectTransform f=progressFill[i].rectTransform;
            f.anchorMin=Vector2.zero;f.anchorMax=new Vector2(Mathf.Clamp01((float)q.current/q.target),1);
            f.offsetMin=Vector2.zero;f.offsetMax=Vector2.zero;
            bool canClaim=s.valid && !s.backupRecovery && !s.clockRollback && slotOk && q.completed && !q.claimed;
            claims[i].interactable=canClaim;
            claimCaptions[i].text=q.claimed?"CLAIMED":canClaim?"CLAIM":"LOCKED";
            claimCaptions[i].color=canClaim?Gold:new Color(.62f,.57f,.69f,1f);
        }
        if (!slotOk) feedback.text="Profile changed. Close/reopen PROGRESS to use another slot.";
    }
}
