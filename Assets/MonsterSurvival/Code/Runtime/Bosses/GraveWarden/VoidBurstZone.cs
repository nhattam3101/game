using System;
using System.Collections.Generic;
using UnityEngine;

// One complete Void Burst area: warning circle, animated ground cracks,
// explosion visual and ONE damage check at the exact explosion moment.
// Attach this to a new, EMPTY prefab named VoidBurstZone_V2.
public class VoidBurstZone : MonoBehaviour
{
    [Header("Gameplay")]
    [Min(0.1f)] public float radius = 1.25f;
    [Min(0.1f)] public float warningDuration = 1.5f;
    [Min(0.05f)] public float explosionDuration = 0.3f;
    [Min(0f)] public float damage = 18f;
    public LayerMask playerLayers = ~0;

    [Header("Warning Circle")]
    public Color warningStartColor = new Color(0.64f, 0.30f, 1f, 0.7f);
    public Color warningEndColor = new Color(1f, 0.25f, 0.58f, 1f);
    public Color warningFillColor = new Color(0.20f, 0.03f, 0.35f, 0.20f);
    [Min(0.001f)] public float ringWidth = 0.04f;
    [Range(24, 120)] public int circleSegments = 64;

    [Header("Animated Ground Fracture")]
    [Range(4, 16)] public int crackRays = 8;
    [Range(3, 9)] public int pointsPerCrack = 6;
    [Range(0f, 0.9f)] public float crackStartsAt = 0.3f;
    public Color crackColor = new Color(0.92f, 0.38f, 1f, 0.95f);
    [Min(0.001f)] public float crackWidth = 0.032f;
    public bool createBranches = true;

    [Header("Explosion")]
    public Color explosionColor = new Color(0.85f, 0.30f, 1f, 0.8f);

    [Header("V2.1 - Explosion Polish")]
    public bool showExplosionWave = true;
    [Min(0.005f)] public float explosionWaveWidth = 0.08f;
    public Color explosionWaveColor = new Color(1f, 0.56f, 1f, 1f);
    public bool showDebris = true;
    [Range(4, 24)] public int debrisCount = 10;
    public Color debrisColor = new Color(0.68f, 0.32f, 0.84f, 0.95f);
    [Min(0f)] public float aftermathDuration = 0.4f;

    [Header("V2.1 - Optional Animation Prefabs")]
    [Tooltip("Leave empty to use generated animated cracks. Can attach your future Sprite Animator prefab.")]
    public GameObject customCrackAnimationPrefab;
    [Tooltip("Optional Sprite Animator prefab for explosion, played when damage occurs.")]
    public GameObject customExplosionAnimationPrefab;
    public bool useGeneratedCracks = true;

    [Header("V2.1 - Optional Sounds")]
    public AudioClip warningSound;
    public AudioClip crackSound;
    public AudioClip explosionSound;
    [Range(0f, 1f)] public float warningVolume = 0.45f;
    [Range(0f, 1f)] public float crackVolume = 0.65f;
    [Range(0f, 1f)] public float explosionVolume = 0.8f;
    [Min(0f)] public float soundSpacing = 0.16f;

    [Header("V2.1 - Optional Camera Shake")]
    public bool useCameraShake = false;
    [Range(0f, 0.5f)] public float cameraShakeAmount = 0.07f;
    [Range(0.05f, 0.8f)] public float cameraShakeDuration = 0.18f;
    [Min(0.1f)] public float cameraShakeMaxDistance = 7f;

    [Header("Material (Optional)")]
    [Tooltip("Material danh cho LineRenderer. De trong se dung Sprites/Default.")]
    public Material lineMaterial;

    private Health attackerHealth;
    private bool hasOwner;
    private bool exploded;
    private bool crackStarted;
    private LineRenderer explosionRing;
    private readonly List<SpriteRenderer> debrisRenderers = new List<SpriteRenderer>();
    private readonly List<Vector2> debrisDirections = new List<Vector2>();
    private GameObject customCrackInstance;
    private GameObject customExplosionInstance;

