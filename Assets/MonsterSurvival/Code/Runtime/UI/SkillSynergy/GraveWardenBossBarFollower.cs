using UnityEngine;

// Attach to the UI RectTransform that represents the whole boss health bar.
// Only follows The Grave Warden. Shadow Titan keeps its original UI behaviour.
[RequireComponent(typeof(RectTransform))]
public class GraveWardenBossBarFollower : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform bossBarRoot;
    [SerializeField] private Canvas parentCanvas;
    [SerializeField] private Camera worldCamera;

    [Header("Position")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.3f, 0f);
    [SerializeField, Min(0.1f)] private float targetSearchInterval = 0.35f;
    [SerializeField] private bool restoreOriginalPositionWhenBossMissing = true;

    private GraveWardenBossAI graveWarden;
    private Vector3 initialWorldPosition;
    private bool originalPositionStored;
    private float nextSearchTime;
    private bool wasFollowing;

    private void Awake()
    {
        if (bossBarRoot == null)
            bossBarRoot = GetComponent<RectTransform>();
        if (parentCanvas == null)
            parentCanvas = GetComponentInParent<Canvas>();
        if (worldCamera == null)
            worldCamera = Camera.main;
    }

    private void OnEnable()
    {
        // Storing the layout position when enabled also supports Retry / reloading the scene.
        if (bossBarRoot != null)
        {
            initialWorldPosition = bossBarRoot.position;
            originalPositionStored = true;
        }
        graveWarden = null;
        wasFollowing = false;
        nextSearchTime = 0f;
    }

    private void LateUpdate()
    {
        if (bossBarRoot == null || parentCanvas == null) return;
        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera == null) return;

        if (graveWarden == null && Time.unscaledTime >= nextSearchTime)
        {
            // This only searches periodically, not on every frame.
            graveWarden = FindAnyObjectByType<GraveWardenBossAI>();
            nextSearchTime = Time.unscaledTime + targetSearchInterval;
        }

        if (graveWarden == null)
        {
            if (wasFollowing && restoreOriginalPositionWhenBossMissing && originalPositionStored)
                bossBarRoot.position = initialWorldPosition;
            wasFollowing = false;
            return;
        }

        RectTransform parentRect = bossBarRoot.parent as RectTransform;
        if (parentRect == null) return;

        Vector3 screenPoint = worldCamera.WorldToScreenPoint(graveWarden.transform.position + worldOffset);
        if (screenPoint.z <= 0f) return;

        Camera uiCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : parentCanvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, screenPoint, uiCamera, out Vector2 point))
        {
            // This works even when CanvasScaler changes the UI scale.
            bossBarRoot.position = parentRect.TransformPoint(point);
            wasFollowing = true;
        }
    }
}
