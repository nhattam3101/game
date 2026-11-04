using UnityEngine;

// One instance per enemy, refreshes strongest DPS and duration instead of stacking components.
[DisallowMultipleComponent]
public sealed class MS7B1BurnStatus : MonoBehaviour
{
    private Health health;
    private float secondsLeft;
    private float tickSeconds = 0.5f;
    private float timeToTick;
    private float damagePerSecond;

    public void Apply(float duration, float dps, float interval)
    {
        if (duration <= 0f || dps <= 0f) return;
        if (health == null) health = GetComponent<Health>();
        if (health == null || health.CurrentHealth <= 0f) return;

        tickSeconds = Mathf.Max(0.1f, interval);
        damagePerSecond = Mathf.Max(damagePerSecond, dps);
        secondsLeft = Mathf.Max(secondsLeft, duration);
        if (timeToTick <= 0f) timeToTick = tickSeconds;
        enabled = true;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (health == null) health = GetComponent<Health>();
        if (health == null || health.CurrentHealth <= 0f) { Destroy(this); return; }
        if (secondsLeft <= 0f) { Destroy(this); return; }

        float step = Mathf.Min(Time.deltaTime, secondsLeft);
        secondsLeft -= Time.deltaTime;
        timeToTick -= step;
        // Cap loops to avoid huge tick bursts after unusual frame stalls.
        int ticks = 0;
        while (timeToTick <= 0f && ticks++ < 4 && health != null && health.CurrentHealth > 0f)
        {
            health.TakeDamage(damagePerSecond * tickSeconds);
            timeToTick += tickSeconds;
        }
        if (secondsLeft <= 0f) Destroy(this);
    }
}
