using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MonsterSurvival.Menu6B1
{
    // 6B.1.2 - persistent visual-only cover. Does not control Waves, Map selection or Settings.
    // IMPORTANT: No map-selection dependencies / hard-coded scene names.
    public sealed class MM6B12SceneCover : MonoBehaviour
    {
        private CanvasGroup group;
        private bool sceneArrived;
        private bool waitForMaps;
        private float timeoutSeconds;
        private float loadedAt;
        private string loadedSceneName;
        private Coroutine monitor;

        public static MM6B12SceneCover Create(bool waitForMapSelection, float timeout)
        {
            GameObject existing = GameObject.Find("MM6B12_PersistentTransition");
            if (existing != null)
                Destroy(existing);

            var root = new GameObject("MM6B12_PersistentTransition", typeof(RectTransform));
            DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            root.AddComponent<CanvasScaler>();
            root.AddComponent<GraphicRaycaster>();
            var cg = root.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = true;

            var screen = new GameObject("DarkCover", typeof(RectTransform), typeof(Image));
            screen.transform.SetParent(root.transform, false);
            var rt = screen.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = screen.GetComponent<Image>();
            img.color = new Color(0.025f, 0.035f, 0.060f, 1f);
            img.raycastTarget = true;

            var component = root.AddComponent<MM6B12SceneCover>();
            component.group = cg;
            component.waitForMaps = waitForMapSelection;
            component.timeoutSeconds = Mathf.Clamp(timeout, 1f, 12f);
            SceneManager.sceneLoaded += component.OnSceneLoaded;
            return component;
        }

        public IEnumerator FadeIn(float seconds)
        {
            if (group == null) yield break;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, seconds));
                yield return null;
            }
            group.alpha = 1f;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            sceneArrived = true;
            loadedAt = Time.realtimeSinceStartup;
            loadedSceneName = scene.name;
            Debug.Log("[6B.1.2] Scene loaded: " + scene.name + "; waiting for destination UI.");
            if (monitor != null) StopCoroutine(monitor);
            monitor = StartCoroutine(WaitForDestination());
        }

        private IEnumerator WaitForDestination()
        {
            // Give Awake/Start and UI layout at least two frames after a scene load.
            yield return null;
            yield return null;
            float waited = 0f;
            bool pickerFound = false;
            while (waitForMaps && waited < timeoutSeconds)
            {
                if (IsMapPanelVisible()) { pickerFound = true; break; }
                yield return new WaitForSecondsRealtime(0.10f);
                waited = Time.realtimeSinceStartup - loadedAt;
            }
            if (waitForMaps)
            {
                if (pickerFound)
                    Debug.Log("[6B.1.2] Map UI ready after " + waited.ToString("F2") + "s in '" + loadedSceneName + "'.");
                else
                    Debug.LogWarning("[6B.1.2] No active Map Selection panel detected in " + timeoutSeconds.ToString("F1") + "s. Fading the cover anyway. If this is a direct gameplay scene, disable Wait For Map Selection in MainMenuController. Otherwise inspect the Map Select script/panel.");
            }
            else
            {
                yield return new WaitForSecondsRealtime(0.22f);
            }
            yield return FadeOut(0.22f);
            Destroy(gameObject);
        }

        private static bool IsMapPanelVisible()
        {
            // Search by UI names without requiring the Map Selection source script.
            var rects = FindObjectsByType<RectTransform>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (var rt in rects)
            {
                if (rt == null || !rt.gameObject.activeInHierarchy) continue;
                string compact = rt.name.Replace(" ", "").Replace("_", "").Replace("-", "").ToLowerInvariant();
                if (!(compact.Contains("mapselection") || compact.Contains("mapselect") ||
                      compact.Contains("selectmap") || compact.Contains("choosemap") ||
                      compact.Contains("levelselect"))) continue;

                bool invisibleParent = false;
                var groups = rt.GetComponentsInParent<CanvasGroup>(false);
                foreach (var cg in groups)
                {
                    if (cg == null || cg.alpha <= 0.01f) { invisibleParent = true; break; }
                }
                if (!invisibleParent) return true;
            }
            return false;
        }

        private IEnumerator FadeOut(float seconds)
        {
            if (group == null) yield break;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = 1f - Mathf.Clamp01(elapsed / Mathf.Max(0.01f, seconds));
                yield return null;
            }
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
}
