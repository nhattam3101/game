
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    [Header("Bullet Settings")]
    public float speed = 10f;
    public float lifeTime = 3f;
    public LayerMask enemyLayer;

    [Header("Damage Number Popup")]
    public DamageNumberPopup damageNumberPrefab;
    public float popupHeightOffset = 0.7f;

    [Header("Critical Hit Audio")]
    public AudioClip criticalHitSound;

    [Range(0f, 1f)]
    public float criticalHitVolume = 0.6f;

    [Header("Special Projectile Effect")]
    public ProjectileEffectData projectileEffect;

    private Rigidbody2D rb;

    // MOI: Component xu ly vu no.
    private ExplosionImpact explosionImpact;

    private float damage;
    private bool initialized;
    private bool isCritical;
    private bool isBeingDestroyed;

    // Dan xuyen
    private int targetsHit;
    private int bonusPierceTargets;

    // Dan nay
    private int bouncesUsed;

    // FROZEN BURST v1.2: Slow bo sung cho Explosion (khong them damage).
    private bool frozenBurstEnabled;
    private float frozenBurstSlowPercent;
    private float frozenBurstSlowDuration;

    // PHANTOM CHAIN 2B: xuyen truoc, roi chuyen sang nay.
    private bool phantomChainEnabled;
    private bool phantomIsChaining;
    private int phantomPierceMaxTargets;
    private int phantomMaxBounces;
    private float phantomDamageRetention;
    private float phantomBounceRadius;

    // Moi vien dan ghi nho quai da trung.
    private readonly HashSet<Health> hitTargets =
        new HashSet<Health>();

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // MOI: Tim ExplosionImpact tren Bullet.
        explosionImpact =
            GetComponent<ExplosionImpact>();
    }

    // PlayerShooter goi truoc Initialize().
    public void ConfigureEffect(
        ProjectileEffectData effect,
        int bonusTargets = 0)
    {
        projectileEffect = effect;

        bonusPierceTargets =
            Mathf.Max(0, bonusTargets);
    }

    // Goi sau ConfigureEffect, truoc Initialize.
    public void ConfigureFrozenBurst(float slowPercent, float slowDuration)
    {
        frozenBurstSlowPercent = Mathf.Clamp01(slowPercent);
        frozenBurstSlowDuration = Mathf.Max(0f, slowDuration);
        frozenBurstEnabled =
            frozenBurstSlowPercent > 0f && frozenBurstSlowDuration > 0f;
    }

    // Goi sau ConfigureEffect va truoc Initialize.
    public void ConfigurePhantomChain(
        int pierceMaxTargets,
        float damageRetention,
        int maxBounces,
        float bounceRadius)
    {
        phantomChainEnabled = true;
        phantomPierceMaxTargets = Mathf.Max(1, pierceMaxTargets);
        phantomDamageRetention = Mathf.Clamp01(damageRetention);
        phantomMaxBounces = Mathf.Max(0, maxBounces);
        phantomBounceRadius = Mathf.Max(0.1f, bounceRadius);
    }

    public void Initialize(
        Vector2 direction,
        float bulletDamage,
        bool critical = false)
    {
        damage = bulletDamage;
        isCritical = critical;

        targetsHit = 0;
        bouncesUsed = 0;
        phantomIsChaining = false;
        isBeingDestroyed = false;

        hitTargets.Clear();

        initialized = true;

        rb.gravityScale = 0f;

        rb.linearVelocity =
            direction.normalized * speed;

        Destroy(gameObject, lifeTime);
    }

    // ====================================
    // KIEM TRA CAC LOAI DAN
    // ====================================

    private bool IsPiercingBullet()
    {
        return projectileEffect != null &&
               projectileEffect.effectType ==
               ProjectileEffectType.Pierce;
    }

    private bool IsRicochetBullet()
    {
        return projectileEffect != null &&
               projectileEffect.effectType ==
               ProjectileEffectType.Ricochet;
    }

    // MOI: Kiem tra dan no.
    private bool IsExplosiveBullet()
    {
        return projectileEffect != null &&
               projectileEffect.effectType ==
               ProjectileEffectType.Explosion;
    }

    // MOI: Nhan dien dan lam cham.
    private bool IsSlowBullet()
    {
        return projectileEffect != null &&
               projectileEffect.effectType ==
               ProjectileEffectType.Slow;
    }

    private int GetMaxTargets()
    {
        if (!IsPiercingBullet())
        {
            return 1;
        }

        return Mathf.Max(
            1,
            projectileEffect.maxPierceTargets +
            bonusPierceTargets
        );
    }

    // ====================================
    // XU LY VA CHAM
    // ====================================

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!initialized || isBeingDestroyed)
            return;

        // Chi trung cac collider Enemy Layer.
        int otherLayer =
            1 << other.gameObject.layer;

        if ((enemyLayer.value & otherLayer) == 0)
            return;

        Health target =
            other.GetComponentInParent<Health>();

        if (target == null ||
            target.CurrentHealth <= 0f)
        {
            return;
        }

        // Khong trung lap cung mot con quai.
        if (hitTargets.Contains(target))
            return;

        hitTargets.Add(target);

        // Luu HP truoc khi gay sat thuong.
        float healthBefore = target.CurrentHealth;

        // Vi tri hien Damage Number.
        Vector3 popupPosition =
            target.transform.position +
            Vector3.up * popupHeightOffset;

        popupPosition.z -= 0.1f;

        // ====================================
        // DAMAGE GOC - GIU NGUYEN
        // ====================================

        target.TakeDamage(damage);

        float healthAfter =
            target.CurrentHealth;

        float actualDamage = Mathf.Max(
            0f,
            healthBefore - healthAfter
        );
        MS7C2CombatBridge.RecordCritical(this, isCritical); // MS7C2_CRIT_HOOK
        MS7B1ElementalHitBridge.AfterBaseHit(this, target, actualDamage); // MS7B1_ELEMENT_HOOK

        // ====================================
        // DAMAGE NUMBERS - GIU NGUYEN
        // ====================================

        if (actualDamage > 0f &&
            damageNumberPrefab != null)
        {
            DamageNumberPopup popup =
                Instantiate(
                    damageNumberPrefab,
                    popupPosition,
                    Quaternion.identity
                );

            popup.Initialize(
                actualDamage,
                isCritical
            );
        }

        // ====================================
        // CRITICAL HIT - GIU NGUYEN
        // ====================================

        if (isCritical)
        {
            SkillPopup.Show(
                target.transform,
                "CRITICAL!"
            );

            Debug.Log(
                "CRITICAL HIT! Damage: " + damage
            );

            if (criticalHitSound != null &&
                AudioManager.Instance != null &&
                AudioManager.Instance.sfxSource != null)
            {
                AudioManager.Instance.sfxSource.PlayOneShot(
                    criticalHitSound,
                    criticalHitVolume
                );
            }
        }

        // ====================================
        // PHAN LOAI HIEU UNG DAN
        // ====================================

        targetsHit++;

        // PHANTOM CHAIN phai duoc xu ly truoc cac kieu dan don le.
        if (phantomChainEnabled)
        {
            HandlePhantomChain(target);
            return;
        }

        // 1. DAN XUYEN
        if (IsPiercingBullet())
        {
            HandlePierce();
            return;
        }

        // 2. DAN NAY
        if (IsRicochetBullet())
        {
            HandleRicochet(target);
            return;
        }

        // 3. MOI: DAN NO DIEN RONG
        if (IsExplosiveBullet())
        {
            HandleExplosion(target);
            return;
        }

        // 4. MOI: DAN LAM CHAM
        if (IsSlowBullet())
        {
            HandleSlow(target);
            return;
        }

        // 5. DAN THUONG
        DestroyBullet();
    }

    // ====================================
    // PHANTOM CHAIN 2B - XUYEN ROI NAY
    // ====================================

    private void HandlePhantomChain(Health lastTarget)
    {
        // Giai doan 1: xuyen toi da N muc tieu theo duong dan goc.
        if (!phantomIsChaining)
        {
            if (targetsHit < phantomPierceMaxTargets)
            {
                damage *= phantomDamageRetention;
                return;
            }

            // Da hit du N muc tieu: chuyen sang giai doan nay.
            phantomIsChaining = true;
        }

        // Giai doan 2: tim muc tieu moi, KHONG ban lai quai da hit.
        if (bouncesUsed >= phantomMaxBounces)
        {
            DestroyBullet();
            return;
        }

        Health nextTarget = FindPhantomBounceTarget(
            lastTarget.transform.position
        );

        if (nextTarget == null)
        {
            DestroyBullet();
            return;
        }

        Vector2 nextDirection =
            (Vector2)(nextTarget.transform.position - transform.position);

        if (nextDirection.sqrMagnitude < 0.0001f)
        {
            DestroyBullet();
            return;
        }

        rb.linearVelocity = nextDirection.normalized * speed;
        bouncesUsed++;

        Debug.Log(
            "Phantom Chain: bounce " + bouncesUsed + "/" +
            phantomMaxBounces + " to " + nextTarget.name
        );
    }

    private Health FindPhantomBounceTarget(Vector3 searchCenter)
    {
        Collider2D[] nearby = Physics2D.OverlapCircleAll(
            searchCenter, phantomBounceRadius, enemyLayer
        );

        Health nearest = null;
        float nearestSqr = float.MaxValue;

        foreach (Collider2D candidateCollider in nearby)
        {
            Health candidate =
                candidateCollider.GetComponentInParent<Health>();

            if (candidate == null || candidate.CurrentHealth <= 0f ||
                hitTargets.Contains(candidate))
            {
                continue;
            }

            float sqr =
                (candidate.transform.position - searchCenter).sqrMagnitude;

            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = candidate;
            }
        }

        return nearest;
    }

    // ====================================
    // 1. DAN XUYEN - GIU NGUYEN
    // ====================================

    private void HandlePierce()
    {
        if (targetsHit >= GetMaxTargets())
        {
            DestroyBullet();
            return;
        }

        damage *= Mathf.Clamp01(
            projectileEffect.damageRetention
        );

        Debug.Log(
            "Piercing Bullet: " +
            targetsHit + "/" +
            GetMaxTargets() +
            " targets hit."
        );
    }

    // ====================================
    // 2. DAN NAY - GIU NGUYEN
    // ====================================

    private void HandleRicochet(Health lastTarget)
    {
        if (projectileEffect == null)
        {
            DestroyBullet();
            return;
        }

        int maxBounces = Mathf.Max(
            0,
            projectileEffect.maxBounces
        );

        if (bouncesUsed >= maxBounces)
        {
            Debug.Log(
                "Ricochet Bullet: Reached max bounces."
            );

            DestroyBullet();
            return;
        }

        Health nextTarget =
            FindNextRicochetTarget(
                lastTarget.transform.position
            );

        if (nextTarget == null)
        {
            Debug.Log(
                "Ricochet Bullet: No target found."
            );

            DestroyBullet();
            return;
        }

        Vector2 direction =
            (Vector2)(
                nextTarget.transform.position -
                transform.position
            );

        if (direction.sqrMagnitude < 0.0001f)
        {
            DestroyBullet();
            return;
        }

        rb.linearVelocity =
            direction.normalized * speed;

        bouncesUsed++;

        Debug.Log(
            "Ricochet Bullet: Bounce " +
            bouncesUsed + "/" + maxBounces +
            " -> " + nextTarget.name
        );
    }

    // ====================================
    // TIM MUC TIEU CHO DAN NAY
    // ====================================

    private Health FindNextRicochetTarget(
        Vector3 searchCenter)
    {
        if (projectileEffect == null)
            return null;

        float searchRadius = Mathf.Max(
            0.1f,
            projectileEffect.bounceSearchRadius
        );

        Collider2D[] colliders =
            Physics2D.OverlapCircleAll(
                searchCenter,
                searchRadius,
                enemyLayer
            );

        Health nearestTarget = null;

        float nearestDistanceSqr =
            float.MaxValue;

        foreach (Collider2D collider in colliders)
        {
            Health candidate =
                collider.GetComponentInParent<Health>();

            if (candidate == null ||
                candidate.CurrentHealth <= 0f)
            {
                continue;
            }

            if (hitTargets.Contains(candidate))
                continue;

            float distanceSqr =
                (
                    candidate.transform.position -
                    searchCenter
                ).sqrMagnitude;

            if (distanceSqr < nearestDistanceSqr)
            {
                nearestDistanceSqr = distanceSqr;
                nearestTarget = candidate;
            }
        }

        return nearestTarget;
    }

    // ====================================
    // 3. MOI: DAN NO DIEN RONG
    // ====================================

    private void HandleExplosion(Health directTarget)
    {
        if (projectileEffect == null)
        {
            DestroyBullet();
            return;
        }

        if (explosionImpact == null)
        {
            Debug.LogWarning(
                "Bullet: Thieu ExplosionImpact component!"
            );

            DestroyBullet();
            return;
        }

        // No tai vi tri quai vua bi ban trung.
        // Damage truc tiep da xu ly o tren.
        Vector3 explosionPosition =
            directTarget.transform.position;

        explosionImpact.Explode(
            explosionPosition,
            damage,
            projectileEffect,
            enemyLayer,
            directTarget,
            damageNumberPrefab,
            popupHeightOffset,
            isCritical
        );

        // Slow them cho TAT CA quai con song trong vung no.
        // ExplosionImpact giu toan quyen tinh sat thuong goc.
        if (frozenBurstEnabled)
        {
            ApplyFrozenBurstSlow(explosionPosition);
        }

        Debug.Log(
            "Explosive Bullet: Explosion triggered!"
        );

        // Huy dan sau khi kich hoat vu no.
        DestroyBullet();
    }

    // Slow dien rong rieng cho Frozen Burst, khong gay damage lan hai.
    private void ApplyFrozenBurstSlow(Vector3 center)
    {
        float radius = Mathf.Max(0.1f, projectileEffect.explosionRadius);
        Collider2D[] overlaps = Physics2D.OverlapCircleAll(
            center, radius, enemyLayer
        );

        HashSet<Health> affected = new HashSet<Health>();

        foreach (Collider2D collider in overlaps)
        {
            Health health = collider.GetComponentInParent<Health>();
            if (health == null || health.CurrentHealth <= 0f ||
                !affected.Add(health))
            {
                continue;
            }

            EnemySlowEffect slow = health.GetComponent<EnemySlowEffect>();
            if (slow == null)
            {
                slow = health.GetComponentInChildren<EnemySlowEffect>();
            }
            if (slow != null)
            {
                slow.ApplySlow(
                    frozenBurstSlowPercent,
                    frozenBurstSlowDuration
                );
            }
        }
    }

    // ====================================
    // 4. MOI: DAN LAM CHAM
    // ====================================

    private void HandleSlow(Health target)
    {
        // Damage truc tiep da duoc xu ly trong
        // OnTriggerEnter2D (khong gay damage lan hai).
        if (projectileEffect != null &&
            target != null &&
            target.CurrentHealth > 0f)
        {
            EnemySlowEffect slow =
                target.GetComponent<EnemySlowEffect>();

            if (slow != null)
            {
                slow.ApplySlow(
                    projectileEffect.slowPercent,
                    projectileEffect.slowDuration
                );
            }
            else
            {
                Debug.LogWarning(
                    target.name +
                    " chua gan EnemySlowEffect!"
                );
            }
        }

        // Dan Slow chi trung mot quai roi bien mat.
        DestroyBullet();
    }

    // ====================================
    // HUY DAN AN TOAN - GIU NGUYEN
    // ====================================

    private void DestroyBullet()
    {
        if (isBeingDestroyed)
            return;

        isBeingDestroyed = true;
        initialized = false;

        rb.linearVelocity = Vector2.zero;

        Collider2D bulletCollider =
            GetComponent<Collider2D>();

        if (bulletCollider != null)
        {
            bulletCollider.enabled = false;
        }

        Destroy(gameObject);
    }
}
