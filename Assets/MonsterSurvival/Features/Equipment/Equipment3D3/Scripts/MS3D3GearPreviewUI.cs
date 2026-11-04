using TMPro;
using UnityEngine;
using UnityEngine.Events;

// Bottom hint only. Does not change 3A/3D1 Buttons, USE, EQUIP or UNEQUIP listeners.
[DisallowMultipleComponent]
public sealed class MS3D3GearPreviewUI : MonoBehaviour
{
    public MS3AInventoryUI inventoryUI;
    public MS3DEquipmentUI equipmentUI;
    public MS3D2BonusCatalog baseBonuses;
    public MS3D3AdvancedCatalog advancedBonuses;
    public TMP_Text details;

    private UnityAction[] clicks;
    private int selected = -1;
    private MS3DPlayerEquipment listener;

    private void OnEnable()
    {
        if (!inventoryUI) inventoryUI = GetComponent<MS3AInventoryUI>();
        if (!equipmentUI) equipmentUI = GetComponent<MS3DEquipmentUI>();
        if (inventoryUI && inventoryUI.cells != null)
        {
            clicks = new UnityAction[inventoryUI.cells.Length];
            for (int i = 0; i < clicks.Length; i++)
            {
                int index = i;
                var cell = inventoryUI.cells[i];
                if (!cell || !cell.button) continue;
                clicks[i] = () => { selected = index; Refresh(); };
                cell.button.onClick.AddListener(clicks[i]);
            }
        }
        listener = equipmentUI ? equipmentUI.equipment : null;
        if (listener) listener.EquipmentChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (listener) listener.EquipmentChanged -= Refresh;
        listener = null;
        if (inventoryUI && inventoryUI.cells != null && clicks != null)
        {
            for (int i = 0; i < clicks.Length && i < inventoryUI.cells.Length; i++)
            {
                var cell = inventoryUI.cells[i];
                if (cell && cell.button && clicks[i] != null) cell.button.onClick.RemoveListener(clicks[i]);
            }
        }
        clicks = null;
    }

    private void Start() { Refresh(); }

    public void Refresh()
    {
        if (!details) return;
        details.text = "SELECT EQUIPMENT TO SEE RARITY / BASE ATTRIBUTES / ADVANCED AFFIXES";
        details.color = new Color(.76f,.70f,.84f,1f);
        if (!inventoryUI || inventoryUI.inventory == null || !equipmentUI || !equipmentUI.equipment) return;
        var slots = inventoryUI.inventory.Slots;
        if (selected < 0 || selected >= slots.Count || slots[selected] == null || slots[selected].IsEmpty) return;
        MS3AItemData item = slots[selected].item;
        MS3DEquipmentDefinition def = null;
        var eq = equipmentUI.equipment;
        if (!item || !eq.catalog || !eq.catalog.TryGet(item, out def) || def == null) return;
        MS3D2Bonus basic = baseBonuses ? baseBonuses.GetBonus(item) : default(MS3D2Bonus);
        MS3D3AdvancedBonus advanced = advancedBonuses ? advancedBonuses.Get(item) : default(MS3D3AdvancedBonus);
        details.text = def.quality.ToString().ToUpperInvariant() + "  /  " + item.displayName + "  /  " + def.type + "\n" +
                       basic.CompactText() + "     |     " + advanced.CompactText();
        details.color = MS3DEquipmentCatalog.QualityColor(def.quality);
    }
}
