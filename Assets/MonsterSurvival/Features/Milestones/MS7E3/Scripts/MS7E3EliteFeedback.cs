using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MonsterSurvival 7E.3: visual-only Elite combat feedback.
/// Keeps EnemyController, Health, EliteEnemy, 7E.1 stats, drops and Wave untouched.
/// Attach to the SAME existing Elite prefab as MS7E1EliteAffix.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(EliteEnemy))]
[RequireComponent(typeof(MS7E1EliteAffix))]
public sealed class MS7E3EliteFeedback : MonoBehaviour
{
    [Header("Visual-only master switch")]
    public bool enableFeedback = true;

    [Header("Affix ring: world-space, no HUD")]
    public bool showAffixRing = true;
    [Range(0.15f, 1.8f)] public float ringRadius = 0.48f;
    [Range(-1f, 1f)] public float ringYOffset = -0.20f;
    [Range(0.015f, 0.15f)] public float ringThickness = 0.048f;
    [Range(0f, 0.2f)] public float ringIdlePulse = 0.045f;

    [Header("Damage feedback")]
    public bool enableHitFlash = true;
    [Range(0.04f, 0.5f)] public float hitFlashSeconds = 0.11f;

    [Header("Enrage: existing EliteEnemy rules are NOT changed")]
    public bool showEnragePulse = true;
    [Range(0.2f, 2f)] public float enrageRingRadius = 0.64f;

    [Header("Death: separate, short-lived visual object")]
    public bool enableDeathBurst = true;
    [Range(3, 18)] public int deathParticles = 9;
    [Range(0.15f, 1.3f)] public float deathBurstSeconds = 0.52f;

    [Header("Diagnostics")]
    public bool logTransitions = false;

    public bool Ready { get { return ready; } }
    public string VisualAffix { get { return ready ? affix.ActiveAffixName : "NOT_READY"; } }
    public bool EnrageVisualActive { get { return enragedVisual; } }
    public int LocalHitFlashes { get { return localHits; } }

    public static int SpawnVisuals { get; private set; }
    public static int HitFlashes { get; private set; }
    public static int EnrageWarnings { get; private set; }
    public static int DeathBursts { get; private set; }

    private const int TraceLimit = 70;
    private static readonly List<string> Trace = new List<string>();
    private static Material sharedRingMaterial;

