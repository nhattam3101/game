using TMPro;
using UnityEngine;

// Cosmetic labels only. Does not add new Buttons or intercept any 3D.1 events.
[DisallowMultipleComponent]
public sealed class MS3D2EquipmentStatLabels : MonoBehaviour
{
    public MS3DEquipmentUI equipmentUI;
    public MS3D2BonusCatalog bonusCatalog;
    public TMP_Text[] statTexts = new TMP_Text[3];
    public RectTransform[] existingNameRects = new RectTransform[3];
    public Vector2[] savedNameMin = new Vector2[3];
    public Vector2[] savedNameMax = new Vector2[3];
    private MS3DPlayerEquipment boundEquipment;

    private void OnEnable()
    {
        if (!equipmentUI) equipmentUI = GetComponent<MS3DEquipmentUI>();
        if (equipmentUI) boundEquipment = equipmentUI.equipment;
        if (boundEquipment) boundEquipment.EquipmentChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (boundEquipment) boundEquipment.EquipmentChanged -= Refresh;
        boundEquipment = null;
    }

    private void Start() { Refresh(); }

    public void Refresh()
    {
        if (!equipmentUI || !equipmentUI.equipment || !bonusCatalog) return;
        for (int i = 0; i < 3 && i < statTexts.Length; i++)
        {
            var label = statTexts[i];
            if (!label) continue;
            var item = equipmentUI.equipment.GetEquipped((MS3DEquipmentType)i);
            // C# CS0165 fix: the out argument is skipped when an earlier
            // short-circuit condition is false. Initialize before using it.
            MS3DEquipmentDefinition def = null;
            bool registered = item && equipmentUI.equipment.catalog &&
                              equipmentUI.equipment.catalog.TryGet(item, out def) && def != null;
            label.text = registered ? bonusCatalog.GetBonus(item).CompactText() : "NO BONUS";
            label.color = registered ? MS3DEquipmentCatalog.QualityColor(def.quality)
                                     : new Color(.56f, .54f, .66f, 1f);
        }
    }
}
