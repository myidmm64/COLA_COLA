using ColaCola.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ColaCola.Presentation
{
    /// <summary>Single-player match rules; the physical devices remain presentation components.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class ColaDuelController : MonoBehaviour
    {
        public enum DuelState { Playing, Won, Lost }
        public ShakeSelectionInteraction interaction;
        public StatusTerminalDisplay terminal;
        public GameObject itemRoot;
        public Light itemLight;
        public ColaItemController Items { get; private set; }
        [Range(1, 99)] public int playerMaxHealth = 3;
        [Range(1, 99)] public int bossBaseHealth = 3;

        public DuelState State { get; private set; }
        public int Level { get; private set; }
        public int PlayerHealth { get; private set; }
        public int BossHealth { get; private set; }
        public int BossMaxHealth { get; private set; }
        public int PlayerTokens { get; private set; }
        public int BossTokens { get; private set; }
        public string LastActionMessage { get; private set; } = "";
        public bool IsPlayerTurn => State == DuelState.Playing && interaction && !interaction.IsBossTurn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ResetOnPlayEntry()
        {
            // Also runs when domain and scene reload are disabled in the Editor.
            foreach (var match in FindObjectsByType<ColaDuelController>(FindObjectsSortMode.None))
                if (match.isActiveAndEnabled) match.RestartRun();
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
            if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled || State != DuelState.Won || !interaction || interaction.IsBusy)
                return false;
            Level = Mathf.Min(9999, Level + 1);
            BossTokens = 0;
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
            ColaDuelMusic.EnsureFor(this);
            playerMaxHealth = Mathf.Clamp(playerMaxHealth, 1, 99);
            bossBaseHealth = Mathf.Clamp(bossBaseHealth, 1, 99);
            PlayerHealth = playerMaxHealth;
            BossMaxHealth = Mathf.Min(99, bossBaseHealth + Level - 1);
            BossHealth = BossMaxHealth;
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
            interaction.ResetSession(seed);
            RefreshTerminal(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnShakeResolved(ShakeResult result)
        {
            if (!isActiveAndEnabled || State != DuelState.Playing) return;
            bool boss = interaction.LastActionWasBoss;
            var modifiers = interaction.LastItemModifiers;
            int reward = result.Burst ? 5 : result.Prediction.Strength;
            if (!result.Burst && modifiers.SafeTip && result.Prediction.BurstProbability >= .5d) reward += 3;
            reward *= modifiers.RewardMultiplier;
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
                State = PlayerHealth == 0 ? DuelState.Lost : DuelState.Won;
                interaction.InputEnabled = false;
                // This event runs before the interaction schedules the next participant.
                interaction.automaticBossTurns = false;
            }
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
        public bool TrySpendTokens(int amount, bool boss)
        {
            if (amount < 0 || !Items || !Items.CanActFor(boss) || (boss ? BossTokens : PlayerTokens) < amount) return false;
            if (boss) BossTokens -= amount;
            else PlayerTokens -= amount;
            RefreshTerminal(true);
            return true;
        }

        public bool TryHealPlayer() => TryHeal(false);
        public bool TryHeal(bool boss)
        {
            if (!Items || !Items.CanActFor(boss) || (boss ? BossHealth >= BossMaxHealth : PlayerHealth >= playerMaxHealth)) return false;
            if (boss) BossHealth++;
            else PlayerHealth++;
            RefreshTerminal(true);
            return true;
        }

        private void Update()
        {
            if (!interaction || !terminal) return;
            if (State == DuelState.Playing && terminal.IsPlayerTurn != IsPlayerTurn)
                terminal.SetActiveParticipant(IsPlayerTurn);
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.rKey.wasPressedThisFrame) RestartRun(fadeIn: State != DuelState.Playing);
            else if (State != DuelState.Playing && !interaction.IsBusy && keyboard.enterKey.wasPressedThisFrame)
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
