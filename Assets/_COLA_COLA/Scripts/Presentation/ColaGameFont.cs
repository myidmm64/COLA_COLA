using UnityEngine;
using ColaCola.Core;
using System.Collections.Generic;

namespace ColaCola.Presentation
{
    /// <summary>Shared smooth system font for ordinary UI and world text; mesh lettering is separate.</summary>
    public static class ColaGameFont
    {
        private static Font cached;
        private static readonly Dictionary<string, Font> bundled = new();
        private static string cachedLanguage;

        public static Font Load()
        {
            string language = ColaLocalization.Language;
            if (!cached || cachedLanguage != language)
            {
                cachedLanguage = language;
                cached = Bundled(LanguageFont(language));
                if (!cached) cached = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Yu Gothic", "Microsoft YaHei", "Arial" }, 18);
                Font.textureRebuilt -= OnTextureRebuilt;
                Font.textureRebuilt += OnTextureRebuilt;
            }
            KeepTextStable(cached);
            return cached;
        }

        public static Font LoadMenu(int candidate = 1)
        {
            string language = ColaLocalization.Language;
            string file = language == "ko" ? candidate switch
            {
                1 => "NotoSansKR-Regular", 2 => "GowunBatang-Regular", _ => "NotoSerifKR-Regular"
            } : LanguageFont(language);
            return Bundled(file) ?? Load();
        }

        private static string LanguageFont(string language) => language switch
        {
            "ja" => "NotoSansJP-Regular", "zh-Hans" => "NotoSansSC-Regular",
            "zh-Hant" => "NotoSansTC-Regular", _ => "NotoSansKR-Regular"
        };

        private static Font Bundled(string name)
        {
            if (!bundled.TryGetValue(name, out var font) || !font)
            { font = Resources.Load<Font>("Fonts/" + name); if (font) bundled[name] = font; }
            KeepTextStable(font);
            return font;
        }

        private static void OnTextureRebuilt(Font rebuilt)
        {
            if (rebuilt == cached) KeepTextStable(rebuilt);
        }

        private static void KeepTextStable(Font font)
        {
            if (font && font.material && font.material.mainTexture)
                font.material.mainTexture.filterMode = FilterMode.Bilinear;
        }
    }
}
