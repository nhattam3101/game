
using UnityEngine;

[RequireComponent(typeof(Bullet))]
public class ProjectileVisualController : MonoBehaviour
{
    [Header("Visual References")]
    public SpriteRenderer bulletRenderer;
    public TrailRenderer bulletTrail;

    private Bullet bullet;
    private Color originalColor;

    private void Awake()
    {
        bullet = GetComponent<Bullet>();

        if (bulletRenderer == null)
        {
            bulletRenderer =
                GetComponent<SpriteRenderer>();
        }

        if (bulletRenderer != null)
        {
            originalColor = bulletRenderer.color;
        }

        if (bulletTrail != null)
        {
            bulletTrail.enabled = false;
        }
    }

    private void Start()
    {
        ApplyVisualEffect();
    }

    private void ApplyVisualEffect()
    {
        if (bullet == null)
            return;

        ProjectileEffectData effect =
            bullet.projectileEffect;

        // Dan thuong: giu nguyen hinh anh goc.
        if (effect == null ||
            effect.effectType == ProjectileEffectType.None)
        {
            if (bulletRenderer != null)
            {
                bulletRenderer.color = originalColor;
            }

            if (bulletTrail != null)
            {
                bulletTrail.enabled = false;
            }

            return;
        }

        // Dan dac biet: doi mau theo Effect Data.
        Color effectColor = effect.projectileColor;

        if (bulletRenderer != null)
        {
            bulletRenderer.color = effectColor;
        }

        // Tao vet sang theo mau dan.
        if (bulletTrail != null)
        {
            bulletTrail.Clear();

            bulletTrail.startColor = effectColor;

            bulletTrail.endColor = new Color(
                effectColor.r,
                effectColor.g,
                effectColor.b,
                0f
            );

            bulletTrail.enabled = true;
        }
    }
}
