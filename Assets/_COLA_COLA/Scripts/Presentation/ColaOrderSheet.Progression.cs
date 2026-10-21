using System.Collections.Generic;
using ColaCola.Core;
using UnityEngine;

namespace ColaCola.Presentation
{
    public sealed partial class ColaOrderSheet
    {
        public enum SheetPage { Items, AbilityChoice, Abilities, Achievements, DebugAchievements, DebugAbilities, BossAbilities }
        public SheetPage Page { get; private set; }
        public bool IsProgressionPage => Page != SheetPage.Items;
        private ColaRunProgression Progression => items && items.match ? items.match.Progression : null;
        private bool ProgressionPageAvailable => isActiveAndEnabled && Progression != null &&
            (Page != SheetPage.AbilityChoice || Progression.ChoiceReady);
        private readonly List<int> progressionEntries = new();
        private readonly List<GameObject> recordTabs = new();
        private string recordName, recordDescription;
        private int inspectedEntry = -1;
        private bool AchievementPage => Page == SheetPage.Achievements || Page == SheetPage.DebugAchievements;
        private bool DebugPage => Page == SheetPage.DebugAbilities || Page == SheetPage.DebugAchievements;
        private bool IsRecordPage => Page == SheetPage.Abilities || Page == SheetPage.Achievements || Page == SheetPage.BossAbilities;
        private bool recordEntering;
        private float recordEnterTime;
        private const float RecordEnterSeconds = .38f;

        private bool AdvanceRecordEntrance(Camera camera)
        {
            if (!recordEntering || !IsOpen) return false;
            recordEnterTime += ColaGameClock.Delta;
            float t = Mathf.Clamp01(recordEnterTime / RecordEnterSeconds);
            float eased = t * t * (3 - 2 * t);
            float belowScreen = HeldLocalPosition.z * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f) + .23f;
            Vector3 local = HeldLocalPosition + Vector3.down * (belowScreen * (1 - eased));
            modelRoot.transform.SetPositionAndRotation(camera.transform.TransformPoint(local), camera.transform.rotation);
            IsSettled = t >= 1;
            if (IsSettled) recordEntering = false;
            return true;
        }

        public bool ToggleRecords()
        {
            if (IsOpen && (Page == SheetPage.Abilities || Page == SheetPage.Achievements || Page == SheetPage.BossAbilities)) { Close(); return true; }
            return ShowProgression(SheetPage.Abilities);
        }

        public bool ToggleDebugProgression(bool achievements)
        {
            var page = achievements ? SheetPage.DebugAchievements : SheetPage.DebugAbilities;
            if (IsOpen && Page == page) { Close(); return true; }
            return ShowProgression(page);
        }

