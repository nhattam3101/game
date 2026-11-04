
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;

    [Header("Player Stats")]
    private PlayerStats playerStats;

    private Rigidbody2D rb;
    private Vector2 movement;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerStats = GetComponent<PlayerStats>();
    }

    private void Update()
    {
        // Đọc phím di chuyển
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        movement = new Vector2(moveX, moveY).normalized;
    }

    private void FixedUpdate()
    {
        // Dừng di chuyển khi game tạm dừng
        if (Time.timeScale == 0f)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // Lấy hệ số tốc độ từ PlayerStats
        float speedMultiplier = playerStats != null
            ? playerStats.moveSpeedMultiplier
            : 1f;

        // Di chuyển nhân vật
        rb.linearVelocity = movement * moveSpeed * speedMultiplier;
    }
}
