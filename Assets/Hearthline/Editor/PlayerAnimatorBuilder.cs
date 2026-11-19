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
        // The Starter Assets walk speed, where the lowered rifle walk is fully blended in (m/s).
        private const float WalkSpeed = 2f;

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

            AddHighCover(controller, machine, locomotion, crouch);
            AddAimLayer(controller);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        // Back to the wall behind high cover (PlayerCover: int Cover 1, float CoverSlide -1 left .. 1 right).
        // Leaning out to aim sets Cover 0, so the legs go back to standing and the aim layer does the arms.
        private static void AddHighCover(AnimatorController controller, AnimatorStateMachine machine, AnimatorState locomotion, AnimatorState crouch)
        {
            AnimationClip idle = Mixamo("Taking Cover Idle"), left = Mixamo("Left Cover Sneak"), right = Mixamo("Right Cover Sneak");
            if (idle == null) return;
            controller.AddParameter(PlayerCover.CoverParameter, AnimatorControllerParameterType.Int);
            controller.AddParameter(PlayerCover.SlideParameter, AnimatorControllerParameterType.Float);
            var tree = new BlendTree
            {
                name = "High Cover Blend",
                blendType = BlendTreeType.Simple1D,
                blendParameter = PlayerCover.SlideParameter,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            if (left != null) tree.AddChild(left, -1f);
            tree.AddChild(idle, 0f);
            if (right != null) tree.AddChild(right, 1f);
            AnimatorState cover = machine.AddState("High Cover", new Vector3(300f, 400f, 0f));
            cover.motion = tree;
            foreach (AnimatorState from in new[] { locomotion, crouch })
            {
                AnimatorStateTransition into = from.AddTransition(cover);
                Instant(into, StanceBlend);
                into.AddCondition(AnimatorConditionMode.Equals, 1f, PlayerCover.CoverParameter);
            }
            AnimatorStateTransition stand = cover.AddTransition(locomotion);
            Instant(stand, StanceBlend);
            stand.AddCondition(AnimatorConditionMode.NotEqual, 1f, PlayerCover.CoverParameter);
            stand.AddCondition(AnimatorConditionMode.IfNot, 0f, Crouch.AnimatorParameter);
            AnimatorStateTransition duck = cover.AddTransition(crouch);
            Instant(duck, StanceBlend);
            duck.AddCondition(AnimatorConditionMode.NotEqual, 1f, PlayerCover.CoverParameter);
            duck.AddCondition(AnimatorConditionMode.If, 0f, Crouch.AnimatorParameter);
        }

        // Combat step 2: an upper-body layer holding the drawn gun (int Weapon: 0 none, 1 long gun, 2 sidearm).
        // PlayerWeapon sets its weight. A long gun is held low at the ready (Rifle Idle, swinging with the lowered
        // rifle walk) and shouldered while aiming (Rifle Aiming Idle). The pistol is still a stand-in pose.
        private static void AddAimLayer(AnimatorController controller)
        {
            AnimationClip rifle = Mixamo("Rifle Idle"), pistol = Mixamo("Pistol Aim");
            AnimationClip rifleAim = Mixamo("Rifle Aiming Idle"), rifleWalk = Mixamo("Walk With Rifle lowered");
            if (rifle == null && pistol == null) return;
            controller.AddParameter("Weapon", AnimatorControllerParameterType.Int);
            controller.AddParameter(PlayerWeapon.AimingParameter, AnimatorControllerParameterType.Bool);
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
            if (rifle != null && rifleWalk != null)
            {
                var ready = new BlendTree
                {
                    name = "Rifle Ready Blend",
                    blendType = BlendTreeType.Simple1D,
                    blendParameter = "Speed",
                    useAutomaticThresholds = false,
                    hideFlags = HideFlags.HideInHierarchy,
                };
                AssetDatabase.AddObjectToAsset(ready, controller);
                ready.AddChild(rifle, 0f);
                ready.AddChild(rifleWalk, WalkSpeed);
                longGun.motion = ready;
            }
            AnimatorState longGunAim = machine.AddState("LongGun Aim", new Vector3(500f, 100f, 0f));
            longGunAim.motion = rifleAim != null ? rifleAim : longGun.motion;
            AnimatorState sidearm = machine.AddState("Sidearm", new Vector3(300f, 200f, 0f));
            sidearm.motion = pistol ?? rifle;
            void To(AnimatorState state, int weapon, bool? aiming = null)
            {
                AnimatorStateTransition t = machine.AddAnyStateTransition(state);
                Instant(t, 0.15f);
                t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.Equals, weapon, "Weapon");
                if (aiming.HasValue) t.AddCondition(aiming.Value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, PlayerWeapon.AimingParameter);
            }
            To(empty, 0);
            To(longGun, 1, false);
            To(longGunAim, 1, true);
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
            string file = MixamoImport.FileFor(name);
            if (file == null) return null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(file))
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
