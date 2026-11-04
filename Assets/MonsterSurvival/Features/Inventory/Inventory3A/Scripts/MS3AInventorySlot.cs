using System;

[Serializable]
public sealed class MS3AInventorySlot
{
    public MS3AItemData item;
    public int quantity;
    public bool IsEmpty { get { return item == null || quantity <= 0; } }
}
