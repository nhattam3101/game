using UnityEngine;

public enum MS7B1ElementType { None = 0, Fire = 1, Ice = 2, Lightning = 3 }

[CreateAssetMenu(menuName = "MonsterSurvival/7B.1/Elemental Ammo Data", fileName = "MS7B1_ElementalAmmoData")]
public sealed class MS7B1ElementData : ScriptableObject
{
    [Header("Fire: damage over time; never replaces bullet damage")]
    [Min(0.1f)] public float burnDuration = 3f;
    [Min(0f)] public float burnDamagePerSecond = 2f;
    [Min(0.1f)] public float burnTickSeconds = 0.5f;

    [Header("Ice: requires EnemySlowEffect already on enemy prefab")]
    [Range(0f, 0.9f)] public float slowPercent = 0.3f;
    [Min(0.1f)] public float slowDuration = 2.0f;

    [Header("Lightning: secondary targets only, never original target")]
    [Range(0, 8)] public int maxChainTargets = 3;
    [Min(0.1f)] public float chainRadius = 3.5f;
    [Range(0f, 2f)] public float chainDamageRatio = 0.45f;
    [Min(0.02f)] public float arcLifetime = 0.12f;

    [Header("Presentation")]
    public Color fireTint = new Color(1f, 0.45f, 0.18f);
    public Color iceTint = new Color(0.55f, 0.9f, 1f);
    public Color lightningTint = new Color(1f, 0.86f, 0.36f);
}
