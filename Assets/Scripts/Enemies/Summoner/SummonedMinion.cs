using UnityEngine;

// Gắn vào MinionEnemy Prefab, hoặc được Summoner tự thêm khi tạo Minion.
// Không ghi đè Health/EnemyController, nên đạn, Slow, EXP/Gold vẫn hoạt động.
[RequireComponent(typeof(Health))]
public class SummonedMinion : MonoBehaviour
{
    [Min(1f)] public float lifetime = 18f;
    private SummonerEnemyAI owner;
    private Health myHealth;
    private bool despawning;
    private float timeLeft;

    private void Awake()
    {
        myHealth = GetComponent<Health>();
        timeLeft = lifetime;
    }

    public void Initialize(SummonerEnemyAI newOwner, float newLifetime)
    {
        owner = newOwner;
        lifetime = Mathf.Max(1f, newLifetime);
        timeLeft = lifetime;
    }

    private void Update()
    {
        if (despawning || Time.timeScale == 0f) return;
        if (myHealth != null && myHealth.CurrentHealth <= 0f) return;
        if (owner == null)
        {
            DespawnFromOwner();
            return;
        }
        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f) DespawnFromOwner();
    }

    public void DespawnFromOwner()
    {
        if (despawning) return;
        despawning = true;
        // Destroy trực tiếp KHÔNG gọi Health.OnDied, tránh cộng EXP/Gold khi tự biến mất.
        Destroy(gameObject);
    }
}
