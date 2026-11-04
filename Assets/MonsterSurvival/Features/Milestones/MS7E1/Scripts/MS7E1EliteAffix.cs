using TMPro;
using UnityEngine;

/// <summary>
/// 7E.1: Adds exactly one optional affix to an EXISTING EliteEnemy prefab.
/// Does not touch WaveManager, EliteSpawnDirector, rewards, save data or the existing Enrage.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(Health))]
public sealed class MS7E1EliteAffix : MonoBehaviour
{
    public enum AffixKind { Random, Bulwark, Swift, Regenerator }

    [Header("One affix per spawned elite")]
    [SerializeField] private AffixKind selection = AffixKind.Random;

    [Header("Bulwark - extra health")]
    [Min(1f)] [SerializeField] private float bulwarkMaxHealthMultiplier = 1.45f;

    [Header("Swift - extra speed and damage")]
    [Min(1f)] [SerializeField] private float swiftMoveSpeedMultiplier = 1.28f;
    [Min(1f)] [SerializeField] private float swiftDamageMultiplier = 1.10f;

    [Header("Regenerator - healing between hits")]
    [Range(0.001f, 0.20f)] [SerializeField] private float regenFractionOfMaxHealth = 0.025f;
    [Min(0.2f)] [SerializeField] private float regenInterval = 4f;
    [Min(0f)] [SerializeField] private float regenDelayAfterTakingDamage = 3f;

    [Header("World-space identifier (no HUD changes)")]
    [SerializeField] private bool showAffixLabel = true;
    [SerializeField] private float labelYOffset = 1.10f;
    [Range(0.05f, 0.35f)] [SerializeField] private float labelScale = 0.15f;

    private EnemyController controller;
    private Health health;
    private AffixKind activeAffix;
    private bool initialized;
    private bool subscribed;
    private float lastSeenHealth;
    private float nextRegenAt;

    public bool Initialized { get { return initialized; } }
    public AffixKind ActiveAffix { get { return activeAffix; } }
    public string ActiveAffixName { get { return activeAffix.ToString(); } }

    private void Awake()
    {
        controller = GetComponent<EnemyController>();
        health = GetComponent<Health>();
    }

    private void Start()
    {
        if (initialized || controller == null || health == null)
        {
            if (controller == null || health == null)
            {
                Debug.LogWarning("[7E.1] Elite missing EnemyController or Health: " + name, this);
                enabled = false;
            }
            return;
        }

        // Unity Random's integer upper bound is EXCLUSIVE. Each spawn gets one fixed affix.
        activeAffix = selection == AffixKind.Random
            ? (AffixKind)Random.Range((int)AffixKind.Bulwark, (int)AffixKind.Regenerator + 1)
            : selection;

        if (activeAffix == AffixKind.Bulwark)
        {
            float before = Mathf.Max(1f, health.maxHealth);
            float after = before * Mathf.Max(1f, bulwarkMaxHealthMultiplier);
            health.maxHealth = after;
            health.Heal(after - before); // Heal() raises the existing Health changed event.
        }
        else if (activeAffix == AffixKind.Swift)
        {
            controller.moveSpeed *= Mathf.Max(1f, swiftMoveSpeedMultiplier);
            controller.damage *= Mathf.Max(1f, swiftDamageMultiplier);
        }

        initialized = true; // Never apply the stat multipliers twice on enable/disable.
        lastSeenHealth = health.CurrentHealth;
        nextRegenAt = Time.time + Mathf.Max(0.2f, regenInterval);
        SubscribeForRegen();

        if (showAffixLabel)
            CreateLabel();
    }

    private void Update()
    {
        if (!initialized || activeAffix != AffixKind.Regenerator ||
            health == null || Time.timeScale <= 0f || health.CurrentHealth <= 0f)
            return;

        if (Time.time < nextRegenAt)
            return;

        nextRegenAt = Time.time + Mathf.Max(0.2f, regenInterval);
        if (health.CurrentHealth < health.maxHealth)
            health.Heal(Mathf.Max(0f, health.maxHealth * regenFractionOfMaxHealth));
    }

    private void OnEnable()
    {
        SubscribeForRegen();
    }

    private void OnDisable()
    {
        UnsubscribeForRegen();
    }

    private void OnDestroy()
    {
        UnsubscribeForRegen();
    }

    private void SubscribeForRegen()
    {
        if (subscribed || !initialized || activeAffix != AffixKind.Regenerator || health == null)
            return;

        health.OnHealthChanged += OnHealthChanged;
        subscribed = true;
    }

    private void UnsubscribeForRegen()
    {
        if (!subscribed || health == null)
            return;

        health.OnHealthChanged -= OnHealthChanged;
        subscribed = false;
    }

    private void OnHealthChanged(float current, float maximum)
    {
        if (current < lastSeenHealth - 0.0001f)
            nextRegenAt = Mathf.Max(nextRegenAt, Time.time + regenDelayAfterTakingDamage);

        lastSeenHealth = current;
    }

    private void CreateLabel()
    {
        // Visual is a child of the enemy, not a Canvas/HUD overlay.
        if (transform.Find("MS7E1_AffixLabel") != null)
            return;

        GameObject labelObject = new GameObject("MS7E1_AffixLabel");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = new Vector3(0f, labelYOffset, -0.05f);
        labelObject.transform.localScale = Vector3.one * labelScale;

        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.text = activeAffix == AffixKind.Bulwark ? "BULWARK" :
                     activeAffix == AffixKind.Swift ? "SWIFT" : "REGEN";
        label.fontSize = 4f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.color = activeAffix == AffixKind.Bulwark
            ? new Color(1f, 0.77f, 0.34f, 1f)
            : activeAffix == AffixKind.Swift
                ? new Color(0.76f, 0.52f, 1f, 1f)
                : new Color(0.44f, 0.93f, 0.86f, 1f);

        label.rectTransform.sizeDelta = new Vector2(6f, 1.2f);
        MeshRenderer renderer = labelObject.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.sortingOrder = 50;
    }

    private void OnValidate()
    {
        bulwarkMaxHealthMultiplier = Mathf.Max(1f, bulwarkMaxHealthMultiplier);
        swiftMoveSpeedMultiplier = Mathf.Max(1f, swiftMoveSpeedMultiplier);
        swiftDamageMultiplier = Mathf.Max(1f, swiftDamageMultiplier);
        regenInterval = Mathf.Max(0.2f, regenInterval);
        regenDelayAfterTakingDamage = Mathf.Max(0f, regenDelayAfterTakingDamage);
    }
}
