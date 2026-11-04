
using UnityEngine;

public enum SkillType
{
    Damage,
    MoveSpeed,
    AttackSpeed,
    MaxHealth,
    Heal,
    DoubleShot,
    Shield,
    CriticalHit,
    ReflectShield,

    // Dan xuyen
    PiercingShot,

    // Dan nay
    RicochetShot,

    // MOI: Dan no
    ExplosiveShot,

    // MOI: Dan lam cham
    SlowShot
}

public enum SkillRarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

[CreateAssetMenu(
    fileName = "NewSkill",
    menuName = "Survival Game/Skill"
)]
public class SkillData : ScriptableObject
{
    [Header("Skill Information")]
    public string skillName;

    [TextArea(2, 4)]
    public string description;

    [Header("Skill Settings")]
    public SkillType skillType;

    public float value = 0.2f;

    [Header("Skill Rarity")]
    public SkillRarity rarity = SkillRarity.Common;

    [Header("Skill Level")]
    [Min(1)]
    public int maxLevel = 5;

    [Header("Skill Icon")]
    public Sprite icon;

    [Header("Projectile Effect (Optional)")]
    public ProjectileEffectData projectileEffect;
}
