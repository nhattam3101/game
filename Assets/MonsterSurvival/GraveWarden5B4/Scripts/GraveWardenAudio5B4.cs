using UnityEngine;
using UnityEngine.Audio;

// MonsterSurvival v1.2 / 5B.4 - stand-alone audio integration.
// Does NOT change GraveWardenBossAI, AnimatorBridge, WaveManager, Health or skill scripts.
// Detects 5B.2 Animator state transitions and Boss Health/Phase events.
[DisallowMultipleComponent]
public class GraveWardenAudio5B4 : MonoBehaviour
{
    [Header("Optional references (auto-find on Boss prefab)")]
    public Animator visualAnimator;
    public GraveWardenBossAI boss;
    public Health bossHealth;

    [Header("Mixer groups - optional; connect if you want in-game volume sliders to work")]
    public AudioMixerGroup sfxOutput;
    public AudioMixerGroup musicOutput;

    [Header("Enable and mix")]
    public bool playBossMusic = true;
    public bool followAnimatorStates = true;
    [Range(0f, 1f)] public float sfxVolume = 0.42f;
    [Range(0f, 1f)] public float musicVolume = 0.18f;
    [Min(0.2f)] public float musicCrossfadeSeconds = 1.6f;
    public bool showTestPanelInPlayMode = false;

    [Header("Phase battle music")]
    public AudioClip phase1BattleMusic;
    public AudioClip phase2BattleMusic;

    [Header("Intro / Tomb Wall")]
    public AudioClip spawnIntro;
    public AudioClip tombWallPrepare;
    public AudioClip tombWallEmerge;

    [Header("Wraith / Reanimate")]
    public AudioClip wraithPrepare;
    public AudioClip wraithSummon;
    public AudioClip reanimateCast;
    public AudioClip reanimateRise;

    [Header("Orbit / Soul Harvest / Empower")]
    public AudioClip spiritOrbitCast;
    public AudioClip soulHarvestAbsorb;
    public AudioClip empoweredCast;

    [Header("Phase 2 / combat")]
    public AudioClip phase2Transform;
    public AudioClip hit;
    public AudioClip death;

    private AudioSource effectsSource;
    private AudioSource phase1Source;
    private AudioSource phase2Source;
    private bool dead;
    private bool musicPaused;
    private bool phase2MusicEnabled;
    private float musicBlend;
    private float lastHealth;
    private float lastHitTime = -100f;
    private int lastAnimationHash;
    private float lastCueTime = -100f;
    private int lastCueHash;

    private static readonly int SSpawn = Animator.StringToHash("Spawn_Intro");
    private static readonly int STombPrepare = Animator.StringToHash("TombWall_Prepare");
    private static readonly int STombSummon = Animator.StringToHash("TombWall_Summon");
    private static readonly int SWraithPrepare = Animator.StringToHash("Wraith_Prepare");
    private static readonly int SWraithSummon = Animator.StringToHash("Wraith_Summon");
    private static readonly int SReanimateCast = Animator.StringToHash("Reanimate_Cast");
    private static readonly int SReanimateRise = Animator.StringToHash("Reanimate_Rise");
    private static readonly int SOrbitCast = Animator.StringToHash("SpiritOrbit_Cast");
    private static readonly int SHarvest = Animator.StringToHash("SoulHarvest_Absorb");
    private static readonly int SEmpowered = Animator.StringToHash("Empowered_Cast");
    private static readonly int SPhase2 = Animator.StringToHash("Phase2_Transform");

    private void Awake()
    {
        if (boss == null) boss = GetComponent<GraveWardenBossAI>();
        if (bossHealth == null) bossHealth = GetComponent<Health>();
        if (visualAnimator == null) visualAnimator = GetComponentInChildren<Animator>(true);

        // Sources are created only on the live Boss instance, not the original prefab asset.
        effectsSource = gameObject.AddComponent<AudioSource>();
        ConfigureSource(effectsSource, false, sfxOutput);
        effectsSource.volume = 1f;
        phase1Source = gameObject.AddComponent<AudioSource>();
        ConfigureSource(phase1Source, true, musicOutput);
        phase2Source = gameObject.AddComponent<AudioSource>();
        ConfigureSource(phase2Source, true, musicOutput);
        if (bossHealth != null) lastHealth = bossHealth.CurrentHealth;
    }

    private static void ConfigureSource(AudioSource source, bool loop, AudioMixerGroup mixer)
    {
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f; // reliable for 2D top-down scenes
        source.outputAudioMixerGroup = mixer;
        source.volume = 0f;
    }

    private void OnEnable()
    {
        if (bossHealth == null) bossHealth = GetComponent<Health>();
        if (bossHealth != null)
        {
            lastHealth = bossHealth.CurrentHealth;
            bossHealth.OnHealthChanged += HandleHealthChanged;
            bossHealth.OnDied += HandleBossDeath;
        }
    }

    private void Start()
    {
        if (visualAnimator != null && visualAnimator.runtimeAnimatorController != null)
            lastAnimationHash = visualAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash;

        PlayEffect(spawnIntro);
        if (playBossMusic && phase1BattleMusic != null)
        {
            phase1Source.clip = phase1BattleMusic;
            phase1Source.Play();
        }
    }

