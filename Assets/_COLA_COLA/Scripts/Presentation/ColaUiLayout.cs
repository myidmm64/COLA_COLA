using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>One text scale for a 720p composition, including native 1080p, 1440p and 4K output.</summary>
    public static class ColaUiLayout
    {
        public static float Scale => Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), .65f, 3f);
        public static float Margin => Mathf.Round(20 * Scale);
        public static float BottomLift => Mathf.Round(16 * Scale);
    }
}
