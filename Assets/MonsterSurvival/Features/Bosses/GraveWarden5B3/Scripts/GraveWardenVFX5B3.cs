using UnityEngine;

// Add-on visual-only director. Safe to attach alongside the 5B.1 Cast Movement Lock boss AI.
// Requires Sprite arrays assigned via the included Editor tool or Inspector.
[DisallowMultipleComponent]
public sealed class GraveWardenVFX5B3 : MonoBehaviour
{
    [Header("VFX Frames - 6 sprites per skill")]
    public Sprite[] tombWallRise = new Sprite[0];
    public Sprite[] wraithPortal = new Sprite[0];
    public Sprite[] reanimateRise = new Sprite[0];
    public Sprite[] spiritOrbit = new Sprite[0];
    public Sprite[] soulHarvest = new Sprite[0];

    [Header("Presentation only")]
    [Min(1f)] public float vfxFramesPerSecond = 10f;
    [Min(0.2f)] public float wallWorldWidth = 1.25f;
    [Min(0.2f)] public float portalWorldWidth = 1.65f;
    [Min(0.2f)] public float reanimateWorldWidth = 1.55f;
    [Min(0.2f)] public float orbitWorldWidth = 3.3f;
    [Min(0.2f)] public float harvestWorldWidth = 1.35f;
    [Min(0.2f)] public float phaseTwoAuraWorldWidth = 2.65f;
    public int effectSortingOrder = 12;

    private GraveWardenBossAI boss;
    private GameObject orbitEffect;
    private GameObject phaseTwoEffect;
    private bool phaseTwoStarted;

    private void Awake() { boss = GetComponent<GraveWardenBossAI>(); }

    // A wall's original collider and GameObject remain unchanged; only its SpriteRenderer animates.
    public void PlayWallRise(GameObject wall)
    {
        if (wall == null || !Has(tombWallRise)) return;
        SpriteRenderer sprite = wall.GetComponentInChildren<SpriteRenderer>();
        if (sprite == null) return;
        GW5B3Flipbook anim = sprite.gameObject.GetComponent<GW5B3Flipbook>();
        if (anim == null) anim = sprite.gameObject.AddComponent<GW5B3Flipbook>();
        sprite.color = Color.white;
        sprite.sortingOrder = effectSortingOrder - 3;
        anim.Initialize(sprite, tombWallRise, vfxFramesPerSecond, false, false);
        // Deliberately do not rescale the wall; that would also move/resize existing collider geometry.
    }

    public void PlayWraithPortal(Vector3 position)
    {
        SpawnTemporary("GW_VFX_WraithPortal", wraithPortal, position, portalWorldWidth,
                       effectSortingOrder, false, null);
    }

    public void PlayReanimate(Vector3 position)
    {
        SpawnTemporary("GW_VFX_ReanimateRise", reanimateRise, position, reanimateWorldWidth,
                       effectSortingOrder, false, null);
    }

    public void PlayOrbit(float duration)
    {
        if (!Has(spiritOrbit)) return;
        if (orbitEffect != null) Destroy(orbitEffect);
        orbitEffect = SpawnTemporary("GW_VFX_OrbitAura", spiritOrbit, transform.position,
                                    orbitWorldWidth, effectSortingOrder - 5, true, transform);
        if (orbitEffect != null) Destroy(orbitEffect, Mathf.Max(0.1f, duration));
    }

    public void PlaySoulHarvest()
    {
        SpawnTemporary("GW_VFX_SoulHarvest", soulHarvest, transform.position,
                       harvestWorldWidth, effectSortingOrder + 1, false, transform);
    }

    private void LateUpdate()
    {
        if (boss == null || boss.IsDead) { Clear(); return; }
        if (!phaseTwoStarted && boss.IsPhaseTwo)
        {
            phaseTwoStarted = true;
            // Use the Harvest imagery as a restrained persistent Phase 2 aura.
            phaseTwoEffect = SpawnTemporary("GW_VFX_Phase2Aura", soulHarvest, transform.position,
                              phaseTwoAuraWorldWidth, effectSortingOrder - 7, true, transform);
            if (phaseTwoEffect != null)
            {
                SpriteRenderer sr = phaseTwoEffect.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(0.85f, 0.55f, 1f, 0.48f);
            }
        }
    }

    private static bool Has(Sprite[] frames) { return frames != null && frames.Length > 0; }

    private GameObject SpawnTemporary(string objectName, Sprite[] frames, Vector3 worldPosition,
                       float worldWidth, int sorting, bool loop, Transform follow)
    {
        if (!Has(frames)) return null;
        GameObject instance = new GameObject(objectName);
        instance.transform.position = worldPosition;
        if (follow != null) instance.transform.SetParent(follow, true);
        SpriteRenderer sr = instance.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        sr.sortingOrder = sorting;
        // Set based on world units, compensating for non-uniform Boss parent visual scales.
        float spriteWidth = Mathf.Max(0.001f, frames[0].bounds.size.x);
        float parentScale = 1f;
        if (follow != null) parentScale = Mathf.Max(0.001f, Mathf.Abs(follow.lossyScale.x));
        float s = worldWidth / spriteWidth / parentScale;
        instance.transform.localScale = new Vector3(s, s, 1f);
        GW5B3Flipbook anim = instance.AddComponent<GW5B3Flipbook>();
        anim.Initialize(sr, frames, vfxFramesPerSecond, loop, !loop);
        return instance;
    }

    public void Clear()
    {
        if (orbitEffect != null) { Destroy(orbitEffect); orbitEffect = null; }
        if (phaseTwoEffect != null) { Destroy(phaseTwoEffect); phaseTwoEffect = null; }
    }

    private void OnDisable() { Clear(); }
    private void OnDestroy() { Clear(); }
}
