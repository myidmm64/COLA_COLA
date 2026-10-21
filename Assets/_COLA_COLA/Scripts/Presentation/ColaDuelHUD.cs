using ColaCola.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ColaCola.Presentation
{
    /// <summary>Debug hints and match-end controls; turn feedback lives on the physical console.</summary>
    [DisallowMultipleComponent]
    public sealed class ColaDuelHUD : MonoBehaviour
    {
        public ColaDuelController match;

        private static readonly Color CreamColor = new Color(.84f, .82f, .67f, 1);
        private static readonly Color GreenColor = new Color(.57f, .70f, .49f, 1);
        private static readonly Color ButtonColor = new Color(.16f, .23f, .16f, 1);
        private static readonly Color PaperColor = new Color(.78f, .76f, .65f, 1);
        private static readonly Color PaperInk = new Color(.13f, .18f, .14f, 1);
        private static readonly Color PaperMuted = new Color(.36f, .39f, .32f, 1);

        private GUIStyle buttonStyle, debugStyle, debugShadowStyle, journalHintStyle;
        private GUIStyle receiptTitle, receiptDetail, receiptSmall;
        private GUISkin styledSkin;
        private Font runtimeFont;
        private Rect debugRect, journalButtonRect;

        public bool BlocksPointer(Vector2 screenPoint)
        {
            if (!match) return false;
            if (match.Progression?.BlocksGameplay ?? false) return true;
            Vector2 point = new(screenPoint.x, Screen.height - screenPoint.y);
            return debugRect.Contains(point) || journalButtonRect.Contains(point);
        }

        private void OnGUI()
        {
            if (!Application.IsPlaying(gameObject) || ColaFrontEnd.BlocksGameplay) return;
            if (!match) match = FindAnyObjectByType<ColaDuelController>();
            if (!match || Screen.width < 160 || Screen.height < 120) return;
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 960f, Screen.height / 640f), .7f, 1.15f);
            EnsureStyles(scale);
            debugRect = journalButtonRect = default;
            var progression = match.Progression;
            if (progression == null) return;
            if (!match.interaction || !match.interaction.IsPlayerShaking)
            {
                var tabContent = new GUIContent(ColaLocalization.Get("ui.tab_hint", "TAB 능력 / 업적"));
                float hintWidth = Mathf.Min(Screen.width - 32 * scale, Mathf.Max(240 * scale, journalHintStyle.CalcSize(tabContent).x));
                var tabHint = new Rect(Mathf.Round(16 * scale), Mathf.Round(Screen.height - 32 * scale), hintWidth, 20 * scale);
                int hintDepth = GUI.depth;
                GUI.depth = -25;
                GUI.Label(tabHint, tabContent, journalHintStyle);
                GUI.depth = hintDepth;
            }
            if (progression.ChoiceReady || progression.JournalOpen) return;
            var sheet = match.GetComponent<ColaOrderSheet>();
            bool menuOpen = sheet && sheet.BlocksGameplay;
            bool busyNow = match.interaction && match.interaction.IsBusy;
            if (match.DebugToolsAvailable && !busyNow && !menuOpen) DrawDebugShortcuts(scale);
            if (!busyNow && !menuOpen && !progression.HasPendingChoice)
            {
                string notice = progression.CurrentNotice;
                if (!string.IsNullOrEmpty(notice))
                    GUI.Label(new Rect(16 * scale, Screen.height - 62 * scale, 430 * scale, 26 * scale), notice, debugStyle);
            }
            if (match.State == ColaDuelController.DuelState.Playing) return;
            float padding = Mathf.Round(9 * scale);
            bool busy = match.interaction && match.interaction.IsBusy;
            if (busy) return;
            DrawMatchEnd(match.State == ColaDuelController.DuelState.Won, match.State == ColaDuelController.DuelState.Completed, busy, scale, padding);
        }

        private void DrawDebugShortcuts(float scale)
        {
            var sheet = match.GetComponent<ColaOrderSheet>();
            string[] lines =
            {
                ColaLocalization.Get("ui.debug_tokens", "Q  토큰 999"),
                ColaLocalization.Get("ui.debug_burst", "W  폭발 확률 100%") + "  [" + (match.GuaranteedBurst ? "ON" : "OFF") + "]",
                ColaLocalization.Get("ui.debug_menu", "E  메뉴판 (모두 무료)") + "  [" + (sheet && sheet.IsOpen ? "ON" : "OFF") + "]",
                ColaLocalization.Get("ui.debug_clear", "R  양쪽 아이템 비우기"),
                ColaLocalization.Get("ui.debug_tools", "T  업적 테스트  /  Y  능력 테스트"),
                ColaLocalization.Get("ui.debug_win", "V  현재 단계 승리 테스트")
            };
            float rowHeight = Mathf.Max(22 * scale, debugStyle.lineHeight + 4);
            for (int i = 0; i < lines.Length; i++)
            {
                float row = rowHeight;
                var rect = new Rect(Mathf.Round(12 * scale), Mathf.Round(12 * scale + i * row),
                    Mathf.Round(Screen.width - 24 * scale), Mathf.Ceil(row));
                GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), lines[i], debugShadowStyle);
                GUI.Label(rect, lines[i], debugStyle);
            }
            float y = 12 * scale + lines.Length * rowHeight + 3 * scale;
            GUI.Label(new Rect(12 * scale, y, 44 * scale, 28 * scale), ColaLocalization.Get("ui.stage", "단계"), debugStyle);
            for (int stage = 1; stage <= 5; stage++)
            {
                Color old = GUI.backgroundColor;
                GUI.backgroundColor = stage == match.Level ? GreenColor : ButtonColor;
                if (GUI.Button(new Rect((58 + (stage - 1) * 34) * scale, y, 30 * scale, 28 * scale), stage.ToString(), buttonStyle))
                    match.ApplyDebugCommand((ColaDuelController.DebugCommand)((int)ColaDuelController.DebugCommand.Stage1 + stage - 1));
                GUI.backgroundColor = old;
            }
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = ButtonColor;
            if (GUI.Button(new Rect(236 * scale, y, 60 * scale, 28 * scale), ColaLocalization.Get("ui.win", "승리"), buttonStyle))
                match.ApplyDebugCommand(ColaDuelController.DebugCommand.WinCurrentStage);
            GUI.backgroundColor = previous;
            debugRect = new Rect(10 * scale, 10 * scale, 300 * scale, y + 30 * scale);
        }

        private void DrawMatchEnd(bool won, bool completed, bool busy, float scale, float padding)
        {
            float width = Mathf.Min(440 * scale, Screen.width - 24 * scale);
            float height = Mathf.Min(258 * scale, Screen.height - 32 * scale);
            var panel = new Rect(Mathf.Round((Screen.width - width) * .5f),
                Mathf.Round((Screen.height - height) * .5f), width, height);
            DrawReceiptPanel(panel, scale);
            float progressWidth = 116 * scale;
            float progressX = panel.center.x - progressWidth * .5f;
            for (int i = 0; i < ColaDuelController.FinalLevel; i++)
                Fill(new Rect(progressX + i * 24 * scale, panel.y + 21 * scale, 20 * scale, 3 * scale),
                    i < match.Level - (won || completed ? 0 : 1) ? PaperInk : new Color(.58f, .58f, .49f));
            GUI.Label(new Rect(panel.x + padding, panel.y + 41 * scale, panel.width - padding * 2, 38 * scale),
                completed ? ColaLocalization.Get("ui.run_complete", "도전 완료") : won ? ColaLocalization.Get("ui.boss_defeated", "보스 격파") : ColaLocalization.Get("ui.run_over", "도전 종료"), receiptTitle);
            GUI.Label(new Rect(panel.x + padding, panel.y + 82 * scale, panel.width - padding * 2, 23 * scale),
                match.Level.ToString("00") + " / 05  ·  " + match.BossName, receiptDetail);
            for (float x = panel.x + 27 * scale; x < panel.xMax - 27 * scale; x += 9 * scale)
                Fill(new Rect(x, panel.y + 118 * scale, 4 * scale, 1), PaperMuted);
            GUI.Label(new Rect(panel.x + padding, panel.y + 131 * scale, panel.width - padding * 2, 23 * scale),
                string.Format(ColaLocalization.Get("ui.run_resources", "체력 {0}/{1}    ·    토큰 {2}"), match.PlayerHealth, match.playerMaxHealth, match.PlayerTokens), receiptDetail);

            bool previousEnabled = GUI.enabled;
            Color previousBackground = GUI.backgroundColor;
            GUI.enabled = previousEnabled && !busy;
            GUI.backgroundColor = ButtonColor;
            var button = new Rect(panel.x + 28 * scale, panel.y + 173 * scale,
                panel.width - 56 * scale, 39 * scale);
            bool pressed = GUI.Button(button, won ? ColaLocalization.Get("ui.next_boss", "다음 보스") : ColaLocalization.Get("ui.new_run", "새 도전"), buttonStyle);
            GUI.backgroundColor = previousBackground;
            GUI.enabled = previousEnabled;
            if (pressed)
            {
                if (won) match.ContinueToNextBoss();
                else match.RestartRun(fadeIn: true);
            }

            GUI.Label(new Rect(panel.x + padding, panel.yMax - 34 * scale, panel.width - padding * 2, 23 * scale),
                won ? ColaLocalization.Get("ui.enter_next", "ENTER  다음 보스") : ColaLocalization.Get("ui.enter_new", "ENTER  새 도전"), receiptSmall);
        }

        private void EnsureStyles(float scale)
        {
            if (buttonStyle == null || styledSkin != GUI.skin)
            {
                styledSkin = GUI.skin;
                if (!runtimeFont) runtimeFont = ColaGameFont.Load();
                debugStyle = FlatLabel(CreamColor, TextAnchor.MiddleLeft);
                debugShadowStyle = FlatLabel(new Color(.015f, .02f, .015f, .95f), TextAnchor.MiddleLeft);
                journalHintStyle = FlatLabel(new Color(.59f, .61f, .55f, .62f), TextAnchor.MiddleLeft);
                receiptTitle = FlatLabel(PaperInk, TextAnchor.MiddleCenter);
                receiptDetail = FlatLabel(PaperMuted, TextAnchor.MiddleCenter);
                receiptSmall = FlatLabel(PaperMuted, TextAnchor.MiddleCenter);
                buttonStyle = new GUIStyle
                {
                    font = GUI.skin.font,
                    alignment = TextAnchor.MiddleCenter,
                    border = new RectOffset(),
                    margin = new RectOffset(),
                    padding = new RectOffset(3, 3, 1, 1),
                    fontStyle = FontStyle.Normal
                };
                foreach (var style in new[] { buttonStyle, debugStyle, debugShadowStyle, journalHintStyle, receiptTitle, receiptDetail, receiptSmall })
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

            buttonStyle.fontSize = Mathf.RoundToInt(15 * scale);
            debugStyle.fontSize = debugShadowStyle.fontSize = Mathf.RoundToInt(14 * scale);
            journalHintStyle.fontSize = Mathf.RoundToInt(12 * scale);
            receiptTitle.fontSize = Mathf.RoundToInt(27 * scale);
            receiptDetail.fontSize = Mathf.RoundToInt(14 * scale);
            receiptSmall.fontSize = Mathf.RoundToInt(11 * scale);
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
                fontStyle = FontStyle.Normal,
                richText = false
            };
            foreach (var state in new[] { style.normal, style.hover, style.active, style.focused,
                style.onNormal, style.onHover, style.onActive, style.onFocused }) state.textColor = color;
            return style;
        }

        private static void ConfigureButtonState(GUIStyleState state, Color textColor)
        {
            state.background = Texture2D.whiteTexture;
            state.textColor = textColor;
        }

        private static Rect Inset(Rect rect, float horizontal, float vertical)
        {
            return new Rect(rect.x + horizontal, rect.y + vertical,
                rect.width - horizontal * 2, rect.height - vertical * 2);
        }

        private static void DrawReceiptPanel(Rect rect, float scale)
        {
            Fill(new Rect(rect.x + 5 * scale, rect.y + 7 * scale, rect.width, rect.height), new Color(.01f, .015f, .01f, .68f));
            Fill(rect, PaperInk);
            Fill(Inset(rect, 2 * scale, 2 * scale), PaperColor);
            Fill(new Rect(rect.x + 2 * scale, rect.y + 2 * scale, rect.width - 4 * scale, 4 * scale), new Color(.40f, .21f, .15f));
        }

        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
