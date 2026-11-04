using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 5D.4: additive, read-only layer on top of 5D.2 + 5D.3.2.
// Does not change save files, rewards, game state or existing button listeners.
[DisallowMultipleComponent]
[DefaultExecutionOrder(2100)]
public sealed class MS5D4ProgressRibbon : MonoBehaviour
{
    [Header("Linked by 5D.4 Installer")]
    public MS5D2ProfilePolish profilePolish;
    public MS5D3StatisticsTab statisticsTab;
    [Range(0.2f, 3f)] public float refreshSeconds = 0.75f;
    [Range(8f, 20f)] public float minimumLabelSize = 11f;

    private RectTransform rows;
    private RectTransform viewport;
    private Transform panel;
    private TMP_Text[] values;
    private RectTransform[] fills;
    private TMP_Text warning;
    private int lastSlot = -1;
    private int lastRevision = int.MinValue;
    private float nextUpdate;
    private bool ready;
    private bool narrow;

    private static readonly Color Purple = new Color(.68f,.46f,.92f,1f);
    private static readonly Color Gold = new Color(.96f,.75f,.40f,1f);
    private static readonly Color Teal = new Color(.48f,.88f,.76f,1f);
    private static readonly Color Muted = new Color(.74f,.67f,.82f,1f);

    private IEnumerator Start()
    {
        // Other scripts build their runtime UI inside Start(). Wait for those children.
        yield return null;
        yield return null;
        if (!profilePolish) profilePolish = GetComponent<MS5D2ProfilePolish>();
        if (!statisticsTab) statisticsTab = GetComponent<MS5D3StatisticsTab>();
        Transform path = transform.Find("MS5D2_PolishedOverlay/ProfileWindow/MS5D3_StatsViewport/MS5D3_StatsRows");
        if (!profilePolish || !statisticsTab || !path)
        {
            Debug.LogWarning("[5D.4] Missing the 5D.2/5D.3 runtime UI. 5D.4 did not change anything.", this);
            enabled = false;
            yield break;
        }
        rows = path as RectTransform;
        viewport = rows.parent as RectTransform;
        panel = viewport.parent;
        if (!rows || !viewport) { enabled = false; yield break; }
        AddFooter();
        // Layout controls content height, which is important after adding the new footer.
        ContentSizeFitter fitter = rows.GetComponent<ContentSizeFitter>();
        if (!fitter) fitter = rows.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        ready = true;
        ApplyResponsive(true);
        Refresh(true);
    }

    private void Update()
    {
        if (!ready || !viewport || !viewport.gameObject.activeInHierarchy) return;
        if (Time.unscaledTime < nextUpdate && lastSlot == MS5A2GoldBank.SelectedSlot) return;
        nextUpdate = Time.unscaledTime + Mathf.Max(.2f, refreshSeconds);
        ApplyResponsive(false);
        Refresh(false);
    }

