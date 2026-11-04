#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Visual-only Grave Warden installer. Reads 207 independent RGBA frames (18 compatible clips + 2 optional).
/// Does NOT edit any gameplay C# class or project scene.
/// Builds in a UNIQUE folder/controller, leaving old 5B.2 and previous PurpleGold packs intact.
/// </summary>
public static class GWReaperPurpleGoldActionV3Installer
{
    private const string Root = "Assets/MonsterSurvival/Features/Bosses/GWReaperPurpleGoldActionV3";
    private const string Frames = Root + "/Frames";
    private const string Generated = Root + "/Generated";
    private const string ControllerPath = Generated + "/GW_GraveWarden_PurpleGoldActionV3.controller";
    private const float Fps = 12f;
    private const float Ppu = 256f; // 512px sprites -> 2 Unity units, same as 256px/128PPU in original 5B2.

    private struct ClipSpec
    {
        public string name;
        public int count;
        public bool loop;
        public ClipSpec(string name, int count, bool loop) { this.name = name; this.count = count; this.loop = loop; }
    }

    private static readonly ClipSpec[] Clips =
    {
        new ClipSpec("Idle",8,true), new ClipSpec("Move",8,true),
        new ClipSpec("Spawn_Intro",12,false), new ClipSpec("TombWall_Prepare",10,false),
        new ClipSpec("TombWall_Summon",8,false),new ClipSpec("Wraith_Prepare",12,false),
        new ClipSpec("Wraith_Summon",8,false),new ClipSpec("Reanimate_Cast",14,false),
        new ClipSpec("Reanimate_Rise",10,false),new ClipSpec("SpiritOrbit_Cast",10,false),
        new ClipSpec("SpiritOrbit_Loop",8,true),new ClipSpec("SoulHarvest_Absorb",10,false),
        new ClipSpec("Empowered_Cast",8,false),new ClipSpec("Hit",5,false),
        new ClipSpec("Phase2_Transform",18,false),new ClipSpec("Phase2_Idle",8,true),
        new ClipSpec("Phase2_Move",8,true),new ClipSpec("Death",18,false),
        new ClipSpec("Scythe_Slash",12,false), new ClipSpec("Scythe_Overhead",12,false)
    };

