using UnityEngine;

// Stage 3D.2: isolated additive stats layer on the SAME Player as PlayerStats and 3D1.
// NEVER rewrites PlayerStats.cs, Health.cs, SkillManager.cs, or old equipment methods.
[DisallowMultipleComponent]
[RequireComponent(typeof(MS3DPlayerEquipment))]
public sealed class MS3D2EquipmentStats : MonoBehaviour
{
    [Header("3D.1 / 3D.2")]
    public MS3DPlayerEquipment equipment;
    public MS3D2BonusCatalog bonusCatalog;

    [Header("Existing Player components (same GameObject)")]
    public PlayerStats playerStats;
    public Health playerHealth;

    private MS3D2Bonus applied;
    private bool activeBonuses;
    private bool listening;

    public MS3D2Bonus AppliedBonuses { get { return applied; } }

    private void Awake()
    {
        Resolve();
    }

    private void Resolve()
    {
        if (!equipment) equipment = GetComponent<MS3DPlayerEquipment>();
        if (!playerStats) playerStats = GetComponent<PlayerStats>();
        if (!playerHealth) playerHealth = GetComponent<Health>();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        Resolve();
        if (equipment != null && !listening)
        {
            equipment.EquipmentChanged += Recalculate;
            listening = true;
        }
        Recalculate();
    }

    private void Start()
    {
        if (Application.isPlaying) Recalculate();
    }

    private void OnDisable()
    {
        if (listening && equipment != null)
            equipment.EquipmentChanged -= Recalculate;
        listening = false;
        // Restore only the applied gear delta; any Skill upgrades remain untouched.
        if (Application.isPlaying && activeBonuses) ApplyDelta(default(MS3D2Bonus) - applied);
        applied = default(MS3D2Bonus);
        activeBonuses = false;
    }

    [ContextMenu("3D.2 / Recalculate Bonuses")]
    public void Recalculate()
    {
        if (!Application.isPlaying) return;
        Resolve();
        if (!equipment || !playerStats || !bonusCatalog)
        {
            if (activeBonuses)
            {
                ApplyDelta(default(MS3D2Bonus) - applied);
                applied = default(MS3D2Bonus);
                activeBonuses = false;
            }
            return;
        }

        MS3D2Bonus total = default(MS3D2Bonus);
        for (int i = 0; i < 3; i++)
        {
            var item = equipment.GetEquipped((MS3DEquipmentType)i);
            // Only a registered equipment item can grant bonuses.
            MS3DEquipmentDefinition definition;
            if (item && equipment.catalog && equipment.catalog.TryGet(item, out definition))
                total += bonusCatalog.GetBonus(item);
        }
        MS3D2Bonus delta = total - applied;
        ApplyDelta(delta);
        applied = total;
        activeBonuses = true;
    }

    private void ApplyDelta(MS3D2Bonus change)
    {
        if (playerStats != null)
        {
            // Do not reset the multipliers: SkillManager adds to these exact fields.
            playerStats.damageMultiplier += change.damagePercent * .01f;
            playerStats.attackSpeedMultiplier += change.attackSpeedPercent * .01f;
            playerStats.moveSpeedMultiplier += change.moveSpeedPercent * .01f;
            playerStats.criticalChance += change.critChancePoints * .01f;
        }
        if (playerHealth != null && Mathf.Abs(change.maxHealthFlat) > .0001f)
        {
            float before = playerHealth.maxHealth;
            float after = Mathf.Max(1f, before + change.maxHealthFlat);
            float actual = after - before;
            playerHealth.maxHealth = after;
            if (Mathf.Abs(actual) > .0001f)
            {
                // Max HP equipment must NEVER act like a repeatable free healing potion.
                // Preserve current HP on equip; clamp only if necessary on unequip.
                // Health.Heal's tiny positive amount emits OnHealthChanged for the HUD.
                // It does NOT cause damage or consume shield charges.
                playerHealth.Heal(.000001f);
            }
        }
    }
}
