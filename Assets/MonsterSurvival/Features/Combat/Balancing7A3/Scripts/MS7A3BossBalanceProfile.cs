using UnityEngine;

// Two independent assets: one for Shadow Titan, one for Grave Warden.
// Values are deliberately modest starting points, NOT tested balance conclusions.
[CreateAssetMenu(fileName = "BossBalance_7A3", menuName = "MonsterSurvival/7A3/Boss Balance Profile")]
public sealed class MS7A3BossBalanceProfile : ScriptableObject
{
    public enum BossKind { ShadowTitan, GraveWarden }

    [Header("Which boss does this profile belong to?")]
    public BossKind boss = BossKind.ShadowTitan;

    [Header("Boss HP (applied BEFORE Health.Awake)")]
    [Range(0.5f, 3f)] public float maxHpMultiplier = 1.10f;

    [Header("Shadow Titan - Shockwave")]
    [Range(0.5f, 2f)] public float shockwaveDamageMultiplier = 1f;
    [Range(0.5f, 2f)] public float shockwaveCooldownMultiplier = 1f;
    [Range(0.5f, 2f)] public float shockwaveWarningMultiplier = 1f;

    [Header("Shadow Titan - Void Burst phase 2")]
    [Range(0.5f, 2f)] public float voidBurstDamageMultiplier = 1f;
    [Range(0.5f, 2f)] public float voidBurstCooldownMultiplier = 1f;
    [Range(0.5f, 2f)] public float voidBurstWarningMultiplier = 1f;

    [Header("Grave Warden - AI skills")]
    [Range(0.5f, 2f)] public float wardenCooldownMultiplier = 1f;
    [Range(0.5f, 2f)] public float wardenGapMultiplier = 1f;
    [Tooltip("Does not change phase transitions or animation.")]
    [Range(0.5f, 2f)] public float wardenMovementMultiplier = 1f;

    [Header("Rewards - existing EnemyController only")]
    [Range(0.5f, 4f)] public float goldMultiplier = 1.15f;
    [Range(0.5f, 4f)] public float expMultiplier = 1.15f;

    [Header("Standalone reward (EXPLICIT OPT-IN ON THE AGENT)")]
    [Min(0)] public int standaloneGold = 30;
    [Min(0)] public int standaloneExp = 60;

    public bool Validate(out string reason)
    {
        bool valid = InRange(maxHpMultiplier, 0.5f, 3f)
            && InRange(shockwaveDamageMultiplier, 0.5f, 2f)
            && InRange(shockwaveCooldownMultiplier, 0.5f, 2f)
            && InRange(shockwaveWarningMultiplier, 0.5f, 2f)
            && InRange(voidBurstDamageMultiplier, 0.5f, 2f)
            && InRange(voidBurstCooldownMultiplier, 0.5f, 2f)
            && InRange(voidBurstWarningMultiplier, 0.5f, 2f)
            && InRange(wardenCooldownMultiplier, 0.5f, 2f)
            && InRange(wardenGapMultiplier, 0.5f, 2f)
            && InRange(wardenMovementMultiplier, 0.5f, 2f)
            && InRange(goldMultiplier, 0.5f, 4f)
            && InRange(expMultiplier, 0.5f, 4f)
            && standaloneGold >= 0 && standaloneExp >= 0;
        reason = valid ? "7A.3 boss multipliers are valid" : "Invalid multiplier / reward (NaN, Infinity or outside allowed range)";
        return valid;
    }

    private static bool InRange(float value, float min, float max)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;
    }
}
