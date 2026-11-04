using UnityEngine;

// Optional visual-only Death animation, without changing Health.OnDied,
// WaveManager, Victory timing, or the existing GraveWardenBossAI.cs.
[RequireComponent(typeof(Health))]
[DisallowMultipleComponent]
public class GraveWardenDeathVisual5B2 : MonoBehaviour
{
    [Header("Drag from GW_Visual child")]
    public SpriteRenderer visualRenderer;
    public Animator visualAnimator;
    [Min(0.1f)] public float deathVisualDuration = 1.55f;

    private Health health;

    private void Awake()
    {
        health = GetComponent<Health>();
        if (visualRenderer == null)
        {
            Transform child = transform.Find("GW_Visual");
            if (child != null) visualRenderer = child.GetComponent<SpriteRenderer>();
        }
        if (visualAnimator == null && visualRenderer != null)
            visualAnimator = visualRenderer.GetComponent<Animator>();
    }

    private void OnEnable()
    {
        if (health == null) health = GetComponent<Health>();
        if (health != null) health.OnDied += SpawnDeathVisual;
    }
    private void OnDisable()
    {
        if (health != null) health.OnDied -= SpawnDeathVisual;
    }

    private void SpawnDeathVisual()
    {
        if (visualRenderer == null || visualAnimator == null ||
            visualAnimator.runtimeAnimatorController == null) return;

        GameObject vfx = new GameObject("GW_DeathVisual_5B2");
        vfx.transform.position = visualRenderer.transform.position;
        vfx.transform.rotation = visualRenderer.transform.rotation;
        vfx.transform.localScale = visualRenderer.transform.lossyScale;

        SpriteRenderer sr = vfx.AddComponent<SpriteRenderer>();
        sr.sprite = visualRenderer.sprite;
        sr.color = visualRenderer.color;
        sr.sortingLayerID = visualRenderer.sortingLayerID;
        sr.sortingOrder = visualRenderer.sortingOrder;
        sr.flipX = visualRenderer.flipX;
        sr.flipY = visualRenderer.flipY;

        Animator a = vfx.AddComponent<Animator>();
        a.runtimeAnimatorController = visualAnimator.runtimeAnimatorController;
        a.Play("Death", 0, 0f);
        Destroy(vfx, deathVisualDuration);
    }
}
