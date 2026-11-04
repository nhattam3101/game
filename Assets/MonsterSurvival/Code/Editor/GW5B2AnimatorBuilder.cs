#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Import 183 separate transparent PNGs and generate an AnimatorController.
// All output assets stay inside GraveWarden5B2/Generated; game C# is untouched.
public static class GW5B2AnimatorBuilder
{
    private const string Root = "Assets/MonsterSurvival/Features/Bosses/GraveWarden5B2";
    private const string FramesFolder = Root + "/Frames";
    private const string Output = Root + "/Generated";
    private const float FPS = 12f;

    private static readonly (string name, int count)[] ClipInfo = {
        ("Idle",8), ("Move",8), ("Spawn_Intro",12),
        ("TombWall_Prepare",10), ("TombWall_Summon",8),
        ("Wraith_Prepare",12), ("Wraith_Summon",8),
        ("Reanimate_Cast",14), ("Reanimate_Rise",10),
        ("SpiritOrbit_Cast",10), ("SpiritOrbit_Loop",8),
        ("SoulHarvest_Absorb",10), ("Empowered_Cast",8),
        ("Hit",5), ("Phase2_Transform",18),
        ("Phase2_Idle",8), ("Phase2_Move",8), ("Death",18)
    };

    [MenuItem("Tools/MonsterSurvival/Build Grave Warden Animations 5B.2")]
    public static void Build()
    {
        if (!Directory.Exists(FramesFolder))
        {
            EditorUtility.DisplayDialog("Grave Warden 5B.2", "Khong tim thay folder: " + FramesFolder, "OK");
            return;
        }
        if (!AssetDatabase.IsValidFolder(Output)) AssetDatabase.CreateFolder(Root, "Generated");
        AssetDatabase.Refresh();
        int importedFrames = 0;
        var clips = new Dictionary<string, AnimationClip>();
        try
        {
            for (int c = 0; c < ClipInfo.Length; c++)
            {
                string clipName = ClipInfo[c].name;
                int count = ClipInfo[c].count;
                EditorUtility.DisplayProgressBar("Grave Warden 5B.2", "Import " + clipName, (float)c / ClipInfo.Length);
                ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[count + 1];
                for (int i = 0; i < count; i++)
                {
                    string assetPath = FramesFolder + "/" + clipName + "/" + clipName + "_" + i.ToString("000") + ".png";
                    if (!File.Exists(assetPath)) throw new IOException("Missing frame: " + assetPath);
                    ConfigureSprite(assetPath);
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                    if (sprite == null) throw new IOException("Cannot import sprite: " + assetPath);
                    keys[i] = new ObjectReferenceKeyframe { time = i / FPS, value = sprite };
                    importedFrames++;
                }
                keys[count] = new ObjectReferenceKeyframe { time = count / FPS, value = keys[count-1].value };
                string clipPath = Output + "/GW_" + clipName + ".anim";
                if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null) AssetDatabase.DeleteAsset(clipPath);
                AnimationClip clip = new AnimationClip { name = clipName, frameRate = FPS };
                var binding = new EditorCurveBinding { path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" };
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = clipName == "Idle" || clipName == "Move" || clipName == "Phase2_Idle" ||
                                    clipName == "Phase2_Move" || clipName == "SpiritOrbit_Loop";
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                AssetDatabase.CreateAsset(clip, clipPath);
                clips.Add(clipName, clip);
            }
            CreateController(clips);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Grave Warden 5B.2", 
                "Da tao 18 Animation Clips (" + importedFrames + " frames) va GW_GraveWarden_5B2.controller trong " + Output + ".\n\nGan Animator Controller vao child GW_Visual, sau do keo Animator vao GraveWardenAnimatorBridge5B1.animator.", "OK");
        }
        catch (Exception e)
        {
            Debug.LogError("[GW 5B.2 Builder] " + e);
            EditorUtility.DisplayDialog("Grave Warden 5B.2 ERROR", e.Message, "OK");
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    private static void ConfigureSprite(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) { AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate); importer = AssetImporter.GetAtPath(assetPath) as TextureImporter; }
        if (importer == null) throw new IOException("Not a texture: " + assetPath);
        bool update = importer.textureType != TextureImporterType.Sprite ||
                      importer.spriteImportMode != SpriteImportMode.Single ||
                      importer.spritePixelsPerUnit != 128f || importer.mipmapEnabled ||
                      importer.textureCompression != TextureImporterCompression.Uncompressed;
        if (update)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 128f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }

    private static void CreateController(Dictionary<string, AnimationClip> clips)
    {
        string path = Output + "/GW_GraveWarden_5B2.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsPhaseTwo", AnimatorControllerParameterType.Bool);
        HashSet<string> baseStates = new HashSet<string> { "Idle", "Move", "Phase2_Idle", "Phase2_Move", "SpiritOrbit_Loop" };
        foreach (string name in clips.Keys)
            if (!baseStates.Contains(name)) controller.AddParameter(name, AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        var states = new Dictionary<string, AnimatorState>();
        foreach (var pair in clips)
        {
            var state = sm.AddState(pair.Key);
            state.motion = pair.Value;
            state.writeDefaultValues = true;
            states.Add(pair.Key, state);
        }
        sm.defaultState = states["Idle"];
        AddBoolTransition(states["Idle"], states["Move"], "IsMoving", true);
        AddBoolTransition(states["Move"], states["Idle"], "IsMoving", false);
        AddBoolTransition(states["Idle"], states["Phase2_Idle"], "IsPhaseTwo", true);
        AddBoolTransition(states["Move"], states["Phase2_Move"], "IsPhaseTwo", true);
        AddBoolTransition(states["Phase2_Idle"], states["Phase2_Move"], "IsMoving", true);
        AddBoolTransition(states["Phase2_Move"], states["Phase2_Idle"], "IsMoving", false);

        foreach (var pair in clips)
        {
            string name = pair.Key;
            if (baseStates.Contains(name)) continue;
            AnimatorStateTransition enter = sm.AddAnyStateTransition(states[name]);
            enter.hasExitTime = false;
            enter.duration = 0.02f;
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0, name);
            // Death visual must stay in Death until the cloned visual is destroyed.
            if (name == "Death") continue;
            AddExitTransition(states[name], states["Idle"], false);
            AddExitTransition(states[name], states["Phase2_Idle"], true);
        }
    }

    private static void AddBoolTransition(AnimatorState from, AnimatorState to, string param, bool active)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.03f;
        t.AddCondition(active ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, param);
    }
    private static void AddExitTransition(AnimatorState from, AnimatorState to, bool phase2)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime = 1f;
        t.duration = 0.02f;
        t.AddCondition(phase2 ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "IsPhaseTwo");
    }
}
#endif
