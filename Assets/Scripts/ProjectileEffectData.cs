
using UnityEngine;

// Cac loai hieu ung cua dan
public enum ProjectileEffectType
{
    None,
    Pierce,
    Ricochet,
    Explosion,
    Slow
}

[CreateAssetMenu(
    fileName = "NewProjectileEffect",
    menuName = "MonsterSurvival/Projectile Effect"
)]
public class ProjectileEffectData : ScriptableObject
{
    [Header("Basic Information")]
    public string effectName = "New Effect";

    [TextArea(2, 4)]
    public string description;

    public ProjectileEffectType effectType =
        ProjectileEffectType.None;

    [Header("Visual")]
    public Color projectileColor = Color.white;

    [Header("Pierce Settings")]
    [Min(1)]
    public int maxPierceTargets = 2;

    [Range(0f, 1f)]
    public float damageRetention = 1f;

    [Header("Ricochet Settings")]
    [Min(0)]
    public int maxBounces = 2;

    [Min(0.1f)]
    public float bounceSearchRadius = 4f;

    [Header("Explosion Settings")]
    [Min(0.1f)]
    public float explosionRadius = 1.5f;

    [Range(0f, 2f)]
    public float explosionDamageMultiplier = 0.75f;

    [Header("Slow Settings")]
    [Range(0f, 1f)]
    public float slowPercent = 0.3f;

    [Min(0.1f)]
    public float slowDuration = 2f;
}
