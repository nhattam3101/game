
using System.Collections.Generic;
using UnityEngine;

public class ExplosionImpact : MonoBehaviour
{
    [Header("Explosion Visual")]
    public ExplosionVisual explosionVisualPrefab;

    [Header("Explosion Audio")]
    public AudioClip explosionSound;

    [Range(0f, 1f)]
    public float explosionVolume = 0.8f;

    public void Explode(
        Vector3 explosionPosition,
        float bulletDamage,
        ProjectileEffectData effect,
        LayerMask enemyLayer,
        Health directHitTarget,
        DamageNumberPopup damageNumberPrefab,
        float popupHeightOffset,
        bool isCritical)
    {
        if (effect == null ||
            effect.effectType !=
            ProjectileEffectType.Explosion)
        {
            return;
        }

        float radius = Mathf.Max(
            0.1f,
            effect.explosionRadius
        );

        float areaDamage =
            bulletDamage *
            Mathf.Max(
                0f,
                effect.explosionDamageMultiplier
            );

        // 1. Tao hieu ung hinh anh.
        if (explosionVisualPrefab != null)
        {
            ExplosionVisual visual =
                Instantiate(
                    explosionVisualPrefab,
                    explosionPosition,
                    Quaternion.identity
                );

            visual.Initialize(radius);
        }

        // 2. Phat am thanh no.
        if (explosionSound != null &&
            AudioManager.Instance != null &&
            AudioManager.Instance.sfxSource != null)
        {
            AudioManager.Instance.sfxSource.PlayOneShot(
                explosionSound,
                explosionVolume
            );
        }

        // 3. Tim cac Collider trong vung no.
        Collider2D[] colliders =
            Physics2D.OverlapCircleAll(
                explosionPosition,
                radius,
                enemyLayer
            );

        // Moi quai chi nhan damage lan 1 lan.
        HashSet<Health> damagedTargets =
            new HashSet<Health>();

        foreach (Collider2D collider in colliders)
        {
            Health target =
                collider.GetComponentInParent<Health>();

            if (target == null ||
                target.CurrentHealth <= 0f)
            {
                continue;
            }

            // Quai bi ban truc tiep da nhan
            // damage tu Bullet.cs.
            if (target == directHitTarget)
                continue;

            if (!damagedTargets.Add(target))
                continue;

            float healthBefore =
                target.CurrentHealth;

            Vector3 popupPosition =
                target.transform.position +
                Vector3.up * popupHeightOffset;

            popupPosition.z -= 0.1f;

            target.TakeDamage(areaDamage);

            float actualDamage = Mathf.Max(
                0f,
                healthBefore -
                target.CurrentHealth
            );

            // Hien Damage Numbers cho
            // cac quai trung sat thuong lan.
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
        }

        Debug.Log(
            "Explosion hit " +
            damagedTargets.Count +
            " additional enemies."
        );
    }
}
