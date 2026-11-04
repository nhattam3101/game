using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Uses UI only; never changes Time.timeScale, XP, coins or enemy/boss systems.
[DisallowMultipleComponent]
public sealed class MS3AInventoryUI : MonoBehaviour
{
    public MS3APlayerInventory inventory;
    public GameObject panel;
    public MS3AInventorySlotUI[] cells = new MS3AInventorySlotUI[MS3APlayerInventory.Capacity];
    public TMP_Text detailName;
    public TMP_Text detailDescription;
    public Button useButton;
    public Button closeButton;
    private int selectedIndex = -1;

    private void Awake()
    {
        if (panel) panel.SetActive(false);
        if (useButton) useButton.onClick.AddListener(UseSelected);
        if (closeButton) closeButton.onClick.AddListener(Close);
        for (int i = 0; i < cells.Length; i++)
        {
            int capture = i;
            if (cells[i] && cells[i].button)
                cells[i].button.onClick.AddListener(() => Select(capture));
        }
    }

    private void OnEnable()
    {
        if (!inventory) inventory = FindFirstObjectByType<MS3APlayerInventory>();
        if (inventory != null) inventory.InventoryChanged += Redraw;
        Redraw();
    }
    private void OnDisable()
    {
        if (inventory != null) inventory.InventoryChanged -= Redraw;
    }
    private void OnDestroy()
    {
        if (useButton) useButton.onClick.RemoveListener(UseSelected);
        if (closeButton) closeButton.onClick.RemoveListener(Close);
    }
    private void Update()
    {
        // Do not open inventory over Pause/Skill/Victory/GameOver panels.
        if (Time.timeScale <= 0f) { if (panel && panel.activeSelf) Close(); return; }
        bool pressed = false;
#if ENABLE_INPUT_SYSTEM
        pressed = Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        pressed = Input.GetKeyDown(KeyCode.I);
#endif
        if (pressed) Toggle();
    }
    public void Toggle()
    {
        if (!panel || Time.timeScale <= 0f) return;
        if (panel.activeSelf) Close();
        else
        {
            panel.SetActive(true);
            Redraw();
        }
    }
    public void Close() { if (panel) panel.SetActive(false); }
    private void Select(int index)
    {
        if (inventory == null || index < 0 || index >= inventory.Slots.Count) return;
        selectedIndex = index;
        Redraw();
    }
    private void UseSelected()
    {
        if (inventory == null || selectedIndex < 0) return;
        if (!inventory.UseSlot(selectedIndex))
        {
            if (detailDescription) detailDescription.text = "Cannot use: item is not consumable or HP is full.";
        }
    }
    public void Redraw()
    {
        if (!inventory) return;
        var slots = inventory.Slots;
        for (int i = 0; i < cells.Length && i < slots.Count; i++)
            if (cells[i]) cells[i].Draw(slots[i], i == selectedIndex);
        MS3AInventorySlot selected = selectedIndex >= 0 && selectedIndex < slots.Count ? slots[selectedIndex] : null;
        bool has = selected != null && !selected.IsEmpty;
        if (detailName) detailName.text = has ? selected.item.displayName : "SELECT AN ITEM";
        if (detailDescription) detailDescription.text = has ? selected.item.description : "Pick up items in the world. Press I to close.";
        if (useButton) useButton.interactable = has && selected.item.itemKind == MS3AItemKind.HealConsumable;
    }
}
