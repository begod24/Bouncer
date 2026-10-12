using System.Collections.Generic;
using Bouncer.Balls;
using Bouncer.Core;
using Bouncer.Player;
using Bouncer.UI;
using Bouncer.Upgrades;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UIImage = UnityEngine.UI.Image;

namespace Bouncer.EditorTools
{
    // Ребаланс мячей 2026-10: роли, руки, шкалы на карточке, сдутый убран. Перезапускаемый.
    static class BallRosterBuilder
    {
        const string Root = "Assets/_Project/";
        const string BallDataDir = Root + "Data/Balls/";
        const string CardDir = Root + "Data/Upgrades/";
        const string DeflatedCard = CardDir + "Card_Ball_Deflated.asset";
        const string DeflatedPrefab = Root + "Prefabs/Balls/Ball_Deflated.prefab";

        [MenuItem("Bouncer/Cards/Build Ball Roster")]
        static void Build()
        {
            ApplyNumbers();
            WriteTexts();
            DropDeflated();
            BuildStatBars();
            WireLandingMarker();
            AssetDatabase.SaveAssets();
            Debug.Log("[Balls] Мячи обновлены: цифры, тексты, шкалы, метка падения. Сдутый убран из колоды и сети.");
        }

        public static void ApplyNumbers()
        {
            Ball("Ball_Default", d =>
            {
                Hands(d, 0, false, 1f, 0f);
                d.ratings = Ratings(2, 3, 3, 1, 1);
            });
            Ball("Ball_Tennis", d =>
            {
                Hands(d, 5, false, 1f, 0.14f);
                d.speed = 26f;
                d.chargedSpeed = 36f;
                d.damage = 1;
                d.chargedDamage = 1;
                d.candleDamage = 2;
                d.knockback = 3f;
                d.chargedKnockback = 6f;
                d.candleKnockback = 9f;
                d.maxRicochets = 4;
                d.perks.extraShots = 0;
                d.perks.spreadAngle = 0f;
                d.ratings = Ratings(1, 4, 3, 1, 1);
            });
            Ball("Ball_Volleyball", d =>
            {
                Hands(d, 0, false, 1f, 0f);
                d.ratings = Ratings(2, 3, 4, 3, 1);
            });
            Ball("Ball_Medicine", d =>
            {
                Hands(d, 2, true, 1.6f, 0f);
                d.damage = 2;
                d.chargedDamage = 4;
                d.candleDamage = 5;
                d.perks.areaRadius = 2.6f;
                d.perks.areaDamage = 1;
                d.perks.ignoreShields = true;
                d.ratings = Ratings(5, 2, 1, 4, 3);
            });
            Ball("Ball_Basketball", d =>
            {
                Hands(d, 0, true, 1f, 0f);
                d.radius = 0.26f;
                d.mass = 0.6f;
                d.speed = 13f;
                d.chargedSpeed = 17f;
                d.upVelocity = 7f;
                d.chargedUpVelocity = 7.5f;
                d.liveGravity = 15f;
                d.chargedLiveGravity = 13f;
                d.damage = 1;
                d.chargedDamage = 2;
                d.candleDamage = 3;
                d.maxLiveTime = 3f;
                d.perks.lob = true;
                d.perks.areaRadius = 1.8f;
                d.perks.areaDamage = 1;
                d.perks.ignoreShields = true;
                d.lobDistance = new Vector2(3f, 20f);
                d.lobTapDistance = 13f;
                d.lobFlightTime = new Vector2(0.55f, 1.05f);
                d.lobMarkerRadius = 0.9f;
                d.ratings = Ratings(3, 2, 1, 3, 2);
            });
            Ball("Ball_PingPong", d =>
            {
                Hands(d, 0, false, 1f, 0f);
                d.radius = 0.11f;
                d.mass = 0.05f;
                d.speed = 26f;
                d.chargedSpeed = 36f;
                d.damage = 1;
                d.chargedDamage = 1;
                d.candleDamage = 2;
                d.liveGravity = 1.2f;
                d.chargedLiveGravity = 0.6f;
                d.maxRicochets = 6;
                d.wallSpeedKeep = 1f;
                d.minLiveSpeed = 6f;
                d.maxLiveTime = 3.5f;
                d.perks.bounceAssist = 50f;
                d.perks.bounceOffTargets = true;
                d.ratings = Ratings(1, 5, 5, 2, 1);
            });
            Ball("Ball_Football", d =>
            {
                Hands(d, 0, false, 1f, 0f);
                d.radius = 0.22f;
                d.mass = 0.45f;
                d.speed = 17f;
                d.chargedSpeed = 24f;
                d.upVelocity = 0f;
                d.chargedUpVelocity = 0f;
                d.liveGravity = 0f;
                d.chargedLiveGravity = 0f;
                d.damage = 1;
                d.chargedDamage = 2;
                d.rollDrag = 6f;
                d.maxLiveTime = 2.2f;
                d.minLiveSpeed = 5f;
                d.maxRicochets = 4;
                d.perks.groundRoll = true;
                d.perks.grazeRadius = 0.55f;
                d.perks.grazeStun = 0.7f;
                d.ratings = Ratings(2, 3, 4, 3, 4);
            });
        }

