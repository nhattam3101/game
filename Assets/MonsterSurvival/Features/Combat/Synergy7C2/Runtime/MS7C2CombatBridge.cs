using UnityEngine;

// Minimal hooks invoked inside existing Bullet / 7B.1 elemental paths.
// Call ordering: Bullet records critical BEFORE 7B.1 handles lightning chain.
public static class MS7C2CombatBridge
{
    public static void RecordCritical(Bullet bullet, bool critical)
    {
        if (bullet == null) return;
        MS7C2ProjectileState state = bullet.GetComponent<MS7C2ProjectileState>();
        if (state == null) state = bullet.gameObject.AddComponent<MS7C2ProjectileState>();
        state.SetCritical(critical);
    }

    // Called only by existing Ice slow application. Does not call ApplySlow itself.
    public static float GlacialSlow(MS7B1ElementalProjectile projectile, float originalSlow)
    {
        if (projectile == null) return originalSlow;
        MS7C2SynergyCombat combat = MS7C2SynergyCombat.Find();
        if (combat == null || !combat.ActiveGlacial(projectile.GetComponent<Bullet>())) return originalSlow;
        combat.CountGlacial();
        return MS7C2SynergyCombat.FormulaGlacialSlow(originalSlow,
            combat.glacialExtraSlow, combat.maxGlacialSlow);
    }

    // Called ONLY by the Lightning elemental chain, never by the direct primary hit.
    public static float AmplifyLightningChain(MS7B1ElementalProjectile projectile, float chainDamage)
    {
        if (projectile == null) return chainDamage;
        MS7C2SynergyCombat combat = MS7C2SynergyCombat.Find();
        if (combat == null || !combat.ActiveThunder(projectile.GetComponent<Bullet>())) return chainDamage;
        combat.CountThunder(); // once per chain calculation (not per jump)
        return MS7C2SynergyCombat.FormulaThunderDamage(chainDamage,
            combat.thunderCriticalChainMultiplier);
    }

    // Called ONCE per successfully visited secondary target in 7B.1 ChainFrom.
    // The existing visited HashSet prevents duplicate hits within a chain.
    public static void AfterLightningChainHit(MS7B1ElementalProjectile projectile,
        Health secondary, MS7B1ElementData activeLightningData)
    {
        if (projectile == null || secondary == null || secondary.CurrentHealth <= 0f) return;
        MS7C2SynergyCombat combat = MS7C2SynergyCombat.Find();
        if (combat == null || !combat.ActiveWildfire()) return;
        if (Random.value >= Mathf.Clamp01(combat.wildfireBurnChance)) return;
        MS7C1SynergyCore core = combat.core;
        if (core == null || core.elementalCards == null) return;
        MS7B2ElementalSkillCoordinator cards = core.elementalCards;
        MS7B1ElementData baseData = cards.baseElementData;
        if (baseData == null) return;
        int fireLevel = core.GetLevel(cards.fireSkill);
        if (fireLevel <= 0) return;
        float dps = MS7C2SynergyCombat.FormulaWildfireDps(baseData.burnDamagePerSecond,
            cards.fireDpsBonusPerLevel, fireLevel, combat.wildfireBurnDpsMultiplier);
        if (dps <= 0f) return;
        MS7B1BurnStatus burn = secondary.GetComponent<MS7B1BurnStatus>();
        if (burn == null) burn = secondary.gameObject.AddComponent<MS7B1BurnStatus>();
        burn.Apply(baseData.burnDuration, dps, baseData.burnTickSeconds);
        MS7B3VisualBridge.AfterFire(secondary, baseData);
        combat.CountWildfire(secondary);
    }
}
