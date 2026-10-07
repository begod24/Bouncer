using System.Collections.Generic;
using Bouncer.Core;
using Bouncer.Enemies;
using UnityEditor;
using UnityEngine;
using PS = UnityEngine.ParticleSystem;

namespace Bouncer.EditorTools
{
    public static partial class EnemyFxBuilder
    {
        const int PatternShock = 0, PatternCry = 1, PatternDrum = 2, PatternFreeze = 3, PatternBlast = 4;
        const string ShockGroundPath = VfxPrefabDir + "Shockwave_Ground.prefab";
        const string CracksPath = VfxPrefabDir + "GroundCracks.prefab";
        const string LandingDustPath = VfxPrefabDir + "LandingDust.prefab";

        // ------------------------------------------------------------------ volumetric waves
        static void BuildWaves()
        {
            // Shockwave — база для вариантов (BlastRing, FreezeWave, кольца карт игрока), её не трогаем:
            // пыльная волна приземлений — отдельная копия
            if (Load<GameObject>(ShockGroundPath) == null)
                AssetDatabase.CopyAsset(VfxPrefabDir + "Shockwave.prefab", ShockGroundPath);

            Wave("BlastRing", PatternBlast, new Vector2(1.1f, 0.4f), 0.45f, (root, duration) =>
            {
                RingBurst(root, "Sparks", s_add, 14, new Vector2(1.8f, 2.6f), new Vector2(0.3f, 0.5f), new Vector2(0.06f, 0.1f),
                    FireYellow, FireOrange, duration, stretch: true);
            });
            Wave("CryWave", PatternCry, new Vector2(0.7f, 0.3f), 0.4f, (root, duration) =>
            {
                var tears = RingBurst(root, "Tears", s_alpha, 10, new Vector2(1.4f, 1.9f), new Vector2(0.35f, 0.55f), new Vector2(0.12f, 0.2f),
                    new Color(0.7f, 0.88f, 1f, 0.95f), new Color(0.85f, 0.95f, 1f, 0.9f), duration, gravity: 1f, up: 2f);
                VfxPassBuilder.Sheet(tears, RowSplinterDrop, 4, 7, false);
            });
            Wave("DrumRing", PatternDrum, new Vector2(0.8f, 0.3f), 0.4f, (root, duration) =>
            {
                var stars = RingBurst(root, "Stars", s_add, 10, new Vector2(1.8f, 2.2f), new Vector2(0.3f, 0.5f), new Vector2(0.12f, 0.22f),
                    new Color(1f, 0.85f, 0.35f), new Color(1f, 0.6f, 0.4f), duration, up: 1f);
                VfxPassBuilder.Sheet(stars, RowStar, 0, 3, false);
                Spin(stars, 360f);
            });
            Wave("FreezeWave", PatternFreeze, new Vector2(1f, 0.35f), 0.45f, (root, duration) =>
            {
                var sparkles = RingBurst(root, "Sparkles", s_add, 14, new Vector2(1.8f, 2.3f), new Vector2(0.35f, 0.6f), new Vector2(0.1f, 0.18f),
                    new Color(0.75f, 0.92f, 1f), Color.white, duration, up: 0.8f);
                VfxPassBuilder.Sheet(sparkles, RowStar, 0, 3, false);
                Spin(sparkles, 270f);
            });
            Wave("Shockwave_Ground", PatternShock, new Vector2(1.2f, 0.4f), 0.7f, (root, duration) =>
            {
                var dust = RingBurst(root, "Dust", s_alpha, 16, new Vector2(1.8f, 2.2f), new Vector2(0.6f, 0.9f), new Vector2(0.5f, 0.9f),
                    DustA, DustB, duration);
                VfxPassBuilder.Sheet(dust, RowPuff, 0, 7, true);
                VfxPassBuilder.SizeCurve(dust, new Keyframe(0f, 0.6f), new Keyframe(1f, 1.3f));
                var pebbles = RingBurst(root, "Pebbles", s_alpha, 10, new Vector2(1.2f, 1.6f), new Vector2(0.4f, 0.7f), new Vector2(0.08f, 0.14f),
                    new Color(0.42f, 0.38f, 0.34f), new Color(0.3f, 0.27f, 0.25f), duration, gravity: 1.6f, up: 3f);
                VfxPassBuilder.Sheet(pebbles, RowSplinterDrop, 0, 3, false);
                Spin(pebbles, 720f);
            }, new Color(0.93f, 0.86f, 0.7f, 0.9f), 0.4f);

            // приземления лягушки, зайца и Бабая — пыльной волной, а не огненным кольцом взрыва
            var ground = Load<GameObject>(ShockGroundPath).GetComponent<ExpandingRing>();
            SetField(EnemyDir + "Frog.prefab", typeof(FrogEnemy), "ringPrefab", ground);
            SetField(EnemyDir + "Boss_Hare.prefab", typeof(HareBoss), "slamRing", ground);
            SetField(EnemyDir + "Boss_Dusk.prefab", typeof(DuskBoss), "ringPrefab", ground);
        }

