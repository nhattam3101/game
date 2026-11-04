using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MS6CButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Range(1f, 1.15f)] public float hoverScale = 1.045f;
    [Range(5f, 30f)] public float smoothing = 16f;
    public Color normalTint = Color.white;
    public Color hoverTint = new Color(0.91f, 0.79f, 1f, 1f);
    public Color pressTint = new Color(0.72f, 0.62f, 0.86f, 1f);
    private RectTransform rect;
    private Graphic graphic;
    private Button button;
    private Vector3 startingScale;
    private bool hovering;
    private bool pressed;
    private bool ready;

    private void Awake() { Cache(); }
    private void Cache()
    {
        if (ready) return;
        rect = transform as RectTransform;
        graphic = GetComponent<Graphic>();
        button = GetComponent<Button>();
        startingScale = rect ? rect.localScale : transform.localScale;
        ready = true;
    }
    private void OnEnable()
    {
        Cache(); hovering = pressed = false;
        if (graphic) graphic.color = normalTint;
        if (rect) rect.localScale = startingScale;
    }
    private void Update()
    {
        if (!ready) Cache();
        bool interactive = button == null || button.interactable;
        float scale = (interactive && hovering) ? (pressed ? 0.985f : hoverScale) : 1f;
        Color tint = (interactive && pressed) ? pressTint :
            (interactive && hovering ? hoverTint : normalTint);
        float t = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
        if (rect) rect.localScale = Vector3.Lerp(rect.localScale, startingScale * scale, t);
        if (graphic) graphic.color = Color.Lerp(graphic.color, tint, t);
    }
    public void OnPointerEnter(PointerEventData eventData) { hovering = true; }
    public void OnPointerExit(PointerEventData eventData) { hovering = false; pressed = false; }
    public void OnPointerDown(PointerEventData eventData) { pressed = true; }
    public void OnPointerUp(PointerEventData eventData) { pressed = false; }
    private void OnDisable()
    {
        if (rect) rect.localScale = startingScale;
        if (graphic) graphic.color = normalTint;
    }
}
