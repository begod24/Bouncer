using System.Collections.Generic;
using System.IO;
using Bouncer.Core;
using Bouncer.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Bouncer.EditorTools
{
    // Карточки 2026-10: эффекты, мячи, умения, карточки, колода, звуки, строки, игрок и HUD.
    // Меню Bouncer → Cards → Build Card Content. Повторный запуск пересобирает всё на тех же путях.
    static partial class CardContentBuilder
    {
        const string Root = "Assets/_Project/";
        const string PrefabDir = Root + "Prefabs/Cards/";
        const string MaterialDir = Root + "Art/Materials/Cards/";
        const string VfxDir = Root + "Art/VFX/";
        const string ModelDir = Root + "Art/Models/AbilityProps/";
        const string LinePath = Root + "Art/Materials/M_Line.mat";
        const string ParticlePath = Root + "Art/Materials/M_Particle.mat";
        const string UnlitTransparentPath = Root + "Art/Materials/M_BlobShadow.mat";
        const string RingTemplatePath = Root + "Prefabs/VFX/FreezeWave.prefab";

        static readonly Color Chalk = new(0.97f, 0.96f, 0.92f, 1f);

        [MenuItem("Bouncer/Cards/Build Card Content")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(MaterialDir);
            AssetDatabase.Refresh();
            ImportTextures();
            BuildMaterials();
            BuildEffects();
            BuildBalls();
            BuildHedgehog();
            BuildAbilities();
            BuildCards();
            WirePlayer();
            WireNetwork();
            WireHud();
            WireSounds();
            // частицы карточек собираются на M_Particle — переводим их на мультяшный атлас (VFX-проход)
            VfxPassBuilder.StyleAllParticles();
            AssetDatabase.SaveAssets();
            Debug.Log("[Cards] Готово: эффекты, мячи, умения, карточки, игрок, HUD, звуки.");
        }

        // ------------------------------------------------------------------ textures and materials
        static void ImportTextures()
        {
            foreach (string name in new[] { "T_Elastic", "T_Handprints", "T_VHS" })
            {
                var importer = AssetImporter.GetAtPath(VfxDir + name + ".png") as TextureImporter;
                if (importer == null)
                    continue;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            foreach (string name in new[] { "T_Tamagotchi_0", "T_Tamagotchi_1", "T_Tamagotchi_2" })
            {
                var importer = AssetImporter.GetAtPath(VfxDir + name + ".png") as TextureImporter;
                if (importer == null)
                    continue;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }

        static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(VfxDir + name + ".png");

        static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                Debug.LogError("[Cards] Нет ассета " + path);
            return asset;
        }

        static Material SaveMaterial(Material material, string name)
        {
            string path = MaterialDir + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            material.name = name;
            if (existing != null)
            {
                existing.shader = material.shader;
                existing.CopyPropertiesFromMaterial(material);
                existing.renderQueue = material.renderQueue;
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Material Unlit(string name, Texture texture, Color color)
        {
            var material = new Material(Load<Material>(UnlitTransparentPath));
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", color);
            material.mainTexture = texture;
            return SaveMaterial(material, name);
        }

        static Material Line(string name, Texture texture, Color color)
        {
            var material = new Material(Load<Material>(LinePath));
            material.mainTexture = texture;
            material.color = color;
            return SaveMaterial(material, name);
        }

        static Material s_bubble, s_gumBubble, s_chalkCross, s_spiral, s_popup, s_photo, s_screen, s_elastic, s_hands, s_waves,
            s_tape, s_chalkLine;

        static void BuildMaterials()
        {
            var bubbleShader = Shader.Find("Bouncer/Bubble");
            s_bubble = SaveMaterial(new Material(bubbleShader), "M_Bubble");
            var gum = new Material(bubbleShader);
            gum.SetColor("_BaseColor", new Color(1f, 0.55f, 0.78f, 1f));
            gum.SetFloat("_Iridescence", 0.15f);
            gum.SetFloat("_CoreAlpha", 0.35f);
            s_gumBubble = SaveMaterial(gum, "M_GumBubble");
            s_chalkCross = Unlit("M_ChalkCross", Tex("T_ChalkCross"), Chalk);
            s_spiral = Unlit("M_ChalkSpiral", Tex("T_ChalkSpiral"), new Color(1f, 1f, 1f, 0.85f));
            s_popup = Unlit("M_ChalkPopup", Tex("T_Popup_0"), Chalk);
            s_photo = Unlit("M_Photo", Tex("T_Photo"), Color.white);
            var screen = Unlit("M_TamagotchiScreen", Tex("T_Tamagotchi_0"), Color.white);
            screen.SetFloat("_Surface", 0f);
            s_screen = screen;
            s_waves = Unlit("M_RadioWaves", Tex("T_RadioWaves"), new Color(1f, 0.85f, 0.3f, 1f));
            s_elastic = Line("M_ElasticLine", Tex("T_Elastic"), Color.white);
            s_hands = Line("M_HandprintLine", Tex("T_Handprints"), Color.white);
            s_tape = Line("M_TapeLine", null, new Color(0.33f, 0.22f, 0.14f, 1f));
            s_chalkLine = Line("M_ChalkLine", null, Chalk);
        }

        // ------------------------------------------------------------------ prefab helpers
        static GameObject SavePrefab(GameObject go, string name)
        {
            string path = PrefabDir + name + ".prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved;
        }

        static T Component<T>(string prefab) where T : Component =>
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + prefab + ".prefab").GetComponent<T>();

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[Cards] Нет поля {field} у {target.GetType().Name}");
                return;
            }
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[Cards] Нет поля {field} у {target.GetType().Name}");
                return;
            }
            if (property.propertyType == SerializedPropertyType.Boolean)
                property.boolValue = value > 0.5f;
            else if (property.propertyType == SerializedPropertyType.Integer)
                property.intValue = Mathf.RoundToInt(value);
            else
                property.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray(Object target, string field, IList<Object> values)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject Model(string name, Transform parent, Vector3 position, float scale = 1f)
        {
            var model = Load<GameObject>(ModelDir + name + ".fbx");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * scale;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        static GameObject Quad(string name, Transform parent, Material material, Vector3 position, Vector3 euler, Vector3 scale)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        static GameObject Sphere(string name, Transform parent, Material material, Vector3 position, float scale)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * scale;
            go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        static ParticleSystem Particles(string name, Transform parent, Color a, Color b, int count, float speed, float life, float size,
            float gravity = 0f, float radius = 0.2f, bool squares = false, bool loop = false, float rate = 0f)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            go.transform.SetParent(parent, false);
            var ps = go.GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = loop ? 1f : 0.5f;
            main.loop = loop;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = Mathf.Max(count * 2, 64);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var emission = ps.emission;
            emission.rateOverTime = rate;
            emission.SetBursts(count > 0 ? new[] { new ParticleSystem.Burst(0f, (short)count) } : new ParticleSystem.Burst[0]);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, squares ? 1f : 0.4f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Load<Material>(ParticlePath);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        static GameObject Burst(string name, float lifetime, params (string name, Color a, Color b, int count, float speed, float life, float size, float gravity)[] systems)
        {
            var root = new GameObject(name);
            foreach (var s in systems)
                Particles(s.name, root.transform, s.a, s.b, s.count, s.speed, s.life, s.size, s.gravity);
            var burst = root.AddComponent<ParticleBurst>();
            SetFloat(burst, "lifetime", lifetime);
            return SavePrefab(root, name);
        }

        static ExpandingRing Ring(string name, Color color, float duration, float startWidth, float endWidth, bool inward = false,
            Material material = null, float arc = 360f)
        {
            string path = PrefabDir + name + ".prefab";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CopyAsset(RingTemplatePath, path);
            var root = PrefabUtility.LoadPrefabContents(path);
            var ring = root.GetComponent<ExpandingRing>();
            var so = new SerializedObject(ring);
            so.FindProperty("duration").floatValue = duration;
            so.FindProperty("color").colorValue = color;
            so.FindProperty("startWidth").floatValue = startWidth;
            so.FindProperty("endWidth").floatValue = endWidth;
            so.FindProperty("inward").boolValue = inward;
            so.ApplyModifiedPropertiesWithoutUndo();
            var line = root.GetComponent<LineRenderer>();
            if (material)
            {
                line.sharedMaterial = material;
                line.textureMode = LineTextureMode.Tile;
            }
            var circle = root.GetComponent<CircleLine>();
            circle.Arc = arc;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<ExpandingRing>();
        }

        static LineRenderer AddLine(GameObject go, Material material, float width, bool loop = false, bool world = true)
        {
            if (!go.TryGetComponent(out LineRenderer line))
                line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.widthMultiplier = width;
            line.loop = loop;
            line.useWorldSpace = world;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
            return line;
        }
    }
}
