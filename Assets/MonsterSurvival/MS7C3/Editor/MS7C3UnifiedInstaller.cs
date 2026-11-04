#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Opt-in, undoable installer: creates ONE UI GameObject, disables old views only.
/// Does not edit SkillManager, combat scripts, prefab assets or Save files.
/// </summary>
public static class MS7C3UnifiedInstaller
{
    private const string Root = "Assets/MonsterSurvival/MS7C3/Icons/";
    private const string UiName = "MS7C3_UnifiedSynergyUI";
    private static readonly string[] IconNames = {
        "FrozenBurst", "PhantomChain", "RapidBlast", "WildfireConduit", "GlacialRebound", "ThunderPrecision"
    };

    [MenuItem("Tools/MonsterSurvival/7C.3/1 - Install Unified UI in Active Scene")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("7C.3", "Exit Play Mode before installing.", "OK");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[7C.3] Open SampleScene before installing.");
            return;
        }
        if (!EditorUtility.DisplayDialog("7C.3 - INSTALL", 
            "Active Scene: " + scene.name +
            "\n\nCreates 1 UI child under existing Canvas. Disables only old Synergy UI behaviours (not gameplay cores)." +
            "\n\nThis is an undoable Scene change. Back up the Scene first. Continue?", "Install", "Cancel"))
            return;

        MS7C3UnifiedSynergyUI[] existing = UnityEngine.Object.FindObjectsByType<MS7C3UnifiedSynergyUI>(
            FindObjectsInactive.Include);
        foreach (MS7C3UnifiedSynergyUI instance in existing)
        {
            if (instance.gameObject.scene == scene)
            {
                Debug.LogWarning("[7C.3] Already installed. Will refresh icon / references; no duplicate created.");
                Configure(instance, scene, false);
                return;
            }
        }

