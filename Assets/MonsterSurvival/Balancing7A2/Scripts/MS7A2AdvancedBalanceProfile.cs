using System;
using System.Collections.Generic;
using UnityEngine;

// Stage 7A.2: Data-only tuning for enemies managed by existing Wave/AI scripts.
[CreateAssetMenu(fileName = "MS7A2_AdvancedEnemyBalance", menuName = "MonsterSurvival/7A/Advanced Enemy Balance Profile")]
public sealed class MS7A2AdvancedBalanceProfile : ScriptableObject
{
    [Serializable]
    public sealed class WaveRow
    {
        [Min(1)] public int wave = 1;
        [Min(1f)] public float healerSpawnInterval = 10f;
        [Min(1)] public int maxActiveHealers = 1;
        [Min(1)] public int healerSpawnsPerWave = 1;
        [Min(0f)] public float healerHealAmount = 4f;
        [Min(0.1f)] public float healerHealInterval = 3f;

        [Min(1f)] public float summonerSpawnInterval = 14f;
        [Min(1)] public int maxActiveSummoners = 1;
        [Min(1)] public int summonerSpawnsPerWave = 1;
        [Min(1f)] public float summonInterval = 12f;
        [Min(1)] public int minionsPerCast = 1;
        [Min(1)] public int maxActiveMinions = 2;

        [Min(0.1f)] public float rangedAttackInterval = 2.6f;
        [Min(0f)] public float rangedProjectileDamage = 7f;
        [Min(0f)] public float specialGoldMultiplier = 1f;
        [Min(0f)] public float specialExpMultiplier = 1f;
        [Min(0f)] public float eliteDamageMultiplier = 1f;
    }

    [Tooltip("One unique row per Wave; no fallback for missing/duplicate rows.")]
    public List<WaveRow> waves = new List<WaveRow>();

    public bool TryGet(int wave, out WaveRow result)
    {
        result = null;
        if (waves == null) return false;
        for (int i = 0; i < waves.Count; i++)
        {
            WaveRow candidate = waves[i];
            if (candidate == null || candidate.wave != wave) continue;
            if (result != null) { result = null; return false; }
            result = candidate;
        }
        return result != null;
    }

    public bool Validate(int waveCount, out string reason)
    {
        reason = "";
        if (waves == null || waves.Count != waveCount) { reason = "Wave row count mismatch"; return false; }
        for (int i = 1; i <= waveCount; i++)
        {
            WaveRow row;
            if (!TryGet(i, out row)) { reason = "Wave " + i + " missing or duplicate"; return false; }
            if (!Valid(row)) { reason = "Wave " + i + " invalid values"; return false; }
        }
        reason = "PASS: " + waveCount + " advanced balance rows valid";
        return true;
    }

    private static bool FiniteNonNegative(float f) { return !float.IsNaN(f) && !float.IsInfinity(f) && f >= 0f; }

    public static bool Valid(WaveRow r)
    {
        return r != null && r.wave >= 1 &&
            r.healerSpawnInterval >= 1f && r.summonerSpawnInterval >= 1f &&
            r.maxActiveHealers >= 1 && r.healerSpawnsPerWave >= 1 &&
            r.maxActiveSummoners >= 1 && r.summonerSpawnsPerWave >= 1 &&
            r.healerHealInterval >= 0.1f && r.summonInterval >= 1f &&
            r.minionsPerCast >= 1 && r.maxActiveMinions >= r.minionsPerCast &&
            r.rangedAttackInterval >= 0.1f &&
            FiniteNonNegative(r.healerHealAmount) && FiniteNonNegative(r.rangedProjectileDamage) &&
            FiniteNonNegative(r.specialGoldMultiplier) && FiniteNonNegative(r.specialExpMultiplier) &&
            FiniteNonNegative(r.eliteDamageMultiplier) &&
            !float.IsInfinity(r.healerSpawnInterval) && !float.IsNaN(r.healerSpawnInterval) &&
            !float.IsInfinity(r.summonerSpawnInterval) && !float.IsNaN(r.summonerSpawnInterval) &&
            !float.IsInfinity(r.rangedAttackInterval) && !float.IsNaN(r.rangedAttackInterval) &&
            !float.IsInfinity(r.healerHealInterval) && !float.IsNaN(r.healerHealInterval) &&
            !float.IsInfinity(r.summonInterval) && !float.IsNaN(r.summonInterval);
    }
}