    private static float nextWarningSoundTime;
    private static float nextCrackSoundTime;
    private static float nextExplosionSoundTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSoundGates()
    {
        nextWarningSoundTime = 0f;
        nextCrackSoundTime = 0f;
        nextExplosionSoundTime = 0f;
    }
    private float elapsed;
    private Transform visualsRoot;
    private LineRenderer warningRing;
    private SpriteRenderer warningFill;
    private SpriteRenderer explosionFill;
    private Material autoCreatedMaterial;

    private readonly List<LineRenderer> crackLines = new List<LineRenderer>();
    private readonly List<Vector3[]> crackPaths = new List<Vector3[]>();

    private static Sprite sharedSoftCircleSprite;

    public void Configure(float newRadius, float newDamage, float newWarning, Health owner)
    {
        radius = Mathf.Max(0.1f, newRadius);
        damage = Mathf.Max(0f, newDamage);
        warningDuration = Mathf.Max(0.1f, newWarning);
        attackerHealth = owner;
        hasOwner = owner != null;
    }

    private void Start()
    {
        radius = Mathf.Max(0.1f, radius);
        warningDuration = Mathf.Max(0.1f, warningDuration);
        explosionDuration = Mathf.Max(0.05f, explosionDuration);
        CreateVisuals();
        UpdateWarning(0f);
        PlaySfx(warningSound, warningVolume, ref nextWarningSoundTime);
    }

    private void Update()
    {
        // A cast is cancelled if the Boss dies, including during Victory/Pause.
        if (hasOwner && (attackerHealth == null || attackerHealth.CurrentHealth <= 0f))
        {
            Destroy(gameObject);
            return;
        }

        // Scaled deltaTime: both the visual and damage PAUSE with Time.timeScale = 0.
        if (Time.timeScale <= 0f)
            return;

        elapsed += Time.deltaTime;

        if (!exploded)
        {
            float progress = Mathf.Clamp01(elapsed / warningDuration);
            UpdateWarning(progress);

            if (elapsed >= warningDuration)
                Explode();
        }
        else
        {
            float explosionTime = elapsed - warningDuration;
            if (explosionTime <= explosionDuration)
            {
                UpdateExplosion(Mathf.Clamp01(explosionTime / explosionDuration));
            }
            else
            {
                float remain = explosionTime - explosionDuration;
                UpdateAftermath(Mathf.Clamp01(remain / Mathf.Max(0.01f, aftermathDuration)));
                if (remain >= aftermathDuration)
                    Destroy(gameObject);
            }
        }
    }

    private void CreateVisuals()
    {
        GameObject root = new GameObject("GeneratedVoidBurstVisuals");
        root.transform.SetParent(transform, false);
        visualsRoot = root.transform;
        visualsRoot.localPosition = Vector3.zero;
        visualsRoot.localRotation = Quaternion.identity;
        visualsRoot.localScale = new Vector3(radius, radius, 1f);

        warningFill = CreateCircleSprite("WarningFill", warningFillColor, -6);
        explosionFill = CreateCircleSprite("ExplosionFlash", explosionColor, 14);
        explosionFill.enabled = false;

        warningRing = CreateLine("WarningRing", warningStartColor, ringWidth, 7, true);
        Vector3[] circle = new Vector3[Mathf.Max(24, circleSegments)];
        for (int i = 0; i < circle.Length; i++)
        {
            float angle = 2f * Mathf.PI * i / circle.Length;
            circle[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        }
        warningRing.positionCount = circle.Length;
        warningRing.SetPositions(circle);

        if (showExplosionWave)
        {
            explosionRing = CreateLine("ExplosionWave", explosionWaveColor,
                explosionWaveWidth, 13, true);
            explosionRing.positionCount = circle.Length;
            explosionRing.SetPositions(circle);
            explosionRing.enabled = false;
        }

        if (useGeneratedCracks)
            CreateCrackPaths();
        if (showDebris)
            CreateDebris();

        // Prefabs are disabled until their own animation starts.
        if (customCrackAnimationPrefab != null)
        {
            customCrackInstance = Instantiate(customCrackAnimationPrefab, visualsRoot);
            customCrackInstance.transform.localPosition = Vector3.zero;
            customCrackInstance.transform.localRotation = Quaternion.identity;
            customCrackInstance.SetActive(false);
        }
        if (customExplosionAnimationPrefab != null)
        {
            customExplosionInstance = Instantiate(customExplosionAnimationPrefab, visualsRoot);
            customExplosionInstance.transform.localPosition = Vector3.zero;
            customExplosionInstance.transform.localRotation = Quaternion.identity;
            customExplosionInstance.SetActive(false);
        }
    }


    private void CreateDebris()
    {
        int count = Mathf.Max(4, debrisCount);
        for (int i = 0; i < count; i++)
        {
            GameObject obj = new GameObject("GroundShard_" + i);
            obj.transform.SetParent(visualsRoot, false);
            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = GetDebrisSprite();
            sr.color = debrisColor;
            sr.sortingOrder = 15;
            sr.enabled = false;
            float angle = 2f * Mathf.PI * (i + 0.27f) / count;
            debrisDirections.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
            debrisRenderers.Add(sr);
        }
    }

    private static Sprite sharedDebrisSprite;
    private static Sprite GetDebrisSprite()
    {
        if (sharedDebrisSprite != null)
            return sharedDebrisSprite;
        Texture2D tex = Texture2D.whiteTexture;
        sharedDebrisSprite = Sprite.Create(tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), tex.width);
        sharedDebrisSprite.name = "VoidBurstDebris_Runtime";
        return sharedDebrisSprite;
    }

