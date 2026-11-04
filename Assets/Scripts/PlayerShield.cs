
using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerShield : MonoBehaviour
{
    [Header("Shield Settings")]
    [SerializeField, Min(0)]
    private int shieldCharges = 0;

    [SerializeField, Min(0)]
    private int maxShieldCharges = 0;

    [Min(1)]
    public int shieldLayerLimit = 5;

    [Header("Auto Recharge")]
    [Min(0.1f)]
    public float rechargeDelay = 8f;

    private float rechargeTimer = 0f;

    [Header("Reflect Damage")]
    [Range(0f, 1f)]
    public float reflectDamagePercent = 0f;

    [Header("Shield Visual")]
    public GameObject shieldVisual;
    public TMP_Text shieldCountText;

    [Header("Shield Break Effect")]
    [Min(0.05f)]
    public float breakEffectDuration = 0.4f;

    [Min(1f)]
    public float breakScaleMultiplier = 1.4f;

    [Range(0, 16)]
    public int fragmentCount = 8;

    [Min(0f)]
    public float fragmentDistance = 1.2f;

    private Transform visualTransform;
    private SpriteRenderer visualRenderer;
    private Vector3 originalScale;
    private Color originalColor;

    private Coroutine breakRoutine;

    public int ShieldCharges
    {
        get { return shieldCharges; }
    }

    public int MaxShieldCharges
    {
        get { return maxShieldCharges; }
    }

    private void Awake()
    {
        shieldLayerLimit = Mathf.Max(
            1,
            shieldLayerLimit
        );

        maxShieldCharges = Mathf.Clamp(
            maxShieldCharges,
            0,
            shieldLayerLimit
        );

        shieldCharges = Mathf.Clamp(
            shieldCharges,
            0,
            maxShieldCharges
        );

        CacheVisual();
    }

    private void Start()
    {
        UpdateShieldVisual();
    }

    private void Update()
    {
        if (Time.timeScale == 0f)
            return;

        if (maxShieldCharges <= 0)
            return;

        if (shieldCharges >= maxShieldCharges)
            return;

        rechargeTimer += Time.deltaTime;

        if (rechargeTimer < rechargeDelay)
            return;

        shieldCharges = Mathf.Min(
            shieldCharges + 1,
            maxShieldCharges
        );

        rechargeTimer = 0f;

        UpdateShieldVisual();

        Debug.Log(
            "La chan hoi phuc: " +
            shieldCharges + "/" + maxShieldCharges
        );
    }

    // Tang so lop la chan khi nang cap Shield.
    public void AddCharges(int amount)
    {
        if (amount <= 0)
            return;

        maxShieldCharges = Mathf.Min(
            maxShieldCharges + amount,
            shieldLayerLimit
        );

        shieldCharges = Mathf.Min(
            shieldCharges + amount,
            maxShieldCharges
        );

        rechargeTimer = 0f;

        UpdateShieldVisual();

        Debug.Log(
            "Nang cap la chan: " +
            shieldCharges + "/" + maxShieldCharges
        );
    }

    // MOI: Nang cap phan tram phan sat thuong.
    // Vi du amount = 0.5f tuc la tang 50%.
    public void AddReflection(float amount)
    {
        if (amount <= 0f)
            return;

        reflectDamagePercent = Mathf.Clamp01(
            reflectDamagePercent + amount
        );

        Debug.Log(
            "Phan sat thuong hien tai: " +
            (reflectDamagePercent * 100f) + "%"
        );
    }

    // Giu ham cu de tuong thich code hien tai.
    public bool TryBlockHit()
    {
        return TryBlockHit(0f, null);
    }

    // MOI: Chan sat thuong va phan don ve ke tan cong.
    public bool TryBlockHit(
        float incomingDamage,
        Health attacker
    )
    {
        if (shieldCharges <= 0)
            return false;

        shieldCharges--;

        rechargeTimer = 0f;

        Debug.Log(
            "La chan da chan don danh! Con: " +
            shieldCharges + "/" + maxShieldCharges
        );

        if (shieldCharges == 0)
        {
            PlayBreakEffect();
        }
        else
        {
            UpdateShieldVisual();
        }

        UpdateShieldCounter();

        // Chi phan sat thuong neu:
        // - Da mo khoa nang cap
        // - Biet ke tan cong
        // - Ke tan cong van con song
        if (reflectDamagePercent > 0f &&
            incomingDamage > 0f &&
            attacker != null &&
            attacker.gameObject != gameObject &&
            attacker.CurrentHealth > 0f)
        {
            float reflectedDamage =
                incomingDamage * reflectDamagePercent;

            // Gay sat thuong nguoc lai len quai.
            attacker.TakeDamage(reflectedDamage);

            Debug.Log(
                "La chan phan lai " +
                reflectedDamage + " damage!"
            );
        }

        return true;
    }

    private void CacheVisual()
    {
        if (shieldVisual == null)
            return;

        visualTransform = shieldVisual.transform;
        originalScale = visualTransform.localScale;

        visualRenderer =
            shieldVisual.GetComponentInChildren<
                SpriteRenderer
            >(true);

        if (visualRenderer != null)
        {
            originalColor = visualRenderer.color;
        }
    }

    private void RestoreVisual()
    {
        if (visualTransform != null)
        {
            visualTransform.localScale = originalScale;
        }

        if (visualRenderer != null)
        {
            visualRenderer.color = originalColor;
        }
    }

    private void UpdateShieldCounter()
    {
        if (shieldCountText == null)
            return;

        shieldCountText.text =
            "Shield: " +
            shieldCharges + "/" + maxShieldCharges;
    }

    private void UpdateShieldVisual()
    {
        UpdateShieldCounter();

        if (shieldVisual == null)
            return;

        if (shieldCharges > 0)
        {
            if (breakRoutine == null)
            {
                RestoreVisual();
            }

            shieldVisual.SetActive(true);
        }
        else if (breakRoutine == null)
        {
            RestoreVisual();
            shieldVisual.SetActive(false);
        }
    }

    private void PlayBreakEffect()
    {
        if (shieldVisual == null)
        {
            UpdateShieldVisual();
            return;
        }

        if (breakRoutine != null)
            return;

        shieldVisual.SetActive(true);

        breakRoutine = StartCoroutine(
            AnimateShieldBreak()
        );
    }

    private IEnumerator AnimateShieldBreak()
    {
        float duration = Mathf.Max(
            0.05f,
            breakEffectDuration
        );

        int count = Mathf.Clamp(
            fragmentCount,
            0,
            16
        );

        GameObject[] fragments =
            new GameObject[count];

        Vector2[] directions =
            new Vector2[count];

        Vector3 center =
            shieldVisual.transform.position;

        // Tao cac manh vo khi con SpriteRenderer.
        if (visualRenderer != null &&
            visualRenderer.sprite != null)
        {
            for (int i = 0; i < count; i++)
            {
                float angle =
                    i * Mathf.PI * 2f /
                    Mathf.Max(1, count);

                directions[i] = new Vector2(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle)
                );

                GameObject fragment =
                    new GameObject("ShieldFragment");

                fragment.transform.SetParent(
                    transform,
                    true
                );

                fragment.transform.position = center;

                fragment.transform.localScale =
                    Vector3.one * 0.15f;

                SpriteRenderer renderer =
                    fragment.AddComponent<SpriteRenderer>();

                renderer.sprite = visualRenderer.sprite;

                renderer.sortingLayerID =
                    visualRenderer.sortingLayerID;

                renderer.sortingOrder =
                    visualRenderer.sortingOrder + 1;

                renderer.color = originalColor;

                fragments[i] = fragment;
            }
        }

        float elapsed = 0f;

        while (elapsed < duration &&
               shieldCharges == 0)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsed / duration
            );

            if (visualTransform != null)
            {
                visualTransform.localScale =
                    Vector3.Lerp(
                        originalScale,
                        originalScale * breakScaleMultiplier,
                        progress
                    );
            }

            if (visualRenderer != null)
            {
                Color color = originalColor;
                color.a *= 1f - progress;

                visualRenderer.color = color;
            }

            for (int i = 0; i < count; i++)
            {
                if (fragments[i] == null)
                    continue;

                fragments[i].transform.position =
                    center +
                    (Vector3)(
                        directions[i] *
                        fragmentDistance *
                        progress
                    );

                SpriteRenderer renderer =
                    fragments[i].GetComponent<SpriteRenderer>();

                if (renderer != null)
                {
                    Color color = originalColor;
                    color.a *= 1f - progress;
                    renderer.color = color;
                }
            }

            yield return null;
        }

        for (int i = 0; i < count; i++)
        {
            if (fragments[i] != null)
            {
                Destroy(fragments[i]);
            }
        }

        RestoreVisual();

        breakRoutine = null;

        UpdateShieldVisual();
    }
}
