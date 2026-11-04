using UnityEngine;

/// <summary>
/// 7C.4.2: One READ-ONLY status source for the six combo displays and QA.
/// New combo unlock MUST come from the existing MS7C1SynergyCore.
/// 7C.2 readiness is NOT a guarantee that a proc happened; only the
/// MS7C2SynergyCombat counters confirm a gameplay proc was recorded.
/// No game state, buffs, skills, Save or scene objects are written here.
/// </summary>
public static class MS7C42SynergyStatus
{
    public struct Snapshot
    {
        public bool ownsBoth;
        public bool coreFound;
        public bool coreUnlocked;
        public bool combatFound;
        public bool ready;
        public int procs;
        public string note;
    }

    public static MS7C1SynergyCore FindCore(PlayerStats player)
    {
        // 7C.1 installer attaches the core to the SAME Player as PlayerStats.
        // Never search another player or profile by accident.
        return player != null ? player.GetComponent<MS7C1SynergyCore>() : null;
    }

    public static MS7C2SynergyCombat FindCombat(MS7C1SynergyCore core)
    {
        if (core == null) return null;
        MS7C2SynergyCombat combat = core.GetComponent<MS7C2SynergyCombat>();
        if (combat == null || !combat.isActiveAndEnabled || combat.core != core) return null;
        return combat;
    }

    public static Snapshot Read(SkillManager skills, PlayerStats player, int index)
    {
        Snapshot result = new Snapshot();
        if (index < 3 || index > 5)
        {
            result.note = "Only indices 3..5 refer to elemental 7C.1 combos.";
            return result;
        }
        result.ownsBoth = MS7C33SynergyRules.Progress(skills, index) == 2;
        MS7C1SynergyCore core = FindCore(player);
        result.coreFound = core != null && core.isActiveAndEnabled;
        if (!result.coreFound)
        {
            result.note = "Missing/disabled 7C.1 core on Player (not a combat bug).";
            return result;
        }
        core.RefreshNow(false); // read the original core's runtime unlock calculation
        if (index == 3) result.coreUnlocked = core.WildfireConduitUnlocked;
        else if (index == 4) result.coreUnlocked = core.GlacialReboundUnlocked;
        else result.coreUnlocked = core.ThunderPrecisionUnlocked;
        if (!result.coreUnlocked)
        {
            result.note = result.ownsBoth ? "Both skills owned; 7C.1 core did not confirm unlock." :
                "Learn the two required skills.";
            return result;
        }
        MS7C2SynergyCombat combat = FindCombat(core);
        result.combatFound = combat != null;
        if (!result.combatFound)
        {
            result.note = "7C.2 combat absent, disabled, or not linked to this 7C.1 core.";
            return result;
        }
        MS7B1ElementLoadout loadout = core.loadout;
        if (loadout == null) loadout = core.GetComponent<MS7B1ElementLoadout>();
        bool lightning = loadout != null && loadout.selectedElement == MS7B1ElementType.Lightning;
        bool ice = loadout != null && loadout.selectedElement == MS7B1ElementType.Ice;
        if (index == 3)
        {
            // Actual Wildfire hook runs only after a confirmed secondary Lightning chain.
            result.ready = lightning;
            result.procs = combat.WildfireProcs;
            result.note = result.ready ? "Lightning equipped; Burn proc requires secondary chain hit + 35% roll." :
                "Equip Lightning to arm Wildfire; ownership alone is not a proc.";
        }
        else if (index == 4)
        {
            // The 7C.2 hook checks Ricochet on the individual Ice projectile.
            bool ricochet = player != null && player.activeProjectileEffect != null &&
                            player.activeProjectileEffect.effectType == ProjectileEffectType.Ricochet;
            result.ready = ice && ricochet;
            result.procs = combat.GlacialHitBonuses;
            result.note = result.ready ? "Ice + Ricochet equipped; enhanced Slow requires valid hit." :
                "Equip Ice AND Ricochet to arm Glacial Slow.";
        }
        else
        {
            // A critical bullet must be recorded at impact before the chain bonus is applied.
            result.ready = lightning;
            result.procs = combat.ThunderCriticalChains;
            result.note = result.ready ? "Lightning equipped; bonus requires CRITICAL chain projectile." :
                "Equip Lightning; only critical Lightning bullets amplify chain.";
        }
        return result;
    }
}
