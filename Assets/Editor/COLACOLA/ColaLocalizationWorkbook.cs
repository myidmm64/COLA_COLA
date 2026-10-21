using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using ColaCola.Core;
using UnityEditor;
using UnityEngine;

namespace ColaCola.Editor
{
    public sealed class ColaLocalizationWorkbook : AssetPostprocessor
    {
        public const string WorkbookPath = "Assets/Localization/COLA_COLA_Translations.xlsx";
        private const string ResourcePath = "Assets/_COLA_COLA/Resources/Localization/Strings.json";
        private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] from)
        {
            if (imported.Contains(WorkbookPath)) Import();
        }
        [MenuItem("COLA_COLA/Localization/Import translation workbook")]
        public static void Import()
        {
            if (!File.Exists(WorkbookPath)) return;
            try
            {
                using var stream = new FileStream(WorkbookPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
                string[] shared = Array.Empty<string>();
                var sharedEntry = zip.GetEntry("xl/sharedStrings.xml");
                if (sharedEntry != null)
                {
                    using var input = sharedEntry.Open();
                    shared = XDocument.Load(input).Descendants(Ns + "si").Select(x => string.Concat(x.Descendants(Ns + "t").Select(t => t.Value))).ToArray();
                }
                var worksheet = zip.GetEntry("xl/worksheets/sheet1.xml");
                if (worksheet == null) throw new InvalidDataException("Translations must remain the first worksheet.");
                using var xml = worksheet.Open();
                var rows = XDocument.Load(xml).Descendants(Ns + "row");
                var headers = new Dictionary<int, string>();
                var table = new ColaLocalization.Table();
                var keys = new HashSet<string>();
                foreach (var row in rows)
                {
                    var values = new Dictionary<int, string>();
                    foreach (var cell in row.Elements(Ns + "c"))
                    {
                        string address = (string)cell.Attribute("r") ?? ""; int column = 0;
                        foreach (char c in address.TakeWhile(char.IsLetter)) column = column * 26 + char.ToUpperInvariant(c) - 'A' + 1;
                        string type = (string)cell.Attribute("t");
                        string value = type == "inlineStr" ? string.Concat(cell.Descendants(Ns + "t").Select(t => t.Value)) : (string)cell.Element(Ns + "v") ?? "";
                        if (type == "s" && int.TryParse(value, out int index) && index < shared.Length) value = shared[index];
                        values[column] = value;
                    }
                    if (values.TryGetValue(1, out string first) && first == "Key") { headers = values; continue; }
                    if (headers.Count == 0 || string.IsNullOrWhiteSpace(first)) continue;
                    if (!keys.Add(first)) throw new InvalidDataException("Duplicate translation key: " + first);
                    var languages = headers.Where(p => p.Key >= 3 && !string.IsNullOrWhiteSpace(p.Value)).OrderBy(p => p.Key).ToArray();
                    table.rows.Add(new ColaLocalization.Row { key = first, languages = languages.Select(p => p.Value).ToArray(), texts = languages.Select(p => values.TryGetValue(p.Key, out var value) ? value.Replace("\\n", "\n") : "").ToArray() });
                }
                if (table.rows.Count == 0) throw new InvalidDataException("No translation keys found.");
                string content = JsonUtility.ToJson(table, true);
                Directory.CreateDirectory(Path.GetDirectoryName(ResourcePath));
                if (!File.Exists(ResourcePath) || File.ReadAllText(ResourcePath) != content)
                { File.WriteAllText(ResourcePath, content); AssetDatabase.ImportAsset(ResourcePath); }
                ColaLocalization.Reload();
            }
            catch (Exception error) { Debug.LogError("Translation workbook import failed: " + error.Message); }
        }
        [MenuItem("COLA_COLA/Localization/Language/Korean")]
        private static void Korean() => ColaLocalization.SetLanguage("ko");
        [MenuItem("COLA_COLA/Localization/Language/English")]
        private static void English() => ColaLocalization.SetLanguage("en");
        [MenuItem("COLA_COLA/Localization/Language/Japanese")]
        private static void Japanese() => ColaLocalization.SetLanguage("ja");
    }
}
