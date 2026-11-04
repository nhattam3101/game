
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Min(0.1f)]
    public float moveSpeed = 5f;

    [Header("Dash")]
    public bool enableDash = true;
    [Min(1f)] public float dashSpeed = 13f;
    [Min(0.05f)] public float dashDuration = 0.16f;
    [Min(0.1f)] public float dashCooldown = 1.15f;
    [Min(0f)] public float dashInvulnerabilityBonus = 0.06f;

    public bool IsDashing => dashTimeRemaining > 0f;
    public float DashCooldownRemaining => Mathf.Max(0f, nextDashTime - Time.time);

    [Header("Player Stats")]
    private PlayerStats playerStats;

    private Rigidbody2D rb;
    private Vector2 movement;
    private Vector2 lastMoveDirection = Vector2.down;
    private Vector2 dashDirection;
    private float dashTimeRemaining;
    private float nextDashTime;
    private Health health;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerStats = GetComponent<PlayerStats>();
        health = GetComponent<Health>();
    }

    private void Update()
    {
        // Đọc phím di chuyển
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        movement = new Vector2(moveX, moveY).normalized;

        if (movement.sqrMagnitude > 0.001f)
            lastMoveDirection = movement;

        if (enableDash && DashPressed() && Time.timeScale > 0f &&
            Time.time >= nextDashTime)
        {
            dashDirection = movement.sqrMagnitude > 0.001f
                ? movement
                : lastMoveDirection;
            dashTimeRemaining = dashDuration;
            nextDashTime = Time.time + dashCooldown;
            if (health != null)
                health.GrantInvulnerability(dashDuration + dashInvulnerabilityBonus);
        }
    }

    private void FixedUpdate()
    {
        // Dừng di chuyển khi game tạm dừng
        if (Time.timeScale == 0f)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (dashTimeRemaining > 0f)
        {
            dashTimeRemaining = Mathf.Max(0f, dashTimeRemaining - Time.fixedDeltaTime);
            rb.linearVelocity = dashDirection * dashSpeed;
            return;
        }

        // Lấy hệ số tốc độ từ PlayerStats
        float speedMultiplier = playerStats != null
            ? playerStats.moveSpeedMultiplier
            : 1f;

        // Di chuyển nhân vật
        rb.linearVelocity = movement * moveSpeed * speedMultiplier;
    }

    private static bool DashPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Space);
#else
        return false;
#endif
    }
}
