using UnityEngine;
using UnityEngine.UI;
using TMPro;

// MonsterSurvival - Boss Health Bar (Unity 6.6)
// Keeps the original HP/name/events and follows the bound boss.
// Supports both Grave Warden and Shadow Titan with different offsets.
public class BossHealthBarUI : MonoBehaviour
{
    [Header("Boss UI - Existing References")]
    public GameObject bossHealthPanel;
    public TMP_Text bossNameText;
    public TMP_Text bossHPText;
    public Image bossHPFill;

    [Header("Boss - Visibility by Type")]
    [Tooltip("OFF: Grave Warden uses its new purple/gold HP bar only. Shadow Titan remains unchanged.")]
    public bool showForGraveWarden = false;

    [Header("Boss - Follow Position")]
    [Tooltip("Follow Grave Warden only when Show For Grave Warden is enabled.")]
    public bool followGraveWarden = true;

    [Tooltip("Allow the health bar to follow Shadow Titan and any other boss bound by WaveManager.")]
    public bool followOtherBosses = true;

    [Tooltip("Offset in world units measured from the boss position.")]
    public Vector3 followWorldOffset = new Vector3(0f, 1.5f, 0f);

    [Tooltip("Offset for Shadow Titan / other bosses. Larger bosses usually need a larger Y offset.")]
    public Vector3 otherBossWorldOffset = new Vector3(0f, 2.5f, 0f);

    [Tooltip("Camera showing the world. Leave empty to use Main Camera.")]
    public Camera worldCamera;

    private Health currentBoss;
    private RectTransform panelRect;
    private Canvas parentCanvas;
    private Vector3 initialLocalPosition;
    private bool hasInitialPosition;
    private bool followCurrentBoss;
    private Vector3 activeWorldOffset;

    private void Awake()
    {
        CacheUIReferences();

        if (bossHealthPanel != null)
            bossHealthPanel.SetActive(false);
    }

    private void CacheUIReferences()
    {
        if (bossHealthPanel == null)
            return;

        if (panelRect == null)
            panelRect = bossHealthPanel.GetComponent<RectTransform>();

        if (parentCanvas == null)
            parentCanvas = bossHealthPanel.GetComponentInParent<Canvas>();

        if (panelRect != null && !hasInitialPosition)
        {
            initialLocalPosition = panelRect.localPosition;
            hasInitialPosition = true;
        }
    }

    // This method is called by WaveManager whenever a boss is spawned.
    public void BindBoss(Health boss, string bossName)
    {
        UnbindBoss();
        CacheUIReferences();

        if (boss == null)
        {
            Debug.LogWarning("BossHealthBarUI: Boss khong co Health!");
            return;
        }

        // Gate visibility BEFORE event subscriptions and SetActive(true).
        // The old red UI must not appear for Grave Warden, even when another
        // system calls BindBoss directly rather than going through WaveManager.
        bool isGraveWarden = boss.GetComponentInParent<GraveWardenBossAI>() != null;
        if (isGraveWarden && !showForGraveWarden)
        {
            // UnbindBoss() above has already hidden the old red panel.
            return;
        }

        currentBoss = boss;
        currentBoss.OnHealthChanged += UpdateHealth;
        currentBoss.OnDied += OnBossDied;

        // Use the bound boss, NOT a global search. This avoids following the wrong enemy.
        followCurrentBoss = isGraveWarden ? followGraveWarden : followOtherBosses;
        activeWorldOffset = isGraveWarden ? followWorldOffset : otherBossWorldOffset;

        if (bossNameText != null)
            bossNameText.text = bossName;

        if (bossHealthPanel != null)
            bossHealthPanel.SetActive(true);

        UpdateHealth(currentBoss.CurrentHealth, currentBoss.maxHealth);

        // Avoid displaying at the previous fixed position for one frame.
        if (followCurrentBoss)
            UpdateFollowPosition();
    }

    private void LateUpdate()
    {
        if (followCurrentBoss && currentBoss != null)
            UpdateFollowPosition();
    }

    private void UpdateFollowPosition()
    {
        if (currentBoss == null)
            return;

        CacheUIReferences();
        if (panelRect == null || parentCanvas == null)
            return;

        RectTransform parentRect = panelRect.parent as RectTransform;
        if (parentRect == null)
            return;

        Camera gameplayCamera = worldCamera != null ? worldCamera : Camera.main;
        if (gameplayCamera == null)
            return;

        Vector3 bossPosition = currentBoss.transform.position + activeWorldOffset;
        Vector3 screenPosition = gameplayCamera.WorldToScreenPoint(bossPosition);
        if (screenPosition.z <= 0f)
            return;

        // Screen Space Overlay requires null here; Screen Space Camera needs canvas camera.
        Camera canvasCamera = null;
        if (parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            canvasCamera = parentCanvas.worldCamera != null
                ? parentCanvas.worldCamera
                : gameplayCamera;

        Vector2 localPoint;
        bool converted = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            new Vector2(screenPosition.x, screenPosition.y),
            canvasCamera,
            out localPoint
        );

        if (converted)
            panelRect.position = parentRect.TransformPoint(localPoint);
    }

    // Keep the existing UI update logic unchanged.
    private void UpdateHealth(float currentHealth, float maxHealth)
    {
        float ratio = 0f;
        if (maxHealth > 0f)
            ratio = Mathf.Clamp01(currentHealth / maxHealth);

        if (bossHPFill != null)
        {
            bossHPFill.type = Image.Type.Filled;
            bossHPFill.fillMethod = Image.FillMethod.Horizontal;
            bossHPFill.fillOrigin = 0;
            bossHPFill.fillAmount = ratio;
        }

        if (bossHPText != null)
        {
            int current = Mathf.CeilToInt(Mathf.Max(0f, currentHealth));
            int maximum = Mathf.CeilToInt(Mathf.Max(0f, maxHealth));
            bossHPText.text = current + " / " + maximum + " HP";
        }
    }

    private void OnBossDied()
    {
        Debug.Log("BossHealthBarUI: Boss da bi tieu diet!");
        UnbindBoss();
    }

    public void UnbindBoss()
    {
        if (currentBoss != null)
        {
            currentBoss.OnHealthChanged -= UpdateHealth;
            currentBoss.OnDied -= OnBossDied;
        }

        currentBoss = null;
        followCurrentBoss = false;

        // Restore the original panel location before the next boss binding.
        if (panelRect != null && hasInitialPosition)
            panelRect.localPosition = initialLocalPosition;

        if (bossHealthPanel != null)
            bossHealthPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        UnbindBoss();
    }
}
