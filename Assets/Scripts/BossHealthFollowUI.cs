
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class BossHealthFollowUI : MonoBehaviour
{
    [Header("Position Settings")]
    public float heightAboveBoss = 0.6f;
    public float screenYOffset = 0f;

    [Header("Search Settings")]
    public float searchInterval = 0.5f;

    private RectTransform panelRect;
    private RectTransform parentRect;
    private Canvas parentCanvas;

    private Transform bossTransform;
    private SpriteRenderer bossSprite;

    private Camera gameCamera;
    private float nextSearchTime;

    private void Awake()
    {
        panelRect = GetComponent<RectTransform>();

        parentRect = transform.parent as RectTransform;

        parentCanvas = GetComponentInParent<Canvas>();

        gameCamera = Camera.main;
    }

    private void OnEnable()
    {
        nextSearchTime = 0f;
    }

    private void LateUpdate()
    {
        // Tim Boss khi Boss xuat hien o Wave 5
        if (bossTransform == null)
        {
            if (Time.unscaledTime < nextSearchTime)
                return;

            nextSearchTime = Time.unscaledTime
                + Mathf.Max(0.1f, searchInterval);

            BossSpecialAttack boss =
                FindFirstObjectByType<BossSpecialAttack>();

            if (boss != null)
            {
                bossTransform = boss.transform;

                bossSprite =
                    boss.GetComponent<SpriteRenderer>();
            }
        }

        if (bossTransform == null)
            return;

        if (gameCamera == null)
        {
            gameCamera = Camera.main;
        }

        if (gameCamera == null || parentRect == null)
            return;

        // Lay diem ngay phia tren dau Boss
        Vector3 worldPosition = bossTransform.position;

        if (bossSprite != null)
        {
            worldPosition.y =
                bossSprite.bounds.max.y
                + heightAboveBoss;
        }
        else
        {
            worldPosition.y += heightAboveBoss;
        }

        // Chuyen toa do the gioi sang man hinh
        Vector3 screenPosition =
            gameCamera.WorldToScreenPoint(worldPosition);

        if (screenPosition.z <= 0f)
            return;

        screenPosition.y += screenYOffset;

        // Xac dinh camera dung cho UI
        Camera uiCamera = null;

        if (parentCanvas != null &&
            parentCanvas.renderMode !=
            RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = parentCanvas.worldCamera;

            if (uiCamera == null)
            {
                uiCamera = gameCamera;
            }
        }

        // Chuyen toa do man hinh sang UI
        Vector2 localPosition;

        bool success =
            RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    parentRect,
                    screenPosition,
                    uiCamera,
                    out localPosition
                );

        if (!success)
            return;

        panelRect.anchoredPosition = localPosition;
    }
}
