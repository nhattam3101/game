using UnityEngine;

/// <summary>
/// VISUAL ONLY. Does not move the enemy, deal damage, or change XP/Gold/A*.
/// Attach to the ROOT of the duplicated minion prefab, not to GW_Visual.
/// </summary>
[DisallowMultipleComponent]
public class GWMinionAnimationBridge5B3 : MonoBehaviour
{
    [Header("Animated child")]
    public Animator visualAnimator;
    public SpriteRenderer visualRenderer;
    public bool isWraith = true;

    [Header("Animation timing")]
    [Min(0.02f)] public float transitionSeconds = 0.07f;
    [Min(0.01f)] public float movingVelocityThreshold = 0.10f;
    public bool showDeathAnimation = true;
    public bool showExtraAnimations = true;

    private Rigidbody2D body;
    private EnemyController enemy;
    private Health health;
    private Transform player;
    private float lastHealth;
    private float actionEnd;
    private float nextAttackVisual;
    private float nextExtra;
    private bool dead;
    private bool empoweredPlayed;
    private string currentState;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        enemy = GetComponent<EnemyController>();
        health = GetComponent<Health>();
        if (visualAnimator == null)
        {
            Transform visual = transform.Find("GW_Visual");
            if (visual != null) visualAnimator = visual.GetComponent<Animator>();
        }
        if (visualRenderer == null && visualAnimator != null)
            visualRenderer = visualAnimator.GetComponent<SpriteRenderer>();
        if (health != null) lastHealth = health.CurrentHealth;
    }

    private void OnEnable()
    {
        if (health == null) health = GetComponent<Health>();
        if (health != null)
        {
            health.OnHealthChanged += OnHealthChanged;
            health.OnDied += OnDied;
            lastHealth = health.CurrentHealth;
        }
    }

    private void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null) player = playerObject.transform;
        if (!Ready()) return;
        SetState("Spawn", true);
        actionEnd = Time.time + ClipSeconds("Spawn", 0.85f);
        nextAttackVisual = Time.time + 0.05f;
        nextExtra = Time.time + Random.Range(4f, 6f);
    }

    private void Update()
    {
        if (dead || Time.timeScale <= 0f || !Ready()) return;
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }
        if (Time.time < actionEnd) return;

        // Animation ONLY: EnemyController remains responsible for actual hits.
        if (enemy != null && enemy.isActiveAndEnabled && player != null)
        {
            float attackDistance = Mathf.Max(0.1f, enemy.attackRange);
            float distance = Vector2.Distance(transform.position, player.position);
            if (distance <= attackDistance && Time.time >= nextAttackVisual)
            {
                PlayAction("Attack", 0.55f);
                nextAttackVisual = Time.time + Mathf.Max(0.3f, enemy.attackInterval);
                return;
            }
        }

        if (showExtraAnimations && !empoweredPlayed && health != null &&
            health.CurrentHealth > 0f && health.CurrentHealth <= health.maxHealth * 0.45f && !isWraith)
        {
            empoweredPlayed = true;
            PlayAction("Empower", 0.65f);
            return;
        }

        bool moving = body != null &&
            body.linearVelocity.sqrMagnitude > movingVelocityThreshold * movingVelocityThreshold;
        if (showExtraAnimations && Time.time >= nextExtra && !moving)
        {
            string extra = isWraith ? "SoulCharge" : "Guard";
            PlayAction(extra, 0.75f);
            nextExtra = Time.time + Random.Range(5f, 8f);
            return;
        }
        SetState(moving ? (isWraith && health != null &&
                     health.CurrentHealth < health.maxHealth * 0.45f ? "HoverFast" : "Move")
                        : "Idle", false);
    }

    private void OnHealthChanged(float current, float maximum)
    {
        if (!dead && current > 0f && current < lastHealth - 0.01f && Ready())
            PlayAction("Hit", 0.35f);
        lastHealth = current;
    }

    private void OnDied()
    {
        if (dead) return;
        dead = true;
        if (!showDeathAnimation || !Ready() || visualRenderer == null) return;

        // Health destroys the gameplay enemy immediately. A standalone sprite
        // clone plays Death without delaying rewards / boss soul collection.
        GameObject effect = new GameObject(isWraith ? "GW_Wraith_Death_Visual" : "GW_Warrior_Death_Visual");
        Transform t = visualRenderer.transform;
        effect.transform.SetPositionAndRotation(t.position, t.rotation);
        effect.transform.localScale = t.lossyScale;
        SpriteRenderer sr = effect.AddComponent<SpriteRenderer>();
        sr.sprite = visualRenderer.sprite;
        sr.color = visualRenderer.color;
        sr.sortingLayerID = visualRenderer.sortingLayerID;
        sr.sortingOrder = visualRenderer.sortingOrder;
        sr.flipX = visualRenderer.flipX;
        sr.flipY = visualRenderer.flipY;
        Animator animator = effect.AddComponent<Animator>();
        animator.runtimeAnimatorController = visualAnimator.runtimeAnimatorController;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Play("Death", 0, 0f);
        Destroy(effect, ClipSeconds("Death", 1.2f) + 0.10f);
    }

    private bool Ready()
    {
        return visualAnimator != null && visualAnimator.runtimeAnimatorController != null;
    }

    private void PlayAction(string state, float fallbackLength)
    {
        if (!Ready()) return;
        SetState(state, true);
        actionEnd = Time.time + ClipSeconds(state, fallbackLength);
    }

    private void SetState(string state, bool force)
    {
        if (!Ready() || (!force && currentState == state)) return;
        if (force) visualAnimator.Play(state, 0, 0f);
        else visualAnimator.CrossFadeInFixedTime(state, transitionSeconds, 0);
        currentState = state;
    }

    private float ClipSeconds(string state, float fallback)
    {
        if (!Ready()) return fallback;
        string ending = "_" + state;
        AnimationClip[] clips = visualAnimator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null && clips[i].name.EndsWith(ending))
                return Mathf.Max(0.1f, clips[i].length);
        }
        return fallback;
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnHealthChanged -= OnHealthChanged;
            health.OnDied -= OnDied;
        }
    }
}
