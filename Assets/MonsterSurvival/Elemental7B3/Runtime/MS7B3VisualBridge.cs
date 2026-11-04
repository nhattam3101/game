using UnityEngine;

// Called ONLY by the existing 7B.1 elemental hit paths, after status/damage is applied.
// Never calls TakeDamage, ApplySlow, AddCoins, or modifies Save/Skill data.
public static class MS7B3VisualBridge
{
    public static void AfterFire(Health target, MS7B1ElementData data)
    {
        MS7B3VisualHub hub = MS7B3VisualHub.Find();
        if (hub != null && target != null && target.CurrentHealth > 0f)
            hub.OnFire(target, data != null ? data.burnDuration : 3f);
    }

    public static void AfterIce(Health target, MS7B1ElementData data)
    {
        MS7B3VisualHub hub = MS7B3VisualHub.Find();
        if (hub != null && target != null && target.CurrentHealth > 0f)
            hub.OnIce(target, data != null ? data.slowDuration : 2f);
    }

    public static void AfterLightningPrimary(Health target)
    {
        MS7B3VisualHub hub = MS7B3VisualHub.Find();
        if (hub != null && target != null) hub.OnLightningPrimary(target.transform.position);
    }

    public static void AfterLightningJump(Vector3 from, Vector3 to, MS7B1ElementData data)
    {
        MS7B3VisualHub hub = MS7B3VisualHub.Find();
        if (hub != null) hub.OnLightningJump(from, to, data != null ? data.arcLifetime : 0.12f);
    }
}
