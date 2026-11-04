
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using MonsterSurvival.Menu6B1;

public class MainMenuController : MonoBehaviour
{
    [Header("Scene Names")]
    public string gameSceneName = "";
    public string mainMenuSceneName = "MainMenu";

    [Header("6B.1.2 - Optional PLAY transition cover")]
    [Tooltip("A dark overlay survives the scene change, so the temporary green area is not visible.")]
    public bool coverPlaySceneTransition = true;

    [Tooltip("Wait until the destination map selection UI is visible. Disable if PLAY should open gameplay directly.")]
    public bool waitForMapSelection = true;

    [Range(1f, 12f)] public float mapReadyTimeoutSeconds = 5f;

    private bool playIsLoading;

    [Header("Menu UI")]
    public GameObject mainWindow;
    public GameObject settingsWindow;

    private void Start()
    {
        // Chi khoi tao giao dien khi
        // doi tuong duoc gan cac panel Main Menu.
        if (mainWindow != null)
        {
            mainWindow.SetActive(true);
        }

        if (settingsWindow != null)
        {
            settingsWindow.SetActive(false);
        }
    }

    public void PlayGame()
    {
        if (string.IsNullOrWhiteSpace(gameSceneName))
        {
            Debug.LogError(
                "MainMenu: Chua nhap ten Scene choi!"
            );

            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(
            gameSceneName))
        {
            Debug.LogError(
                "MainMenu: Scene khong nam trong " +
                "Build Profiles: " + gameSceneName
            );

            return;
        }

        if (playIsLoading) return;
        playIsLoading = true;
        Time.timeScale = 1f;

        if (coverPlaySceneTransition)
        {
            StartCoroutine(PlayWithPersistentCover());
        }
        else
        {
            SceneManager.LoadScene(gameSceneName);
        }
    }

    private IEnumerator PlayWithPersistentCover()
    {
        // The V2 overlay belongs to MainMenu and disappears when the scene changes.
        // This NEW overlay lives until the destination UI becomes ready.
        MM6B12SceneCover cover = MM6B12SceneCover.Create(
            waitForMapSelection, mapReadyTimeoutSeconds);
        yield return cover.FadeIn(0.16f);
        Debug.Log("[6B.1.2] PLAY: loading scene '" + gameSceneName + "'.");
        SceneManager.LoadScene(gameSceneName);
    }

    public void OpenSettings()
    {
        if (mainWindow != null)
        {
            mainWindow.SetActive(false);
        }

        if (settingsWindow != null)
        {
            settingsWindow.SetActive(true);
        }
    }

    public void CloseSettings()
    {
        if (settingsWindow != null)
        {
            settingsWindow.SetActive(false);
        }

        if (mainWindow != null)
        {
            mainWindow.SetActive(true);
        }
    }

    public void ReturnToMainMenu()
    {
        if (!Application.CanStreamedLevelBeLoaded(
            mainMenuSceneName))
        {
            Debug.LogError(
                "MainMenu: Khong tim thay Scene " +
                mainMenuSceneName
            );

            return;
        }

        // Tranh mang trang thai Pause sang Menu
        Time.timeScale = 1f;

        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;

#if UNITY_EDITOR
        // Khi test bang Unity Editor
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // Khi chay ban build
        Application.Quit();
#endif
    }
}
