using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Bouncer.EditorTools
{
    /// <summary>
    /// Собирает аниматоры детей из клипов Anim_Kids.fbx (ba_kids_anim.py в Bouncer.blend):
    /// AC_Kid — в игре: слой тела (бег по направлению — Idle/Run_F/B/L/R, рывок, подкат, выбывание, радость)
    /// и слой рук и корпуса по маске AM_KidUpperBody (замах, бросок, ловля, удар); AC_KidSelect — позы экрана выбора.
    /// Пересобирает на месте: GUID не меняются, ссылки префабов не рвутся. Параметры задаёт KidAnimator.
    /// </summary>
    static class KidAnimatorBuilder
    {
        const string ClipsPath = "Assets/_Project/Art/Models/Kids/Anim_Kids.fbx";
        const string Folder = "Assets/_Project/Art/Animation";
        const string GamePath = Folder + "/AC_Kid.controller";
        const string SelectPath = Folder + "/AC_KidSelect.controller";
        const string UpperMaskPath = Folder + "/AM_KidUpperBody.mask";

        const AnimatorConditionMode On = AnimatorConditionMode.If;
        const AnimatorConditionMode Off = AnimatorConditionMode.IfNot;

        [MenuItem("Bouncer/Build Kid Animators")]
        public static void Build()
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(ClipsPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview")).ToDictionary(c => c.name);
            Directory.CreateDirectory(Folder);
            var mask = UpperBodyMask();
            BuildGame(clips, mask);
            BuildSelect(clips);
            AssetDatabase.SaveAssets();
            Debug.Log($"[KidAnimatorBuilder] {GamePath}, {SelectPath} from {clips.Count} clips");
        }

        static AvatarMask UpperBodyMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, UpperMaskPath);
            }
            for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
                mask.SetHumanoidBodyPartActive(part, part is AvatarMaskBodyPart.Body or AvatarMaskBodyPart.Head
                    or AvatarMaskBodyPart.LeftArm or AvatarMaskBodyPart.RightArm
                    or AvatarMaskBodyPart.LeftFingers or AvatarMaskBodyPart.RightFingers);
            EditorUtility.SetDirty(mask);
            return mask;
        }

        static void BuildGame(Dictionary<string, AnimationClip> clips, AvatarMask mask)
        {
            var c = Controller(GamePath);
            foreach (var name in new[] { "MoveX", "MoveZ", "RunSpeed", "Charge" })
                c.AddParameter(name, AnimatorControllerParameterType.Float);
            foreach (var name in new[] { "Dashing", "Sliding", "Down", "Cheer", "Charging", "Catching" })
                c.AddParameter(name, AnimatorControllerParameterType.Bool);
            foreach (var name in new[] { "Throw", "Caught", "Hurt" })
                c.AddParameter(name, AnimatorControllerParameterType.Trigger);
            var parameters = c.parameters;
            parameters.First(p => p.name == "RunSpeed").defaultFloat = 1f;
            c.parameters = parameters;

            // ---- тело
            var body = c.layers[0].stateMachine;
            var move = c.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.FreeformDirectional2D;
            tree.blendParameter = "MoveX";
            tree.blendParameterY = "MoveZ";
            tree.AddChild(clips["Idle"], Vector2.zero);
            tree.AddChild(clips["Run_F"], new Vector2(0f, 1f));
            tree.AddChild(clips["Run_B"], new Vector2(0f, -1f));
            tree.AddChild(clips["Run_L"], new Vector2(-1f, 0f));
            tree.AddChild(clips["Run_R"], new Vector2(1f, 0f));
            move.speedParameterActive = true;
            move.speedParameter = "RunSpeed";
            body.defaultState = move;
            var dash = State(body, "Dash", clips);
            var slide = State(body, "Slide", clips);
            var down = State(body, "Down", clips);
            var cheer = State(body, "Cheer", clips);
            FromAny(body, down, 0.12f, ("Down", On));
            FromAny(body, slide, 0.05f, ("Sliding", On), ("Down", Off));
            FromAny(body, dash, 0.04f, ("Dashing", On), ("Down", Off));
            To(down, move, 0.2f, ("Down", Off));
            To(slide, move, 0.12f, ("Sliding", Off));
            To(dash, move, 0.1f, ("Dashing", Off));
            To(move, cheer, 0.2f, ("Cheer", On));
            To(cheer, move, 0.2f, ("Cheer", Off));

            // ---- руки и корпус поверх бега
            c.AddLayer("UpperBody");
            var layers = c.layers;
            layers[1].avatarMask = mask;
            layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
            layers[1].defaultWeight = 1f;
            c.layers = layers;
            var upper = c.layers[1].stateMachine;
            var empty = upper.AddState("Empty");
            upper.defaultState = empty;
            var charge = c.CreateBlendTreeInController("Charge", out var chargeTree, 1);
            chargeTree.blendType = BlendTreeType.Simple1D;
            chargeTree.blendParameter = "Charge";
            chargeTree.useAutomaticThresholds = false;
            chargeTree.AddChild(clips["Charge_Start"], 0f);
            chargeTree.AddChild(clips["Charge_Full"], 1f);
            var toss = State(upper, "Throw", clips);
            var reach = State(upper, "Catch", clips);
            var hug = State(upper, "Caught", clips);
            var hurt = State(upper, "Hurt", clips);
            FromAny(upper, hurt, 0.03f, ("Hurt", On));
            FromAny(upper, toss, 0.02f, ("Throw", On));
            FromAny(upper, hug, 0.03f, ("Caught", On));
            To(empty, charge, 0.08f, ("Charging", On));
            To(charge, empty, 0.12f, ("Charging", Off));
            To(empty, reach, 0.04f, ("Catching", On));
            To(reach, empty, 0.1f, ("Catching", Off));
            To(toss, charge, 0.08f, ("Charging", On));
            To(toss, reach, 0.05f, ("Catching", On));
            AfterClip(toss, empty, 0.12f);
            AfterClip(hug, empty, 0.1f);
            AfterClip(hurt, empty, 0.1f);
            EditorUtility.SetDirty(c);
        }

        static void BuildSelect(Dictionary<string, AnimationClip> clips)
        {
            var c = Controller(SelectPath);
            var sm = c.layers[0].stateMachine;
            sm.defaultState = State(sm, "Idle", clips);
            foreach (var pose in new[] { "Pose_Think", "Pose_Cheer", "Pose_Ready", "Pose_Tough", "Cheer" })
                State(sm, pose, clips);
            EditorUtility.SetDirty(c);
        }

        /// <summary>Контроллер по пути: новый или прежний, очищенный от слоёв, состояний и параметров.</summary>
        static AnimatorController Controller(string path)
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (c == null)
                return AnimatorController.CreateAnimatorControllerAtPath(path);
            foreach (var p in c.parameters)
                c.RemoveParameter(p);
            while (c.layers.Length > 1)
                c.RemoveLayer(c.layers.Length - 1);
            var sm = c.layers[0].stateMachine;
            foreach (var t in sm.anyStateTransitions)
                sm.RemoveAnyStateTransition(t);
            foreach (var s in sm.states)
                sm.RemoveState(s.state);
            foreach (var s in sm.stateMachines)
                sm.RemoveStateMachine(s.stateMachine);
            // Деревья смешивания и машины состояний удалённых слоёв остаются в файле — убрать хвосты.
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is BlendTree || (o is AnimatorStateMachine m && m != sm))
                    Object.DestroyImmediate(o, true);
            return c;
        }

        static AnimatorState State(AnimatorStateMachine sm, string clip, Dictionary<string, AnimationClip> clips)
        {
            var state = sm.AddState(clip);
            state.motion = clips[clip];
            return state;
        }

        static void FromAny(AnimatorStateMachine sm, AnimatorState to, float duration, params (string, AnimatorConditionMode)[] when)
        {
            var t = sm.AddAnyStateTransition(to);
            t.canTransitionToSelf = false;
            Setup(t, duration, when);
        }

        static void To(AnimatorState from, AnimatorState to, float duration, params (string, AnimatorConditionMode)[] when)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            Setup(t, duration, when);
        }

        /// <summary>Разовый клип доигрывает и уходит.</summary>
        static void AfterClip(AnimatorState from, AnimatorState to, float duration)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = 0.9f;
            t.duration = duration;
            t.hasFixedDuration = true;
        }

        static void Setup(AnimatorStateTransition t, float duration, (string, AnimatorConditionMode)[] when)
        {
            t.duration = duration;
            t.hasFixedDuration = true;
            t.exitTime = 0f;
            foreach (var (parameter, mode) in when)
                t.AddCondition(mode, 0f, parameter);
        }
    }
}
