using Bouncer.UI;
using Bouncer.Upgrades;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Components;

namespace Bouncer.EditorTools
{
    // Тетрадка 2026-10-12: четвёртая вкладка «Комбо» — все комбо карточками (несобранные серые, рецепт виден).
    // Меню Bouncer → UI → Build Notebook Combos. Перезапускаемый.
    static class NotebookCombosBuilder
    {
        const string GameUiPath = "Assets/_Project/Prefabs/UI/GameUI.prefab";
        const float TabWidth = 280f;

        [MenuItem("Bouncer/UI/Build Notebook Combos")]
        public static void Build()
        {
            var root = PrefabUtility.LoadPrefabContents(GameUiPath);
            try
            {
                var notebook = root.GetComponentInChildren<NotebookScreen>(true);
                var tabsRow = notebook.transform.Find("NotebookTabs");
                var tabs = tabsRow.GetComponent<ChalkTabs>();

                // вкладка
                var tab = tabsRow.Find("Tab_Combos");
                if (tab == null)
                {
                    var source = tabsRow.Find("Tab_Bestiary");
                    tab = Object.Instantiate(source.gameObject, tabsRow, false).transform;
                    tab.name = "Tab_Combos";
                    tab.SetSiblingIndex(tabsRow.Find("Tab_HowTo").GetSiblingIndex() + 1);
                }
                var label = tab.Find("Label");
                if (label.TryGetComponent(out LocalizeStringEvent localize))
                    localize.StringReference.SetReference("UI", "notebook.tab.combos");
                label.GetComponent<TMP_Text>().text = "Комбо";
                var newMark = tab.Find("New");
                string[] order = { "Tab_Bestiary", "Tab_Records", "Tab_HowTo", "Tab_Combos" };
                for (int i = 0; i < order.Length; i++)
                {
                    // четыре вкладки в ряд: длинные надписи («How to play») ужимаются, а не наезжают друг на друга
                    var text = tabsRow.Find(order[i]).Find("Label").GetComponent<TMP_Text>();
                    if (!text.enableAutoSizing)
                    {
                        text.fontSizeMax = text.fontSize;
                        text.fontSizeMin = text.fontSize * 0.55f;
                        text.enableAutoSizing = true;
                    }
                    text.textWrappingMode = TextWrappingModes.NoWrap;
                    var rect = (RectTransform)tabsRow.Find(order[i]);
                    rect.sizeDelta = new Vector2(TabWidth, rect.sizeDelta.y);
                    rect.anchoredPosition = new Vector2((i - (order.Length - 1) * 0.5f) * (TabWidth + 10f), rect.anchoredPosition.y);
                    var labelRect = text.rectTransform;
                    labelRect.offsetMin = new Vector2(12f, labelRect.offsetMin.y);
                    labelRect.offsetMax = new Vector2(-12f, labelRect.offsetMax.y);
                }
                var rowRect = (RectTransform)tabsRow;
                rowRect.sizeDelta = new Vector2(order.Length * (TabWidth + 10f), rowRect.sizeDelta.y);
                var underline = (RectTransform)tabsRow.Find("Underline");
                underline.sizeDelta = new Vector2(TabWidth - 50f, underline.sizeDelta.y);
                var tabsSo = new SerializedObject(tabs);
                SetArray(tabsSo.FindProperty("tabs"), order, name => tabsRow.Find(name).GetComponent<UnityEngine.UI.Button>());
                SetArray(tabsSo.FindProperty("labels"), order, name => tabsRow.Find(name).Find("Label").GetComponent<TMP_Text>());
                tabsSo.ApplyModifiedPropertiesWithoutUndo();

                // страница
                var page = notebook.transform.Find("Page_Combos");
                if (page == null)
                {
                    var go = new GameObject("Page_Combos", typeof(RectTransform));
                    page = go.transform;
                    page.SetParent(notebook.transform, false);
                    page.SetSiblingIndex(notebook.transform.Find("Page_HowTo").GetSiblingIndex() + 1);
                    var rect = (RectTransform)page;
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                }
                var grid = page.Find("Grid") as RectTransform;
                if (grid == null)
                {
                    grid = (RectTransform)new GameObject("Grid", typeof(RectTransform)).transform;
                    grid.SetParent(page, false);
                }
                grid.anchorMin = grid.anchorMax = new Vector2(0.5f, 0.5f);
                grid.anchoredPosition = new Vector2(0f, -20f);
                grid.sizeDelta = Vector2.zero;
                var template = page.Find("CardTemplate");
                if (template == null)
                {
                    var card = root.GetComponentInChildren<UpgradeScreen>(true).GetComponentInChildren<UpgradeCardView>(true);
                    template = Object.Instantiate(card.gameObject, page, false).transform;
                    template.name = "CardTemplate";
                }
                template.gameObject.SetActive(false);
                if (!page.TryGetComponent(out NotebookCombos combos))
                    combos = page.gameObject.AddComponent<NotebookCombos>();
                var combosSo = new SerializedObject(combos);
                combosSo.FindProperty("deck").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<CardDeck>("Assets/_Project/Data/Upgrades/CardDeck_Default.asset");
                combosSo.FindProperty("template").objectReferenceValue = template.GetComponent<UpgradeCardView>();
                combosSo.FindProperty("grid").objectReferenceValue = grid;
                combosSo.ApplyModifiedPropertiesWithoutUndo();
                page.gameObject.SetActive(false);

                // страница в списке Тетрадки
                var notebookSo = new SerializedObject(notebook);
                notebookSo.FindProperty("combos").objectReferenceValue = combos;
                var pages = notebookSo.FindProperty("pages");
                if (pages.arraySize < 4)
                    pages.arraySize = 4;
                var entry = pages.GetArrayElementAtIndex(3);
                entry.FindPropertyRelative("panel").objectReferenceValue = page.gameObject;
                entry.FindPropertyRelative("hintKey").stringValue = "notebook.hint";
                entry.FindPropertyRelative("first").objectReferenceValue = null;
                entry.FindPropertyRelative("newMark").objectReferenceValue = newMark ? newMark.gameObject : null;
                notebookSo.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, GameUiPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            var ui = LocalizationEditorSettings.GetStringTableCollection("UI");
            Entry(ui, "notebook.tab.combos", "Комбо", "Combos");
            Entry(ui, "notebook.combo.locked", "Собери эти две карточки — и узнаешь.", "Collect these two cards to find out.");
            EditorUtility.SetDirty(ui.SharedData);
            foreach (var table in ui.StringTables)
                EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            Debug.Log("[Notebook] Вкладка «Комбо» готова.");
        }

        static void SetArray<T>(SerializedProperty array, string[] names, System.Func<string, T> get) where T : Object
        {
            array.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = get(names[i]);
        }

        static void Entry(UnityEditor.Localization.StringTableCollection collection, string key, string ru, string en)
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
    }
}
