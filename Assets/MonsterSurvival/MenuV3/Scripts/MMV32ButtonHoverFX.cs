using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Visual-only hover feedback. Does not add/remove Button.onClick listeners.
// Uses unscaled time so transitions remain responsive while gameplay is paused.
[RequireComponent(typeof(Button))]
[DisallowMultipleComponent]
public sealed class MMV32ButtonHoverFX : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
    IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    [Header("Optional accent Image (child; must not receive raycasts)")]
    public Image accent;

    [Header("Hover polish")]
    [Range(1f, 1.10f)] public float hoverScale = 1.035f;
    [Range(0f, 12f)] public float hoverOffsetX = 4.0f;
    [Range(1f, 30f)] public float transitionSpeed = 12.0f;
    [Range(0f, 1f)] public float tintStrength = .13f;
    public Color hoverTint = new Color(.78f,.60f,1f,1f);

    private Button button;
    private Graphic target;
    private RectTransform rect;
    private Vector3 startingScale;
    private Vector2 startingPosition;
    private Color startingColor;
    private Color accentColor;
    private Selectable.Transition startingTransition;
    private bool remembered;
    private bool over;
    private bool down;
    private bool selected;
    private float amount;

    private void Awake() { Capture(); }

    private void OnEnable()
    {
        Capture();
        if (button != null) button.transition = Selectable.Transition.None;
        if (accent != null)
        {
            accent.raycastTarget = false;
            accentColor = accent.color;
            Color c=accentColor; c.a=0f; accent.color=c;
        }
    }

    private void Capture()
    {
        if (remembered) return;
        button=GetComponent<Button>();
        rect=transform as RectTransform;
        target=button!=null ? button.targetGraphic : null;
        if (target==null) target=GetComponent<Graphic>();
        if (rect==null || button==null) return;
        startingScale=rect.localScale;
        startingPosition=rect.anchoredPosition;
        startingColor=target!=null ? target.color : Color.white;
        startingTransition=button.transition;
        remembered=true;
    }

    private void Update()
    {
        if (!Application.isPlaying || !remembered) return;
        bool hovered=(over || selected) && button!=null && button.IsActive() && button.IsInteractable();
        float goal=hovered ? 1f : 0f;
        amount=Mathf.MoveTowards(amount,goal,Time.unscaledDeltaTime*transitionSpeed/6f);
        float eased=amount*amount*(3f-2f*amount);
        float pressed = down && hovered ? .976f : 1f;
        float size = Mathf.Lerp(1f,hoverScale,eased)*pressed;
        rect.localScale=startingScale*size;
        rect.anchoredPosition=startingPosition+Vector2.right*(hoverOffsetX*eased);
        if (target!=null)
        {
            Color c=Color.Lerp(startingColor,hoverTint,tintStrength*eased);
            c.a=startingColor.a;
            target.color=c;
        }
        if (accent != null)
        {
            Color c=accentColor;
            c.a=accentColor.a*.82f*eased;
            accent.color=c;
        }
    }

    public void OnPointerEnter(PointerEventData e) { over=true; }
    public void OnPointerExit(PointerEventData e) { over=false; down=false; }
    public void OnPointerDown(PointerEventData e) { down=true; }
    public void OnPointerUp(PointerEventData e) { down=false; }
    public void OnSelect(BaseEventData e) { selected=true; }
    public void OnDeselect(BaseEventData e) { selected=false; down=false; }

    private void OnDisable() { Restore(); }
    private void OnDestroy() { Restore(); }

    private void Restore()
    {
        if (!remembered) return;
        if (rect!=null) { rect.localScale=startingScale; rect.anchoredPosition=startingPosition; }
        if (target!=null) target.color=startingColor;
        if (button!=null) button.transition=startingTransition;
        if (accent!=null) { Color c=accent.color;c.a=0;accent.color=c; }
        amount=0f; over=false; down=false; selected=false;
    }
}