        static void Wave(string prefab, int pattern, Vector2 band, float linger, System.Action<Transform, float> particles,
            Color? color = null, float duration = 0f)
        {
            string path = VfxPrefabDir + prefab + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var ring = root.GetComponent<ExpandingRing>();
            var quad = EnsureQuad(root.transform, "Wave", Vector3.zero, Quaternion.Euler(90f, 0f, 0f), Vector3.one, s_waves[pattern]);
            var so = new SerializedObject(ring);
            so.FindProperty("wave").objectReferenceValue = quad;
            so.FindProperty("waveBand").vector2Value = band;
            so.FindProperty("linger").floatValue = linger;
            if (color.HasValue)
                so.FindProperty("color").colorValue = color.Value;
            if (duration > 0f)
                so.FindProperty("duration").floatValue = duration;
            so.ApplyModifiedPropertiesWithoutUndo();
            particles(root.transform, so.FindProperty("duration").floatValue);
            Save(root, path);
        }

        // Частицы, летящие вместе с фронтом. Скорость в префабе — «на метр радиуса за длительность волны»:
        // ExpandingRing.Play домножает её на радиус/длительность. 2 и сопротивление 1,6/длительность — ровно с фронтом
        static PS RingBurst(Transform parent, string name, Material material, int count, Vector2 speed, Vector2 life, Vector2 size,
            Color a, Color b, float duration, float gravity = 0f, float up = 0f, bool stretch = false)
        {
            var ps = Ensure(parent, name, new Vector3(0f, 0.08f, 0f));
            Setup(ps, material, false, life, speed, size, a, b, gravity, world: false, max: count * 2);
            Burst(ps, count);
            Circle(ps, 0.3f, true);
            Drag(ps, 1.6f / Mathf.Max(0.05f, duration));
            if (up > 0f)
                Rise(ps, new Vector2(up * 0.6f, up));
            if (stretch)
            {
                Stretch(ps, 0.06f, 1.5f);
                VfxPassBuilder.Sheet(ps, RowMisc, 0, 0, false);
            }
            Fade(ps, 0f, 0.55f);
            return ps;
        }

        static void SetField(string path, System.Type type, string field, Object value)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            var component = root.GetComponent(type);
            if (component)
            {
                var so = new SerializedObject(component);
                so.FindProperty(field).objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            else
            {
                Debug.LogError("[EnemyFX] Нет " + type.Name + " в " + path);
            }
            PrefabUtility.UnloadPrefabContents(root);
        }

        // ------------------------------------------------------------------ landing dust (frog, bosses, crashes)
        static void BuildLandingDust()
        {
            var root = PrefabUtility.LoadPrefabContents(LandingDustPath);
            // пыль кольцом по асфальту
            var ring = Ensure(root.transform, "Ring", new Vector3(0f, 0.1f, 0f));
            Setup(ring, s_alpha, false, new Vector2(0.5f, 0.85f), new Vector2(2.5f, 3.6f), new Vector2(0.4f, 0.7f), DustA, DustB,
                gravity: -0.05f, world: false, max: 24);
            Burst(ring, 12);
            Circle(ring, 0.35f, true);
            Drag(ring, 3.5f);
            VfxPassBuilder.Sheet(ring, RowPuff, 0, 7, true);
            VfxPassBuilder.SizeCurve(ring, new Keyframe(0f, 0.6f), new Keyframe(1f, 1.3f));
            Fade(ring, 0f, 0.5f);
            // камешки вверх
            var pebbles = Ensure(root.transform, "Pebbles", new Vector3(0f, 0.1f, 0f));
            Setup(pebbles, s_alpha, false, new Vector2(0.45f, 0.7f), new Vector2(2.5f, 4.2f), new Vector2(0.07f, 0.13f),
                new Color(0.42f, 0.38f, 0.34f), new Color(0.3f, 0.27f, 0.25f), gravity: 1.6f, world: false, max: 16);
            Burst(pebbles, 8);
            Cone(pebbles, 50f, 0.3f);
            VfxPassBuilder.Sheet(pebbles, RowSplinterDrop, 0, 3, false);
            Spin(pebbles, 720f);
            Fade(pebbles, 0f, 0.7f);

            var burst = root.GetComponent<ParticleBurst>();
            var so = new SerializedObject(burst);
            var lifetime = so.FindProperty("lifetime");
            lifetime.floatValue = Mathf.Max(lifetime.floatValue, 1.2f);
            so.ApplyModifiedPropertiesWithoutUndo();
            Save(root, LandingDustPath);
        }

