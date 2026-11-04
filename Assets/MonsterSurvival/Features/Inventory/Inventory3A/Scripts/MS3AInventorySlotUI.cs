using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MS3AInventorySlotUI : MonoBehaviour
{
    public Button button;
    public Image frame;
    public Image icon;
    public TMP_Text quantityText;

    // 3A.1 HOTFIX: the original installer created slot Image with
    // raycastTarget=false. A Button with a non-raycast targetGraphic
    // cannot receive pointer clicks. Close works because it uses
    // MakeButton, which enables raycastTarget.
    // Repair at runtime for scenes that already have Inventory3A.
    private void Awake() { EnsureClickable(); }
    private void OnEnable() { EnsureClickable(); }

    public void EnsureClickable()
    {
        if (frame != null && !frame.raycastTarget)
            frame.raycastTarget = true;

        // This should already be assigned by the 3A installer.
        // Recover it only if an existing scene has a missing targetGraphic.
        if (button != null && frame != null && button.targetGraphic == null)
            button.targetGraphic = frame;
    }

    public void Draw(MS3AInventorySlot slot, bool selected)
    {
        bool has = slot != null && !slot.IsEmpty;
        if (icon != null)
        {
            icon.enabled = has && slot.item.icon != null;
            icon.sprite = has ? slot.item.icon : null;
        }
        if (quantityText != null)
            quantityText.text = has && slot.quantity > 1 ? slot.quantity.ToString() : "";
        if (frame != null)
            frame.color = selected ? new Color(.57f, .32f, .79f, .96f) : new Color(.13f, .12f, .21f, .97f);
    }
}
