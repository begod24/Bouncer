using Bouncer.Core;
using UnityEditor;
using UnityEngine;

namespace Bouncer.EditorTools
{
    // «Настоящая жвачка» 2026-10-12: материалы тела, края, блика и ниток, пузыри и брызги на префабе GumSpot.
    // Меню Bouncer → Art → Build Gum VFX. Перезапускаемый.
    static class GumFxBuilder
    {
        const string Root = "Assets/_Project/";
        const string MaterialDir = Root + "Art/Materials/Cards/";
        const string SpotPath = Root + "Prefabs/VFX/GumSpot.prefab";

        static readonly int TintColor = Shader.PropertyToID("_TintColor");
        static readonly int FlashColor = Shader.PropertyToID("_FlashColor");

        [MenuItem("Bouncer/Art/Build Gum VFX")]
        public static void Build()
        {
            var palette = Load<Material>(Root + "Art/Materials/M_Palette.mat");
            // 2026-10-14: жвачка полупрозрачная и без записи глубины, очередь 2980–2982 (до Telegraph = 2990),
            // поэтому кольца, маркер падения и линии на земле всегда рисуются поверх неё
            var body = Palette(palette, "M_Gum_Body", new Color(0.97f, 0.47f, 0.73f), 0f, 0.8f, 2980);
            var rim = Palette(palette, "M_Gum_Rim", new Color(0.84f, 0.29f, 0.57f), 0f, 0.85f, 2981);
            // блик белый при любом свете, но не светится в темноте как лампочка
            var gloss = Palette(palette, "M_Gum_Gloss", Color.white, 0.75f, 0.9f, 2982);

            // пузырь на пятне: плотнее и светлее, чем пузыри умения, иначе розовый на розовом не виден
            var bubble = new Material(Shader.Find("Bouncer/Bubble"));
            bubble.SetColor("_BaseColor", new Color(1f, 0.8f, 0.92f, 1f));
            bubble.SetFloat("_CoreAlpha", 0.55f);
            bubble.SetFloat("_RimAlpha", 1f);
            bubble.SetFloat("_RimPower", 1.6f);
            bubble.SetFloat("_Iridescence", 0.1f);
            bubble.SetFloat("_Highlight", 1.6f);
            bubble = Save(bubble, "M_Gum_SpotBubble");

            var strand = new Material(Load<Material>(Root + "Art/Materials/M_Line.mat")) { color = new Color(1f, 0.66f, 0.86f, 1f) };
            strand = Save(strand, "M_Gum_Strand");

            var root = PrefabUtility.LoadPrefabContents(SpotPath);
            var renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { body, rim, gloss };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var spot = root.GetComponent<GumSpot>();
            var so = new SerializedObject(spot);
            so.FindProperty("bubbleMaterial").objectReferenceValue = bubble;
            so.FindProperty("strandMaterial").objectReferenceValue = strand;
            so.FindProperty("bubbleSize").vector2Value = new Vector2(0.26f, 0.4f);
            so.FindProperty("strandWidth").floatValue = 0.1f;
            so.FindProperty("popBurst").objectReferenceValue =
                Load<GameObject>(Root + "Prefabs/Cards/GumPopBurst.prefab").GetComponent<ParticleBurst>();
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, SpotPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            Debug.Log("[Gum] Жвачка: глянцевые пятна, пузыри, нитки и «чпок» готовы.");
        }

        static Material Palette(Material template, string name, Color tint, float flash, float opacity, int queue)
        {
            var material = new Material(template);
            material.SetColor("_BaseColor", new Color(1f, 1f, 1f, opacity));
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = queue;
            material.SetColor(TintColor, new Color(tint.r, tint.g, tint.b, 1f));
            material.SetColor(FlashColor, new Color(1f, 1f, 1f, flash));
            return Save(material, name);
        }

        static Material Save(Material material, string name)
        {
            string path = MaterialDir + name + ".mat";
            material.name = name;
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(material, path);
                return material;
            }
            existing.shader = material.shader;
            existing.CopyPropertiesFromMaterial(material);
            existing.renderQueue = material.renderQueue;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                Debug.LogError("[Gum] Нет ассета " + path);
            return asset;
        }
    }
}
