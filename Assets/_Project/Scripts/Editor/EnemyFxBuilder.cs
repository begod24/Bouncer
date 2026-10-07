using System.Collections.Generic;
using Bouncer.Core;
using Bouncer.Enemies;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using PS = UnityEngine.ParticleSystem;

namespace Bouncer.EditorTools
{
    // Эффекты врагов 2026-10: мультяшный огонь коня, иней на замороженных, свечение элиты, объёмные волны,
    // пыль таранов и приземлений, трещины от Физрука, луч Денди, мигалка Трансформера, дым мешка Бабая, след морковки.
    // Меню Bouncer → Art → Build Enemy FX, повторный запуск ничего не ломает (системы ищутся по имени).
    // Трещины: python3 Tools/vfx_art.py fx. Шейдеры: Art/Shaders/ToonFire, Scorch, Wave, Beam, Glow; иней и контур — в PaletteLit.
    public static partial class EnemyFxBuilder
    {
        const string Root = "Assets/_Project/";
        const string EnemyDir = Root + "Prefabs/Enemies/";
        const string VfxPrefabDir = Root + "Prefabs/VFX/";
        const string FxMaterialDir = Root + "Art/Materials/Fx/";

        const int RowPuff = VfxPassBuilder.RowPuff;
        const int RowStar = VfxPassBuilder.RowStar;
        const int RowStraw = VfxPassBuilder.RowStraw;
        const int RowSplinterDrop = VfxPassBuilder.RowSplinterDrop;
        const int RowMisc = VfxPassBuilder.RowMisc;

        static readonly Color DustA = new(0.82f, 0.76f, 0.66f, 0.85f);
        static readonly Color DustB = new(0.7f, 0.65f, 0.58f, 0.75f);
        static readonly Color FireOrange = new(1f, 0.55f, 0.15f, 1f);
        static readonly Color FireYellow = new(1f, 0.88f, 0.4f, 1f);

        static Material s_alpha, s_glow, s_add, s_trail;
        static Material s_fire, s_scorch, s_cracks, s_beam, s_lamp, s_groundGlow;
        static readonly Material[] s_waves = new Material[5];

        [MenuItem("Bouncer/Art/Build Enemy FX")]
        public static void BuildAll()
        {
            AssetDatabase.Refresh();
            if (!BuildMaterials())
                return;
            BuildFireSpot();
            BuildFireHorse();
            BuildRamFx();
            BuildWaves();
            BuildLandingDust();
            BuildFizrukSmash();
            BuildDendy();
            BuildSiren();
            BuildSackSmoke();
            BuildCarrotTrail();
            BuildEliteGlow();
            WireNetwork();
            AssetDatabase.SaveAssets();
            Debug.Log("[EnemyFX] Готово: огонь коня, волны, пыль, трещины, луч Денди, мигалка, дым мешка, след морковки, элита.");
        }

        // ------------------------------------------------------------------ materials
        static bool BuildMaterials()
        {
            s_alpha = Load<Material>(FxMaterialDir + "M_FxParticle.mat");
            s_glow = Load<Material>(FxMaterialDir + "M_FxParticleGlow.mat");
            s_add = Load<Material>(FxMaterialDir + "M_FxParticleAdd.mat");
            s_trail = Load<Material>(FxMaterialDir + "M_BallTrail.mat");
            if (!s_alpha || !s_glow || !s_add || !s_trail)
            {
                Debug.LogError("[EnemyFX] Нет материалов VFX-прохода — сначала Bouncer → Art → Apply VFX Pass");
                return false;
            }

            s_fire = VfxPassBuilder.MaterialAt("M_FxToonFire", "Bouncer/ToonFire");
            s_scorch = VfxPassBuilder.MaterialAt("M_FxScorch", "Bouncer/Scorch");
            VfxPassBuilder.Import("T_Cracks", TextureWrapMode.Clamp, true);
            s_cracks = VfxPassBuilder.ParticleMaterial("M_FxCracks", VfxPassBuilder.Tex("T_Cracks"), false, 1f, 0f);
            s_beam = VfxPassBuilder.MaterialAt("M_FxBeam", "Bouncer/Beam");

            s_lamp = VfxPassBuilder.MaterialAt("M_FxGlowLamp", "Bouncer/Glow");
            s_lamp.SetFloat("_Billboard", 1f);
            s_lamp.SetFloat("_Soft", 0f);
            s_groundGlow = VfxPassBuilder.MaterialAt("M_FxGlowGround", "Bouncer/Glow");
            s_groundGlow.SetFloat("_Billboard", 0f);
            s_groundGlow.SetFloat("_Soft", 1f);

            string[] names = { "Shock", "Cry", "Drum", "Freeze", "Blast" };
            for (int i = 0; i < names.Length; i++)
            {
                var m = VfxPassBuilder.MaterialAt("M_FxWave_" + names[i], "Bouncer/Wave");
                m.SetFloat("_Pattern", i);
                m.SetFloat("_FrontWhite", i == 4 ? 0.75f : 0.55f);
                m.SetFloat("_TailShade", i == 0 ? 0.9f : 0.85f);
                m.SetFloat("_Lit", i == 0 ? 0.8f : 0.3f);
                s_waves[i] = m;
            }
            return true;
        }

