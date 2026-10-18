using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Debug shortcut hints and match-end controls. Normal status stays on the physical props.</summary>
    [DisallowMultipleComponent]
    public sealed class ColaDuelHUD : MonoBehaviour
    {
        public ColaDuelController match;

        private static readonly Color PanelColor = new Color(.045f, .065f, .052f, 1);
        private static readonly Color BorderColor = new Color(.25f, .32f, .24f, 1);
        private static readonly Color CreamColor = new Color(.84f, .82f, .67f, 1);
        private static readonly Color GreenColor = new Color(.57f, .70f, .49f, 1);
        private static readonly Color MutedColor = new Color(.58f, .62f, .51f, 1);
        private static readonly Color ButtonColor = new Color(.16f, .23f, .16f, 1);

        private GUIStyle titleStyle, detailStyle, buttonStyle, debugStyle, debugShadowStyle;
        private GUISkin styledSkin;
        private Font runtimeFont;

        private void OnGUI()
        {
            if (!Application.IsPlaying(gameObject)) return;
            if (!match) match = FindAnyObjectByType<ColaDuelController>();
            if (!match || Screen.width < 160 || Screen.height < 120) return;
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 960f, Screen.height / 640f), .7f, 1.15f);
            EnsureStyles(scale);
            if (match.DebugToolsAvailable) DrawDebugShortcuts(scale);
            if (match.State == ColaDuelController.DuelState.Playing) return;
            float padding = Mathf.Round(9 * scale);
            bool busy = match.interaction && match.interaction.IsBusy;
            DrawMatchEnd(match.State == ColaDuelController.DuelState.Won, busy, scale, padding);
        }

        private void DrawDebugShortcuts(float scale)
        {
            var sheet = match.GetComponent<ColaOrderSheet>();
            string[] lines =
            {
                "Q  토큰 999",
                "W  폭발 확률 100%  [" + (match.GuaranteedBurst ? "ON" : "OFF") + "]",
                "E  메뉴판 (모두 무료)  [" + (sheet && sheet.IsOpen ? "ON" : "OFF") + "]"
            };
            for (int i = 0; i < lines.Length; i++)
            {
                var rect = new Rect(12 * scale, (12 + i * 22) * scale, Screen.width - 24 * scale, 22 * scale);
                GUI.Label(new Rect(rect.x + scale, rect.y + scale, rect.width, rect.height), lines[i], debugShadowStyle);
                GUI.Label(rect, lines[i], debugStyle);
            }
        }

        private void DrawMatchEnd(bool won, bool busy, float scale, float padding)
        {
            float width = Mathf.Min(440 * scale, Screen.width - 24 * scale);
            float height = Mathf.Min(192 * scale, Screen.height - 86 * scale);
            var panel = new Rect(Mathf.Round((Screen.width - width) * .5f),
                Mathf.Round((Screen.height - height) * .5f), width, height);
            DrawPanel(panel);
            GUI.Label(new Rect(panel.x + padding, panel.y + 14 * scale, panel.width - padding * 2, 32 * scale),
                won ? "BOSS DEFEATED" : "YOU LOST", titleStyle);
            GUI.Label(new Rect(panel.x + padding, panel.y + 53 * scale, panel.width - padding * 2, 23 * scale),
                won ? "SAME BOSS. HIGHER DIFFICULTY." : "RESET AND TRY AGAIN.", detailStyle);

            bool previousEnabled = GUI.enabled;
            Color previousBackground = GUI.backgroundColor;
            GUI.enabled = previousEnabled && !busy;
            GUI.backgroundColor = ButtonColor;
            var button = new Rect(panel.x + 24 * scale, panel.y + 91 * scale,
                panel.width - 48 * scale, 37 * scale);
            bool pressed = GUI.Button(button, won ? "NEXT LEVEL" : "RESTART RUN", buttonStyle);
            GUI.backgroundColor = previousBackground;
            GUI.enabled = previousEnabled;
            if (pressed)
            {
                if (won) match.ContinueToNextBoss();
                else match.RestartRun(fadeIn: true);
            }

            GUI.Label(new Rect(panel.x + padding, panel.yMax - 36 * scale, panel.width - padding * 2, 23 * scale),
                won ? "ENTER TO CONTINUE" : "ENTER / R TO RESTART", detailStyle);
        }

        private void EnsureStyles(float scale)
        {
            if (titleStyle == null || styledSkin != GUI.skin)
            {
                styledSkin = GUI.skin;
                if (!runtimeFont) runtimeFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Noto Sans CJK KR", "Arial" }, 18);
                titleStyle = FlatLabel(CreamColor, TextAnchor.MiddleCenter);
                detailStyle = FlatLabel(MutedColor, TextAnchor.MiddleCenter);
                debugStyle = FlatLabel(CreamColor, TextAnchor.MiddleLeft);
                debugShadowStyle = FlatLabel(new Color(.015f, .02f, .015f, .95f), TextAnchor.MiddleLeft);
                buttonStyle = new GUIStyle
                {
                    font = GUI.skin.font,
                    alignment = TextAnchor.MiddleCenter,
                    border = new RectOffset(),
                    margin = new RectOffset(),
                    padding = new RectOffset(3, 3, 1, 1),
                    fontStyle = FontStyle.Bold
                };
                foreach (var style in new[] { titleStyle, detailStyle, buttonStyle, debugStyle, debugShadowStyle })
                    style.font = runtimeFont;
                ConfigureButtonState(buttonStyle.normal, CreamColor);
                ConfigureButtonState(buttonStyle.hover, Color.white);
                ConfigureButtonState(buttonStyle.active, GreenColor);
                ConfigureButtonState(buttonStyle.focused, CreamColor);
                ConfigureButtonState(buttonStyle.onNormal, CreamColor);
                ConfigureButtonState(buttonStyle.onHover, Color.white);
                ConfigureButtonState(buttonStyle.onActive, GreenColor);
                ConfigureButtonState(buttonStyle.onFocused, CreamColor);
            }

            titleStyle.fontSize = Mathf.RoundToInt(23 * scale);
            detailStyle.fontSize = Mathf.RoundToInt(12 * scale);
            buttonStyle.fontSize = Mathf.RoundToInt(15 * scale);
            debugStyle.fontSize = debugShadowStyle.fontSize = Mathf.RoundToInt(14 * scale);
        }

        private static GUIStyle FlatLabel(Color color, TextAnchor alignment)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = alignment,
                clipping = TextClipping.Clip,
                wordWrap = false,
                margin = new RectOffset(),
                padding = new RectOffset(),
                richText = false
            };
            style.normal.textColor = color;
            return style;
        }

        private static void ConfigureButtonState(GUIStyleState state, Color textColor)
        {
            state.background = Texture2D.whiteTexture;
            state.textColor = textColor;
        }

        private void OnDestroy() { if (runtimeFont) Destroy(runtimeFont); }

        private static Rect Inset(Rect rect, float horizontal, float vertical)
        {
            return new Rect(rect.x + horizontal, rect.y + vertical,
                rect.width - horizontal * 2, rect.height - vertical * 2);
        }

        private static void DrawPanel(Rect rect)
        {
            Color previous = GUI.color;
            GUI.color = BorderColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = PanelColor;
            GUI.DrawTexture(Inset(rect, 1, 1), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
