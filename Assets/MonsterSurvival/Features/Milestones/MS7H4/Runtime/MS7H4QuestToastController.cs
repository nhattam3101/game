using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 7H.4 -- additive, READ ONLY notifications. Never writes a Save, Gold, or quest marker.
// Survives MainMenu -> SampleScene -> MainMenu in a single play session so the
// first saved Victory can be compared with the pre-game snapshot.
[DisallowMultipleComponent]
public sealed class MS7H4QuestToastController : MonoBehaviour
{
    [Header("7H.4 - UI Only (no Save writes)")]
    public bool notificationsEnabled = true;
    [Range(1f, 6f)] public float toastSeconds = 3f;
    [Range(0.5f, 4f)] public float pollingSeconds = 1.1f;
    [Range(0.1f, 0.8f)] public float fadeSeconds = .28f;
    [Range(0.6f, 1.4f)] public float uiScale = 1f;
    public bool verboseLogs = false;

    private const int NumberOfQuests = 10; // 7H1=3, 7H2=3, 7H3=4.
    private const int MaxQueuedToasts = 8;
    private static MS7H4QuestToastController instance;
    private sealed class SnapshotFlags
    {
        public readonly bool[] completed = new bool[NumberOfQuests];
        public readonly bool[] claimed = new bool[NumberOfQuests];
        public readonly string[] titles = new string[NumberOfQuests];
        public readonly int[] rewards = new int[NumberOfQuests];
    }
    private readonly Dictionary<int, SnapshotFlags> baselines = new Dictionary<int, SnapshotFlags>();
    private readonly Queue<string> pendingToasts = new Queue<string>();
    private float nextPoll;
    private float toastTime;
    private bool toastShowing;
    private bool warnedReadError;
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Text toastLabel;
    private Image toastBackground;
    private RectTransform toastRect;
    private int completionEvents, claimEvents, previews;
    private string lastEvent = "Not observed yet";

    public static MS7H4QuestToastController Instance { get { return instance; } }
    public int CompletionEvents { get { return completionEvents; } }
    public int ClaimEvents { get { return claimEvents; } }
    public int QueuedCount { get { return pendingToasts.Count; } }
    public int PreviewCount { get { return previews; } }
    public string LastEvent { get { return lastEvent; } }
    public int BaselineProfiles { get { return baselines.Count; } }

    private void Awake()
    {
        if (!Application.isPlaying) return;
        if (instance != null && instance != this)
        {
            Destroy(gameObject); // Re-entering MainMenu must not create a second listener.
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject); // Root GameObject installed by Editor, never an existing UI root.
        CreateToastCanvas();
    }