        // ------------------------------------------------------------------ fire horse
        static void BuildFireSpot()
        {
            string path = VfxPrefabDir + "FireSpot.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            // старая дрожащая картинка на земле
            var old = root.transform.Find("Visual");
            if (old)
                Object.DestroyImmediate(old.gameObject);

            var scorch = Ensure(root.transform, "Scorch", new Vector3(0f, 0.03f, 0f));
            Setup(scorch, s_scorch, false, new Vector2(3f, 3f), Vector2.zero, new Vector2(1.6f, 1.8f), Color.white, Color.white, world: false, max: 2);
            Burst(scorch, 1);
            RandomYaw(scorch);
            Renderer(scorch).renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            AgeStreams(scorch);
            VfxPassBuilder.SizeCurve(scorch, new Keyframe(0f, 0.55f), new Keyframe(0.06f, 1f), new Keyframe(1f, 1f));

            var flames = Ensure(root.transform, "Flames", new Vector3(0f, 0.02f, 0f));
            Setup(flames, s_fire, true, new Vector2(0.45f, 0.7f), new Vector2(0.5f, 1f), new Vector2(0.55f, 0.85f), Color.white, Color.white,
                world: false, max: 20);
            Rate(flames, 14f);
            Cone(flames, 8f, 0.38f);
            AgeStreams(flames);
            // язык стоит на земле, а не уходит в неё наполовину
            Renderer(flames).pivot = new Vector3(0f, 0.45f, 0f);
            VfxPassBuilder.SizeCurve(flames, new Keyframe(0f, 0.55f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.75f));

            var embers = Ensure(root.transform, "Embers", new Vector3(0f, 0.1f, 0f));
            Setup(embers, s_add, true, new Vector2(0.6f, 1f), new Vector2(1.2f, 2.2f), new Vector2(0.05f, 0.1f), FireOrange, FireYellow,
                gravity: -0.1f, world: false, max: 16);
            Rate(embers, 7f);
            Cone(embers, 18f, 0.45f);
            Noise(embers, 0.6f, 1.5f);
            Fade(embers, 0.1f, 0.6f);
            VfxPassBuilder.Sheet(embers, RowMisc, 6, 6, false);

            var so = new SerializedObject(root.GetComponent<FireSpot>());
            SetArray(so.FindProperty("flames"), flames, embers);
            so.FindProperty("scorch").objectReferenceValue = scorch;
            so.ApplyModifiedPropertiesWithoutUndo();
            Save(root, path);
        }

        static void BuildFireHorse()
        {
            string path = EnemyDir + "Elite_RockingHorse.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var model = root.transform.Find("Visual/Elite_RockingHorse");
            if (model == null)
            {
                Debug.LogError("[EnemyFX] В Elite_RockingHorse нет Visual/Elite_RockingHorse");
                PrefabUtility.UnloadPrefabContents(root);
                return;
            }
            // точки сняты с модели сбоку: грива по шее, хвост сзади вниз, перо на голове
            FlameStrip(model, "ManeFire", new Vector3(0f, 0.95f, 0.15f), new Vector3(0f, 1.25f, 0.42f), 24f, new Vector2(0.28f, 0.42f));
            FlameStrip(model, "TailFire", new Vector3(0f, 0.82f, -0.45f), new Vector3(0f, 0.47f, -0.68f), 18f, new Vector2(0.26f, 0.4f));
            FlameStrip(model, "PlumeFire", new Vector3(0f, 1.5f, 0.47f), new Vector3(0f, 1.58f, 0.47f), 8f, new Vector2(0.28f, 0.36f));
            Save(root, path);
        }

