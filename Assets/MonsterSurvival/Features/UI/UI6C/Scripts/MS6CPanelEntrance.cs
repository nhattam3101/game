using UnityEngine;

// Fade and subtle scale-in use unscaled time: work during Pause/Level Up/Game Over.
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class MS6CPanelEntrance : MonoBehaviour
{
    [Range(0.08f, 0.7f)] public float duration = 0.24f;
    [Range(0f, 0.08f)] public float shrinkOnEnter = 0.025f;
    private RectTransform rect;
    private CanvasGroup group;
    private Vector3 originalScale;
    private float clock;
    private bool initialized;
    private void Awake() { Init(); }
    private void Init()
    {
        if (initialized) return;
        rect = transform as RectTransform;
        group = GetComponent<CanvasGroup>();
        originalScale = rect ? rect.localScale : transform.localScale;
        initialized = true;
    }
    private void OnEnable()
    {
        Init(); clock = 0f;
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        if (rect) rect.localScale = originalScale * (1f - shrinkOnEnter);
    }
    private void Update()
    {
        if (!group || !initialized) return;
        clock += Time.unscaledDeltaTime;
        float x = Mathf.Clamp01(clock / Mathf.Max(0.01f, duration));
        float eased = x * x * (3f - 2f * x);
        group.alpha = eased;
        if (rect) rect.localScale = Vector3.Lerp(originalScale * (1f - shrinkOnEnter), originalScale, eased);
        if (x >= 1f) { group.interactable = true; group.blocksRaycasts = true; }
    }
    private void OnDisable()
    {
        if (group) { group.alpha = 1f; group.interactable = true; group.blocksRaycasts = true; }
        if (rect) rect.localScale = originalScale;
    }
}
