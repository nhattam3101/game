
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public enum GameSessionState
{
    Starting,
    Running,
    Victory,
    Defeat
}

public class GameManager : MonoBehaviour
{
    [Header("Game Over UI")]
    public GameObject gameOverPanel;

    [Header("Victory UI")]
    public GameObject victoryPanel;

    [Header("Victory Settings")]
    public bool victoryByWaves = true;

    [Header("Old Timer Settings")]
    public float stageDuration = 120f;

    [Header("Old Timer UI (Optional)")]
    public TMP_Text timerText;

    [Header("Navigation")]
    public string mainMenuScenePath = "Assets/MonsterSurvival/Content/Scenes/MainMenu.unity";

    private float remainingTime;
    private bool isGameOver;

    private Health playerHealth;

    public bool IsGameOver
    {
        get { return isGameOver; }
    }

    public GameSessionState State { get; private set; } = GameSessionState.Starting;
    public float ElapsedPlayTime { get; private set; }
    public System.Action<GameSessionState> OnSessionEnded;

    private void Start()
    {
        Time.timeScale = 1f;

        isGameOver = false;
        State = GameSessionState.Running;
        ElapsedPlayTime = 0f;
        remainingTime = stageDuration;

        // An man hinh Game Over va Victory
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }

        // Neu dung Wave thi an dong ho cu
        if (victoryByWaves && timerText != null)
        {
            timerText.gameObject.SetActive(false);
        }

        // Tim Player
        GameObject player =
            GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            playerHealth = player.GetComponent<Health>();

            if (playerHealth != null)
            {
                playerHealth.OnDied += GameOver;
            }
            else
            {
                Debug.LogError(
                    "Player thieu component Health!"
                );
            }
        }
        else
        {
            Debug.LogError(
                "Khong tim thay Player co Tag Player!"
            );
        }

        // Chi hien dong ho khi dung che do cu
        if (!victoryByWaves)
        {
            UpdateTimerUI();
        }
    }

    private void Update()
    {
        if (isGameOver || Time.timeScale == 0f)
        {
            return;
        }

        ElapsedPlayTime += Time.deltaTime;

        // Che do Wave:
        // Khong dem thoi gian Victory cu.
        if (victoryByWaves)
        {
            return;
        }

        // Che do Victory theo thoi gian cu
        remainingTime -= Time.deltaTime;

        remainingTime = Mathf.Max(
            0f,
            remainingTime
        );

        UpdateTimerUI();

        if (remainingTime <= 0f)
        {
            Victory();
        }
    }

    private void UpdateTimerUI()
    {
        if (timerText == null)
        {
            return;
        }

        int seconds =
            Mathf.CeilToInt(remainingTime);

        int minutes = seconds / 60;

        seconds %= 60;

        timerText.text =
            $"{minutes:00}:{seconds:00}";
    }

    public void GameOver()
    {
        EndSession(GameSessionState.Defeat);
    }

    public void Victory()
    {
        EndSession(GameSessionState.Victory);
    }

    private void EndSession(GameSessionState result)
    {
        if (isGameOver || (result != GameSessionState.Victory && result != GameSessionState.Defeat))
            return;

        isGameOver = true;
        State = result;

        if (gameOverPanel != null)
            gameOverPanel.SetActive(result == GameSessionState.Defeat);
        if (victoryPanel != null)
            victoryPanel.SetActive(result == GameSessionState.Victory);

        Time.timeScale = 0f;
        OnSessionEnded?.Invoke(result);
        Debug.Log(result == GameSessionState.Victory
            ? "Hoan thanh man choi!"
            : "Player da thua!");
    }

    public void RestartStage()
    {
        Time.timeScale = 1f;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }

    public void ReturnToMainMenu()
    {
        if (string.IsNullOrWhiteSpace(mainMenuScenePath) ||
            !Application.CanStreamedLevelBeLoaded(mainMenuScenePath))
        {
            Debug.LogError("GameManager: MainMenu scene is missing or disabled: " + mainMenuScenePath, this);
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuScenePath);
    }

    private void OnDestroy()
    {
        // Huy dang ky su kien cua Player
        if (playerHealth != null)
        {
            playerHealth.OnDied -= GameOver;
        }

        Time.timeScale = 1f;
    }
}
