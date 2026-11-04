using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    [Min(0.1f)] public float speed = 6f;
    [Min(0.1f)] public float lifetime = 4f;

    [Header("4B.5 - Obstacle Blocking")]
    [Tooltip("Choose the Obstacles layer only. Do not include Player, Enemy or Ground.")]
    public LayerMask blockingLayers;

    [Tooltip("Raycast ahead once per physics step so fast bullets cannot pass thin obstacles. Turn off if trigger collision alone is sufficient.")]
    public bool preventWallTunneling = true;

    [Min(0f)] public float wallCheckPadding = 0.05f;

    private Rigidbody2D rb;
    private Collider2D projectileCollider;
    private Health ownerHealth;
    private float damage;
    private bool hasHit;
    private bool initialized;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        projectileCollider = GetComponent<Collider2D>();
    }

    // Signature preserved: RangedEnemyController invokes this method.
    public void Initialize(Vector2 direction, float bulletDamage, Health shooterHealth)
    {
        damage = Mathf.Max(0f, bulletDamage);
        ownerHealth = shooterHealth;
        hasHit = false;
        initialized = true;

        rb.gravityScale = 0f;
        rb.linearVelocity = direction.normalized * speed;
        Destroy(gameObject, lifetime);
    }

    private bool IsBlockingLayer(int layer)
    {
        return (blockingLayers.value & (1 << layer)) != 0;
    }

    // A lightweight forward raycast complements collision callbacks for thin walls.
    private void FixedUpdate()
    {
        if (!initialized || hasHit || !preventWallTunneling || blockingLayers.value == 0)
            return;

        Vector2 velocity = rb.linearVelocity;
        float step = velocity.magnitude * Time.fixedDeltaTime;
        if (step <= 0.0001f)
            return;

        RaycastHit2D wall = Physics2D.Raycast(
            rb.position,
            velocity.normalized,
            step + wallCheckPadding,
            blockingLayers
        );

        if (wall.collider != null)
            HitObstacle();
    }

    private void HitObstacle()
    {
        if (hasHit)
            return;

        hasHit = true;
        rb.linearVelocity = Vector2.zero;

        if (projectileCollider != null)
            projectileCollider.enabled = false;

        Destroy(gameObject);
    }

    // Supports projectiles configured with non-trigger colliders for walls.
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (hasHit || collision == null)
            return;

        if (IsBlockingLayer(collision.gameObject.layer))
            HitObstacle();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit || other == null)
            return;

        // Cover takes priority over Player hit. This prevents shooting through trees/walls.
        if (IsBlockingLayer(other.gameObject.layer))
        {
            HitObstacle();
            return;
        }

        // Original behavior: ONLY damage a Player who has PlayerStats.
        PlayerStats playerStats = other.GetComponentInParent<PlayerStats>();
        if (playerStats == null)
            return;

        Health playerHealth = playerStats.GetComponent<Health>();
        if (playerHealth == null || playerHealth.CurrentHealth <= 0f)
            return;

        hasHit = true;

        // IMPORTANT: Preserve shooter Health for Reflect Shield compatibility.
        playerHealth.TakeDamage(damage, ownerHealth);

        Destroy(gameObject);
    }
}