    private static void RestartVisualAnimation(GameObject instance)
    {
        if (instance == null) return;
        instance.SetActive(true);
        Animator animator = instance.GetComponentInChildren<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.Play(0, 0, 0f);
    }

    private void PlaySfx(AudioClip clip, float volume, ref float nextAllowedTime)
    {
        if (clip == null || volume <= 0f || Time.time < nextAllowedTime)
            return;
        nextAllowedTime = Time.time + soundSpacing;
        // Reuse the game's existing music/SFX volume and settings.
        if (AudioManager.Instance != null && AudioManager.Instance.sfxSource != null)
            AudioManager.Instance.sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private void PlayImpactCameraShake()
    {
        if (!useCameraShake) return;
        Camera camera = Camera.main;
        if (camera == null) return;
        VoidBurstCameraShake shaker = camera.GetComponent<VoidBurstCameraShake>();
        if (shaker != null)
            shaker.RequestShake(transform.position, cameraShakeAmount,
                cameraShakeDuration, cameraShakeMaxDistance);
    }

    private void CreateCrackPaths()
    {
        // A different but repeatable jagged layout for each zone position.
        int seed = 9173 + Mathf.RoundToInt(transform.position.x * 149f)
                   + Mathf.RoundToInt(transform.position.y * 911f);
        System.Random random = new System.Random(seed);
        int count = Mathf.Max(4, crackRays);
        int pointCount = Mathf.Max(3, pointsPerCrack);

        for (int i = 0; i < count; i++)
        {
            float a = 2f * Mathf.PI * i / count + ((float)random.NextDouble() - 0.5f) * 0.32f;
            Vector3[] points = new Vector3[pointCount];

            for (int j = 0; j < pointCount; j++)
            {
                float t = j / (float)(pointCount - 1);
                float distance = Mathf.Lerp(0.10f, 0.93f, t);
                float jaggedAngle = a + (j == 0 ? 0f : ((float)random.NextDouble() - 0.5f) * 0.21f);
                points[j] = new Vector3(
                    Mathf.Cos(jaggedAngle) * distance,
                    Mathf.Sin(jaggedAngle) * distance,
                    0f);
            }

            RegisterCrack("Crack_" + i, points, crackWidth);

            if (createBranches && i % 2 == 0)
            {
                int branchIndex = Mathf.Max(1, pointCount / 2);
                Vector3 start = points[branchIndex];
                float branchAngle = a + (i % 4 == 0 ? 0.65f : -0.65f);
                Vector3[] branch = new Vector3[3];
                branch[0] = start;
                branch[1] = start + new Vector3(Mathf.Cos(branchAngle), Mathf.Sin(branchAngle), 0f) * 0.14f;
                branch[2] = start + new Vector3(Mathf.Cos(branchAngle + 0.1f), Mathf.Sin(branchAngle + 0.1f), 0f) * 0.29f;
                RegisterCrack("Branch_" + i, branch, crackWidth * 0.7f);
            }
        }
    }

    private void RegisterCrack(string name, Vector3[] points, float width)
    {
        LineRenderer line = CreateLine(name, crackColor, width, 9, false);
        line.enabled = false;
        crackLines.Add(line);
        crackPaths.Add(points);
    }

    private void UpdateWarning(float t)
    {
        if (warningRing != null)
        {
            // Warning edge shrinks slightly and becomes brighter toward explosion.
            float pulse = 1f + 0.028f * Mathf.Sin(elapsed * 11f);
            warningRing.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.90f, t) * pulse;
            Color c = Color.Lerp(warningStartColor, warningEndColor, t);
            warningRing.startColor = c;
            warningRing.endColor = c;
        }

        if (warningFill != null)
        {
            Color c = warningFillColor;
            c.a = Mathf.Lerp(0.12f, 0.32f, t);
            warningFill.color = c;
        }

        float crackProgress = Mathf.Clamp01((t - crackStartsAt) / Mathf.Max(0.01f, 1f - crackStartsAt));
        if (!crackStarted && crackProgress > 0.001f)
        {
            crackStarted = true;
            RestartVisualAnimation(customCrackInstance);
            PlaySfx(crackSound, crackVolume, ref nextCrackSoundTime);
        }
        for (int i = 0; i < crackLines.Count; i++)
        {
            LineRenderer lr = crackLines[i];
            if (crackProgress <= 0f)
            {
                lr.enabled = false;
                continue;
            }
            lr.enabled = true;
            SetGrowingLine(lr, crackPaths[i], crackProgress);
        }
    }

