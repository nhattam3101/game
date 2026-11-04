using UnityEngine;

// Lightweight, per-enemy status visuals. Reuses particle systems on refresh;
// uses scaled game time so Pause stops the effects without desynchronizing duration.
[DisallowMultipleComponent]
public sealed class MS7B3StatusVisual : MonoBehaviour
{
    private Health health;
    private ParticleSystem fire;
    private ParticleSystem ice;
    private float fireUntil;
    private float iceUntil;

    private void Awake() { health = GetComponent<Health>(); }

    public void ShowFire(float seconds, Material material, float size)
    {
        fireUntil = Mathf.Max(fireUntil, Time.time + Mathf.Max(0.1f, seconds));
        if (fire == null)
            fire = MakeParticles("7B3_FireEmbers", new Color(1f, .36f, .08f, .9f),
                13f, .65f, .6f, size, material);
        if (!fire.isPlaying) fire.Play();
    }

    public void ShowIce(float seconds, Material material, float size)
    {
        iceUntil = Mathf.Max(iceUntil, Time.time + Mathf.Max(0.1f, seconds));
        if (ice == null)
            ice = MakeParticles("7B3_FrostCrystals", new Color(.48f, .91f, 1f, .83f),
                8f, .85f, .17f, size * .85f, material);
        if (!ice.isPlaying) ice.Play();
    }

    private ParticleSystem MakeParticles(string name, Color tint, float rate,
        float lifetime, float velocity, float size, Material material)
    {
        GameObject holder = new GameObject(name);
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = Vector3.zero;
        ParticleSystem particles = holder.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = lifetime;
        main.startSpeed = velocity;
        main.startSize = Mathf.Max(.025f, size);
        main.startColor = tint;
        main.maxParticles = 24;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.playOnAwake = false;
        var emission = particles.emission;
        emission.rateOverTime = rate;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = .35f;
        var color = particles.colorOverLifetime;
        color.enabled = true;
        Gradient alpha = new Gradient();
        alpha.SetKeys(new[] {
            new GradientColorKey(Color.white, 0f),
            new GradientColorKey(Color.white, 1f)
        }, new[] {
            new GradientAlphaKey(0f, 0f),
            new GradientAlphaKey(1f, .2f),
            new GradientAlphaKey(.7f, .65f),
            new GradientAlphaKey(0f, 1f)
        });
        color.color = new ParticleSystem.MinMaxGradient(alpha);
        ParticleSystemRenderer renderer = holder.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = 130;
        if (material != null) renderer.sharedMaterial = material;
        return particles;
    }

    private void Update()
    {
        if (health == null || health.CurrentHealth <= 0f)
        {
            Destroy(this);
            return;
        }
        // Burn VFX ends when the existing Burn component ends, not merely by a timer.
        if (fire != null && fire.isPlaying &&
            (Time.time >= fireUntil || GetComponent<MS7B1BurnStatus>() == null))
            fire.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (ice != null && ice.isPlaying && Time.time >= iceUntil)
            ice.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (Time.time >= fireUntil + 2f && Time.time >= iceUntil + 2f)
            Destroy(this);
    }

    private void OnDestroy()
    {
        if (fire != null) Destroy(fire.gameObject);
        if (ice != null) Destroy(ice.gameObject);
    }
}