        static void FlameStrip(Transform parent, string name, Vector3 from, Vector3 to, float rate, Vector2 size)
        {
            Vector3 along = to - from;
            Vector3 dir = along.normalized;
            var rotation = Quaternion.LookRotation(dir, Mathf.Abs(dir.y) > 0.95f ? Vector3.forward : Vector3.up);
            var ps = Ensure(parent, name, (from + to) * 0.5f, rotation);
            // язык поднимается сам, в мировых координатах; скорость коня наследует почти всю —
            // на таране грива стелется коротким шлейфом, а не висит огнём в воздухе позади
            Setup(ps, s_fire, true, new Vector2(0.22f, 0.38f), Vector2.zero, size, Color.white, Color.white, world: true, max: 40, playOnAwake: true);
            Rate(ps, rate);
            Box(ps, new Vector3(0.07f, 0.05f, Mathf.Max(0.05f, along.magnitude)));
            Rise(ps, new Vector2(0.7f, 1.1f));
            Inherit(ps, 0.75f);
            AgeStreams(ps);
            Renderer(ps).pivot = new Vector3(0f, 0.35f, 0f);
            VfxPassBuilder.SizeCurve(ps, new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.6f));
        }

        // ------------------------------------------------------------------ rams: dust and speed lines
        static void BuildRamFx()
        {
            RamFx("RockingHorse", 6f, 0.6f, 1.8f, 1.5f, false);
            RamFx("Elite_RockingHorse", 6f, 0.6f, 1.8f, 1.5f, true);
            RamFx("RCCar", 8.5f, 0.6f, 1.2f, 0.5f, false);
            RamFx("Boss_Transformer", 10f, 1.9f, 4.2f, 1.4f, false);
        }

        static void RamFx(string prefab, float minSpeed, float width, float length, float height, bool runnerSparks)
        {
            string path = EnemyDir + prefab + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var systems = new List<PS>();
            float scale = Mathf.Clamp(width / 0.6f, 1f, 2f);

            var dust = Ensure(root.transform, "RamDust", new Vector3(0f, 0.12f, -length * 0.35f));
            Setup(dust, s_alpha, true, new Vector2(0.5f, 0.9f), new Vector2(0.3f, 1f), new Vector2(0.45f, 0.8f) * scale, DustA, DustB,
                gravity: -0.05f, world: true, max: 60, playOnAwake: true);
            Rate(dust, 0f, 2.2f);
            Box(dust, new Vector3(width, 0.05f, 0.3f), 1f);
            VfxPassBuilder.Sheet(dust, RowPuff, 0, 7, true);
            VfxPassBuilder.SizeCurve(dust, new Keyframe(0f, 0.6f), new Keyframe(1f, 1.3f));
            Fade(dust, 0.1f, 0.5f);
            systems.Add(dust);

            var lines = Ensure(root.transform, "SpeedLines", new Vector3(0f, height * 0.55f, 0f));
            Setup(lines, s_glow, true, new Vector2(0.12f, 0.22f), Vector2.zero, new Vector2(0.05f, 0.09f), new Color(1f, 1f, 1f, 0.85f),
                new Color(1f, 0.97f, 0.9f, 0.6f), world: true, max: 40, playOnAwake: true);
            Rate(lines, 0f, 3f);
            Box(lines, new Vector3(width * 1.3f, height * 0.9f, length * 0.6f));
            Inherit(lines, -0.4f);
            Stretch(lines, 0.1f, 2f);
            VfxPassBuilder.Sheet(lines, RowMisc, 0, 0, false);
            Fade(lines, 0.1f, 0.5f);
            systems.Add(lines);

            if (runnerSparks)
            {
                // огненный конь: искры из-под полозьев
                var sparks = Ensure(root.transform, "RunnerSparks", new Vector3(0f, 0.06f, 0f));
                Setup(sparks, s_add, true, new Vector2(0.25f, 0.45f), new Vector2(1.5f, 3.5f), new Vector2(0.05f, 0.09f), FireOrange, FireYellow,
                    gravity: 2f, world: true, max: 50, playOnAwake: true);
                Rate(sparks, 0f, 6f);
                var shape = sparks.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(0.5f, 1.6f, 0.02f);
                shape.rotation = new Vector3(-90f, 0f, 0f);
                shape.randomDirectionAmount = 0.4f;
                Inherit(sparks, -0.15f);
                Stretch(sparks, 0.05f, 1.5f);
                VfxPassBuilder.Sheet(sparks, RowMisc, 0, 0, false);
                systems.Add(sparks);
            }

            foreach (var system in systems)
            {
                var emission = system.emission;
                emission.enabled = false;
            }
            var fx = root.GetComponent<SpeedFx>();
            if (!fx)
                fx = root.AddComponent<SpeedFx>();
            var so = new SerializedObject(fx);
            so.FindProperty("minSpeed").floatValue = minSpeed;
            SetArray(so.FindProperty("systems"), systems.ToArray());
            so.ApplyModifiedPropertiesWithoutUndo();
            Save(root, path);
        }

