using System.Collections.Generic;
using Bouncer.Balls;
using Bouncer.Player;
using Bouncer.Upgrades;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;

namespace Bouncer.EditorTools
{
    // Колода 2026-10-12: убранные карточки, цифры старых карточек, правила колоды и тексты ВСЕХ карточек.
    // Тексты карточек живут только здесь — простыми словами, ru и en.
    // Меню Bouncer → Cards → Apply Deck & Texts. Build Card Content вызывает его в конце. Перезапускаемый.
    static class CardDeckBuilder
    {
        const string CardDir = "Assets/_Project/Data/Upgrades/";
        const string BallDir = "Assets/_Project/Data/Balls/";
        const string IconDir = "Assets/_Project/Art/UI/Icons/";

        /// <summary>Убраны из колоды 2026-10-12 (Свисток вошёл в «Замри!»).</summary>
        public static readonly string[] Removed =
        {
            "Card_Perk_Chalk", "Card_Pas_Boots", "Card_Pas_GumBubble", "Card_Pas_Mirror", "Card_Pas_Pass",
            "Card_Pas_CheatSheet", "Card_Pas_Whistle",
        };

        static readonly string[] RemovedKeys =
        {
            "card.perk_chalk", "card.pas_boots", "card.pas_gumbubble", "card.pas_mirror", "card.pas_pass",
            "card.pas_cheatsheet", "card.pas_whistle", "card.pas_flashlight",
        };

        [MenuItem("Bouncer/Cards/Apply Deck & Texts")]
        public static void Apply()
        {
            ApplyNumbers();
            ApplyBalls();
            ApplyCombos();
            ApplyDeck();
            WriteTexts();
            AssetDatabase.SaveAssets();
            Debug.Log("[Cards] Колода и тексты карточек обновлены.");
        }

        // ------------------------------------------------------------------ numbers
        static void ApplyNumbers()
        {
            Card<StatCard>("Card_Pas_Speed", c => c.moveSpeed = 0.1f);       // ×3 = +30%
            Card<StatCard>("Card_Pas_Pickup", c => c.pickupRadius = 0.3f);   // ×2 = +60%
            Card<StatCard>("Card_Pas_Catch", c => c.catchWindow = 0.35f / 3f); // ×3 = +35%
            Card<StatCard>("Card_Pas_Pies", c =>
            {
                c.maxStacks = 1;
                c.maxLives = 1;
                c.heal = 1;
            });
            Card<StatCard>("Card_Pas_Freeze", c =>
            {
                c.rarity = CardRarity.Rare;
                c.maxStacks = 2;
                c.whistleFreeze = 1.5f;
                var icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/Icons/Icon_FreezeWhistle.png");
                if (icon != null)
                    c.icon = icon;
            });
            // Свисток вошёл в «Замри!»: Морская фигура теперь «Замри!» + Домино
            var freeze = AssetDatabase.LoadAssetAtPath<UpgradeCard>(CardDir + "Card_Pas_Freeze.asset");
            var domino = AssetDatabase.LoadAssetAtPath<UpgradeCard>(CardDir + "Card_Pas_Domino.asset");
            Card<UpgradeCard>("Card_Combo_SeaFigure", c => c.requires = new[] { freeze, domino });
            Card<StatCard>("Card_Pas_SecondWind", c =>
            {
                c.secondWind = true;
                var icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/Icons/Icon_SecondWindUp.png");
                if (icon != null)
                    c.icon = icon;
            });
        }

