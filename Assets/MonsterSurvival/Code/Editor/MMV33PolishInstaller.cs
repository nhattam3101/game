#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// V3.3 add-on: only adds/removes new components in the OPEN MainMenu.
// No existing V3 scripts, V3.2 components, AudioManager or gameplay Scene are modified.
public static class MMV33PolishInstaller
{
    private const string AudioRoot = "Assets/MonsterSurvival/Features/UI/MenuV3/Audio/";

    [MenuItem("Tools/MonsterSurvival/6B V3/9 - ADD Page Transitions + UI Sound (OPEN MainMenu)")]
    private static void Install()
    {
        MMV3MenuController menu = GetMenu();
        if (menu == null) return;
        var fx = menu.GetComponent<MMV33PageTransitions>();
        if (fx == null) fx = Undo.AddComponent<MMV33PageTransitions>(menu.gameObject);
        fx.menu = menu;
        EditorUtility.SetDirty(fx);

        AudioClip hover = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioRoot + "V3_Hover.wav");
        AudioClip click = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioRoot + "V3_Click.wav");
        int added = 0, reused = 0;
        foreach (Button b in Buttons(menu))
        {
            if (b == null) continue;
            var audio = b.GetComponent<MMV33ButtonSounds>();
            if (audio == null) { audio = Undo.AddComponent<MMV33ButtonSounds>(b.gameObject); added++; }
            else reused++;
            audio.hoverClip = hover;
            audio.clickClip = click;
            EditorUtility.SetDirty(audio);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[6B V3.3] Installed. Transition=READY, new button sounds=" + added + ", reused=" + reused + ". Previous systems untouched.");
        EditorUtility.DisplayDialog("6B V3.3", "Installed in OPEN MainMenu. Save Ctrl+S, enter Play Mode. Test PLAY / MAP SELECT / SETTINGS and BACK.", "OK");
    }

    [MenuItem("Tools/MonsterSurvival/6B V3/10 - REMOVE V3.3 Page Transitions + UI Sound")]
    private static void Uninstall()
    {
        MMV3MenuController menu = GetMenu();
        if (menu == null) return;
        foreach (Button b in Buttons(menu))
        {
            if (b == null) continue;
            var s = b.GetComponent<MMV33ButtonSounds>();
            if (s != null) Undo.DestroyObjectImmediate(s);
        }
        var fx = menu.GetComponent<MMV33PageTransitions>();
        if (fx != null) Undo.DestroyObjectImmediate(fx);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[6B V3.3] Removed V3.3 components only; V3.1 and V3.2 retained.");
        EditorUtility.DisplayDialog("6B V3.3", "Removed the V3.3 effect components. Ctrl+S to save.", "OK");
    }

    [MenuItem("Tools/MonsterSurvival/6B V3/11 - CHECK V3.3 Setup (READ ONLY)")]
    private static void Check()
    {
        MMV3MenuController menu = GetMenu();
        if (menu == null) return;
        int total = 0, withSound = 0;
        foreach (Button b in Buttons(menu))
        {
            if (b == null) continue;
            total++;
            if (b.GetComponent<MMV33ButtonSounds>() != null) withSound++;
        }
        var fx = menu.GetComponent<MMV33PageTransitions>();
        Debug.Log("[6B V3.3 CHECK] PageTransitions=" + (fx ? "READY" : "MISSING") +
            ", UI sounds=" + withSound + "/" + total +
            ", Home=" + (menu.homePage ? "YES" : "NO") +
            ", Maps=" + (menu.mapsPage ? "YES" : "NO") +
            ", Settings=" + (menu.settingsPage ? "YES" : "NO") +
            ", AudioManager present=" + (FindAudio(SceneManager.GetActiveScene()) ? "YES" : "NO"));
    }

    private static bool FindAudio(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponentInChildren<AudioManager>(true) != null) return true;
        return false;
    }

    private static IEnumerable<Button> Buttons(MMV3MenuController menu)
    {
        var seen = new HashSet<Button>();
        Button[] main = {
            menu.playButton, menu.mapsButton, menu.settingsButton,
            menu.quitButton, menu.mapsBackButton,
            menu.mapsStartButton, menu.settingsBackButton
        };
        foreach (Button b in main) if (b != null && seen.Add(b)) yield return b;
        if (menu.mapCards != null)
            foreach (Button b in menu.mapCards)
                if (b != null && seen.Add(b)) yield return b;
    }

    private static MMV3MenuController GetMenu()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("6B V3.3", "Stop Play Mode first.", "OK");
            return null;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "MainMenu")
        {
            EditorUtility.DisplayDialog("6B V3.3", "Open MainMenu.unity first, not SampleScene.", "OK");
            return null;
        }
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name != "MMV3_Canvas") continue;
            var controller = root.GetComponentInChildren<MMV3MenuController>(true);
            if (controller != null) return controller;
        }
        EditorUtility.DisplayDialog("6B V3.3", "Could not locate MMV3_Canvas/MMV3MenuController. Install V3 first, but do not reinstall it if already present.", "OK");
        return null;
    }
}
#endif
