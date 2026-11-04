using UnityEngine;

// Attach to the SCENE Player. Sounds/VFX are cosmetic only; no gameplay writes.
// Sound clips are original generated 7B.3 WAVs and are optional.
[DisallowMultipleComponent]
public sealed class MS7B3VisualHub : MonoBehaviour
{
    [Header("Enable / Disable Independently")]
    public bool enableVfx = true;
    public bool enableAudio = true;

    [Header("Original 7B.3 Audio Clips")]
    public AudioClip fireHit;
    public AudioClip iceHit;
    public AudioClip lightningHit;
    [Range(0f, 1f)] public float volume = .30f;
    [Min(.02f)] public float minSecondsBetweenSameSound = .12f;

    [Header("Compact World-Space VFX")]
    [Range(.03f, .35f)] public float particleSize = .13f;
    [Range(.08f, .6f)] public float ringLifetime = .22f;
    [Range(.06f, .4f)] public float electricCoreLifetime = .13f;

    private static MS7B3VisualHub current;
    private AudioSource sfx;
    private Material effectMaterial;
    private Texture2D glowTexture;
    private float nextFire, nextIce, nextElectric;

    public static MS7B3VisualHub Find()
    {
        if (current == null) current = FindFirstObjectByType<MS7B3VisualHub>();
        return current;
    }

    private void Awake()
    {
        if (current != null && current != this)
        {
            Debug.LogWarning("[7B.3] Duplicate VisualHub: only first hub emits effects.", this);
            enabled = false;
            return;
        }
        current = this;
        // Never change an existing Player AudioSource or the AudioManager setup.
        GameObject local = new GameObject("7B3_LocalSFXFallback");
        local.transform.SetParent(transform, false);
        sfx = local.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;
        sfx.loop = false;
        CreateSharedMaterial();
    }

    private void CreateSharedMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return;
        glowTexture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        glowTexture.name = "7B3_GeneratedGlow";
        glowTexture.filterMode = FilterMode.Bilinear;
        glowTexture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[16 * 16];
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
        {
            float dx = (x - 7.5f) / 7.5f;
            float dy = (y - 7.5f) / 7.5f;
            float alpha = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)), 1.4f);
            pixels[y * 16 + x] = new Color(1f, 1f, 1f, alpha);
        }
        glowTexture.SetPixels(pixels);
        glowTexture.Apply();
        effectMaterial = new Material(shader) { name = "7B3_RuntimeVFXMaterial" };
        effectMaterial.mainTexture = glowTexture;
    }

    public void OnFire(Health target, float seconds)
    {
        if (!isActiveAndEnabled || target == null) return;
        if (enableVfx)
        {
            MS7B3StatusVisual state = EnsureStatus(target);
            state.ShowFire(seconds, effectMaterial, particleSize);
            SpawnRing(target.transform.position, new Color(1f, .38f, .1f, .85f));
        }
        PlayOne(fireHit, ref nextFire);
    }

    public void OnIce(Health target, float seconds)
    {
        if (!isActiveAndEnabled || target == null) return;
        if (enableVfx)
        {
            MS7B3StatusVisual state = EnsureStatus(target);
            state.ShowIce(seconds, effectMaterial, particleSize);
            SpawnRing(target.transform.position, new Color(.48f, .89f, 1f, .8f));
        }
        PlayOne(iceHit, ref nextIce);
    }

    public void OnLightningPrimary(Vector3 position)
    {
        if (!isActiveAndEnabled) return;
        if (enableVfx) SpawnRing(position, new Color(1f, .9f, .42f, .9f));
        PlayOne(lightningHit, ref nextElectric);
    }

    public void OnLightningJump(Vector3 from, Vector3 to, float lifetime)
    {
        if (!isActiveAndEnabled || !enableVfx) return;
        if ((to - from).sqrMagnitude <= .0001f) return;
        GameObject arc = new GameObject("7B3_LightningCore");
        MS7B3ArcFlash flash = arc.AddComponent<MS7B3ArcFlash>();
        flash.Initialize(from, to, effectMaterial, Mathf.Min(.4f, Mathf.Max(.06f, lifetime)));
    }

    private void SpawnRing(Vector3 center, Color color)
    {
        GameObject ring = new GameObject("7B3_ImpactRing");
        MS7B3PulseRing pulse = ring.AddComponent<MS7B3PulseRing>();
        pulse.Initialize(center, color, effectMaterial, ringLifetime);
    }

    private MS7B3StatusVisual EnsureStatus(Health target)
    {
        MS7B3StatusVisual status = target.GetComponent<MS7B3StatusVisual>();
        if (status == null) status = target.gameObject.AddComponent<MS7B3StatusVisual>();
        return status;
    }

    private void PlayOne(AudioClip clip, ref float nextTime)
    {
        if (!enableAudio || clip == null || Time.timeScale <= 0f) return;
        if (Time.time < nextTime) return;
        AudioSource output = AudioManager.Instance != null && AudioManager.Instance.sfxSource != null
            ? AudioManager.Instance.sfxSource : sfx;
        if (output == null) return;
        nextTime = Time.time + minSecondsBetweenSameSound;
        // Reuse the existing AudioManager SFX mixer when present, honoring the Settings slider.
        output.PlayOneShot(clip, volume);
    }

    private void OnDestroy()
    {
        if (current == this) current = null;
        if (effectMaterial != null) Destroy(effectMaterial);
        if (glowTexture != null) Destroy(glowTexture);
    }
}
