using UnityEngine;

/// <summary>
/// Watch an existing SampleScene run. No edits to GameManager, WaveManager, HUD or coins.
/// Only unspent Gold earned after the run starts is deposited, once after Victory.
/// Game Over, Restart before Victory and leaving the scene do not deposit.
/// </summary>
[DisallowMultipleComponent]
public sealed class MS5A2BankOnVictory : MonoBehaviour
{
    [Header("Existing scene components")]
    public GameManager gameManager;
    public PlayerProgression progression;

    [Header("Read-only run state")]
    [SerializeField] private int capturedProfileSlot;
    [SerializeField] private int startingCoins;
    [SerializeField] private int pendingGold;
    [SerializeField] private bool checkedEndOfRun;
    [SerializeField] private bool bankedSuccessfully;
    [SerializeField] private string lastMessage = "Run not started";

    public string LastMessage { get { return lastMessage; } }
    public bool BankedSuccessfully { get { return bankedSuccessfully; } }

    private void Start()
    {
        // Start, not Update: keeps this run tied to exactly one profile slot.
        capturedProfileSlot = MS5A2GoldBank.SelectedSlot;
        if (!gameManager) gameManager = FindFirstObjectByType<GameManager>();
        if (!progression) progression = FindFirstObjectByType<PlayerProgression>();
        startingCoins = progression ? Mathf.Max(0, progression.coins) : 0;
        checkedEndOfRun = false;
        bankedSuccessfully = false;
        pendingGold = 0;
        lastMessage = "Watching profile " + capturedProfileSlot + "; starting match Gold=" + startingCoins;
        if (!gameManager || !progression)
            Debug.LogError("[Save 5A.2] GameManager / PlayerProgression missing; Gold will not auto-bank.", this);
    }

    private void Update()
    {
        if (checkedEndOfRun || !gameManager || !progression) return;
        if (!gameManager.IsGameOver) return;
        checkedEndOfRun = true;

        bool victory = gameManager.victoryPanel && gameManager.victoryPanel.activeSelf;
        bool defeat = gameManager.gameOverPanel && gameManager.gameOverPanel.activeSelf;
        if (!victory || defeat)
        {
            lastMessage = "Run ended without Victory: no deposit.";
            return;
        }

        // Net remaining Gold, not total lifetime pickups; bank balance is never put in coins.
        pendingGold = Mathf.Max(0, progression.coins - startingCoins);
        CommitPending();
    }

    [ContextMenu("Retry pending Victory deposit (Play Mode)")]
    public void RetryPendingFromInspector()
    {
        if (!Application.isPlaying || !checkedEndOfRun || bankedSuccessfully || pendingGold < 0)
            return;
        if (!gameManager || !gameManager.victoryPanel || !gameManager.victoryPanel.activeSelf)
            return;
        CommitPending();
    }

    private void CommitPending()
    {
        if (bankedSuccessfully) return;
        int balance;
        string result;
        if (MS5A2GoldBank.TryDeposit(capturedProfileSlot, pendingGold, out balance, out result))
        {
            bankedSuccessfully = true; // exactly once for this scene run, even if UI stays open.
            lastMessage = result;
            Debug.Log("[Save 5A.2] " + result, this);
        }
        else
        {
            lastMessage = result;
            Debug.LogError("[Save 5A.2] " + result +
                           " | Victory reward remains pending while this scene stays open. " +
                           "Fix the save error and use component ContextMenu > Retry pending Victory deposit.", this);
        }
    }
}
