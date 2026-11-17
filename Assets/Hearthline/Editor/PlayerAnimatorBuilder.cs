using Hearthline.Game;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Hearthline.EditorTools
{
    /// <summary>
    /// Builds the player's animator (ADR 0019): a copy of the Starter Assets controller (idle, walk, run, jump),
    /// plus the stances made from Mixamo clips: crouch so far. Generated: change this builder, not the asset.
    /// Without the Mixamo clips (a fresh checkout before they are downloaded) the plain Starter Assets
    /// controller is used and crouching still works, just without its pose.
    /// </summary>
    public static class PlayerAnimatorBuilder
    {
        public const string StarterController = "Assets/ThirdParty/StarterAssets/Runtime/ThirdPersonController/Character/Animations/StarterAssetsThirdPerson.controller";
        public const string Folder = "Assets/Hearthline/Art/Animation";
        public const string Path = Folder + "/Player.controller";

        // Blend thresholds are in the controller's Speed (m/s). The crouched walk sits at the default crouch
        // speed so feet roughly match; Travis tunes both together.
        private const float CrouchWalkSpeed = 1.4f;
        private const float StanceBlend = 0.2f;

        public static RuntimeAnimatorController Build()
        {
            AnimationClip crouchIdle = Mixamo("Crouching Idle");
            AnimationClip crouchWalk = Mixamo("Crouched Walking");
            if (crouchIdle == null || crouchWalk == null)
            {
                Debug.LogWarning("Mixamo crouch clips not found in " + MixamoImport.Folder + "; the player uses the Starter Assets animator (see docs/NEEDS.md).");
                return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(StarterController);
            }

            SceneBuildKit.EnsureFolder(Folder);
            AssetDatabase.DeleteAsset(Path);
            if (!AssetDatabase.CopyAsset(StarterController, Path)) throw new System.InvalidOperationException("Could not copy " + StarterController);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Path);

            controller.AddParameter(Crouch.AnimatorParameter, AnimatorControllerParameterType.Bool);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState locomotion = State(machine, "Idle Walk Run Blend");
            AnimatorState inAir = State(machine, "InAir");

            var tree = new BlendTree
            {
                name = "Crouch Blend",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(crouchIdle, 0f);
            tree.AddChild(crouchWalk, CrouchWalkSpeed);

            AnimatorState crouch = machine.AddState("Crouch", new Vector3(300f, 300f, 0f));
            crouch.motion = tree;
            crouch.speedParameter = "MotionSpeed";
            crouch.speedParameterActive = true;

            AnimatorStateTransition down = locomotion.AddTransition(crouch);
            Instant(down, StanceBlend);
            down.AddCondition(AnimatorConditionMode.If, 0f, Crouch.AnimatorParameter);

            AnimatorStateTransition up = crouch.AddTransition(locomotion);
            Instant(up, StanceBlend);
            up.AddCondition(AnimatorConditionMode.IfNot, 0f, Crouch.AnimatorParameter);

            AnimatorStateTransition fall = crouch.AddTransition(inAir);
            Instant(fall, 0.05f);
            fall.AddCondition(AnimatorConditionMode.If, 0f, "FreeFall");

            AddAimLayer(controller);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        // Combat step 2: an upper-body layer holding the drawn gun (int Weapon: 0 none, 1 long gun, 2 sidearm).
        // PlayerWeapon sets its weight. Stand-in poses until the aim clips in docs/NEEDS.md arrive.
        private static void AddAimLayer(AnimatorController controller)
        {
            AnimationClip rifle = Mixamo("Rifle Idle"), pistol = Mixamo("Pistol Aim");
            if (rifle == null && pistol == null) return;
            controller.AddParameter("Weapon", AnimatorControllerParameterType.Int);
            controller.AddLayer("Aim");
            AnimatorControllerLayer[] layers = controller.layers;
            AnimatorControllerLayer aim = layers[layers.Length - 1];
            aim.avatarMask = UpperBodyMask();
            aim.defaultWeight = 0f;
            layers[layers.Length - 1] = aim;
            controller.layers = layers;

            AnimatorStateMachine machine = aim.stateMachine;
            AnimatorState empty = machine.AddState("Empty", new Vector3(300f, 0f, 0f));
            machine.defaultState = empty;
            AnimatorState longGun = machine.AddState("LongGun", new Vector3(300f, 100f, 0f));
            longGun.motion = rifle ?? pistol;
            AnimatorState sidearm = machine.AddState("Sidearm", new Vector3(300f, 200f, 0f));
            sidearm.motion = pistol ?? rifle;
            void To(AnimatorState state, int weapon)
            {
                AnimatorStateTransition t = machine.AddAnyStateTransition(state);
                Instant(t, 0.15f);
                t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.Equals, weapon, "Weapon");
            }
            To(empty, 0);
            To(longGun, 1);
            To(sidearm, 2);
        }

        private static AvatarMask UpperBodyMask()
        {
            string path = Folder + "/UpperBody.mask";
            AssetDatabase.DeleteAsset(path);
            var mask = new AvatarMask();
            var upper = new[]
            {
                AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm,
                AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers, AvatarMaskBodyPart.LeftHandIK, AvatarMaskBodyPart.RightHandIK,
            };
            for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
                mask.SetHumanoidBodyPartActive(part, System.Array.IndexOf(upper, part) >= 0);
            AssetDatabase.CreateAsset(mask, path);
            return mask;
        }

        private static AnimationClip Mixamo(string name)
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(MixamoImport.Folder + name + ".fbx"))
                if (o is AnimationClip clip && !clip.name.StartsWith("__preview")) return clip;
            return null;
        }

        private static AnimatorState State(AnimatorStateMachine machine, string name)
        {
            foreach (ChildAnimatorState child in machine.states)
                if (child.state.name == name) return child.state;
            throw new System.InvalidOperationException("The Starter Assets animator has no state " + name);
        }

        private static void Instant(AnimatorStateTransition t, float duration)
        {
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = duration;
        }
    }
}
