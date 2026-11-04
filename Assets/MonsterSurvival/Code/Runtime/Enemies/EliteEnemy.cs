using UnityEngine;

// Attach this alongside the existing EnemyController and Health.
// It does not replace chasing, damage handling, or EXP/Gold rewards.
[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(Health))]
public class EliteEnemy : MonoBehaviour
{
    [Header("Enrage When Health Is Low")]
    [Range(0.05f, 0.95f)]
    public float enrageHealthThreshold = 0.5f;

    [Min(1f)]
    public float enrageSpeedMultiplier = 1.6f;

    [Min(1f)]
    public float enrageDamageMultiplier = 1.4f;

    [Header("Optional Visual")]
    public GameObject enrageAura;

    private EnemyController enemyController;
    private Health health;
    private bool enraged;

    private void Awake()
    {
        enemyController = GetComponent<EnemyController>();
        health = GetComponent<Health>();

        if (enrageAura != null)
            enrageAura.SetActive(false);
    }

    private void Update()
    {
        if (enraged || health == null || enemyController == null)
            return;

        if (health.CurrentHealth <= 0f)
            return;

        if (health.maxHealth <= 0f)
            return;

        if (health.CurrentHealth <= health.maxHealth * enrageHealthThreshold)
            EnterEnrage();
    }

    private void EnterEnrage()
    {
        if (enraged)
            return;

        enraged = true;

        enemyController.moveSpeed *= enrageSpeedMultiplier;
        enemyController.damage *= enrageDamageMultiplier;

        if (enrageAura != null)
            enrageAura.SetActive(true);

        Debug.Log(gameObject.name + " entered ENRAGE!");
    }
}
