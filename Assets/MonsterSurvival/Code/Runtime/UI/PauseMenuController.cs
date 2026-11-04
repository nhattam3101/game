
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PauseMenuController : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject pausePanel;
    public GameObject pauseWindow;
    public GameObject settingsWindow;

    [Header("Game References")]
    public GameManager gameManager;
    public SkillManager skillManager;

    private bool isPaused = false;

    public bool IsPaused
    {
        get { return isPaused; }
    }

    private void Awake()
    {
        // An Pause Menu khi khoi tao
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (!EscapePressed())
            return;

        // Neu Pause Menu dang mo
        if (isPaused)
        {
            // ESC trong Settings se quay lai Pause
            if (settingsWindow != null &&
                settingsWindow.activeSelf)
            {
                CloseSettings();
            }
            else
            {
                ResumeGame();
            }

            return;
        }

        // Khong cho mo Pause khi Game Over / Victory
        if (gameManager != null &&
            gameManager.IsGameOver)
        {
            return;
        }

        // Khong cho Pause de len bang chon ky nang
        if (skillManager != null &&
            skillManager.skillPanel != null &&
            skillManager.skillPanel.activeInHierarchy)
        {
            return;
        }

        // Khong chiem quyen tam dung cua he thong khac
        if (Time.timeScale <= 0f)
        {
            return;
        }

        PauseGame();
    }

    private bool EscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        // Ho tro Unity Input System
        return Keyboard.current != null &&
               Keyboard.current.escapeKey.wasPressedThisFrame;

#elif ENABLE_LEGACY_INPUT_MANAGER
        // Ho tro Input Manager cu
        return Input.GetKeyDown(KeyCode.Escape);

#else
        return false;
#endif
    }

    public void PauseGame()
    {
        if (isPaused)
            return;

        if (gameManager == null || pausePanel == null)
        {
            Debug.LogWarning(
                "PauseMenuController: Chua gan " +
                "GameManager hoac PausePanel!"
            );
            return;
        }

        if (gameManager.IsGameOver)
            return;

        if (skillManager != null &&
            skillManager.skillPanel != null &&
            skillManager.skillPanel.activeInHierarchy)
        {
            return;
        }

        if (Time.timeScale <= 0f)
            return;

        isPaused = true;

        if (pauseWindow != null)
        {
            pauseWindow.SetActive(true);
        }

        if (settingsWindow != null)
        {
            settingsWindow.SetActive(false);
        }

        pausePanel.SetActive(true);

        Time.timeScale = 0f;

        Debug.Log("Game Paused");
    }

    public void ResumeGame()
    {
        if (!isPaused)
            return;

        isPaused = false;

        Time.timeScale = 1f;

        if (settingsWindow != null)
        {
            settingsWindow.SetActive(false);
        }

        if (pauseWindow != null)
        {
            pauseWindow.SetActive(true);
        }

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        Debug.Log("Game Resumed");
    }

    public void RestartGame()
    {
        if (gameManager == null)
        {
            Debug.LogWarning(
                "PauseMenuController: Chua gan GameManager!"
            );
            return;
        }

        // Dam bao game khong bi dung khi tai Scene
        isPaused = false;

        Time.timeScale = 1f;

        // Su dung Restart cu, khong viet lai
        gameManager.RestartStage();
    }

    public void OpenSettings()
    {
        if (!isPaused)
            return;

        if (pauseWindow != null)
        {
            pauseWindow.SetActive(false);
        }

        if (settingsWindow != null)
        {
            settingsWindow.SetActive(true);
        }
    }

    public void CloseSettings()
    {
        if (!isPaused)
            return;

        if (settingsWindow != null)
        {
            settingsWindow.SetActive(false);
        }

        if (pauseWindow != null)
        {
            pauseWindow.SetActive(true);
        }
    }
}