        // ------------------------------------------------------------------ elites
        static void BuildEliteGlow()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab Elite_", new[] { EnemyDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!file.StartsWith("Elite_") || file.EndsWith("Debris"))
                    continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                var flash = root.GetComponent<HitFlash>();
                if (!flash)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                    continue;
                }
                var bounds = new Bounds(root.transform.position, Vector3.zero);
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    if (r is MeshRenderer or SkinnedMeshRenderer)
                        bounds.Encapsulate(r.bounds);
                float radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.85f, 0.35f, 1.3f);

                var sparks = Ensure(root.transform, "EliteSparks", new Vector3(0f, 0.08f, 0f));
                Setup(sparks, s_add, true, new Vector2(0.7f, 1.1f), Vector2.zero, new Vector2(0.1f, 0.2f), Color.white, Color.white,
                    world: true, max: 30, playOnAwake: true);
                Rate(sparks, 9f);
                Circle(sparks, radius, true);
                Rise(sparks, new Vector2(0.8f, 1.5f));
                VfxPassBuilder.Sheet(sparks, RowStar, 0, 3, false);
                Spin(sparks, 180f);
                Fade(sparks, 0.15f, 0.6f);

                var glow = root.GetComponent<EliteGlow>();
                if (!glow)
                    glow = root.AddComponent<EliteGlow>();
                var so = new SerializedObject(glow);
                so.FindProperty("hitFlash").objectReferenceValue = flash;
                so.FindProperty("sparks").objectReferenceValue = sparks;
                so.ApplyModifiedPropertiesWithoutUndo();
                Save(root, path);
            }
        }

        // ------------------------------------------------------------------ particle helpers
        static PS Ensure(Transform parent, string name, Vector3 position) => Ensure(parent, name, position, Quaternion.identity);

        static PS Ensure(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var t = parent.Find(name);
            GameObject go;
            if (t == null)
            {
                go = new GameObject(name, typeof(PS));
                go.transform.SetParent(parent, false);
            }
            else
            {
                go = t.gameObject;
                if (!go.GetComponent<PS>())
                    go.AddComponent<PS>();
            }
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = Vector3.one;
            var ps = go.GetComponent<PS>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        // Все модули, которыми пользуемся, приводятся к одному виду — повторный запуск даёт тот же результат
        static void Setup(PS ps, Material material, bool loop, Vector2 life, Vector2 speed, Vector2 size, Color a, Color b,
            float gravity = 0f, bool world = true, int max = 64, bool playOnAwake = false)
        {
            var main = ps.main;
            main.duration = loop ? 1f : 0.5f;
            main.loop = loop;
            main.playOnAwake = playOnAwake;
            main.startDelay = 0f;
            main.prewarm = false;
            main.startLifetime = new PS.MinMaxCurve(life.x, life.y);
            main.startSpeed = new PS.MinMaxCurve(speed.x, speed.y);
            main.startSize3D = false;
            main.startSize = new PS.MinMaxCurve(size.x, size.y);
            main.startRotation3D = false;
            main.startRotation = new PS.MinMaxCurve(0f);
            main.startColor = new PS.MinMaxGradient(a, b);
            main.gravityModifier = gravity;
            main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = max;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(System.Array.Empty<PS.Burst>());

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            shape.radiusThickness = 1f;
            shape.arc = 360f;
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;
            shape.scale = Vector3.one;
            shape.randomDirectionAmount = 0f;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = false;
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = false;
            var inherit = ps.inheritVelocity;
            inherit.enabled = false;
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = false;
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = false;
            var noise = ps.noise;
            noise.enabled = false;
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = false;
            var colors = ps.colorOverLifetime;
            colors.enabled = false;
            var collision = ps.collision;
            collision.enabled = false;
            var subEmitters = ps.subEmitters;
            subEmitters.enabled = false;
            var trails = ps.trails;
            trails.enabled = false;

            var renderer = Renderer(ps);
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = 4f;
            renderer.pivot = Vector3.zero;
            renderer.sortingFudge = 0f;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal,
                ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV,
            });
        }

        static ParticleSystemRenderer Renderer(PS ps) => ps.GetComponent<ParticleSystemRenderer>();

        // для ToonFire и Scorch: UV, возраст 0..1 и случайное число частицы — в TEXCOORD0.xyzw
        static void AgeStreams(PS ps) => Renderer(ps).SetActiveVertexStreams(new List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV,
            ParticleSystemVertexStream.AgePercent, ParticleSystemVertexStream.StableRandomX,
        });

        static void Burst(PS ps, int count)
        {
            var emission = ps.emission;
            emission.SetBursts(new[] { new PS.Burst(0f, (short)count) });
        }

        static void Rate(PS ps, float perSecond, float perMeter = 0f)
        {
            var emission = ps.emission;
            emission.rateOverTime = perSecond;
            emission.rateOverDistance = perMeter;
        }

        static void RandomYaw(PS ps)
        {
            var main = ps.main;
            main.startRotation = new PS.MinMaxCurve(0f, Mathf.PI * 2f);
        }

        // конус вверх
        static void Cone(PS ps, float angle, float radius)
        {
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = radius;
            shape.rotation = new Vector3(-90f, 0f, 0f);
        }

        // конус вдоль своей оси Z (вспышки выстрела, искры)
        static void ForwardCone(PS ps, float angle, float radius)
        {
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = radius;
        }

        // круг, лежащий на земле; частицы летят наружу
        static void Circle(PS ps, float radius, bool edge)
        {
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.radiusThickness = edge ? 0f : 1f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
        }

        static void Box(PS ps, Vector3 scale, float randomDirection = 0f)
        {
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = scale;
            shape.randomDirectionAmount = randomDirection;
        }

        // подъём вверх в мировых координатах (все оси скорости — в одном режиме)
        static void Rise(PS ps, Vector2 up)
        {
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new PS.MinMaxCurve(0f, 0f);
            velocity.y = new PS.MinMaxCurve(up.x, up.y);
            velocity.z = new PS.MinMaxCurve(0f, 0f);
        }

        static void Drag(PS ps, float drag)
        {
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.separateAxes = false;
            limit.limit = new PS.MinMaxCurve(1000f);
            limit.dampen = 0f;
            limit.drag = new PS.MinMaxCurve(drag);
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;
        }

        static void Inherit(PS ps, float multiplier)
        {
            var inherit = ps.inheritVelocity;
            inherit.enabled = true;
            inherit.mode = ParticleSystemInheritVelocityMode.Initial;
            inherit.curve = new PS.MinMaxCurve(multiplier);
        }

        static void Stretch(PS ps, float velocityScale, float lengthScale)
        {
            var renderer = Renderer(ps);
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = velocityScale;
            renderer.lengthScale = lengthScale;
            renderer.cameraVelocityScale = 0f;
        }

        static void Noise(PS ps, float strength, float frequency)
        {
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = strength;
            noise.frequency = frequency;
            noise.scrollSpeed = 0.5f;
            noise.quality = ParticleSystemNoiseQuality.Low;
        }

        static void Spin(PS ps, float degrees)
        {
            var main = ps.main;
            main.startRotation = new PS.MinMaxCurve(0f, Mathf.PI * 2f);
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            float rad = degrees * Mathf.Deg2Rad;
            rotation.z = new PS.MinMaxCurve(-rad, rad);
        }

        static void Fade(PS ps, float inUntil, float outFrom)
        {
            var colors = ps.colorOverLifetime;
            colors.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                inUntil > 0f
                    ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inUntil), new GradientAlphaKey(1f, outFrom), new GradientAlphaKey(0f, 1f) }
                    : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, outFrom), new GradientAlphaKey(0f, 1f) });
            colors.color = gradient;
        }

        // ------------------------------------------------------------------ prefab helpers
        static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        static void Save(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void SetArray(SerializedProperty array, params Object[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        static MeshRenderer EnsureQuad(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
        {
            var t = parent.Find(name);
            var go = t ? t.gameObject : new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            if (!t)
                go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }
    }
}
