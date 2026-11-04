using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Tiny unscaled hover/click feedback for upgrade UI only. No other UI or gameplay touched.
[DisallowMultipleComponent]
public sealed class MS5B2ButtonMotion : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public Image targetGraphic;
    public Color baseColor = new Color(.34f,.23f,.49f,1f);
    public float hoverScale = 1.035f;
    public float lerpSpeed = 13f;

    private RectTransform rect;
    private Button button;
    private Vector3 originalScale = Vector3.one;
    private bool hovered;
    private bool pressed;

    private void Awake()
    {
        rect=transform as RectTransform;
        if (rect) originalScale=rect.localScale;
        button=GetComponent<Button>();
        if (!targetGraphic) targetGraphic=GetComponent<Image>();
    }
    private void Update()
    {
        if (!rect || !targetGraphic) return;
        bool enabledButton=button && button.interactable;
        float scale=pressed && enabledButton ? .97f : hovered && enabledButton ? hoverScale : 1f;
        rect.localScale=Vector3.Lerp(rect.localScale,originalScale*scale,
            Mathf.Clamp01(Time.unscaledDeltaTime*lerpSpeed));
        Color target=!enabledButton ? new Color(baseColor.r*.55f,baseColor.g*.55f,baseColor.b*.55f,.65f) :
            pressed ? Color.Lerp(baseColor,Color.black,.22f) :
            hovered ? Color.Lerp(baseColor,Color.white,.24f) : baseColor;
        targetGraphic.color=Color.Lerp(targetGraphic.color,target,Mathf.Clamp01(Time.unscaledDeltaTime*lerpSpeed));
    }
    private void OnDisable()
    {
        hovered=pressed=false;
        if (rect) rect.localScale=originalScale;
        if (targetGraphic) targetGraphic.color=baseColor;
    }
    public void OnPointerEnter(PointerEventData eventData) { hovered=true; }
    public void OnPointerExit(PointerEventData eventData) { hovered=false;pressed=false; }
    public void OnPointerDown(PointerEventData eventData) { pressed=true; }
    public void OnPointerUp(PointerEventData eventData) { pressed=false; }
}
