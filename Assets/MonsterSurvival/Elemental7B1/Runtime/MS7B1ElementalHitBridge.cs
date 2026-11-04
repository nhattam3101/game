using UnityEngine;

// Called only after Bullet has applied its OWN base damage.
// It never repeats bullet's direct damage and never edits Save/Gold/EXP.
public static class MS7B1ElementalHitBridge
{
    public static void AfterBaseHit(Component bulletComponent, Health enemy, float actualDamage)
    {
        if (bulletComponent == null || enemy == null || actualDamage <= 0f) return;
        MS7B1ElementalProjectile addon = bulletComponent.GetComponent<MS7B1ElementalProjectile>();
        if (addon == null) return;
        Bullet bullet = bulletComponent as Bullet;
        if (bullet == null) return;
        addon.ApplyConfirmedHit(enemy, actualDamage, bullet.enemyLayer);
    }
}
