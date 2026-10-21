using System.Collections.Generic;
using ColaCola.Core;
using UnityEngine;

namespace ColaCola.Presentation
{
    public sealed partial class ColaOrderSheet
    {
        private readonly List<TextMesh> printedLabels = new();
        private GUIStyle printInk, printMuted, printLight;
        private Font printFont;
        public bool PrintedTextFits { get; private set; } = true;

        private void LateUpdate()
        {
            // The held sheet is still the actual 3D clipboard. Print at its projected paper coordinates
            // so tiny TextMesh glyphs do not lose detail to the scene's low-resolution materials.
            if (!IsOpen) return;
            foreach (var lettering in printedLabels)
                if (lettering) lettering.GetComponent<Renderer>().enabled = false;
        }

        private void DrawReadablePrint()
        {
            if (!Application.IsPlaying(gameObject) || !IsOpen || !IsSettled || !IsVisible || !items) return;
            var nextFont = ColaGameFont.Load();
            if (printInk == null || printFont != nextFont)
            {
                printFont = nextFont;
                printInk = new GUIStyle { font = printFont, alignment = TextAnchor.MiddleLeft, wordWrap = true, padding = new RectOffset(), margin = new RectOffset() };
                printInk.normal.textColor = new Color(.10f, .15f, .11f);
                printMuted = new GUIStyle(printInk); printMuted.normal.textColor = new Color(.38f, .20f, .14f);
                printLight = new GUIStyle(printInk); printLight.normal.textColor = new Color(.96f, .91f, .77f);
            }
            int oldDepth = GUI.depth; var oldColor = GUI.color;
            GUI.depth = -19; GUI.color = Color.white; PrintedTextFits = true;
            DrawPrint(ControlRect(-.128f, .176f, -.015f, .157f), "FIZZERIA", printMuted, 12);
            DrawPrint(ControlRect(-.128f, .157f, .113f, .130f), titleText.text, printInk, 24);
            if (Page != SheetPage.AbilityChoice)
                DrawPrint(ControlRect(.120f, .177f, .142f, .155f), "×", printLight, 17, true);
            bool tabs = IsRecordPage;
            if (tabs)
            {
                for (int i = 0; i < 3; i++)
                {
                    float x = (i - 1) * .09f;
                    string key = i == 0 ? "ui.my_abilities" : i == 1 ? "ui.boss_abilities" : "ui.achievements";
                    DrawPrint(ControlRect(x - .038f, .129f, x + .038f, .108f), ColaLocalization.Get(key, ""), printLight, 14, true);
                }
            }
            else if (Page != SheetPage.AbilityChoice)
            {
                DrawPrint(ControlRect(-.122f, .129f, .026f, .109f), balanceText.text, printInk, 14);
                DrawPrint(ControlRect(.036f, .129f, .130f, .109f), inventoryText.text, printInk, 14);
            }
            int visible = IsProgressionPage ? Mathf.Min(ColaItemController.MenuSize, progressionEntries.Count - FirstVisible) : Mathf.Min(ColaItemController.MenuSize, ColaItemCatalog.All.Count - FirstVisible);
            for (int row = 0; row < visible; row++)
            {
                int index = FirstVisible + row;
                float y = Page == SheetPage.AbilityChoice ? .065f - row * .064f : .093f - row * .029f;
                string name;
                bool owned = false, action = false;
                if (IsProgressionPage)
                {
                    Entry(progressionEntries[index], out name, out _, out owned);
                    action = Page == SheetPage.AbilityChoice || AchievementPage || Page == SheetPage.DebugAbilities && !owned;
                }
                else name = ColaItemCatalog.All[index].Name;
                DrawPrint(ControlRect(-.130f, y + .011f, -.106f, y - .011f), (index + 1).ToString(), printLight, 14, true);
                DrawPrint(ControlRect(-.098f, y + .013f, IsProgressionPage ? .073f : .028f, y - .013f), name, printInk, 18);
                if (action)
                {
                    string caption = Page == SheetPage.AbilityChoice ? ColaLocalization.Get("ui.select", "선택") : owned ? ColaLocalization.Get("ui.earned", "달성") : DebugPage ? ColaLocalization.Get("ui.add", "추가") : "—";
                    DrawPrint(ControlRect(.079f, y + .012f, .132f, y - .012f), caption, printLight, 14, true);
                }
            }
            if (!string.IsNullOrEmpty(descriptionText.text))
            {
                DrawPrint(ControlRect(-.122f, -.080f, .130f, -.099f), descriptionTitle.text, printMuted, 15);
                bool checkout = Page == SheetPage.Items || DebugPage;
                DrawPrint(ControlRect(-.122f, -.102f, .130f, checkout ? -.134f : -.174f), descriptionText.text, printInk, 15);
            }
            if (Page == SheetPage.Items || DebugPage)
                DrawPrint(ControlRect(-.121f, -.142f, .121f, -.166f), checkoutText.text, printLight, 16, true);
            GUI.color = oldColor; GUI.depth = oldDepth;
        }

        private void DrawPrint(Rect rect, string value, GUIStyle style, int size, bool centered = false)
        {
            if (string.IsNullOrEmpty(value) || rect.width <= 0 || rect.height <= 0) return;
            style.alignment = centered ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            int minimum = Mathf.Max(10, Mathf.RoundToInt(10 * ColaUiLayout.Scale));
            style.fontSize = Mathf.RoundToInt(size * ColaUiLayout.Scale);
            var content = new GUIContent(value);
            while (style.fontSize > minimum && style.CalcHeight(content, rect.width) > rect.height - 2) style.fontSize--;
            if (style.CalcHeight(content, rect.width) > rect.height + 1) PrintedTextFits = false;
            GUI.Label(rect, content, style);
        }
    }
}
