
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 2f;
    public float stopDistance = 0.7f;

    [Header("Attack")]
    public float attackRange = 0.8f;
    public float damage = 10f;
    public float attackInterval = 1f;

    [Header("Rewards")]
    public int expReward = 5;
    public int coinReward = 2;

    [Header("Animation (Optional)")]
    [Tooltip("Gan Animator cua EliteEnemy; quai thuong co the de trong.")]
    public Animator attackAnimator;

    private bool hasAttackTrigger;

    private Rigidbody2D rb;

    // MOI: Doc he so Slow tu component rieng.
    private EnemySlowEffect slowEffect;

    private Transform player;
    private Health playerHealth;
    private Health enemyHealth;

    private float attackTimer;

    private bool isDead;
    private bool rewardGiven;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        slowEffect = GetComponent<EnemySlowEffect>();

        enemyHealth = GetComponent<Health>();


        // Animator la tuy chon: quai cu khong co Animator van hoat dong.
        if (attackAnimator == null)
        {
            attackAnimator = GetComponentInChildren<Animator>();
        }

        if (attackAnimator != null &&
            attackAnimator.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter parameter in
                     attackAnimator.parameters)
            {
                if (parameter.name == "Attack" &&
                    parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    hasAttackTrigger = true;
                    break;
                }
            }
        }
    }

    private void Start()
    {
        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            player = playerObject.transform;

            playerHealth =
                playerObject.GetComponent<Health>();
        }
        else
        {
            Debug.LogWarning(
                "Khong tim thay Player!"
            );
        }

        if (enemyHealth != null)
        {
            enemyHealth.OnDied += Die;
        }
        else
        {
            Debug.LogWarning(
                gameObject.name + " khong co Health!"
            );
        }
    }

    private void Update()
    {
        if (Time.timeScale == 0f || isDead)
            return;

        if (player == null || playerHealth == null)
            return;

        attackTimer -= Time.deltaTime;

        float distance = Vector2.Distance(
            transform.position,
            player.position
        );

        if (distance <= attackRange &&
            attackTimer <= 0f)
        {
            // Phat animation khi den thoi diem gay damage.
            // Khong thay doi co che sat thuong va attack timer cu.
            if (hasAttackTrigger && attackAnimator != null)
            {
                attackAnimator.SetTrigger("Attack");
            }

            // MOI: Truyen Health cua quai.
            playerHealth.TakeDamage(
                damage,
                enemyHealth
            );

            attackTimer = attackInterval;
        }
    }

    private void FixedUpdate()
    {
        if (Time.timeScale == 0f || isDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (player == null)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 direction =
            (Vector2)(player.position - transform.position);

        float distance = direction.magnitude;

        if (distance > stopDistance)
        {
            // MOI: Slow khong thay doi moveSpeed goc.
            float speedMultiplier = slowEffect != null
                ? slowEffect.SpeedMultiplier
                : 1f;

            float effectiveSpeed =
                moveSpeed * speedMultiplier;

            rb.linearVelocity =
                direction.normalized * effectiveSpeed;
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        rb.linearVelocity = Vector2.zero;

        if (!rewardGiven)
        {
            rewardGiven = true;

            GiveRewards();
        }

        // Health.cs se tu huy quai.
    }

    private void GiveRewards()
    {
        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject == null)
        {
            Debug.LogWarning(
                "Khong tim thay Player de trao thuong."
            );
            return;
        }

        PlayerProgression progression =
            playerObject.GetComponent<PlayerProgression>();

        if (progression == null)
        {
            Debug.LogWarning(
                "Player thieu PlayerProgression!"
            );
            return;
        }

        if (expReward > 0)
        {
            progression.AddExp(expReward);
        }

        if (coinReward > 0)
        {
            progression.AddCoins(coinReward);
        }

        Debug.Log(
            gameObject.name +
            " thuong " + expReward +
            " EXP va " + coinReward + " Gold."
        );
    }

    private void OnDestroy()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDied -= Die;
        }
    }
}