        // ------------------------------------------------------------------ Fizruk smash: cracks + dust
        static void BuildFizrukSmash()
        {
            var cracks = CracksPrefab();
            string path = EnemyDir + "Boss_Fizruk.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var so = new SerializedObject(root.GetComponent<FizrukBoss>());
            so.FindProperty("smashCracks").objectReferenceValue = cracks;
            so.FindProperty("smashDust").objectReferenceValue = Load<GameObject>(LandingDustPath).GetComponent<ParticleBurst>();
            so.ApplyModifiedPropertiesWithoutUndo();
            Save(root, path);
        }

        static ParticleBurst CracksPrefab()
        {
            bool exists = Load<GameObject>(CracksPath) != null;
            var root = exists ? PrefabUtility.LoadPrefabContents(CracksPath) : new GameObject("GroundCracks");
            var ps = Ensure(root.transform, "Cracks", new Vector3(0f, 0.02f, 0f));
            Setup(ps, s_cracks, false, new Vector2(2.4f, 2.4f), Vector2.zero, new Vector2(2.5f, 2.9f),
                new Color(0.17f, 0.14f, 0.12f, 0.92f), new Color(0.2f, 0.17f, 0.14f, 0.92f), world: false, max: 2);
            Burst(ps, 1);
            RandomYaw(ps);
            Renderer(ps).renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            // четыре рисунка 2×2, у каждой вспышки случайный
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 2;
            sheet.numTilesY = 2;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.cycleCount = 1;
            sheet.startFrame = new PS.MinMaxCurve(0f);
            sheet.frameOverTime = new PS.MinMaxCurve(0f, 0.999f);
            VfxPassBuilder.SizeCurve(ps, new Keyframe(0f, 0.7f), new Keyframe(0.05f, 1f), new Keyframe(1f, 1f));
            Fade(ps, 0f, 0.7f);

            var burst = root.GetComponent<ParticleBurst>();
            if (!burst)
                burst = root.AddComponent<ParticleBurst>();
            VfxPassBuilder.SetFloat(burst, "lifetime", 2.5f);
            if (exists)
            {
                Save(root, CracksPath);
            }
            else
            {
                PrefabUtility.SaveAsPrefabAsset(root, CracksPath);
                Object.DestroyImmediate(root);
            }
            return Load<GameObject>(CracksPath).GetComponent<ParticleBurst>();
        }

        // ------------------------------------------------------------------ Dendy beam
        static void BuildDendy()
        {
            var muzzle = ShotBurst("DendyMuzzle", 8, 22f, new Vector2(4f, 7f), 0f, 0.9f);
            var hit = ShotBurst("DendyHitSparks", 12, 50f, new Vector2(3f, 6f), 1.2f, 0.7f);

            string path = EnemyDir + "DendyGun.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var laser = root.GetComponentInChildren<LineRenderer>(true);
            if (laser)
            {
                laser.sharedMaterial = s_beam;
                laser.textureMode = LineTextureMode.Stretch;
                laser.numCapVertices = 0;
                laser.alignment = LineAlignment.View;
                laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                laser.receiveShadows = false;
            }
            var so = new SerializedObject(root.GetComponent<DendyGunEnemy>());
            // ширина вместе со свечением: ядро — около трети
            so.FindProperty("laserWidth").floatValue = 0.1f;
            so.FindProperty("beamWidth").floatValue = 0.55f;
            so.FindProperty("muzzleFlash").objectReferenceValue = muzzle;
            so.FindProperty("hitSparks").objectReferenceValue = hit;
            so.ApplyModifiedPropertiesWithoutUndo();
            Save(root, path);
        }

