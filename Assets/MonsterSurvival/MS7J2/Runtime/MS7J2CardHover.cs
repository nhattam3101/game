using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 7J.2.3: visual hover is POINTER ONLY. Unity's Button selection used to
// remain selected after the dashboard was hidden, permanently tinting a card.
// Reset on disable/re-enable so the next Hub visit always starts neutral.
[DisallowMultipleComponent]
public sealed class MS7J2CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
{
    public Image background;
    public Image topRule;
    public Image accent;
    private bool hovered;
    private bool pressed;

    public void OnPointerEnter(PointerEventData e) { hovered = true; Apply(); }
    public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; Apply(); }
    public void OnPointerDown(PointerEventData e) { pressed = true; Apply(); }
    public void OnPointerUp(PointerEventData e) { pressed = false; Apply(); }
    public void OnSelect(BaseEventData e) { /* Selection must never latch the hover tint. */ }
    public void OnDeselect(BaseEventData e) { pressed = false; Apply(); }
    private void OnEnable() { ClearVisual(); }
    private void OnDisable() { ClearVisual(); }

    public void ClearVisual()
    {
        hovered = false;
        pressed = false;
        Apply();
    }
    private void Apply()
    {
        bool hot = hovered && isActiveAndEnabled;
        if (background) background.color = pressed && hot ? MS7J2UI.CardPressed :
            hot ? MS7J2UI.CardHover : MS7J2UI.Card;
        if (topRule) topRule.color = hot ? MS7J2UI.Gold : MS7J2UI.PurpleLine;
        if (accent) accent.color = hot ? MS7J2UI.Gold : MS7J2UI.SoftGold;
    }
}
