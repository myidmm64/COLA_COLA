using ColaCola.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ColaCola.Presentation
{
    /// <summary>Single-player match rules; the physical devices remain presentation components.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class ColaDuelController : MonoBehaviour
    {
        public enum DuelState { Playing, Won, Lost, Completed }
        public const int FinalLevel = 5;
        public string BossName => ColaBossAppearance.NameFor(Level);
        public enum DebugCommand { SetTokens999, ToggleGuaranteedBurst, ToggleFreeItemMenu, ClearItems, Stage1, Stage2, Stage3, Stage4, Stage5, ToggleAchievementWindow, ToggleAbilityWindow, WinCurrentStage }
        public ShakeSelectionInteraction interaction;
        public StatusTerminalDisplay terminal;
        public GameObject itemRoot;
        public Light itemLight;
        public ColaItemController Items { get; private set; }
        public ColaRunProgression Progression { get; private set; }
        [Range(1, ColaItemBalance.StartingHealth)] public int playerMaxHealth = ColaItemBalance.StartingHealth;
        [Range(1, ColaItemBalance.StartingHealth)] public int bossBaseHealth = ColaItemBalance.StartingHealth;

        [Header("Debug (Editor / Development Build)")]
        [Tooltip("E로 전환합니다. 모든 아이템이 무료인 테스트 메뉴를 사용합니다.")]
        public bool debugItemTestMenu;
        [Tooltip("W로 전환합니다. 양쪽의 다음 흔들기 예고와 실제 폭발 확률을 100%로 고정합니다. 피해 보정은 유지됩니다.")]
        public bool debugGuaranteedBurst;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const bool DebugFeaturesEnabled = true;
#else
        private const bool DebugFeaturesEnabled = false;
#endif
        public bool DebugToolsAvailable => DebugFeaturesEnabled;
        public bool ItemTestMenuEnabled => DebugFeaturesEnabled && debugItemTestMenu;
        public bool GuaranteedBurst => DebugFeaturesEnabled && debugGuaranteedBurst;

        public DuelState State { get; private set; }
        public int Level { get; private set; }
        public int PlayerHealth { get; private set; }
        public int BossHealth { get; private set; }
        public int BossMaxHealth { get; private set; }
        public static int BossHealthForStage(int level) => level <= 1 ? 3 : level == 2 ? 4 : 5;
        public int PlayerTokens { get; private set; }
        public int BossTokens { get; private set; }
        public string LastActionMessage { get; private set; } = "";
        public bool IsPlayerTurn => State == DuelState.Playing && interaction && !interaction.IsBossTurn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ResetOnPlayEntry()
        {
            // Also runs when domain and scene reload are disabled in the Editor.
            foreach (var match in FindObjectsByType<ColaDuelController>(FindObjectsSortMode.None))
                if (match.isActiveAndEnabled) { match.RestartRun(); ColaFrontEnd.EnsureFor(match).ShowMainMenu(); }
        }

        private void OnEnable()
        {
            if (Application.IsPlaying(gameObject)) RestartRun();
        }

        private void Subscribe()
        {
            interaction.ShakeResolved -= OnShakeResolved;
            interaction.ShakeResolved += OnShakeResolved;
        }

        public void RestartRun(int? seed = null, bool fadeIn = false)
        {
            if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled || !interaction || !terminal) return;
            Subscribe();
            Progression ??= new ColaRunProgression(this);
            Progression.ResetRun(seed);
            debugGuaranteedBurst = debugItemTestMenu = false;
            Level = 1;
            PlayerTokens = BossTokens = 0;
            Items = ColaItemController.EnsureFor(this);
            Items.ResetRun(seed);
            GetComponent<ColaBossItemAI>().ResetDecisions(seed);
            BeginDuel(seed);
            if (fadeIn) interaction.RevealNewRound();
        }

        public bool ContinueToNextBoss()
        {
            if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled || State != DuelState.Won || Level >= FinalLevel || !interaction || interaction.IsBusy || (Progression?.BlocksGameplay ?? false))
                return false;
            Level++;
            Items.ResetEffects();
            Items.ResetBossInventory();
            GetComponent<ColaBossItemAI>().ResetDecisions();
            BeginDuel();
            interaction.RevealNewRound();
            return true;
        }

        private void BeginDuel(int? seed = null)
        {
            TabletopReadability.Apply(transform.root);
            ColaCameraBreathing.EnsureFor(transform.root);
            if (interaction.shakeAnimator && interaction.shakeAnimator.playerAnimator)
                ColaFpsSleeveExtension.EnsureFor(interaction.shakeAnimator.playerAnimator);
            ColaDuelMusic.EnsureFor(this);
            ColaPizzeriaAmbience.EnsureFor(this);
            ColaPizzeriaStaff.EnsureFor(this);
            ColaBossAppearance.EnsureFor(this);
            playerMaxHealth = Mathf.Clamp(playerMaxHealth, 1, ColaItemBalance.StartingHealth);
            bossBaseHealth = Mathf.Clamp(bossBaseHealth, 1, ColaItemBalance.StartingHealth);
            PlayerHealth = playerMaxHealth;
            BossMaxHealth = BossHealthForStage(Level);
            BossHealth = BossMaxHealth;
            BossTokens = ColaBossTactics.StartingTokensForStage(Level);
            State = DuelState.Playing;
            LastActionMessage = "Choose a strength, then shake the cola.";
            if (itemRoot) itemRoot.SetActive(false);
            if (itemLight) itemLight.enabled = false;
            if (interaction.shakeAnimator)
            {
                interaction.shakeAnimator.StopPlayerPlayPreview();
                interaction.shakeAnimator.previewPlayerOnPlay = false;
            }
            interaction.InputEnabled = true;
            interaction.automaticBossTurns = true;
            Progression.BeginBoss();
            interaction.ResetSession(seed);
            Items.RefreshCapacity();
            RefreshTerminal(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnShakeResolved(ShakeResult result)
        {
            if (!isActiveAndEnabled || State != DuelState.Playing) return;
            bool boss = interaction.LastActionWasBoss;
            var modifiers = interaction.LastItemModifiers;
            int reward = Progression.Reward(result.Prediction, result.Burst, modifiers, boss);
            int damage = result.Burst ? modifiers.Damage : 0;
            if (boss)
            {
                BossTokens = Mathf.Min(9999, BossTokens + reward);
                if (result.Burst) BossHealth = Mathf.Max(0, BossHealth - damage);
            }
            else
            {
                PlayerTokens = Mathf.Min(9999, PlayerTokens + reward);
                if (result.Burst) PlayerHealth = Mathf.Max(0, PlayerHealth - damage);
            }
            LastActionMessage = (boss ? "BOSS" : "YOU") + (result.Burst ? "  BURST: -" + damage + " HP" : "  SAFE") + "  /  +" + reward + " TOKENS";
            if (PlayerHealth == 0 || BossHealth == 0)
            {
                State = PlayerHealth == 0 ? DuelState.Lost : Level >= FinalLevel ? DuelState.Completed : DuelState.Won;
                if (State == DuelState.Completed) LastActionMessage = "도전 완료 — 보스 5명 격파!";
                interaction.InputEnabled = false;
                // This event runs before the interaction schedules the next participant.
                interaction.automaticBossTurns = false;
                ColaTabletopAudio.Cue(interaction, State == DuelState.Lost ? "Lose" : "Win", interaction.bottle.position, .65f);
            }
            else ColaTabletopAudio.Cue(interaction, "Tokens", terminal.transform.position, .42f);
            Progression.OnShake(result, boss, modifiers);
            RefreshTerminal(true);
        }

        private void RefreshTerminal(bool animate)
        {
            terminal.SetPlayerState(PlayerHealth, playerMaxHealth, PlayerTokens, animate);
            terminal.SetBossState(BossHealth, BossMaxHealth, BossTokens, animate);
            terminal.SetActiveParticipant(IsPlayerTurn);
        }

        public void SetActionMessage(string message) => LastActionMessage = message;

        public bool TrySpendPlayerTokens(int amount) => TrySpendTokens(amount, false);
        public bool CanAffordTokens(int amount, bool boss = false)
            => amount >= 0 && (boss ? BossTokens : PlayerTokens) >= amount;

        public bool TrySpendTokens(int amount, bool boss)
        {
            if (!Items || !Items.CanActFor(boss) || !CanAffordTokens(amount, boss)) return false;
            if (boss) BossTokens -= amount;
            else PlayerTokens -= amount;
            RefreshTerminal(true);
            return true;
        }

        internal bool TrySpendWaiterTokens(bool boss)
        {
            if (!Items || !Items.CanOrderWaiter(boss)) return false;
            if (boss) BossTokens -= Items.WaiterCostFor(true);
            else PlayerTokens -= Items.WaiterCostFor(false);
            RefreshTerminal(true);
            return true;
        }

        public bool TryHealPlayer() => TryHeal(false);

        internal void GrantPassiveTokens(int amount, bool boss = false)
        {
            if (boss) BossTokens = Mathf.Min(9999, BossTokens + Mathf.Max(0, amount));
            else PlayerTokens = Mathf.Min(9999, PlayerTokens + Mathf.Max(0, amount));
            RefreshTerminal(true);
        }

        public bool TryRemoveOpponentTokens(int maximum, bool boss)
        {
            if (maximum <= 0 || !Items || !Items.CanActFor(boss) || (boss ? PlayerTokens : BossTokens) == 0) return false;
            if (boss) PlayerTokens = Mathf.Max(0, PlayerTokens - maximum);
            else BossTokens = Mathf.Max(0, BossTokens - maximum);
            RefreshTerminal(true);
            return true;
        }

        public bool CanTransferTokens(int maximum, bool boss)
            => maximum > 0 && (boss ? PlayerTokens : BossTokens) > 0 && (boss ? BossTokens : PlayerTokens) < 9999;

        public bool TryTransferTokens(int maximum, bool boss)
        {
            if (!Items || !Items.CanActFor(boss) || !CanTransferTokens(maximum, boss)) return false;
            int amount = Mathf.Min(maximum, boss ? PlayerTokens : BossTokens, 9999 - (boss ? BossTokens : PlayerTokens));
            if (boss) { PlayerTokens -= amount; BossTokens += amount; }
            else { BossTokens -= amount; PlayerTokens += amount; }
            RefreshTerminal(true);
            return true;
        }

        public bool TryGrantItemTokens(int amount, bool boss)
        {
            if (!Items || !Items.CanActFor(boss) || amount <= 0 || (boss ? BossTokens : PlayerTokens) >= 9999) return false;
            if (boss) BossTokens = Mathf.Min(9999, BossTokens + amount);
            else PlayerTokens = Mathf.Min(9999, PlayerTokens + amount);
            RefreshTerminal(true);
            return true;
        }

        public bool TryHeal(bool boss)
        {
            if (!Items || !Items.CanActFor(boss) || (boss ? BossHealth >= BossMaxHealth : PlayerHealth >= playerMaxHealth)) return false;
            if (boss) BossHealth++;
            else PlayerHealth++;
            RefreshTerminal(true);
            return true;
        }

        // Keyboard and Play-mode verification share this path; no synthetic OS input is required.
        public bool ApplyDebugCommand(DebugCommand command)
        {
            if (!DebugFeaturesEnabled || !Application.IsPlaying(gameObject) || !isActiveAndEnabled ||
                !interaction || !terminal) return false;
            if ((int)command >= (int)DebugCommand.Stage1 && (int)command <= (int)DebugCommand.Stage5)
                return SetDebugStage((int)command - (int)DebugCommand.Stage1 + 1);
            if (command == DebugCommand.WinCurrentStage) return SetDebugVictory();
            if (command == DebugCommand.ToggleAchievementWindow || command == DebugCommand.ToggleAbilityWindow)
            {
                if (!Items) return false;
                var recordSheet = Items.GetComponent<ColaOrderSheet>();
                if (!recordSheet || !recordSheet.ToggleDebugProgression(command == DebugCommand.ToggleAchievementWindow)) return false;
                Progression.MarkDebugAssisted(); return true;
            }
            if (command == DebugCommand.ClearItems)
            {
                if (!Items) return false;
                Progression.MarkDebugAssisted();
                Items.ClearAllItems();
                return true;
            }
            if (State != DuelState.Playing) return false;
            if (Progression != null && Progression.BlocksGameplay) return false;
            switch (command)
            {
                case DebugCommand.SetTokens999:
                    Progression.MarkDebugAssisted();
                    PlayerTokens = 999;
                    RefreshTerminal(false);
                    if (Items) Items.GetComponent<ColaOrderSheet>()?.RefreshAvailability();
                    return true;
                case DebugCommand.ToggleGuaranteedBurst:
                    Progression.MarkDebugAssisted();
                    debugGuaranteedBurst = !debugGuaranteedBurst;
                    if (interaction.Model != null) interaction.Model.ForceBurst = GuaranteedBurst;
                    if (!interaction.IsBusy) interaction.RefreshPrediction();
                    return true;
                case DebugCommand.ToggleFreeItemMenu:
                    if (!Items) return false;
                    var sheet = Items.GetComponent<ColaOrderSheet>();
                    if (!sheet) return false;
                    Progression.MarkDebugAssisted();
                    if (sheet.IsOpen || sheet.BlocksGameplay)
                    {
                        debugItemTestMenu = false;
                        sheet.Close();
                        sheet.RefreshAvailability();
                        return true;
                    }
                    debugItemTestMenu = true;
                    if (sheet.Toggle()) return true;
                    debugItemTestMenu = false;
                    return false;
                default: return false;
            }
        }

        private bool SetDebugVictory()
        {
            if (State != DuelState.Playing || !Items || Progression == null) return false;
            Progression.MarkDebugAssisted();
            Progression.CancelSelection();
            Progression.JournalOpen = false;
            interaction.CancelPendingActions();
            ColaInteractionCamera.ResetFor(interaction);
            Items.GetComponent<ColaOrderSheet>()?.Close();
            BossHealth = 0;
            State = Level >= FinalLevel ? DuelState.Completed : DuelState.Won;
            LastActionMessage = "테스트 승리";
            interaction.InputEnabled = interaction.automaticBossTurns = false;
            RefreshTerminal(true);
            ColaTabletopAudio.Cue(interaction, "Win", interaction.bottle.position, .65f);
            return true;
        }

        private bool SetDebugStage(int level)
        {
            if (!Items || Progression == null) return false;
            Progression.MarkDebugAssisted();
            Progression.CancelSelection();
            Progression.JournalOpen = false;
            interaction.CancelPendingActions();
            ColaInteractionCamera.ResetFor(interaction);
            Items.GetComponent<ColaOrderSheet>()?.Close();
            Level = Mathf.Clamp(level, 1, FinalLevel);
            Items.ResetRun();
            GetComponent<ColaBossItemAI>().ResetDecisions();
            BeginDuel();
            return true;
        }

        private void Update()
        {
            if (!interaction || !terminal || ColaFrontEnd.BlocksGameplay) return;
            Progression?.FlushPendingGrants();
            if (GuaranteedBurst || ItemTestMenuEnabled) Progression?.MarkDebugAssisted();
            if (State == DuelState.Playing && terminal.IsPlayerTurn != IsPlayerTurn)
                terminal.SetActiveParticipant(IsPlayerTurn);
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.qKey.wasPressedThisFrame) ApplyDebugCommand(DebugCommand.SetTokens999);
            if (keyboard.wKey.wasPressedThisFrame) ApplyDebugCommand(DebugCommand.ToggleGuaranteedBurst);
            if (keyboard.eKey.wasPressedThisFrame) ApplyDebugCommand(DebugCommand.ToggleFreeItemMenu);
            if (keyboard.rKey.wasPressedThisFrame) ApplyDebugCommand(DebugCommand.ClearItems);
            if (keyboard.tKey.wasPressedThisFrame) ApplyDebugCommand(DebugCommand.ToggleAchievementWindow);
            if (keyboard.yKey.wasPressedThisFrame) ApplyDebugCommand(DebugCommand.ToggleAbilityWindow);
            if (keyboard.vKey.wasPressedThisFrame) ApplyDebugCommand(DebugCommand.WinCurrentStage);
            if (keyboard.tabKey.wasPressedThisFrame && Items) Items.GetComponent<ColaOrderSheet>()?.ToggleRecords();
            if (State != DuelState.Playing && !interaction.IsBusy && !Progression.BlocksGameplay && keyboard.enterKey.wasPressedThisFrame)
            {
                if (State == DuelState.Won) ContinueToNextBoss();
                else RestartRun(fadeIn: true);
            }
        }

        private void OnDisable()
        {
            if (!interaction) return;
            interaction.ShakeResolved -= OnShakeResolved;
            if (!Application.IsPlaying(gameObject)) return;
            interaction.InputEnabled = false;
            interaction.CancelPendingActions();
        }
    }
}
