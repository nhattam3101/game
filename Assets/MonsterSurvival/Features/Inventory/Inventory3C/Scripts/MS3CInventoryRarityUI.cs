using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

// Add to EXISTING MS3A_InventoryCanvas; does not replace 3A or 3A.1.
// Changes only visual UI colors and a tiny rarity label. Slot buttons stay clickable.
[DisallowMultipleComponent]
public sealed class MS3CInventoryRarityUI : MonoBehaviour
{
    public MS3AInventoryUI inventoryUI;
    public MS3CRarityCatalog rarityCatalog;
    public TMP_Text rarityLabel;
    private int selected = -1;
    private bool hooked;
    private UnityAction[] clickHandlers;

    private static readonly Color Empty = new Color(.13f, .12f, .21f, .97f);
    private static readonly Color Selected = new Color(.57f, .32f, .79f, .96f);

    private void OnEnable()
    {
        if (!inventoryUI) inventoryUI = GetComponent<MS3AInventoryUI>();
        HookButtons();
    }

    private void OnDisable() { UnhookButtons(); }

    private void HookButtons()
    {
        if (hooked || !inventoryUI || inventoryUI.cells == null) return;
        clickHandlers = new UnityAction[inventoryUI.cells.Length];
        for (int i = 0; i < inventoryUI.cells.Length; i++)
        {
            int slotIndex = i;
            MS3AInventorySlotUI cell = inventoryUI.cells[i];
            if (cell == null || cell.button == null) continue;
            clickHandlers[i] = () => { selected = slotIndex; };
            cell.button.onClick.AddListener(clickHandlers[i]);
        }
        hooked = true;
    }

    private void UnhookButtons()
    {
        if (!hooked || !inventoryUI || inventoryUI.cells == null) return;
        for (int i = 0; i < inventoryUI.cells.Length; i++)
        {
            var cell = inventoryUI.cells[i];
            if (cell != null && cell.button != null && clickHandlers != null && i < clickHandlers.Length && clickHandlers[i] != null)
                cell.button.onClick.RemoveListener(clickHandlers[i]);
        }
        clickHandlers = null;
        hooked = false;
    }

    private void LateUpdate()
    {
        if (!inventoryUI || !inventoryUI.panel || !inventoryUI.panel.activeInHierarchy) return;
        if (inventoryUI.inventory == null || inventoryUI.cells == null) return;
        var slots = inventoryUI.inventory.Slots;
        for (int i = 0; i < inventoryUI.cells.Length && i < slots.Count; i++)
        {
            MS3AInventorySlotUI cell = inventoryUI.cells[i];
            if (cell == null || cell.frame == null) continue;
            var item = slots[i].IsEmpty ? null : slots[i].item;
            bool on = i == selected;
            if (!item) { cell.frame.color = on ? Selected : Empty; continue; }
            MS3CRarity rarity = rarityCatalog ? rarityCatalog.GetRarity(item) : MS3CRarity.Common;
            Color rarityColor = MS3CRarityCatalog.ColorOf(rarity);
            Color baseColor = on ? Selected : Empty;
            Color result = Color.Lerp(baseColor, rarityColor, on ? .72f : .28f);
            result.a = .97f;
            cell.frame.color = result;
        }
        if (rarityLabel != null)
        {
            MS3AItemData item = selected >= 0 && selected < slots.Count && !slots[selected].IsEmpty
                ? slots[selected].item : null;
            if (item != null)
            {
                var rarity = rarityCatalog ? rarityCatalog.GetRarity(item) : MS3CRarity.Common;
                rarityLabel.text = rarity.ToString().ToUpperInvariant();
                rarityLabel.color = MS3CRarityCatalog.ColorOf(rarity);
            }
            else rarityLabel.text = "";
        }
    }
}
