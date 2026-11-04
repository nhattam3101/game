using UnityEngine;

// EXTRA equipment stats; no changes to existing PlayerShooter or PlayerStats scripts.
// Applies only the delta from currently equipped assets; reversible on disable.
[DisallowMultipleComponent]
[RequireComponent(typeof(MS3DPlayerEquipment))]
public sealed class MS3D3AdvancedStats : MonoBehaviour
{
    public MS3DPlayerEquipment equipment;
    public MS3D3AdvancedCatalog advancedCatalog;
    public PlayerShooter shooter;

    private MS3D3AdvancedBonus applied;
    private bool subscribed;
    private bool appliedOnce;

    private void Awake() { Resolve(); }

    private void Resolve()
    {
        if (!equipment) equipment = GetComponent<MS3DPlayerEquipment>();
        if (!shooter) shooter = GetComponent<PlayerShooter>();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        Resolve();
        if (equipment && !subscribed)
        {
            equipment.EquipmentChanged += Recalculate;
            subscribed = true;
        }
        if (shooter && startingBulletSpeed <= .0001f) startingBulletSpeed = shooter.bulletSpeed;
        Recalculate();
    }

    private void OnDisable()
    {
        if (equipment && subscribed) equipment.EquipmentChanged -= Recalculate;
        subscribed = false;
        if (Application.isPlaying && appliedOnce && shooter)
            Apply(default(MS3D3AdvancedBonus) - applied);
        applied = default(MS3D3AdvancedBonus);
        appliedOnce = false;
        startingBulletSpeed = 0f;
    }

    [ContextMenu("3D.3 / Recalculate Advanced Attributes")]
    public void Recalculate()
    {
        if (!Application.isPlaying) return;
        Resolve();
        MS3D3AdvancedBonus total = default(MS3D3AdvancedBonus);
        if (equipment && shooter && advancedCatalog && equipment.catalog)
        {
            for (int i = 0; i < 3; i++)
            {
                MS3AItemData item = equipment.GetEquipped((MS3DEquipmentType)i);
                MS3DEquipmentDefinition def = null;
                if (item && equipment.catalog.TryGet(item, out def) && def != null)
                    total += advancedCatalog.Get(item);
            }
        }
        if (shooter)
        {
            MS3D3AdvancedBonus change = total - applied;
            Apply(change);
        }
        applied = total;
        appliedOnce = shooter != null;
    }

    private void Apply(MS3D3AdvancedBonus b)
    {
        if (!shooter) return;
        shooter.bulletDamage += b.flatBulletDamage;
        shooter.attackRange += b.attackRangeFlat;
        // Percent contributes additively to the original bulletSpeed value.
        // This stat is reversible even with changes to 3D2 and Skills.
        shooter.bulletSpeed += b.bulletSpeedPercent * .01f * startingBulletSpeed;
    }

    private float startingBulletSpeed;
}