        static Sprite Icon(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(IconDir + "Icon_" + name + ".png");

        static BallDefinition BallData(string name) => AssetDatabase.LoadAssetAtPath<BallDefinition>(BallDir + name + ".asset");

        static UpgradeCard Load(string name) => AssetDatabase.LoadAssetAtPath<UpgradeCard>(CardDir + name + ".asset");

        // ------------------------------------------------------------------ cards × balls
        static void ApplyBalls()
        {
            var football = BallData("Ball_Football");
            var medicine = BallData("Ball_Medicine");
            var basketball = BallData("Ball_Basketball");
            // спят: футбольный не касается асфальта и и так проходит сквозь врагов; у тяжёлых почти нет рикошетов
            Card<UpgradeCard>("Card_Mod_Bouncy", c => c.sleepsWith = new[] { football });
            Card<UpgradeCard>("Card_Mod_Slingshot", c => c.sleepsWith = new[] { football });
            Card<UpgradeCard>("Card_Mod_Wall", c => c.sleepsWith = new[] { medicine, basketball });
            // «Хулиганство» с теннисными забирает 2 мяча (иначе почти вдвое больше урона за ту же цену)
            var tennis = BallData("Ball_Tennis");
            tennis.fewerBallsScale = 2;
            EditorUtility.SetDirty(tennis);
        }

        // ------------------------------------------------------------------ combos
        static readonly List<UpgradeCard> s_combos = new();

        static T Combo<T>(string asset, string key, string icon, UpgradeCategory category, Color wrapper, params string[] parts)
            where T : UpgradeCard
        {
            string path = CardDir + asset + ".asset";
            var card = AssetDatabase.LoadAssetAtPath<T>(path);
            if (card == null)
            {
                card = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(card, path);
            }
            card.title = new UnityEngine.Localization.LocalizedString("Content", "card." + key + ".title");
            card.description = new UnityEngine.Localization.LocalizedString("Content", "card." + key + ".description");
            card.category = category;
            card.rarity = CardRarity.Gold;
            card.wrapperColor = wrapper;
            if (Icon(icon) is { } sprite)
                card.icon = sprite;
            card.maxStacks = 1;
            card.weight = 1f;
            var requires = new UpgradeCard[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                requires[i] = Load(parts[i]);
            card.requires = requires;
            EditorUtility.SetDirty(card);
            s_combos.Add(card);
            return card;
        }

        static void ApplyCombos()
        {
            s_combos.Clear();
            // комбо мячей: работают, только пока этот мяч в руках
            Combo<BallPerkCard>("Card_Combo_Hail", "combo_hail", "Hail", UpgradeCategory.Modifier, new Color(0.72f, 0.86f, 0.38f),
                "Card_Ball_Tennis", "Card_Mod_Split").perks = new BallPerks { twinSplit = true };
            Combo<BallPerkCard>("Card_Combo_Serve", "combo_serve", "Serve", UpgradeCategory.Modifier, new Color(0.4f, 0.62f, 0.9f),
                "Card_Ball_Volleyball", "Card_Mod_EagleEye").perks = new BallPerks { chainBounces = 2, chainRange = 4f };
            Combo<BallPerkCard>("Card_Combo_Kettlebell", "combo_kettlebell", "Kettlebell", UpgradeCategory.Modifier, new Color(0.55f, 0.55f, 0.6f),
                "Card_Ball_Medicine", "Card_Mod_Hooligan").perks = new BallPerks { areaStun = 1f };
            Combo<BallPerkCard>("Card_Combo_Dunk", "combo_dunk", "Dunk", UpgradeCategory.Modifier, new Color(0.95f, 0.55f, 0.2f),
                "Card_Ball_Basketball", "Card_Mod_Bouncy").perks = new BallPerks { dunkRange = 6f };
            Combo<BallPerkCard>("Card_Combo_Smash", "combo_smash", "Smash", UpgradeCategory.Modifier, new Color(0.95f, 0.85f, 0.7f),
                "Card_Ball_PingPong", "Card_Mod_Wall").perks = new BallPerks { smash = 5 };
            Combo<BallPerkCard>("Card_Combo_Penalty", "combo_penalty", "Penalty", UpgradeCategory.Modifier, new Color(0.6f, 0.78f, 0.5f),
                "Card_Ball_Football", "Card_Pas_Tackle").perks = new BallPerks { penalty = true };

            // остальные комбо
            var sun = Combo<BallPerkCard>("Card_Combo_Yoyo", "combo_yoyo", "Sun", UpgradeCategory.Modifier, new Color(0.98f, 0.75f, 0.3f),
                "Card_Mod_Boomerang", "Card_Mod_Elastic");
            sun.perks = new BallPerks { yoyo = true };
            var petard = Combo<BallPerkCard>("Card_Combo_BounceBomb", "combo_bouncebomb", "Petard", UpgradeCategory.Modifier,
                new Color(0.93f, 0.4f, 0.25f), "Card_Mod_HotPotato", "Card_Mod_Bouncy");
            petard.nextThrowAfterCatch = true;
            // вместе с отскоком Попрыгунчика — 3 взрыва
            petard.perks = new BallPerks { floorBounces = 2, bounceBlastRadius = 2.2f, bounceBlastDamage = 1 };
            var leapfrog = Combo<StatCard>("Card_Combo_Roll", "combo_roll", "Leapfrog", UpgradeCategory.Passive, new Color(0.64f, 0.48f, 0.82f),
                "Card_Pas_Tackle", "Card_Pas_JumpRope");
            leapfrog.leapfrog = true;
            Combo<BallPerkCard>("Card_Combo_SeaFigure", "combo_seafigure", "Shatter", UpgradeCategory.Modifier, new Color(0.5f, 0.78f, 0.95f),
                "Card_Pas_Freeze", "Card_Pas_Domino").perks = new BallPerks { shatter = true };
            Combo<AbilityCard>("Card_Combo_Barricade", "combo_barricade", "Fort", UpgradeCategory.Passive, new Color(0.75f, 0.62f, 0.5f),
                "Card_Pas_Lid", "Card_Ability_Fence");

            // «Штаб»: забор буквой П, отбитые мячи летят заряженными во врагов
            var fort = AssetDatabase.LoadAssetAtPath<FenceAbility>("Assets/_Project/Data/Abilities/Ability_Barricade.asset");
            if (fort != null)
            {
                fort.fort = true;
                fort.redirect = true;
                var hud = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/HUD_Ability_Fort.png");
                if (hud != null)
                    fort.hudIcon = hud;
                EditorUtility.SetDirty(fort);
            }
        }

        static void Card<T>(string name, System.Action<T> edit) where T : UpgradeCard
        {
            var card = AssetDatabase.LoadAssetAtPath<T>(CardDir + name + ".asset");
            if (card == null)
            {
                Debug.LogError("[Cards] Нет карточки " + name);
                return;
            }
            edit(card);
            EditorUtility.SetDirty(card);
        }

        // ------------------------------------------------------------------ deck
        static void ApplyDeck()
        {
            var deck = AssetDatabase.LoadAssetAtPath<CardDeck>(CardDir + "CardDeck_Default.asset");
            var removed = new HashSet<string>(Removed);
            int count = deck.deck.RemoveAll(card => card == null || removed.Contains(card.name));
            foreach (var combo in s_combos)
                if (!deck.deck.Contains(combo))
                    deck.deck.Add(combo);
            deck.maxGolds = 1;
            deck.maxGoldsCoop = 2;
            EditorUtility.SetDirty(deck);
            if (count > 0)
                Debug.Log("[Cards] Из колоды убрано карточек: " + count);
        }

        // ------------------------------------------------------------------ texts
        static void WriteTexts()
        {
            var content = LocalizationEditorSettings.GetStringTableCollection("Content");
            foreach (var key in RemovedKeys)
            {
                content.SharedData.RemoveKey(key + ".title");
                content.SharedData.RemoveKey(key + ".description");
            }
            // строки без ключа (остаются в таблицах языков после RemoveKey) — тоже долой
            foreach (var table in content.StringTables)
            {
                var orphans = new List<long>();
                foreach (var entry in table.Values)
                    if (content.SharedData.GetEntry(entry.KeyId) == null)
                        orphans.Add(entry.KeyId);
                foreach (long id in orphans)
                    table.Remove(id);
            }
            foreach (var (key, ruTitle, ru, enTitle, en) in CardTexts)
            {
                Entry(content, "card." + key + ".title", ruTitle, enTitle);
                Entry(content, "card." + key + ".description", ru, en);
            }

            var ui = LocalizationEditorSettings.GetStringTableCollection("UI");
            foreach (var (key, ru, en) in UiTexts)
                Entry(ui, key, ru, en);

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

        // Ключ, название и описание (ru), название и описание (en). Одна короткая мысль на карточку, без цифр, где можно.
        static readonly (string key, string ruTitle, string ru, string enTitle, string en)[] CardTexts =
        {
            // мячи
            ("ball_tennis", "Теннисные мячи", "5 мячей. Зажми бросок — летят очередью.",
                "Tennis Balls", "5 balls. Hold throw to fire a burst."),
            ("ball_volleyball", "Волейбольный мяч", "Попал — мяч летит ещё в 2 врагов.",
                "Volleyball", "After a hit it hops to 2 more enemies."),
            ("ball_pingpong", "Пинг-понг", "Быстрый. Отскакивает от всего до 6 раз.",
                "Ping-Pong Ball", "Fast. Bounces off anything up to 6 times."),
            ("ball_medicine", "Набивной мяч", "Тяжёлый: бьёт всех рядом. Всего 2 мяча.",
                "Medicine Ball", "Heavy: hits everyone nearby. Only 2 balls."),
            ("ball_basketball", "Баскетбольный мяч", "Летит дугой в прицел и бьёт всех вокруг.",
                "Basketball", "Arcs to your aim and hits everyone around."),
            ("ball_football", "Футбольный мяч", "Катится и сбивает всех. Рывок в него — пинок.",
                "Football", "Rolls and knocks everyone down. Dash into it to kick."),

            // броски
            ("mod_boomerang", "Бумеранг", "Мяч возвращается к тебе и сбивает всех по пути.",
                "Boomerang", "The ball flies back to you, hitting everyone."),
            ("mod_bouncy", "Попрыгунчик", "Мяч отскакивает от земли и бьёт снова.",
                "Bouncy Ball", "The ball bounces off the ground and hits again."),
            ("mod_eagle", "Глаз-алмаз", "Мяч сам летит в ближайшего врага.",
                "Eagle Eye", "The ball curves into the nearest enemy."),
            ("mod_elastic", "На резинке", "Упавший мяч сам прыгает тебе в руки.",
                "On a String", "A dropped ball jumps back into your hands."),
            ("mod_extraball", "Ещё мяч", "+1 мяч в руки.",
                "One More Ball", "+1 ball in your hands."),
            ("mod_gum", "Жвачка", "Мяч оставляет липкий след. Враги в нём вязнут.",
                "Bubble Gum", "The ball leaves a sticky trail. Enemies get stuck."),
            ("mod_hooligan", "Хулиганство", "Бросок бьёт на 1 сильнее, но мячей меньше.",
                "Hooligan", "Throws hit 1 harder, but you get fewer balls."),
            ("mod_hotpotato", "Горячая картошка", "Поймал мяч — следующий бросок взрывается.",
                "Hot Potato", "Catch a ball and your next throw explodes."),
            ("mod_slingshot", "Рогатка", "Заряженный бросок пробивает врагов насквозь.",
                "Slingshot", "A charged throw goes through enemies."),
            ("mod_split", "Раздвоение", "При попадании мяч раскалывается на два.",
                "Split", "On a hit the ball splits in two."),
            ("mod_wall", "Стеночка", "Каждый отскок от стены: +1 урон.",
                "Off the Wall", "Each wall bounce: +1 damage."),

            // пассивные
            ("pas_speed", "Новые кеды", "Бегаешь на 10% быстрее.",
                "New Sneakers", "Run 10% faster."),
            ("pas_jumprope", "Скакалка", "Рывок дальше и чаще.",
                "Jump Rope", "Dash farther and more often."),
            ("pas_tackle", "Подкат", "Рывок сбивает врагов с ног.",
                "Slide Tackle", "Your dash knocks enemies down."),
            ("pas_catch", "Цепкие руки", "Ловить легче.",
                "Sticky Hands", "Catching is easier."),
            ("pas_pickup", "Длинные руки", "Подбираешь мячи и монетки дальше.",
                "Long Arms", "Pick up balls and coins from farther away."),
            ("pas_countrhyme", "Считалочка", "Каждый 3-й бросок сам заряжен.",
                "Counting Rhyme", "Every 3rd throw is charged."),
            ("pas_domino", "Домино", "Выбитый враг падает на соседей и бьёт их.",
                "Domino", "A knocked-out enemy falls on its neighbours."),
            ("pas_freeze", "Замри!", "Идеально поймал — враги вокруг замирают.",
                "Freeze!", "Perfect catch — enemies around you freeze."),
            ("pas_lid", "Крышка от кастрюли", "Раз в 20 с отбивает один мяч.",
                "Pot Lid", "Blocks one ball every 20 s."),
            ("pas_tamagotchi", "Тамагочи", "Поймай 5 мячей — питомец спасёт от выбивания.",
                "Tamagotchi", "Catch 5 balls and your pet saves you once."),
            ("pas_secondwind", "Второе дыхание", "Раз за прогулку: выбили — встаёшь сам.",
                "Second Wind", "Once per run: knocked out? Get back up."),
            ("pas_pies", "Бабушкины пирожки", "+1 сердце.",
                "Grandma's Pies", "+1 heart."),
            ("pas_piggy", "Копилка", "В ларьке +1 монетка за каждые 10.",
                "Piggy Bank", "At the kiosk: +1 coin for every 10."),
            ("pas_luckywrapper", "Счастливый фантик", "Выбираешь из 4 карточек, а не из 3.",
                "Lucky Wrapper", "Pick from 4 cards instead of 3."),
            ("pas_circleguard", "Своих не бросаем", "Поднял друга — враги вокруг замирают.",
                "No One Left Behind", "Pick up a friend — enemies around freeze."),
            ("treat_sandwich", "Бутерброд", "+1 сердце. От бабушки.",
                "Sandwich", "+1 heart. From grandma."),

            // умения на Q
            ("ability_fence", "Забор", "Q: ставишь забор. Чужие мячи отскакивают.",
                "Fence", "Q: put up a fence. Enemy balls bounce off."),
            ("ability_bubbles", "Мыльные пузыри", "Q: пузыри ловят чужие мячи.",
                "Soap Bubbles", "Q: bubbles catch enemy balls."),
            ("ability_capgun", "Пугач", "Q: хлопок! Мячи в тебя падают.",
                "Cap Gun", "Q: bang! Balls flying at you drop."),
            ("ability_camera", "Фотоаппарат «Смена»", "Q: вспышка! Враги в кадре замирают.",
                "«Smena» Camera", "Q: flash! Enemies in the frame freeze."),
            ("ability_magnet", "Магнит", "Q: мячи вокруг летят тебе в руки.",
                "Magnet", "Q: balls around fly into your hands."),
            ("ability_rewind", "Перемотка", "Q: отматываешь себя на 3 с назад.",
                "Rewind", "Q: rewind yourself 3 s back."),
            ("ability_carousel", "Карусель", "Q: кружишься и бросаешь все мячи кольцом.",
                "Merry-Go-Round", "Q: spin and throw all your balls in a ring."),
            ("ability_elastic", "Резиночка", "Q: резинка до друга. Враги о неё спотыкаются.",
                "Jump Elastic", "Q: an elastic to a friend. Enemies trip on it."),
            ("ability_walkie", "Рация", "Q: отметь врага — все мячи летят в него.",
                "Walkie-Talkie", "Q: mark an enemy — all balls fly at it."),

            // комбо
            ("combo_hail", "Град", "Каждый мяч очереди раскалывается дважды.",
                "Hail", "Every burst ball splits twice."),
            ("combo_serve", "Подача", "Мяч летит не в 2, а в 4 врагов.",
                "Serve", "The ball hops to 4 enemies, not 2."),
            ("combo_dunk", "Данк", "Упав, мяч прыгает к врагу и бьёт ещё раз.",
                "Dunk", "After landing the ball hops to an enemy and hits again."),
            ("combo_smash", "Смэш", "Каждый отскок — сильнее и быстрее.",
                "Smash", "Every bounce hits harder and faster."),
            ("combo_penalty", "Пенальти", "Пинок в рывке — в полную силу.",
                "Penalty", "A dash kick goes full power."),
            ("combo_kettlebell", "Гиря", "Удар по площади оглушает всех.",
                "Kettlebell", "The area hit stuns everyone."),
            ("combo_bouncebomb", "Петарда", "Поймал мяч — следующий взрывается на каждом отскоке.",
                "Firecracker", "Catch a ball — your next one explodes on every bounce."),
            ("combo_roll", "Чехарда", "Рывок во врага — прыгаешь через него.",
                "Leapfrog", "Dash into an enemy to leap over it."),
            ("combo_yoyo", "Солнышко", "Мяч на нитке облетает вокруг тебя.",
                "Around the World", "A ball on a string circles around you."),
            ("combo_barricade", "Штаб", "Q: штаб из досок. Чужие мячи летят обратно.",
                "Fort", "Q: a plank fort. Enemy balls fly back."),
            ("combo_seafigure", "Морская фигура", "Удар по замершему врагу разбивает его.",
                "Statues", "Hitting a frozen enemy shatters it."),
        };

        static readonly (string key, string ru, string en)[] UiTexts =
        {
            ("secondwind.title", "Второе дыхание!", "Second wind!"),
            ("card.sleep.warning", "уснут: {0}", "will sleep: {0}"),
            ("pockets.tag.combo_same", "комбо · тот же карман", "combo · same pocket"),
            ("pockets.asleep", "{0} · спит", "{0} · asleep"),
            ("secondwind.hint", "Встаёшь сам через 3 секунды", "You'll get back up in 3 seconds"),
            ("pockets.replace.subtitle", "Чтобы взять «{0}», продай одну из своих за полцены",
                "To take “{0}”, sell one of yours for half price"),
            ("pockets.replace.drop", "ПРОДАЁШЬ · +{0}", "SELLING · +{0}"),
            ("pockets.replace.stamp", "ПРОДАТЬ", "SELL"),
            ("pockets.replace.pick", "Выбери сверху, какую продать", "Pick above which one to sell"),
            ("pockets.replace.confirm", "Продать и взять", "Sell and take"),
            ("pockets.tag.full", "карманы полны · продашь одну", "pockets full · sell one"),
            ("pockets.tag.swap", "заменит умение · старое за полцены", "replaces ability · old one sells for half"),
            // категории на карточке: чтобы пассивки и умения на Q не назывались одинаково
            ("card.category.ball", "МЯЧ", "BALL"),
            ("card.category.modifier", "БРОСОК", "THROW"),
            ("card.category.passive", "НАВЫК", "PERK"),
            ("card.category.ability", "УМЕНИЕ · Q", "ABILITY · Q"),
            ("card.category.combo", "КОМБО", "COMBO"),
            ("shop.hint",
                "Клик или Enter — купить    L — отложить одну    R — перебрать    P / Tab — карманы    Esc — уйти\n" +
                "Геймпад: A — купить    X — отложить одну    Y — перебрать    LB / Select — карманы    B — уйти",
                "Click or Enter — buy    L — hold one    R — reroll    P / Tab — pockets    Esc — leave\n" +
                "Gamepad: A — buy    X — hold one    Y — reroll    LB / Select — pockets    B — leave"),
          };
    }
}