        public bool ShowProgression(SheetPage page)
        {
            if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled || Progression == null || page == SheetPage.Items) return false;
            if (page == SheetPage.AbilityChoice)
            { if (!Progression.ChoiceReady) return false; }
            else if (Progression.HasPendingChoice || items.match.interaction.IsBusy) return false;
            if ((page == SheetPage.DebugAchievements || page == SheetPage.DebugAbilities) && !items.match.DebugToolsAvailable) return false;
            bool switchingRecordTab = IsOpen && IsRecordPage && (page == SheetPage.Abilities || page == SheetPage.Achievements || page == SheetPage.BossAbilities);
            Close(); Page = page; scrollPosition = 0; inspectedEntry = -1;
            recordName = recordDescription = "";
            Progression.JournalOpen = page != SheetPage.AbilityChoice;
            IsOpen = true;
            recordEntering = IsRecordPage && !switchingRecordTab;
            recordEnterTime = 0;
            IsSettled = !recordEntering;
            var camera = items.match.interaction.playerCamera;
            float belowScreen = HeldLocalPosition.z * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f) + .23f;
            Vector3 local = HeldLocalPosition + (recordEntering ? Vector3.down * belowScreen : Vector3.zero);
            modelRoot.transform.SetPositionAndRotation(camera.transform.TransformPoint(local), camera.transform.rotation);
            RefreshAvailability();
            ColaTabletopAudio.Cue(items.match.interaction, "MenuOpen", modelRoot.transform.position, .45f);
            return true;
        }

        private void SyncProgressionPage()
        {
            if (Progression == null) return;
            if (Progression.ChoiceReady && (!IsOpen || Page != SheetPage.AbilityChoice)) ShowProgression(SheetPage.AbilityChoice);
            else if (IsOpen && Page == SheetPage.AbilityChoice && !Progression.ChoiceReady) Close();
        }

        private void BuildRecordTabs()
        {
            if (recordTabs.Count > 0) return;
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * .09f;
                var tab = Key("RecordTab_" + i, new Vector3(x, .119f, -.013f), new Vector2(.083f, .022f), available);
                tab.AddComponent<ColaOrderTarget>().Bind(i == 0 ? ColaOrderTarget.Action.RecordAbilities : i == 1 ? ColaOrderTarget.Action.RecordBossAbilities : ColaOrderTarget.Action.RecordAchievements);
                var text = Text("RecordTabText_" + i, i == 0 ? ColaLocalization.Get("ui.my_abilities", "내 능력") : i == 1 ? ColaLocalization.Get("ui.boss_abilities", "상대 능력") : ColaLocalization.Get("ui.achievements", "업적"), new Vector3(x, .119f, -.016f), 48, .0023f,
                    new Color(.91f, .86f, .71f), TextAnchor.MiddleCenter);
                recordTabs.Add(tab); recordTabs.Add(text.gameObject);
            }
            SetRecordTabsVisible(false);
        }

        private void SetRecordTabsVisible(bool visible)
        { foreach (var tab in recordTabs) if (tab) tab.SetActive(visible); }

        private void CollectEntries()
        {
            progressionEntries.Clear();
            if (Progression == null) return;
            if (Page == SheetPage.AbilityChoice)
            { foreach (var id in Progression.Choices) progressionEntries.Add((int)id); }
            else if (AchievementPage)
            { foreach (var entry in ColaProgressionCatalog.Achievements) progressionEntries.Add((int)entry.Id); }
            else
                foreach (var entry in ColaProgressionCatalog.Abilities)
                    if (Page == SheetPage.DebugAbilities || Progression.Has(entry.Id, Page == SheetPage.BossAbilities)) progressionEntries.Add((int)entry.Id);
        }

        private void Entry(int id, out string name, out string description, out bool owned)
        {
            if (AchievementPage)
            {
                var entry = ColaProgressionCatalog.Achievements[id];
                name = entry.Name; description = entry.Description; owned = Progression.IsUnlocked(entry.Id);
            }
            else
            {
                var entry = ColaProgressionCatalog.Abilities[id];
                name = entry.Name; description = entry.Description; owned = Progression.Has(entry.Id, Page == SheetPage.BossAbilities);
            }
        }

        private void RefreshProgression()
        {
            CollectEntries();
            foreach (var renderer in detailedPrint) if (renderer) renderer.enabled = IsOpen;
            SetRecordTabsVisible(IsOpen && (Page == SheetPage.Abilities || Page == SheetPage.Achievements || Page == SheetPage.BossAbilities));
            titleText.text = Page switch
            {
                SheetPage.AbilityChoice => ColaLocalization.Get("ui.ability_choice", "오늘의 서비스"),
                SheetPage.DebugAbilities => ColaLocalization.Get("ui.ability_debug", "능력 테스트"),
                SheetPage.DebugAchievements => ColaLocalization.Get("ui.achievement_debug", "업적 테스트"),
                _ => ""
            };
            titleText.characterSize = .003f; titleText.anchor = TextAnchor.MiddleLeft;
            titleText.transform.localPosition = new Vector3(-.128f, .141f, -.009f);
            bool tabs = Page == SheetPage.Abilities || Page == SheetPage.Achievements || Page == SheetPage.BossAbilities;
            bool showMetadata = IsOpen && !tabs && Page != SheetPage.AbilityChoice;
            balanceText.GetComponent<Renderer>().enabled = inventoryText.GetComponent<Renderer>().enabled = showMetadata;
            balanceText.text = Page == SheetPage.AbilityChoice ? "" : "DEBUG";
            inventoryText.text = Page == SheetPage.AbilityChoice ? "" : AchievementPage ? Progression.UnlockedCount + " / 26" : Progression.AbilityCount + " / " + ColaProgressionCatalog.Abilities.Count;
            closeTarget.enabled = IsOpen && Page != SheetPage.AbilityChoice;
            closeTarget.GetComponent<Renderer>().enabled = IsOpen && Page != SheetPage.AbilityChoice;
            var closeMark = modelRoot.transform.Find("CloseMark");
            if (closeMark) closeMark.GetComponent<Renderer>().enabled = IsOpen && Page != SheetPage.AbilityChoice;
            bool footer = DebugPage;
            checkoutTarget.enabled = IsOpen && IsSettled && footer;
            checkoutRenderer.enabled = checkoutText.GetComponent<Renderer>().enabled = IsOpen && footer;
            checkoutRenderer.sharedMaterial = checkoutReady;
            checkoutText.text = Page == SheetPage.DebugAchievements ? ColaLocalization.Get("ui.unlock_all", "모두 달성") : ColaLocalization.Get("ui.grant_all", "모두 획득");
            if (progressionEntries.Count == 0 && Page == SheetPage.BossAbilities)
            { recordName = ColaLocalization.Get("ui.no_boss_abilities", "상대 능력 없음"); recordDescription = ""; }
            if (progressionEntries.Count == 0 && Page == SheetPage.Abilities)
            { recordName = ColaLocalization.Get("ui.no_abilities", "보유 능력 없음"); recordDescription = ColaLocalization.Get("ui.ability_hint", "양쪽 폭발을 합쳐 3회마다 능력을 선택합니다."); }
            SetBoardDescription(recordName, recordDescription);
            for (int i = 0; i < itemRows.Length; i++)
            {
                int index = FirstVisible + i;
                bool offered = i < ColaItemController.MenuSize && index < progressionEntries.Count;
                itemRows[i].SetActive(offered);
                nameTargets[i].enabled = offered && IsOpen && IsSettled;
                purchaseTargets[i].enabled = false;
                removeTargets[i].enabled = false;
                if (!offered) continue;
                int id = progressionEntries[index];
                Entry(id, out string name, out _, out bool owned);
                bool showAction = Page == SheetPage.AbilityChoice || AchievementPage || Page == SheetPage.DebugAbilities && !owned;
                purchaseTargets[i].enabled = showAction && IsOpen && IsSettled;
                float y = Page == SheetPage.AbilityChoice ? .065f - i * .064f : .093f - i * .029f;
                itemRows[i].transform.localPosition = new Vector3(0, y, 0);
                numberTexts[i].text = (index + 1).ToString();
                itemNameTexts[i].text = name; itemNameTexts[i].characterSize = .0025f;
                rowRenderers[i].enabled = IsOpen && (id == inspectedEntry || owned);
                priceTexts[i].GetComponent<Renderer>().enabled = purchaseRenderers[i].enabled = IsOpen && showAction;
                purchaseRenderers[i].sharedMaterial = owned ? selectedKey : available;
                priceTexts[i].characterSize = .0026f;
                priceTexts[i].text = Page == SheetPage.AbilityChoice ? ColaLocalization.Get("ui.select", "선택") : owned ? (AchievementPage ? ColaLocalization.Get("ui.earned", "달성") : "") : DebugPage ? ColaLocalization.Get("ui.add", "추가") : "—";
                removeRenderers[i].enabled = false;
                if (removeMarks[i]) removeMarks[i].GetComponent<Renderer>().enabled = false;
            }
        }

        private int TargetEntry(ColaOrderTarget target)
        {
            var collider = target.GetComponent<Collider>();
            for (int row = 0; row < ColaItemController.MenuSize; row++)
                if (purchaseTargets[row] == collider || nameTargets[row] == collider)
                { int index = FirstVisible + row; return index < progressionEntries.Count ? progressionEntries[index] : -1; }
            return -1;
        }

        private bool HandleProgressionTarget(ColaOrderTarget target)
        {
            if (!IsOpen || !CanOpen || !target || !target.gameObject.activeInHierarchy || !target.transform.IsChildOf(modelRoot.transform)) return false;
            if (target.Kind == ColaOrderTarget.Action.Close)
            { if (Page != SheetPage.AbilityChoice) Close(); return true; }
            if (!IsSettled) return false;
            if (target.Kind == ColaOrderTarget.Action.RecordAbilities || target.Kind == ColaOrderTarget.Action.RecordAchievements || target.Kind == ColaOrderTarget.Action.RecordBossAbilities)
                return ShowProgression(target.Kind == ColaOrderTarget.Action.RecordAbilities ? SheetPage.Abilities : target.Kind == ColaOrderTarget.Action.RecordBossAbilities ? SheetPage.BossAbilities : SheetPage.Achievements);
            if (target.Kind == ColaOrderTarget.Action.Checkout && DebugPage)
            {
                if (AchievementPage) Progression.DebugSetAllAchievements(true);
                else foreach (var ability in ColaProgressionCatalog.Abilities) Progression.DebugGrantAbility(ability.Id);
                Refresh(); return true;
            }
            int id = TargetEntry(target);
            return id >= 0 && SelectProgressionEntry(id);
        }

        // Shared with Play probes: selection does not require synthetic OS input.
        public bool SelectProgressionEntry(int id)
        {
            if (!IsOpen || !IsSettled || !IsProgressionPage || !progressionEntries.Contains(id)) return false;
            InspectProgressionEntry(id);
            if (Page == SheetPage.AbilityChoice)
            { if (!Progression.ChooseAbility((ColaAbilityId)id)) return false; Close(); }
            else if (Page == SheetPage.DebugAchievements) Progression.DebugSetAchievement((ColaAchievementId)id, !Progression.IsUnlocked((ColaAchievementId)id));
            else if (Page == SheetPage.DebugAbilities) Progression.DebugGrantAbility((ColaAbilityId)id);
            Refresh(); return true;
        }

        public void InspectProgressionEntry(int id)
        {
            if (!IsProgressionPage || !progressionEntries.Contains(id)) return;
            inspectedEntry = id; Entry(id, out recordName, out recordDescription, out _);
        }

        private bool HoverProgression(Vector2 screenPoint)
        {
            if (!IsOpen || !IsSettled || !IsVisible) return false;
            Physics.SyncTransforms();
            if (Physics.Raycast(items.match.interaction.playerCamera.ScreenPointToRay(screenPoint), out var hit, 8, ~0, QueryTriggerInteraction.Ignore) &&
                hit.collider.TryGetComponent<ColaOrderTarget>(out var target) && target.transform.IsChildOf(modelRoot.transform))
            { int id = TargetEntry(target); if (id >= 0) { InspectProgressionEntry(id); return true; } }
            return false;
        }

        private void DrawProgressionOverlay()
        {
            if (!Application.IsPlaying(gameObject) || !IsOpen || !IsSettled || !IsVisible || !ProgressionPageAvailable) return;
            int depth = GUI.depth; GUI.depth = -20;
            Color background = GUI.backgroundColor; GUI.backgroundColor = new Color(.45f, .43f, .30f);
            if (EntryCount > ColaItemController.MenuSize)
                scrollPosition = GUI.VerticalScrollbar(ControlRect(.136f, .106f, .148f, -.067f), scrollPosition, ColaItemController.MenuSize, 0, EntryCount);
            GUI.backgroundColor = background;
            GUI.depth = depth;
        }
    }
}
