using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.Reflection;

// 7J.2: Visual-only replacement for 7J.1 hub presentation.
// 7J.1 host + MainMenu module buttons stay authoritative; no Save/Quest/Gold writes.
[DisallowMultipleComponent]
public sealed class MS7J2PlayerHubPolish : MonoBehaviour
{
    [Tooltip("Keep MainMenu loaded: older Quest/Record scripts require that active Scene.")]
    public string hostSceneName = "MainMenu";
    public string State { get; private set; } = "STARTING";

    private MS7J1HubEntry host;
    private TMP_FontAsset menuFont;
    private Canvas dashboard;
    private Canvas shield;
    private Canvas returnBar;
    private GameObject profileLabel;
    private GameObject statusLabel;
    private RectTransform viewport;
    private RectTransform content;
    private GridLayoutGroup grid;
    private ScrollRect scroller;
    private bool closing;
    // 7J.2.1: native popup lifecycle, no private fields or Save mutations.
    private int activeModule = -1;
    private string launchDetail = "NO MODULE CLICKED";
    private Coroutine openingRoutine;
    public string LastLaunchDiagnostic { get { return launchDetail; } }
    public string TrackedPopupName { get { return trackedPopup ? trackedPopup.name : "NONE"; } }
    private GameObject trackedPopup;
    private bool popupWasVisible;
    private float openedAt;
    private int lastSelectedSlot = -1;
    private float nextSyncAt;
    private string syncSummary = "NOT SYNCED";
    private readonly List<Button> closeHooks = new List<Button>();
    private UnityAction closeCallback;
    private bool returning;
    public int ActiveModuleIndex { get { return activeModule; } }
    public bool DashboardVisible { get { return dashboard && dashboard.gameObject.activeInHierarchy; } }
    public string SyncSummary { get { return syncSummary; } }
    private int lastWidth = -1, lastHeight = -1;
    private readonly List<Canvas> addedCanvas = new List<Canvas>();
    private readonly List<GraphicRaycaster> addedRaycasters = new List<GraphicRaycaster>();
    private readonly Dictionary<Canvas, int> previousSorting = new Dictionary<Canvas, int>();
    private readonly Dictionary<Canvas, bool> previousOverride = new Dictionary<Canvas, bool>();

    private static readonly string[] Titles = {
        "MANAGE SAVES", "PERMANENT UPGRADES", "ACHIEVEMENTS", "PROFILE & STATS",
        "LONG-TERM QUESTS", "PROGRESS / DAILY / WEEKLY", "ENDLESS RECORDS / REWARDS"
    };
    private static readonly string[] Details = {
        "Profiles  /  Backup  /  Recovery", "Vitality  /  Strength  /  Fury  /  Agility",
        "Boss achievements  /  Unlocks", "Wins  /  Statistics  /  Progress",
        "One-time quests  /  Rewards", "Milestones  /  Daily  /  Weekly",
        "Highest wave  /  Bosses  /  Gold claims"
    };
    private static readonly string[] Launchers = {
        "MS5A4_BTN_Open", "MS5B2_BTN_Upgrades", "MS5C4_BTN_Achievements",
        "MS5D1_BTN_ProfileOverview", "Quest Launcher", "Progress Launcher", "BTN_7I4Records"
    };
    // Original overlays are already created by MainMenu-side services.
    // 7H1/7H2 have separate overlays, not a popup named after the service.
    private static readonly string[] TrackingNames = {
        "MS5A4_SaveManager", "MS5B2_UpgradeShop", "",
        "", "Quest Board Overlay", "Progress Overlay", "7I4_Records_Modal"
    };
    private static readonly string[] OriginalPopupNames = {
        "MS5A4_SaveManager", "MS5B2_UpgradeShop", "",
        "", "", "", "7I4_Records_Modal"
    };

