using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Purely cosmetic. Does not interact with Save, Button.onClick, or map selection.
[DisallowMultipleComponent]
public sealed class MS5C2ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public Image background;
    public Color idle = new Color(.28f, .19f, .40f, 1f);
    public Color highlighted = new Color(.46f, .31f, .63f, 1f);
    public float speed = 12f;
    private bool hovering;
    private bool pressing;
    private RectTransform rect;
    private Vector3 baseScale;

    private void Awake()
    {
        rect = transform as RectTransform;
        if (rect) baseScale = rect.localScale;
        if (!background) background = GetComponent<Image>();
    }

    private void OnEnable()
    {
        hovering = false;
        pressing = false;
        if (rect == null) rect = transform as RectTransform;
        if (rect) baseScale = rect.localScale;
    }

    private void OnDisable()
    {
        if (rect) rect.localScale = baseScale;
        if (background) background.color = idle;
    }

    public void OnPointerEnter(PointerEventData data) { hovering = true; }
    public void OnPointerExit(PointerEventData data) { hovering = false; pressing = false; }
    public void OnPointerDown(PointerEventData data) { pressing = true; }
    public void OnPointerUp(PointerEventData data) { pressing = false; }

    private void Update()
    {
        bool active = !GetComponent<Button>() || GetComponent<Button>().IsInteractable();
        Color target = !active ? new Color(.19f, .17f, .26f, 1f) : hovering ? highlighted : idle;
        if (background) background.color = Color.Lerp(background.color, target, Mathf.Clamp01(Time.unscaledDeltaTime * speed));
        if (rect)
        {
            float scale = pressing ? .975f : hovering && active ? 1.027f : 1f;
            rect.localScale = Vector3.Lerp(rect.localScale, baseScale * scale, Mathf.Clamp01(Time.unscaledDeltaTime * speed));
        }
    }
}