        Canvas canvas = FindSafeCanvas(scene);
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("7C.3", 
                "Could not find a gameplay Canvas. Open the gameplay Scene with SkillManager and Canvas, then retry.", "OK");
            return;
        }
        GameObject go = new GameObject(UiName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Install 7C.3 Synergy UI");
        go.transform.SetParent(canvas.rootCanvas.transform, false);
        MS7C3UnifiedSynergyUI ui = Undo.AddComponent<MS7C3UnifiedSynergyUI>(go);
        Configure(ui,scene,true);
        Selection.activeGameObject=go;
        Debug.Log("[7C.3] Installed under existing Canvas. Save Scene manually after checking Audit.");
    }

    private static void Configure(MS7C3UnifiedSynergyUI ui, Scene scene, bool firstInstall)
    {
        PlayerStats stats = FindSceneComponent<PlayerStats>(scene);
        SkillManager skills = FindSceneComponent<SkillManager>(scene);
        Sprite[] icons = new Sprite[6];
        for (int i=0;i<IconNames.Length;i++)
        {
            string path=Root + "MS7C3_" + IconNames[i] + ".png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            icons[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        Undo.RecordObject(ui,"7C.3 Configure UI");
        ui.EditorAssign(stats,skills,icons);
        // Bind only an explicitly named public bool from an existing 7C.2 component.
        // No 7C.2 component is created or modified.
        ui.EditorBindCombatSources(
            Find7C2Source(scene, "IsWildfireConduitActive"),
            Find7C2Source(scene, "IsGlacialReboundActive"),
            Find7C2Source(scene, "IsThunderPrecisionActive"));
        EditorUtility.SetDirty(ui);

        if (firstInstall)
        {
            List<MonoBehaviour> views = new List<MonoBehaviour>();
            List<bool> states = new List<bool>();
            MonoBehaviour[] all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include);
            foreach (MonoBehaviour behaviour in all)
            {
                if (behaviour == null || behaviour.gameObject.scene != scene || behaviour == ui) continue;
                string className=behaviour.GetType().Name;
                bool oldLegacy = className == "SkillSynergyPanelUI";
                bool old7C1View = className.StartsWith("MS7C1",StringComparison.OrdinalIgnoreCase) &&
                    (className.IndexOf("Readout",StringComparison.OrdinalIgnoreCase) >= 0 ||
                     className.IndexOf("PanelUI",StringComparison.OrdinalIgnoreCase) >= 0 ||
                     className.IndexOf("SynergyUI",StringComparison.OrdinalIgnoreCase) >= 0);
                // IMPORTANT: never disable MS7C1 unlock core or MS7C2 combat code.
                if (!oldLegacy && !old7C1View) continue;
                views.Add(behaviour);
                states.Add(behaviour.enabled);
                if (behaviour.enabled)
                {
                    Undo.RecordObject(behaviour,"7C.3 Disable obsolete UI view");
                    behaviour.enabled=false;
                    EditorUtility.SetDirty(behaviour);
                }
                Debug.Log("[7C.3] Old view: " + className + " enabled originally=" + states[states.Count-1]);
            }
            ui.EditorRememberLegacy(views.ToArray(),states.ToArray());
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[7C.3] Config: playerStats="+(stats!=null)+
            "; skillManager="+(skills!=null)+"; icons="+CountIcons(icons)+"/6; Gameplay scripts left untouched.");
    }

    [MenuItem("Tools/MonsterSurvival/7C.3/2 - Audit Active Scene (Read Only)")]
    public static void Audit()
    {
        Scene scene=SceneManager.GetActiveScene();
        MS7C3UnifiedSynergyUI[] uis=UnityEngine.Object.FindObjectsByType<MS7C3UnifiedSynergyUI>(
            FindObjectsInactive.Include);
        int count=0;
        MS7C3UnifiedSynergyUI found=null;
        foreach (var ui in uis) if (ui.gameObject.scene==scene) { count++; found=ui; }
        int errors=0;
        int warn=0;
        if (count!=1) { errors++; Debug.LogError("[7C.3 AUDIT] Expected 1 UI, found "+count); }
        else Debug.Log("[7C.3 AUDIT] PASS: Exactly one unified UI");
        if (found!=null)
        {
            if (!found.EditorReferencesComplete) { errors++; Debug.LogError("[7C.3 AUDIT] Missing PlayerStats/SkillManager references"); }
            else Debug.Log("[7C.3 AUDIT] PASS: Gameplay refs linked");
            if (!found.EditorIconComplete) { warn++; Debug.LogWarning("[7C.3 AUDIT] Some icon assets missing"); }
            else Debug.Log("[7C.3 AUDIT] PASS: six sprites linked");
        }
        MonoBehaviour[] scripts=UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
        foreach (var script in scripts)
        {
            if(script==null || script.gameObject.scene != scene || !script.enabled) continue;
            string name=script.GetType().Name;
            if(name=="SkillSynergyPanelUI" ||
               (name.StartsWith("MS7C1",StringComparison.OrdinalIgnoreCase) &&
                (name.Contains("Readout") || name.Contains("PanelUI") || name.Contains("SynergyUI"))))
            {
                warn++;
                Debug.LogWarning("[7C.3 AUDIT] Old UI still ENABLED: "+name+" at "+script.name+
                    ". Do not disable an MS7C1 core, only its UI view.");
            }
        }
        Debug.Log("[7C.3 AUDIT] FINISHED: error="+errors+" warning="+warn+
                  ". This is an EDIT-MODE structure audit, NOT a Play Mode verification of 7C2 activity.");
    }

    [MenuItem("Tools/MonsterSurvival/7C.3/3 - Uninstall UI and Restore Prior Views")]
    public static void Uninstall()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("7C.3", "Exit Play Mode first.", "OK");
            return;
        }
        Scene scene=SceneManager.GetActiveScene();
        if(!EditorUtility.DisplayDialog("7C.3 - ROLLBACK",
            "Remove the 7C.3 UI object and restore recorded enabled flags of old UI views?"+
            "\nNo combat, prefab, skill, or save data will be changed.","Restore","Cancel"))return;
        MS7C3UnifiedSynergyUI[] uis=UnityEngine.Object.FindObjectsByType<MS7C3UnifiedSynergyUI>(
            FindObjectsInactive.Include);
        foreach(var ui in uis)
        {
            if(ui==null || ui.gameObject.scene!=scene)continue;
            var views=ui.EditorPreviousPanels;
            var states=ui.EditorPreviousStates;
            if(views!=null && states!=null)
            {
                for(int i=0;i<Math.Min(views.Length,states.Length);i++)
                {
                    if(views[i]==null)continue;
                    Undo.RecordObject(views[i],"Restore previous Synergy UI state");
                    views[i].enabled=states[i];
                    EditorUtility.SetDirty(views[i]);
                }
            }
            Undo.DestroyObjectImmediate(ui.gameObject);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[7C.3] Rolled back UI only. Save Scene if correct.");
    }

    private static MonoBehaviour Find7C2Source(Scene scene, string boolMember)
    {
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
        var scripts = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include);
        foreach(var script in scripts)
        {
            if (script == null || script.gameObject.scene != scene) continue;
            Type type = script.GetType();
            if (!type.Name.StartsWith("MS7C2", StringComparison.OrdinalIgnoreCase)) continue;
            var prop = type.GetProperty(boolMember, flags);
            if (prop != null && prop.PropertyType == typeof(bool) && prop.GetIndexParameters().Length == 0)
                return script;
            var field = type.GetField(boolMember, flags);
            if (field != null && field.FieldType == typeof(bool)) return script;
            var method = type.GetMethod(boolMember, flags, null, Type.EmptyTypes, null);
            if (method != null && method.ReturnType == typeof(bool)) return script;
        }
        return null;
    }

    private static T FindSceneComponent<T>(Scene scene) where T: Component
    {
        T[] all=UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        foreach(T t in all) if(t.gameObject.scene==scene) return t;
        return null;
    }

    private static Canvas FindSafeCanvas(Scene scene)
    {
        SkillManager manager=FindSceneComponent<SkillManager>(scene);
        if(manager!=null && manager.skillPanel!=null)
        {
            Canvas canvas=manager.skillPanel.GetComponentInParent<Canvas>(true);
            if(canvas!=null && canvas.gameObject.scene==scene)return canvas.rootCanvas;
        }
        Canvas[] all=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        foreach(Canvas canvas in all)
            if(canvas.gameObject.scene==scene && canvas.isRootCanvas &&
               canvas.name.IndexOf("Gameplay",StringComparison.OrdinalIgnoreCase)>=0)
                return canvas;
        foreach(Canvas canvas in all)
            if(canvas.gameObject.scene==scene && canvas.isRootCanvas &&
               canvas.name.IndexOf("HUD",StringComparison.OrdinalIgnoreCase)>=0)
                return canvas;
        foreach(Canvas canvas in all)
            if(canvas.gameObject.scene==scene && canvas.isRootCanvas) return canvas;
        return null;
    }

    private static int CountIcons(Sprite[] sprites)
    {
        int count=0;
        foreach(var s in sprites)if(s!=null)count++;
        return count;
    }
}
#endif
