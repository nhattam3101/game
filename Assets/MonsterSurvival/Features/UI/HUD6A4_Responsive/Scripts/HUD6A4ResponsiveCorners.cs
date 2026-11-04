using UnityEngine;

// 6A.4 - Display/layout ONLY. Does not read or modify any gameplay state.
// Attach to HUD6A2_Minimal (the overlay Canvas root).
[DisallowMultipleComponent]
public sealed class HUD6A4ResponsiveCorners : MonoBehaviour
{
    [Header("HUD 6A.2 and 6A.3 Objects")]
    public RectTransform playerStatus;
    public RectTransform gameStatus6A3;

    [Header("Small-screen behavior")]
    [Tooltip("Minimum empty horizontal gap between the two corner widgets, in Canvas units.")]
    [Min(0f)] public float minimumGap = 20f;
    [Tooltip("Vertical gap when the right widget must move below the left widget.")]
    [Min(0f)] public float stackedGap = 14f;
    [Range(0.6f, 1f)] public float smallestStackScale = 0.84f;
    public bool respectScreenSafeArea = true;

    private RectTransform hudRoot;
    private Vector2 leftOriginalAnchor;
    private Vector2 rightOriginalAnchor;
    private Vector2 leftOriginalPosition;
    private Vector2 rightOriginalPosition;
    private Vector3 leftOriginalScale;
    private Vector3 rightOriginalScale;
    private bool cached;
    private int screenWidth = -1;
    private int screenHeight = -1;
    private Rect previousSafeArea;
    private Vector2 previousRootSize;

    private void Awake()
    {
        FindTargets();
        CaptureDefaults();
    }

    private void OnEnable()
    {
        FindTargets();
        CaptureDefaults();
        ApplyLayout();
    }

    private void LateUpdate()
    {
        if (!cached || hudRoot == null || playerStatus == null || gameStatus6A3 == null)
            return;

        Rect safeArea = respectScreenSafeArea
            ? Screen.safeArea
            : new Rect(0f, 0f, Screen.width, Screen.height);
        Vector2 rootSize = hudRoot.rect.size;

        if (screenWidth != Screen.width || screenHeight != Screen.height ||
            previousSafeArea != safeArea || previousRootSize != rootSize)
            ApplyLayout();
    }

    private void FindTargets()
    {
        if (hudRoot == null) hudRoot = GetComponent<RectTransform>();
        if (playerStatus == null)
        {
            Transform found = transform.Find("PlayerStatus");
            if (found != null) playerStatus = found.GetComponent<RectTransform>();
        }
        if (gameStatus6A3 == null)
        {
            Transform found = transform.Find("GameStatus_6A3");
            if (found != null) gameStatus6A3 = found.GetComponent<RectTransform>();
        }
    }

    private void CaptureDefaults()
    {
        if (cached || playerStatus == null || gameStatus6A3 == null) return;
        leftOriginalAnchor = playerStatus.anchorMin;
        rightOriginalAnchor = gameStatus6A3.anchorMin;
        leftOriginalPosition = playerStatus.anchoredPosition;
        rightOriginalPosition = gameStatus6A3.anchoredPosition;
        leftOriginalScale = playerStatus.localScale;
        rightOriginalScale = gameStatus6A3.localScale;
        cached = true;
    }

    public void ApplyLayout()
    {
        if (!cached || hudRoot == null) return;
        if (Screen.width <= 0 || Screen.height <= 0) return;

        Rect safe = respectScreenSafeArea
            ? Screen.safeArea
            : new Rect(0f, 0f, Screen.width, Screen.height);
        float rootWidth = Mathf.Max(1f, hudRoot.rect.width);
        float rootHeight = Mathf.Max(1f, hudRoot.rect.height);
        float leftInset = (safe.xMin / Screen.width) * rootWidth;
        float rightInset = ((Screen.width - safe.xMax) / Screen.width) * rootWidth;
        float topInset = ((Screen.height - safe.yMax) / Screen.height) * rootHeight;
        float safeWidth = rootWidth - leftInset - rightInset;

        // Maintain original corner anchors so existing layout/font dimensions stay untouched.
        playerStatus.anchorMin = leftOriginalAnchor;
        playerStatus.anchorMax = leftOriginalAnchor;
        gameStatus6A3.anchorMin = rightOriginalAnchor;
        gameStatus6A3.anchorMax = rightOriginalAnchor;

        float leftW = playerStatus.rect.width * leftOriginalScale.x;
        float rightW = gameStatus6A3.rect.width * rightOriginalScale.x;
        float leftMargin = Mathf.Max(0f, leftOriginalPosition.x);
        float rightMargin = Mathf.Max(0f, -rightOriginalPosition.x);
        bool stack = safeWidth < leftW + rightW + leftMargin + rightMargin + minimumGap;

        playerStatus.localScale = leftOriginalScale;
        gameStatus6A3.localScale = rightOriginalScale;
        playerStatus.anchoredPosition = leftOriginalPosition + new Vector2(leftInset, -topInset);

        if (stack)
        {
            // Keep both cards at the top, leaving the central gameplay area unobstructed.
            float maxCardWidth = Mathf.Max(leftW + leftMargin, rightW + rightMargin);
            float scale = Mathf.Clamp((safeWidth - 18f) / Mathf.Max(1f, maxCardWidth),
                smallestStackScale, 1f);
            playerStatus.localScale = Vector3.Scale(leftOriginalScale, new Vector3(scale, scale, 1f));
            gameStatus6A3.localScale = Vector3.Scale(rightOriginalScale, new Vector3(scale, scale, 1f));

            float yBelowLeft = leftOriginalPosition.y - topInset -
                playerStatus.rect.height * leftOriginalScale.y * scale - stackedGap;
            gameStatus6A3.anchoredPosition = new Vector2(
                rightOriginalPosition.x - rightInset, yBelowLeft);
        }
        else
        {
            gameStatus6A3.anchoredPosition = rightOriginalPosition +
                new Vector2(-rightInset, -topInset);
        }

        screenWidth = Screen.width;
        screenHeight = Screen.height;
        previousSafeArea = safe;
        previousRootSize = hudRoot.rect.size;
    }

    private void OnDisable()
    {
        // Returning to edit mode / disabling the component should not persist an altered layout.
        if (!cached || playerStatus == null || gameStatus6A3 == null) return;
        playerStatus.anchorMin = leftOriginalAnchor;
        playerStatus.anchorMax = leftOriginalAnchor;
        gameStatus6A3.anchorMin = rightOriginalAnchor;
        gameStatus6A3.anchorMax = rightOriginalAnchor;
        playerStatus.anchoredPosition = leftOriginalPosition;
        gameStatus6A3.anchoredPosition = rightOriginalPosition;
        playerStatus.localScale = leftOriginalScale;
        gameStatus6A3.localScale = rightOriginalScale;
    }
}
