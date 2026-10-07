using Bouncer.Core;
using Bouncer.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Bouncer.EditorTools
{
    static partial class CardContentBuilder
    {
        static readonly Color Wood = new(0.73f, 0.53f, 0.34f);
        static readonly Color WoodDark = new(0.45f, 0.3f, 0.18f);
        static readonly Color Dust = new(0.78f, 0.72f, 0.62f, 0.8f);

        static void BuildEffects()
        {
            // всплески частиц
            Burst("FenceSplinters", 1f, ("Splinters", Wood, WoodDark, 10, 4.5f, 0.7f, 0.12f, 1.5f));
            Burst("FenceDust", 1.2f, ("Dust", Dust, new Color(0.9f, 0.86f, 0.78f, 0.6f), 14, 1.2f, 0.9f, 0.5f, -0.05f));
            Burst("BubblePopBurst", 0.8f, ("Drops", new Color(0.8f, 0.95f, 1f, 0.9f), new Color(1f, 0.85f, 1f, 0.9f), 12, 2.5f, 0.45f, 0.09f, 0.6f));
            Burst("CapGunSmoke", 1.4f,
                ("Smoke", new Color(0.7f, 0.72f, 0.78f, 0.85f), new Color(0.55f, 0.57f, 0.65f, 0.7f), 10, 1.4f, 1.1f, 0.45f, -0.08f),
                ("Caps", new Color(0.92f, 0.2f, 0.18f), new Color(1f, 0.55f, 0.3f), 10, 3.5f, 0.8f, 0.07f, 1.2f),
                ("Flash", new Color(1f, 0.95f, 0.6f), new Color(1f, 0.8f, 0.3f), 4, 0.5f, 0.12f, 0.6f, 0f));
            Burst("Dazzle", 1f, ("Stars", new Color(1f, 0.95f, 0.5f), Color.white, 6, 1.2f, 0.6f, 0.14f, -0.1f));
            Burst("MagnetSparks", 1f,
                ("Red", new Color(0.95f, 0.25f, 0.2f), new Color(1f, 0.5f, 0.4f), 8, 3f, 0.5f, 0.08f, 0f),
                ("Blue", new Color(0.3f, 0.55f, 1f), new Color(0.6f, 0.8f, 1f), 8, 3f, 0.5f, 0.08f, 0f));
            Burst("GumPopBurst", 1f, ("Gum", new Color(1f, 0.5f, 0.75f), new Color(1f, 0.7f, 0.85f), 14, 3.5f, 0.6f, 0.12f, 1.4f));
            var pixels = Burst("PixelBurst", 1.2f, ("Pixels", new Color(0.25f, 0.35f, 0.2f), new Color(0.65f, 0.8f, 0.45f), 18, 2.5f, 0.9f, 0.1f, 0.4f));

            // кольца
            Ring("CapGunCone", new Color(1f, 0.92f, 0.7f, 0.9f), 0.3f, 0.25f, 0.05f, arc: 76f);
            Ring("MagnetPull", new Color(0.95f, 0.45f, 0.45f, 0.9f), 0.45f, 0.08f, 0.3f, inward: true);
            Ring("PassRing", Color.white, 0.4f, 0.18f, 0.04f);
            Ring("HandRing", Color.white, 0.6f, 0.6f, 0.4f, material: s_hands);
            Ring("SeaRing", new Color(0.6f, 0.85f, 1f, 0.95f), 0.7f, 0.3f, 0.05f);

            // меловые надписи над головой: раз, два, три!, БАХ!, ЗАМРИ!, сердечко
            var popup = Quad("ChalkPopup", null, s_popup, Vector3.zero, Vector3.zero, Vector3.one);
            var bill = popup.AddComponent<BillboardPopup>();
            SetArray(bill, "frames", new Object[] { Tex("T_Popup_0"), Tex("T_Popup_1"), Tex("T_Popup_2"), Tex("T_Popup_3"), Tex("T_Popup_4"), Tex("T_Popup_5") });
            SetFloat(bill, "size", 1.1f);
            popup.transform.localScale = new Vector3(2f, 1f, 1f);
            SavePrefab(popup, "ChalkPopup");

            BuildFence();
            BuildBubble();
            BuildProps();
            BuildCameraFx();
            BuildMarkAndBand();
            BuildChalkMark();
        }

        static void BuildFence()
        {
            var root = new GameObject("FenceWall") { layer = Layers.Player };
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(3.3f, 1.3f, 0.3f);
            box.center = new Vector3(0f, 0.65f, 0f);
            var wall = root.AddComponent<FenceWall>();
            var planksRoot = new GameObject("Planks").transform;
            planksRoot.SetParent(root.transform, false);
            var planks = new System.Collections.Generic.List<Object>();
            const int count = 17;
            for (int i = 0; i < count; i++)
            {
                var plank = Model("Prop_FencePlank", planksRoot, new Vector3(-1.52f + i * 0.19f, 0f, 0f));
                plank.transform.localEulerAngles = new Vector3(0f, Random.Range(-3f, 3f), Random.Range(-2f, 2f));
                planks.Add(plank.transform);
            }
            foreach (float y in new[] { 0.32f, 0.82f })
                planks.Add(Model("Prop_FenceRail", planksRoot, new Vector3(0f, y, 0.06f)).transform);
            SetArray(wall, "planks", planks);

            var outlineGo = new GameObject("Outline");
            outlineGo.transform.SetParent(root.transform, false);
            var outline = AddLine(outlineGo, s_chalkLine, 0.06f, loop: true, world: false);
            outline.alignment = LineAlignment.TransformZ;
            outlineGo.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
            outline.positionCount = 4;
            outline.SetPositions(new[] { new Vector3(-1.7f, -0.25f, -0.03f), new Vector3(1.7f, -0.25f, -0.03f), new Vector3(1.7f, 0.25f, -0.03f), new Vector3(-1.7f, 0.25f, -0.03f) });
            outline.startColor = outline.endColor = Chalk;
            Set(wall, "outline", outline);

            var dust = Particles("RiseDust", root.transform, Dust, new Color(0.9f, 0.86f, 0.78f, 0.6f), 26, 1.4f, 0.8f, 0.45f, -0.05f);
            var shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(3.3f, 0.1f, 0.4f);
            Set(wall, "riseDust", dust);
            Set(wall, "splinters", Component<ParticleBurst>("FenceSplinters"));
            SavePrefab(root, "FenceWall");
        }

        static void BuildBubble()
        {
            var root = new GameObject("SoapBubble");
            var bubble = root.AddComponent<SoapBubble>();
            var body = Sphere("Body", root.transform, s_bubble, Vector3.zero, 1f);
            Set(bubble, "body", body.transform);
            Set(bubble, "popBurst", Component<ParticleBurst>("BubblePopBurst"));
            SavePrefab(root, "SoapBubble");
        }

        static AbilityProp Prop(string name, string model, Vector3 modelOffset, float life, float bob, float spin, float recoil,
            float modelScale = 1f, Vector3? modelEuler = null)
        {
            var root = new GameObject(name);
            var go = Model(model, root.transform, modelOffset, modelScale);
            if (modelEuler.HasValue)
                go.transform.localEulerAngles = modelEuler.Value;
            var prop = root.AddComponent<AbilityProp>();
            SetFloat(prop, "life", life);
            SetFloat(prop, "bob", bob);
            SetFloat(prop, "spin", spin);
            SetFloat(prop, "recoil", recoil);
            return SavePrefab(root, name).GetComponent<AbilityProp>();
        }

        static void BuildProps()
        {
            Prop("CapGunProp", "Prop_CapGun", Vector3.zero, 0.6f, 0f, 0f, 0.08f, 1.6f);
            Prop("CameraProp", "Prop_CameraSmena", Vector3.zero, 0.7f, 0f, 0f, 0.04f, 1.6f);
            Prop("MagnetProp", "Prop_Magnet", Vector3.zero, 0.9f, 0.06f, 0f, 0f, 2.2f, new Vector3(180f, 0f, 0f));
            Prop("CassetteProp", "Prop_Cassette", Vector3.zero, 0.7f, 0.04f, 0f, 0f, 2.4f);
            Prop("WalkieProp", "Prop_Walkie", new Vector3(0f, -0.15f, 0f), 0.8f, 0.02f, 0f, 0f, 1.5f);

            var spiralRoot = new GameObject("SpinSpiral");
            Quad("Spiral", spiralRoot.transform, s_spiral, Vector3.zero, new Vector3(90f, 0f, 0f), Vector3.one * 2.6f);
            var spiral = spiralRoot.AddComponent<AbilityProp>();
            SetFloat(spiral, "life", 1.6f);
            SetFloat(spiral, "bob", 0f);
            SetFloat(spiral, "spin", -420f);
            SavePrefab(spiralRoot, "SpinSpiral");
        }

        static void BuildCameraFx()
        {
            var root = new GameObject("CameraFlash");
            var flash = root.AddComponent<CameraFlash>();
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(root.transform, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 80f;
            light.innerSpotAngle = 40f;
            light.color = new Color(1f, 0.98f, 0.92f);
            light.shadows = LightShadows.None;
            light.enabled = false;
            Set(flash, "flash", light);
            SetFloat(flash, "intensity", 70f);
            SavePrefab(root, "CameraFlash");

            var photo = Quad("PhotoPrint", null, s_photo, Vector3.zero, Vector3.zero, new Vector3(0.34f, 0.41f, 1f));
            photo.AddComponent<PhotoPrint>();
            SavePrefab(photo, "PhotoPrint");
        }

        static void BuildMarkAndBand()
        {
            var root = new GameObject("TargetMark");
            var view = root.AddComponent<TargetMarkView>();
            var circleGo = new GameObject("Circle");
            circleGo.transform.SetParent(root.transform, false);
            var line = AddLine(circleGo, s_chalkLine, 0.07f, loop: true, world: false);
            line.alignment = LineAlignment.View;
            var circle = circleGo.AddComponent<CircleLine>();
            circle.Radius = 0.9f;
            Set(view, "circle", circle);
            var waves = Quad("Waves", root.transform, s_waves, new Vector3(0f, 2f, 0f), Vector3.zero, Vector3.one * 0.9f);
            Set(view, "waves", waves.transform);
            SavePrefab(root, "TargetMark");

            var bandGo = new GameObject("ElasticBand");
            var band = AddLine(bandGo, s_elastic, 0.09f);
            band.textureMode = LineTextureMode.Tile;
            band.alignment = LineAlignment.View;
            bandGo.AddComponent<ElasticBand>();
            SavePrefab(bandGo, "ElasticBand");
        }

        static void BuildChalkMark()
        {
            var root = new GameObject("ChalkMark");
            var mark = root.AddComponent<ChalkMark>();
            Quad("Cross", root.transform, s_chalkCross, Vector3.zero, new Vector3(90f, 0f, 0f), Vector3.one);
            var dust = Particles("Dust", root.transform, new Color(1f, 1f, 1f, 0.7f), new Color(0.9f, 0.9f, 0.88f, 0.5f), 8, 0.8f, 0.6f, 0.25f, -0.05f, 0.3f);
            Set(mark, "dust", dust);
            SavePrefab(root, "ChalkMark");
        }
    }
}
