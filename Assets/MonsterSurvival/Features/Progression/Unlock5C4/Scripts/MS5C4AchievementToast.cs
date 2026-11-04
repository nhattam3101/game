using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Read-only post-save notification. Watches the exact run Profile's 5C.3 achievement IDs.
// A toast appears ONLY after the saved unlock bit changes from false to true.
[DisallowMultipleComponent]
public sealed class MS5C4AchievementToast : MonoBehaviour
{
    [Range(1f, 8f)] public float displaySeconds = 3.2f;
    public AudioClip unlockSound;
    [Range(0f, 1f)] public float soundVolume = .38f;
    private const string TitanId = "achievement.shadow_titan";
    private const string WardenId = "achievement.grave_warden";
    private readonly Queue<string> pending = new Queue<string>();
    private int runSlot;
    private bool hasBaseline;
    private bool seenTitan;
    private bool seenWarden;
    private bool running;
    private bool warnedAboutProfile;
    private float nextPoll;
    private GameObject canvasObject;
    private AudioSource fallbackAudio;
    private CanvasGroup group;
    private TMP_Text title;
    private TMP_Text description;
    private RectTransform panel;
    private static readonly Color Gold = new Color(.98f,.77f,.42f,1f);

    private void Start()
    {
        runSlot = MS5A2GoldBank.SelectedSlot;
        BuildToast();
        Observe(); // initial baseline; never celebrates already-unlocked achievements
    }

    private void Update()
    {
        if (Time.unscaledTime < nextPoll) return;
        nextPoll = Time.unscaledTime + .40f;
        if (runSlot != MS5A2GoldBank.SelectedSlot)
        {
            if (!warnedAboutProfile) Debug.LogWarning("[5C.4] Profile changed during run: toasts suppressed.",this);
            warnedAboutProfile = true;
            return;
        }
        Observe();
        if (!running && pending.Count > 0) StartCoroutine(PlayQueue());
    }

    private void Observe()
    {
        MS5C1UnlockService.Snapshot snapshot;
        string reason;
        if (!MS5C1UnlockService.TryGetSnapshot(runSlot,out snapshot,out reason)) return;
        bool titan=snapshot.IsUnlocked(TitanId);
        bool warden=snapshot.IsUnlocked(WardenId);
        if (!hasBaseline)
        {
            seenTitan=titan;seenWarden=warden;hasBaseline=true;
            return;
        }
        if (!seenTitan && titan) pending.Enqueue("SHADOW TITAN");
        if (!seenWarden && warden) pending.Enqueue("GRAVE WARDEN");
        seenTitan=titan;seenWarden=warden;
    }

    private IEnumerator PlayQueue()
    {
        running=true;
        while (pending.Count > 0)
        {
            string boss = pending.Dequeue();
            PlayUnlockSound();
            title.text="ACHIEVEMENT UNLOCKED";
            description.text=boss=="SHADOW TITAN" ? "TITAN SLAYER  ·  PROFILE "+runSlot.ToString("00") :
                "WARDEN BREAKER  ·  PROFILE "+runSlot.ToString("00");
            float t=0f;
            float length=Mathf.Max(1f,displaySeconds);
            while (t < length)
            {
                t += Time.unscaledDeltaTime;
                float fadeIn=Mathf.Clamp01(t/.30f);
                float fadeOut=Mathf.Clamp01((length-t)/.55f);
                float a=Mathf.Min(fadeIn,fadeOut);
                group.alpha=a;
                panel.localScale=Vector3.one*(1f+.045f*(1f-Mathf.Min(1f,t/.35f)));
                yield return null;
            }
            group.alpha=0f;
        }
        running=false;
    }

    private void PlayUnlockSound()
    {
        if (!unlockSound || soundVolume <= 0f) return;
        AudioManager audio = AudioManager.Instance;
        if (audio && audio.sfxSource)
        {
            // Existing routed AudioSource respects the game's SFX mixer volume.
            audio.sfxSource.PlayOneShot(unlockSound, soundVolume);
        }
        else
        {
            // Fallback for a Scene without an active AudioManager.
            if (!fallbackAudio && canvasObject) fallbackAudio = canvasObject.AddComponent<AudioSource>();
            if (fallbackAudio)
            {
                fallbackAudio.playOnAwake = false;
                fallbackAudio.spatialBlend = 0f;
                fallbackAudio.PlayOneShot(unlockSound,
                    soundVolume * PlayerPrefs.GetFloat("MonsterSurvival_SFXVolume", .8f));
            }
        }
    }

    private void BuildToast()
    {
        canvasObject=new GameObject("MS5C4_AchievementToastRuntime",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
        Canvas canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=850;
        CanvasScaler scaler=canvasObject.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        panel=Rect("ToastPanel",canvasObject.transform,new Vector2(.34f,.83f),new Vector2(.66f,.965f));
        Image background=panel.gameObject.AddComponent<Image>();background.raycastTarget=false;
        background.color=new Color(.105f,.062f,.19f,.97f);
        group=panel.gameObject.AddComponent<CanvasGroup>();group.alpha=0f;group.blocksRaycasts=false;group.interactable=false;
        Image top=Rect("GoldLine",panel,new Vector2(.03f,.92f),new Vector2(.97f,.956f)).gameObject.AddComponent<Image>();
        top.color=Gold;top.raycastTarget=false;
        title=Label("ACHIEVEMENT UNLOCKED",panel,new Vector2(.06f,.43f),new Vector2(.94f,.87f),32,Gold);
        description=Label("",panel,new Vector2(.06f,.11f),new Vector2(.94f,.48f),23,new Color(.96f,.91f,1f,1f));
    }
    private static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
    {
        GameObject go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);
        RectTransform rt=go.GetComponent<RectTransform>();rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=rt.offsetMax=Vector2.zero;return rt;
    }
    private static TMP_Text Label(string value,Transform parent,Vector2 min,Vector2 max,int size,Color color)
    {
        RectTransform rt=Rect("Text",parent,min,max);TextMeshProUGUI t=rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text=value;t.color=color;t.fontSize=size;t.fontSizeMin=13;t.fontSizeMax=size;t.enableAutoSizing=true;
        t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;
        if(TMP_Settings.defaultFontAsset)t.font=TMP_Settings.defaultFontAsset;
        return t;
    }
    private void OnDestroy(){if(canvasObject)Destroy(canvasObject);}
}
