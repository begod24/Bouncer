using System.Collections.Generic;
using Bouncer.Core;
using Bouncer.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bouncer.EditorTools
{
    // VFX-проход 2026-10: мультяшные частицы из атласа, импакт-кадры, заливка телеграфов, след мяча, дождь и лужи,
    // контровой свет у врагов и мячей. Меню Bouncer → Art → Apply VFX Pass, повторный запуск ничего не ломает.
    // Текстуры: python3 Tools/vfx_art.py fx. Ёжика (Glow/SpikySparks/Shards на мячах) не трогаем.
    public static class VfxPassBuilder
    {
        const string Root = "Assets/_Project/";
        const string VfxDir = Root + "Art/VFX/";
        const string MaterialDir = Root + "Art/Materials/Fx/";
        const string PrefabDir = Root + "Prefabs/VFX/";
        const string ShaderDir = Root + "Art/Shaders/";
        const string RingTemplatePath = Root + "Prefabs/VFX/FreezeWave.prefab";

        internal const int Tiles = 8;

        // ряды атласа T_FxAtlas (см. Tools/vfx_art.py)
        internal const int RowPuff = 0, RowStar = 1, RowPaper = 2, RowFeather = 3, RowStraw = 4, RowFluff = 5, RowSplinterDrop = 6, RowMisc = 7;

        enum Mat { Alpha, Glow, Add, Ground }

        readonly struct Style
        {
            public readonly int Row, From, To;
            public readonly bool Animate;
            public readonly Mat Material;
            public readonly float Size;
            public readonly float Spin;
            public readonly bool KeepRotation;

            public Style(int row, int from, int to, bool animate, Mat material, float size = 1f, float spin = 0f, bool keepRotation = false)
            {
                Row = row;
                From = from;
                To = to;
                Animate = animate;
                Material = material;
                Size = size;
                Spin = spin;
                KeepRotation = keepRotation;
            }
        }

        static Style Puff(Mat mat = Mat.Alpha, float size = 0.85f) => new(RowPuff, 0, 7, true, mat, size);

        // «Префаб/Система» → стиль. Системы не из списка остаются как были (в том числе ёжик на мячах).
        static readonly Dictionary<string, Style> Styles = new()
        {
            ["CapGunSmoke/Smoke"] = Puff(),
            ["CapGunSmoke/Caps"] = new(RowPaper, 0, 5, false, Mat.Alpha, 1.6f, 540f),
            ["CapGunSmoke/Flash"] = new(RowStar, 6, 6, false, Mat.Add, 1.1f, 90f),
            ["BubblePopBurst/Drops"] = new(RowSplinterDrop, 5, 7, false, Mat.Alpha, 1.5f),
            ["ChalkMark/Dust"] = Puff(),
            ["Dazzle/Stars"] = new(RowStar, 0, 3, false, Mat.Add, 1.5f, 180f),
            ["FenceDust/Dust"] = Puff(),
            ["FenceWall/RiseDust"] = Puff(),
            ["FenceSplinters/Splinters"] = new(RowSplinterDrop, 0, 3, false, Mat.Alpha, 1.5f, 720f),
            ["GumPopBurst/Gum"] = new(RowSplinterDrop, 5, 7, false, Mat.Alpha, 1.4f),
            ["MagnetSparks/Red"] = new(RowStar, 0, 2, false, Mat.Add, 1.6f, 360f),
            ["MagnetSparks/Blue"] = new(RowStar, 0, 2, false, Mat.Add, 1.6f, 360f),
            ["PixelBurst/Pixels"] = new(RowPaper, 6, 7, false, Mat.Alpha, 1f, 0f, keepRotation: true),
            ["Chick/KeySparks"] = new(RowMisc, 0, 0, false, Mat.Add, 1f, 0f, keepRotation: true),
            ["Elite_Chick/KeySparks"] = new(RowMisc, 0, 0, false, Mat.Add, 1f, 0f, keepRotation: true),
            ["Shadow/ShadowSmoke"] = Puff(),
            ["Elite_Shadow/ShadowSmoke"] = Puff(),
            ["ShadowSmoke/ShadowSmoke"] = Puff(),
            ["ShadowPoof/ShadowPoof"] = Puff(),
            ["ChickBlast/Flash"] = new(RowMisc, 6, 6, false, Mat.Add, 1f, 0f, keepRotation: true),
            ["ChickBlast/Fire"] = Puff(Mat.Glow),
            ["ChickBlast/Smoke"] = Puff(),
            ["ChickBlast/Sparks"] = new(RowMisc, 0, 0, false, Mat.Add, 1f, 0f, keepRotation: true),
            ["FeatherPuff/FeatherPuff"] = new(RowFeather, 0, 7, false, Mat.Alpha, 1.3f, 160f),
            ["LandingDust/Smoke"] = Puff(),
            ["SlowCloud/SlowCloud"] = Puff(),
            ["StrawBurst/Dust"] = Puff(),
            ["StrawBurst/Straw"] = new(RowStraw, 0, 7, false, Mat.Alpha, 2.2f, 300f),
            ["StuffingBurst/StuffingBurst"] = new(RowFluff, 0, 7, false, Mat.Alpha, 1.1f, 120f),
        };

        static Material s_alpha, s_glow, s_add, s_ground, s_impact, s_trail, s_telegraph;

        [MenuItem("Bouncer/Art/Apply VFX Pass")]
        public static void ApplyAll()
        {
            AssetDatabase.Refresh();
            ImportTextures();
            BuildMaterials();
            StyleAllParticles();
            BuildImpactPrefabs();
            BuildTelegraphFills();
            BuildRain();
            WireTrails();
            MarkRimPrefabs();
            NameRimLayer();
            AssetDatabase.SaveAssets();
            Debug.Log("[VFX] Готово: частицы, импакт-кадры, телеграфы, дождь, след мяча, контровой свет.");
        }

        // ------------------------------------------------------------------ textures and materials
        static void ImportTextures()
        {
            Import("T_FxAtlas", TextureWrapMode.Clamp, true);
            Import("T_Impact", TextureWrapMode.Clamp, false);
            Import("T_TrailChalk", TextureWrapMode.Clamp, false);
        }

        internal static void Import(string name, TextureWrapMode wrap, bool mips)
        {
            var importer = AssetImporter.GetAtPath(VfxDir + name + ".png") as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("[VFX] Нет текстуры " + name + " — запусти python3 Tools/vfx_art.py fx");
                return;
            }
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.wrapMode = wrap;
            importer.mipmapEnabled = mips;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        internal static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(VfxDir + name + ".png");

        internal static Material MaterialAt(string name, string shader)
        {
            System.IO.Directory.CreateDirectory(MaterialDir);
            string path = MaterialDir + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var s = Shader.Find(shader);
            if (s == null)
                Debug.LogError("[VFX] Нет шейдера " + shader);
            if (material == null)
            {
                material = new Material(s) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != s)
            {
                material.shader = s;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        internal static Material ParticleMaterial(string name, Texture texture, bool additive, float lit, float groundFade, bool onTop = false)
        {
            var m = MaterialAt(name, "Bouncer/Particle");
            m.SetTexture("_BaseMap", texture);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Additive", additive ? 1f : 0f);
            m.SetFloat("_Lit", lit);
            m.SetFloat("_GroundFade", groundFade);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_ZTest", (float)(onTop ? CompareFunction.Always : CompareFunction.LessEqual));
            m.renderQueue = onTop ? (int)RenderQueue.Transparent + 50 : -1;
            return m;
        }

        static void BuildMaterials()
        {
            var atlas = Tex("T_FxAtlas");
            s_alpha = ParticleMaterial("M_FxParticle", atlas, false, 1f, 0.25f);
            s_glow = ParticleMaterial("M_FxParticleGlow", atlas, false, 0f, 0.25f);
            s_add = ParticleMaterial("M_FxParticleAdd", atlas, true, 0f, 0.1f);
            s_ground = ParticleMaterial("M_FxParticleGround", atlas, false, 1f, 0f);
            s_impact = ParticleMaterial("M_FxImpact", Tex("T_Impact"), false, 0f, 0f, onTop: true);

            s_trail = MaterialAt("M_BallTrail", "Bouncer/Trail");
            s_trail.SetTexture("_BaseMap", Tex("T_TrailChalk"));
            s_trail.SetFloat("_Steps", 3f);
            s_trail.SetFloat("_Core", 0.35f);

            s_telegraph = MaterialAt("M_TelegraphFill", "Bouncer/Telegraph");

            var puddle = AssetDatabase.LoadAssetAtPath<Material>(Root + "Art/Materials/M_Puddle.mat");
            if (puddle)
            {
                var shader = Shader.Find("Bouncer/Puddle");
                if (puddle.shader != shader)
                {
                    puddle.shader = shader;
                    puddle.SetColor("_BaseColor", new Color(0.08f, 0.1f, 0.14f, 0.92f));
                    puddle.SetFloat("_Smoothness", 0.92f);
                }
                EditorUtility.SetDirty(puddle);
            }
        }

        static Material For(Mat mat) => mat switch
        {
            Mat.Glow => s_glow,
            Mat.Add => s_add,
            Mat.Ground => s_ground,
            _ => s_alpha,
        };

        static bool IsFxMaterial(Material m) => m != null && m.shader != null && m.shader.name == "Bouncer/Particle";

        // ------------------------------------------------------------------ particles
        public static void StyleAllParticles()
        {
            if (s_alpha == null)
            {
                ImportTextures();
                BuildMaterials();
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Root + "Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                bool any = false;
                foreach (var ps in asset.GetComponentsInChildren<ParticleSystem>(true))
                    if (Styles.ContainsKey(asset.name + "/" + ps.name))
                        any = true;
                if (!any)
                    continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                bool changed = false;
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (!Styles.TryGetValue(asset.name + "/" + ps.name, out var style))
                        continue;
                    // в варианте префаба систему мог уже настроить базовый — тогда только проверяем материал
                    Apply(ps, style);
                    changed = true;
                }
                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void Apply(ParticleSystem ps, Style style)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            bool first = !IsFxMaterial(renderer.sharedMaterial);
            renderer.sharedMaterial = For(style.Material);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (style.Row == RowStraw && renderer.renderMode == ParticleSystemRenderMode.Stretch)
                renderer.renderMode = ParticleSystemRenderMode.Billboard;

            var main = ps.main;
            if (first && !Mathf.Approximately(style.Size, 1f))
                main.startSizeMultiplier *= style.Size;
            // клубки и капли нарисованы со светом сверху-слева — почти не крутим; остальное — как угодно
            bool drops = style.Row == RowSplinterDrop && style.From >= 4;
            if (!style.KeepRotation)
                main.startRotation = drops
                    ? new ParticleSystem.MinMaxCurve(0f)
                    : style.Row == RowPuff
                        ? new ParticleSystem.MinMaxCurve(-0.25f, 0.25f)
                        : new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            Sheet(ps, style.Row, style.From, style.To, style.Animate);

            var rotation = ps.rotationOverLifetime;
            if (style.Spin > 0f)
            {
                rotation.enabled = true;
                float rad = style.Spin * Mathf.Deg2Rad;
                rotation.z = new ParticleSystem.MinMaxCurve(-rad, rad);
            }
            else if (!style.KeepRotation)
            {
                rotation.enabled = false;
            }
        }

        internal static void Sheet(ParticleSystem ps, int row, int from, int to, bool animate)
        {
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = Tiles;
            sheet.numTilesY = Tiles;
            sheet.animation = ParticleSystemAnimationType.SingleRow;
            sheet.rowMode = ParticleSystemAnimationRowMode.Custom;
            sheet.rowIndex = row;
            sheet.cycleCount = 1;
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f);
            float a = (float)from / Tiles;
            float b = (to + 1f) / Tiles - 0.001f;
            sheet.frameOverTime = animate
                ? new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, a, 1f, b))
                : new ParticleSystem.MinMaxCurve(a, b);
        }

        // ------------------------------------------------------------------ impact frames and perfect catch
        static ParticleSystem NewSystem(string name, Transform parent, Material material, Color a, Color b, int count,
            Vector2 speed, Vector2 life, Vector2 size, float gravity = 0f, float radius = 0.05f)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            go.transform.SetParent(parent, false);
            var ps = go.GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = Mathf.Max(8, count * 2);
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.65f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = fade;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        internal static void SizeCurve(ParticleSystem ps, params Keyframe[] keys)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(keys));
        }

        static GameObject SavePrefab(GameObject root, string name)
        {
            string path = PrefabDir + name + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static ParticleBurst ImpactStar(string name, Color star, Color bits)
        {
            var root = new GameObject(name);
            var flash = NewSystem("Star", root.transform, s_impact, star, star, 1, Vector2.zero, new Vector2(0.15f, 0.15f),
                new Vector2(1f, 1.25f));
            var main = flash.main;
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            var sheet = flash.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 4;
            sheet.numTilesY = 1;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.cycleCount = 1;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 0.999f));
            var colors = flash.colorOverLifetime;
            colors.enabled = false;
            SizeCurve(flash, new Keyframe(0f, 0.75f), new Keyframe(0.35f, 1.05f), new Keyframe(1f, 1.15f));

            var sparks = NewSystem("Bits", root.transform, s_add, bits, Color.white, 6, new Vector2(2.5f, 5f), new Vector2(0.18f, 0.3f),
                new Vector2(0.12f, 0.2f), 0.6f, 0.1f);
            Sheet(sparks, RowStar, 0, 3, false);
            var sparkMain = sparks.main;
            sparkMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            SizeCurve(sparks, new Keyframe(0f, 1f), new Keyframe(1f, 0.3f));

            var burst = root.AddComponent<ParticleBurst>();
            SetFloat(burst, "lifetime", 0.45f);
            return SavePrefab(root, name).GetComponent<ParticleBurst>();
        }

        static ParticleBurst PerfectCatchBurst()
        {
            var root = new GameObject("PerfectCatch");
            var gold = new Color(1f, 0.86f, 0.3f);
            var sparkle = NewSystem("Sparkle", root.transform, s_add, gold, Color.white, 1, Vector2.zero, new Vector2(0.28f, 0.28f),
                new Vector2(1.5f, 1.5f));
            Sheet(sparkle, RowStar, 0, 0, false);
            var main = sparkle.main;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f);
            var spin = sparkle.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(4f);
            SizeCurve(sparkle, new Keyframe(0f, 0.2f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f));

            var stars = NewSystem("Stars", root.transform, s_add, gold, Color.white, 10, new Vector2(2.5f, 4.2f), new Vector2(0.35f, 0.55f),
                new Vector2(0.14f, 0.24f), -0.15f, 0.2f);
            Sheet(stars, RowStar, 0, 3, false);
            var starsMain = stars.main;
            starsMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            SizeCurve(stars, new Keyframe(0f, 1f), new Keyframe(1f, 0.2f));

            var burst = root.AddComponent<ParticleBurst>();
            SetFloat(burst, "lifetime", 0.7f);
            return SavePrefab(root, "PerfectCatch").GetComponent<ParticleBurst>();
        }

        static ExpandingRing GoldRing()
        {
            string path = PrefabDir + "PerfectRing.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                AssetDatabase.CopyAsset(RingTemplatePath, path);
            var root = PrefabUtility.LoadPrefabContents(path);
            var ring = root.GetComponent<ExpandingRing>();
            var so = new SerializedObject(ring);
            so.FindProperty("duration").floatValue = 0.3f;
            so.FindProperty("color").colorValue = new Color(1f, 0.86f, 0.35f, 0.95f);
            so.FindProperty("startWidth").floatValue = 0.28f;
            so.FindProperty("endWidth").floatValue = 0.04f;
            so.FindProperty("inward").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<ExpandingRing>();
        }

        static void BuildImpactPrefabs()
        {
            var enemy = ImpactStar("ImpactStar", new Color(1f, 0.96f, 0.72f), new Color(1f, 0.85f, 0.35f));
            var player = ImpactStar("ImpactStar_Player", new Color(1f, 0.45f, 0.35f), new Color(1f, 0.7f, 0.4f));
            var perfect = PerfectCatchBurst();
            var ring = GoldRing();

            string path = Root + "Prefabs/Player.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            if (!root.TryGetComponent(out ImpactFrames frames))
                frames = root.AddComponent<ImpactFrames>();
            var so = new SerializedObject(frames);
            so.FindProperty("enemyHit").objectReferenceValue = enemy;
            so.FindProperty("playerHit").objectReferenceValue = player;
            so.FindProperty("perfectCatch").objectReferenceValue = perfect;
            so.FindProperty("perfectRing").objectReferenceValue = ring;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // ------------------------------------------------------------------ telegraphs
        static void BuildTelegraphFills()
        {
            foreach (string name in new[] { "GroundMarker_Circle", "GroundMarker_Line" })
            {
                string path = PrefabDir + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                var fillTransform = root.transform.Find("Fill");
                GameObject fill;
                if (fillTransform)
                {
                    fill = fillTransform.gameObject;
                }
                else
                {
                    fill = new GameObject("Fill", typeof(MeshFilter), typeof(MeshRenderer));
                    fill.transform.SetParent(root.transform, false);
                }
                fill.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                fill.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                var renderer = fill.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = s_telegraph;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                var marker = root.GetComponent<GroundMarker>();
                var so = new SerializedObject(marker);
                so.FindProperty("fill").objectReferenceValue = renderer;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ------------------------------------------------------------------ rain
        static void BuildRain()
        {
            string path = PrefabDir + "Weather.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var rain = root.transform.Find("Rain")?.GetComponent<ParticleSystem>();
            if (rain == null)
            {
                Debug.LogError("[VFX] В Weather нет Rain");
                PrefabUtility.UnloadPrefabContents(root);
                return;
            }

            // капли: тонкие штрихи с ярким ядром, косой ветер с разбросом, гибнут ровно у асфальта (12 м / 22 м/с)
            var main = rain.main;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.045f);
            main.maxParticles = 2200;
            var rainEmission = rain.emission;
            rainEmission.rateOverTime = 1200f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.78f, 0.86f, 1f, 0.42f), new Color(0.9f, 0.95f, 1f, 0.62f));
            var velocity = rain.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-2.1f, -1.1f);
            velocity.y = new ParticleSystem.MinMaxCurve(-22f, -22f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            // смерть капли ловится только на шаге симуляции (уходит под асфальт до 0,4 м),
            // поэтому брызги рождаются по столкновению с плоскостью земли — точно в точке касания
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f);
            var weatherController = root.GetComponent<MonoBehaviour>();
            float height = 12f;
            if (weatherController != null)
            {
                var heightProp = new SerializedObject(weatherController).FindProperty("rainHeight");
                if (heightProp != null)
                    height = heightProp.floatValue;
            }
            var plane = rain.transform.Find("GroundPlane");
            if (plane == null)
            {
                plane = new GameObject("GroundPlane").transform;
                plane.SetParent(rain.transform, false);
            }
            plane.localPosition = new Vector3(0f, -height + 0.03f, 0f);
            plane.localRotation = Quaternion.identity;
            var collision = rain.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.Planes;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.SetPlane(0, plane);
            collision.bounce = 0f;
            collision.dampen = 1f;
            collision.lifetimeLoss = 1f;
            collision.radiusScale = 0.01f;
            collision.enableDynamicColliders = false;
            collision.sendCollisionMessages = false;
            var rainShape = rain.shape;
            rainShape.scale = new Vector3(rainShape.scale.x, 0f, rainShape.scale.z);

            var rainRenderer = rain.GetComponent<ParticleSystemRenderer>();
            rainRenderer.sharedMaterial = s_ground;
            rainRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            rainRenderer.lengthScale = 2f;
            rainRenderer.velocityScale = 0.09f;
            Sheet(rain, RowMisc, 0, 0, false);
            var rainMain = rain.main;
            rainMain.startRotation = new ParticleSystem.MinMaxCurve(0f);

            // всплески: мультяшная «корона» из атласа, 4 кадра, стоит на асфальте
            var splash = rain.transform.Find("Splash")?.GetComponent<ParticleSystem>();
            if (splash)
            {
                var sMain = splash.main;
                sMain.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.28f);
                sMain.startSpeed = new ParticleSystem.MinMaxCurve(0f);
                sMain.gravityModifier = 0f;
                sMain.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.48f);
                sMain.startRotation = new ParticleSystem.MinMaxCurve(0f);
                sMain.startColor = new ParticleSystem.MinMaxGradient(new Color(0.82f, 0.9f, 1f, 0.75f), new Color(0.95f, 0.98f, 1f, 0.9f));
                var shape = splash.shape;
                shape.enabled = false;
                var splashEmission = splash.emission;
                splashEmission.rateOverTime = 0f;
                splashEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)1) });
                Sheet(splash, RowMisc, 2, 5, true);
                var sRenderer = splash.GetComponent<ParticleSystemRenderer>();
                sRenderer.sharedMaterial = s_ground;
                sRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                sRenderer.pivot = new Vector3(0f, 0.4f, 0f);
                SetSubEmitter(rain, splash, 0.4f);
            }

            // круги на асфальте под каплей
            var rippleTransform = rain.transform.Find("Ripple");
            ParticleSystem ripple;
            if (rippleTransform)
            {
                ripple = rippleTransform.GetComponent<ParticleSystem>();
            }
            else
            {
                ripple = NewSystem("Ripple", rain.transform, s_ground, Color.white, Color.white, 1, Vector2.zero, Vector2.one, Vector2.one);
            }
            // саб-эмиттер испускает по своим вспышкам: одна частица на каплю
            var rippleEmission = ripple.emission;
            rippleEmission.rateOverTime = 0f;
            rippleEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)1) });
            var rMain = ripple.main;
            rMain.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.5f);
            rMain.startSpeed = new ParticleSystem.MinMaxCurve(0f);
            rMain.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            rMain.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.85f, 1f, 0.45f), new Color(0.9f, 0.95f, 1f, 0.6f));
            rMain.maxParticles = 400;
            rMain.simulationSpace = ParticleSystemSimulationSpace.World;
            var rShape = ripple.shape;
            rShape.enabled = false;
            Sheet(ripple, RowMisc, 7, 7, false);
            SizeCurve(ripple, new Keyframe(0f, 0.15f), new Keyframe(1f, 1f));
            var rRenderer = ripple.GetComponent<ParticleSystemRenderer>();
            rRenderer.sharedMaterial = s_ground;
            rRenderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            rRenderer.pivot = Vector3.zero;
            ripple.transform.localPosition = Vector3.zero;
            SetSubEmitter(rain, ripple, 0.35f);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void SetSubEmitter(ParticleSystem parent, ParticleSystem child, float probability)
        {
            var subs = parent.subEmitters;
            subs.enabled = true;
            for (int i = 0; i < subs.subEmittersCount; i++)
            {
                if (subs.GetSubEmitterSystem(i) != child)
                    continue;
                subs.SetSubEmitterType(i, ParticleSystemSubEmitterType.Collision);
                subs.SetSubEmitterEmitProbability(i, probability);
                return;
            }
            subs.AddSubEmitter(child, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritNothing, probability);
        }

        // ------------------------------------------------------------------ ball trail
        static void WireTrails()
        {
            var paths = new List<string> { Root + "Prefabs/Ball.prefab" };
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Root + "Prefabs/Balls" }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            foreach (string path in paths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                foreach (var trail in root.GetComponentsInChildren<TrailRenderer>(true))
                {
                    if (trail.GetComponentInParent<PlayerController>() != null)
                        continue;
                    trail.sharedMaterial = s_trail;
                    trail.textureMode = LineTextureMode.Stretch;
                    trail.numCapVertices = 0;
                    trail.shadowCastingMode = ShadowCastingMode.Off;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ------------------------------------------------------------------ rim light
        static void MarkRimPrefabs()
        {
            var paths = new List<string> { Root + "Prefabs/Ball.prefab" };
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Root + "Prefabs/Balls", Root + "Prefabs/Enemies" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("Debris.prefab"))
                    paths.Add(path);
            }
            foreach (string path in paths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                bool changed = false;
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer is ParticleSystemRenderer or LineRenderer or TrailRenderer)
                        continue;
                    var material = renderer.sharedMaterial;
                    if (material == null || material.shader == null || material.shader.name != "Bouncer/PaletteLit")
                        continue;
                    if ((renderer.renderingLayerMask & FxGlobals.RimLayerMask) != 0)
                        continue;
                    renderer.renderingLayerMask |= FxGlobals.RimLayerMask;
                    changed = true;
                }
                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void NameRimLayer()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
                return;
            var so = new SerializedObject(assets[0]);
            var layers = so.FindProperty("m_RenderingLayers");
            if (layers == null || !layers.isArray)
                return;
            while (layers.arraySize <= FxGlobals.RimLayerBit)
                layers.InsertArrayElementAtIndex(layers.arraySize);
            var element = layers.GetArrayElementAtIndex(FxGlobals.RimLayerBit);
            if (element.stringValue == "Rim")
                return;
            element.stringValue = "Rim";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
