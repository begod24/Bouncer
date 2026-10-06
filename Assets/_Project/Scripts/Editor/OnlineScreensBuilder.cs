using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Bouncer.EditorTools
{
    /// <summary>
    /// Собирает в GameUI экраны сетевой игры по образцу «Пройти обучение?» (меловые кнопки, заголовок
    /// с подчёркиванием, подсказка внизу): «Гуляем вместе» (OnlineScreen: имя, создать комнату, код, локальная сеть)
    /// и «Комната» (LobbyScreen: код, игроки, дети, опасность, «Готов!» / «Гулять!», «Выйти»), и подключает их
    /// вместе с «Как гуляем?» к RunScreens. Готовые экраны пересобираются заново.
    /// </summary>
    static class OnlineScreensBuilder
    {
        const string GameUiPath = "Assets/_Project/Prefabs/UI/GameUI.prefab";
        const string NetSessionPath = "Assets/_Project/Prefabs/Net/NetSession.prefab";
        const string KidRosterPath = "Assets/_Project/Data/Characters/KidRoster.asset";
        const string CaveatPath = "Assets/_Project/Art/Fonts/Caveat SDF.asset";
        const string UnderlinePath = "Assets/_Project/Art/UI/HUD_Underline.png";

        static readonly Color Chalk = new(0.96f, 0.95f, 0.93f, 1f);
        static readonly Color Gold = new(0.98f, 0.85f, 0.36f, 1f);

        [MenuItem("Bouncer/UI/Build Online Screens")]
        static void Build()
        {
            var root = PrefabUtility.LoadPrefabContents(GameUiPath);
            try
            {
                var screens = root.transform.Find("Screens");
                var mode = screens.Find("ModeScreen");
                if (mode == null)
                {
                    Debug.LogError("[OnlineScreensBuilder] Нет ModeScreen в GameUI.");
                    return;
                }
                foreach (var old in new[] { "OnlineScreen", "LobbyScreen" })
                    if (screens.Find(old) is { } existing)
                        Object.DestroyImmediate(existing.gameObject);

                var online = BuildOnline(screens, mode.GetSiblingIndex() + 1);
                var lobby = BuildLobby(screens, online.GetSiblingIndex() + 1);
                var versus = mode.Find("Button_ДругПротивДруга/Label");
                if (versus != null)
                    versus.GetComponent<TMP_Text>().color = new Color(Chalk.r, Chalk.g, Chalk.b, 0.4f);
                Wire(screens, mode, online, lobby);
                PrefabUtility.SaveAsPrefabAsset(root, GameUiPath);
                Debug.Log("[OnlineScreensBuilder] Экраны сетевой игры собраны.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ---------- «Гуляем вместе» ----------

        static Transform BuildOnline(Transform screens, int index)
        {
            var screen = Clone(screens.Find("TutorialAskScreen"), screens, "OnlineScreen", index);
            Key(screen.Find("Title"), "online.title", "Гуляем вместе");
            Place(screen.Find("Title"), 0f, 400f);
            Place(screen.Find("Underline"), 0f, 328f);

            var body = screen.Find("Body");
            body.name = "NameCaption";
            Key(body, "online.name", "Как тебя зовут во дворе?");
            Place(body, 0f, 250f, 1200f, 60f);

            var nameField = MakeField(screen, body, "Field_Name", 0f, 170f, 640f, "online.name.placeholder", "Имя");

            var create = screen.Find("Button_Да");
            create.name = "Button_СоздатьКомнату";
            Place(create, 0f, 60f, 640f, 84f);
            Key(create.Find("Label"), "online.create", "Создать комнату");

            var codeField = MakeField(screen, body, "Field_Code", -190f, -70f, 560f, "online.code.placeholder", "Код комнаты или IP");

            var join = screen.Find("Button_Нет");
            join.name = "Button_Войти";
            Place(join, 300f, -70f, 340f, 84f);
            Key(join.Find("Label"), "online.join", "Войти");

            var lan = Clone(create, screen, "Button_ЛокальнаяСеть");
            Place(lan, 0f, -180f, 760f, 84f);
            Key(lan.Find("Label"), "online.lan", "Комната по локальной сети");

            var status = PlainText(body, screen, "Status", 0f, -300f, 1400f, 110f, 42f);
            Clone(screens.Find("KidSelectScreen/Button_Назад"), screen, "Button_Назад");

            var component = AddComponent(screen, "Bouncer.UI.OnlineScreen, Bouncer.UI");
            var so = new SerializedObject(component);
            so.FindProperty("sessionPrefab").objectReferenceValue = NetSessionComponent();
            so.FindProperty("nameField").objectReferenceValue = nameField;
            so.FindProperty("codeField").objectReferenceValue = codeField;
            so.FindProperty("createButton").objectReferenceValue = create.GetComponent<Button>();
            so.FindProperty("joinButton").objectReferenceValue = join.GetComponent<Button>();
            so.FindProperty("lanButton").objectReferenceValue = lan.GetComponent<Button>();
            so.FindProperty("status").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();
            return screen;
        }

        // ---------- «Комната» ----------

        static Transform BuildLobby(Transform screens, int index)
        {
            var screen = Clone(screens.Find("TutorialAskScreen"), screens, "LobbyScreen", index);
            var title = screen.Find("Title");
            RemoveKey(title);
            title.GetComponent<TMP_Text>().text = "Комната · вместе";
            Place(title, 0f, 430f);
            Place(screen.Find("Underline"), 0f, 362f);

            var body = screen.Find("Body");
            var code = PlainText(body, screen, "Code", -120f, 290f, 1000f, 70f, 54f);
            code.color = Gold;
            code.text = "Код комнаты: ABC123";
            var copy = Clone(screen.Find("Button_Нет"), screen, "Button_Скопировать");
            Place(copy, 560f, 290f, 360f, 84f);
            Key(copy.Find("Label"), "lobby.copy", "Скопировать");

            var rows = new TMP_Text[4];
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = PlainText(body, screen, "Row_" + (i + 1), 0f, 195f - i * 62f, 1200f, 60f, 46f);
                rows[i].alignment = TextAlignmentOptions.Left;
                rows[i].text = $"{i + 1}. свободно";
            }

            body.name = "KidsCaption";
            Key(body, "lobby.kids", "Кем гуляешь?");
            Place(body, 0f, -60f, 1000f, 56f);

            var kidTemplate = screens.Find("KidSelectScreen/View/Button_Kid_otlichnik");
            var kidButtons = new Button[4];
            for (int i = 0; i < kidButtons.Length; i++)
            {
                var kid = Clone(kidTemplate, screen, "Button_Kid_" + (i + 1));
                foreach (var hover in kid.GetComponents<MonoBehaviour>())
                    if (hover != null && hover.GetType().Name == "KidSelectButton")
                        Object.DestroyImmediate(hover);
                RemoveKey(kid.Find("Label"));
                Place(kid, -600f + i * 400f, -130f, 360f, 84f);
                kidButtons[i] = kid.GetComponent<Button>();
            }

            var danger = Clone(screens.Find("KidSelectScreen/DangerStepper"), screen, "DangerStepper");
            var dangerRect = (RectTransform)danger;
            dangerRect.anchorMin = dangerRect.anchorMax = new Vector2(0.5f, 0.5f);
            Place(danger, 0f, -225f, 420f, 64f);

            var ready = screen.Find("Button_Да");
            ready.name = "Button_Готов";
            Place(ready, 0f, -320f, 640f, 84f);
            RemoveKey(ready.Find("Label"));
            ready.Find("Label").GetComponent<TMP_Text>().text = "Готов!";

            var start = screen.Find("Button_Нет");
            start.name = "Button_Гулять";
            Place(start, 0f, -320f, 640f, 84f);
            Key(start.Find("Label"), "lobby.start", "Гулять!");

            var status = PlainText(screen.Find("KidsCaption"), screen, "Status", 0f, -400f, 1400f, 60f, 40f);
            var leave = Clone(screens.Find("KidSelectScreen/Button_Назад"), screen, "Button_Выйти");
            Key(leave.Find("Label"), "lobby.leave", "Выйти");

            var component = AddComponent(screen, "Bouncer.UI.LobbyScreen, Bouncer.UI");
            var so = new SerializedObject(component);
            so.FindProperty("roster").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Object>(KidRosterPath);
            so.FindProperty("title").objectReferenceValue = title.GetComponent<TMP_Text>();
            so.FindProperty("code").objectReferenceValue = code;
            so.FindProperty("copyButton").objectReferenceValue = copy.GetComponent<Button>();
            SetArray(so.FindProperty("rows"), rows);
            SetArray(so.FindProperty("kidButtons"), kidButtons);
            so.FindProperty("dangerStepper").objectReferenceValue = danger.GetComponent("ChalkStepper");
            so.FindProperty("readyButton").objectReferenceValue = ready.GetComponent<Button>();
            so.FindProperty("readyLabel").objectReferenceValue = ready.Find("Label").GetComponent<TMP_Text>();
            so.FindProperty("startButton").objectReferenceValue = start.GetComponent<Button>();
            so.FindProperty("leaveButton").objectReferenceValue = leave.GetComponent<Button>();
            so.FindProperty("status").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();
            return screen;
        }

        // ---------- RunScreens ----------

        static void Wire(Transform screens, Transform mode, Transform online, Transform lobby)
        {
            var runScreens = screens.GetComponent("RunScreens");
            var so = new SerializedObject(runScreens);
            SetScreen(so, "mode", mode, mode.Find("Button_Одному"));
            SetScreen(so, "online", online, online.Find("Button_СоздатьКомнату"));
            SetScreen(so, "lobby", lobby, lobby.Find("Button_Гулять"));
            so.FindProperty("onlineScreen").objectReferenceValue = online.GetComponent("OnlineScreen");
            so.FindProperty("lobbyScreen").objectReferenceValue = lobby.GetComponent("LobbyScreen");
            SetArray(so.FindProperty("soloButtons"), new[] { mode.Find("Button_Одному").GetComponent<Button>() });
            SetArray(so.FindProperty("coopButtons"), new[] { mode.Find("Button_Вместе").GetComponent<Button>() });
            // «Назад» на «Как гуляем?» и «Гуляем вместе» закрывает экран, как и на остальных экранах поверх.
            var backs = so.FindProperty("backButtons");
            foreach (var back in new[] { mode.Find("Button_Назад"), online.Find("Button_Назад") })
            {
                var button = back.GetComponent<Button>();
                bool has = false;
                for (int i = 0; i < backs.arraySize; i++)
                    has |= backs.GetArrayElementAtIndex(i).objectReferenceValue == button;
                if (has)
                    continue;
                backs.arraySize++;
                backs.GetArrayElementAtIndex(backs.arraySize - 1).objectReferenceValue = button;
            }
            // Ссылки на кнопки удалённых экранов (прошлая сборка) — убрать.
            for (int i = backs.arraySize - 1; i >= 0; i--)
                if (backs.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    backs.DeleteArrayElementAtIndex(i);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetScreen(SerializedObject so, string name, Transform screen, Transform first)
        {
            so.FindProperty(name + ".group").objectReferenceValue = screen.GetComponent<CanvasGroup>();
            so.FindProperty(name + ".first").objectReferenceValue = first != null ? first.GetComponent<Selectable>() : null;
        }

        // ---------- Детали ----------

        /// <summary>Поле ввода мелом: светлая полоска, подчёркивание, подсказка с переводом, текст шрифтом Caveat (все буквы).</summary>
        static TMP_InputField MakeField(Transform screen, Transform textTemplate, string name, float x, float y, float width,
            string placeholderKey, string placeholderPreview)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CaveatPath);
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
            var rect = (RectTransform)go.transform;
            rect.SetParent(screen, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, 84f);
            var back = go.GetComponent<Image>();
            back.color = Color.white;

            var line = new GameObject("Underline", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var lineRect = (RectTransform)line.transform;
            lineRect.SetParent(rect, false);
            lineRect.anchorMin = Vector2.zero;
            lineRect.anchorMax = new Vector2(1f, 0f);
            lineRect.anchoredPosition = new Vector2(0f, 2f);
            lineRect.sizeDelta = new Vector2(0f, 26f);
            var lineImage = line.GetComponent<Image>();
            lineImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(UnderlinePath);
            lineImage.raycastTarget = false;

            var area = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            var areaRect = (RectTransform)area.transform;
            areaRect.SetParent(rect, false);
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = new Vector2(22f, 6f);
            areaRect.offsetMax = new Vector2(-22f, -6f);

            var placeholder = Clone(textTemplate, areaRect, "Placeholder").GetComponent<TMP_Text>();
            Stretch((RectTransform)placeholder.transform);
            Key(placeholder.transform, placeholderKey, placeholderPreview);
            Style(placeholder, font, new Color(Chalk.r, Chalk.g, Chalk.b, 0.4f));

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(areaRect, false);
            Stretch((RectTransform)textGo.transform);
            var text = textGo.GetComponent<TextMeshProUGUI>();
            Style(text, font, Chalk);

            var field = go.GetComponent<TMP_InputField>();
            field.textViewport = areaRect;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.fontAsset = font;
            field.pointSize = 52f;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.customCaretColor = true;
            field.caretColor = Gold;
            field.caretWidth = 3;
            field.selectionColor = new Color(Gold.r, Gold.g, Gold.b, 0.35f);
            field.targetGraphic = back;
            // Полоска поля светлеет под мышью и пока в нём печатают.
            var colors = field.colors;
            colors.normalColor = new Color(Chalk.r, Chalk.g, Chalk.b, 0.07f);
            colors.highlightedColor = new Color(Chalk.r, Chalk.g, Chalk.b, 0.13f);
            colors.selectedColor = new Color(Chalk.r, Chalk.g, Chalk.b, 0.18f);
            colors.pressedColor = colors.selectedColor;
            colors.disabledColor = new Color(Chalk.r, Chalk.g, Chalk.b, 0.03f);
            colors.colorMultiplier = 1f;
            field.colors = colors;
            return field;
        }

        static void Style(TMP_Text text, TMP_FontAsset font, Color color)
        {
            text.font = font;
            text.fontSize = 52f;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = color;
            text.raycastTarget = false;
        }

        /// <summary>Текст без перевода (его пишет код) — копия образца с другим местом и размером.</summary>
        static TMP_Text PlainText(Transform template, Transform parent, string name, float x, float y, float width, float height, float fontSize)
        {
            var copy = Clone(template, parent, name);
            RemoveKey(copy);
            Place(copy, x, y, width, height);
            var text = copy.GetComponent<TMP_Text>();
            text.text = "";
            text.fontSize = fontSize;
            return text;
        }

        static Transform Clone(Transform template, Transform parent, string name, int siblingIndex = -1)
        {
            var copy = Object.Instantiate(template.gameObject, parent).transform;
            copy.name = name;
            if (siblingIndex >= 0)
                copy.SetSiblingIndex(siblingIndex);
            return copy;
        }

        static Component AddComponent(Transform target, string typeName)
        {
            var type = Type.GetType(typeName);
            if (type == null)
                throw new InvalidOperationException($"Нет типа {typeName}");
            return target.gameObject.AddComponent(type);
        }

        static Component NetSessionComponent()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetSessionPath);
            return prefab != null ? prefab.GetComponent("NetSession") : null;
        }

        static void SetArray<T>(SerializedProperty property, T[] values) where T : Object
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        static void Place(Transform target, float x, float y, float width = -1f, float height = -1f)
        {
            var rect = (RectTransform)target;
            rect.anchoredPosition = new Vector2(x, y);
            if (width > 0f)
                rect.sizeDelta = new Vector2(width, height);
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Надпись берёт перевод по ключу из таблицы UI (а в редакторе видна по-русски).</summary>
        static void Key(Transform target, string key, string preview)
        {
            var localize = target.GetComponent<LocalizeStringEvent>();
            if (localize != null)
                localize.StringReference.SetReference("UI", key);
            var text = target.GetComponent<TMP_Text>();
            if (text != null)
                text.text = preview;
        }

        static void RemoveKey(Transform target)
        {
            var localize = target.GetComponent<LocalizeStringEvent>();
            if (localize != null)
                Object.DestroyImmediate(localize);
        }
    }
}