        static ParticleBurst ShotBurst(string name, int sparks, float angle, Vector2 speed, float gravity, float flashSize)
        {
            string path = VfxPrefabDir + name + ".prefab";
            bool exists = Load<GameObject>(path) != null;
            var root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(name);

            var flash = Ensure(root.transform, "Flash", Vector3.zero);
            Setup(flash, s_add, false, new Vector2(0.1f, 0.12f), Vector2.zero, new Vector2(flashSize, flashSize * 1.15f),
                new Color(1f, 0.95f, 0.7f), Color.white, world: false, max: 2);
            Burst(flash, 1);
            VfxPassBuilder.Sheet(flash, RowMisc, 6, 6, false);
            VfxPassBuilder.SizeCurve(flash, new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.4f));
            Fade(flash, 0f, 0.4f);

            var bits = Ensure(root.transform, "Sparks", Vector3.zero);
            Setup(bits, s_add, false, new Vector2(0.15f, 0.3f), speed, new Vector2(0.05f, 0.08f), FireYellow, Color.white,
                gravity: gravity, world: false, max: sparks * 2);
            Burst(bits, sparks);
            ForwardCone(bits, angle, 0.05f);
            Stretch(bits, 0.05f, 1.5f);
            VfxPassBuilder.Sheet(bits, RowMisc, 0, 0, false);
            Fade(bits, 0f, 0.6f);

