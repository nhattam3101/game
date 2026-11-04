
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class EnemyHitEffects : MonoBehaviour
{
    [Header("Visual Reference")]
    public SpriteRenderer targetSprite;

    [Header("Animation (Optional)")]
    [Tooltip("Gan Animator cua EliteEnemy; quai khong co Animator de trong.")]
    public Animator hitAnimator;

    private bool hasHitTrigger;

    [Header("Hit Flash")]
    public bool enableHitFlash = true;

    public Color flashColor = Color.white;

    [Min(0.01f)]
    public float flashDuration = 0.15f;

    [Header("Hit Shake")]
    public bool enableHitShake = true;

    [Min(0f)]
    public float shakeDistance = 0.06f;

    [Header("Death Effect")]
    public bool enableDeathEffect = true;

    [Min(0.05f)]
    public float deathDuration = 0.4f;

    [Min(0f)]
    public float deathRiseSpeed = 0.5f;

    [Range(0f, 1f)]
    public float deathFinalScale = 0.3f;

    private Health enemyHealth;
    private Color originalColor;
    private Vector3 originalLocalPosition;

    private float previousHealth;
    private bool hasDied;

    private Coroutine hitCoroutine;

    private void Start()
    {
        enemyHealth = GetComponent<Health>();


        // Tu dong tim Animator; khong bat buoc cho quai cu.
        if (hitAnimator == null)
        {
            hitAnimator = GetComponentInChildren<Animator>();
        }

        if (hitAnimator != null &&
            hitAnimator.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter parameter in
                     hitAnimator.parameters)
            {
                if (parameter.name == "Hit" &&
                    parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    hasHitTrigger = true;
                    break;
                }
            }
        }

        if (targetSprite == null)
        {
            targetSprite =
                GetComponentInChildren<SpriteRenderer>();
        }

        if (enemyHealth == null ||
            targetSprite == null)
        {
            Debug.LogWarning(
                "EnemyHitEffects: Thieu Health hoac SpriteRenderer!"
            );

            return;
        }

        originalColor = targetSprite.color;
        originalLocalPosition =
            targetSprite.transform.localPosition;

        previousHealth = enemyHealth.CurrentHealth;

        // Lang nghe HP thay doi
        enemyHealth.OnHealthChanged += OnHealthChanged;

        // Lang nghe quai chet
        enemyHealth.OnDied += OnEnemyDied;
    }

    private void OnHealthChanged(
        float currentHealth,
        float maxHealth)
    {
        if (hasDied)
            return;

        // Chi chay khi quai mat HP
        if (currentHealth < previousHealth)
        {
            // Quai con song moi phat animation Hit; khi chet de
            // he thong Death Effect hien tai xu ly nhu cu.
            if (currentHealth > 0f &&
                hasHitTrigger && hitAnimator != null)
            {
                hitAnimator.SetTrigger("Hit");
            }

            if (hitCoroutine != null)
            {
                StopCoroutine(hitCoroutine);
            }

            if (targetSprite != null)
            {
                targetSprite.color = originalColor;

                if (targetSprite.transform != transform)
                {
                    targetSprite.transform.localPosition =
                        originalLocalPosition;
                }
            }

            hitCoroutine = StartCoroutine(
                PlayHitEffect()
            );
        }

        previousHealth = currentHealth;
    }

    private IEnumerator PlayHitEffect()
    {
        if (targetSprite == null)
            yield break;

        float elapsed = 0f;

        if (enableHitFlash)
        {
            targetSprite.color = flashColor;
        }

        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsed / Mathf.Max(0.01f, flashDuration)
            );

            // Chuyen mau dan ve ban dau
            if (enableHitFlash &&
                targetSprite != null)
            {
                targetSprite.color = Color.Lerp(
                    flashColor,
                    originalColor,
                    progress
                );
            }

            // Chi rung phan hinh anh khi Sprite
            // nam trong GameObject con.
            // Khong di chuyen Rigidbody2D goc.
            if (enableHitShake &&
                targetSprite != null &&
                targetSprite.transform != transform)
            {
                Vector2 offset =
                    Random.insideUnitCircle *
                    shakeDistance *
                    (1f - progress);

                targetSprite.transform.localPosition =
                    originalLocalPosition +
                    new Vector3(
                        offset.x,
                        offset.y,
                        0f
                    );
            }

            yield return null;
        }

        if (targetSprite != null)
        {
            targetSprite.color = originalColor;

            if (targetSprite.transform != transform)
            {
                targetSprite.transform.localPosition =
                    originalLocalPosition;
            }
        }

        hitCoroutine = null;
    }

    private void OnEnemyDied()
    {
        if (hasDied)
            return;

        hasDied = true;

        if (hitCoroutine != null)
        {
            StopCoroutine(hitCoroutine);
            hitCoroutine = null;
        }

        if (targetSprite == null)
            return;

        // Khoi phuc hinh anh truoc khi tao bong
        targetSprite.color = originalColor;

        if (targetSprite.transform != transform)
        {
            targetSprite.transform.localPosition =
                originalLocalPosition;
        }

        if (enableDeathEffect)
        {
            CreateDeathEffect();
        }
    }

    private void CreateDeathEffect()
    {
        if (targetSprite.sprite == null)
            return;

        // Tao doi tuong hieu ung doc lap
        GameObject ghostObject = new GameObject(
            gameObject.name + "_DeathGhost"
        );

        ghostObject.transform.position =
            targetSprite.transform.position;

        ghostObject.transform.rotation =
            targetSprite.transform.rotation;

        ghostObject.transform.localScale =
            targetSprite.transform.lossyScale;

        // Sao chep hinh anh quai
        SpriteRenderer ghostRenderer =
            ghostObject.AddComponent<SpriteRenderer>();

        ghostRenderer.sprite = targetSprite.sprite;
        ghostRenderer.color = originalColor;

        ghostRenderer.flipX = targetSprite.flipX;
        ghostRenderer.flipY = targetSprite.flipY;

        ghostRenderer.sortingLayerID =
            targetSprite.sortingLayerID;

        ghostRenderer.sortingOrder =
            targetSprite.sortingOrder;

        ghostRenderer.sharedMaterial =
            targetSprite.sharedMaterial;

        // Them hieu ung mo dan
        EnemyDeathGhost ghostEffect =
            ghostObject.AddComponent<EnemyDeathGhost>();

        ghostEffect.Play(
            deathDuration,
            deathRiseSpeed,
            deathFinalScale
        );
    }

    private void OnDestroy()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnHealthChanged -=
                OnHealthChanged;

            enemyHealth.OnDied -= OnEnemyDied;
        }
    }
}