    [MenuItem("Tools/MonsterSurvival/Grave Warden Action V3/1 - Build 20 Animations")]
    public static void Build()
    {
        if (!Directory.Exists(Frames))
        {
            EditorUtility.DisplayDialog("Action V3", "Chua import Frames. Kiem tra " + Frames, "OK");
            return;
        }
        if (!AssetDatabase.IsValidFolder(Generated))
            AssetDatabase.CreateFolder(Root, "Generated");
        var built = new Dictionary<string,AnimationClip>();
        var count = 0;
        try
        {
            for (int c=0; c<Clips.Length; c++)
            {
                ClipSpec spec = Clips[c];
                EditorUtility.DisplayProgressBar("Grave Warden Action V3", spec.name, (float)c/Clips.Length);
                var frames = new ObjectReferenceKeyframe[spec.count + 1];
                for (int i=0;i<spec.count;i++)
                {
                    string path = Frames + "/" + spec.name + "/" + spec.name + "_" + i.ToString("000") + ".png";
                    if (!File.Exists(path)) throw new IOException("Thieu anh " + path);
                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) throw new IOException("Khong the import PNG: " + path);
                    bool dirty = importer.textureType != TextureImporterType.Sprite ||
                                 importer.spriteImportMode != SpriteImportMode.Single ||
                                 importer.spritePixelsPerUnit != Ppu ||
                                 importer.mipmapEnabled ||
                                 importer.textureCompression != TextureImporterCompression.Uncompressed ||
                                 importer.filterMode != FilterMode.Bilinear;
                    if (dirty)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        importer.spriteImportMode = SpriteImportMode.Single;
                        importer.spritePixelsPerUnit = Ppu;
                        importer.mipmapEnabled = false;
                        importer.alphaIsTransparency = true;
                        importer.filterMode = FilterMode.Bilinear;
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        importer.SaveAndReimport();
                    }
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite==null) throw new IOException("Khong thay Sprite: " + path);
                    frames[i] = new ObjectReferenceKeyframe { time = i / Fps, value = sprite };
                    count++;
                }
                frames[spec.count] = new ObjectReferenceKeyframe {
                    time = spec.count / Fps,
                    value = frames[spec.count-1].value
                };
                string clipPath = Generated + "/GW_ActionV3_" + spec.name + ".anim";
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip==null)
                {
                    clip = new AnimationClip { name=spec.name, frameRate=Fps };
                    AssetDatabase.CreateAsset(clip, clipPath);
                }
                clip.name = spec.name;
                clip.frameRate = Fps;
                AnimationUtility.SetObjectReferenceCurve(clip,
                    new EditorCurveBinding { path="", type=typeof(SpriteRenderer), propertyName="m_Sprite" },
                    frames);
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = spec.loop;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip);
                built[spec.name] = clip;
            }
            // Only replace our *own* generated controller, never old 5B2/Dark 5B5 controllers.
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)!=null)
                AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("IsMoving",AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsPhaseTwo",AnimatorControllerParameterType.Bool);
            foreach (ClipSpec spec in Clips)
                if (!IsBaseState(spec.name)) controller.AddParameter(spec.name,AnimatorControllerParameterType.Trigger);
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            var states = new Dictionary<string, AnimatorState>();
            foreach (ClipSpec spec in Clips)
            {
                AnimatorState state = sm.AddState(spec.name);
                state.motion = built[spec.name];
                state.writeDefaultValues = true;
                states.Add(spec.name,state);
            }
            sm.defaultState = states["Idle"];
            Transition(states["Idle"],states["Move"],"IsMoving",true);
            Transition(states["Move"],states["Idle"],"IsMoving",false);
            Transition(states["Idle"],states["Phase2_Idle"],"IsPhaseTwo",true);
            Transition(states["Move"],states["Phase2_Move"],"IsPhaseTwo",true);
            Transition(states["Phase2_Idle"],states["Phase2_Move"],"IsMoving",true);
            Transition(states["Phase2_Move"],states["Phase2_Idle"],"IsMoving",false);
            Transition(states["Phase2_Idle"],states["Idle"],"IsPhaseTwo",false);
            Transition(states["Phase2_Move"],states["Move"],"IsPhaseTwo",false);
            foreach (ClipSpec spec in Clips)
            {
                string name=spec.name;
                if (IsBaseState(name))continue;
                AnimatorStateTransition entrance = sm.AddAnyStateTransition(states[name]);
                entrance.hasExitTime=false; entrance.duration=0.02f;entrance.canTransitionToSelf=false;
                entrance.AddCondition(AnimatorConditionMode.If,0f,name);
                if (name=="Death")continue;
                Exit(states[name],states["Idle"],false);
                Exit(states[name],states["Phase2_Idle"],true);
            }
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Action V3 - Thanh cong",
                "Da build " + count + " PNG / 20 clip va Controller moi.\n" + ControllerPath +
                "\n\nTiep theo: Chon Prefab GraveWardenBoss trong PROJECT va chay menu 2.", "OK");
        }
        catch(Exception error)
        {
            Debug.LogError("[Action V3] Build: " + error);
            EditorUtility.DisplayDialog("Action V3 - LOI",error.Message,"OK");
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    private static bool IsBaseState(string s) =>
        s=="Idle" || s=="Move" || s=="Phase2_Idle" || s=="Phase2_Move" ;

    private static void Transition(AnimatorState from,AnimatorState to,string parameter,bool value)
    {
        AnimatorStateTransition t=from.AddTransition(to);
        t.hasExitTime=false;t.duration=0.03f;
        t.AddCondition(value?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0f,parameter);
    }
    private static void Exit(AnimatorState from,AnimatorState to,bool phase2)
    {
        AnimatorStateTransition t=from.AddTransition(to);
        t.hasExitTime=true;t.exitTime=1f;t.duration=0.02f;
        t.AddCondition(phase2?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0f,"IsPhaseTwo");
    }

    [MenuItem("Tools/MonsterSurvival/Grave Warden Action V3/2 - Apply to SELECTED Boss Prefab")]
    public static void Apply()
    {
        string path=AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrEmpty(path)||!path.EndsWith("GraveWardenBoss.prefab",StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("Action V3", "Click 1 lan vao Assets/MonsterSurvival/Content/Prefabs/GraveWardenBoss.prefab trong cua so Project (KHONG chon Boss trong Hierarchy).", "OK");
            return;
        }
        RuntimeAnimatorController controller=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if(controller==null)
        {
            EditorUtility.DisplayDialog("Action V3", "Chua co Controller. Chay 1 - Build 20 Animations truoc.", "OK");return;
        }
        if(!EditorUtility.DisplayDialog("Xac nhan - backup truoc",
            "Da sao luu GraveWardenBoss.prefab va controller cu?\n\nChi thay sprite/controller tren GW_Visual; AI/HP/ky nang khong doi.",
            "Da sao luu - Apply","Huy"))return;
        GameObject root=null;
        try
        {
            root=PrefabUtility.LoadPrefabContents(path);
            Transform vis=root.transform.Find("GW_Visual");
            if(vis==null)
            {
                GameObject go=new GameObject("GW_Visual",typeof(SpriteRenderer),typeof(Animator));
                go.transform.SetParent(root.transform,false);vis=go.transform;
                vis.localPosition=Vector3.zero;vis.localRotation=Quaternion.identity;vis.localScale=Vector3.one;
            }
            SpriteRenderer sr=vis.GetComponent<SpriteRenderer>();
            if(sr==null)sr=vis.gameObject.AddComponent<SpriteRenderer>();
            Animator an=vis.GetComponent<Animator>();
            if(an==null)an=vis.gameObject.AddComponent<Animator>();
            an.runtimeAnimatorController=controller;
            an.applyRootMotion=false;
            Sprite init=AssetDatabase.LoadAssetAtPath<Sprite>(Frames+"/Idle/Idle_000.png");
            if(init!=null)sr.sprite=init;
            // Keep it above the existing root sprite; do not toggle/disable gameplay root objects.
            SpriteRenderer oldSr=root.GetComponent<SpriteRenderer>();
            if(oldSr!=null && oldSr.sortingLayerID==sr.sortingLayerID && sr.sortingOrder<=oldSr.sortingOrder)
                sr.sortingOrder=oldSr.sortingOrder+1;
            bool connected=false;
            foreach(MonoBehaviour mb in root.GetComponents<MonoBehaviour>())
            {
                if(mb==null||mb.GetType().Name!="GraveWardenAnimatorBridge5B1")continue;
                SerializedObject obj=new SerializedObject(mb);
                SerializedProperty prop=obj.FindProperty("animator");
                if(prop!=null && prop.propertyType==SerializedPropertyType.ObjectReference)
                {
                    prop.objectReferenceValue=an;obj.ApplyModifiedPropertiesWithoutUndo();connected=true;
                }
            }
            PrefabUtility.SaveAsPrefabAsset(root,path);
            EditorUtility.DisplayDialog("Action V3 - Da gan",
                "Controller moi da gan vao GW_Visual. " + (connected?"AnimatorBridge5B1 ket noi OK.":"CANH BAO: keo thu cong GW_Visual/Animator vao AnimatorBridge5B1.animator.")+
                "\n\nNeu sprite TankEnemy cu dang hien: tat checkbox chi component SpriteRenderer o ROOT. KHONG tat GameObject Boss.","OK");
        }
        catch(Exception e)
        {
            Debug.LogError("[Action V3] Apply: "+e);
            EditorUtility.DisplayDialog("Action V3 - LOI",e.Message,"OK");
        }
        finally {if(root!=null)PrefabUtility.UnloadPrefabContents(root);}
    }
}
#endif