    private void Update()
    {
        if (dead) return;
        if (followAnimatorStates && visualAnimator != null && visualAnimator.runtimeAnimatorController != null)
            DetectAnimationCue();

        if (boss != null && boss.IsPhaseTwo && !phase2MusicEnabled)
            BeginPhaseTwoMusic();

        // TimeScale is used for Pause in the project; do not keep Boss music playing behind Pause menu.
        bool shouldPause = Time.timeScale <= 0f;
        if (musicPaused != shouldPause)
        {
            musicPaused = shouldPause;
            if (shouldPause)
            {
                phase1Source.Pause();
                phase2Source.Pause();
            }
            else
            {
                phase1Source.UnPause();
                phase2Source.UnPause();
            }
        }

        if (musicPaused) return;
        if (phase2MusicEnabled && musicBlend < 1f)
            musicBlend = Mathf.MoveTowards(musicBlend, 1f,
                Time.unscaledDeltaTime / Mathf.Max(0.2f, musicCrossfadeSeconds));

        if (phase1Source != null) phase1Source.volume = (1f - musicBlend) * musicVolume;
        if (phase2Source != null) phase2Source.volume = musicBlend * musicVolume;
        if (phase2MusicEnabled && musicBlend >= 1f && phase1Source.isPlaying)
            phase1Source.Stop();
    }

    private void BeginPhaseTwoMusic()
    {
        phase2MusicEnabled = true;
        if (!playBossMusic || phase2BattleMusic == null) return;
        phase2Source.clip = phase2BattleMusic;
        phase2Source.volume = 0f;
        phase2Source.Play();
    }

    private void DetectAnimationCue()
    {
        // Observe next state while transitioning, otherwise we can miss short summon clips.
        AnimatorStateInfo current = visualAnimator.GetCurrentAnimatorStateInfo(0);
        int hash = current.shortNameHash;
        if (visualAnimator.IsInTransition(0))
        {
            AnimatorStateInfo next = visualAnimator.GetNextAnimatorStateInfo(0);
            if (next.shortNameHash != 0 && next.shortNameHash != hash)
                hash = next.shortNameHash;
        }
        if (hash == lastAnimationHash) return;
        lastAnimationHash = hash;
        PlayAnimationCue(hash);
    }

    private void PlayAnimationCue(int hash)
    {
        // Skip repeated near-identical transitions; short state transitions can happen twice.
        if (hash == lastCueHash && Time.time - lastCueTime < 0.75f) return;
        AudioClip clip = null;
        if (hash == STombPrepare) clip = tombWallPrepare;
        else if (hash == STombSummon) clip = tombWallEmerge;
        else if (hash == SWraithPrepare) clip = wraithPrepare;
        else if (hash == SWraithSummon) clip = wraithSummon;
        else if (hash == SReanimateCast) clip = reanimateCast;
        else if (hash == SReanimateRise) clip = reanimateRise;
        else if (hash == SOrbitCast) clip = spiritOrbitCast;
        else if (hash == SHarvest) clip = soulHarvestAbsorb;
        else if (hash == SEmpowered) clip = empoweredCast;
        else if (hash == SPhase2) clip = phase2Transform;
        else if (hash == SSpawn) return; // Spawn Intro plays once in Start.
        if (clip != null)
        {
            lastCueHash = hash;
            lastCueTime = Time.time;
            PlayEffect(clip);
        }
    }

    private void HandleHealthChanged(float current, float maximum)
    {
        if (dead) return;
        if (current > 0f && current < lastHealth && Time.time - lastHitTime >= 0.45f)
        {
            PlayEffect(hit);
            lastHitTime = Time.time;
        }
        lastHealth = current;
    }

    private void HandleBossDeath()
    {
        if (dead) return;
        dead = true;
        if (phase1Source != null) phase1Source.Stop();
        if (phase2Source != null) phase2Source.Stop();
        // Death sound survives destruction of the Boss GameObject during Victory.
        if (death != null)
        {
            GameObject oneShot = new GameObject("GW_DeathAudio_5B4");
            AudioSource source = oneShot.AddComponent<AudioSource>();
            ConfigureSource(source, false, sfxOutput);
            source.volume = 1f;
            source.PlayOneShot(death, sfxVolume);
            Destroy(oneShot, death.length + 0.5f);
        }
    }

    private void PlayEffect(AudioClip clip)
    {
        if (clip == null || dead || effectsSource == null) return;
        effectsSource.PlayOneShot(clip, sfxVolume);
    }

    private void OnDisable()
    {
        if (bossHealth != null)
        {
            bossHealth.OnHealthChanged -= HandleHealthChanged;
            bossHealth.OnDied -= HandleBossDeath;
        }
        if (phase1Source != null) phase1Source.Stop();
        if (phase2Source != null) phase2Source.Stop();
    }

    // Optional button test panel. Disable after installation to avoid cluttering Game view.
    private void OnGUI()
    {
        if (!Application.isPlaying || !showTestPanelInPlayMode || dead) return;
        GUILayout.BeginArea(new Rect(Screen.width - 220, 70, 212, 288), GUI.skin.box);
        GUILayout.Label("GRAVE WARDEN AUDIO 5B.4");
        if (GUILayout.Button("Tomb Wall")) { PlayEffect(tombWallPrepare); PlayEffect(tombWallEmerge); }
        if (GUILayout.Button("Summon Wraith")) PlayEffect(wraithSummon);
        if (GUILayout.Button("Reanimate")) PlayEffect(reanimateRise);
        if (GUILayout.Button("Spirit Orbit")) PlayEffect(spiritOrbitCast);
        if (GUILayout.Button("Soul Harvest")) PlayEffect(soulHarvestAbsorb);
        if (GUILayout.Button("Phase 2")) PlayEffect(phase2Transform);
        if (GUILayout.Button("Music Phase 2")) BeginPhaseTwoMusic();
        GUILayout.Label("Turn off Test Panel after test.");
        GUILayout.EndArea();
    }
}
