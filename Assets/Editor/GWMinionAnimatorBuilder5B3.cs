#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public sealed class GWMinionAnimatorBuilder5B3 : EditorWindow
{
    private const string ROOT = "Assets/MonsterSurvival/GraveWardenMinions5B3";
    private const string OUT = ROOT + "/Generated";
    private GameObject minionSource;
    private GameObject graveWardenBoss;

    private static readonly string[] WraithNames =
        { "Idle", "Move", "Spawn", "Attack", "Hit", "Death", "SoulCharge", "HoverFast" };
    private static readonly int[] WraithFrames = { 16, 18, 14, 18, 10, 18, 14, 18 };
    private static readonly string[] WarriorNames =
        { "Idle", "Move", "Spawn", "Attack", "Hit", "Death", "Guard", "Empower" };
    private static readonly int[] WarriorFrames = { 16, 18, 14, 18, 10, 18, 14, 16 };

    [MenuItem("Tools/MonsterSurvival/5B.3 Animated Wraith and Skeleton Setup")]
    private static void ShowWindow()
    {
        GetWindow<GWMinionAnimatorBuilder5B3>("GW Minions 5B.3").minSize = new Vector2(520, 360);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("GRAVE WARDEN - ANIMATED MINIONS 5B.3", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("250 separate PNGs / 16 animation clips / 2 controllers.\n" +
            "Only the COPY of MinionEnemy is modified; existing Wave 4 minion is preserved.", MessageType.Info);
        EditorGUILayout.Space(8);
        minionSource = (GameObject)EditorGUILayout.ObjectField(
            "Original working MinionEnemy", minionSource, typeof(GameObject), false);
        graveWardenBoss = (GameObject)EditorGUILayout.ObjectField(
            "GraveWardenBoss prefab", graveWardenBoss, typeof(GameObject), false);
        EditorGUILayout.Space(12);
        if (GUILayout.Button("1) Build 16 Animation Clips + 2 Controllers", GUILayout.Height(37)))
        {
            BuildAll();
        }
        if (GUILayout.Button("2) Create TWO Animated Minion Prefabs", GUILayout.Height(37)))
        {
            CreateMinions();
        }
        if (GUILayout.Button("3) Assign New Minions to Grave Warden", GUILayout.Height(37)))
        {
            AssignToBoss();
        }
        EditorGUILayout.HelpBox("After step 3, press Play, Ancient Ruins / Wave 5. " +
            "Do not add GW_Visual's Animator to the original MinionEnemy. " +
            "Sprite frames are SINGLE sprites; do not use Slice.", MessageType.None);
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    private static bool IsLoop(string name)
    {
        return name == "Idle" || name == "Move" || name == "HoverFast" || name == "Guard";
    }

    private static int Fps(string name)
    {
        if (name == "Attack") return 18;
        if (name == "Move" || name == "HoverFast") return 16;
        return 14;
    }

    private static Sprite[] LoadFrames(string category, string clip, int count)
    {
        Sprite[] frames = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            string path = string.Format("{0}/Frames/{1}/{2}/{1}_{2}_{3:D3}.png",
                ROOT, category, clip, i);
            frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (frames[i] == null)
            {
                Debug.LogError("[GW5B.3] Missing Sprite: " + path +
                    ". Check that PNGs were copied into Assets and imported as Sprite (2D and UI).");
                return null;
            }
        }
        return frames;
    }

    private static AnimationClip CreateClip(string category, string name, int framesCount)
    {
        Sprite[] frames = LoadFrames(category, name, framesCount);
        if (frames == null) return null;
        string path = OUT + "/GW_" + category + "_" + name + ".anim";
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) != null)
            AssetDatabase.DeleteAsset(path);
        AnimationClip clip = new AnimationClip();
        clip.frameRate = Fps(name);
        clip.name = "GW_" + category + "_" + name;
        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++)
        {
            keys[i] = new ObjectReferenceKeyframe();
            keys[i].time = (float)i / clip.frameRate;
            keys[i].value = frames[i];
        }
        EditorCurveBinding binding = new EditorCurveBinding
        {
            path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite"
        };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = IsLoop(name);
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static bool BuildCategory(string category, string[] names, int[] frameCounts)
    {
        Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
        for (int i = 0; i < names.Length; i++)
        {
            AnimationClip clip = CreateClip(category, names[i], frameCounts[i]);
            if (clip == null) return false;
            clips[names[i]] = clip;
        }
        string ctlPath = OUT + "/GW_" + category + "_Animated.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ctlPath) != null)
            AssetDatabase.DeleteAsset(ctlPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ctlPath);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        Dictionary<string, AnimatorState> states = new Dictionary<string, AnimatorState>();
        foreach (string key in names)
        {
            AnimatorState state = machine.AddState(key);
            state.motion = clips[key];
            states[key] = state;
        }
        machine.defaultState = states["Spawn"];
        AnimatorStateTransition endSpawn = states["Spawn"].AddTransition(states["Idle"]);
        endSpawn.hasExitTime = true;
        endSpawn.exitTime = 1f;
        endSpawn.duration = 0.04f;
        AssetDatabase.SaveAssets();
        return true;
    }

    private static void BuildAll()
    {
        EnsureFolder(OUT);
        try
        {
            bool a = BuildCategory("Wraith", WraithNames, WraithFrames);
            bool b = a && BuildCategory("Warrior", WarriorNames, WarriorFrames);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (a && b) EditorUtility.DisplayDialog("GW 5B.3", "Built 16 clips and 2 controllers in Generated.", "OK");
            else EditorUtility.DisplayDialog("GW 5B.3", "Missing sprites. Check Console + Frames folder.", "OK");
        }
        catch (Exception e) { Debug.LogException(e); EditorUtility.DisplayDialog("GW5B.3", e.Message, "OK"); }
    }

    private static string PrefabPath(string category)
    {
        return OUT + "/GW_" + category + "_Animated.prefab";
    }

    private void CreateMinions()
    {
        if (minionSource == null || !PrefabUtility.IsPartOfPrefabAsset(minionSource))
        {
            EditorUtility.DisplayDialog("GW5B.3", "Select the original MinionEnemy PREFAB in Project, not a Scene instance.", "OK");
            return;
        }
        foreach (string category in new[] { "Wraith", "Warrior" })
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(OUT + "/GW_" + category + "_Animated.controller") == null)
            {
                EditorUtility.DisplayDialog("GW5B.3", "First click step 1: Build Animations.", "OK");
                return;
            }
        }
        string sourcePath = AssetDatabase.GetAssetPath(minionSource);
        try
        {
            foreach (string category in new[] { "Wraith", "Warrior" })
            {
                string dest = PrefabPath(category);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(dest) == null)
                {
                    if (!AssetDatabase.CopyAsset(sourcePath, dest))
                        throw new Exception("Could not duplicate MinionEnemy to " + dest);
                }
                GameObject root = PrefabUtility.LoadPrefabContents(dest);
                try
                {
                    root.name = "GW_" + category + "_Animated";
                    // The source MinionEnemy is NEVER modified.
                    MonoBehaviour[] scripts = root.GetComponentsInChildren<MonoBehaviour>(true);
                    foreach (MonoBehaviour script in scripts)
                    {
                        if (script != null && script.GetType().Name == "SummonedMinion")
                            DestroyImmediate(script);
                    }
                    Transform visual = root.transform.Find("GW_Visual");
                    if (visual == null)
                    {
                        GameObject newVisual = new GameObject("GW_Visual");
                        newVisual.transform.SetParent(root.transform, false);
                        visual = newVisual.transform;
                    }
                    visual.localPosition = Vector3.zero;
                    visual.localRotation = Quaternion.identity;
                    visual.localScale = Vector3.one;
                    SpriteRenderer sr = visual.GetComponent<SpriteRenderer>();
                    if (sr == null) sr = visual.gameObject.AddComponent<SpriteRenderer>();
                    SpriteRenderer[] allSR = root.GetComponentsInChildren<SpriteRenderer>(true);
                    foreach (SpriteRenderer other in allSR)
                    {
                        if (other == sr) continue;
                        sr.sortingLayerID = other.sortingLayerID;
                        sr.sortingOrder = other.sortingOrder;
                        other.enabled = false; // only the copied prefab, never original
                    }
                    sr.enabled = true;
                    sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(string.Format(
                        "{0}/Frames/{1}/Idle/{1}_Idle_000.png", ROOT, category));
                    Animator animator = visual.GetComponent<Animator>();
                    if (animator == null) animator = visual.gameObject.AddComponent<Animator>();
                    animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                        OUT + "/GW_" + category + "_Animated.controller");
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    GWMinionAnimationBridge5B3 bridge = root.GetComponent<GWMinionAnimationBridge5B3>();
                    if (bridge == null) bridge = root.AddComponent<GWMinionAnimationBridge5B3>();
                    bridge.isWraith = category == "Wraith";
                    bridge.visualAnimator = animator;
                    bridge.visualRenderer = sr;
                    Health hp = root.GetComponent<Health>();
                    if (hp != null) hp.maxHealth = category == "Wraith" ? 20f : 25f;
                    EnemyController controller = root.GetComponent<EnemyController>();
                    if (controller != null)
                    {
                        controller.damage = category == "Wraith" ? 3f : 4f;
                        controller.expReward = 1;
                        controller.coinReward = 1;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, dest);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("GW5B.3", "Animated Wraith + Skeleton Warrior prefabs are ready in Generated.", "OK");
        }
        catch (Exception e) { Debug.LogException(e); EditorUtility.DisplayDialog("GW5B.3", e.Message, "OK"); }
    }

    private void AssignToBoss()
    {
        if (graveWardenBoss == null || !PrefabUtility.IsPartOfPrefabAsset(graveWardenBoss))
        {
            EditorUtility.DisplayDialog("GW5B.3", "Select GraveWardenBoss PREFAB asset in Project, not Scene instance.", "OK");
            return;
        }
        GameObject wraith = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath("Wraith"));
        GameObject warrior = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath("Warrior"));
        if (wraith == null || warrior == null)
        {
            EditorUtility.DisplayDialog("GW5B.3", "First run step 2 to create both minion prefabs.", "OK");
            return;
        }
        string path = AssetDatabase.GetAssetPath(graveWardenBoss);
        GameObject boss = PrefabUtility.LoadPrefabContents(path);
        try
        {
            MonoBehaviour summons = null;
            foreach (MonoBehaviour component in boss.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component != null && component.GetType().Name == "GraveWardenSummons5B1")
                {
                    summons = component;
                    break;
                }
            }
            if (summons == null)
            {
                EditorUtility.DisplayDialog("GW5B.3", "GraveWardenSummons5B1 not found on Boss Prefab. See manual assignment in README.", "OK");
                return;
            }
            SerializedObject so = new SerializedObject(summons);
            SerializedProperty w = so.FindProperty("wraithPrefab");
            SerializedProperty r = so.FindProperty("reanimatedWarriorPrefab");
            if (w == null || r == null)
                throw new Exception("Prefabs fields not found. Please connect manually in Inspector.");
            w.objectReferenceValue = wraith;
            r.objectReferenceValue = warrior;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(boss, path);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("GW5B.3", "Boss now summons animated Wraith + Skeleton Warrior.", "OK");
        }
        catch (Exception e) { Debug.LogException(e); EditorUtility.DisplayDialog("GW5B.3", e.Message, "OK"); }
        finally { PrefabUtility.UnloadPrefabContents(boss); }
    }
}
#endif
