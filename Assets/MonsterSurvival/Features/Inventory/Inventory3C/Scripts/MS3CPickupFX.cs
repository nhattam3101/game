using UnityEngine;

// ADDITIVE 3C component for existing 3A pickup prefabs.
// Moves only the SpriteRenderer child, NEVER the trigger / Rigidbody2D / pickup root.
[DisallowMultipleComponent]
[RequireComponent(typeof(MS3AItemPickup))]
public sealed class MS3CPickupFX : MonoBehaviour
{
    public MS3CRarityCatalog rarityCatalog;
    [Range(0f, .30f)] public float bobLocalUnits = .11f;
    [Range(0f, 5f)] public float bobSpeed = 2.2f;
    [Range(0f, 1f)] public float glowStrength = .30f;
    [Range(0, 6)] public int sparkleCount = 4;
    public bool collectBurst = true;

    private MS3AItemPickup pickup;
    private SpriteRenderer art;
    private SpriteRenderer aura;
    private SpriteRenderer[] sparkles;
    private Vector3 initialPos;
    private Vector3 initialScale;
    private float spawnAt;
    private float seed;
    private MS3AItemData lastItem;
    private MS3CRarity rarity;
    private Color tint;
    private bool ready;
    private bool collectedEffectPlayed;

    private void Start()
    {
        pickup = GetComponent<MS3AItemPickup>();
        if (pickup == null) { enabled = false; return; }
        // 3B instantiates first, then assigns pickup.item; Start runs afterwards.
        SetupArt();
        if (art == null) { Debug.LogWarning("[Inventory 3C] No visible SpriteRenderer: " + name, this); enabled = false; return; }
        initialPos = art.transform.localPosition;
        initialScale = art.transform.localScale;
        spawnAt = Time.time;
        seed = Random.Range(0f, 6.28f);
        BuildFX();
        ApplyRarity();
        ready = true;
    }

    private void SetupArt()
    {
        Transform resized = transform.Find("MS3B1_SmallerWorldVisual");
        if (resized != null) art = resized.GetComponent<SpriteRenderer>();
        if (art != null && art.sprite != null) return;
        SpriteRenderer root = GetComponent<SpriteRenderer>();
        if (root == null || root.sprite == null) return;
        // Fallback for users who did not apply 3B.1. Keep original size, move visuals only.
        GameObject visual = new GameObject("MS3C_RuntimeMovingVisual");
        visual.transform.SetParent(transform, false);
        art = visual.AddComponent<SpriteRenderer>();
        art.sprite = root.sprite;
        art.sharedMaterial = root.sharedMaterial;
        art.color = root.color;
        art.flipX = root.flipX;
        art.flipY = root.flipY;
        art.sortingLayerID = root.sortingLayerID;
        art.sortingOrder = root.sortingOrder;
        root.enabled = false;
    }

    private void BuildFX()
    {
        aura = MS3CVisualFX.CreateGlow(art.transform, "MS3C_Glow", MS3CVisualFX.SoftDot, Color.white,
            art.sortingLayerID, art.sortingOrder - 1);
        Vector2 spriteSize = art.sprite != null ? art.sprite.bounds.size : Vector2.one;
        aura.transform.localScale = new Vector3(Mathf.Max(.8f, spriteSize.x * 1.55f), Mathf.Max(.8f, spriteSize.y * 1.55f), 1f);
        sparkleCount = Mathf.Clamp(sparkleCount, 0, 6);
        sparkles = new SpriteRenderer[sparkleCount];
        for (int i = 0; i < sparkleCount; i++)
        {
            sparkles[i] = MS3CVisualFX.CreateGlow(art.transform, "MS3C_Spark_" + i,
                MS3CVisualFX.SoftDot, Color.white, art.sortingLayerID, art.sortingOrder + 1);
            sparkles[i].transform.localScale = Vector3.one * Mathf.Max(.09f, spriteSize.x * .13f);
        }
    }

    private void ApplyRarity()
    {
        lastItem = pickup.item;
        rarity = rarityCatalog != null ? rarityCatalog.GetRarity(lastItem) : MS3CRarity.Common;
        tint = MS3CRarityCatalog.ColorOf(rarity);
    }

    private void Update()
    {
        if (!ready || art == null || pickup == null) return;
        if (lastItem != pickup.item) ApplyRarity();
        float time = Time.time;
        art.transform.localPosition = initialPos + Vector3.up * (Mathf.Sin(time * bobSpeed + seed) * bobLocalUnits);
        // One-time spawn pop: never changes root/collider or the 3B.1 final visual size.
        float popT = Mathf.Clamp01((time - spawnAt) / .26f);
        float pop = 1f - .25f * (1f - popT) * (1f - popT) + .12f * Mathf.Sin(popT * Mathf.PI);
        art.transform.localScale = initialScale * pop;
        if (aura != null)
        {
            Color c = tint;
            float rarityBoost = rarity == MS3CRarity.Legendary ? 1.35f : rarity == MS3CRarity.Common ? .60f : 1f;
            c.a = glowStrength * rarityBoost * (.74f + .26f * Mathf.Sin(time * 2.8f + seed));
            aura.color = c;
        }
        if (sparkles == null) return;
        float radius = art.sprite != null ? Mathf.Max(.6f, art.sprite.bounds.size.x) * .75f : .8f;
        for (int i = 0; i < sparkles.Length; i++)
        {
            SpriteRenderer sparkle = sparkles[i];
            if (sparkle == null) continue;
            float angle = time * (.55f + .11f * i) + seed + i * 6.28318f / Mathf.Max(1, sparkles.Length);
            sparkle.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * .72f, 0f);
            Color c = tint;
            c.a = (.18f + .60f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(time * 4.8f + i * 1.9f + seed)), 2f)) *
                  (rarity == MS3CRarity.Common ? .40f : 1f);
            sparkle.color = c;
        }
    }

    private void OnDisable()
    {
        if (art != null)
        {
            art.transform.localPosition = initialPos;
            art.transform.localScale = initialScale;
        }
        // 3A decrements amount after successfully adding items to inventory.
        // OnDisable fires on Destroy: amount<=0 uniquely signals fully collected pickup.
        if (Application.isPlaying && ready && !collectedEffectPlayed && collectBurst &&
            pickup != null && pickup.amount <= 0)
        {
            collectedEffectPlayed = true;
            MS3CCollectBurst.Spawn(transform.position, rarity);
        }
    }
}
