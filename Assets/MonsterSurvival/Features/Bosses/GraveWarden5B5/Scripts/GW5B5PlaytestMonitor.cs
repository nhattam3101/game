using UnityEngine;

/// <summary>
/// 5B.5 diagnostic-only overlay. Install as its own GameObject in SampleScene,
/// never on the boss prefab. No HP, AI, spawn, animation, audio, or VFX edits.
/// Remove the test GameObject before the final build.
/// </summary>
[DisallowMultipleComponent]
public sealed class GW5B5PlaytestMonitor : MonoBehaviour
{
    [Header("Test only - Remove before final build")]
    public bool showOverlay = true;
    public bool logImportantEvents = true;
    [Range(0.5f, 5f)] public float scanIntervalSeconds = 1.5f;

    [Header("Expected gameplay caps")]
    [Min(1)] public int wraithLimit = 4;
    [Min(1)] public int warriorLimit = 2;
    [Min(1)] public int wallLimit = 3;
    [Min(1)] public int orbLimit = 4;

    private GraveWardenBossAI currentBoss;
    private bool bossWasSeen;
    private bool seenPhaseTwo;
    private bool bossLeftScene;
    private int afterBossScans;
    private bool alertedCleanup;
    private float scanAt;
    private float fpsTimer;
    private int fpsFrames;
    private float fps;
    private int wraiths, warriors, walls, orbs;
    private int peakWraiths, peakWarriors, peakWalls, peakOrbs;
    private bool alertedWraith, alertedWarrior, alertedWall, alertedOrb;
    private GUIStyle heading, label;
    private bool initedStyle;

    private void Update()
    {
        fpsFrames++;
        fpsTimer += Time.unscaledDeltaTime;
        if (fpsTimer >= 1f)
        {
            fps = fpsFrames / Mathf.Max(0.01f, fpsTimer);
            fpsFrames = 0;
            fpsTimer = 0f;
        }
        if (Time.unscaledTime < scanAt) return;
        scanAt = Time.unscaledTime + scanIntervalSeconds;
        Scan();
    }

    private void Scan()
    {
        if (currentBoss == null)
        {
            GraveWardenBossAI next = FindAnyObjectByType<GraveWardenBossAI>();
            if (next != null)
            {
                currentBoss = next;
                bossWasSeen = true;
                bossLeftScene = false;
                afterBossScans = 0;
                alertedCleanup = false;
                seenPhaseTwo = false;
                if (logImportantEvents) Debug.Log("[GW 5B.5] Grave Warden detected; begin Boss tests.", this);
            }
        }
        if (currentBoss != null && currentBoss.IsPhaseTwo && !seenPhaseTwo)
        {
            seenPhaseTwo = true;
            if (logImportantEvents) Debug.Log("[GW 5B.5] Phase 2 reached. Verify one transition, HP bar, music, reanimation.", this);
        }
        if (bossWasSeen && currentBoss == null)
        {
            bossWasSeen = false;
            bossLeftScene = true;
            if (logImportantEvents) Debug.Log("[GW 5B.5] Boss no longer active. MANUALLY confirm Victory/Retry and scene UI.", this);
        }

        wraiths = warriors = walls = orbs = 0;
        // A single scan at 1.5s; avoid FindObjectsByType on every frame.
        Transform[] objects = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (Transform t in objects)
        {
            if (t == null) continue;
            string n = t.gameObject.name;
            if (n == "GW_Wraith_Runtime") wraiths++;
            else if (n == "GW_ReanimatedWarrior_Runtime") warriors++;
            else if (n == "GW_TombWall_Runtime") walls++;
            else if (n == "GW_SpiritOrb_Runtime") orbs++;
        }
        peakWraiths = Mathf.Max(peakWraiths, wraiths);
        peakWarriors = Mathf.Max(peakWarriors, warriors);
        peakWalls = Mathf.Max(peakWalls, walls);
        peakOrbs = Mathf.Max(peakOrbs, orbs);
        if (bossLeftScene && currentBoss == null)
        {
            afterBossScans++;
            if (afterBossScans >= 2 && !alertedCleanup && wraiths + warriors + walls + orbs > 0)
            {
                alertedCleanup = true;
                Debug.LogWarning("[GW 5B.5] Runtime objects still active after Boss disappeared: Wraith=" +
                                 wraiths + ", Warrior=" + warriors + ", Wall=" + walls + ", Orb=" + orbs +
                                 ". Check cleanup after Victory/Retry.", this);
            }
        }
        if (wraiths > wraithLimit && !alertedWraith) { alertedWraith = true; Debug.LogWarning("[GW 5B.5] Wraith count exceeded limit: " + wraiths, this); }
        if (warriors > warriorLimit && !alertedWarrior) { alertedWarrior = true; Debug.LogWarning("[GW 5B.5] Warrior count exceeded limit: " + warriors, this); }
        if (walls > wallLimit && !alertedWall) { alertedWall = true; Debug.LogWarning("[GW 5B.5] Wall count exceeded limit: " + walls, this); }
        if (orbs > orbLimit && !alertedOrb) { alertedOrb = true; Debug.LogWarning("[GW 5B.5] Spirit Orb count exceeded limit: " + orbs, this); }
    }

    private void OnGUI()
    {
        if (!showOverlay || !Application.isPlaying) return;
        if (!initedStyle)
        {
            heading = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13 };
            label = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            initedStyle = true;
        }
        float left = Mathf.Clamp(Screen.width - 300f, 4f, Screen.width);
        float top = Mathf.Min(220f, Mathf.Max(10f, Screen.height - 255f));
        GUILayout.BeginArea(new Rect(left, top, 290, 250), GUI.skin.box);
        GUILayout.Label("GW 5B.5 - PLAYTEST ONLY", heading);
        if (currentBoss == null)
            GUILayout.Label("Boss: waiting for Ancient Ruins Wave 5", label);
        else
        {
            Health hp = currentBoss.BossHealth;
            GUILayout.Label("Boss: " + (currentBoss.IsPhaseTwo ? "PHASE 2" : "PHASE 1"), label);
            if (hp != null)
                GUILayout.Label("HP: " + Mathf.CeilToInt(hp.CurrentHealth) + "/" + Mathf.CeilToInt(hp.maxHealth), label);
            GUILayout.Label("Current skill movement managed by BossAI", label);
        }
        GUILayout.Label("FPS (approx): " + Mathf.RoundToInt(fps), label);
        GUILayout.Label("Wraith: " + wraiths + "/" + wraithLimit + "   peak " + peakWraiths, label);
        GUILayout.Label("Warrior: " + warriors + "/" + warriorLimit + "   peak " + peakWarriors, label);
        GUILayout.Label("Wall: " + walls + "/" + wallLimit + "   peak " + peakWalls, label);
        GUILayout.Label("Orbit orbs: " + orbs + "/" + orbLimit + "   peak " + peakOrbs, label);
        GUILayout.Label("Pause, Victory, Retry: check manually", label);
        GUILayout.EndArea();
    }
}
