
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Player Multipliers")]
    public float damageMultiplier = 1f;
    public float attackSpeedMultiplier = 1f;
    public float moveSpeedMultiplier = 1f;

    [Header("Projectile Upgrades")]
    [Min(1)]
    public int bulletsPerShot = 1;

    [Header("Critical Hit")]
    [Range(0f, 1f)]
    public float criticalChance = 0f;

    [Min(1f)]
    public float criticalDamageMultiplier = 2f;

    [Header("Special Projectile Upgrades")]
    public ProjectileEffectData activeProjectileEffect;

    [Min(0)]
    public int piercingBonusTargets = 0;

    // Ghi nho Piercing Shot da duoc mo khoa.
    // Khong bi reset khi doi sang loai dan khac.
    private bool piercingUnlocked = false;

    // FROZEN BURST v1.2: Ghi nho hai ky nang da nhan trong luot choi.
    // Khong thay doi SkillType/ProjectileEffectType, tranh lech SkillData assets.
    private ProjectileEffectData unlockedFrostEffect;
    private ProjectileEffectData unlockedExplosionEffect;

    // PHANTOM CHAIN 2B: ghi nho Pierce + Ricochet qua cac lan doi dan.
    private ProjectileEffectData unlockedPierceEffect;
    private ProjectileEffectData unlockedRicochetEffect;

    public bool PhantomChainUnlocked { get; private set; }

    public int PhantomPierceMaxTargets => unlockedPierceEffect != null
        ? Mathf.Max(1, unlockedPierceEffect.maxPierceTargets + piercingBonusTargets)
        : 1;

    public float PhantomPierceDamageRetention => unlockedPierceEffect != null
        ? Mathf.Clamp01(unlockedPierceEffect.damageRetention)
        : 1f;

    public int PhantomMaxBounces => unlockedRicochetEffect != null
        ? Mathf.Max(0, unlockedRicochetEffect.maxBounces)
        : 0;

    public float PhantomBounceSearchRadius => unlockedRicochetEffect != null
        ? Mathf.Max(0.1f, unlockedRicochetEffect.bounceSearchRadius)
        : 0.1f;

    // Phantom chi co hieu luc voi dan Pierce/Ricochet dang trang bi.
    public bool IsPhantomChainActive =>
        PhantomChainUnlocked &&
        activeProjectileEffect != null &&
        (activeProjectileEffect.effectType == ProjectileEffectType.Pierce ||
         activeProjectileEffect.effectType == ProjectileEffectType.Ricochet);

    private void TryUnlockPhantomChain()
    {
        if (PhantomChainUnlocked ||
            unlockedPierceEffect == null ||
            unlockedRicochetEffect == null)
        {
            return;
        }

        PhantomChainUnlocked = true;

        // Uu tien dan Pierce khi lan dau du 2 ky nang.
        // Frozen Burst van duoc ghi nho va chay khi chon lai Explosion.
        activeProjectileEffect = unlockedPierceEffect;

        SkillPopup.Show(transform, "PHANTOM CHAIN UNLOCKED!");
        Debug.Log("[Skill Synergy] Phantom Chain unlocked: Pierce + Ricochet.");
    }

    // RAPID BLAST 2C: Attack Speed + Explosive Shot da duoc lua chon.
    // Ghi nho ky nang theo LUOT CHOI; khong thay doi enum/ScriptableObject.
    private bool attackSpeedUnlocked;
    public bool RapidBlastUnlocked { get; private set; }

    // Chi kich hoat voi dan Explosion dang trang bi.
    public bool IsRapidBlastActive =>
        RapidBlastUnlocked &&
        activeProjectileEffect != null &&
        activeProjectileEffect.effectType == ProjectileEffectType.Explosion;

    private void TryUnlockRapidBlast()
    {
        if (RapidBlastUnlocked ||
            !attackSpeedUnlocked ||
            unlockedExplosionEffect == null)
        {
            return;
        }

        RapidBlastUnlocked = true;

        // Lan dau mo khoa: chuyen sang Explosion.
        // Frozen Burst va Phantom Chain van duoc ghi nho nhu cu.
        activeProjectileEffect = unlockedExplosionEffect;

        SkillPopup.Show(transform, "RAPID BLAST UNLOCKED!");
        Debug.Log("[Skill Synergy] Rapid Blast unlocked: Attack Speed + Explosive.");
    }

    public bool FrozenBurstUnlocked { get; private set; }
    public float FrozenBurstSlowPercent =>
        unlockedFrostEffect != null ? unlockedFrostEffect.slowPercent : 0f;
    public float FrozenBurstSlowDuration =>
        unlockedFrostEffect != null ? unlockedFrostEffect.slowDuration : 0f;

    private void TryUnlockFrozenBurst()
    {
        if (FrozenBurstUnlocked ||
            unlockedFrostEffect == null ||
            unlockedExplosionEffect == null)
        {
            return;
        }

        FrozenBurstUnlocked = true;

        // Khi du ca hai skill, tu trang bi Explosion: Explosion + Slow.
        // Neu sau nay Player chon Pierce/Ricochet thi van co the doi dan
        // nhu co che cu; Frozen Burst duoc ghi nho va se hoat dong lai
        // khi ExplosiveShot duoc chon tiep.
        activeProjectileEffect = unlockedExplosionEffect;

        SkillPopup.Show(transform, "FROZEN BURST UNLOCKED!");
        Debug.Log("[Skill Synergy] Frozen Burst unlocked: Frost + Explosive.");
    }

    // Ap dung ky nang khi nguoi choi lua chon.
    public void ApplySkill(SkillData skill)
    {
        if (skill == null)
        {
            Debug.LogWarning(
                "SkillData khong ton tai!"
            );

            return;
        }

        switch (skill.skillType)
        {
            // =================================
            // 1. TANG SAT THUONG
            // =================================
            case SkillType.Damage:
                {
                    damageMultiplier += skill.value;
                    break;
                }

            // =================================
            // 2. TANG TOC DO TAN CONG
            // =================================
            case SkillType.AttackSpeed:
                {
                    attackSpeedMultiplier += skill.value;
                    attackSpeedUnlocked = true;
                    TryUnlockRapidBlast();
                    break;
                }

            // =================================
            // 3. TANG TOC DO DI CHUYEN
            // =================================
            case SkillType.MoveSpeed:
                {
                    moveSpeedMultiplier += skill.value;
                    break;
                }

            // =================================
            // 4. TANG MAU TOI DA
            // =================================
            case SkillType.MaxHealth:
                {
                    Health health =
                        GetComponent<Health>();

                    if (health != null)
                    {
                        health.maxHealth += skill.value;
                        health.Heal(skill.value);
                    }

                    break;
                }

            // =================================
            // 5. HOI MAU
            // =================================
            case SkillType.Heal:
                {
                    Health health =
                        GetComponent<Health>();

                    if (health != null)
                    {
                        health.Heal(skill.value);
                    }

                    break;
                }

            // =================================
            // 6. BAN THEM DAN
            // =================================
            case SkillType.DoubleShot:
                {
                    int extraBullets = Mathf.Max(
                        1,
                        Mathf.RoundToInt(skill.value)
                    );

                    bulletsPerShot += extraBullets;

                    Debug.Log(
                        "So dan moi lan ban: " +
                        bulletsPerShot
                    );

                    break;
                }

            // =================================
            // 7. LA CHAN
            // =================================
            case SkillType.Shield:
                {
                    PlayerShield shield =
                        GetComponent<PlayerShield>();

                    if (shield == null)
                    {
                        shield =
                            gameObject.AddComponent<PlayerShield>();
                    }

                    int charges = Mathf.Max(
                        1,
                        Mathf.RoundToInt(skill.value)
                    );

                    shield.AddCharges(charges);

                    Debug.Log(
                        "La chan: " +
                        shield.ShieldCharges + "/" +
                        shield.MaxShieldCharges
                    );

                    break;
                }

            // =================================
            // 8. TI LE CHI MANG
            // =================================
            case SkillType.CriticalHit:
                {
                    criticalChance = Mathf.Clamp01(
                        criticalChance + skill.value
                    );

                    Debug.Log(
                        "Ti le chi mang: " +
                        (criticalChance * 100f) + "%"
                    );

                    break;
                }

            // =================================
            // 9. LA CHAN PHAN SAT THUONG
            // =================================
            case SkillType.ReflectShield:
                {
                    PlayerShield shield =
                        GetComponent<PlayerShield>();

                    if (shield == null)
                    {
                        shield =
                            gameObject.AddComponent<PlayerShield>();
                    }

                    shield.AddReflection(skill.value);

                    Debug.Log(
                        "Da nang cap la chan phan sat thuong!"
                    );

                    break;
                }

            // =================================
            // 10. DAN XUYEN
            // =================================
            case SkillType.PiercingShot:
                {
                    if (skill.projectileEffect == null)
                    {
                        Debug.LogWarning(
                            "PiercingShot thieu ProjectileEffectData!"
                        );

                        break;
                    }

                    if (skill.projectileEffect.effectType !=
                        ProjectileEffectType.Pierce)
                    {
                        Debug.LogWarning(
                            "PiercingShot can Effect Type = Pierce!"
                        );

                        break;
                    }

                    if (!piercingUnlocked)
                    {
                        piercingUnlocked = true;
                        piercingBonusTargets = 0;
                    }
                    else
                    {
                        int extraTargets = Mathf.Max(
                            1,
                            Mathf.RoundToInt(skill.value)
                        );

                        piercingBonusTargets += extraTargets;
                    }

                    activeProjectileEffect =
                        skill.projectileEffect;

                    unlockedPierceEffect = skill.projectileEffect;
                    TryUnlockPhantomChain();

                    Debug.Log(
                        "Piercing Shot activated. " +
                        "Bonus targets: " +
                        piercingBonusTargets
                    );

                    break;
                }

            // =================================
            // 11. DAN NAY
            // =================================
            case SkillType.RicochetShot:
                {
                    if (skill.projectileEffect == null)
                    {
                        Debug.LogWarning(
                            "RicochetShot thieu ProjectileEffectData!"
                        );

                        break;
                    }

                    if (skill.projectileEffect.effectType !=
                        ProjectileEffectType.Ricochet)
                    {
                        Debug.LogWarning(
                            "RicochetShot can Effect Type = Ricochet!"
                        );

                        break;
                    }

                    activeProjectileEffect =
                        skill.projectileEffect;

                    unlockedRicochetEffect = skill.projectileEffect;
                    TryUnlockPhantomChain();

                    Debug.Log(
                        "Ricochet Shot activated! " +
                        "Max bounces: " +
                        activeProjectileEffect.maxBounces
                    );

                    break;
                }

            // =================================
            // 12. MOI: DAN NO
            // =================================
            case SkillType.ExplosiveShot:
                {
                    if (skill.projectileEffect == null)
                    {
                        Debug.LogWarning(
                            "ExplosiveShot thieu ProjectileEffectData!"
                        );

                        break;
                    }

                    if (skill.projectileEffect.effectType !=
                        ProjectileEffectType.Explosion)
                    {
                        Debug.LogWarning(
                            "ExplosiveShot can Effect Type = Explosion!"
                        );

                        break;
                    }

                    // Trang bi dan no.
                    activeProjectileEffect =
                        skill.projectileEffect;

                    // Nho ExplosiveShot da so huu (ke ca sau khi doi dan).
                    unlockedExplosionEffect = skill.projectileEffect;
                    TryUnlockFrozenBurst();
                    TryUnlockRapidBlast();

                    Debug.Log(
                        "Explosive Shot activated! " +
                        "Explosion radius: " +
                        activeProjectileEffect.explosionRadius
                    );

                    break;
                }

            // =================================
            // 13. MOI: DAN LAM CHAM (FROST SHOT)
            // =================================
            case SkillType.SlowShot:
                {
                    if (skill.projectileEffect == null)
                    {
                        Debug.LogWarning(
                            "SlowShot thieu ProjectileEffectData!"
                        );

                        break;
                    }

                    if (skill.projectileEffect.effectType !=
                        ProjectileEffectType.Slow)
                    {
                        Debug.LogWarning(
                            "SlowShot can Effect Type = Slow!"
                        );

                        break;
                    }

                    // Chi thay doi loai dan dang trang bi.
                    // Khong xoa nang cap Pierce/Ricochet/Explosion.
                    activeProjectileEffect =
                        skill.projectileEffect;

                    // Nho FrostShot da so huu (ke ca sau khi doi dan).
                    unlockedFrostEffect = skill.projectileEffect;
                    TryUnlockFrozenBurst();

                    Debug.Log(
                        "Frost Shot received. Slow: " +
                        (skill.projectileEffect.slowPercent * 100f) +
                        "% for " +
                        skill.projectileEffect.slowDuration +
                        " seconds."
                    );

                    break;
                }

            default:
                {
                    Debug.LogWarning(
                        "Chua xu ly ky nang: " +
                        skill.skillName
                    );

                    break;
                }
        }

        Debug.Log(
            "Da ap dung ky nang: " +
            skill.skillName
        );
    }
}
