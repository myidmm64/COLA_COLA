using System;
using System.Collections;
using System.IO;
using System.Text;
using ColaCola.Core;
using ColaCola.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColaCola.Editor
{
    [InitializeOnLoad]
    internal static class ColaMenuPlayReview
    {
        private const string Key = "COLA_COLA.MenuPlayReview";
        internal const string Folder = ProjectVerificationBridge.Root + "/menu-design";
        static ColaMenuPlayReview() { if (SessionState.GetBool(Key, false)) Hook(); }
        internal static void Begin()
        {
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Started", false);
            SessionState.SetString(Key + ".Language", PlayerPrefs.GetString("COLA_COLA.Language", "ko"));
            SessionState.SetBool(Key + ".HadLanguage", PlayerPrefs.HasKey("COLA_COLA.Language"));
            var previous = EditorWindow.focusedWindow;
            SessionState.SetString(Key + ".Focus", previous ? previous.GetEntityId().ToString() : "");
            Hook();
            EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            var window = EditorWindow.GetWindow(type);
            SessionState.SetBool(Key + ".Maximized", window.maximized);
            window.maximized = true; window.Show(); window.Focus();
            EditorApplication.isPlaying = true;
        }
        private static void Hook()
        {
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Changed; EditorApplication.playModeStateChanged += Changed;
        }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || Time.frameCount < 5 || SessionState.GetBool(Key + ".Started", false)) return;
            SessionState.SetBool(Key + ".Started", true);
            new GameObject("MenuGameViewReview").AddComponent<ColaMenuReviewProbe>();
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            if (SessionState.GetBool(Key + ".HadLanguage", false)) PlayerPrefs.SetString("COLA_COLA.Language", SessionState.GetString(Key + ".Language", "ko"));
            else PlayerPrefs.DeleteKey("COLA_COLA.Language");
            PlayerPrefs.Save(); ColaLocalization.Reload();
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Changed;
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(type).maximized = SessionState.GetBool(Key + ".Maximized", false);
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if (window.GetEntityId().ToString() == SessionState.GetString(Key + ".Focus", "")) window.Focus();
        }
    }

    public sealed class ColaMenuReviewProbe : MonoBehaviour
    {
        private IEnumerator Start()
        {
            var match = FindAnyObjectByType<ColaDuelController>();
            if (!match || !match.interaction) { Debug.LogError("Menu review: live duel missing."); yield break; }
            var front = ColaFrontEnd.EnsureFor(match);
            string originalLanguage = ColaLocalization.Language;
            int originalFont = front.MenuFontCandidate;
            Directory.CreateDirectory(ColaMenuPlayReview.Folder);
            var report = new StringBuilder("Actual Unity Play / original Game view. No preview camera, Camera.Render or OS input.\n");
            report.AppendLine("Menu-only review: Q/W/E unnecessary; no gameplay, token, RNG or item state modified.");
            try
            {
                ColaLocalization.SetLanguage("ko"); front.ShowMainMenu();
                for (int i = 0; i < 3; i++)
                {
                    front.MenuFontCandidate = i;
                    yield return new WaitForSecondsRealtime(i == 0 ? 3 : .6f);
                    yield return new WaitForEndOfFrame();
                    string name = i == 0 ? "01-NotoSerifKR.png" : i == 1 ? "02-NotoSansKR.png" : "03-GowunBatang.png";
                    ScreenCapture.CaptureScreenshot(ColaMenuPlayReview.Folder + "/" + name);
                    yield return new WaitForSecondsRealtime(.5f);
                    report.AppendLine($"{name}: {Screen.width}x{Screen.height}; font={ColaGameFont.LoadMenu(i).name}; live menu camera={match.interaction.playerCamera.transform.position}.");
                }
                var data = JsonUtility.FromJson<ColaLocalization.Table>(Resources.Load<TextAsset>("Localization/Strings").text);
                foreach (string language in ColaLocalization.Languages)
                {
                    ColaLocalization.SetLanguage(language);
                    foreach (var row in data.rows)
                    {
                        int index = Array.IndexOf(row.languages, language);
                        if (index < 0 || index >= row.texts.Length || string.IsNullOrWhiteSpace(row.texts[index]))
                            throw new InvalidOperationException("Missing translation: " + language + "/" + row.key);
                    }
                    var font = ColaGameFont.Load();
                    string sample = ColaLocalization.Get("ui.start", "") + ColaLocalization.Get("ui.settings", "") + ColaLocalization.Get("item.SafetyCap.effect", "");
                    foreach (char letter in sample)
                        if (!char.IsWhiteSpace(letter) && !font.HasCharacter(letter)) throw new InvalidOperationException("Missing font glyph: " + language + "/" + letter);
                    report.AppendLine(language + ": all " + data.rows.Count + " translation rows nonempty; sample glyphs present in " + font.name + ".");
                }
            }
            finally { front.MenuFontCandidate = originalFont; ColaLocalization.SetLanguage(originalLanguage); }
            File.WriteAllText(ColaMenuPlayReview.Folder + "/Report.md", report + "\nScreenshots require separate visual inspection. No full gameplay validation claimed.\n");
            Debug.Log("COLA_MENU_DESIGN_PASS");
        }
    }
}
