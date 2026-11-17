using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hearthline.EditorTools
{
    /// <summary>
    /// Import settings for the Mixamo clips in Assets/ThirdParty/Mixamo (ADR 0019). The files themselves are
    /// git-ignored and untouched; this rule sets how Unity reads them, so a fresh checkout gets the same result:
    /// Humanoid rig (so they play on any humanoid, the Starter Assets mannequin today), root motion baked into
    /// the pose (the controller moves the character, not the clip), and looping for idles and walks.
    /// Skeleton-only Mixamo files rest in their first frame, not a T-pose, and Unity maps the body from the rest
    /// pose; a crouch mapped from a crouch comes out bent. So every clip borrows the avatar of Mixamo's "T-Pose"
    /// clip (T-Pose.fbx in the same folder) once it is there; until then each builds its own, roughly.
    /// </summary>
    public sealed class MixamoImport : AssetPostprocessor
    {
        public const string Folder = "Assets/ThirdParty/Mixamo/";

        // Raise when the rules below change, so Unity reimports the clips.
        public override uint GetVersion() => 3;

        public const string TPoseFile = Folder + "T-Pose.fbx";

        private bool IsMixamo => assetPath.StartsWith(Folder, StringComparison.OrdinalIgnoreCase);

        private void OnPreprocessModel()
        {
            if (!IsMixamo) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            Avatar tPose = IsTPose(assetPath) ? null : TPoseAvatar();
            if (tPose != null)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = tPose;
            }
            else
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.sourceAvatar = null;
            }
            importer.importAnimation = true;
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }

        private void OnPreprocessAnimation()
        {
            if (!IsMixamo) return;
            var importer = (ModelImporter)assetImporter;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool loop = Loops(name);
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.name = name;
                clip.loopTime = loop;
                clip.loopPose = loop;
                // Keep the character in place: the controller does the moving.
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = true;
                clip.lockRootHeightY = true;
                clip.keepOriginalPositionY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
        }

        // When the T-pose arrives (or changes), reimport the other clips so they pick up its avatar.
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool tPoseChanged = false;
            foreach (string path in imported) tPoseChanged |= IsTPose(path);
            foreach (string path in deleted) tPoseChanged |= IsTPose(path);
            if (!tPoseChanged) return;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Folder.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsTPose(path)) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        private static bool IsTPose(string path) => string.Equals(path, TPoseFile, StringComparison.OrdinalIgnoreCase);

        public static Avatar TPoseAvatar()
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(TPoseFile))
                if (o is Avatar avatar && avatar.isValid && avatar.isHuman) return avatar;
            return null;
        }

        /// <summary>Idles, walks, runs and aims loop; one-shots (transitions, turns, gathering) do not.</summary>
        public static bool Loops(string clipName)
        {
            string n = clipName.ToLowerInvariant();
            if (n.Contains(" to ") || n.Contains("turn") || n.Contains("put back") || n.Contains("gather") || n == "crouching") return false;
            return n.Contains("idle") || n.Contains("walk") || n.Contains("run") || n.Contains("aim") || n.Contains("strafe");
        }
    }
}
