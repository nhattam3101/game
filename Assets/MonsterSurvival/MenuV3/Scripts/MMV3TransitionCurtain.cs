using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Session-only map choice (no PlayerPrefs and no gameplay data changes).
public static class MMV3MapSession
{
    public static int SelectedMap { get; private set; } = -1;
    public static bool HasChoice { get { return SelectedMap >= 0; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() { SelectedMap = -1; }

    public static void Set(int index) { SelectedMap = Mathf.Clamp(index, 0, 2); }
    public static int ConsumeOrDefault(int fallback)
    {
        int result = SelectedMap >= 0 ? SelectedMap : fallback;
        SelectedMap = -1;
        return result;
    }
}

// One dark transition canvas survives the scene load; gameplay explicitly signals readiness.
public sealed class MMV3TransitionCurtain : MonoBehaviour
{
    private static MMV3TransitionCurtain instance;
    private CanvasGroup group;
    private bool ready;

    public static void BeginLoad(string sceneName)
    {
        if (instance != null) return;
        var go = new GameObject("MMV3_PersistentTransition");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<MMV3TransitionCurtain>();
        instance.BuildCanvas();
        instance.StartCoroutine(instance.Run(sceneName));
    }

    public static void NotifyGameplayReady()
    {
        if (instance != null) instance.ready = true;
    }

    private void BuildCanvas()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        gameObject.AddComponent<CanvasScaler>();
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = true;
        group.blocksRaycasts = true;
        var bg = new GameObject("DarkCover", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(transform, false);
        var rt = bg.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var image = bg.GetComponent<Image>();
        image.color = new Color(0.035f, 0.025f, 0.065f, 1f);
        image.raycastTarget = true;
    }

    private IEnumerator Run(string sceneName)
    {
        yield return Fade(0f, 1f, 0.22f);
        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (op == null) { Debug.LogError("[Menu V3] Could not load " + sceneName); Destroy(gameObject); yield break; }
        while (!op.isDone) yield return null;

        float elapsed = 0f;
        while (!ready && elapsed < 10f)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        if (!ready)
            Debug.LogWarning("[Menu V3] Gameplay ready signal timed out. Check MMV3GameplayBootstrap and MapSelectionUI.");
        yield return Fade(1f, 0f, 0.34f);
        Destroy(gameObject);
    }

    private IEnumerator Fade(float start, float end, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, end, Mathf.Clamp01(t / duration));
            yield return null;
        }
        group.alpha = end;
    }

    private void OnDestroy() { if (instance == this) instance = null; }
}
