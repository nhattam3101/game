
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

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

    private float remainingTime;
    private bool isGameOver;

    private Health playerHealth;

    public bool IsGameOver
    {
        get { return isGameOver; }
    }

    private void Start()
    {
        Time.timeScale = 1f;

        isGameOver = false;
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
        if (isGameOver)
        {
            return;
        }

        isGameOver = true;

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        Time.timeScale = 0f;

        Debug.Log("Player da thua!");
    }

    public void Victory()
    {
        if (isGameOver)
        {
            return;
        }

        isGameOver = true;

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }

        Time.timeScale = 0f;

        Debug.Log("Hoan thanh man choi!");
    }

    public void RestartStage()
    {
        Time.timeScale = 1f;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
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