            var burst = root.GetComponent<ParticleBurst>();
            if (!burst)
                burst = root.AddComponent<ParticleBurst>();
            VfxPassBuilder.SetFloat(burst, "lifetime", 0.5f);
            if (exists)
            {
                Save(root, path);
            }
            else
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
            }
            return Load<GameObject>(path).GetComponent<ParticleBurst>();
        }

        // ------------------------------------------------------------------ Transformer siren
        static void BuildSiren()
        {
            string path = EnemyDir + "Boss_Transformer.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var rig = root.GetComponentInChildren<TransformerRig>(true);
            var cabin = root.transform.Find("Visual/Boss_Transformer/Transformer_Pelvis/Transformer_Chest/Transformer_Cabin");
            if (rig == null || cabin == null)
            {
                Debug.LogError("[EnemyFX] В Boss_Transformer нет TransformerRig или Transformer_Cabin");
                PrefabUtility.UnloadPrefabContents(root);
                return;
            }

            // маячок — самая высокая точка кабины, когда Трансформер стоит машиной
            var rigSo = new SerializedObject(rig);
            var parts = rigSo.FindProperty("parts");
            var carPositions = rigSo.FindProperty("carPositions");
            var carRotations = rigSo.FindProperty("carRotations");
            var saved = new List<(Transform part, Vector3 position, Quaternion rotation)>();
            for (int i = 0; i < parts.arraySize; i++)
            {
                var part = parts.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (!part)
                    continue;
                saved.Add((part, part.localPosition, part.localRotation));
                part.localPosition = carPositions.GetArrayElementAtIndex(i).vector3Value;
                part.localRotation = carRotations.GetArrayElementAtIndex(i).quaternionValue;
            }
            var mesh = cabin.GetComponent<MeshFilter>().sharedMesh;
            Vector3 top = cabin.position;
            float best = float.NegativeInfinity;
            foreach (var v in mesh.vertices)
            {
                Vector3 world = cabin.TransformPoint(v);
                if (world.y > best)
                {
                    best = world.y;
                    top = world;
                }
            }
            Vector3 lampLocal = cabin.InverseTransformPoint(top + Vector3.up * 0.08f);
            float lampScale = 1.1f / Mathf.Max(0.01f, cabin.lossyScale.x);
            foreach (var (part, position, rotation) in saved)
            {
                part.localPosition = position;
                part.localRotation = rotation;
            }

            var lamp = EnsureQuad(cabin, "SirenLamp", lampLocal, Quaternion.identity, Vector3.one * lampScale, s_lamp);
            var red = EnsureQuad(root.transform, "SirenGroundRed", new Vector3(-1.5f, 0.04f, 0f), Quaternion.Euler(90f, 0f, 0f),
                Vector3.one * 4.5f, s_groundGlow);
            var blue = EnsureQuad(root.transform, "SirenGroundBlue", new Vector3(1.5f, 0.04f, 0f), Quaternion.Euler(90f, 0f, 0f),
                Vector3.one * 4.5f, s_groundGlow);

            var siren = root.GetComponent<SirenLights>();
            if (!siren)
                siren = root.AddComponent<SirenLights>();
            var so = new SerializedObject(siren);
            so.FindProperty("rig").objectReferenceValue = rig;
            so.FindProperty("health").objectReferenceValue = root.GetComponent<Health>();
            so.FindProperty("lamp").objectReferenceValue = lamp;
            so.FindProperty("groundRed").objectReferenceValue = red;
            so.FindProperty("groundBlue").objectReferenceValue = blue;
            so.ApplyModifiedPropertiesWithoutUndo();
            Save(root, path);
        }

        // ------------------------------------------------------------------ Babai's sack smoke
        static void BuildSackSmoke()
        {
            string path = EnemyDir + "Boss_Dusk.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var sack = root.transform.Find("Visual/Boss_Dusk/Dusk_Body/Dusk_Sack");
            if (sack == null)
            {
                Debug.LogError("[EnemyFX] В Boss_Dusk нет Dusk_Sack");
                PrefabUtility.UnloadPrefabContents(root);
                return;
            }
            // из завязанной горловины сверху
            var bounds = sack.GetComponent<MeshFilter>().sharedMesh.bounds;
            var smoke = Ensure(sack, "SackSmoke", new Vector3(bounds.center.x, bounds.max.y - 0.05f, bounds.center.z));
            Setup(smoke, s_alpha, true, new Vector2(1.3f, 2f), new Vector2(0.2f, 0.5f), new Vector2(0.3f, 0.5f),
                new Color(0.16f, 0.09f, 0.22f, 0.8f), new Color(0.28f, 0.18f, 0.36f, 0.7f), world: true, max: 40, playOnAwake: true);
            Rate(smoke, 6f);
            Cone(smoke, 20f, 0.08f);
            Rise(smoke, new Vector2(0.2f, 0.4f));
            Noise(smoke, 0.3f, 0.8f);
            VfxPassBuilder.Sheet(smoke, RowPuff, 0, 7, true);
            VfxPassBuilder.SizeCurve(smoke, new Keyframe(0f, 0.5f), new Keyframe(1f, 1.6f));
            Fade(smoke, 0.15f, 0.55f);
            Save(root, path);
        }

        // ------------------------------------------------------------------ carrot boomerang trail
        static void BuildCarrotTrail()
        {
            string path = EnemyDir + "CarrotBoomerang.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var t = root.transform.Find("Trail");
            var go = t ? t.gameObject : new GameObject("Trail", typeof(TrailRenderer));
            if (!t)
                go.transform.SetParent(root.transform, false);
            go.transform.localPosition = Vector3.zero;
            var trail = go.GetComponent<TrailRenderer>();
            trail.time = 0.35f;
            trail.minVertexDistance = 0.1f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 0f));
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0f), new GradientColorKey(new Color(1f, 0.8f, 0.35f), 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            trail.sharedMaterial = s_trail;
            trail.textureMode = LineTextureMode.Stretch;
            trail.numCapVertices = 0;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = true;

            // зелёные клочки ботвы
            var bits = Ensure(root.transform, "LeafBits", Vector3.zero);
            Setup(bits, s_alpha, true, new Vector2(0.4f, 0.6f), new Vector2(0.2f, 0.6f), new Vector2(0.1f, 0.16f),
                new Color(0.45f, 0.78f, 0.28f), new Color(0.3f, 0.6f, 0.2f), gravity: 0.6f, world: true, max: 20, playOnAwake: true);
            Rate(bits, 0f, 2f);
            VfxPassBuilder.Sheet(bits, RowStraw, 0, 7, false);
            Spin(bits, 300f);
            Fade(bits, 0f, 0.6f);

            var so = new SerializedObject(root.GetComponent<CarrotBoomerang>());
            so.FindProperty("trail").objectReferenceValue = trail;
            so.ApplyModifiedPropertiesWithoutUndo();
            Save(root, path);
        }

        // ------------------------------------------------------------------ network: new effects reach guests
        static void WireNetwork()
        {
            const string path = Root + "Prefabs/Net/NetRoom.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            foreach (var behaviour in root.GetComponents<MonoBehaviour>())
            {
                if (behaviour == null || behaviour.GetType().Name != "NetWorld")
                    continue;
                var so = new SerializedObject(behaviour);
                var array = so.FindProperty("effects");
                foreach (string prefabPath in new[] { ShockGroundPath, CracksPath, LandingDustPath, VfxPrefabDir + "FireSpot.prefab" })
                {
                    var prefab = Load<GameObject>(prefabPath);
                    bool has = false;
                    for (int i = 0; i < array.arraySize; i++)
                        has |= array.GetArrayElementAtIndex(i).objectReferenceValue == prefab;
                    if (has)
                        continue;
                    array.arraySize++;
                    array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = prefab;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            Save(root, path);
        }
    }
}
