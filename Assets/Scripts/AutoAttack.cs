
using UnityEngine;

public class AutoAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    public float attackRange = 3f;
    public float damage = 10f;
    public float attackInterval = 1f;
    public LayerMask enemyLayer;

    [Header("Player Stats")]
    private PlayerStats playerStats;

    private float nextAttackTime;

    private void Awake()
    {
        // Lấy chỉ số nâng cấp từ Player
        playerStats = GetComponent<PlayerStats>();
    }

    private void Update()
    {
        // Không tấn công khi game đang tạm dừng
        if (Time.timeScale == 0f)
            return;

        // Chưa đến thời gian tấn công tiếp theo
        if (Time.time < nextAttackTime)
            return;

        // Tìm các quái vật trong phạm vi tấn công
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            attackRange,
            enemyLayer
        );

        Health nearestHealth = null;
        float nearestDistance = float.MaxValue;

        // Tìm quái vật còn sống gần nhất
        foreach (Collider2D hit in hits)
        {
            Health target = hit.GetComponent<Health>();

            if (target == null || target.CurrentHealth <= 0f)
                continue;

            float distance = Vector2.Distance(
                transform.position,
                hit.transform.position
            );

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestHealth = target;
            }
        }

        // Không có quái trong phạm vi
        if (nearestHealth == null)
            return;

        // Lấy hệ số sát thương
        float damageMultiplier = playerStats != null
            ? playerStats.damageMultiplier
            : 1f;

        // Gây sát thương lên quái gần nhất
        nearestHealth.TakeDamage(damage * damageMultiplier);

        // Lấy hệ số tốc độ tấn công
        float speedMultiplier = playerStats != null
            ? playerStats.attackSpeedMultiplier
            : 1f;

        // Tính thời gian chờ giữa hai đòn đánh
        float effectiveInterval = attackInterval
            / Mathf.Max(0.1f, speedMultiplier);

        nextAttackTime = Time.time + effectiveInterval;
    }

    private void OnDrawGizmosSelected()
    {
        // Vẽ phạm vi tấn công trong Scene View
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(
            transform.position,
            attackRange
        );
    }
}
