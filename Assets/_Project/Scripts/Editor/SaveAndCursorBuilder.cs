using System;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Bouncer.EditorTools
{
    // 2026-10: курсор мелом (Cursor_Chalk/Cursor_Shadow из Tools/ui_art.py) + кнопки «Продолжить» и «Новая прогулка» на заставке.
    static class SaveAndCursorBuilder
    {
        const string GameUiPath = "Assets/_Project/Prefabs/UI/GameUI.prefab";
        const string CursorPrefabPath = "Assets/_Project/Resources/GameCursor.prefab";
        const string ChalkPath = "Assets/_Project/Art/UI/Cursor_Chalk.png";
        const string ShadowPath = "Assets/_Project/Art/UI/Cursor_Shadow.png";

        [MenuItem("Bouncer/UI/Build Cursor And Continue")]
        static void Build()
        {
            WriteTexts();
            BuildCursorPrefab();
            BuildTitleButtons();
            AssetDatabase.SaveAssets();
            Debug.Log("[SaveAndCursorBuilder] Курсор и кнопка «Продолжить» собраны.");
        }

        static void WriteTexts()
        {
            var ui = LocalizationEditorSettings.GetStringTableCollection("UI");
            Entry(ui, "title.continue", "Продолжить · арена {0}", "Continue · arena {0}");
            Entry(ui, "title.newwalk", "Новая прогулка", "New walk");
            EditorUtility.SetDirty(ui.SharedData);
            foreach (var table in ui.StringTables)
                EditorUtility.SetDirty(table);
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

        static void BuildCursorPrefab()
        {
            var chalk = AssetDatabase.LoadAssetAtPath<Sprite>(ChalkPath);
            var shadow = AssetDatabase.LoadAssetAtPath<Sprite>(ShadowPath);
            if (chalk == null || shadow == null)
                throw new InvalidOperationException("Нет спрайтов курсора в Art/UI");

            var root = new GameObject("GameCursor", typeof(RectTransform));
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 30000;
                var group = root.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;

                var cursor = NewRect("Cursor", root.transform);
                cursor.anchorMin = cursor.anchorMax = Vector2.zero;
                cursor.pivot = new Vector2(20f / 128f, 1f - 14f / 128f);
                cursor.sizeDelta = new Vector2(56f, 56f);

                var shadowRect = NewRect("Shadow", cursor);
                Stretch(shadowRect);
                shadowRect.offsetMin = new Vector2(3f, -4f);
                shadowRect.offsetMax = new Vector2(3f, -4f);
                var shadowImage = shadowRect.gameObject.AddComponent<Image>();
                shadowImage.sprite = shadow;
                shadowImage.color = new Color(1f, 1f, 1f, 0.6f);
                shadowImage.raycastTarget = false;

                var chalkRect = NewRect("Chalk", cursor);
                Stretch(chalkRect);
                var chalkImage = chalkRect.gameObject.AddComponent<Image>();
                chalkImage.sprite = chalk;
                chalkImage.color = new Color(0.97f, 0.96f, 0.92f, 1f);
                chalkImage.raycastTarget = false;

                var type = Type.GetType("Bouncer.UI.GameCursor, Bouncer.UI");
                if (type == null)
                    throw new InvalidOperationException("Нет типа GameCursor");
                var component = root.AddComponent(type);
                var so = new SerializedObject(component);
                so.FindProperty("root").objectReferenceValue = cursor;
                so.FindProperty("group").objectReferenceValue = group;
                so.ApplyModifiedPropertiesWithoutUndo();

                System.IO.Directory.CreateDirectory("Assets/_Project/Resources");
                PrefabUtility.SaveAsPrefabAsset(root, CursorPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void BuildTitleButtons()
        {
            var prefab = PrefabUtility.LoadPrefabContents(GameUiPath);
            try
            {
                var screens = prefab.transform.Find("Screens");
                var title = screens.Find("TitleScreen");
                foreach (var old in new[] { "Button_Продолжить", "Button_НоваяПрогулка" })
                    if (title.Find(old) is { } existing)
                        Object.DestroyImmediate(existing.gameObject);
                if (title.GetComponent("TitleMenuLayout") is { } oldLayout)
                    Object.DestroyImmediate(oldLayout);

                var play = title.Find("Button_Играть");
                var notebook = title.Find("Button_Тетрадка");
                var settings = title.Find("Button_Настройки");
                var quit = title.Find("Button_Выход");

                var cont = Clone(play, title, "Button_Продолжить", play.GetSiblingIndex() + 1);
                var contLabel = cont.Find("Label");
                var contLocalize = contLabel.GetComponent<LocalizeStringEvent>();
                if (contLocalize != null)
                    Object.DestroyImmediate(contLocalize);
                contLabel.GetComponent<TMP_Text>().text = "Продолжить · арена 2";

                var newWalk = Clone(notebook, title, "Button_НоваяПрогулка", cont.GetSiblingIndex() + 1);
                var mark = newWalk.Find("New");
                if (mark != null)
                    Object.DestroyImmediate(mark.gameObject);
                var newLabel = newWalk.Find("Label");
                newLabel.GetComponent<LocalizeStringEvent>().StringReference.SetReference("UI", "title.newwalk");
                newLabel.GetComponent<TMP_Text>().text = "Новая прогулка";

                var layoutType = Type.GetType("Bouncer.UI.TitleMenuLayout, Bouncer.UI");
                if (layoutType == null)
                    throw new InvalidOperationException("Нет типа TitleMenuLayout");
                var layout = title.gameObject.AddComponent(layoutType);
                var so = new SerializedObject(layout);
                so.FindProperty("continueButton").objectReferenceValue = cont.GetComponent<Button>();
                so.FindProperty("continueLabel").objectReferenceValue = contLabel.GetComponent<TMP_Text>();
                so.FindProperty("playButton").objectReferenceValue = play.GetComponent<Button>();
                so.FindProperty("newWalkButton").objectReferenceValue = newWalk.GetComponent<Button>();
                var rows = so.FindProperty("lowerRows");
                rows.arraySize = 3;
                rows.GetArrayElementAtIndex(0).objectReferenceValue = notebook;
                rows.GetArrayElementAtIndex(1).objectReferenceValue = settings;
                rows.GetArrayElementAtIndex(2).objectReferenceValue = quit;
                var ys = so.FindProperty("lowerRowsWithSave");
                ys.arraySize = 3;
                ys.GetArrayElementAtIndex(0).floatValue = -240f;
                ys.GetArrayElementAtIndex(1).floatValue = -320f;
                ys.GetArrayElementAtIndex(2).floatValue = -400f;
                so.FindProperty("newWalkY").floatValue = -160f;
                so.ApplyModifiedPropertiesWithoutUndo();

                var runScreens = screens.GetComponent("RunScreens");
                var rso = new SerializedObject(runScreens);
                var continues = rso.FindProperty("continueButtons");
                continues.arraySize = 1;
                continues.GetArrayElementAtIndex(0).objectReferenceValue = cont.GetComponent<Button>();
                rso.FindProperty("titleLayout").objectReferenceValue = layout;
                var plays = rso.FindProperty("playButtons");
                plays.arraySize = 2;
                plays.GetArrayElementAtIndex(0).objectReferenceValue = play.GetComponent<Button>();
                plays.GetArrayElementAtIndex(1).objectReferenceValue = newWalk.GetComponent<Button>();
                rso.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(prefab, GameUiPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static Transform Clone(Transform template, Transform parent, string name, int siblingIndex)
        {
            var copy = Object.Instantiate(template.gameObject, parent).transform;
            copy.name = name;
            copy.SetSiblingIndex(siblingIndex);
            return copy;
        }
    }
}