    private Health health;
    private EliteEnemy oldElite;
    private MS7E1EliteAffix affix;
    private LineRenderer affixRing;
    private LineRenderer enrageRing;
    private SpriteRenderer[] sprites;
    private Color[] colorsBeforeFlash;
    private bool subscribed;
    private bool ready;
    private bool dying;
    private bool enragedVisual;
    private bool flashing;
    private int localHits;
    private float flashUntil;
    private Color affixColor = Color.white;
    private float previousHealth;
    private float ringSeed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        SpawnVisuals = 0;
        HitFlashes = 0;
        EnrageWarnings = 0;
        DeathBursts = 0;
        Trace.Clear();
        sharedRingMaterial = null;
    }

    public static string GetRecentTrace()
    {
        return Trace.Count == 0 ? "(none)" : string.Join("\n", Trace.ToArray());
    }

    private static void Record(string item)
    {
        if (Trace.Count >= TraceLimit) Trace.RemoveAt(0);
        Trace.Add(Time.time.ToString("F1") + "s | " + item);
    }

    private void Awake()
    {
        health = GetComponent<Health>();
        oldElite = GetComponent<EliteEnemy>();
        affix = GetComponent<MS7E1EliteAffix>();
        ringSeed = UnityEngine.Random.Range(0f, 6.28f);
        previousHealth = health != null ? health.CurrentHealth : 0f;
        Subscribe();
    }

    private void OnEnable()
    {
        Subscribe();
        if (affixRing != null) affixRing.gameObject.SetActive(true);
        if (enrageRing != null) enrageRing.gameObject.SetActive(true);
    }

    private void Start()
    {
        Subscribe();
    }

    private void Subscribe()
    {
        if (subscribed || health == null) return;
        health.OnHealthChanged += OnHealthChanged;
        health.OnDied += OnDeath;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || health == null) return;
        health.OnHealthChanged -= OnHealthChanged;
        health.OnDied -= OnDeath;
        subscribed = false;
    }

    private void Update()
    {
        if (!enableFeedback || dying || health == null || health.CurrentHealth <= 0f)
        {
            if (flashing) RestoreSprites();
            if (affixRing != null) affixRing.enabled = false;
            if (enrageRing != null) enrageRing.enabled = false;
            return;
        }

        // MS7E1EliteAffix picks its affix in Start(). Never guess an affix earlier.
        if (!ready && affix != null && affix.Initialized)
            CreateVisualsOnce();

        if (!ready) return;

        if (flashing && Time.time >= flashUntil)
            RestoreSprites();

        if (Time.timeScale <= 0f) return;

        if (!enragedVisual && oldElite != null && health.maxHealth > 0f &&
            health.CurrentHealth <= health.maxHealth * oldElite.enrageHealthThreshold)
        {
            enragedVisual = true;
            EnrageWarnings++;
            Record(name + " | ENRAGE_VISUAL | " + VisualAffix);
            if (logTransitions) Debug.Log("[7E.3] Enrage feedback: " + name, this);
        }

        float pulse = 1f + Mathf.Sin(Time.time * 3.5f + ringSeed) * ringIdlePulse;
        if (affixRing != null)
        {
            affixRing.enabled = showAffixRing;
            affixRing.transform.localScale = Vector3.one * pulse;
            Color current = flashing ? Color.white : affixColor;
            current.a = 0.92f;
            affixRing.startColor = current;
            affixRing.endColor = current;
        }

        if (enrageRing != null)
        {
            enrageRing.enabled = showEnragePulse && enragedVisual;
            if (enrageRing.enabled)
            {
                float wave = 1f + 0.16f * Mathf.Sin(Time.time * 6.5f);
                enrageRing.transform.localScale = Vector3.one * wave;
                Color red = new Color(1f, 0.23f, 0.24f, 0.65f + 0.25f * Mathf.Abs(Mathf.Sin(Time.time * 5f)));
                enrageRing.startColor = red;
                enrageRing.endColor = red;
            }
        }
    }

    private void CreateVisualsOnce()
    {
        ready = true;
        SpawnVisuals++;
        previousHealth = health.CurrentHealth;
        switch (affix.ActiveAffix)
        {
            case MS7E1EliteAffix.AffixKind.Bulwark:
                affixColor = new Color(1f, 0.76f, 0.29f, 1f); break;
            case MS7E1EliteAffix.AffixKind.Swift:
                affixColor = new Color(0.74f, 0.48f, 1f, 1f); break;
            case MS7E1EliteAffix.AffixKind.Regenerator:
                affixColor = new Color(0.36f, 0.94f, 0.82f, 1f); break;
            default: affixColor = Color.white; break;
        }
        // Child sprites belong to the original prefab; do not change their base tint.
        sprites = GetComponentsInChildren<SpriteRenderer>(true);
        colorsBeforeFlash = new Color[sprites.Length];
        affixRing = BuildRing("MS7E3_AffixRing", ringRadius, ringYOffset, ringThickness, 43);
        enrageRing = BuildRing("MS7E3_EnragePulse", enrageRingRadius, ringYOffset, ringThickness * 1.22f, 44);
        if (enrageRing != null) enrageRing.enabled = false;
        Record(name + " | READY | " + VisualAffix);
        if (logTransitions) Debug.Log("[7E.3] Visual ready: " + VisualAffix, this);
    }

    private static Material RingMaterial()
    {
        if (sharedRingMaterial != null) return sharedRingMaterial;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) return null;
        sharedRingMaterial = new Material(shader);
        sharedRingMaterial.name = "MS7E3_Runtime_Ring_Unlit";
        sharedRingMaterial.hideFlags = HideFlags.HideAndDontSave;
        return sharedRingMaterial;
    }

    private LineRenderer BuildRing(string childName, float radius, float y, float width, int order)
    {
        Material material = RingMaterial();
        if (material == null)
        {
            Debug.LogWarning("[7E.3] Could not find a compatible unlit sprite shader. Ring disabled; combat is unaffected.", this);
            return null;
        }
        Transform child = transform.Find(childName);
        GameObject go = child != null ? child.gameObject : new GameObject(childName);
        if (child == null) go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, y, -0.05f);
        LineRenderer line = go.GetComponent<LineRenderer>();
        if (line == null) line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 40;
        line.widthMultiplier = width;
        line.numCapVertices = 0;
        line.numCornerVertices = 1;
        line.sortingOrder = order;
        line.textureMode = LineTextureMode.Stretch;
        for (int i = 0; i < 40; i++)
        {
            float theta = i * Mathf.PI * 2f / 40f;
            line.SetPosition(i, new Vector3(Mathf.Cos(theta) * radius, Mathf.Sin(theta) * radius * 0.42f, 0f));
        }
        return line;
    }

    private void OnHealthChanged(float now, float max)
    {
        bool damaged = now < previousHealth - 0.001f;
        previousHealth = now;
        if (!enableFeedback || dying || !damaged || now <= 0f) return;

        // Health may be damaged before MS7E1's Start() completes; safe to skip visuals then.
        if (!ready || !enableHitFlash || sprites == null) return;
        if (!flashing)
        {
            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i] != null) colorsBeforeFlash[i] = sprites[i].color;
        }
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null) continue;
            Color original = colorsBeforeFlash[i];
            sprites[i].color = Color.Lerp(original, Color.white, 0.9f);
            Color highlighted = sprites[i].color;
            highlighted.a = original.a;
            sprites[i].color = highlighted;
        }
        flashing = true;
        flashUntil = Time.time + hitFlashSeconds;
        localHits++;
        HitFlashes++;
        Record(name + " | HIT_FLASH | " + VisualAffix);
    }

    private void RestoreSprites()
    {
        if (sprites != null && colorsBeforeFlash != null)
        {
            for (int i = 0; i < sprites.Length && i < colorsBeforeFlash.Length; i++)
                if (sprites[i] != null) sprites[i].color = colorsBeforeFlash[i];
        }
        flashing = false;
    }

    private void OnDeath()
    {
        if (dying) return;
        dying = true;
        if (enableFeedback && enableDeathBurst && ready)
        {
            MS7E3DeathBurst.Spawn(transform.position, affixColor, deathParticles, deathBurstSeconds);
            DeathBursts++;
            Record(name + " | DEATH_BURST | " + VisualAffix);
        }
        if (flashing) RestoreSprites();
        Unsubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        if (flashing) RestoreSprites();
        if (affixRing != null) affixRing.gameObject.SetActive(false);
        if (enrageRing != null) enrageRing.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (flashing) RestoreSprites();
    }
}
