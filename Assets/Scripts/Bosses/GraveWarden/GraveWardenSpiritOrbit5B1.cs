using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class GraveWardenSpiritOrbit5B1 : MonoBehaviour
{
    [Header("Orbit appearance - required")]
    public Sprite orbitSprite;
    public int sortingOrder = 15;

    [Header("Attack")]
    [Min(1)] public int phase1Orbs = 3;
    [Min(1)] public int phase2Orbs = 4;
    [Min(1)] public int maxOrbs = 4;
    [Min(0.5f)] public float radius = 1.6f;
    [Min(0.1f)] public float rotationsPerSecond = 0.45f;
    [Min(0.1f)] public float activeSeconds = 5f;
    [Min(0f)] public float damage = 5f;
    [Min(0.2f)] public float hitCooldown = 1f;
    [Min(0.1f)] public float orbPhysicalRadius = 0.35f;

    private GraveWardenBossAI boss;
    private GraveWardenVFX5B3 upgradedVfx;
    private readonly List<GameObject> active = new List<GameObject>();
    private readonly Dictionary<Health, float> recentlyHit = new Dictionary<Health, float>();
    private Coroutine lifeRoutine;

    private void Awake() { boss = GetComponent<GraveWardenBossAI>(); upgradedVfx = GetComponent<GraveWardenVFX5B3>(); }

    public bool CanActivate()
    {
        active.RemoveAll(x => x == null);
        return orbitSprite != null && boss != null && !boss.IsDead && active.Count == 0;
    }

    public void Activate(bool phaseTwo, bool empowered)
    {
        if (!CanActivate()) return;
        int amount = Mathf.Min(maxOrbs, (phaseTwo ? phase2Orbs : phase1Orbs) + (empowered ? 1 : 0));
        for (int i = 0; i < amount; i++)
        {
            GameObject orb = new GameObject("GW_SpiritOrb_Runtime");
            orb.transform.position = transform.position;
            SpriteRenderer renderer = orb.AddComponent<SpriteRenderer>();
            renderer.sprite = orbitSprite;
            renderer.color = new Color(0.75f, 0.35f, 1f, 0.95f);
            renderer.sortingOrder = sortingOrder;
            float size = Mathf.Max(0.01f, orbitSprite.bounds.size.x);
            orb.transform.localScale = Vector3.one * (orbPhysicalRadius * 2f / size);
            CircleCollider2D collider = orb.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = size * 0.5f;
            Rigidbody2D rb = orb.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            GraveWardenSpiritOrb5B1 mover = orb.AddComponent<GraveWardenSpiritOrb5B1>();
            mover.Initialize(this, transform, rb, i, amount, radius, rotationsPerSecond);
            active.Add(orb);
        }
        float orbitLifetime = activeSeconds + (empowered ? 1.5f : 0f);
        if (upgradedVfx != null) upgradedVfx.PlayOrbit(orbitLifetime);
        lifeRoutine = StartCoroutine(Expire(orbitLifetime));
    }

    private IEnumerator Expire(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        ClearAll();
    }

    public void TryHit(Collider2D target)
    {
        if (boss == null || boss.IsDead || target == null) return;
        PlayerStats stats = target.GetComponentInParent<PlayerStats>();
        if (stats == null) return;
        Health hp = stats.GetComponent<Health>();
        if (hp == null || hp.CurrentHealth <= 0f) return;
        float next;
        if (recentlyHit.TryGetValue(hp, out next) && Time.time < next) return;
        recentlyHit[hp] = Time.time + hitCooldown;
        hp.TakeDamage(damage, boss.BossHealth); // Reflect Shield compatible.
    }

    public void ClearAll()
    {
        if (lifeRoutine != null)
        {
            StopCoroutine(lifeRoutine);
            lifeRoutine = null;
        }
        for (int i = 0; i < active.Count; i++) if (active[i] != null) Destroy(active[i]);
        active.Clear();
        recentlyHit.Clear();
    }
}

