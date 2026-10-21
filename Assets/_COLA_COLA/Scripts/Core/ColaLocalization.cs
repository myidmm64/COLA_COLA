using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColaCola.Core
{
    public static class ColaLocalization
    {
        [Serializable] public sealed class Row { public string key; public string[] languages, texts; }
        [Serializable] public sealed class Table { public List<Row> rows = new(); }
        private static Dictionary<string, Row> rows;
        private static string language;
        private static readonly List<string> languages = new();
        private static readonly HashSet<string> translated = new(StringComparer.Ordinal);
        public static IReadOnlyList<string> Languages { get { EnsureLoaded(); return languages; } }
        public static bool HasTranslation(string code) { EnsureLoaded(); return translated.Contains(code); }
        public static string NativeName(string code) => code switch
        {
            "ko" => "한국어", "en" => "English", "ja" => "日本語",
            "zh-Hans" => "简体中文", "zh-Hant" => "繁體中文", "fr" => "Français",
            "de" => "Deutsch", "es" => "Español", "pt-BR" => "Português (Brasil)", "ru" => "Русский",
            _ => code
        };
        public static string Language => language ??= PlayerPrefs.GetString("COLA_COLA.Language", "ko");
        public static void SetLanguage(string language) { ColaLocalization.language = language; PlayerPrefs.SetString("COLA_COLA.Language", language); PlayerPrefs.Save(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reload() { rows = null; language = null; }
        private static void EnsureLoaded()
        {
            if (rows != null) return;
            rows = new Dictionary<string, Row>(StringComparer.Ordinal);
            languages.Clear(); translated.Clear();
            var asset = Resources.Load<TextAsset>("Localization/Strings");
            if (asset)
            {
                var table = JsonUtility.FromJson<Table>(asset.text);
                if (table?.rows != null) foreach (var entry in table.rows)
                {
                    if (string.IsNullOrEmpty(entry.key) || entry.languages == null || entry.texts == null) continue;
                    rows[entry.key] = entry;
                    for (int i = 0; i < entry.languages.Length; i++)
                    {
                        string code = entry.languages[i];
                        if (string.IsNullOrWhiteSpace(code)) continue;
                        if (!languages.Contains(code)) languages.Add(code);
                        if (i < entry.texts.Length && !string.IsNullOrWhiteSpace(entry.texts[i])) translated.Add(code);
                    }
                }
            }
            if (!languages.Contains("ko")) languages.Insert(0, "ko");
            translated.Add("ko");
        }
        public static string Get(string key, string fallback)
        {
            EnsureLoaded();
            if (!rows.TryGetValue(key, out var row) || row.languages == null || row.texts == null) return fallback;
            int selected = Array.IndexOf(row.languages, Language);
            if (selected >= 0 && selected < row.texts.Length && !string.IsNullOrEmpty(row.texts[selected])) return row.texts[selected];
            int korean = Array.IndexOf(row.languages, "ko");
            return korean >= 0 && korean < row.texts.Length && !string.IsNullOrEmpty(row.texts[korean]) ? row.texts[korean] : fallback;
        }
    }
}
