using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Bouncer.EditorTools
{
    public static class KazakhCsv
    {
        const string Path = "Tools/Localization/kk_translation.csv";
        static readonly string[] Tables = { "UI", "Content" };

        [MenuItem("Bouncer/Localization/Export Kazakh CSV")]
        public static void Export()
        {
            var csv = new StringBuilder();
            csv.AppendLine("table,key,ru,en,kk");
            int rows = 0;
            foreach (var name in Tables)
            {
                var collection = LocalizationEditorSettings.GetStringTableCollection(name);
                var ru = collection.GetTable("ru") as StringTable;
                var en = collection.GetTable("en") as StringTable;
                var kk = collection.GetTable("kk") as StringTable;
                foreach (var entry in collection.SharedData.Entries)
                {
                    csv.Append(Cell(name)).Append(',').Append(Cell(entry.Key)).Append(',')
                        .Append(Cell(ru?.GetEntry(entry.Id)?.Value)).Append(',')
                        .Append(Cell(en?.GetEntry(entry.Id)?.Value)).Append(',')
                        .Append(Cell(kk?.GetEntry(entry.Id)?.Value)).Append('\n');
                    rows++;
                }
            }
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            File.WriteAllText(Path, csv.ToString(), new UTF8Encoding(true));
            Debug.Log($"Kazakh CSV: {rows} rows → {Path}");
        }

        [MenuItem("Bouncer/Localization/Import Kazakh CSV")]
        public static void Import()
        {
            if (!File.Exists(Path))
            {
                Debug.LogError($"Kazakh CSV: no file {Path}");
                return;
            }
            var rows = Parse(File.ReadAllText(Path, Encoding.UTF8));
            if (rows.Count == 0)
                return;
            var header = rows[0];
            int tableColumn = header.IndexOf("table"), keyColumn = header.IndexOf("key"), kkColumn = header.IndexOf("kk");
            if (tableColumn < 0 || keyColumn < 0 || kkColumn < 0)
            {
                Debug.LogError("Kazakh CSV: the header must have table, key and kk columns");
                return;
            }
            var tables = new Dictionary<string, (StringTableCollection collection, StringTable kk)>();
            foreach (var name in Tables)
            {
                var collection = LocalizationEditorSettings.GetStringTableCollection(name);
                tables[name] = (collection, collection.GetTable("kk") as StringTable);
            }
            int imported = 0;
            for (int i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.Count <= Mathf.Max(tableColumn, Mathf.Max(keyColumn, kkColumn)))
                    continue;
                string value = row[kkColumn];
                if (string.IsNullOrWhiteSpace(value) || !tables.TryGetValue(row[tableColumn], out var target) || target.kk == null)
                    continue;
                if (target.collection.SharedData.GetEntry(row[keyColumn]) == null)
                    continue;
                target.kk.AddEntry(row[keyColumn], value);
                imported++;
            }
            foreach (var pair in tables.Values)
                if (pair.kk != null)
                    EditorUtility.SetDirty(pair.kk);
            AssetDatabase.SaveAssets();
            Debug.Log($"Kazakh CSV: imported {imported} strings");
        }

        static string Cell(string value)
        {
            value ??= string.Empty;
            return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }

        static List<List<string>> Parse(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            cell.Append('"');
                            i++;
                        }
                        else
                        {
                            quoted = false;
                        }
                    }
                    else
                    {
                        cell.Append(c);
                    }
                    continue;
                }
                switch (c)
                {
                    case '"':
                        quoted = true;
                        break;
                    case ',':
                        row.Add(cell.ToString());
                        cell.Clear();
                        break;
                    case '\r':
                        break;
                    case '\n':
                        row.Add(cell.ToString());
                        cell.Clear();
                        rows.Add(row);
                        row = new List<string>();
                        break;
                    case '﻿':
                        break;
                    default:
                        cell.Append(c);
                        break;
                }
            }
            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                rows.Add(row);
            }
            return rows;
        }
    }
}
