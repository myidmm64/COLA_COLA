using System.Collections.Generic;
using ColaCola.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ColaCola.Presentation
{
    [DefaultExecutionOrder(-300), DisallowMultipleComponent]
    public sealed class ColaFrontEnd : MonoBehaviour
    {
        private enum Page { Hidden, Main, Settings, Credits }
        private enum Dropdown { None, Resolution, Language }
        private static ColaFrontEnd instance;
        private Page page, settingsReturn;
        private ColaDuelController match;
        private GUIStyle title, small, text, choice, heading, valueStyle;
        private Texture2D menuShade;
        private Font styledFont;
        private readonly float[] menuHover = new float[3];
        private int menuFontCandidate = 1;
        public int MenuFontCandidate { get => menuFontCandidate; set { menuFontCandidate = Mathf.Clamp(value, 0, 2); styledFont = null; } }
        private ColaMenuBackdrop backdrop;
        private Dropdown dropdown;
        private Vector2 dropdownScroll, creditsScroll;
        private Rect dropdownButtonRect;
        private readonly List<string> languages = new();
        private readonly List<Vector2Int> sizes = new();
        private float previousTimeScale = 1, volume = 1;
        private int resolution;
        private bool fullscreen;
        private static int escapeFrame = -1;
        public static bool ConsumedEscapeThisFrame => escapeFrame == Time.frameCount;
        public static bool BlocksGameplay => instance && instance.page != Page.Hidden;
        public static bool AnimatesMenuBackground => instance && (instance.page == Page.Main || instance.page == Page.Credits ||
            instance.page == Page.Settings && instance.settingsReturn == Page.Main);
        private static readonly Vector2Int[] ResolutionPresets = {
            new(1280, 720), new(1366, 768), new(1600, 900), new(1920, 1080),
            new(2560, 1080), new(2560, 1440), new(3440, 1440), new(3840, 2160)
        };

        public static ColaFrontEnd EnsureFor(ColaDuelController owner)
        {
            var front = owner.GetComponent<ColaFrontEnd>();
            if (!front) front = owner.gameObject.AddComponent<ColaFrontEnd>();
            front.match = owner; instance = front;
            return front;
        }

        private void Awake()
        {
            instance = this;
            volume = Mathf.Clamp01(PlayerPrefs.GetFloat("COLA_COLA.MasterVolume", .8f));
            AudioListener.volume = volume;
            fullscreen = PlayerPrefs.GetInt("COLA_COLA.Fullscreen", Screen.fullScreen ? 1 : 0) != 0;
            // A compact authored list; never expose every refresh-rate/driver mode.
            int desktopWidth = Mathf.Max(Screen.currentResolution.width, Screen.width);
            int desktopHeight = Mathf.Max(Screen.currentResolution.height, Screen.height);
            foreach (var size in ResolutionPresets)
                if (size.x <= desktopWidth && size.y <= desktopHeight) sizes.Add(size);
            if (sizes.Count == 0) sizes.Add(ResolutionPresets[0]);
            var saved = new Vector2Int(PlayerPrefs.GetInt("COLA_COLA.Width", 1920), PlayerPrefs.GetInt("COLA_COLA.Height", 1080));
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < sizes.Count; i++)
            {
                float distance = Mathf.Abs(sizes[i].x - saved.x) + Mathf.Abs(sizes[i].y - saved.y);
                if (distance < nearest) { nearest = distance; resolution = i; }
            }
            ApplyDisplay();
            languages.AddRange(ColaLocalization.Languages);

        }

        public void ShowMainMenu()
        {
            if (match && match.interaction)
            {
                match.Items?.GetComponent<ColaOrderSheet>()?.Close();
                match.interaction.CancelPendingActions();
                if (match.Progression != null) match.Progression.JournalOpen = false;
            }
            SetPage(Page.Main);
        }

        private void SetPage(Page next)
        {
            bool wasPaused = page != Page.Hidden;
            bool wasMenuBackground = AnimatesMenuBackground;
            page = next; dropdown = Dropdown.None;
            bool paused = next != Page.Hidden;
            if (!wasPaused && paused) previousTimeScale = Time.timeScale;
            ColaGameClock.SetPaused(paused); AudioListener.pause = paused;
            Time.timeScale = paused ? 0 : previousTimeScale;
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
            if (!backdrop) { backdrop = GetComponent<ColaMenuBackdrop>(); if (!backdrop) backdrop = gameObject.AddComponent<ColaMenuBackdrop>(); }
            backdrop.SetVisible(AnimatesMenuBackground, match && match.interaction ? match.interaction.playerCamera : null);
            if (!wasMenuBackground && AnimatesMenuBackground)
                GetComponent<ColaPizzeriaStaff>()?.BeginMenuAmbience();
        }

        private void Update()
        {
            if (!Application.IsPlaying(gameObject) || Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
            escapeFrame = Time.frameCount;
            if (dropdown != Dropdown.None) { dropdown = Dropdown.None; return; }
            if (page == Page.Settings) { Save(); SetPage(settingsReturn); }
            else if (page == Page.Credits) SetPage(Page.Main);
            else { settingsReturn = page; SetPage(Page.Settings); }
        }

        private static string L(string key, string fallback) => ColaLocalization.Get("ui." + key, fallback);
        private void OnGUI()
        {
            if (!Application.IsPlaying(gameObject) || page == Page.Hidden) return;
            EnsureStyles();
            int depth = GUI.depth; GUI.depth = -20000;
            var previous = GUI.color; GUI.color = Color.white;
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), .55f, 2);
            DrawMenuShade(page == Page.Main);
            float x = 100 * scale, w = Mathf.Min(480 * scale, Screen.width - x * 2);
            title.fontSize = Mathf.RoundToInt(76 * scale); text.fontSize = choice.fontSize = Mathf.RoundToInt(22 * scale);
            small.fontSize = Mathf.RoundToInt(13 * scale); heading.fontSize = Mathf.RoundToInt(32 * scale);
            valueStyle.fontSize = Mathf.RoundToInt(17 * scale);
            if (page == Page.Main)
            {
                choice.fontSize = Mathf.RoundToInt(24 * scale);
                x = Screen.width * .065f;
                w = Mathf.Min(465 * scale, Screen.width * .46f);
                float y = Screen.height * .23f;
                GUI.Label(new Rect(x, y, w, 110 * scale), "FIZZERIA", title);
                y += 155 * scale;
                if (MainButton(new Rect(x + 25 * scale, y, w - 45 * scale, 53 * scale), L("start", "게임 시작"), 0, scale))
                { SetPage(Page.Hidden); match.RestartRun(fadeIn: true); }
                if (MainButton(new Rect(x + 25 * scale, y + 62 * scale, w - 45 * scale, 53 * scale), L("settings", "설정"), 1, scale))
                { settingsReturn = Page.Main; SetPage(Page.Settings); }
                if (MainButton(new Rect(x + 25 * scale, y + 124 * scale, w - 45 * scale, 53 * scale), L("quit", "게임 종료"), 2, scale)) Quit();
                if (GUI.Button(new Rect(Screen.width - 150 * scale, Screen.height - 55 * scale, 120 * scale, 30 * scale), L("credits", "크레딧"), small)) SetPage(Page.Credits);
            }
            else if (page == Page.Settings) DrawSettings(scale);
            else
            {
                float y = Screen.height * .12f;
                GUI.Label(new Rect(x, y, w, 60 * scale), L("credits", "크레딧"), heading);
                var credits = Resources.Load<TextAsset>("Localization/Credits");
                string creditText = credits ? credits.text : "FIZZERIA\nMade with Unity";
                var viewport = new Rect(x, y + 70 * scale, Screen.width - x * 2, Screen.height - y - 190 * scale);
                var content = new Rect(0, 0, viewport.width - 26 * scale, text.CalcHeight(new GUIContent(creditText), viewport.width - 26 * scale));
                creditsScroll = GUI.BeginScrollView(viewport, creditsScroll, content);
                GUI.Label(content, creditText, text);
                GUI.EndScrollView();
                if (Button(new Rect(x, Screen.height - 100 * scale, w, 45 * scale), L("back", "돌아가기"))) SetPage(Page.Main);
            }
            GUI.color = previous; GUI.depth = depth;
        }

        private void DrawSettings(float scale)
        {
            float x = Mathf.Max(36 * scale, (Screen.width - 540 * scale) * .5f);
            float w = Mathf.Min(540 * scale, Screen.width - x * 2);
            float y = Mathf.Max(24 * scale, (Screen.height - 480 * scale) * .5f);
            bool enabledBefore = GUI.enabled;
            GUI.enabled = enabledBefore && dropdown == Dropdown.None;
            GUI.Label(new Rect(x, y, w, 50 * scale), L("settings", "설정"), heading);
            if (GUI.Button(new Rect(x + w - 36 * scale, y, 36 * scale, 36 * scale), "×", choice)) { Save(); SetPage(settingsReturn); }
            y += 80 * scale;
            GUI.Label(new Rect(x, y, w, 35 * scale), L("master_volume", "메인 볼륨"), text);
            DrawVolumeSlider(new Rect(x, y + 43 * scale, w - 85 * scale, 28 * scale), scale);
            GUI.Label(new Rect(x + w - 70 * scale, y + 38 * scale, 70 * scale, 30 * scale), Mathf.RoundToInt(volume * 100).ToString(), valueStyle);
            y += 100 * scale;
            GUI.Label(new Rect(x, y, w * .44f, 38 * scale), L("resolution", "해상도"), text);
            GUI.enabled = enabledBefore;
            DrawDropdownButton(new Rect(x + w * .45f, y, w * .55f, 40 * scale), Dropdown.Resolution,
                sizes[resolution].x + " × " + sizes[resolution].y, scale);
            GUI.enabled = enabledBefore && dropdown == Dropdown.None;
            y += 63 * scale;
            GUI.Label(new Rect(x, y, w * .6f, 38 * scale), L("fullscreen", "전체화면"), text);
            if (GUI.Button(new Rect(x + w - 95 * scale, y, 95 * scale, 38 * scale), fullscreen ? "ON" : "OFF", valueStyle)) { fullscreen = !fullscreen; ApplyDisplay(); }
            y += 63 * scale;
            GUI.Label(new Rect(x, y, w * .42f, 38 * scale), L("language", "언어"), text);
            GUI.enabled = enabledBefore;
            DrawDropdownButton(new Rect(x + w * .45f, y, w * .55f, 40 * scale), Dropdown.Language,
                ColaLocalization.NativeName(ColaLocalization.Language), scale);
            GUI.enabled = enabledBefore && dropdown == Dropdown.None;
            y += 79 * scale;
            if (Button(new Rect(x, y, w, 48 * scale), L("main_menu", "메인 메뉴로 이동"))) { Save(); ShowMainMenu(); }
            GUI.enabled = enabledBefore;
            if (dropdown != Dropdown.None) DrawDropdownPopup(scale);
        }

        private void DrawVolumeSlider(Rect rect, float scale)
        {
            int id = GUIUtility.GetControlID(0xC01A, FocusType.Keyboard, rect);
            var e = Event.current;
            var track = new Rect(rect.x + 5 * scale, rect.y + 9 * scale, rect.width - 10 * scale, 7 * scale);
            float next = volume;
            if (GUI.enabled)
            {
                switch (e.GetTypeForControl(id))
                {
                    case EventType.MouseDown:
                        if (e.button == 0 && rect.Contains(e.mousePosition))
                        { GUIUtility.hotControl = GUIUtility.keyboardControl = id; next = Mathf.InverseLerp(track.x, track.xMax, e.mousePosition.x); e.Use(); }
                        break;
                    case EventType.MouseDrag:
                        if (GUIUtility.hotControl == id) { next = Mathf.InverseLerp(track.x, track.xMax, e.mousePosition.x); e.Use(); }
                        break;
                    case EventType.MouseUp:
                        if (GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); }
                        break;
                    case EventType.KeyDown:
                        if (GUIUtility.keyboardControl == id)
                        {
                            if (e.keyCode == KeyCode.LeftArrow) next -= .05f;
                            else if (e.keyCode == KeyCode.RightArrow) next += .05f;
                            else if (e.keyCode == KeyCode.Home) next = 0;
                            else if (e.keyCode == KeyCode.End) next = 1;
                            else break;
                            e.Use();
                        }
                        break;
                }
            }
            next = Mathf.Clamp01(next);
            if (!Mathf.Approximately(next, volume)) { volume = next; AudioListener.volume = volume; PlayerPrefs.SetFloat("COLA_COLA.MasterVolume", volume); }
            Fill(new Rect(track.x - scale, track.y - scale, track.width + 2 * scale, track.height + 2 * scale), new Color(.30f, .34f, .27f));
            Fill(track, new Color(.055f, .075f, .06f));
            Fill(new Rect(track.x, track.y, track.width * volume, track.height), new Color(.32f, .47f, .37f));
            for (int i = 0; i <= 10; i++)
                Fill(new Rect(track.x + track.width * i / 10f, track.yMax + 4 * scale, scale, (i % 5 == 0 ? 5 : 3) * scale), new Color(.38f, .42f, .34f, .7f));
            float knob = track.x + track.width * volume;
            Fill(new Rect(knob - 4 * scale, track.y - 6 * scale, 8 * scale, 19 * scale), new Color(.73f, .73f, .61f));
            Fill(new Rect(knob - scale, track.y - 2 * scale, 2 * scale, 11 * scale), new Color(.34f, .37f, .29f));
        }

        private void DrawDropdownButton(Rect rect, Dropdown kind, string label, float scale)
        {
            Fill(rect, new Color(.15f, .19f, .15f, .88f));
            if (GUI.Button(rect, GUIContent.none, choice))
            { dropdown = dropdown == kind ? Dropdown.None : kind; dropdownScroll = Vector2.zero; }
            if (dropdown == kind) dropdownButtonRect = rect;
            GUI.Label(new Rect(rect.x + 12 * scale, rect.y, rect.width - 40 * scale, rect.height), label, choice);
            GUI.Label(new Rect(rect.xMax - 25 * scale, rect.y, 18 * scale, rect.height), dropdown == kind ? "▴" : "▾", small);
        }

        private void DrawDropdownPopup(float scale)
        {
            bool isLanguage = dropdown == Dropdown.Language;
            int count = isLanguage ? languages.Count : sizes.Count;
            float rowHeight = 35 * scale;
            float top = dropdownButtonRect.yMax + 5 * scale;
            float height = Mathf.Min(245 * scale, count * rowHeight + 8 * scale, Screen.height - top - 12 * scale);
            var popup = new Rect(dropdownButtonRect.x, top, dropdownButtonRect.width, Mathf.Max(scale, height));
            if (Event.current.type == EventType.MouseDown && !popup.Contains(Event.current.mousePosition) && !dropdownButtonRect.Contains(Event.current.mousePosition))
            { dropdown = Dropdown.None; Event.current.Use(); return; }
            Fill(popup, new Color(.055f, .075f, .06f, .99f));
            Fill(new Rect(popup.x, popup.y, popup.width, scale), new Color(.48f, .50f, .40f));
            var content = new Rect(0, 0, popup.width - 22 * scale, count * rowHeight + 6 * scale);
            dropdownScroll = GUI.BeginScrollView(popup, dropdownScroll, content);
            for (int i = 0; i < count; i++)
            {
                string code = isLanguage ? languages[i] : null;
                bool available = !isLanguage || ColaLocalization.HasTranslation(code);
                var row = new Rect(8 * scale, i * rowHeight + 3 * scale, content.width - 12 * scale, rowHeight - 2 * scale);
                if (isLanguage ? code == ColaLocalization.Language : i == resolution) Fill(row, new Color(.26f, .34f, .26f, .7f));
                bool previous = GUI.enabled; GUI.enabled = previous && available;
                string label = isLanguage ? ColaLocalization.NativeName(code) + (available ? "" : " · …") : sizes[i].x + " × " + sizes[i].y;
                if (GUI.Button(row, label, choice))
                {
                    if (isLanguage) ColaLocalization.SetLanguage(code);
                    else { resolution = i; ApplyDisplay(); }
                    dropdown = Dropdown.None;
                }
                GUI.enabled = previous;
            }
            GUI.EndScrollView();
        }

        private bool Button(Rect rect, string label)
        {
            bool hovered = rect.Contains(Event.current.mousePosition);
            if (hovered) { Fill(new Rect(rect.x - 15, rect.y + 10, 3, rect.height - 20), new Color(.65f, .29f, .20f)); Fill(rect, new Color(.68f, .64f, .49f, .045f)); }
            return GUI.Button(rect, label, choice);
        }

        private bool MainButton(Rect rect, string label, int index, float scale)
        {
            bool hovered = rect.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint)
                menuHover[index] = Mathf.MoveTowards(menuHover[index], hovered ? 1 : 0, Time.unscaledDeltaTime * 6);
            float amount = menuHover[index];
            Fill(new Rect(rect.x - 23 * scale, rect.y + 22 * scale, 5 * scale, 5 * scale), new Color(.65f, .28f, .20f, .25f + .75f * amount));
            Fill(new Rect(rect.x, rect.yMax - 4 * scale, rect.width * (.10f + .72f * amount), scale), new Color(.65f, .57f, .41f, .12f + .28f * amount));
            Color normal = choice.normal.textColor, hover = choice.hover.textColor, active = choice.active.textColor;
            choice.normal.textColor = choice.hover.textColor = choice.active.textColor = Color.Lerp(new Color(.79f, .76f, .66f), new Color(1, .92f, .75f), amount);
            bool clicked = GUI.Button(new Rect(rect.x + 7 * scale * amount, rect.y, rect.width, rect.height), label, choice);
            choice.normal.textColor = normal; choice.hover.textColor = hover; choice.active.textColor = active;
            return clicked;
        }

        private void DrawMenuShade(bool main)
        {
            if (!menuShade)
            {
                menuShade = new Texture2D(128, 64, TextureFormat.RGBA32, false) { name = "Menu / Soft Shade", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color[128 * 64];
                for (int y = 0; y < 64; y++) for (int x = 0; x < 128; x++)
                {
                    float u = x / 127f, v = y / 63f;
                    float left = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.12f, .72f, u));
                    float edge = Mathf.Pow(Mathf.Abs(v - .5f) * 2, 3) * .52f;
                    float alpha = Mathf.Clamp01(.12f + left * .79f + edge);
                    pixels[y * 128 + x] = new Color(.022f, .019f, .016f, alpha);
                }
                menuShade.SetPixels(pixels); menuShade.Apply(false, true);
            }
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), menuShade);
            if (!main) Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(.018f, .022f, .019f, .77f));
            float bar = Screen.height * .025f;
            Fill(new Rect(0, 0, Screen.width, bar), new Color(.012f, .012f, .010f));
            Fill(new Rect(0, Screen.height - bar, Screen.width, bar), new Color(.012f, .012f, .010f));
        }
        private void EnsureStyles()
        {
            Font font = ColaGameFont.LoadMenu(menuFontCandidate);
            if (title != null && styledFont == font) return;
            styledFont = font;
            title = Style(76, new Color(.83f, .81f, .69f), FontStyle.Bold);
            text = Style(22, new Color(.70f, .71f, .63f)); text.wordWrap = true;
            choice = Style(22, new Color(.75f, .76f, .66f)); choice.hover.textColor = new Color(.95f, .90f, .75f); choice.active.textColor = choice.hover.textColor;
            heading = Style(32, new Color(.82f, .81f, .71f));
            small = Style(13, new Color(.47f, .52f, .45f)); small.hover.textColor = new Color(.72f, .72f, .61f);
            valueStyle = Style(17, new Color(.70f, .72f, .62f)); valueStyle.alignment = TextAnchor.MiddleRight;
            foreach (var style in new[] { title, text, choice, heading, small, valueStyle }) style.font = font;
        }
        private static GUIStyle Style(int size, Color color, FontStyle weight = FontStyle.Normal)
        {
            var s = new GUIStyle { font = ColaGameFont.Load(), fontSize = size, fontStyle = weight, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(), margin = new RectOffset() };
            foreach (var state in new[] { s.normal, s.hover, s.active, s.focused }) state.textColor = color;
            return s;
        }
        private static void Fill(Rect rect, Color color) { var old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old; }
        private void ApplyDisplay()
        {
            var size = sizes[resolution];
            Screen.SetResolution(size.x, size.y, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            PlayerPrefs.SetInt("COLA_COLA.Width", size.x); PlayerPrefs.SetInt("COLA_COLA.Height", size.y); PlayerPrefs.SetInt("COLA_COLA.Fullscreen", fullscreen ? 1 : 0);
        }
        private void Save() { PlayerPrefs.SetFloat("COLA_COLA.MasterVolume", volume); PlayerPrefs.Save(); }
        private void Quit()
        {
            Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        private void OnDestroy()
        {
            if (menuShade) Destroy(menuShade);
            if (instance != this) return;
            Save(); instance = null; AudioListener.pause = false; ColaGameClock.SetPaused(false); Time.timeScale = previousTimeScale;
        }
    }
}
