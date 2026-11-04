using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Real-run recorder only. No Save5A writes during defeat, quit, restart or scene teardown.
// File for Endless records is separate and carries its own integrity check.
[DefaultExecutionOrder(500)]
[DisallowMultipleComponent]
public sealed class MS7I4RunRecorder : MonoBehaviour
{
    [Header("Existing Components")]
    public MS7I1EndlessDirector endless;
    public MS7I2GameplayModeBootstrap modeBridge;
    public MS7I3EndlessBossRotation rotation;
    public WaveManager waveManager;

    [Header("OFF on installation; enable after QA")]
    public bool enableEndlessRecords = false;

    public int CapturedSlot { get; private set; }
    public string BoundProfileId { get; private set; }
    public string RunId { get; private set; }
    public bool Captured { get; private set; }
    public string Status { get; private set; } = "NOT STARTED";
    public int Errors { get; private set; }
    public int LastRecordedClearedWave { get; private set; }
    public int LastRecordedReward { get; private set; }

    private float nextIdentityRetry;
    private bool finished;

    private void Awake()
    {
        if (SceneManager.GetActiveScene().name != "SampleScene") { enabled = false; return; }
        if (!waveManager) waveManager = GetComponent<WaveManager>();
        if (!endless) endless = GetComponent<MS7I1EndlessDirector>();
        if (!rotation) rotation = GetComponent<MS7I3EndlessBossRotation>();
        if (!modeBridge) modeBridge = GetComponent<MS7I2GameplayModeBootstrap>();
        CapturedSlot = MS5A2GoldBank.SelectedSlot;
        RunId = Guid.NewGuid().ToString("N");
        Status = enableEndlessRecords ? "READY TO VERIFY PROFILE" : "DISABLED (NO RECORD / REWARD)";
    }

    private void Start() { if (enableEndlessRecords) TryBindProfile(); }

    private bool IsRealEndless()
    {
        return enableEndlessRecords && endless && endless.RuntimeEnabled &&
            endless.EndlessStarted && modeBridge && modeBridge.applied &&
            modeBridge.appliedMode == MS7I2GameMode.Endless && modeBridge.cameFromMainMenu &&
            waveManager && waveManager.gameManager;
    }

    private void TryBindProfile()
    {
        if (BoundProfileId != null) return;
        MS5A1SaveData data;
        string reason;
        if (MS7I4RecordService.TryReadSaveIdentity(CapturedSlot, out data, out reason))
        {
            BoundProfileId = data.profileId;
            Status = "BOUND Profile " + CapturedSlot + " / Endless-only pending GameOver";
        }
        else Status = "WAITING FOR VALID 5A PROFILE: " + reason;
    }

    private void Update()
    {
        if (!enableEndlessRecords || finished) return;
        if (BoundProfileId == null && Time.unscaledTime >= nextIdentityRetry)
        {
            nextIdentityRetry = Time.unscaledTime + 1f;
            TryBindProfile();
        }
        TryCaptureDefeat();
    }

    private void TryCaptureDefeat()
    {
        if (!IsRealEndless() || !waveManager.gameManager.IsGameOver) return;
        // GameManager.IsGameOver also becomes true for Victory; require visible GameOver panel.
        GameObject loss = waveManager.gameManager.gameOverPanel;
        GameObject win = waveManager.gameManager.victoryPanel;
        if (!loss || !loss.activeInHierarchy || (win && win.activeInHierarchy))
        {
            Status = "BLOCKED: not verified GameOver panel; no Endless reward";
            return;
        }
        finished = true; // one attempt per real run. Never auto-retry ambiguous disk failures.
        if (string.IsNullOrEmpty(BoundProfileId))
        {
            Errors++; Status = "BLOCKED: no verified Profile at run start"; return;
        }
        int reached = Mathf.Max(6, endless.PeakEndlessWave);
        int cleared = 5 + Mathf.Max(0, endless.WavesFinished);
        int bosses = rotation && rotation.enableBossRotation ? rotation.BossesDefeated : 0;
        // In 7I.1, WavesFinished increments at timer end BEFORE 7I.3 boss gate.
        // Never count a Wave-10/15/... clear while that Boss still lives.
        if (rotation && rotation.enableBossRotation && rotation.IsRotatingBossWave(cleared) &&
            (rotation.ActiveBossWave != cleared || !rotation.ActiveBossDefeated))
            cleared--;
        cleared = Mathf.Clamp(cleared, 5, reached);
        string result;
        if (MS7I4RecordService.TryRecordDefeat(CapturedSlot, BoundProfileId, RunId,
            reached, cleared, bosses, out result))
        {
            Captured = true;
            LastRecordedClearedWave = cleared;
            LastRecordedReward = MS7I4RecordService.RewardFor(cleared, bosses);
            Status = "RECORDED Profile=" + CapturedSlot + " reached=" + reached +
                " cleared=" + cleared + " bossKills=" + bosses +
                " pendingGold=" + LastRecordedReward + " (NO 5A GOLD WRITE)";
            Debug.Log("[7I.4] " + Status, this);
        }
        else { Errors++; Status = "NO RECORD / NO REWARD: " + result; Debug.LogWarning("[7I.4] " + Status, this); }
    }

    private void OnDisable()
    {
        // A destroyed scene without a confirmed GameOver must NEVER grant rewards.
        if (!finished && enableEndlessRecords && Application.isPlaying)
            TryCaptureDefeat();
    }
}
