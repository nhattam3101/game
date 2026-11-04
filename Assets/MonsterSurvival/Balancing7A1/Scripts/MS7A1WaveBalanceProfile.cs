using System;
using System.Collections.Generic;
using UnityEngine;

// 7A.1 - Data only: does not own the spawner or change Save/Gold.
[CreateAssetMenu(fileName = "MS7A1_BalancedWaves", menuName = "MonsterSurvival/7A/Wave Balance Profile")]
public sealed class MS7A1WaveBalanceProfile : ScriptableObject
{
    [Serializable]
    public sealed class WaveRow
    {
        [Min(1)] public int wave = 1;
        [Min(0.1f)] public float spawnInterval = 2f;
        [Min(1)] public int maxEnemies = 20;
        [Min(0f)] public float normalWeight = 100f;
        [Min(0f)] public float fastWeight = 0f;
        [Min(0f)] public float tankWeight = 0f;
        [Min(0.1f)] public float rangedSpawnInterval = 8f;
        [Min(1)] public int maxRangedEnemies = 1;
    }

    [Tooltip("One row per wave. The boss spawning rules remain in the original WaveManager.")]
    public List<WaveRow> rows = new List<WaveRow>();

    public bool TryGetRow(int wave, out WaveRow row)
    {
        row = null;
        if (rows == null) return false;

        // Fail closed when IDs are ambiguous rather than applying an arbitrary row.
        for (int i = 0; i < rows.Count; i++)
        {
            WaveRow candidate = rows[i];
            if (candidate == null || candidate.wave != wave) continue;
            if (row != null) { row = null; return false; }
            row = candidate;
        }
        return row != null;
    }

    public bool Validate(int totalWaves, out string report)
    {
        int expected = Mathf.Max(1, totalWaves);
        if (rows == null || rows.Count != expected)
        {
            report = "Expected " + expected + " rows; found " + (rows == null ? 0 : rows.Count) + ".";
            return false;
        }

        for (int wave = 1; wave <= expected; wave++)
        {
            WaveRow row;
            if (!TryGetRow(wave, out row))
            {
                report = "Wave " + wave + " is missing or duplicated.";
                return false;
            }
            if (!ValidRow(row))
            {
                report = "Wave " + wave + " has invalid interval/cap/weights.";
                return false;
            }
        }

        report = "PASS: " + expected + " unique, valid wave rows.";
        return true;
    }

    public static bool ValidRow(WaveRow r)
    {
        if (r == null) return false;
        if (r.wave < 1 || r.maxEnemies < 1 || r.maxRangedEnemies < 1) return false;
        if (float.IsNaN(r.spawnInterval) || float.IsInfinity(r.spawnInterval) || r.spawnInterval < 0.1f) return false;
        if (float.IsNaN(r.rangedSpawnInterval) || float.IsInfinity(r.rangedSpawnInterval) || r.rangedSpawnInterval < 0.1f) return false;
        if (float.IsNaN(r.normalWeight) || float.IsInfinity(r.normalWeight) || r.normalWeight < 0f) return false;
        if (float.IsNaN(r.fastWeight) || float.IsInfinity(r.fastWeight) || r.fastWeight < 0f) return false;
        if (float.IsNaN(r.tankWeight) || float.IsInfinity(r.tankWeight) || r.tankWeight < 0f) return false;
        return r.normalWeight + r.fastWeight + r.tankWeight > 0.0001f;
    }
}
