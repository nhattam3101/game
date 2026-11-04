using UnityEngine;

public enum MS3AItemKind { HealConsumable, Material }

// Inventory is intentionally separate from PlayerProgression.coins and EXP.
[CreateAssetMenu(fileName = "Item_", menuName = "MonsterSurvival/Inventory 3A/Item Data")]
public sealed class MS3AItemData : ScriptableObject
{
    [Tooltip("Unique string ID for future Save System; no Save System is added in 3A.")]
    public string itemId = "item_unique_id";
    public string displayName = "New Item";
    [TextArea(2, 4)] public string description = "";
    public Sprite icon;
    public MS3AItemKind itemKind = MS3AItemKind.Material;
    [Min(1)] public int maxStack = 20;
    [Min(0)] public float healAmount = 25f;
    public int StackLimit { get { return Mathf.Max(1, maxStack); } }
}
