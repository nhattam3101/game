using UnityEngine;

// Boss-owned souls, NOT tied to Unity Destroy/OnDestroy.
[DisallowMultipleComponent]
public class GraveWardenSoulHarvest5B1 : MonoBehaviour
{
    [Min(1)] public int capacity = 5;
    [Min(1)] public int empowerCost = 3;
    public bool showSoulLogs = true;
    public int Souls { get; private set; }

    public void GainFromDefeatedMinion()
    {
        GraveWardenBossAI boss = GetComponent<GraveWardenBossAI>();
        if (boss == null || boss.IsDead) return;
        Souls = Mathf.Min(capacity, Souls + 1);
        boss.TriggerAnimation("SoulHarvest_Absorb");
        GraveWardenVFX5B3 upgradedVfx = GetComponent<GraveWardenVFX5B3>();
        if (upgradedVfx != null) upgradedVfx.PlaySoulHarvest();
        if (showSoulLogs) Debug.Log("[GraveWarden] Soul Harvest " + Souls + "/" + capacity, this);
    }

    public bool TrySpendForEmpower()
    {
        if (Souls < empowerCost) return false;
        Souls -= empowerCost;
        if (showSoulLogs) Debug.Log("[GraveWarden] EMPOWER next skill, souls left: " + Souls, this);
        return true;
    }
}
