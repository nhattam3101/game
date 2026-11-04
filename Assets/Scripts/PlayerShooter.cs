
using UnityEngine;

public class PlayerShooter : MonoBehaviour
{
    [Header("Bullet")]
    public Bullet bulletPrefab;
    public float bulletSpeed = 10f;
    public float bulletDamage = 10f;

    [Header("Shooting")]
    public float attackRange = 8f;
    public float attackInterval = 1f;
    public LayerMask enemyLayer;

    [Header("Multiple Bullets")]
    public float bulletSpacing = 0.22f;

    [Header("Rapid Blast 2C - Attack Speed + Explosive")]
    [Min(2)] public int rapidBlastEveryVolleys = 3;
    [Min(0.1f)] public float rapidBlastCooldown = 2f;
    [Range(1f, 60f)] public float rapidBlastSpreadAngle = 16f;
    [Range(0.1f, 1f)] public float rapidBlastBonusDamageMultiplier = 0.65f;

    private PlayerStats playerStats;
    private float nextAttackTime;
    private int rapidBlastVolleyCounter;
    private float nextRapidBlastTime;

    private void Awake()
    {
        playerStats = GetComponent<PlayerStats>();

        if (playerStats == null)
        {
            Debug.LogWarning(
                "Player chua co PlayerStats!"
            );
        }
    }