        static void Ball(string name, System.Action<BallDefinition> edit)
        {
            var data = AssetDatabase.LoadAssetAtPath<BallDefinition>(BallDataDir + name + ".asset");
            if (data == null)
            {
                Debug.LogError("[Balls] Нет " + name);
                return;
            }
            edit(data);
            EditorUtility.SetDirty(data);
        }

        static void Hands(BallDefinition d, int hands, bool fixedHands, float charge, float autoFire)
        {
            d.hands = hands;
            d.fixedHands = fixedHands;
            d.chargeTimeMultiplier = charge;
            d.autoFireInterval = autoFire;
        }

        static BallRatings Ratings(int damage, int speed, int ricochets, int area, int control) =>
            new() { damage = damage, speed = speed, ricochets = ricochets, area = area, control = control };

        // ------------------------------------------------------------------ texts
        static void WriteTexts()
        {
            var content = LocalizationEditorSettings.GetStringTableCollection("Content");
            Entry(content, "card.ball_tennis.title", "Теннисные мячи", "Tennis balls");
            Entry(content, "card.ball_tennis.description", "5 мячей в руках. Зажми бросок — очередь.",
                "5 balls in hand. Hold throw to fire a burst.");
            Entry(content, "card.ball_basketball.description", "Летит дугой в точку прицела. Щит не спасает.",
                "Arcs to where you aim. Shields don't help.");
            Entry(content, "card.ball_football.description", "Катится и сбивает с ног. Рывок в лежащий мяч — пинок.",
                "Rolls and knocks enemies down. Dash into it to kick it.");
            Entry(content, "card.ball_pingpong.description", "Отскакивает от стен и врагов до 6 раз.",
                "Bounces off walls and enemies up to 6 times.");
            Entry(content, "card.ball_medicine.description", "Бьёт 2–4 и задевает всех рядом. Щит не спасает. Всего 2 мяча.",
                "Hits for 2–4 and splashes around. Ignores shields. Only 2 balls.");

            var ui = LocalizationEditorSettings.GetStringTableCollection("UI");
            Entry(ui, "card.stat.damage", "Урон", "Damage");
            Entry(ui, "card.stat.speed", "Скорость", "Speed");
            Entry(ui, "card.stat.ricochets", "Рикошеты", "Bounces");
            Entry(ui, "card.stat.area", "Площадь", "Area");
            Entry(ui, "card.stat.control", "Контроль", "Control");
            Entry(ui, "card.blocked.ball", "Нельзя: мешает {0}", "Not with the {0}");

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

        // ------------------------------------------------------------------ deflated ball
        static void DropDeflated()
        {
            var deck = AssetDatabase.LoadAssetAtPath<CardDeck>(CardDir + "CardDeck_Default.asset");
            if (deck != null)
            {
                int removed = deck.deck.RemoveAll(card => card == null || AssetDatabase.GetAssetPath(card) == DeflatedCard);
                if (removed > 0)
                    EditorUtility.SetDirty(deck);
            }

            const string netRoom = Root + "Prefabs/Net/NetRoom.prefab";
            var root = PrefabUtility.LoadPrefabContents(netRoom);
            foreach (var behaviour in root.GetComponents<MonoBehaviour>())
            {
                if (behaviour == null || behaviour.GetType().Name != "NetBalls")
                    continue;
                var so = new SerializedObject(behaviour);
                var list = so.FindProperty("prefabs");
                for (int i = list.arraySize - 1; i >= 0; i--)
                {
                    var item = list.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (item == null || AssetDatabase.GetAssetPath(item) == DeflatedPrefab)
                    {
                        list.GetArrayElementAtIndex(i).objectReferenceValue = null;
                        list.DeleteArrayElementAtIndex(i);
                    }
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, netRoom);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // ------------------------------------------------------------------ stat bars on card views
        static void BuildStatBars()
        {
            const string path = Root + "Prefabs/UI/GameUI.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            foreach (var view in root.GetComponentsInChildren<UpgradeCardView>(true))
            {
                var so = new SerializedObject(view);
                var description = so.FindProperty("description").objectReferenceValue as TMP_Text;
                var window = description ? description.transform.parent as RectTransform : null;
                if (window == null)
                    continue;
                var old = window.Find("BallStats");
                if (old)
                    Object.DestroyImmediate(old.gameObject);
                var bars = BuildBars(window, description, knob);
                so.FindProperty("ballStats").objectReferenceValue = bars;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static BallStatBars BuildBars(RectTransform window, TMP_Text style, Sprite knob)
        {
            const float rowHeight = 25f;
            const float pip = 18f;
            const float pipStep = 25f;
            const float labelWidth = 140f;

            var go = new GameObject("BallStats", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(window, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -274f);
            rect.sizeDelta = new Vector2(-48f, rowHeight * BallStatBars.Rows);
            var bars = go.AddComponent<BallStatBars>();

            var labels = new TMP_Text[BallStatBars.Rows];
            var pips = new UIImage[BallStatBars.Rows * BallStatBars.Pips];
            float left = labelWidth + 6f;
            float pipsWidth = left + pipStep * (BallStatBars.Pips - 1) + pip;
            for (int row = 0; row < BallStatBars.Rows; row++)
            {
                var line = new GameObject("Row_" + row, typeof(RectTransform));
                var lineRect = (RectTransform)line.transform;
                lineRect.SetParent(rect, false);
                lineRect.anchorMin = new Vector2(0.5f, 1f);
                lineRect.anchorMax = new Vector2(0.5f, 1f);
                lineRect.pivot = new Vector2(0.5f, 1f);
                lineRect.sizeDelta = new Vector2(pipsWidth, rowHeight);
                lineRect.anchoredPosition = new Vector2(0f, -row * rowHeight);

                var labelGo = new GameObject("Label", typeof(RectTransform));
                var labelRect = (RectTransform)labelGo.transform;
                labelRect.SetParent(lineRect, false);
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(0f, 1f);
                labelRect.pivot = new Vector2(0f, 0.5f);
                labelRect.sizeDelta = new Vector2(labelWidth, 0f);
                labelRect.anchoredPosition = Vector2.zero;
                var label = labelGo.AddComponent<TextMeshProUGUI>();
                label.font = style.font;
                label.fontSharedMaterial = style.fontSharedMaterial;
                label.color = style.color;
                label.fontSize = 26f;
                label.enableAutoSizing = false;
                label.alignment = TextAlignmentOptions.MidlineRight;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.raycastTarget = false;
                label.text = "Урон";
                labels[row] = label;

                for (int i = 0; i < BallStatBars.Pips; i++)
                {
                    var dot = new GameObject("Pip_" + i, typeof(RectTransform));
                    var dotRect = (RectTransform)dot.transform;
                    dotRect.SetParent(lineRect, false);
                    dotRect.anchorMin = new Vector2(0f, 0.5f);
                    dotRect.anchorMax = new Vector2(0f, 0.5f);
                    dotRect.pivot = new Vector2(0f, 0.5f);
                    dotRect.sizeDelta = new Vector2(pip, pip);
                    dotRect.anchoredPosition = new Vector2(left + i * pipStep, 0f);
                    var image = dot.AddComponent<UIImage>();
                    image.sprite = knob;
                    image.raycastTarget = false;
                    pips[row * BallStatBars.Pips + i] = image;
                }
            }

            var so = new SerializedObject(bars);
            var labelList = so.FindProperty("labels");
            labelList.arraySize = labels.Length;
            for (int i = 0; i < labels.Length; i++)
                labelList.GetArrayElementAtIndex(i).objectReferenceValue = labels[i];
            var pipList = so.FindProperty("pips");
            pipList.arraySize = pips.Length;
            for (int i = 0; i < pips.Length; i++)
                pipList.GetArrayElementAtIndex(i).objectReferenceValue = pips[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(false);
            return bars;
        }

        // ------------------------------------------------------------------ landing marker
        static void WireLandingMarker()
        {
            const string path = Root + "Prefabs/Player.prefab";
            var marker = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/VFX/GroundMarker_Circle.prefab");
            var root = PrefabUtility.LoadPrefabContents(path);
            var indicators = new List<AimIndicator>(root.GetComponentsInChildren<AimIndicator>(true));
            foreach (var indicator in indicators)
            {
                var so = new SerializedObject(indicator);
                so.FindProperty("landingMarkerPrefab").objectReferenceValue = marker ? marker.GetComponent<GroundMarker>() : null;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (indicators.Count == 0)
                Debug.LogWarning("[Balls] В Player.prefab нет AimIndicator — метка падения не подключена");
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