    private static void SetGrowingLine(LineRenderer lr, Vector3[] points, float progress)
    {
        float steps = Mathf.Clamp01(progress) * (points.Length - 1);
        int segment = Mathf.Min(points.Length - 2, Mathf.FloorToInt(steps));
        int count = Mathf.Min(points.Length, segment + 2);
        lr.positionCount = count;

        for (int i = 0; i < count - 1; i++)
            lr.SetPosition(i, points[i]);

        float fraction = Mathf.Clamp01(steps - segment);
        lr.SetPosition(count - 1, Vector3.Lerp(points[segment], points[segment + 1], fraction));
    }

    private void Explode()
    {
        if (exploded)
            return;

        exploded = true;
        DealDamageOnce();
        PlaySfx(explosionSound, explosionVolume, ref nextExplosionSoundTime);
        PlayImpactCameraShake();
        RestartVisualAnimation(customExplosionInstance);
        if (explosionRing != null)
            explosionRing.enabled = true;
        foreach (SpriteRenderer shard in debrisRenderers)
            shard.enabled = true;

        if (warningRing != null)
            warningRing.enabled = false;
        if (warningFill != null)
            warningFill.enabled = false;

        // Keep the cracks visible through the flash, then fade them.
        if (explosionFill != null)
        {
            explosionFill.enabled = true;
            explosionFill.color = explosionColor;
        }
        Debug.Log("Void Burst: BOOM at " + transform.position);
    }

