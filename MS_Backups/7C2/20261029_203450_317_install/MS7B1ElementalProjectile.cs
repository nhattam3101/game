using System.Collections.Generic;
using UnityEngine;

// Companion component on the existing Bullet prefab, invoked by a single hook
// placed AFTER Bullet's original TakeDamage and actualDamage computation.
[DisallowMultipleComponent]
public sealed class MS7B1ElementalProjectile : MonoBehaviour
{
    private MS7B1ElementData settings;
    private MS7B1ElementType element = MS7B1ElementType.None;
    private bool sampled;
    private readonly HashSet<Health> processedTargets = new HashSet<Health>();
    private static bool warnedMissingSlow;
    private static Material sharedArcMaterial;

    private void Start() { SnapshotLoadout(); }

    private void SnapshotLoadout()
    {
        if (sampled) return;
        sampled = true;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        MS7B1ElementLoadout loadout = player.GetComponent<MS7B1ElementLoadout>();
        if (loadout == null || loadout.elementData == null) return;
        settings = loadout.elementData;
        element = loadout.selectedElement;

        SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>();
        if (renderer == null || element == MS7B1ElementType.None) return;
        if (element == MS7B1ElementType.Fire) renderer.color = settings.fireTint;
        else if (element == MS7B1ElementType.Ice) renderer.color = settings.iceTint;
        else if (element == MS7B1ElementType.Lightning) renderer.color = settings.lightningTint;
    }

    public void ApplyConfirmedHit(Health primary, float actualDamage, LayerMask enemyMask)
    {
        SnapshotLoadout();
        if (settings == null || element == MS7B1ElementType.None ||
            primary == null || actualDamage <= 0f || !processedTargets.Add(primary)) return;
        if (primary.CompareTag("Player")) return;

        if (element == MS7B1ElementType.Fire)
        {
            if (primary.CurrentHealth <= 0f) return;
            MS7B1BurnStatus burn = primary.GetComponent<MS7B1BurnStatus>();
            if (burn == null) burn = primary.gameObject.AddComponent<MS7B1BurnStatus>();
            burn.Apply(settings.burnDuration, settings.burnDamagePerSecond, settings.burnTickSeconds);
            MS7B3VisualBridge.AfterFire(primary, settings);
        }
        else if (element == MS7B1ElementType.Ice)
        {
            if (primary.CurrentHealth <= 0f) return;
            EnemySlowEffect slow = primary.GetComponent<EnemySlowEffect>();
            if (slow != null)
            {
                // Compatibility with legacy Frost/Slow Shot: EXACTLY ONE ApplySlow per hit.
                // Bullet.HandleSlow executes after the 7B.1 elemental bridge.
                Bullet oldBullet = GetComponent<Bullet>();
                bool hasLegacySlow = oldBullet != null &&
                    oldBullet.projectileEffect != null &&
                    oldBullet.projectileEffect.effectType == ProjectileEffectType.Slow;
                if (!hasLegacySlow)
                    slow.ApplySlow(settings.slowPercent, settings.slowDuration);
                MS7B3VisualBridge.AfterIce(primary, settings);
            }
            else if (!warnedMissingSlow)
            {
                warnedMissingSlow = true;
                Debug.LogWarning("[7B.1] Ice requires EnemySlowEffect on ENEMY PREFABS before spawn. Add it to each intended enemy prefab.");
            }
        }
        else if (element == MS7B1ElementType.Lightning)
        {
            MS7B3VisualBridge.AfterLightningPrimary(primary);
            ChainFrom(primary, actualDamage, enemyMask);
        }
    }

    private void ChainFrom(Health primary, float actualDamage, LayerMask mask)
    {
        int cap = Mathf.Clamp(settings.maxChainTargets, 0, 8);
        if (cap == 0) return;
        float arcDamage = Mathf.Max(0f, actualDamage * settings.chainDamageRatio);
        if (arcDamage <= 0f) return;

        HashSet<Health> visited = new HashSet<Health>();
        visited.Add(primary);
        Vector3 center = primary.transform.position;
        for (int hop = 0; hop < cap; hop++)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, settings.chainRadius, mask);
            Health best = null;
            float bestSq = float.MaxValue;
            foreach (Collider2D hit in hits)
            {
                if (hit == null) continue;
                Health candidate = hit.GetComponentInParent<Health>();
                if (candidate == null || candidate.CurrentHealth <= 0f ||
                    candidate.CompareTag("Player") || visited.Contains(candidate)) continue;
                float sq = (candidate.transform.position - center).sqrMagnitude;
                if (sq < bestSq) { best = candidate; bestSq = sq; }
            }
            if (best == null) break;
            Vector3 destination = best.transform.position;
            DrawArc(center, destination);
            MS7B3VisualBridge.AfterLightningJump(center, destination, settings);
            visited.Add(best); // no duplicate damage from multi-collider enemies
            best.TakeDamage(arcDamage); // primary damage is NEVER repeated
            center = destination;
        }
    }

    private void DrawArc(Vector3 a, Vector3 b)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return;
        if (sharedArcMaterial == null) sharedArcMaterial = new Material(shader) { name = "MS7B1_ArcMat" };
        GameObject obj = new GameObject("MS7B1_LightningArc");
        LineRenderer line = obj.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.SetPosition(0, a);
        line.SetPosition(1, b);
        line.sharedMaterial = sharedArcMaterial;
        line.startWidth = line.endWidth = 0.08f;
        line.startColor = line.endColor = settings.lightningTint;
        line.sortingOrder = 120;
        Destroy(obj, Mathf.Max(0.02f, settings.arcLifetime));
    }
}
