using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Hearthline.EditorTools
{
    /// <summary>
    /// Render setup for the look pass (ADR 0028): the height fog and light shaft pass on the PC renderer.
    /// Idempotent: run it as often as you like.
    /// </summary>
    public static class LookKit
    {
        public const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        public const string FogMaterialPath = "Assets/Hearthline/Art/Shaders/HeightFog.mat";
        private const string FeatureName = "Hearthline Height Fog";

        public static string InstallHeightFog()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (data == null) return "No renderer at " + RendererPath;

            var material = AssetDatabase.LoadAssetAtPath<Material>(FogMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Hearthline/HeightFog"));
                AssetDatabase.CreateAsset(material, FogMaterialPath);
            }

            var feature = data.rendererFeatures.OfType<FullScreenPassRendererFeature>().FirstOrDefault(f => f.name == FeatureName);
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = FeatureName;
                AssetDatabase.AddObjectToAsset(feature, data);
                var so = new SerializedObject(data);
                SerializedProperty list = so.FindProperty("m_RendererFeatures");
                SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedProperties();
            }
            feature.passMaterial = material;
            feature.passIndex = 0;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.requirements = ScriptableRenderPassInput.Depth;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return "Height fog pass installed on " + RendererPath;
        }
    }
}
