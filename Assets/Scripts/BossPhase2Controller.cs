
using UnityEngine;

[RequireComponent(typeof(Health))]
public class BossPhase2Controller : MonoBehaviour
{
    [Header("Boss References")]
    public Health bossHealth;
    public EnemyController bossController;
    public Animator bossAnimator;

    [Header("Phase 2 Settings")]
    [Range(0.1f, 0.9f)]
    public float phase2HealthThreshold = 0.5f;

    [Min(1f)]
    public float phase2MoveSpeedMultiplier = 1.3f;

    [Min(1f)]
    public float phase2DamageMultiplier = 1.4f;

    [Range(0.1f, 1f)]
    public float phase2AttackIntervalMultiplier = 0.7f;

    [Header("Phase 2 Visual (Optional)")]
    public GameObject phase2Aura;

    [Header("Debug")]
    [SerializeField]
    private bool phase2Activated = false;

    // Ghi nho chi so goc de tranh nhan nhieu lan.
    private float originalMoveSpeed;
    private float originalDamage;
    private float originalAttackInterval;

    public bool IsPhase2 => phase2Activated;

    private void Awake()
    {
        if (bossHealth == null)
        {
            bossHealth = GetComponent<Health>();
        }

        if (bossController == null)
        {
            bossController =
                GetComponent<EnemyController>();
        }

        if (bossAnimator == null)
        {
            bossAnimator = GetComponent<Animator>();
        }

        if (bossController != null)
        {
            originalMoveSpeed =
                bossController.moveSpeed;

            originalDamage =
                bossController.damage;

            originalAttackInterval =
                bossController.attackInterval;
        }

        if (phase2Aura != null)
        {
            phase2Aura.SetActive(false);
        }
    }

    private void Start()
    {
        if (bossHealth == null)
        {
            Debug.LogError(
                "BossPhase2Controller: Thieu Health!"
            );

            return;
        }

        bossHealth.OnHealthChanged += OnHealthChanged;

        // Ho tro truong hop boss bat dau voi HP thap.
        CheckPhase2(
            bossHealth.CurrentHealth,
            bossHealth.maxHealth
        );
    }

    private void OnHealthChanged(
        float currentHealth,
        float maxHealth)
    {
        CheckPhase2(currentHealth, maxHealth);
    }

    private void CheckPhase2(
        float currentHealth,
        float maxHealth)
    {
        if (phase2Activated)
            return;

        if (currentHealth <= 0f || maxHealth <= 0f)
            return;

        float healthPercent =
            currentHealth / maxHealth;

        if (healthPercent <= phase2HealthThreshold)
        {
            ActivatePhase2();
        }
    }

    private void ActivatePhase2()
    {
        if (phase2Activated)
            return;

        phase2Activated = true;

        Debug.Log(
            "SHADOW TITAN ENTERED PHASE 2!"
        );

        // 1. Tang suc manh danh thuong.
        if (bossController != null)
        {
            bossController.moveSpeed =
                originalMoveSpeed *
                phase2MoveSpeedMultiplier;

            bossController.damage =
                originalDamage *
                phase2DamageMultiplier;

            bossController.attackInterval =
                Mathf.Max(
                    0.1f,
                    originalAttackInterval *
                    phase2AttackIntervalMultiplier
                );
        }

        // 2. Chuyen animation.
        if (bossAnimator != null)
        {
            bool hasPhase2Trigger = false;

            foreach (
                AnimatorControllerParameter parameter
                in bossAnimator.parameters)
            {
                if (parameter.name == "Phase2" &&
                    parameter.type ==
                    AnimatorControllerParameterType.Trigger)
                {
                    hasPhase2Trigger = true;
                    break;
                }
            }

            if (hasPhase2Trigger)
            {
                bossAnimator.SetTrigger("Phase2");
            }
            else
            {
                Debug.LogWarning(
                    "Shadow Titan Animator thieu " +
                    "Trigger Phase2!"
                );
            }
        }
        else
        {
            Debug.LogWarning(
                "Shadow Titan chua gan Animator!"
            );
        }

        // 3. Bat hieu ung Phase 2 neu co.
        if (phase2Aura != null)
        {
            phase2Aura.SetActive(true);
        }

        Debug.Log(
            "Phase 2 stats: " +
            "Speed = " +
            (bossController != null
                ? bossController.moveSpeed : 0f) +
            ", Damage = " +
            (bossController != null
                ? bossController.damage : 0f)
        );
    }

    private void OnDestroy()
    {
        if (bossHealth != null)
        {
            bossHealth.OnHealthChanged -= OnHealthChanged;
        }
    }
}
