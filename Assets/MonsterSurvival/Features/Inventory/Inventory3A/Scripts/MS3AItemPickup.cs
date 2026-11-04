using UnityEngine;

// Put on a collectible prefab. Requires Trigger Collider2D, with Rigidbody2D on either side.
[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public sealed class MS3AItemPickup : MonoBehaviour
{
    public MS3AItemData item;
    [Min(1)] public int amount = 1;
    private bool taking;

    private void Reset()
    {
        var c = GetComponent<Collider2D>();
        if (c != null) c.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other) { TryCollect(other); }
    private void OnTriggerStay2D(Collider2D other) { TryCollect(other); }

    private void TryCollect(Collider2D other)
    {
        if (taking || item == null || amount <= 0) return;
        var inventory = other.GetComponentInParent<MS3APlayerInventory>();
        if (inventory == null || !inventory.CompareTag("Player")) return;
        taking = true;
        int accepted = inventory.AddItem(item, amount);
        amount -= accepted;
        if (amount <= 0) Destroy(gameObject);
        else taking = false; // Full inventory: leave remaining item in the world.
    }
}
