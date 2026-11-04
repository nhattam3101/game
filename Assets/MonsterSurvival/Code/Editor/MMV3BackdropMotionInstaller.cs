#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Additive patch: modifies only MainMenu V3's backdrop animation.
// Does NOT run the V3 installer again, or touch SampleScene or menu buttons.
public static class MMV3BackdropMotionInstaller
{
    private const string FxName = "V3_BackgroundMotion";

    [MenuItem("Tools/MonsterSurvival/6B V3/4 - ADD Animated Background (OPEN MainMenu)")]
    public static void Install()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Menu V3 Background", "Stop Play Mode first.", "OK");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "MainMenu")
        {
            EditorUtility.DisplayDialog("Menu V3 Background",
                "Open the existing MainMenu scene (not SampleScene) and try again.", "OK");
            return;
        }
        Transform canvas = FindRoot(scene,"MMV3_Canvas");
        Transform bg = canvas ? canvas.Find("V3_Backdrop") : null;
        Transform ui = canvas ? canvas.Find("MMV3_Root") : null;
        if (!canvas || !bg || !ui || !bg.GetComponent<Image>())
        {
            EditorUtility.DisplayDialog("Menu V3 Background",
                "V3 structure not found: MMV3_Canvas/V3_Backdrop/MMV3_Root. Install V3 first (then its hotfixes).", "OK");
            return;
        }
        Transform fx = canvas.Find(FxName);
        if (!fx)
        {
            GameObject go = new GameObject(FxName,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(MMV3BackdropMotion));
            Undo.RegisterCreatedObjectUndo(go,"Add V3 Background Motion");
            go.transform.SetParent(canvas,false);
            fx = go.transform;
        }
        RectTransform rect = fx as RectTransform;
        Undo.RecordObject(rect,"Fit Background Motion");
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        rect.SetSiblingIndex(bg.GetSiblingIndex()+1); // ABOVE background, BELOW all menu controls

        MMV3BackdropMotion motion = fx.GetComponent<MMV3BackdropMotion>();
        if (!motion) motion = Undo.AddComponent<MMV3BackdropMotion>(fx.gameObject);
        Undo.RecordObject(motion,"Configure V3 Background Motion");
        motion.backdrop = bg as RectTransform;
        Transform home = ui.Find("PAGE_Home");
        motion.homePage = home ? home.gameObject : null;
        motion.raycastTarget = false;
        motion.enabled = true;
        EditorUtility.SetDirty(motion);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = fx.gameObject;
        Debug.Log("[Menu V3 BG] Animated background installed. UI buttons, map flow, audio and gameplay unchanged.");
        EditorUtility.DisplayDialog("Menu V3 Background", "Installed in MainMenu only. Save (Ctrl+S), then press Play to preview movement.", "OK");
    }

    [MenuItem("Tools/MonsterSurvival/6B V3/5 - REMOVE Animated Background (OPEN MainMenu)")]
    public static void Remove()
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog("Menu V3 Background", "Stop Play Mode first.", "OK"); return; }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "MainMenu")
        {
            EditorUtility.DisplayDialog("Menu V3 Background", "Open MainMenu scene first.", "OK");
            return;
        }
        Transform canvas = FindRoot(scene,"MMV3_Canvas");
        Transform fx = canvas ? canvas.Find(FxName) : null;
        if (fx)
        {
            Undo.DestroyObjectImmediate(fx.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Menu V3 BG] Only background motion removed. Original V3 backdrop and UI preserved.");
        }
        else Debug.Log("[Menu V3 BG] Nothing to remove.");
    }

    private static Transform FindRoot(Scene scene,string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name) return root.transform;
        return null;
    }
}
#endif
