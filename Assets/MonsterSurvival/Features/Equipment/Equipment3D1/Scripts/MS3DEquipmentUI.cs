using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Add-on to existing MS3A_InventoryCanvas. Existing I, X, USE and item events are untouched.
[DisallowMultipleComponent]
public sealed class MS3DEquipmentUI : MonoBehaviour
{
    [Header("Existing 3A objects")]
    public MS3AInventoryUI inventoryUI;
    public MS3DPlayerEquipment equipment;
    [Header("3D1 widgets")]
    public RectTransform inventoryWindow;
    public RectTransform equipmentWindow;
    public Button equipButton;
    public TMP_Text hint;
    public Button[] unequipButtons = new Button[3];
    public Image[] slotIcons = new Image[3];
    public Image[] slotBorders = new Image[3];
    public TMP_Text[] slotNames = new TMP_Text[3];
    [Header("Installer rollback data")]
    public Vector2 originalWindowPosition;
    public Vector3 originalWindowScale = Vector3.one;
    public RectTransform originalUseButtonRect;
    public Vector2 originalUseAnchorMin;
    public Vector2 originalUseAnchorMax;

    private int selectedIndex = -1;
    private UnityAction[] selectHandlers;
    private readonly UnityAction[] unequipHandlers = new UnityAction[3];
    private float previousWidth = -1f;
    private float previousHeight = -1f;
    private bool hooked;

    private void OnEnable()
    {
        if (inventoryUI == null) inventoryUI = GetComponent<MS3AInventoryUI>();
        if (equipment == null && inventoryUI != null && inventoryUI.inventory != null)
            equipment = inventoryUI.inventory.GetComponent<MS3DPlayerEquipment>();
        Hook();
        Refresh();
    }

    private void OnDisable() { Unhook(); }

    private void Hook()
    {
        if (hooked || inventoryUI == null) return;
        if (inventoryUI.cells != null)
        {
            selectHandlers = new UnityAction[inventoryUI.cells.Length];
            for (int i = 0; i < inventoryUI.cells.Length; i++)
            {
                var cell = inventoryUI.cells[i];
                if (cell == null || cell.button == null) continue;
                int index = i;
                selectHandlers[i] = () => { selectedIndex = index; Refresh(); };
                cell.button.onClick.AddListener(selectHandlers[i]);
            }
        }
        if (equipButton) equipButton.onClick.AddListener(EquipSelected);
        for (int i = 0; i < unequipButtons.Length && i < 3; i++)
        {
            var button = unequipButtons[i];
            if (button == null) continue;
            var type = (MS3DEquipmentType)i;
            unequipHandlers[i] = () => Unequip(type);
            button.onClick.AddListener(unequipHandlers[i]);
        }
        if (equipment != null)
        {
            equipment.EquipmentChanged += Refresh;
            if (equipment.Inventory != null) equipment.Inventory.InventoryChanged += Refresh;
        }
        hooked = true;
    }

    private void Unhook()
    {
        if (!hooked) return;
        if (inventoryUI && inventoryUI.cells != null && selectHandlers != null)
        {
            for (int i = 0; i < inventoryUI.cells.Length && i < selectHandlers.Length; i++)
                if (inventoryUI.cells[i] && inventoryUI.cells[i].button && selectHandlers[i] != null)
                    inventoryUI.cells[i].button.onClick.RemoveListener(selectHandlers[i]);
        }
        if (equipButton) equipButton.onClick.RemoveListener(EquipSelected);
        for (int i = 0; i < unequipButtons.Length && i < 3; i++)
            if (unequipButtons[i] && unequipHandlers[i] != null)
                unequipButtons[i].onClick.RemoveListener(unequipHandlers[i]);
        if (equipment != null)
        {
            equipment.EquipmentChanged -= Refresh;
            if (equipment.Inventory != null) equipment.Inventory.InventoryChanged -= Refresh;
        }
        hooked = false;
    }

    private void EquipSelected()
    {
        if (!equipment) return;
        string result;
        equipment.TryEquip(selectedIndex, out result);
        if (inventoryUI) inventoryUI.Redraw();
        Refresh();
        if (hint) hint.text = result;
    }

    private void Unequip(MS3DEquipmentType type)
    {
        if (!equipment) return;
        string result;
        equipment.TryUnequip(type, out result);
        if (inventoryUI) inventoryUI.Redraw();
        Refresh();
        if (hint) hint.text = result;
    }

    public void Refresh()
    {
        if (!equipment || !inventoryUI || equipment.Inventory == null) return;
        var slots = equipment.Inventory.Slots;
        MS3AItemData item = selectedIndex >= 0 && selectedIndex < slots.Count &&
                            slots[selectedIndex] != null && !slots[selectedIndex].IsEmpty
            ? slots[selectedIndex].item : null;
        MS3DEquipmentDefinition definition;
        bool wearable = equipment.catalog && equipment.catalog.TryGet(item, out definition);
        if (equipButton) equipButton.interactable = wearable;
        for (int i = 0; i < 3; i++)
        {
            MS3AItemData equipped = equipment.GetEquipped((MS3DEquipmentType)i);
            if (i < slotIcons.Length && slotIcons[i] != null)
            {
                slotIcons[i].sprite = equipped ? equipped.icon : null;
                slotIcons[i].enabled = equipped != null && equipped.icon != null;
                slotIcons[i].raycastTarget = false;
            }
            if (i < slotNames.Length && slotNames[i] != null)
                slotNames[i].text = equipped ? equipped.displayName : "EMPTY  •  CLICK TO UNEQUIP";
            if (i < unequipButtons.Length && unequipButtons[i] != null)
                unequipButtons[i].interactable = equipped != null;
            if (i < slotBorders.Length && slotBorders[i] != null)
            {
                MS3DEquipmentDefinition row;
                Color tint = equipped && equipment.catalog && equipment.catalog.TryGet(equipped, out row)
                    ? MS3DEquipmentCatalog.QualityColor(row.quality)
                    : new Color(.33f, .25f, .48f);
                tint.a = .98f;
                slotBorders[i].color = tint;
            }
        }
    }

    private void LateUpdate()
    {
        // Responsive paired windows: only transforms (not the 3A slot layout) are scaled.
        if (inventoryWindow == null || equipmentWindow == null || inventoryUI == null ||
            inventoryUI.panel == null || !inventoryUI.panel.activeInHierarchy) return;
        var canvasRect = GetComponent<RectTransform>();
        if (canvasRect == null) return;
        float width = canvasRect.rect.width;
        float height = canvasRect.rect.height;
        if (Mathf.Abs(width - previousWidth) < .5f && Mathf.Abs(height - previousHeight) < .5f) return;
        previousWidth = width;
        previousHeight = height;
        float scale = Mathf.Min(1f, Mathf.Max(.46f, (width - 42f) / 1060f),
                                    Mathf.Max(.46f, (height - 25f) / 740f));
        inventoryWindow.localScale = Vector3.one * scale;
        equipmentWindow.localScale = Vector3.one * scale;
        inventoryWindow.anchoredPosition = new Vector2(-186f * scale, 0f);
        equipmentWindow.anchoredPosition = new Vector2(351f * scale, 0f);
    }
}
