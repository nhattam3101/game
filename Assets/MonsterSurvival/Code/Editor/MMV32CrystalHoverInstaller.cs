#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Non-destructive add-on for the EXISTING Menu V3 scene.
// Never reinstalls MMV3Installer, never opens or alters gameplay scenes.
public static class MMV32CrystalHoverInstaller
{
    private const string CanvasName = "MMV3_Canvas";
    private const string CrystalName = "V3_CrystalFX_V32";
    private const string AccentName = "V3_HoverAccent_V32";

    [MenuItem("Tools/MonsterSurvival/6B V3/6 - ADD Crystal + Hover (OPEN MainMenu)")]
    public static void Add()
    {
        if (!ValidateScene()) return;
        Scene scene = SceneManager.GetActiveScene();
        GameObject canvas = FindRoot(scene, CanvasName);
        Transform backdrop = canvas.transform.Find("V3_Backdrop");
        MMV3MenuController menu = canvas.GetComponentInChildren<MMV3MenuController>(true);
        if (backdrop == null || menu == null)
        {
            EditorUtility.DisplayDialog("V3.2", "Missing V3_Backdrop or MMV3MenuController. Do NOT run V3 INSTALL again; open the correct scene.", "OK");
            return;
        }

        // Create precisely one extra uGUI graphic as child of V3_Backdrop:
        // following the backdrop's existing drift without extra SpriteRenderers.
        Transform previous = backdrop.Find(CrystalName);
        GameObject crystal;
        if (previous == null)
        {
            crystal = new GameObject(CrystalName, typeof(RectTransform), typeof(CanvasRenderer));
            crystal.layer = LayerMask.NameToLayer("UI");
            Undo.RegisterCreatedObjectUndo(crystal, "Add animated V3.2 crystal");
            crystal.transform.SetParent(backdrop, false);
        }
        else crystal = previous.gameObject;
        RectTransform rt = crystal.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(.748f, .542f);
        rt.pivot = new Vector2(.5f, .5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(212f, 320f);
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
        rt.SetAsLastSibling();
        var jewel = crystal.GetComponent<MMV32CrystalFX>();
        if (jewel == null) jewel = Undo.AddComponent<MMV32CrystalFX>(crystal);
        jewel.homePage = menu.homePage;
        jewel.raycastTarget = false;
        EditorUtility.SetDirty(jewel);

        int added = 0;
        int existing = 0;
        foreach (Button b in EnumerateButtons(menu))
        {
            if (!b) continue;
            MMV32ButtonHoverFX fx = b.GetComponent<MMV32ButtonHoverFX>();
            if (fx == null) { fx = Undo.AddComponent<MMV32ButtonHoverFX>(b.gameObject); added++; }
            else existing++;
            if (fx.accent == null)
            {
                Transform old = b.transform.Find(AccentName);
                GameObject strip;
                if (old == null)
                {
                    strip = new GameObject(AccentName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    Undo.RegisterCreatedObjectUndo(strip, "Add V3.2 button accent");
                    strip.transform.SetParent(b.transform, false);
                    strip.layer = LayerMask.NameToLayer("UI");
                }
                else strip = old.gameObject;
                RectTransform bar = strip.GetComponent<RectTransform>();
                bool mapCard = b.name.StartsWith("MapCard_");
                if (mapCard)
                {
                    bar.anchorMin = new Vector2(.08f, .015f);
                    bar.anchorMax = new Vector2(.92f, .025f);
                    fx.hoverScale = 1.02f;
                    fx.hoverOffsetX = 0f;
                    fx.tintStrength = .045f;
                }
                else
                {
                    bar.anchorMin = new Vector2(.008f,.15f);
                    bar.anchorMax = new Vector2(.019f,.85f);
                }
                bar.pivot = new Vector2(.5f,.5f);
                bar.offsetMin = bar.offsetMax = Vector2.zero;
                bar.SetAsLastSibling();
                Image image = strip.GetComponent<Image>();
                image.color = new Color(.98f,.77f,.46f,1f);
                image.raycastTarget = false;
                fx.accent = image;
            }
            EditorUtility.SetDirty(fx);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[6B V3.2] Crystal FX installed; hover added " + added + ", already present " + existing + ". No V3 UI or gameplay script replaced.");
        EditorUtility.DisplayDialog("6B V3.2", "Crystal animation + hover polish installed in OPEN MainMenu.\n\nSave Scene with Ctrl+S, then enter Play Mode.\n\nExisting Menu Controller and background motion were preserved.", "OK");
    }

    [MenuItem("Tools/MonsterSurvival/6B V3/7 - REMOVE Crystal + Hover (OPEN MainMenu)")]
    public static void Remove()
    {
        if (!ValidateScene()) return;
        Scene scene = SceneManager.GetActiveScene();
        GameObject canvas = FindRoot(scene, CanvasName);
        Transform backdrop = canvas.transform.Find("V3_Backdrop");
        if (backdrop != null)
        {
            Transform jewel = backdrop.Find(CrystalName);
            if (jewel != null) Undo.DestroyObjectImmediate(jewel.gameObject);
        }
        MMV3MenuController menu = canvas.GetComponentInChildren<MMV3MenuController>(true);
        if (menu != null)
        {
            foreach (Button b in EnumerateButtons(menu))
            {
                if (!b) continue;
                var fx = b.GetComponent<MMV32ButtonHoverFX>();
                if (fx != null) Undo.DestroyObjectImmediate(fx);
                Transform accent = b.transform.Find(AccentName);
                if (accent != null) Undo.DestroyObjectImmediate(accent.gameObject);
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[6B V3.2] Removed ONLY CrystalFX/hover add-ons. V3 controller/background untouched.");
        EditorUtility.DisplayDialog("6B V3.2", "Removed V3.2 visuals. Save the Scene with Ctrl+S.", "OK");
    }

    [MenuItem("Tools/MonsterSurvival/6B V3/8 - CHECK Crystal + Hover (READ ONLY)")]
    public static void Check()
    {
        if (!ValidateScene()) return;
        Scene scene = SceneManager.GetActiveScene();
        GameObject canvas = FindRoot(scene, CanvasName);
        MMV3MenuController menu = canvas.GetComponentInChildren<MMV3MenuController>(true);
        Transform backdrop = canvas.transform.Find("V3_Backdrop");
        bool crystal = backdrop != null && backdrop.Find(CrystalName) != null;
        int total = 0, ready = 0;
        if (menu != null)
            foreach (Button b in EnumerateButtons(menu))
            {
                if (!b) continue;
                total++;
                if (b.GetComponent<MMV32ButtonHoverFX>()) ready++;
            }
        Debug.Log("[6B V3.2 CHECK] Crystal="+(crystal?"INSTALLED":"MISSING")+", Hover="+ready+"/"+total+
            ", V3Controller="+(menu?"PRESENT":"MISSING")+", Scene="+scene.name);
    }

    private static IEnumerable<Button> EnumerateButtons(MMV3MenuController menu)
    {
        yield return menu.playButton;
        yield return menu.mapsButton;
        yield return menu.settingsButton;
        yield return menu.quitButton;
        yield return menu.mapsBackButton;
        yield return menu.mapsStartButton;
        yield return menu.settingsBackButton;
        if (menu.mapCards != null)
            foreach (Button b in menu.mapCards) yield return b;
    }

    private static bool ValidateScene()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("6B V3.2", "Stop Play Mode before using the menu.", "OK");
            return false;
        }
        Scene scene = SceneManager.GetActiveScene();
        GameObject canvas = FindRoot(scene, CanvasName);
        if (scene.name != "MainMenu" || canvas == null)
        {
            EditorUtility.DisplayDialog("6B V3.2", "Open MainMenu Scene with MMV3_Canvas, NOT SampleScene.", "OK");
            return false;
        }
        return true;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name) return root;
        return null;
    }
}
#endif