    private void DealDamageOnce()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius, playerLayers);
        HashSet<Health> damaged = new HashSet<Health>();

        foreach (Collider2D hit in hits)
        {
            PlayerStats stats = hit.GetComponentInParent<PlayerStats>();
            if (stats == null)
                continue;

            Health hp = stats.GetComponent<Health>();
            if (hp == null || hp.CurrentHealth <= 0f || !damaged.Add(hp))
                continue;

            hp.TakeDamage(damage, attackerHealth); // Preserve Shield and Reflect Shield.
            Debug.Log("Void Burst hit Player: " + damage + " damage (before Shield).");
        }
    }

    private void UpdateExplosion(float t)
    {
        if (explosionFill != null)
        {
            explosionFill.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.15f, t);
            Color c = explosionColor;
            c.a = Mathf.Lerp(explosionColor.a, 0f, t);
            explosionFill.color = c;
        }

        if (explosionRing != null)
        {
            explosionRing.transform.localScale = Vector3.one * Mathf.Lerp(0.16f, 1.3f, t);
            Color ringColor = explosionWaveColor;
            ringColor.a *= (1f - t);
            explosionRing.startColor = ringColor;
            explosionRing.endColor = ringColor;
        }

        for (int i = 0; i < debrisRenderers.Count; i++)
        {
            SpriteRenderer shard = debrisRenderers[i];
            Vector2 dir = debrisDirections[i];
            shard.transform.localPosition = (Vector3)(dir * Mathf.Lerp(0.08f, 1.05f, t));
            shard.transform.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 50f * t);
            shard.transform.localScale = new Vector3(0.04f, 0.12f, 1f) * (1f - 0.75f * t);
            Color c = debrisColor;
            c.a *= (1f - t);
            shard.color = c;
        }

        Color fading = crackColor;
        fading.a = Mathf.Lerp(crackColor.a, crackColor.a * 0.4f, t);
        foreach (LineRenderer crack in crackLines)
        {
            crack.startColor = fading;
            crack.endColor = fading;
        }
    }

    private void UpdateAftermath(float t)
    {
        if (explosionFill != null)
            explosionFill.enabled = false;
        if (explosionRing != null)
            explosionRing.enabled = false;
        foreach (SpriteRenderer shard in debrisRenderers)
            shard.enabled = false;

        Color fading = crackColor;
        fading.a = crackColor.a * 0.4f * (1f - t);
        foreach (LineRenderer crack in crackLines)
        {
            crack.startColor = fading;
            crack.endColor = fading;
        }
    }

    private SpriteRenderer CreateCircleSprite(string name, Color color, int order)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(visualsRoot, false);
        SpriteRenderer spriteRenderer = obj.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetCircleSprite();
        spriteRenderer.color = color;
        spriteRenderer.sortingOrder = order;
        return spriteRenderer;
    }

    private static Sprite GetCircleSprite()
    {
        if (sharedSoftCircleSprite != null)
            return sharedSoftCircleSprite;

        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "VoidBurstSoftCircle_Runtime";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f - size * 0.5f) / (size * 0.5f);
                float ny = (y + 0.5f - size * 0.5f) / (size * 0.5f);
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                float alpha = 1f - Mathf.SmoothStep(0.86f, 1f, d);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }
        }
        tex.SetPixels(pixels);
        tex.Apply(false, true);
        sharedSoftCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), size * 0.5f);
        sharedSoftCircleSprite.name = "VoidBurstSoftCircleSprite_Runtime";
        return sharedSoftCircleSprite;
    }

    private LineRenderer CreateLine(string name, Color color, float width, int order, bool loop)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(visualsRoot, false);
        LineRenderer lr = obj.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = loop;
        lr.alignment = LineAlignment.View;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.startColor = color;
        lr.endColor = color;
        lr.numCornerVertices = 3;
        lr.numCapVertices = 3;
        lr.sortingOrder = order;

        if (lineMaterial != null)
        {
            lr.sharedMaterial = lineMaterial;
        }
        else
        {
            if (autoCreatedMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null)
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    shader = Shader.Find("Unlit/Color");
                if (shader != null)
                    autoCreatedMaterial = new Material(shader);
                else
                    Debug.LogWarning("VoidBurstZone: Khong tim thay shader. Hay gan Line Material.");
            }
            if (autoCreatedMaterial != null)
                lr.sharedMaterial = autoCreatedMaterial;
        }
        return lr;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.8f, 0.2f, 1f, 0.75f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }

    private void OnDestroy()
    {
        if (autoCreatedMaterial != null)
            Destroy(autoCreatedMaterial);
        // Created from Unity built-in whiteTexture; release only our Sprite wrapper.
        // The underlying built-in texture must never be destroyed.
        // Static visual sprites remain shared for the rest of the game session.
    }
}
