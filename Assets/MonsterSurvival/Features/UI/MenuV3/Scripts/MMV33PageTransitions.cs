using UnityEngine;

// V3.3 - presentation-only page enter animations.
// Never changes Button.onClick, scene loading, map choice or sound settings.
// Does not require edits to MMV3MenuController.
[DisallowMultipleComponent]
[DefaultExecutionOrder(850)]
public sealed class MMV33PageTransitions : MonoBehaviour
{
    [Header("Existing V3 controller")]
    public MMV3MenuController menu;

    [Header("Page enter animation")]
    [Range(0.12f, 0.8f)] public float enterDuration = 0.28f;
    [Range(0f, 40f)] public float risePixels = 18f;
    public bool animateInitialHomePage = true;

    private sealed class Page
    {
        public GameObject go;
        public RectTransform rect;
        public CanvasGroup group;
        public bool groupAddedByUs;
        public Vector2 basePosition;
        public float originalAlpha;
        public bool originalInteractable;
        public bool originalBlocksRaycasts;
    }

    private Page[] pages;
    private int currentPage = -1;
    private float elapsed;
    private bool initialized;
    private bool animating;

    private void Start()
    {
        if (menu == null) menu = GetComponent<MMV3MenuController>();
        if (menu == null)
        {
            Debug.LogWarning("[6B V3.3] MMV3MenuController is missing. Effects disabled.", this);
            enabled = false;
            return;
        }

        pages = new Page[] {
            MakePage(menu.homePage),
            MakePage(menu.mapsPage),
            MakePage(menu.settingsPage)
        };
        initialized = true;
        int initial = ActivePageIndex();
        if (initial >= 0)
        {
            currentPage = initial;
            if (animateInitialHomePage) StartEnter(initial);
        }
    }

    private static Page MakePage(GameObject go)
    {
        if (go == null) return null;
        RectTransform rt = go.transform as RectTransform;
        if (rt == null) return null;
        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        bool added = cg == null;
        if (added) cg = go.AddComponent<CanvasGroup>();
        return new Page {
            go = go, rect = rt, group = cg, groupAddedByUs = added,
            basePosition = rt.anchoredPosition, originalAlpha = cg.alpha,
            originalInteractable = cg.interactable,
            originalBlocksRaycasts = cg.blocksRaycasts
        };
    }

    private int ActivePageIndex()
    {
        if (pages == null) return -1;
        for (int i = 0; i < pages.Length; i++)
            if (pages[i] != null && pages[i].go != null && pages[i].go.activeInHierarchy)
                return i;
        return -1;
    }

    private void LateUpdate()
    {
        if (!initialized) return;
        int now = ActivePageIndex();
        if (now != currentPage)
        {
            if (currentPage >= 0) RestorePage(pages[currentPage]);
            currentPage = now;
            if (now >= 0) StartEnter(now);
            else animating = false;
        }

        if (!animating || currentPage < 0) return;
        Page p = pages[currentPage];
        if (p == null || p.group == null || p.rect == null)
        {
            animating = false;
            return;
        }
        elapsed += Time.unscaledDeltaTime;
        float f = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, enterDuration));
        // Smoothstep ease-out: little motion at the end.
        float eased = f * f * (3f - 2f * f);
        p.group.alpha = Mathf.Lerp(0f, p.originalAlpha, eased);
        p.rect.anchoredPosition = p.basePosition + Vector2.down * (risePixels * (1f - eased));
        if (f >= 1f)
        {
            animating = false;
            RestorePage(p);
        }
    }

    private void StartEnter(int index)
    {
        Page p = pages[index];
        if (p == null || p.group == null || p.rect == null) return;
        elapsed = 0f;
        animating = true;
        p.group.alpha = 0f;
        // New pages do not receive mouse clicks until their entrance finishes.
        p.group.interactable = false;
        p.group.blocksRaycasts = false;
        p.rect.anchoredPosition = p.basePosition + Vector2.down * risePixels;
    }

    private static void RestorePage(Page p)
    {
        if (p == null) return;
        if (p.rect != null) p.rect.anchoredPosition = p.basePosition;
        if (p.group == null) return;
        p.group.alpha = p.originalAlpha;
        p.group.interactable = p.originalInteractable;
        p.group.blocksRaycasts = p.originalBlocksRaycasts;
    }

    private void OnDisable() { RestoreAll(); }
    private void OnDestroy() { RestoreAll(); }

    private void RestoreAll()
    {
        if (pages == null) return;
        foreach (Page p in pages)
        {
            if (p == null) continue;
            RestorePage(p);
            if (p.groupAddedByUs && p.group != null)
            {
                // Components created at runtime are temporary and do not alter Scene assets.
                if (Application.isPlaying) Destroy(p.group);
                else DestroyImmediate(p.group);
            }
        }
        pages = null;
        initialized = false;
    }
}