    private void Start()
    {
        // Seed the selected Profile as soon as the menu starts, before the first match.
        if (Application.isPlaying && instance == this && SceneManager.GetActiveScene().name == "MainMenu")
        {
            PollSelectedProfile();
            nextPoll = Time.unscaledTime + Mathf.Max(.5f, pollingSeconds);
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (!Application.isPlaying || instance != this) return;
        bool inMenu = SceneManager.GetActiveScene().name == "MainMenu";
        if (canvas != null) canvas.enabled = inMenu && notificationsEnabled;
        if (!inMenu) return;

        if (Time.unscaledTime >= nextPoll)
        {
            nextPoll = Time.unscaledTime + Mathf.Max(.5f, pollingSeconds);
            PollSelectedProfile();
        }
        AnimateToast(Time.unscaledDeltaTime);
    }

    private void PollSelectedProfile()
    {
        int slot = MS5A2GoldBank.SelectedSlot;
        if (slot < 1 || slot > 3) return;
        SnapshotFlags now;
        if (!TryRead(slot, out now)) return;
        SnapshotFlags before;
        if (!baselines.TryGetValue(slot, out before))
        {
            baselines[slot] = now; // No historical notification spam on first visit.
            if (verboseLogs) Debug.Log("[7H.4] Baseline created for Profile " + slot, this);
            return;
        }
        int newEvents = 0;
        for (int i = 0; i < NumberOfQuests; i++)
        {
            // Claim takes priority over completion when both change in the same poll.
            if (!before.claimed[i] && now.claimed[i])
            {
                QueueEvent("REWARD CLAIMED  +" + now.rewards[i] + " GOLD  |  " + now.titles[i]);
                claimEvents++;
                newEvents++;
            }
            else if (!before.completed[i] && now.completed[i] && !now.claimed[i])
            {
                QueueEvent("QUEST COMPLETE  |  " + now.titles[i]);
                completionEvents++;
                newEvents++;
            }
        }
        // Replace even if nothing changed; period resets transition to false without a toast.
        baselines[slot] = now;
        if (verboseLogs && newEvents > 0)
            Debug.Log("[7H.4] " + newEvents + " NEW status changes on Profile " + slot, this);
    }

    private static bool TryRead(int slot, out SnapshotFlags state)
    {
        state = null;
        try
        {
            MS7H1QuestSnapshot first = MS7H1QuestService.ReadSnapshot(slot);
            MS7H2Snapshot progress = MS7H2ProgressService.ReadSnapshot(slot);
            MS7H3Snapshot period = MS7H3PeriodicQuestService.ReadSnapshot(slot);
            if (first == null || progress == null || period == null ||
                !first.valid || !progress.valid || !period.valid ||
                first.backupRecovery || progress.backupRecovery || period.backupRecovery ||
                period.clockRollback || first.quests == null || progress.quests == null ||
                period.quests == null || first.quests.Length != 3 || progress.quests.Length != 3 ||
                period.quests.Length != 4) return false;

            SnapshotFlags flags = new SnapshotFlags();
            for (int i = 0; i < 3; i++)
            {
                MS7H1QuestView q = first.quests[i];
                if (q == null) return false;
                flags.completed[i] = q.completed;
                flags.claimed[i] = q.claimed;
                flags.titles[i] = q.title;
                flags.rewards[i] = q.reward;
            }
            for (int i = 0; i < 3; i++)
            {
                MS7H2QuestView q = progress.quests[i];
                if (q == null) return false;
                int idx = i + 3;
                flags.completed[idx] = q.completed;
                flags.claimed[idx] = q.claimed;
                flags.titles[idx] = q.title;
                flags.rewards[idx] = q.reward;
            }
            for (int i = 0; i < 4; i++)
            {
                MS7H3QuestView q = period.quests[i];
                if (q == null) return false;
                int idx = i + 6;
                flags.completed[idx] = q.completed;
                flags.claimed[idx] = q.claimed;
                flags.titles[idx] = q.title;
                flags.rewards[idx] = q.reward;
            }
            state = flags;
            return true;
        }
        catch (Exception ex)
        {
            // No Save mutation, no log flood on broken files.
            if (instance != null && !instance.warnedReadError)
            {
                instance.warnedReadError = true;
                Debug.LogWarning("[7H.4] Read-only preview skipped: " + ex.Message, instance);
            }
            return false;
        }
    }

    private void QueueEvent(string message)
    {
        lastEvent = message;
        if (!notificationsEnabled) return;
        if (pendingToasts.Count >= MaxQueuedToasts) pendingToasts.Dequeue();
        pendingToasts.Enqueue(message);
    }

    public void PreviewOnly()
    {
        if (!Application.isPlaying || instance != this || SceneManager.GetActiveScene().name != "MainMenu") return;
        previews++;
        QueueEvent("7H.4 UI PREVIEW  |  No Gold or Save changed");
    }

    private void AnimateToast(float delta)
    {
        if (canvasGroup == null || toastRect == null) return;
        if (!toastShowing)
        {
            if (pendingToasts.Count == 0)
            {
                canvasGroup.alpha = 0f;
                return;
            }
            toastLabel.text = pendingToasts.Dequeue();
            toastShowing = true;
            toastTime = 0f;
        }
        toastTime += delta;
        float duration = Mathf.Max(1f, toastSeconds);
        float fade = Mathf.Min(Mathf.Max(.1f, fadeSeconds), duration * .25f);
        float a = Mathf.Min(1f, toastTime / fade, (duration - toastTime) / fade);
        canvasGroup.alpha = Mathf.Clamp01(a);
        if (toastTime >= duration)
        {
            canvasGroup.alpha = 0f;
            toastShowing = false;
        }
    }

    private void CreateToastCanvas()
    {
        GameObject root = new GameObject("7H4 Toast Canvas [runtime]", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.layer = 5;
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 270;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        GameObject panel = new GameObject("Quest Toast (noninteractive)", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panel.layer = 5;
        panel.transform.SetParent(root.transform, false);
        toastRect = panel.GetComponent<RectTransform>();
        toastRect.anchorMin = new Vector2(.5f, 0f);
        toastRect.anchorMax = new Vector2(.5f, 0f);
        toastRect.pivot = new Vector2(.5f, 0f);
        toastRect.anchoredPosition = new Vector2(0f, 96f);
        toastRect.sizeDelta = new Vector2(525f * uiScale, 52f * uiScale);
        toastBackground = panel.GetComponent<Image>();
        toastBackground.color = new Color(.13f, .075f, .20f, .96f);
        toastBackground.raycastTarget = false;
        canvasGroup = panel.GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0f;

        GameObject word = new GameObject("Gold Quest Notification", typeof(RectTransform), typeof(Text));
        word.layer = 5;
        word.transform.SetParent(panel.transform, false);
        RectTransform textRect = word.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12, 4);
        textRect.offsetMax = new Vector2(-12, -4);
        toastLabel = word.GetComponent<Text>();
        toastLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (toastLabel.font == null)
            toastLabel.font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Liberation Sans" }, 22);
        toastLabel.fontSize = Mathf.RoundToInt(19f * uiScale);
        toastLabel.resizeTextForBestFit = true;
        toastLabel.resizeTextMinSize = 12;
        toastLabel.resizeTextMaxSize = Mathf.RoundToInt(19f * uiScale);
        toastLabel.alignment = TextAnchor.MiddleCenter;
        toastLabel.color = new Color(.96f, .82f, .58f, 1f);
        toastLabel.raycastTarget = false;
        toastLabel.text = "";
    }

    public static string PureTests()
    {
        // Deterministic and strictly in-memory: no profile read, no scene edits, no Save writes.
        bool[] oldComplete = new bool[NumberOfQuests];
        bool[] newComplete = new bool[NumberOfQuests];
        bool[] oldClaim = new bool[NumberOfQuests];
        bool[] newClaim = new bool[NumberOfQuests];
        int passes = 0;
        if (CountTransitions(oldComplete, newComplete, oldClaim, newClaim) == 0) passes++;
        newComplete[0] = true;
        if (CountTransitions(oldComplete, newComplete, oldClaim, newClaim) == 1) passes++;
        oldComplete[0] = true;
        if (CountTransitions(oldComplete, newComplete, oldClaim, newClaim) == 0) passes++;
        newClaim[0] = true;
        if (CountTransitions(oldComplete, newComplete, oldClaim, newClaim) == 1) passes++;
        oldClaim[0] = true;
        if (CountTransitions(oldComplete, newComplete, oldClaim, newClaim) == 0) passes++;
        newComplete[0] = false; // Daily reset never produces a false toast.
        if (CountTransitions(oldComplete, newComplete, oldClaim, newClaim) == 0) passes++;
        return "7H.4 IN-MEMORY PURE TESTS=" + passes + "/6 " + (passes == 6 ? "PASS" : "FAIL");
    }

    private static int CountTransitions(bool[] a, bool[] b, bool[] x, bool[] y)
    {
        int total = 0;
        for (int i = 0; i < NumberOfQuests; i++)
            if ((!x[i] && y[i]) || (!a[i] && b[i] && !y[i])) total++;
        return total;
    }
}
