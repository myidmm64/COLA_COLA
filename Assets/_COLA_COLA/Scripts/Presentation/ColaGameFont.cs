using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Shared smooth system font for ordinary UI and world text; mesh lettering is separate.</summary>
    public static class ColaGameFont
    {
        private static Font cached;

        public static Font Load()
        {
            if (!cached)
            {
                cached = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Noto Sans CJK KR", "Arial" }, 18);
                Font.textureRebuilt -= OnTextureRebuilt;
                Font.textureRebuilt += OnTextureRebuilt;
            }
            KeepTextStable(cached);
            return cached;
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