    private IEnumerator Start()
    {
        if (gameObject.scene.name != "PlayerHub") { State = "WRONG SCENE"; yield break; }
        Scene main = SceneManager.GetSceneByName(hostSceneName);
        if (!main.IsValid() || !main.isLoaded)
        {
            State = "NEEDS MAINMENU: Open PLAYER HUB from MainMenu";
            Debug.LogError("[7J.2] " + State, this);
            yield break;
        }
        foreach (GameObject root in main.GetRootGameObjects())
        {
            host = root.GetComponentInChildren<MS7J1HubEntry>(true);
            if (host) break;
        }
        if (!host || !host.menu)
        {
            State = "MISSING 7J.1 MAINMENU HUB ENTRY";
            Debug.LogError("[7J.2] " + State, this);
            yield break;
        }
        // Reuse the exact font asset already visible in MainMenu V3.
        if (host.menu.selectedMapCaption && host.menu.selectedMapCaption.font)
            menuFont = host.menu.selectedMapCaption.font;
        else if (host.menu.footerMessage && host.menu.footerMessage.font)
            menuFont = host.menu.footerMessage.font;
        else
        {
            TMP_Text[] fonts = host.menu.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in fonts) if (text && text.font) { menuFont = text.font; break; }
        }
        Build();
        State = "READY: 7J.2 POLISHED HUB";
        yield return null;
        LayoutNow();
    }

    private void Build()
    {
        shield = MS7J2UI.Canvas("MS7J2_PopupBackdrop", transform, 240);
        var shieldBG = MS7J2UI.Rect("Opaque overlay behind original popup", shield.transform);
        MS7J2UI.Anchors(shieldBG, 0, 0, 1, 1);
        MS7J2UI.Image(shieldBG, MS7J2UI.Ink, true);
        shield.gameObject.SetActive(false);

        dashboard = MS7J2UI.Canvas("MS7J2_ProfessionalDashboard", transform, 310);
        var bg = MS7J2UI.Rect("Background", dashboard.transform);
        MS7J2UI.Anchors(bg, 0, 0, 1, 1);
        MS7J2UI.Image(bg, MS7J2UI.Ink, true);
        MS7J2UI.Rule("Fine accent line", dashboard.transform, MS7J2UI.PurpleLine, .07f, .802f, .93f, .804f);
        MS7J2UI.Rule("Header gold marker", dashboard.transform, MS7J2UI.Gold, .07f, .818f, .074f, .941f);
        MS7J2UI.Label("Section number", dashboard.transform, "MENU  /  02", 17, MS7J2UI.Gold,
            TextAlignmentOptions.Left, menuFont, .09f, .928f, .43f, .958f);
        MS7J2UI.Label("PLAYER HUB", dashboard.transform, "PLAYER HUB", 58, MS7J2UI.Pale,
            TextAlignmentOptions.Left, menuFont, .09f, .847f, .69f, .929f, true);
        MS7J2UI.Label("Subtitle", dashboard.transform, "PROFILE   /   UPGRADES   /   ACHIEVEMENTS   /   QUESTS   /   RECORDS", 19,
            MS7J2UI.Muted, TextAlignmentOptions.Left, menuFont, .09f, .818f, .76f, .858f);

        var profileBadge = MS7J2UI.Rect("Profile badge background", dashboard.transform);
        MS7J2UI.Anchors(profileBadge, .76f, .872f, .93f, .93f);
        MS7J2UI.Image(profileBadge, MS7J2UI.Card);
        MS7J2UI.Rule("Profile badge accent", profileBadge, MS7J2UI.Gold, 0, .94f, 1, 1);
        profileLabel = MS7J2UI.Label("Live selected Profile", profileBadge, "PROFILE 01", 22, MS7J2UI.Gold,
            TextAlignmentOptions.Center, menuFont, .025f, .03f, .975f, .93f);

        var scrollGo = MS7J2UI.Rect("Seven tab navigation scroll", dashboard.transform);
        MS7J2UI.Anchors(scrollGo, .065f, .165f, .935f, .787f);
        viewport = scrollGo;
        var viewportImage = MS7J2UI.Image(scrollGo, new Color(0, 0, 0, 0), true);
        viewportImage.raycastTarget = true;
        scrollGo.gameObject.AddComponent<RectMask2D>();
        scroller = scrollGo.gameObject.AddComponent<ScrollRect>();
        scroller.horizontal = false;
        scroller.vertical = true;
        scroller.movementType = ScrollRect.MovementType.Clamped;
        scroller.scrollSensitivity = 36f;
        scroller.viewport = viewport;
        content = MS7J2UI.Rect("MS7J2_ModuleGrid", scrollGo);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.spacing = new Vector2(22f, 18f);
        grid.padding = new RectOffset(6, 6, 6, 6);
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroller.content = content;

        for (int i = 0; i < 7; i++) CreateCard(i);

        MS7J2UI.Rule("Footer divider", dashboard.transform, MS7J2UI.PurpleLine, .07f, .147f, .93f, .149f);
        var exit = MakeButton("Return to Main Menu", dashboard.transform, MS7J2UI.Card, CloseHub);
        MS7J2UI.Anchors((RectTransform)exit.transform, .07f, .044f, .324f, .125f);
        MS7J2UI.Rule("Exit gold edge", exit.transform, MS7J2UI.Gold, 0, 0, .012f, 1);
        MS7J2UI.Label("Exit caption", exit.transform, "<   BACK TO MAIN MENU", 23, MS7J2UI.Pale,
            TextAlignmentOptions.Center, menuFont, .03f, .03f, .97f, .97f, true);
        statusLabel = MS7J2UI.Label("Hub status", dashboard.transform, "SELECT A CATEGORY TO VIEW YOUR PROGRESS", 17,
            MS7J2UI.Muted, TextAlignmentOptions.Right, menuFont, .38f, .048f, .93f, .12f);

        returnBar = MS7J2UI.Canvas("MS7J2_ReturnFromModule", transform, 400);
        var ret = MakeButton("Return to Player Hub", returnBar.transform, MS7J2UI.Card, ReturnToHub);
        RectTransform r = (RectTransform)ret.transform;
        r.anchorMin = r.anchorMax = new Vector2(0, 0);
        r.pivot = new Vector2(0, 0);
        r.anchoredPosition = new Vector2(24f, 22f);
        r.sizeDelta = new Vector2(280f, 62f);
        MS7J2UI.Rule("Return gold edge", r, MS7J2UI.Gold, 0, 0, .011f, 1);
        MS7J2UI.Label("Return caption", r, "<   PLAYER HUB", 23, MS7J2UI.Pale,
            TextAlignmentOptions.Center, menuFont, .03f, .04f, .97f, .96f, true);
        returnBar.gameObject.SetActive(false);
        RefreshProfile();
        RefreshSavedDisplay(true);
    }
    private Button MakeButton(string name, Transform parent, Color color, UnityEngine.Events.UnityAction click)
    {
        var rt = MS7J2UI.Rect(name, parent);
        var image = MS7J2UI.Image(rt, color, true);
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None; // Hover handled explicitly, without washed-out text.
        button.onClick.AddListener(click);
        return button;
    }
    private void CreateCard(int index)
    {
        int captured = index;
        Button button = MakeButton("Category_" + (index + 1), content, MS7J2UI.Card, () => OpenModule(captured));
        RectTransform card = button.GetComponent<RectTransform>();
        Image bg = button.GetComponent<Image>();
        var top = MS7J2UI.Rule("Fine top rule", card, MS7J2UI.PurpleLine, 0, .978f, 1, 1);
        var left = MS7J2UI.Rule("Gold side accent", card, MS7J2UI.SoftGold, 0, .12f, .005f, .88f);
        var numberBlock = MS7J2UI.Rect("Index badge", card);
        MS7J2UI.Anchors(numberBlock, .035f, .235f, .13f, .765f);
        MS7J2UI.Image(numberBlock, new Color(.18f, .12f, .27f, 1f));
        MS7J2UI.Label("Index number", numberBlock, (index + 1).ToString("00"), 28, MS7J2UI.Gold,
            TextAlignmentOptions.Center, menuFont, .02f, 0, .98f, 1f, true);
        MS7J2UI.Rule("Category divider", card, MS7J2UI.PurpleLine, .156f, .16f, .158f, .84f);
        MS7J2UI.Label("Category title", card, Titles[index], 28, MS7J2UI.Pale,
            TextAlignmentOptions.Left, menuFont, .19f, .49f, .965f, .82f, true);
        MS7J2UI.Label("Category description", card, Details[index], 18, MS7J2UI.Muted,
            TextAlignmentOptions.Left, menuFont, .19f, .21f, .965f, .49f);
        var hover = card.gameObject.AddComponent<MS7J2CardHover>();
        hover.background = bg;
        hover.topRule = top.GetComponent<Image>();
        hover.accent = left.GetComponent<Image>();
    }

    private void LayoutNow()
    {
        if (!viewport || !grid) return;
        int width = Screen.width, height = Screen.height;
        if (width <= 0 || height <= 0) return;
        lastWidth = width; lastHeight = height;
        bool narrow = (float)width / height < 1.47f;
        int cols = narrow ? 1 : 2;
        grid.constraintCount = cols;
        float actualWidth = viewport.rect.width;
        if (actualWidth < 100f) actualWidth = 1660f;
        float usable = actualWidth - grid.padding.left - grid.padding.right - grid.spacing.x * (cols - 1);
        grid.cellSize = new Vector2(Mathf.Max(160f, usable / cols), narrow ? 148f : 142f);
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        Canvas.ForceUpdateCanvases();
    }
    private void Update()
    {
        if (closing || !dashboard) return;
        if (dashboard.gameObject.activeSelf && (lastWidth != Screen.width || lastHeight != Screen.height))
            LayoutNow();
        int slot = MS5A2GoldBank.SelectedSlot;
        if (slot != lastSelectedSlot)
        {
            if (activeModule < 0) RefreshSavedDisplay(true);
            else State = "PROFILE CHANGED: return to Hub and reopen module to verify selected Save";
        }
        if (activeModule < 0 && dashboard.gameObject.activeSelf && Time.unscaledTime >= nextSyncAt)
            RefreshSavedDisplay(false);
        if (activeModule >= 0 && Time.unscaledTime > openedAt + .2f)
        {
            // Covers original popup X even if its button is generated dynamically,
            // and never forcibly toggles a module while it is visible.
            if (!trackedPopup) trackedPopup = ResolveRealPopup(activeModule);
            if (trackedPopup)
            {
                bool visible = activeModule == 2 || activeModule == 3 || activeModule == 6 ?
                    IsRenderedUI(trackedPopup) : trackedPopup.activeInHierarchy;
                if (visible) popupWasVisible = true;
                else if (popupWasVisible) ReturnToHubInternal(false);
            }
            else if (Time.unscaledTime > openedAt + 1.25f)
            {
                int missing = activeModule;
                ReturnToHubInternal(false);
                if (missing >= 0 && missing < Titles.Length)
                    MS7J2UI.SetLabel(statusLabel, "MODULE UI NOT FOUND: " + Titles[missing] + " / CHECK MODULE INSTALL");
            }
        }
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape) && activeModule >= 0) ReturnToHub();
#endif
    }
    private void RefreshProfile()
    {
        int slot = MS5A2GoldBank.SelectedSlot;
        if (profileLabel) MS7J2UI.SetLabel(profileLabel, "PROFILE " + slot.ToString("00"));
    }
    private static int CountReady(MS7H1QuestView[] views)
    {
        int result = 0;
        if (views != null) foreach (var q in views) if (q != null && q.completed && !q.claimed) result++;
        return result;
    }
    private static int CountReady(MS7H2QuestView[] views)
    {
        int result = 0;
        if (views != null) foreach (var q in views) if (q != null && q.completed && !q.claimed) result++;
        return result;
    }
    private static int CountReady(MS7H3QuestView[] views)
    {
        int result = 0;
        if (views != null) foreach (var q in views) if (q != null && q.completed && !q.claimed) result++;
        return result;
    }
    // Read the real Save & service snapshots for the currently selected Profile.
    // Never writes or backfills SaveData, never invokes reward/claim operations.
    private void RefreshSavedDisplay(bool force)
    {
        if (!force && Time.unscaledTime < nextSyncAt) return;
        nextSyncAt = Time.unscaledTime + 2f;
        int slot = MS5A2GoldBank.SelectedSlot;
        lastSelectedSlot = slot;
        RefreshProfile();
        if (slot < 1 || slot > 3)
        {
            syncSummary = "SELECT A VALID PROFILE 01-03";
            MS7J2UI.SetLabel(statusLabel, syncSummary);
            return;
        }
        try
        {
            MS5A1SaveData data = null;
            string reason;
            bool backupOnly;
            bool valid = MS5A1SaveService.TryLoad(slot, out data, out reason, out backupOnly) && data != null;
            if (!valid || backupOnly)
            {
                syncSummary = "PROFILE " + slot.ToString("00") + " / " +
                    (backupOnly ? "BACKUP-ONLY SAVE; CLAIMS BLOCKED" : "NO VERIFIED PRIMARY SAVE") +
                    " / OPEN MANAGE SAVES";
                MS7J2UI.SetLabel(statusLabel, syncSummary);
                return;
            }
            var q1 = MS7H1QuestService.ReadSnapshot(slot);
            var q2 = MS7H2ProgressService.ReadSnapshot(slot);
            var q3 = MS7H3PeriodicQuestService.ReadSnapshot(slot);
            int n1 = q1.valid && !q1.backupRecovery ? CountReady(q1.quests) : 0;
            int n2 = q2.valid && !q2.backupRecovery ? CountReady(q2.quests) : 0;
            int n3 = q3.valid && !q3.backupRecovery && !q3.clockRollback ? CountReady(q3.quests) : 0;
            int pending = 0, reward = 0;
            MS7I4ProfileRecord record;
            string recordInfo;
            bool rOK = MS7I4RecordService.TrySnapshot(slot, out record, out pending, out reward, out recordInfo);
            syncSummary = "GOLD " + data.bankGold + "  /  REV " + data.revision +
                "  |  QUEST " + n1 + "  PROGRESS " + n2 + "  DAILY/WEEKLY " + n3 +
                "  |  ENDLESS " + (rOK ? "+" + reward : "--");
            MS7J2UI.SetLabel(statusLabel, syncSummary);
        }
        catch (Exception e)
        {
            syncSummary = "READ FAILED / PROFILE " + slot.ToString("00") + " / CHECK CONSOLE";
            MS7J2UI.SetLabel(statusLabel, syncSummary);
            Debug.LogWarning("[7J.2.1] Hub read-only refresh: " + e.Message, this);
        }
    }
    private Button FindButton(string exactName)
    {
        Scene scene = SceneManager.GetSceneByName(hostSceneName);
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Button b in root.GetComponentsInChildren<Button>(true))
                if (b.name == exactName) return b;
        return null;
    }
    private GameObject FindPopup(string exactName)
    {
        Scene scene = SceneManager.GetSceneByName(hostSceneName);
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (RectTransform t in root.GetComponentsInChildren<RectTransform>(true))
                if (t.name == exactName) return t.gameObject;
        return null;
    }
    // Never treat MS5C4_AchievementGallery / MS5D1_ProfileOverview
    // as a popup. In the actual MainMenu scene these are service-only nodes
    // (no graphics/children at edit time), whereas the real windows are
    // generated at runtime and can have a different name.
    private static bool IsRenderedUI(GameObject go)
    {
        if (!go || !go.activeInHierarchy) return false;
        Graphic[] graphics = go.GetComponentsInChildren<Graphic>(true);
        foreach (Graphic g in graphics)
        {
            if (!g || !g.isActiveAndEnabled || !g.gameObject.activeInHierarchy) continue;
            if (g.color.a < .04f) continue;
            bool hidden = false;
            for (Transform t = g.transform; t != null; t = t.parent)
            {
                CanvasGroup cg = t.GetComponent<CanvasGroup>();
                if (cg && cg.alpha < .04f) { hidden = true; break; }
            }
            if (!hidden) return true;
        }
        return false;
    }
    private GameObject ResolveRealPopup(int index)
    {
        if (index < 0 || index >= Launchers.Length) return null;
        if (index != 2 && index != 3)
            return FindPopup(TrackingNames[index]);

        // 5C4/5D1: look INSIDE each service for an actual live UI subtree first.
        string serviceName = index == 2 ? "MS5C4_AchievementGallery" : "MS5D1_ProfileOverview";
        GameObject service = FindPopup(serviceName);
        if (service)
        {
            foreach (Transform t in service.transform)
                if (IsRenderedUI(t.gameObject)) return t.gameObject;
        }
        // Some versions create a sibling modal under the main Canvas.
        Scene main = SceneManager.GetSceneByName(hostSceneName);
        if (!main.IsValid() || !main.isLoaded) return null;
        GameObject best = null;
        int bestScore = -1;
        foreach (GameObject root in main.GetRootGameObjects())
        foreach (RectTransform t in root.GetComponentsInChildren<RectTransform>(true))
        {
            string n = t.name.ToLowerInvariant();
            if (t.gameObject == service || !IsRenderedUI(t.gameObject)) continue;
            bool kind = index == 2 ? (n.Contains("achiev") || n.Contains("gallery")) :
                (n.Contains("profile") || n.Contains("overview") || n.Contains("statistic"));
            if (!kind) continue;
            bool frame = n.Contains("overlay") || n.Contains("modal") || n.Contains("panel") ||
                         n.Contains("window") || n.Contains("gallery") || n.Contains("popup") ||
                         n.Contains("dialog") || n.Contains("screen") ||
                         (t.GetComponent<Canvas>() && kind);
            if (!frame || n.Contains("launcher") || n.Contains("btn_") || n.Contains("button") ||
                n.Contains("profilecard") || n.Contains("widget")) continue;
            int graphicsCount = t.GetComponentsInChildren<Graphic>(true).Length;
            if (graphicsCount < 2) continue;
            int score = graphicsCount + (n.Contains("overlay") ? 100 : 0) + (n.Contains("modal") ? 100 : 0);
            if (score > bestScore) { bestScore = score; best = t.gameObject; }
        }
        return best;
    }
    private static void ClearUnitySelection()
    {
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    }
    private void ClearCardHighlights()
    {
        if (!content) return;
        foreach (MS7J2CardHover hover in content.GetComponentsInChildren<MS7J2CardHover>(true))
            if (hover) hover.ClearVisual();
        ClearUnitySelection();
    }
    private MS7I4RecordsMenu FindRecordsMenu()
    {
        Scene main = SceneManager.GetSceneByName(hostSceneName);
        if (!main.IsValid() || !main.isLoaded) return null;
        foreach (GameObject root in main.GetRootGameObjects())
        {
            MS7I4RecordsMenu records = root.GetComponentInChildren<MS7I4RecordsMenu>(true);
            if (records) return records;
        }
        return null;
    }
    private bool OpenEndlessNativeIfNeeded()
    {
        GameObject modal = FindPopup("7I4_Records_Modal");
        if (modal && modal.activeInHierarchy) return true;
        MS7I4RecordsMenu records = FindRecordsMenu();
        if (!records || !records.enabled || !records.Built) return false;
        // Only invokes the original UI-only Open() method; never Claim().
        MethodInfo open = typeof(MS7I4RecordsMenu).GetMethod("Open", BindingFlags.Instance | BindingFlags.NonPublic);
        if (open == null || open.GetParameters().Length != 0) return false;
        try { open.Invoke(records, null); }
        catch (Exception e) { Debug.LogWarning("[7J.2.3] Records open failed: " + e.Message, this); }
        modal = FindPopup("7I4_Records_Modal");
        return modal && modal.activeInHierarchy;
    }
    private void PromotePopup(int index)
    {
        if (index < 0 || index >= OriginalPopupNames.Length) return;
        string name = OriginalPopupNames[index];
        if (string.IsNullOrEmpty(name)) return;
        GameObject go = FindPopup(name);
        if (!go) return;
        Canvas c = go.GetComponent<Canvas>();
        if (!c) { c = go.AddComponent<Canvas>(); addedCanvas.Add(c); }
        else if (!previousSorting.ContainsKey(c))
        {
            previousSorting[c] = c.sortingOrder;
            previousOverride[c] = c.overrideSorting;
        }
        c.overrideSorting = true;
        c.sortingOrder = 260;
        GraphicRaycaster r = go.GetComponent<GraphicRaycaster>();
        if (!r) { r = go.AddComponent<GraphicRaycaster>(); addedRaycasters.Add(r); }
    }
    private void RestorePopupCanvases()
    {
        foreach (var pair in previousSorting)
            if (pair.Key) { pair.Key.sortingOrder = pair.Value; pair.Key.overrideSorting = previousOverride[pair.Key]; }
        previousSorting.Clear();
        previousOverride.Clear();
        foreach (var r in addedRaycasters) if (r) Destroy(r);
        addedRaycasters.Clear();
        foreach (var c in addedCanvas) if (c) Destroy(c);
        addedCanvas.Clear();
    }
    private static bool IsCloseButton(Button b)
    {
        return b && (b.name == "Close" || b.name == "BTN_Close" || b.name == "CloseButton" ||
                     b.name == "Close Button" || b.name == "Exit" || b.name == "X");
    }
    private void HookCloseButtons()
    {
        RemoveCloseHooks();
        if (!trackedPopup) return;
        closeCallback = OnOriginalCloseClicked;
        foreach (Button b in trackedPopup.GetComponentsInChildren<Button>(true))
        {
            if (!IsCloseButton(b)) continue;
            b.onClick.AddListener(closeCallback);
            closeHooks.Add(b);
        }
    }
    private void RemoveCloseHooks()
    {
        if (closeCallback != null)
            foreach (Button b in closeHooks) if (b) b.onClick.RemoveListener(closeCallback);
        closeHooks.Clear();
        closeCallback = null;
    }
    private void OnOriginalCloseClicked()
    {
        // Let the original service's Close/X listener run first and complete the UI transition.
        if (activeModule >= 0 && !closing && !returning)
            StartCoroutine(ReturnAfterOriginalClose());
    }
    private IEnumerator ReturnAfterOriginalClose()
    {
        yield return null;
        if (activeModule >= 0 && !closing) ReturnToHubInternal(false);
    }
    private void OpenModule(int index)
    {
        if (closing || openingRoutine != null || activeModule >= 0 || !dashboard ||
            !dashboard.gameObject.activeSelf || index < 0 || index >= Launchers.Length) return;
        Button launcher = FindButton(Launchers[index]);
        if (!launcher)
        {
            launchDetail = "MISSING LAUNCHER " + Launchers[index];
            State = launchDetail;
            MS7J2UI.SetLabel(statusLabel, launchDetail);
            ClearCardHighlights();
            Debug.LogWarning("[7J.2.3] " + State, this);
            return;
        }
        // Preserve the already-working 7J.2.1 flow for tabs 01/02/05/06.
        // Their service roots may not contain a visible Graphic, and imposing a
        // visual probe here would incorrectly block working tabs.
        if (index != 2 && index != 3 && index != 6)
        {
            ClearCardHighlights();
            PromotePopup(index);
            shield.gameObject.SetActive(true);
            dashboard.gameObject.SetActive(false);
            returnBar.gameObject.SetActive(true);
            activeModule = index;
            openedAt = Time.unscaledTime;
            try { launcher.onClick.Invoke(); }
            catch (Exception e)
            {
                Debug.LogError("[7J.2.3] Existing module open failed: " + e, this);
                ReturnToHubInternal(false);
                return;
            }
            trackedPopup = FindPopup(TrackingNames[index]);
            popupWasVisible = trackedPopup && trackedPopup.activeInHierarchy;
            HookCloseButtons();
            launchDetail = "OPENED EXISTING TAB " + (index + 1).ToString("00");
            State = launchDetail;
            return;
        }
        openingRoutine = StartCoroutine(OpenModuleChecked(index, launcher));
    }
    private IEnumerator OpenModuleChecked(int index, Button launcher)
    {
        ClearCardHighlights();
        // Achievements/Profile legacy launchers require an active Home context;
        // Endless Records is owned by Map Select. We do not change the Save/profile.
        if (host && host.menu)
        {
            if (index == 6)
            {
                if (host.menu.homePage) host.menu.homePage.SetActive(false);
                if (host.menu.mapsPage) host.menu.mapsPage.SetActive(true);
                if (host.menu.settingsPage) host.menu.settingsPage.SetActive(false);
            }
            else if (index == 2 || index == 3)
            {
                if (host.menu.homePage) host.menu.homePage.SetActive(true);
                if (host.menu.mapsPage) host.menu.mapsPage.SetActive(false);
                if (host.menu.settingsPage) host.menu.settingsPage.SetActive(false);
            }
        }
        // Legacy 7J1 used SetActive(false) for these launchers; ensure they
        // can receive their original OnClick actions without being visible.
        if (!launcher.gameObject.activeSelf) launcher.gameObject.SetActive(true);
        try { launcher.onClick.Invoke(); }
        catch (Exception e)
        {
            launchDetail = "ORIGINAL OPEN EXCEPTION " + e.Message;
            Debug.LogError("[7J.2.3] " + e, this);
        }
        // Give dynamically-created legacy overlays / fade scripts time to open.
        GameObject popup = null;
        // Force one frame of coroutine suspension; avoid a synchronously
        // completed StartCoroutine leaving openingRoutine permanently non-null.
        yield return null;
        float deadline = Time.unscaledTime + .65f;
        do
        {
            if (index == 6) OpenEndlessNativeIfNeeded();
            popup = ResolveRealPopup(index);
            if (popup && IsRenderedUI(popup)) break;
            yield return null;
        } while (Time.unscaledTime < deadline && dashboard && dashboard.gameObject.activeSelf);

        if (!popup || !IsRenderedUI(popup))
        {
            launchDetail = "OPEN FAILED TAB " + (index + 1).ToString("00") +
                ": NO VISIBLE POPUP. Check original module UI / Console.";
            State = launchDetail;
            MS7J2UI.SetLabel(statusLabel, launchDetail);
            Debug.LogWarning("[7J.2.3] " + launchDetail, this);
            // Crucial: leave dashboard visible instead of a black shield.
            if (host) host.RestoreHomePage();
            if (shield) shield.gameObject.SetActive(false);
            if (returnBar) returnBar.gameObject.SetActive(false);
            ClearCardHighlights();
            openingRoutine = null;
            yield break;
        }
        activeModule = index;
        trackedPopup = popup;
        popupWasVisible = true;
        openedAt = Time.unscaledTime;
        // Original 5C4/5D1 windows may use nested canvases or a runtime modal;
        // never add a Canvas to the service-only root that has NO graphics.
        if (index != 2 && index != 3) PromotePopup(index);
        // The original 5C4/5D1 renderer can be on MainMenu Canvas order 0:
        // do not put the Hub's opaque overlay in front of it.
        shield.gameObject.SetActive(index != 2 && index != 3);
        dashboard.gameObject.SetActive(false);
        returnBar.gameObject.SetActive(true);
        HookCloseButtons();
        launchDetail = "OPENED TAB " + (index + 1).ToString("00") +
            " POPUP=" + popup.name + " GRAPHICS=" + popup.GetComponentsInChildren<Graphic>(true).Length;
        State = launchDetail;
        Debug.Log("[7J.2.3] " + launchDetail, this);
        openingRoutine = null;
    }
    private void RequestNativePopupClose()
    {
        if (activeModule < 0) return;
        GameObject popup = trackedPopup ? trackedPopup : FindPopup(TrackingNames[activeModule]);
        if (!popup || !popup.activeInHierarchy) return;
        // Prefer each module's original Close handler: some modules own an opening flag.
        foreach (var b in popup.GetComponentsInChildren<Button>(true))
        {
            if (IsCloseButton(b) && b.gameObject.activeInHierarchy)
            {
                b.onClick.Invoke();
                return;
            }
        }
        // Last resort: hide only the popup, never any Save/Quest service component.
        popup.SetActive(false);
    }
    public void ReturnToHub()
    {
        ReturnToHubInternal(true);
    }
    private void ReturnToHubInternal(bool requestClose)
    {
        if (!dashboard || !host || closing || returning) return;
        if (openingRoutine != null) { StopCoroutine(openingRoutine); openingRoutine = null; }
        returning = true;
        ClearCardHighlights();
        RemoveCloseHooks();
        if (requestClose) RequestNativePopupClose();
        activeModule = -1;
        trackedPopup = null;
        popupWasVisible = false;
        RestorePopupCanvases();
        // Restore parent page before showing the dashboard; eliminates blank backdrop.
        host.RestoreHomePage();
        shield.gameObject.SetActive(false);
        returnBar.gameObject.SetActive(false);
        dashboard.gameObject.SetActive(true);
        ClearCardHighlights();
        RefreshSavedDisplay(true);
        LayoutNow();
        State = "READY: 7J.2.3 FIXED HUB";
        returning = false;
    }
    public void CloseHub()
    {
        if (!closing) StartCoroutine(Unload());
    }
    private IEnumerator Unload()
    {
        closing = true;
        if (openingRoutine != null) { StopCoroutine(openingRoutine); openingRoutine = null; }
        ClearCardHighlights();
        RemoveCloseHooks();
        RequestNativePopupClose();
        activeModule = -1;
        RestorePopupCanvases();
        if (host) host.RestoreHomePage();
        if (shield) shield.gameObject.SetActive(false);
        AsyncOperation op = SceneManager.UnloadSceneAsync(gameObject.scene);
        if (op != null) while (!op.isDone) yield return null;
        closing = false;
    }
    private void OnDestroy() { RemoveCloseHooks(); RestorePopupCanvases(); }
}
