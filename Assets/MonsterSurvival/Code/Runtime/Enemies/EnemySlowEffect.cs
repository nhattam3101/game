
using UnityEngine;

public class EnemySlowEffect : MonoBehaviour
{
    [Header("Current Slow Status")]
    [SerializeField]
    [Range(0f, 1f)]
    private float currentSlowPercent = 0f;

    [SerializeField]
    private float remainingTime = 0f;

    // 1 = toc do binh thuong
    // 0.7 = giam 30% toc do
    public float SpeedMultiplier
    {
        get
        {
            return 1f - currentSlowPercent;
        }
    }

    public bool IsSlowed
    {
        get
        {
            return remainingTime > 0f &&
                   currentSlowPercent > 0f;
        }
    }

    private void Update()
    {
        if (remainingTime <= 0f)
            return;

        // Time.deltaTime tu dong dung khi
        // Time.timeScale = 0 (Pause Menu).
        remainingTime -= Time.deltaTime;

        if (remainingTime <= 0f)
        {
            RemoveSlow();
        }
    }

    // Duoc goi khi quai trung Slow Bullet.
    public void ApplySlow(
        float slowPercent,
        float duration)
    {
        float validSlow = Mathf.Clamp(
            slowPercent,
            0f,
            0.95f
        );

        float validDuration = Mathf.Max(
            0f,
            duration
        );

        if (validSlow <= 0f ||
            validDuration <= 0f)
        {
            return;
        }

        // Giu hieu ung manh nhat,
        // khong cong don Slow vo han.
        currentSlowPercent = Mathf.Max(
            currentSlowPercent,
            validSlow
        );

        // Lam moi thoi gian neu trung dan tiep.
        remainingTime = Mathf.Max(
            remainingTime,
            validDuration
        );

        Debug.Log(
            gameObject.name +
            " slowed: " +
            (currentSlowPercent * 100f) +
            "% for " +
            remainingTime +
            " seconds."
        );
    }

    public void RemoveSlow()
    {
        currentSlowPercent = 0f;
        remainingTime = 0f;
    }

    private void OnDisable()
    {
        RemoveSlow();
    }
}
