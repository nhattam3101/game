using UnityEngine;
using UnityEngine.SceneManagement;

// Additive, watches 7H2's successfully saved Victory; does not duplicate boss/Gold events.
[DisallowMultipleComponent]
public sealed class MS7H3PeriodicVictoryObserver : MonoBehaviour
{
    public MS7H2VictoryProgressObserver progressObserver;
    public MS5A3PersistentRun persistentRun;
    [SerializeField] private int capturedSlot;
    [SerializeField] private int beforeWins, beforeBosses, beforeEarnedGold;
    [SerializeField] private bool initialized, finished;
    [SerializeField] private int attempts;
    [SerializeField] private string status = "Waiting for committed 7H2 Victory";
    private float nextRetry;
    public bool Finished { get { return finished; } }
    public int Attempts { get { return attempts; } }
    public string Status { get { return status; } }
    private void Start()
    {
        if (SceneManager.GetActiveScene().name != "SampleScene") { enabled=false;return; }
        capturedSlot=MS5A2GoldBank.SelectedSlot;
        if (progressObserver==null) progressObserver=GetComponent<MS7H2VictoryProgressObserver>();
        if (persistentRun==null && progressObserver!=null) persistentRun=progressObserver.persistentRun;
        if (persistentRun==null) persistentRun=FindFirstObjectByType<MS5A3PersistentRun>();
        MS5A1SaveData data = null; string message; bool recovered;
        bool safe = capturedSlot>=1 && capturedSlot<=3 &&
            MS5A1SaveService.TryLoad(capturedSlot,out data,out message,out recovered) && data!=null && !recovered;
        if (safe)
        {
            beforeWins=MS7H2ProgressService.Wins(data);
            beforeBosses=MS7H2ProgressService.Bosses(data);
            beforeEarnedGold=MS7H2ProgressService.EarnedGold(data);
        }
        initialized=safe && progressObserver!=null && persistentRun!=null;
        status=initialized?"7H3 observing 7H2 committed run (no legacy progress backfill)":
            "BLOCKED: missing valid Save, 7H2 observer or 5A3 PersistentRun";
        if(!initialized) Debug.LogWarning("[7H3] "+status,this);
    }

    private void Update()
    {
        if (!initialized || finished || !Application.isPlaying) return;
        if (capturedSlot != MS5A2GoldBank.SelectedSlot)
        {
            status = "BLOCKED: Profile changed inside match";
            return;
        }
        if (persistentRun == null || !persistentRun.Committed) return;
        if (Time.unscaledTime < nextRetry) return;
        nextRetry = Time.unscaledTime + 2f;
        TryCommitAfter7H2(false);
    }
    
    private void TryCommitAfter7H2(bool unloading)
    {
        if (!initialized || finished || !Application.isPlaying ||
            persistentRun == null || !persistentRun.Committed ||
            progressObserver == null || capturedSlot != MS5A2GoldBank.SelectedSlot)
            return;
    
        // On scene unload, 7H2 might not have reached its next Update() yet.
        // 7H2's existing RetryProgressSync is idempotent by committed run ID.
        if (unloading && !progressObserver.Finished)
            progressObserver.RetryProgressSync();
    
        if (!progressObserver.Finished)
        {
            status = "PENDING: 7H2 has not committed progress yet";
            return;
        }
        attempts++;
        MS5A1SaveData data = null;
        string info;
        bool recovered;
        if (!MS5A1SaveService.TryLoad(capturedSlot, out data, out info, out recovered) ||
            data == null || recovered)
        {
            status = "BLOCKED: Save unavailable after Victory";
            return;
        }
        string result;
        bool ok = MS7H3PeriodicQuestService.TrySyncVictory(capturedSlot,
            data.lastCommittedRunId, beforeWins, beforeBosses, beforeEarnedGold, out result);
        status = result;
        if (ok || result == "Run already recorded in 7H3")
        {
            finished = true;
            Debug.Log("[7H3.3] " + status, this);
        }
        else if (unloading || attempts == 1 || attempts % 8 == 0)
            Debug.LogWarning("[7H3.3] Sync pending: " + result, this);
    }
    
    private void OnDisable()
    {
        // Last chance to save in SampleScene BEFORE the 7H2 owner is unloaded.
        // Do not write when the user quits the game without committed Victory.
        if (initialized && !finished && Application.isPlaying &&
            persistentRun != null && persistentRun.Committed)
            TryCommitAfter7H2(true);
    }
    
}