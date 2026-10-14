using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Bouncer.EditorTools
{
    // Вариант B тумана (2026-10-14): полноэкранный проход «Bouncer/VolumetricFog» в URP_Renderer.
    // Меню Bouncer → Art → Build Volumetric Fog. Добавляет материал и фичу «VolumetricFog» ВКЛЮЧЁННОЙ (с 2026-10-14 вариант B постоянный;
    // WeatherController ставит _Bouncer_VolFog = 1). Выключить: снять галку у фичи и убрать SetGlobalFloat(VolFogId).
    static class VolumetricFogBuilder
    {
        const string RendererPath = "Assets/_Project/Settings/Rendering/URP_Renderer.asset";
        const string MaterialPath = "Assets/_Project/Art/Materials/Fx/M_VolumetricFog.mat";

        [MenuItem("Bouncer/Art/Build Volumetric Fog")]
        public static void Build()
        {
            var shader = Shader.Find("Hidden/Bouncer/VolumetricFog");
            if (shader == null) { Debug.LogError("[Fog] Нет шейдера Hidden/Bouncer/VolumetricFog"); return; }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "M_VolumetricFog" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
                material.shader = shader;

            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            FullScreenPassRendererFeature feature = null;
            foreach (var f in data.rendererFeatures)
                if (f is FullScreenPassRendererFeature fs && fs.name == "VolumetricFog")
                    feature = fs;
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = "VolumetricFog";
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                var so = new SerializedObject(data);
                var features = so.FindProperty("m_RendererFeatures");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                var map = so.FindProperty("m_RendererFeatureMap");
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            material.SetFloat("_Sigma", 1.0f);
            material.SetFloat("_Steps", 32f);
            EditorUtility.SetDirty(material);
            feature.passMaterial = material;
            feature.passIndex = 0;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingTransparents;
            feature.fetchColorBuffer = true;
            feature.requirements = ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log("[Fog] Вариант B добавлен в URP_Renderer (включён).");
        }
    }
}
