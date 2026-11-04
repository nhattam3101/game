using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

// 5B.4: PRESENTATION ONLY. Does not call TryPurchase or TrySave.
// Installed on the existing MS5B2_UpgradeShop; no changes to 5B.2 scripts.
[DisallowMultipleComponent]
public sealed class MS5B4PurchaseFeedback : MonoBehaviour
{
    [Header("UI Sounds (original, bundled clips)")]
    public AudioClip successClip;
    public AudioClip blockedClip;
    [Range(0f, 1f)] public float localSfxVolume = 0.55f;
    public bool followExistingSfxVolume = true;

    [Header("Visuals")]
    [Range(0.25f, 1.4f)] public float flashDuration = 0.66f;
    [Range(0f, 0.075f)] public float successScaleBoost = 0.028f;
    public bool animatePurchaseGlow = true;

    private static readonly string[] Keys = { "vitality", "strength", "fury", "agility" };
    private static readonly Color[] Accent = {
        new Color(.99f, .49f, .55f, 1f), new Color(.96f, .75f, .42f, 1f),
        new Color(.70f, .47f, 1f, 1f), new Color(.48f, .82f, 1f, 1f)
    };
    private readonly Button[] buttons = new Button[4];
    private readonly RectTransform[] cardRects = new RectTransform[4];
    private readonly Image[] cardImages = new Image[4];
    private readonly Image[,] glow = new Image[4, 4];
    private readonly TMP_Text[] shineTexts = new TMP_Text[4];
    private readonly Vector3[] baseScales = new Vector3[4];
    private readonly Color[] baseColors = new Color[4];
    private readonly float[] timer = new float[4];
    private readonly bool[] registered = new bool[4];
    private readonly UnityAction[] clickActions = new UnityAction[4];
    private RectTransform window;
    private AudioSource audioSource;
    private MS5B1PermanentUpgrades.Snapshot lastSnapshot;
    private int lastSlot = -1;
    private bool bound;
    private bool previousVisible;
    private float retryElapsed;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (!audioSource) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.loop = false;
        TryBind();
    }

    private void Update()
    {
        if (!bound)
        {
            retryElapsed += Time.unscaledDeltaTime;
            if (retryElapsed > .20f) { retryElapsed = 0f; TryBind(); }
            return;
        }

        bool visible = window && window.gameObject.activeInHierarchy;
        if (visible != previousVisible)
        {
            previousVisible = visible;
            if (visible) CaptureCurrentProfile();
            else ResetVisuals();
        }
        if (!visible) return;

        // Only reacquire a baseline when the chosen profile changes; no save writes.
        if (lastSlot != MS5A2GoldBank.SelectedSlot) CaptureCurrentProfile();
        Animate(Time.unscaledDeltaTime);
    }

    private bool TryBind()
    {
        if (bound) return true;
        Transform win = transform.Find("MS5B2_Window");
        if (!win) return false; // 5B.2 BuildUI may not have run yet.
        Transform panel = win.Find("ShopPanel");
        if (!panel) return false;

        for (int i = 0; i < Keys.Length; ++i)
        {
            Transform card = panel.Find("Upgrade_" + Keys[i]);
            if (!card) return false;
            Transform btn = card.Find("BTN_Upgrade");
            if (!btn || !btn.GetComponent<Button>()) return false;
        }

        window = win as RectTransform;
        for (int i = 0; i < Keys.Length; ++i)
        {
            int captured = i;
            Transform card = panel.Find("Upgrade_" + Keys[i]);
            cardRects[i] = card as RectTransform;
            cardImages[i] = card.GetComponent<Image>();
            baseScales[i] = cardRects[i] ? cardRects[i].localScale : Vector3.one;
            baseColors[i] = cardImages[i] ? cardImages[i].color : Color.white;
            buttons[i] = card.Find("BTN_Upgrade").GetComponent<Button>();
            if (!registered[i])
            {
                // Added AFTER 5B.2's original listener. Purchase has already returned
                // by the time OnUpgradeButtonClicked runs.
                clickActions[i] = () => OnUpgradeButtonClicked(captured);
                buttons[i].onClick.AddListener(clickActions[i]);
                registered[i] = true;
            }
            BuildDecorations(i);
        }
        bound = true;
        return true;
    }

    private void CaptureCurrentProfile()
    {
        lastSlot = MS5A2GoldBank.SelectedSlot;
        MS5B1PermanentUpgrades.Snapshot data;
        string status;
        lastSnapshot = MS5B1PermanentUpgrades.TryGetSnapshot(lastSlot, out data, out status)
            ? data : null;
    }

    private void OnUpgradeButtonClicked(int index)
    {
        if (index < 0 || index >= Keys.Length) return;
        int slotNow = MS5A2GoldBank.SelectedSlot;
        MS5B1PermanentUpgrades.Snapshot after;
        string status;
        bool readOk = MS5B1PermanentUpgrades.TryGetSnapshot(slotNow, out after, out status);

        // No VFX unless a verified purchase in the currently viewed slot occurred:
        // exact level +1, lower bank Gold, higher save revision.
        bool success = readOk && lastSnapshot != null && slotNow == lastSlot &&
            after.LevelOf(Keys[index]) == lastSnapshot.LevelOf(Keys[index]) + 1 &&
            after.bankGold < lastSnapshot.bankGold && after.revision > lastSnapshot.revision;
        if (success)
        {
            PlaySound(successClip);
            timer[index] = Mathf.Max(0.25f, flashDuration);
        }
        else
        {
            // Failed attempt: muted cue only, no optimistic success animation.
            PlaySound(blockedClip);
        }
        lastSlot = slotNow;
        lastSnapshot = readOk ? after : null;
    }

    private void Animate(float unscaledDelta)
    {
        for (int i = 0; i < Keys.Length; ++i)
        {
            timer[i] = Mathf.Max(0f, timer[i] - unscaledDelta);
            float duration = Mathf.Max(0.01f, flashDuration);
            float timeLeft = timer[i] / duration;
            float elapsed = 1f - Mathf.Clamp01(timeLeft);
            float pop = timer[i] > 0f ? Mathf.Sin(Mathf.PI * elapsed) : 0f;
            float strength = animatePurchaseGlow ? Mathf.Clamp01(timeLeft) : 0f;
            if (cardRects[i]) cardRects[i].localScale = baseScales[i] * (1f + successScaleBoost * pop);
            if (cardImages[i]) cardImages[i].color = Color.Lerp(baseColors[i], Accent[i], strength * .14f);
            for (int edge = 0; edge < 4; ++edge)
            {
                if (!glow[i,edge]) continue;
                Color c = Accent[i];
                c.a = animatePurchaseGlow ? strength * (.84f + .16f * Mathf.Sin(elapsed * 19f)) : 0f;
                glow[i,edge].color = c;
            }
            if (shineTexts[i])
            {
                Color textColor = Accent[i];
                textColor.a = strength * .95f;
                shineTexts[i].color = textColor;
                if (shineTexts[i].rectTransform)
                    shineTexts[i].rectTransform.anchoredPosition = new Vector2(0, elapsed * 10f);
            }
        }
    }

    private void BuildDecorations(int i)
    {
        if (!cardRects[i]) return;
        Transform existed = cardRects[i].Find("MS5B4_EffectLayer");
        if (existed) return;
        RectTransform fx = CreateRect("MS5B4_EffectLayer", cardRects[i], Vector2.zero, Vector2.one);
        // Draw above contents, but NEVER capture pointer events (fixes 3A-era click issue).
        glow[i,0] = AddLine(fx, new Vector2(.0f,.987f), new Vector2(1f,1f));
        glow[i,1] = AddLine(fx, new Vector2(.0f,.0f), new Vector2(1f,.013f));
        glow[i,2] = AddLine(fx, new Vector2(.0f,.0f), new Vector2(.007f,1f));
        glow[i,3] = AddLine(fx, new Vector2(.993f,.0f), new Vector2(1f,1f));
        RectTransform message = CreateRect("TXT_MS5B4_LevelUp", fx,
            new Vector2(.62f,.80f),new Vector2(.975f,.98f));
        TextMeshProUGUI tmp = message.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = "LEVEL UP";
        tmp.alignment = TextAlignmentOptions.Right;
        tmp.fontSize = 22f;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 12f;
        tmp.fontSizeMax = 22f;
        tmp.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset) tmp.font = TMP_Settings.defaultFontAsset;
        Color c = Accent[i]; c.a = 0f; tmp.color = c;
        shineTexts[i] = tmp;
    }

    private static Image AddLine(Transform parent, Vector2 min, Vector2 max)
    {
        RectTransform rt = CreateRect("GlowBorder", parent, min, max);
        Image image = rt.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        image.color = new Color(1f,1f,1f,0f);
        return image;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.layer = 5;
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private void PlaySound(AudioClip clip)
    {
        if (!clip || !audioSource) return;
        float volume = localSfxVolume;
        if (followExistingSfxVolume)
        {
            AudioManager manager = AudioManager.Instance;
            volume *= manager ? manager.GetSFXVolume() : PlayerPrefs.GetFloat("SFXVolume", .8f);
        }
        if (volume > .001f) audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    // Preview deliberately does not read, mutate or save any profile data.
    public void PreviewSuccess(int index)
    {
        if (!Application.isPlaying || !TryBind() || index < 0 || index >= Keys.Length) return;
        if (window && !window.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("[5B.4] Open UPGRADES before previewing FX.");
            return;
        }
        timer[index] = flashDuration;
        PlaySound(successClip);
    }

    public void PreviewBlocked()
    {
        if (Application.isPlaying) PlaySound(blockedClip);
    }

    private void ResetVisuals()
    {
        for (int i = 0; i < Keys.Length; ++i)
        {
            timer[i] = 0f;
            if (cardRects[i]) cardRects[i].localScale = baseScales[i];
            if (cardImages[i]) cardImages[i].color = baseColors[i];
            for (int j = 0; j < 4; j++)
                if (glow[i,j]) { Color c = glow[i,j].color; c.a=0; glow[i,j].color=c; }
            if (shineTexts[i]) { Color c = shineTexts[i].color; c.a=0;shineTexts[i].color=c; }
        }
    }

    private void OnDisable() { ResetVisuals(); }
    private void OnDestroy()
    {
        for (int i = 0; i < Keys.Length; ++i)
            if (registered[i] && buttons[i])
                buttons[i].onClick.RemoveListener(clickActions[i]);
    }
}