    private void Update()
    {
        // Dung ban khi game pause
        if (Time.timeScale == 0f)
            return;

        // Kiem tra hoi chieu
        if (Time.time < nextAttackTime)
            return;

        if (bulletPrefab == null)
            return;

        // Tim quai trong pham vi
        Collider2D[] enemies =
            Physics2D.OverlapCircleAll(
                transform.position,
                attackRange,
                enemyLayer
            );

        Health nearestEnemy = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider2D enemy in enemies)
        {
            Health health =
                enemy.GetComponentInParent<Health>();

            if (health == null ||
                health.CurrentHealth <= 0f)
            {
                continue;
            }

            float distance = Vector2.Distance(
                transform.position,
                health.transform.position
            );

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestEnemy = health;
            }
        }

        if (nearestEnemy == null)
            return;

        Shoot(nearestEnemy.transform.position);

        // Tinh toc do ban sau nang cap
        float speedMultiplier = playerStats != null
            ? playerStats.attackSpeedMultiplier
            : 1f;

        float effectiveInterval =
            attackInterval /
            Mathf.Max(0.1f, speedMultiplier);

        nextAttackTime =
            Time.time + effectiveInterval;
    }

    private void Shoot(Vector3 targetPosition)
    {
        Vector2 direction =
            (targetPosition - transform.position).normalized;

        if (direction.sqrMagnitude < 0.001f)
            return;

        // So dan moi lan ban
        int bulletCount = playerStats != null
            ? Mathf.Max(
                1,
                playerStats.bulletsPerShot
            )
            : 1;

        // He so sat thuong
        float damageMultiplier = playerStats != null
            ? playerStats.damageMultiplier
            : 1f;

        // Xac suat chi mang
        float criticalChance = playerStats != null
            ? Mathf.Clamp01(
                playerStats.criticalChance
            )
            : 0f;

        // He so sat thuong chi mang
        float criticalMultiplier = playerStats != null
            ? Mathf.Max(
                1f,
                playerStats.criticalDamageMultiplier
            )
            : 2f;

        // Vector vuong goc voi huong ban
        Vector2 perpendicular = new Vector2(
            -direction.y,
            direction.x
        );

        // Tao cac vien dan
        for (int i = 0; i < bulletCount; i++)
        {
            float offset =
                (i - (bulletCount - 1) / 2f)
                * bulletSpacing;

            Vector3 spawnPosition =
                transform.position +
                (Vector3)(perpendicular * offset);

            // Moi vien dan co ti le chi mang rieng
            bool isCritical =
                Random.value < criticalChance;

            // Sat thuong co ban sau nang cap
            float finalDamage =
                bulletDamage * damageMultiplier;

            // Neu chi mang thi nhan them sat thuong
            if (isCritical)
            {
                finalDamage *= criticalMultiplier;
            }

            Bullet bullet = Instantiate(
                bulletPrefab,
                spawnPosition,
                Quaternion.identity
            );

            bullet.speed = bulletSpeed;
            bullet.enemyLayer = enemyLayer;

            // ================================
            // MOI: GAN HIEU UNG DAN DAC BIET
            // ================================

            if (playerStats != null &&
                playerStats.activeProjectileEffect != null)
            {
                bullet.ConfigureEffect(
                    playerStats.activeProjectileEffect,
                    playerStats.piercingBonusTargets
                );

                // Phantom Chain: xuyen theo Pierce, sau do nay theo Ricochet.
                // Moi vien dan giu mot ban sao thong so tai luc duoc ban.
                if (playerStats.IsPhantomChainActive)
                {
                    bullet.ConfigurePhantomChain(
                        playerStats.PhantomPierceMaxTargets,
                        playerStats.PhantomPierceDamageRetention,
                        playerStats.PhantomMaxBounces,
                        playerStats.PhantomBounceSearchRadius
                    );
                }

                // Frozen Burst chi hoat dong khi dang trang bi dan no.
                // Cac kieu dan khac van giu nguyen nhu v1.1.
                if (playerStats.FrozenBurstUnlocked &&
                    playerStats.activeProjectileEffect.effectType ==
                    ProjectileEffectType.Explosion)
                {
                    bullet.ConfigureFrozenBurst(
                        playerStats.FrozenBurstSlowPercent,
                        playerStats.FrozenBurstSlowDuration
                    );
                }
            }

            // GIU NGUYEN sat thuong va chi mang
            bullet.Initialize(
                direction,
                finalDamage,
                isCritical
            );
        }

        // RAPID BLAST: moi N luot ban Explosion, them 2 vien cheo.
        // Moi dot cach nhau it nhat rapidBlastCooldown giay.
        // Chi goi SAU khi cac vien dan thuong da duoc tao.
        TryFireRapidBlast(direction, damageMultiplier,
            criticalChance, criticalMultiplier);

        // ================================
        // GIU NGUYEN AM THANH BAN
        // ================================

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayShoot();
        }
    }

    // Khong thay doi so vien cua DoubleShot, tan cong thuong, Crit.
    // Hai vien bonus cung dung cac thiet lap ProjectileEffectData/ExplosionImpact.
    private void TryFireRapidBlast(Vector2 direction, float damageMultiplier,
        float criticalChance, float criticalMultiplier)
    {
        if (playerStats == null || !playerStats.IsRapidBlastActive)
        {
            // Chuyen sang Pierce / Ricochet / Slow thi ket thuc chuoi.
            rapidBlastVolleyCounter = 0;
            return;
        }

        rapidBlastVolleyCounter++;

        if (rapidBlastVolleyCounter % Mathf.Max(2, rapidBlastEveryVolleys) != 0 ||
            Time.time < nextRapidBlastTime)
        {
            return;
        }

        nextRapidBlastTime = Time.time + Mathf.Max(0.1f, rapidBlastCooldown);

        // Hai vien cheo duoc phat ra chi MỘT LAN moi dot Rapid Blast.
        for (int i = 0; i < 2; i++)
        {
            float angle = i == 0 ? -rapidBlastSpreadAngle : rapidBlastSpreadAngle;
            Vector2 extraDirection =
                (Quaternion.Euler(0f, 0f, angle) * direction).normalized;

            bool isCritical = Random.value < criticalChance;
            float extraDamage = bulletDamage * damageMultiplier *
                rapidBlastBonusDamageMultiplier;

            if (isCritical)
            {
                extraDamage *= criticalMultiplier;
            }

            Bullet bonusBullet = Instantiate(
                bulletPrefab,
                transform.position,
                Quaternion.identity
            );

            bonusBullet.speed = bulletSpeed;
            bonusBullet.enemyLayer = enemyLayer;

            // Dung chinh asset Explosion hien tai, khong chinh sua asset.
            bonusBullet.ConfigureEffect(playerStats.activeProjectileEffect,
                playerStats.piercingBonusTargets);

            // Dong thoi ke thua Frozen Burst neu da mo khoa.
            if (playerStats.FrozenBurstUnlocked)
            {
                bonusBullet.ConfigureFrozenBurst(
                    playerStats.FrozenBurstSlowPercent,
                    playerStats.FrozenBurstSlowDuration
                );
            }

            bonusBullet.Initialize(extraDirection, extraDamage, isCritical);
        }

        Debug.Log("[Skill Synergy] RAPID BLAST! +2 explosive bullets.");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            attackRange
        );
    }
}
