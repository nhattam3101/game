#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MS5C3Installer
{
    private const string Root = "Tools/MonsterSurvival/5C Unlock System/";
    private const string Backup = "Assets/MonsterSurvival/Features/Progression/Unlock5C3/Backups";

    [MenuItem(Root + "6 - INSTALL 5C.3 Map Gate + Boss Achievements (BOTH scenes)")]
    public static void Install()
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog("5C.3", "Stop Play Mode first.", "OK"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string menuPath = FindScene("MainMenu"), gamePath = FindScene("SampleScene");
        if (menuPath == null || gamePath == null)
        { EditorUtility.DisplayDialog("5C.3", "MainMenu / SampleScene not found. Install aborted.", "OK"); return; }

        // Validate dependencies in BOTH scenes before modifying either.
        Scene menuScene = EditorSceneManager.OpenScene(menuPath, OpenSceneMode.Single);
        MMV3MenuController menu = FindComponent<MMV3MenuController>(menuScene);
        MS5C2MapUnlockUI preview = FindComponent<MS5C2MapUnlockUI>(menuScene);
        if (!menu || !preview || (menu.mapCards == null || menu.mapCards.Length < 3) || !menu.mapCards[0] || !menu.mapCards[1] || !menu.mapCards[2] || !menu.mapsStartButton)
        { EditorUtility.DisplayDialog("5C.3", "Requires existing Menu V3 + installed 5C.2 + three Map Cards + Start Button.", "OK"); return; }
        Scene gameScene = EditorSceneManager.OpenScene(gamePath, OpenSceneMode.Single);
        WaveManager wave = FindComponent<WaveManager>(gameScene);
        WaveMapSwitcher switcher = FindComponent<WaveMapSwitcher>(gameScene);
        MMV3GameplayBootstrap bootstrap = FindComponent<MMV3GameplayBootstrap>(gameScene);
        if (!wave || !switcher || !bootstrap)
        {
            EditorSceneManager.OpenScene(menuPath, OpenSceneMode.Single);
            EditorUtility.DisplayDialog("5C.3", "SampleScene requires WaveManager, WaveMapSwitcher and MMV3GameplayBootstrap. Nothing changed.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("5C.3 — APPLY MAP ENFORCEMENT",
            "Install Map START verification, gameplay map guard and Boss achievement watcher?\n\n" +
            "Legacy Profiles: all maps stay PLAYABLE unless user explicitly activates locks.\n" +
            "New Profiles: only Green Meadow starts unlocked. A new profile save is initialized on Map Select.\n" +
            "Boss wins persist achievements immediately; Boss loot is unchanged.\n\n" +
            "Both Scene files are backed up. No Save is touched by installer.", "INSTALL", "CANCEL"))
        { EditorSceneManager.OpenScene(menuPath, OpenSceneMode.Single); return; }
        try
        {
            EnsureFolder(Backup);
            BackupScene(gamePath, Backup + "/SampleScene_BEFORE_5C3.unity");
            BackupScene(menuPath, Backup + "/MainMenu_BEFORE_5C3.unity");
            // SampleScene first.
            GameObject monitor = FindRoot(gameScene, "MS5C3_RuntimeGuards");
            if (!monitor) { monitor = new GameObject("MS5C3_RuntimeGuards"); SceneManager.MoveGameObjectToScene(monitor, gameScene); Undo.RegisterCreatedObjectUndo(monitor, "Create 5C.3 guards"); }
            MS5C3GameplayGate guard = monitor.GetComponent<MS5C3GameplayGate>();
            if (!guard) guard = Undo.AddComponent<MS5C3GameplayGate>(monitor);
            guard.waveManager = wave;guard.mapSwitcher = switcher;guard.enemySpawner = wave.enemySpawner;
            MS5C3BossAchievements achievements = monitor.GetComponent<MS5C3BossAchievements>();
            if (!achievements) achievements = Undo.AddComponent<MS5C3BossAchievements>(monitor);
            achievements.waveManager = wave;
            EditorUtility.SetDirty(guard);EditorUtility.SetDirty(achievements);
            EditorSceneManager.MarkSceneDirty(gameScene);EditorSceneManager.SaveScene(gameScene);
            // MainMenu second.
            menuScene = EditorSceneManager.OpenScene(menuPath, OpenSceneMode.Single);
            menu = FindComponent<MMV3MenuController>(menuScene);
            preview = FindComponent<MS5C2MapUnlockUI>(menuScene);
            MS5C3MainMenuGate gate = menu.GetComponent<MS5C3MainMenuGate>();
            if (!gate) gate = Undo.AddComponent<MS5C3MainMenuGate>(menu.gameObject);
            gate.menu = menu;gate.mapUnlockPreview = preview;
            EditorUtility.SetDirty(gate);
            EditorSceneManager.MarkSceneDirty(menuScene);EditorSceneManager.SaveScene(menuScene);
            Debug.Log("[5C.3] INSTALLED: menu gate + gameplay guard + boss achievements. Saved BOTH scenes. No JSON changed.");
            EditorUtility.DisplayDialog("5C.3 INSTALLED", "Open MainMenu and Play. Check new Profile + legacy Profile behaviors. Run 5C.3 Audit.", "OK");
        }
        catch (Exception ex)
        { Debug.LogError("[5C.3] Installer failed: " + ex);EditorUtility.DisplayDialog("5C.3 ERROR", ex.Message, "OK"); }
    }

    [MenuItem(Root + "7 - AUDIT 5C.3 + Profiles (READ ONLY)")]
    public static void Audit()
    {
        Scene scene = SceneManager.GetActiveScene();
        Debug.Log("[5C.3 AUDIT] Scene=" + scene.name);
        if (scene.name == "MainMenu")
        {
            var gate = FindComponent<MS5C3MainMenuGate>(scene);
            var preview = FindComponent<MS5C2MapUnlockUI>(scene);
            Debug.Log("[5C.3 AUDIT] MainMenu Gate=" + (gate ? "PASS" : "MISSING") +
                ", 5C.2=" + (preview ? "PASS" : "MISSING") + ", Start=" + (gate && gate.menu && gate.menu.mapsStartButton ? "PASS" : "MISSING"));
        }
        if (scene.name == "SampleScene")
        {
            var guard = FindComponent<MS5C3GameplayGate>(scene);
            var achievement = FindComponent<MS5C3BossAchievements>(scene);
            Debug.Log("[5C.3 AUDIT] Gameplay Gate=" + (guard && guard.mapSwitcher && guard.waveManager ? "PASS" : "MISSING") +
                ", Achievements=" + (achievement && achievement.waveManager ? "PASS" : "MISSING"));
        }
        for (int slot = 1; slot <= 3; slot++)
            for (int index = 0; index < 3; index++)
            {
                MS5C3MapPolicy.State state;
                bool valid = MS5C3MapPolicy.TryGet(slot, index, out state);
                Debug.Log("[5C.3 AUDIT] Slot=" + slot + ", map=" + index + ", valid=" + valid +
                    ", allowed=" + state.allowed + ", legacy=" + state.legacy + ", status=" + state.status);
            }
        Debug.Log("[5C.3 AUDIT] READ ONLY: no profile file, Scene or achievement changed.");
    }

    [MenuItem(Root + "8 - REMOVE 5C.3 Components (BOTH scenes)")]
    public static void Remove()
    {
        if (Application.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!EditorUtility.DisplayDialog("REMOVE 5C.3", "Remove ONLY 5C.3 components from MainMenu and SampleScene? Existing unlocks, achievements and saved Gold remain untouched.", "REMOVE", "CANCEL")) return;
        string main = FindScene("MainMenu"), game = FindScene("SampleScene");
        if (main == null || game == null) { Debug.LogError("[5C.3] Missing Scene.");return; }
        Scene g=EditorSceneManager.OpenScene(game,OpenSceneMode.Single);
        var runtime = FindComponent<MS5C3GameplayGate>(g);
        if (runtime && runtime.gameObject.name == "MS5C3_RuntimeGuards") Undo.DestroyObjectImmediate(runtime.gameObject);
        else
        {
            if (runtime) Undo.DestroyObjectImmediate(runtime);
            var a=FindComponent<MS5C3BossAchievements>(g);if(a)Undo.DestroyObjectImmediate(a);
        }
        EditorSceneManager.MarkSceneDirty(g);EditorSceneManager.SaveScene(g);
        Scene m=EditorSceneManager.OpenScene(main,OpenSceneMode.Single);
        var gate=FindComponent<MS5C3MainMenuGate>(m);if(gate)Undo.DestroyObjectImmediate(gate);
        EditorSceneManager.MarkSceneDirty(m);EditorSceneManager.SaveScene(m);
        Debug.Log("[5C.3] Runtime components removed; no profile data changed.");
    }

    private static string FindScene(string name)
    {
        foreach (string id in AssetDatabase.FindAssets("t:Scene " + name))
        {
            string p=AssetDatabase.GUIDToAssetPath(id);
            if (Path.GetFileNameWithoutExtension(p).Equals(name,StringComparison.OrdinalIgnoreCase) && !p.Contains("/Backups/")) return p;
        }
        return null;
    }
    private static GameObject FindRoot(Scene scene,string name)
    { foreach(var root in scene.GetRootGameObjects())if(root.name == name)return root;return null; }
    private static T FindComponent<T>(Scene scene) where T:Component
    { foreach(var root in scene.GetRootGameObjects()) {T t=root.GetComponentInChildren<T>(true);if(t)return t;} return null; }
    private static void BackupScene(string source,string destination)
    { if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(destination)) {if(!AssetDatabase.CopyAsset(source,destination)) throw new IOException("Backup failed " + source); AssetDatabase.ImportAsset(destination);} }
    private static void EnsureFolder(string path)
    {string[] s=path.Split('/');string current=s[0];for(int i=1;i<s.Length;i++){string n=current+"/"+s[i];if(!AssetDatabase.IsValidFolder(n))AssetDatabase.CreateFolder(current,s[i]);current=n;}}
}
#endif
