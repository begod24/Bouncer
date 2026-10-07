using System.Collections.Generic;
using System.Linq;
using Bouncer.Audio;
using Bouncer.Balls;
using Bouncer.Core;
using Bouncer.Player;
using Bouncer.Upgrades;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Bouncer.EditorTools
{
    static partial class CardContentBuilder
    {
        const string BallDataDir = Root + "Data/Balls/";
        const string BallPrefabDir = Root + "Prefabs/Balls/";
        const string AbilityDir = Root + "Data/Abilities/";
        const string CardDir = Root + "Data/Upgrades/";
        const string IconDir = Root + "Art/UI/Icons/";

        // ------------------------------------------------------------------ balls
        static readonly string[] AllBalls =
        {
            Root + "Prefabs/Ball.prefab", BallPrefabDir + "Ball_Tennis.prefab", BallPrefabDir + "Ball_Volleyball.prefab",
            BallPrefabDir + "Ball_Medicine.prefab", BallPrefabDir + "Ball_Deflated.prefab", BallPrefabDir + "Ball_Basketball.prefab",
            BallPrefabDir + "Ball_PingPong.prefab", BallPrefabDir + "Ball_Football.prefab",
        };

        static BallDefinition BallData(string name, System.Action<SerializedObject> edit)
        {
            string path = BallDataDir + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<BallDefinition>(path) == null)
                AssetDatabase.CopyAsset(BallDataDir + "Ball_Default.asset", path);
            var data = AssetDatabase.LoadAssetAtPath<BallDefinition>(path);
            var so = new SerializedObject(data);
            edit(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }

        static void F(SerializedObject so, string field, float value)
        {
            var p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogError("[Cards] Нет поля мяча " + field);
                return;
            }
            if (p.propertyType == SerializedPropertyType.Boolean)
                p.boolValue = value > 0.5f;
            else if (p.propertyType == SerializedPropertyType.Integer)
                p.intValue = Mathf.RoundToInt(value);
            else
                p.floatValue = value;
        }

        static void BallPrefab(string name, BallDefinition data, string model)
        {
            string path = BallPrefabDir + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                AssetDatabase.CopyAsset(BallPrefabDir + "Ball_Tennis.prefab", path);
            var root = PrefabUtility.LoadPrefabContents(path);
            var mesh = root.transform.Find("Mesh");
            var fbx = Load<GameObject>(Root + "Art/Models/Balls/" + model + ".fbx");
            mesh.GetComponent<MeshFilter>().sharedMesh = fbx.GetComponentInChildren<MeshFilter>().sharedMesh;
            Set(root.GetComponent<Ball>(), "definition", data);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void BuildBalls()
        {
            var area = Load<BallDefinition>(BallDataDir + "Ball_Medicine.asset").areaEffect;
            var landing = Load<GameObject>(Root + "Prefabs/VFX/LandingDust.prefab");
            var basket = BallData("Ball_Basketball", so =>
            {
                F(so, "radius", 0.26f); F(so, "mass", 0.6f);
                F(so, "speed", 13f); F(so, "chargedSpeed", 17f);
                F(so, "upVelocity", 7f); F(so, "chargedUpVelocity", 7.5f);
                F(so, "liveGravity", 15f); F(so, "chargedLiveGravity", 13f);
                F(so, "damage", 1); F(so, "chargedDamage", 2);
                F(so, "maxLiveTime", 3f);
                F(so, "perks.lob", 1); F(so, "perks.areaRadius", 1.8f); F(so, "perks.areaDamage", 1);
                so.FindProperty("areaEffect").objectReferenceValue = area;
                so.FindProperty("landEffect").objectReferenceValue = landing;
            });
            var ping = BallData("Ball_PingPong", so =>
            {
                F(so, "radius", 0.11f); F(so, "mass", 0.05f);
                F(so, "speed", 26f); F(so, "chargedSpeed", 36f);
                F(so, "damage", 1); F(so, "chargedDamage", 2);
                F(so, "liveGravity", 1.2f); F(so, "chargedLiveGravity", 0.6f);
                F(so, "maxRicochets", 6); F(so, "wallSpeedKeep", 1f); F(so, "minLiveSpeed", 6f); F(so, "maxLiveTime", 3.5f);
                F(so, "perks.bounceAssist", 35f);
            });
            var foot = BallData("Ball_Football", so =>
            {
                F(so, "radius", 0.22f); F(so, "mass", 0.45f);
                F(so, "speed", 17f); F(so, "chargedSpeed", 24f);
                F(so, "upVelocity", 0f); F(so, "chargedUpVelocity", 0f);
                F(so, "liveGravity", 0f); F(so, "chargedLiveGravity", 0f);
                F(so, "damage", 1); F(so, "chargedDamage", 2);
                F(so, "rollDrag", 6f); F(so, "maxLiveTime", 2.2f); F(so, "minLiveSpeed", 5f); F(so, "maxRicochets", 4);
                F(so, "perks.groundRoll", 1); F(so, "perks.grazeRadius", 0.55f); F(so, "perks.grazeStun", 0.7f);
            });
            BallPrefab("Ball_Basketball", basket, "Ball_Basketball");
            BallPrefab("Ball_PingPong", ping, "Ball_PingPong");
            BallPrefab("Ball_Football", foot, "Ball_Football");

            var chalk = Component<ChalkMark>("ChalkMark");
            foreach (string path in AllBalls)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                Set(root.GetComponent<Ball>(), "chalkMark", chalk);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ------------------------------------------------------------------ abilities
        static T Asset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static Sprite Hud(string name) => Load<Sprite>(Root + "Art/UI/HUD_Ability_" + name + ".png");

        static readonly List<AbilityDefinition> s_abilities = new();

        static T Ability<T>(string name, string hud, float cooldown) where T : AbilityDefinition
        {
            var ability = Asset<T>(AbilityDir + "Ability_" + name + ".asset");
            ability.cooldown = cooldown;
            ability.hudIcon = Hud(hud);
            s_abilities.Add(ability);
            EditorUtility.SetDirty(ability);
            return ability;
        }

        static void BuildAbilities()
        {
            System.IO.Directory.CreateDirectory(AbilityDir);
            AssetDatabase.Refresh();
            s_abilities.Clear();
            var fence = Ability<FenceAbility>("Fence", "Fence", 12f);
            Set(fence, "fence", Component<FenceWall>("FenceWall"));
            fence.redirect = false;
            var barricade = Ability<FenceAbility>("Barricade", "Barricade", 12f);
            Set(barricade, "fence", Component<FenceWall>("FenceWall"));
            barricade.redirect = true;
            var bubbles = Ability<BubblesAbility>("Bubbles", "Bubbles", 14f);
            Set(bubbles, "bubble", Component<SoapBubble>("SoapBubble"));
            var gun = Ability<CapGunAbility>("CapGun", "CapGun", 8f);
            Set(gun, "gun", Component<AbilityProp>("CapGunProp"));
            Set(gun, "smoke", Component<ParticleBurst>("CapGunSmoke"));
            Set(gun, "bang", Component<BillboardPopup>("ChalkPopup"));
            Set(gun, "cone", Component<ExpandingRing>("CapGunCone"));
            var camera = Ability<CameraAbility>("Camera", "Camera", 12f);
            Set(camera, "cameraProp", Component<AbilityProp>("CameraProp"));
            Set(camera, "flash", Component<CameraFlash>("CameraFlash"));
            Set(camera, "photo", Component<PhotoPrint>("PhotoPrint"));
            Set(camera, "dazzle", Component<ParticleBurst>("Dazzle"));
            var magnet = Ability<MagnetAbility>("Magnet", "Magnet", 10f);
            Set(magnet, "magnet", Component<AbilityProp>("MagnetProp"));
            Set(magnet, "pull", Component<ExpandingRing>("MagnetPull"));
            Set(magnet, "sparks", Component<ParticleBurst>("MagnetSparks"));
            Ability<RewindAbility>("Rewind", "Rewind", 16f);
            Ability<CarouselAbility>("Carousel", "Carousel", 20f);
            var elastic = Ability<ElasticAbility>("Elastic", "ElasticBand", 14f);
            Set(elastic, "band", Component<ElasticBand>("ElasticBand"));
            var walkie = Ability<WalkieAbility>("Walkie", "Walkie", 12f);
            Set(walkie, "walkie", Component<AbilityProp>("WalkieProp"));
            Set(walkie, "markView", Component<TargetMarkView>("TargetMark"));

            var catalog = Asset<AbilityCatalog>(AbilityDir + "AbilityCatalog.asset");
            foreach (var ability in s_abilities)
                if (!catalog.abilities.Contains(ability))
                    catalog.abilities.Add(ability);
            EditorUtility.SetDirty(catalog);
        }

        static AbilityDefinition AbilityAsset(string name) => Load<AbilityDefinition>(AbilityDir + "Ability_" + name + ".asset");

        // ------------------------------------------------------------------ cards
        struct Text
        {
            public string RuTitle, RuDescription, EnTitle, EnDescription;
        }

        static readonly Dictionary<string, Text> s_texts = new();
        static readonly List<UpgradeCard> s_cards = new();

        static T Card<T>(string asset, string key, string icon, UpgradeCategory category, CardRarity rarity, Color wrapper, Text text,
            bool coopOnly = false, params UpgradeCard[] requires) where T : UpgradeCard
        {
            var card = Asset<T>(CardDir + asset + ".asset");
            card.title = new LocalizedString("Content", "card." + key + ".title");
            card.description = new LocalizedString("Content", "card." + key + ".description");
            card.category = category;
            card.rarity = rarity;
            card.wrapperColor = wrapper;
            card.icon = Load<Sprite>(IconDir + "Icon_" + icon + ".png");
            card.maxStacks = 1;
            card.weight = 1f;
            card.coopOnly = coopOnly;
            card.requires = requires;
            s_texts["card." + key] = text;
            s_cards.Add(card);
            EditorUtility.SetDirty(card);
            return card;
        }

        static Text T(string ruTitle, string ruDescription, string enTitle, string enDescription) =>
            new() { RuTitle = ruTitle, RuDescription = ruDescription, EnTitle = enTitle, EnDescription = enDescription };

        static void BuildCards()
        {
            s_cards.Clear();
            s_texts.Clear();
            var active = new Color(0.98f, 0.78f, 0.35f);
            var coop = new Color(0.55f, 0.85f, 0.62f);

            var fence = Card<AbilityCard>("Card_Ability_Fence", "ability_fence", "Fence", UpgradeCategory.Passive, CardRarity.Rare, new Color(0.82f, 0.62f, 0.4f),
                T("Забор", "Q: перед тобой вырастает забор на 4 с. Чужие мячи отскакивают, свои пролетают.",
                  "Fence", "Q: a picket fence rises in front of you for 4 s. Enemy balls bounce off, yours fly through."));
            fence.ability = AbilityAsset("Fence");
            Card<AbilityCard>("Card_Ability_Bubbles", "ability_bubbles", "Bubbles", UpgradeCategory.Passive, CardRarity.Rare, new Color(0.7f, 0.85f, 0.98f),
                T("Мыльные пузыри", "Q: веер пузырей. Чужой мяч застревает в пузыре и падает ничьим.",
                  "Soap Bubbles", "Q: blow a fan of bubbles. An enemy ball gets stuck inside and drops harmless.")).ability = AbilityAsset("Bubbles");
            Card<AbilityCard>("Card_Ability_CapGun", "ability_capgun", "CapGun", UpgradeCategory.Passive, CardRarity.Common, new Color(0.92f, 0.45f, 0.38f),
                T("Пугач", "Q: хлопок пистоном. Враги в 4,5 м перед тобой вздрагивают, летящие в тебя мячи падают.",
                  "Cap Gun", "Q: a cap bang. Enemies within 4.5 m in front flinch, balls flying at you drop.")).ability = AbilityAsset("CapGun");
            Card<AbilityCard>("Card_Ability_Camera", "ability_camera", "Camera", UpgradeCategory.Passive, CardRarity.Rare, new Color(0.62f, 0.66f, 0.74f),
                T("Фотоаппарат «Смена»", "Q: вспышка! Враги в кадре замирают на 1,3 с, тени становятся плотными и получают урон.",
                  "«Smena» Camera", "Q: flash! Enemies in frame freeze for 1.3 s, shadows turn solid and take damage.")).ability = AbilityAsset("Camera");
            Card<AbilityCard>("Card_Ability_Magnet", "ability_magnet", "Magnet", UpgradeCategory.Passive, CardRarity.Common, new Color(0.95f, 0.5f, 0.5f),
                T("Магнит", "Q: ничьи мячи в 9 м летят к тебе в руки.",
                  "Magnet", "Q: loose balls within 9 m fly into your hands.")).ability = AbilityAsset("Magnet");
            Card<AbilityCard>("Card_Ability_Rewind", "ability_rewind", "Rewind", UpgradeCategory.Passive, CardRarity.Rare, new Color(0.6f, 0.5f, 0.42f),
                T("Перемотка", "Q: перемотай себя на 3 секунды назад. Пока плёнка крутится, тебя не задеть.",
                  "Rewind", "Q: rewind yourself 3 seconds back. Nothing can hit you while the tape spins.")).ability = AbilityAsset("Rewind");
            Card<AbilityCard>("Card_Ability_Carousel", "ability_carousel", "Carousel", UpgradeCategory.Passive, CardRarity.Gold, active,
                T("Карусель", "Q: кружишься 1,4 с. Мячи из рук разлетаются кольцом заряженными, чужие мячи рядом ловятся сами.",
                  "Merry-Go-Round", "Q: spin for 1.4 s. Your balls fly out in a charged ring, enemy balls nearby are caught.")).ability = AbilityAsset("Carousel");
            Card<AbilityCard>("Card_Ability_Elastic", "ability_elastic", "ElasticBand", UpgradeCategory.Passive, CardRarity.Rare, coop,
                T("Резиночка", "Q: натяни резинку до товарища на 5 с. Враги, перешагнувшие её, спотыкаются. Только вместе.",
                  "Jump Elastic", "Q: stretch an elastic to a teammate for 5 s. Enemies crossing it trip. Co-op only."), coopOnly: true).ability = AbilityAsset("Elastic");
            Card<AbilityCard>("Card_Ability_Walkie", "ability_walkie", "Walkie", UpgradeCategory.Passive, CardRarity.Rare, new Color(0.5f, 0.7f, 0.45f),
                T("Рация", "Q: отметь врага на 6 с. Мячи всех ребят доворачивают к нему и бьют на 1 сильнее.",
                  "Walkie-Talkie", "Q: mark an enemy for 6 s. Everyone's balls curve toward it and hit 1 harder.")).ability = AbilityAsset("Walkie");

            var count = Card<StatCard>("Card_Pas_CountRhyme", "pas_countrhyme", "CountRhyme", UpgradeCategory.Passive, CardRarity.Common, new Color(0.98f, 0.88f, 0.45f),
                T("Считалочка", "Раз, два, три! Каждый третий бросок сам заряжен и бьёт на 1 сильнее.",
                  "Counting Rhyme", "One, two, three! Every third throw is charged and hits 1 harder."));
            count.countEvery = 3;
            count.countBonus = 1;
            var chalk = Card<BallPerkCard>("Card_Perk_Chalk", "perk_chalk", "Chalk", UpgradeCategory.Modifier, CardRarity.Common, new Color(0.9f, 0.9f, 0.86f),
                T("Мел", "Твой мяч, коснувшись асфальта, рисует меловой крестик. Наступил — бежишь на 30% быстрее.",
                  "Chalk", "Your ball draws a chalk cross where it lands. Step on it to run 30% faster."));
            chalk.perks = new BallPerks { chalk = true };
            Card<StatCard>("Card_Pas_GumBubble", "pas_gumbubble", "GumBubble", UpgradeCategory.Passive, CardRarity.Rare, new Color(0.98f, 0.6f, 0.8f),
                T("Жвачный пузырь", "Раз в 15 с пузырь принимает чужой мяч: мяч падает у ног, враги рядом вязнут.",
                  "Bubble Gum", "Every 15 s a gum bubble takes an enemy ball: it drops at your feet, enemies nearby get stuck.")).gumBubbleCooldown = 15f;
            Card<StatCard>("Card_Pas_Tamagotchi", "pas_tamagotchi", "Tamagotchi", UpgradeCategory.Passive, CardRarity.Rare, new Color(0.95f, 0.7f, 0.85f),
                T("Тамагочи", "Корми питомца ловлей: после 5 пойманных мячей сытый тамагочи спасёт от выбивания.",
                  "Tamagotchi", "Feed your pet by catching: after 5 catches a full tamagotchi saves you from being knocked out.")).tamagotchi = true;
            Card<StatCard>("Card_Pas_Pass", "pas_pass", "Pass", UpgradeCategory.Passive, CardRarity.Common, coop,
                T("Пас", "Мяч, пойманный у товарища, сразу заряжен. Только вместе.",
                  "Pass", "A ball caught from a teammate is charged right away. Co-op only."), coopOnly: true).passCharge = true;
            Card<StatCard>("Card_Pas_CircleGuard", "pas_circleguard", "CircleGuard", UpgradeCategory.Passive, CardRarity.Rare, coop,
                T("Круговая порука", "Подняв товарища, замораживаешь врагов вокруг на 1,5 с, и тебя секунду не задеть. Только вместе.",
                  "All for One", "Reviving a teammate freezes enemies around for 1.5 s and makes you untouchable for a second. Co-op only."),
                coopOnly: true).circleGuard = true;

            var lid = Load<UpgradeCard>(CardDir + "Card_Pas_Lid.asset");
            var whistle = Load<UpgradeCard>(CardDir + "Card_Pas_Whistle.asset");
            var freeze = Load<UpgradeCard>(CardDir + "Card_Pas_Freeze.asset");
            Card<AbilityCard>("Card_Combo_Barricade", "combo_barricade", "Barricade", UpgradeCategory.Passive, CardRarity.Gold, new Color(0.75f, 0.62f, 0.5f),
                T("Баррикада", "Q: забор с крышкой от кастрюли. Отбитый мяч летит вперёд твоим броском.",
                  "Barricade", "Q: a fence with a pot lid. Blocked balls fly forward as your own throws."), false, lid, fence).ability = AbilityAsset("Barricade");
            var sea = Card<BallPerkCard>("Card_Combo_SeaFigure", "combo_seafigure", "SeaFigure", UpgradeCategory.Modifier, CardRarity.Gold, new Color(0.5f, 0.78f, 0.95f),
                T("Морская фигура", "«Море волнуется — замри!» Свисток и «Замри!» держат в полтора раза дольше, а по замершим твои мячи бьют на 1 сильнее.",
                  "Statues", "Whistle and Freeze! hold enemies 1.5× longer, and your balls hit frozen enemies 1 harder."), false, whistle, freeze);
            sea.perks = new BallPerks { frozenBonus = 1 };

            Card<BallTypeCard>("Card_Ball_Basketball", "ball_basketball", "Ball_Basketball", UpgradeCategory.Ball, CardRarity.Rare, new Color(0.95f, 0.55f, 0.2f),
                T("Баскетбольный мяч", "Летит навесом над головами и щитами, бьёт на излёте и по площади, когда падает.",
                  "Basketball", "Lobbed over heads and shields; hits on the way down and splashes where it lands.")).ballPrefab =
                Load<GameObject>(BallPrefabDir + "Ball_Basketball.prefab").GetComponent<Ball>();
            Card<BallTypeCard>("Card_Ball_PingPong", "ball_pingpong", "Ball_PingPong", UpgradeCategory.Ball, CardRarity.Common, new Color(0.98f, 0.92f, 0.8f),
                T("Пинг-понг", "Крошечный и быстрый: урон 1, но отскакивает до 6 раз и после стены доворачивает к врагу.",
                  "Ping-Pong Ball", "Tiny and fast: 1 damage, but bounces up to 6 times and curves toward enemies off walls.")).ballPrefab =
                Load<GameObject>(BallPrefabDir + "Ball_PingPong.prefab").GetComponent<Ball>();
            Card<BallTypeCard>("Card_Ball_Football", "ball_football", "Ball_Football", UpgradeCategory.Ball, CardRarity.Rare, new Color(0.85f, 0.88f, 0.9f),
                T("Футбольный мяч", "Пинок по асфальту: мяч катится и сбивает с ног всех на пути.",
                  "Football", "A kick along the ground: the ball rolls and knocks down everyone in its path.")).ballPrefab =
                Load<GameObject>(BallPrefabDir + "Ball_Football.prefab").GetComponent<Ball>();

            var deck = Load<CardDeck>(CardDir + "CardDeck_Default.asset");
            var flashlight = AssetDatabase.LoadAssetAtPath<UpgradeCard>(CardDir + "Card_Pas_Flashlight.asset");
            deck.deck.RemoveAll(card => card == null || card == flashlight);
            foreach (var card in s_cards)
                if (!deck.deck.Contains(card))
                    deck.deck.Add(card);
            EditorUtility.SetDirty(deck);
            if (flashlight != null)
                AssetDatabase.DeleteAsset(CardDir + "Card_Pas_Flashlight.asset");

            WriteTexts();
        }

        static void WriteTexts()
        {
            var content = LocalizationEditorSettings.GetStringTableCollection("Content");
            foreach (var pair in s_texts)
            {
                Entry(content, pair.Key + ".title", pair.Value.RuTitle, pair.Value.EnTitle);
                Entry(content, pair.Key + ".description", pair.Value.RuDescription, pair.Value.EnDescription);
            }
            var ui = LocalizationEditorSettings.GetStringTableCollection("UI");
            Entry(ui, "pockets.tag.swap", "заменит умение", "replaces ability");
            Entry(ui, "card.category.ability", "умение", "ability");
            foreach (var collection in new[] { content, ui })
            {
                EditorUtility.SetDirty(collection.SharedData);
                foreach (var table in collection.StringTables)
                    EditorUtility.SetDirty(table);
            }
        }

        static void Entry(StringTableCollection collection, string key, string ru, string en)
        {
            if (collection.SharedData.GetEntry(key) == null)
                collection.SharedData.AddKey(key);
            foreach (var table in collection.StringTables)
            {
                string code = table.LocaleIdentifier.Code;
                if (code == "ru")
                    table.AddEntry(key, ru);
                else if (code == "en")
                    table.AddEntry(key, en);
            }
        }

        // ------------------------------------------------------------------ player, network, HUD, sounds
        static T Ensure<T>(GameObject go) where T : Component => go.TryGetComponent(out T c) ? c : go.AddComponent<T>();

        static Transform Child(Transform parent, string name)
        {
            var found = parent.Find(name);
            if (found != null)
                Object.DestroyImmediate(found.gameObject);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void WirePlayer()
        {
            const string path = Root + "Prefabs/Player.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var t = root.transform;

            Set(Ensure<PlayerAbilities>(root), "catalog", Load<AbilityCatalog>(AbilityDir + "AbilityCatalog.asset"));

            var rewind = Ensure<PlayerRewind>(root);
            var tape = AddLine(Child(t, "RewindTape").gameObject, s_tape, 0.12f);
            tape.enabled = false;
            Set(rewind, "tape", tape);
            Set(rewind, "cassette", Component<AbilityProp>("CassetteProp"));

            var spin = Ensure<PlayerSpin>(root);
            var blurGo = Child(t, "SpinBlur");
            blurGo.localPosition = new Vector3(0f, 0.9f, 0f);
            AddLine(blurGo.gameObject, s_chalkLine, 0.18f, world: false);
            var blur = blurGo.gameObject.AddComponent<CircleLine>();
            blur.Radius = 0.9f;
            blur.Line.enabled = false;
            Set(spin, "blur", blur);
            Set(spin, "spiral", Component<AbilityProp>("SpinSpiral"));

            var fx = Ensure<PlayerCardFx>(root);
            var oldBubble = t.Find("GumBubble");
            if (oldBubble != null)
                Object.DestroyImmediate(oldBubble.gameObject);
            var bubble = Sphere("GumBubble", t, s_gumBubble, new Vector3(0f, 1.15f, 0.42f), 0.3f);
            bubble.SetActive(false);
            Set(fx, "gumBubble", bubble.transform);
            Set(fx, "gumPop", Component<ParticleBurst>("GumPopBurst"));
            Set(fx, "gumSpot", Load<GameObject>(Root + "Prefabs/VFX/GumSpot.prefab").GetComponent<GumSpot>());

            var tama = Child(t, "Tamagotchi");
            const float tamaScale = 2.6f;
            var model = Model("Prop_Tamagotchi", tama, Vector3.zero, tamaScale);
            model.transform.localEulerAngles = new Vector3(0f, 180f, 0f);
            var screen = Quad("Screen", tama, s_screen, new Vector3(0f, 0.095f * tamaScale, -0.0405f * tamaScale), Vector3.zero,
                new Vector3(0.072f * tamaScale, 0.056f * tamaScale, 1f));
            tama.gameObject.SetActive(false);
            Set(fx, "tamagotchi", tama);
            Set(fx, "tamagotchiScreen", screen.GetComponent<MeshRenderer>());
            SetArray(fx, "tamagotchiFaces", new Object[] { Tex("T_Tamagotchi_0"), Tex("T_Tamagotchi_1"), Tex("T_Tamagotchi_2") });
            Set(fx, "pixelBurst", Component<ParticleBurst>("PixelBurst"));
            Set(fx, "chalkPopup", Component<BillboardPopup>("ChalkPopup"));
            Set(fx, "passRing", Component<ExpandingRing>("PassRing"));
            Set(fx, "handRing", Component<ExpandingRing>("HandRing"));
            Set(fx, "seaRing", Component<ExpandingRing>("SeaRing"));

            var trailGo = Child(t, "ChalkTrail");
            trailGo.localPosition = new Vector3(0f, 0.15f, 0f);
            var trail = trailGo.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = s_chalkLine;
            trail.time = 0.35f;
            trail.widthMultiplier = 0.3f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.startColor = new Color(1f, 1f, 1f, 0.7f);
            trail.endColor = new Color(1f, 1f, 1f, 0f);
            trail.emitting = false;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Set(fx, "chalkTrail", trail);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void WireNetwork()
        {
            const string path = Root + "Prefabs/Net/NetRoom.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            foreach (var behaviour in root.GetComponents<MonoBehaviour>())
            {
                string type = behaviour.GetType().Name;
                if (type == "NetBalls")
                    AppendUnique(behaviour, "prefabs", BallPrefabDir + "Ball_Basketball.prefab", BallPrefabDir + "Ball_PingPong.prefab",
                        BallPrefabDir + "Ball_Football.prefab");
                else if (type == "NetWorld")
                    AppendUnique(behaviour, "effects", PrefabDir + "ChalkMark.prefab");
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void AppendUnique(Object target, string field, params string[] prefabPaths)
        {
            var so = new SerializedObject(target);
            var array = so.FindProperty(field);
            foreach (string prefabPath in prefabPaths)
            {
                var go = Load<GameObject>(prefabPath);
                Object value = array.arrayElementType.Contains("Ball") ? go.GetComponent<Ball>() : go;
                bool has = false;
                for (int i = 0; i < array.arraySize; i++)
                    has |= array.GetArrayElementAtIndex(i).objectReferenceValue == value;
                if (has)
                    continue;
                array.arraySize++;
                array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireHud()
        {
            const string path = Root + "Prefabs/UI/GameUI.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var hud = root.GetComponentsInChildren<MonoBehaviour>(true).First(b => b.GetType().Name == "GameHud");
            var so = new SerializedObject(hud);
            var lid = so.FindProperty("lidIcon").objectReferenceValue as Image;
            var abilityProperty = so.FindProperty("abilityIcon");
            var old = abilityProperty.objectReferenceValue as Image;
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var copy = Object.Instantiate(lid.gameObject, lid.transform.parent);
            copy.name = "AbilityIcon";
            var rect = (RectTransform)copy.transform;
            rect.anchoredPosition += new Vector2(((RectTransform)lid.transform).rect.width + 14f, 0f);
            var icon = copy.GetComponent<Image>();
            icon.sprite = Hud("Fence");
            abilityProperty.objectReferenceValue = icon;
            so.ApplyModifiedPropertiesWithoutUndo();

            var canvas = root.GetComponentInChildren<Canvas>(true).rootCanvas.transform;
            var existing = canvas.Find("RewindOverlay");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);
            var overlay = new GameObject("RewindOverlay", typeof(RectTransform), typeof(RawImage));
            overlay.transform.SetParent(canvas, false);
            var overlayRect = (RectTransform)overlay.transform;
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
            var raw = overlay.GetComponent<RawImage>();
            raw.texture = Tex("T_VHS");
            raw.color = new Color(1f, 1f, 1f, 0f);
            raw.uvRect = new Rect(0f, 0f, 6f, 3f);
            raw.raycastTarget = false;
            raw.enabled = false;
            overlay.AddComponent<Bouncer.UI.RewindOverlay>();
            overlay.transform.SetAsLastSibling();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void WireSounds()
        {
            var bank = Load<SoundBank>(Root + "Data/Audio/SoundBank.asset");
            var cues = new (SoundCue cue, string clip, float volume, bool ui)[]
            {
                (SoundCue.FenceRise, "FenceRise", 0.75f, false), (SoundCue.FenceKnock, "FenceKnock", 0.7f, false),
                (SoundCue.BubbleBlow, "BubbleBlow", 0.6f, false), (SoundCue.BubblePop, "BubblePop", 0.55f, false),
                (SoundCue.CapGun, "CapGun", 0.85f, false), (SoundCue.CameraFlash, "CameraFlash", 0.75f, false),
                (SoundCue.MagnetPull, "MagnetPull", 0.6f, false), (SoundCue.Rewind, "Rewind", 0.7f, false),
                (SoundCue.ElasticTwang, "ElasticTwang", 0.65f, false), (SoundCue.RadioCrackle, "RadioCrackle", 0.6f, false),
                (SoundCue.TamagotchiBeep, "TamagotchiBeep", 0.55f, false), (SoundCue.ChalkScribble, "ChalkScribble", 0.45f, false),
                (SoundCue.SpinWhoosh, "SpinWhoosh", 0.7f, false), (SoundCue.AbilityNotReady, "AbilityNotReady", 0.5f, true),
            };
            foreach (var (cue, clip, volume, ui) in cues)
            {
                bank.entries.RemoveAll(e => e.cue == cue);
                bank.entries.Add(new SoundBank.Entry
                {
                    cue = cue,
                    clips = new[] { Load<AudioClip>(Root + "Audio/SFX/" + clip + ".wav") },
                    volume = volume,
                    pitchJitter = 0.06f,
                    ui = ui,
                    minInterval = 0.05f,
                });
            }
            EditorUtility.SetDirty(bank);
        }
    }
}
