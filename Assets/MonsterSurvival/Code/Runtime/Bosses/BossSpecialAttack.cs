using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class BossSpecialAttack : MonoBehaviour
{
    [Header("Shockwave Settings")]
    public float cooldown = 8f;
    public float warningDuration = 1.5f;
    public float shockwaveRadius = 2.5f;
    public float shockwaveDamage = 15f;

    [Header("Phase 2 - Shockwave Upgrades (Optional)")]
    [Tooltip("Script BossPhase2Controller tren ShadowTitan. De trong neu khong co, se tu tim.")]
    public BossPhase2Controller phase2Controller;

    [Range(0.2f, 1f)]
    [Tooltip("0.6: cooldown 8s se con 4.8s o Phase 2.")]
    public float phase2CooldownMultiplier = 0.6f;

    [Range(1f, 3f)]
    [Tooltip("1.2: Shockwave 15 damage se thanh 18 damage o Phase 2.")]
    public float phase2DamageMultiplier = 1.2f;

    private bool IsPhase2Active =>
        phase2Controller != null && phase2Controller.IsPhase2;

    [Header("Shockwave Audio")]
    public AudioClip shockwaveWarningSound;
    public AudioClip shockwaveBlastSound;

    [Range(0f, 1f)]
    public float warningVolume = 0.7f;

    [Range(0f, 1f)]
    public float blastVolume = 0.9f;


    [Header("Boss Flash")]
    public Color warningColor = Color.yellow;
    private Color originalColor = Color.white;

    [Header("Shrinking Ring Warning")]
    public bool useShrinkingRing = true;
    public Color shrinkingRingColor = new Color(1f, 0.2f, 0.2f, 0.95f);
    public float shrinkingRingWidth = 0.12f;
    public float shrinkingRingMinRadius = 0.35f;

    [Header("Expanding Shockwave Ring")]
    public bool useExpandingWave = true;
    public Color expandingWaveColor = new Color(1f, 0.6f, 0.1f, 1f);
    public float expandingWaveWidth = 0.18f;
    public float expandingWaveDuration = 0.35f;

    [Header("Line Renderer Settings")]
    [Range(24, 120)]
    public int circleSegments = 60;
    public int sortingOrderOffset = 5;

    private Health bossHealth;
    private Health playerHealth;
    private Transform player;
    private SpriteRenderer bossRenderer;

    private LineRenderer shrinkingRingRenderer;
    private LineRenderer expandingWaveRenderer;

    private Coroutine attackRoutine;

    private void Start()
    {
        bossHealth = GetComponent<Health>();

        // Phase 2 chi la tuy chon, khong anh huong Shockwave Phase 1.
        if (phase2Controller == null)
            phase2Controller = GetComponent<BossPhase2Controller>();
        bossRenderer = GetComponent<SpriteRenderer>();

        if (bossRenderer != null)
        {
            originalColor = bossRenderer.color;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
            playerHealth = playerObject.GetComponent<Health>();
        }

        CreateRingEffects();

        attackRoutine = StartCoroutine(AttackRoutine());
    }

    private void CreateRingEffects()
    {
        if (useShrinkingRing)
        {
            GameObject shrinkingObj = new GameObject("BossShrinkingRing");
            shrinkingObj.transform.SetParent(transform, false);
            shrinkingObj.transform.localPosition = Vector3.zero;

            shrinkingRingRenderer = shrinkingObj.AddComponent<LineRenderer>();
            SetupLineRenderer(
                shrinkingRingRenderer,
                shrinkingRingColor,
                shrinkingRingWidth,
                "ShrinkingRingMaterial"
            );

            shrinkingRingRenderer.enabled = false;
            DrawUnitCircle(shrinkingRingRenderer, circleSegments);
        }

        if (useExpandingWave)
        {
            GameObject expandingObj = new GameObject("BossExpandingWave");
            expandingObj.transform.SetParent(transform, false);
            expandingObj.transform.localPosition = Vector3.zero;

            expandingWaveRenderer = expandingObj.AddComponent<LineRenderer>();
            SetupLineRenderer(
                expandingWaveRenderer,
                expandingWaveColor,
                expandingWaveWidth,
                "ExpandingWaveMaterial"
            );

            expandingWaveRenderer.enabled = false;
            DrawUnitCircle(expandingWaveRenderer, circleSegments);
        }
    }

    private void SetupLineRenderer(LineRenderer lr, Color color, float width, string materialName)
    {
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Stretch;
        lr.numCornerVertices = 8;
        lr.numCapVertices = 8;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        lr.startWidth = width;
        lr.endWidth = width;

        lr.startColor = color;
        lr.endColor = color;

        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.name = materialName;
        lr.material = mat;

        if (bossRenderer != null)
        {
            lr.sortingLayerID = bossRenderer.sortingLayerID;
            lr.sortingOrder = bossRenderer.sortingOrder + sortingOrderOffset;
        }
        else
        {
            lr.sortingOrder = sortingOrderOffset;
        }
    }

    private void DrawUnitCircle(LineRenderer lr, int segments)
    {
        if (lr == null) return;

        lr.positionCount = segments;

        float angleStep = 360f / segments;

        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.Deg2Rad * (i * angleStep);
            float x = Mathf.Cos(angle);
            float y = Mathf.Sin(angle);

            lr.SetPosition(i, new Vector3(x, y, 0f));
        }
    }

    private IEnumerator AttackRoutine()
    {
        while (true)
        {
            // Dynamic cooldown: neu Boss chuyen Phase khi dang cho,
            // moc thoi gian se duoc cap nhat ma khong can restart coroutine.
            float cooldownElapsed = 0f;
            while (true)
            {
                if (bossHealth == null || bossHealth.CurrentHealth <= 0f)
                    yield break;

                float multiplier = IsPhase2Active
                    ? phase2CooldownMultiplier
                    : 1f;

                float currentCooldown = Mathf.Max(0.1f, cooldown * multiplier);
                if (cooldownElapsed >= currentCooldown)
                    break;

                cooldownElapsed += Time.deltaTime;
                yield return null;
            }

            if (bossHealth == null || bossHealth.CurrentHealth <= 0f)
                yield break;

            yield return StartCoroutine(PlayWarningPhase());

            if (bossHealth == null || bossHealth.CurrentHealth <= 0f)
                yield break;

            yield return StartCoroutine(PlayShockwavePhase());

            if (bossRenderer != null)
            {
                bossRenderer.color = originalColor;
            }
        }
    }

    private IEnumerator PlayWarningPhase()
    {
        // MOI: Phat am thanh canh bao Shockwave
        PlayShockwaveSound(
            shockwaveWarningSound,
            warningVolume
        );

        if (bossRenderer != null)
        {
            bossRenderer.color = warningColor;
        }

        if (shrinkingRingRenderer != null)
        {
            shrinkingRingRenderer.enabled = true;
        }

        float elapsed = 0f;
        float startRadius = shockwaveRadius;
        float endRadius = Mathf.Clamp(shrinkingRingMinRadius, 0.1f, shockwaveRadius);

        while (elapsed < warningDuration)
        {
            if (bossHealth == null || bossHealth.CurrentHealth <= 0f)
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / warningDuration);

            float currentRadius = Mathf.Lerp(startRadius, endRadius, t);

            if (shrinkingRingRenderer != null)
            {
                shrinkingRingRenderer.transform.localScale = new Vector3(currentRadius, currentRadius, 1f);

                Color c = shrinkingRingColor;
                c.a = Mathf.Lerp(1f, 0.35f, t) * (0.75f + 0.25f * Mathf.PingPong(Time.time * 6f, 1f));
                shrinkingRingRenderer.startColor = c;
                shrinkingRingRenderer.endColor = c;
            }

            yield return null;
        }

        if (shrinkingRingRenderer != null)
        {
            shrinkingRingRenderer.enabled = false;
        }
    }

    private IEnumerator PlayShockwavePhase()
    {
        // MOI: Phat am thanh Shockwave
        PlayShockwaveSound(
            shockwaveBlastSound,
            blastVolume
        );

        // Gây sát thương ngay thời điểm kích hoạt shockwave
        DealShockwaveDamage();

        if (expandingWaveRenderer != null)
        {
            expandingWaveRenderer.enabled = true;
        }

        float elapsed = 0f;
        float startRadius = 0.15f;
        float endRadius = shockwaveRadius;

        while (elapsed < expandingWaveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / expandingWaveDuration);

            float currentRadius = Mathf.Lerp(startRadius, endRadius, t);

            if (expandingWaveRenderer != null)
            {
                expandingWaveRenderer.transform.localScale = new Vector3(currentRadius, currentRadius, 1f);

                Color c = expandingWaveColor;
                c.a = Mathf.Lerp(1f, 0f, t);
                expandingWaveRenderer.startColor = c;
                expandingWaveRenderer.endColor = c;
            }

            yield return null;
        }

        if (expandingWaveRenderer != null)
        {
            expandingWaveRenderer.enabled = false;
        }
    }

    private void DealShockwaveDamage()
    {
        if (player == null || playerHealth == null)
            return;

        float distance = Vector2.Distance(transform.position, player.position);

        if (distance <= shockwaveRadius)
        {
            float effectiveDamage = shockwaveDamage *
                (IsPhase2Active ? phase2DamageMultiplier : 1f);

            playerHealth.TakeDamage(effectiveDamage, bossHealth);
            Debug.Log("Boss dùng Shockwave trúng Player!");
        }
        else
        {
            Debug.Log("Boss dùng Shockwave nhưng Player đã né khỏi vùng ảnh hưởng.");
        }
    }

    private void PlayShockwaveSound(
        AudioClip clip,
        float volume)
    {
        if (clip == null)
            return;

        if (AudioManager.Instance == null)
            return;

        AudioSource source =
            AudioManager.Instance.sfxSource;

        if (source == null)
            return;

        source.PlayOneShot(
            clip,
            Mathf.Clamp01(volume)
        );
    }


    private void OnDisable()
    {
        if (shrinkingRingRenderer != null)
        {
            shrinkingRingRenderer.enabled = false;
        }

        if (expandingWaveRenderer != null)
        {
            expandingWaveRenderer.enabled = false;
        }

        if (bossRenderer != null)
        {
            bossRenderer.color = originalColor;
        }

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, shockwaveRadius);
    }
}