    private void AddFooter()
    {
        RectTransform footer = Rect("MS5D4_ProgressSummary", rows);
        Image bg = footer.gameObject.AddComponent<Image>();
        bg.color = new Color(.18f,.12f,.25f,.97f);
        bg.raycastTarget = false;
        LayoutElement le = footer.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 150f;
        le.preferredHeight = 150f;
        Text(footer,"TITLE", "PROGRESSION SUMMARY", 16f, Gold, new Vector2(.045f,.77f),new Vector2(.98f,.98f));
        values = new TMP_Text[3];
        fills = new RectTransform[3];
        string[] titles = { "MAPS", "BOSS BADGES", "UPGRADES" };
        Color[] colors = { Teal, Gold, Purple };
        for (int i = 0; i < 3; i++)
        {
            float y = .59f - i * .23f;
            Text(footer,"LABEL_"+i,titles[i],13f,Muted,new Vector2(.045f,y),new Vector2(.32f,y+.19f));
            RectTransform track = Rect("TRACK_"+i,footer);
            Track(track,new Vector2(.33f,y+.055f),new Vector2(.78f,y+.132f));
            Image trackBg = track.gameObject.AddComponent<Image>();
            trackBg.color = new Color(.07f,.05f,.12f,1f); trackBg.raycastTarget = false;
            RectTransform fill = Rect("FILL_"+i,track);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f,1f);
            fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
            Image bar = fill.gameObject.AddComponent<Image>();
            bar.color = colors[i]; bar.raycastTarget = false;
            fills[i] = fill;
            values[i] = Text(footer,"VALUE_"+i,"—",13f,colors[i],
                new Vector2(.79f,y),new Vector2(.97f,y+.20f));
            values[i].alignment = TextAlignmentOptions.MidlineRight;
        }
        warning = Text(footer,"WARNING","READ ONLY",11f,Muted,
            new Vector2(.045f,0f), new Vector2(.98f,.12f));
    }

    private void Refresh(bool force)
    {
        int slot = MS5A2GoldBank.SelectedSlot;
        if (slot < 1 || slot > 3) { SetUnavailable("UNKNOWN PROFILE"); return; }
        MS5A1SaveData data;
        string why;
        bool fromBackup;
        bool saved = MS5A1SaveService.TryLoad(slot,out data,out why,out fromBackup);
        bool fresh = !saved && !string.IsNullOrEmpty(why) && why.StartsWith("NO_SAVE",StringComparison.Ordinal);
        if (!saved && !fresh) { SetUnavailable("SAVE UNAVAILABLE — " + why); lastSlot = slot; return; }
        if (fresh) data = MS5A1SaveService.CreateFresh(slot);
        if (data == null) { SetUnavailable("NO DATA"); return; }
        if (!force && slot == lastSlot && data.revision == lastRevision) return;
        lastSlot = slot; lastRevision = data.revision;
        int mapCount = 1 + (Has(data,"map.sand_dunes")?1:0) + (Has(data,"map.ancient_ruins")?1:0);
        int bosses = (Has(data,"achievement.shadow_titan")?1:0) + (Has(data,"achievement.grave_warden")?1:0);
        int upgrades = 0;
        string[] ids = { "vitality", "strength", "fury", "agility" };
        foreach (string id in ids)
        {
            int level = 0;
            if (data.permanentUpgrades != null)
                foreach (MS5A1UpgradeEntry e in data.permanentUpgrades)
                    if (e != null && e.id == id) { level = Mathf.Clamp(e.level,0,10); break; }
            upgrades += level;
        }
        SetBar(0,mapCount,3);
        SetBar(1,bosses,2);
        SetBar(2,upgrades,40);
        warning.text = fromBackup ? "BACKUP ONLY — progress may be older; this panel never writes data." :
                       fresh ? "NEW PROFILE — no Save file created by this viewer." :
                       "READ ONLY · PROFILE " + slot.ToString("00") + " · REV " + data.revision;
        warning.color = fromBackup ? new Color(1f,.60f,.48f,1f) : Muted;
    }

    private void SetUnavailable(string reason)
    {
        if (fills == null || values == null) return;
        for (int i=0;i<3;i++) SetBar(i,0,0);
        if (warning) { warning.text = reason ?? "SAVE UNAVAILABLE"; warning.color = new Color(1f,.6f,.48f,1f); }
        lastRevision = int.MinValue;
    }

    private void SetBar(int index,int count,int max)
    {
        if (fills == null || index < 0 || index >= fills.Length) return;
        fills[index].anchorMax = new Vector2(max > 0 ? Mathf.Clamp01((float)count/max) : 0f,1f);
        values[index].text = max > 0 ? count + " / " + max : "—";
    }

    private static bool Has(MS5A1SaveData data,string id)
    {
        return data != null && data.unlockedContentIds != null && data.unlockedContentIds.Contains(id);
    }

    private void ApplyResponsive(bool force)
    {
        if (!viewport || !rows) return;
        bool compact = viewport.rect.width < 730f;
        if (!force && compact == narrow) return;
        narrow = compact;
        // Keep the 5D.3 layout and existing icon references intact; only adjust text fit.
        for (int i=0; i<rows.childCount; i++)
        {
            Transform child = rows.GetChild(i);
            if (!child.name.StartsWith("Row_",StringComparison.Ordinal)) continue;
            Transform labelTransform = child.Find("TXT_Label");
            Transform valueTransform = child.Find("TXT_Value");
            Transform icon = child.Find("ICON_Statistics");
            TMP_Text label = labelTransform ? labelTransform.GetComponent<TMP_Text>() : null;
            TMP_Text value = valueTransform ? valueTransform.GetComponent<TMP_Text>() : null;
            if (label)
            {
                label.enableAutoSizing = true;
                label.fontSizeMin = Mathf.Min(minimumLabelSize,12f);
                label.fontSizeMax = compact ? 13f : 15f;
                label.rectTransform.anchorMax = new Vector2(compact ? .70f : .69f,1f);
                label.rectTransform.offsetMin = new Vector2(compact ? 49f : 77f,0f);
            }
            if (value)
            {
                value.enableAutoSizing = true;
                value.fontSizeMin = 10f;
                value.fontSizeMax = compact ? 17f : 22f;
                value.rectTransform.anchorMin = new Vector2(compact ? .71f : .69f,0f);
            }
            RectTransform iconRect = icon as RectTransform;
            if (iconRect)
            {
                float side = compact ? 30f : 44f;
                iconRect.sizeDelta = new Vector2(side,side);
                iconRect.anchoredPosition = new Vector2(compact ? 27f : 39f,0f);
            }
        }
        LayoutRebuilder.MarkLayoutForRebuild(rows);
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        GameObject go = new GameObject(name,typeof(RectTransform));
        go.layer = 5; go.transform.SetParent(parent,false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one;
        r.offsetMin=Vector2.zero; r.offsetMax=Vector2.zero;
        return r;
    }

    private static void Track(RectTransform r, Vector2 min, Vector2 max)
    {
        r.anchorMin=min; r.anchorMax=max;
        r.offsetMin=Vector2.zero; r.offsetMax=Vector2.zero;
    }

    private static TMP_Text Text(Transform parent,string name,string text,float size,Color tint,Vector2 min,Vector2 max)
    {
        RectTransform r=Rect("TXT_"+name,parent);
        Track(r,min,max);
        TextMeshProUGUI t=r.gameObject.AddComponent<TextMeshProUGUI>();
        t.fontSize=size; t.enableAutoSizing=true;
        t.fontSizeMin=10f; t.fontSizeMax=size;
        t.color=tint; t.text=text;
        t.alignment=TextAlignmentOptions.MidlineLeft;
        t.raycastTarget=false;
        if (TMP_Settings.defaultFontAsset) t.font=TMP_Settings.defaultFontAsset;
        return t;
    }
}
