using System;
using System.Collections;
using ColaCola.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace ColaCola.Presentation
{
    /// <summary>Physical strength selection followed by a separate click on the central cola.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class ShakeSelectionInteraction : MonoBehaviour
    {
        public PressureInteractionSettings settings;
        public Camera playerCamera;
        public PressureGaugeDisplay gauge;
        public Transform[] strengthButtons = new Transform[3];
        public Transform bottle, cap;
        public Collider bottleCollider;
        public ColaShakeAnimator shakeAnimator;
        public ColaBurstPresentation burstPresentation;
        public ColaItemController items;
        public bool automaticBossTurns = true;
        public bool InputEnabled { get; set; } = true;
        [Min(0)] public float bossThinkSeconds = .65f;
        private BottlePressureModel model;
        private System.Random random;
        private System.Random bossChoiceRandom;
        private int sessionVersion;
        private readonly Vector3[] buttonRest = new Vector3[3];
        private Vector3 bottleRestPosition, bottleRestScale, capRestPosition;
        private Quaternion bottleRestRotation;
        private bool captured, busy;
        private bool ForceBurst => items && items.match && items.match.GuaranteedBurst;
        public int CommittedActionCount { get; private set; }
        public int BottleNumber { get; private set; }
        public bool IsBusy => busy || (shakeAnimator && shakeAnimator.IsPlaying) || (items && items.IsUsingItem);
        public BottlePressureModel Model => model;
        public ShakeResult LastResult { get; private set; }
        public bool LastActionWasBoss { get; private set; }
        public bool IsBossTurn { get; private set; }
        public bool BossItemPhase { get; private set; }
        public bool BossPassedTurn { get; private set; }
        public int PlannedBossStrength { get; private set; }
        public ItemActionModifiers LastItemModifiers { get; private set; } = ItemActionModifiers.None;
        public event Action<ShakeResult> ShakeResolved;

        private void OnEnable()
        {
            if (!HasBindings) return;
            CaptureRestPose();
            if (Application.IsPlaying(gameObject))
            {
                burstPresentation = ColaBurstPresentation.EnsureFor(this);
                ResetSession();
            }
            else RefreshAuthoringView();
        }

        private bool HasBindings => gauge && bottle && bottleCollider && playerCamera && settings &&
            strengthButtons != null && strengthButtons.Length == 3 && Array.TrueForAll(strengthButtons, t => t);

        public void RefreshAuthoringView()
        {
            if (!HasBindings) return;
            if (!captured) CaptureRestPose();
            RestorePoses();
            gauge.Show(0, 20, null, null, true);
        }

        private void CaptureRestPose()
        {
            for (int i = 0; i < 3; i++) buttonRest[i] = strengthButtons[i].localPosition;
            bottleRestPosition = bottle.localPosition;
            bottleRestRotation = bottle.localRotation;
            bottleRestScale = bottle.localScale;
            if (cap) capRestPosition = cap.localPosition;
            captured = true;
        }

        public void ResetSession(int? seed = null)
        {
            if (!HasBindings) return;
            if (Application.IsPlaying(gameObject)) ColaInteractionGuide.EnsureFor(this);
            if (Application.IsPlaying(gameObject)) ColaTabletopAudio.EnsureFor(this);
            if (Application.IsPlaying(gameObject)) ColaTurnIndicator.EnsureFor(this);
            CancelPendingActions();
            if (!captured) CaptureRestPose();
            RestorePoses();
            random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            bossChoiceRandom = seed.HasValue ? new System.Random(seed.Value ^ 0x43A1) : new System.Random();
            IsBossTurn = false;
            LastActionWasBoss = false;
            LastResult = default;
            LastItemModifiers = ItemActionModifiers.None;
            model = new BottlePressureModel(Mathf.Clamp(settings.minimumBottleLimit, 1, 999), settings.probabilityExponent);
            CommittedActionCount = 0;
            BottleNumber = 0;
            BeginNextBottle();
        }

        public void CancelPendingActions()
        {
            sessionVersion++;
            GetComponent<ColaTabletopAudio>()?.CancelAll();
            if (items) items.CancelItemUsePresentation();
            if (shakeAnimator) shakeAnimator.CancelAndRestore();
            if (burstPresentation) burstPresentation.CancelAndRestore();
            StopAllCoroutines();
            busy = false;
            IsBossTurn = false;
            BossItemPhase = BossPassedTurn = false;
            PlannedBossStrength = 0;
            model?.ClearSelection();
            RestorePoses();
            RefreshView(true);
        }

        public void BeginBottle(int limit, int pressure = 0)
        {
            if (IsBusy) return;
            SetBottle(limit, pressure);
        }

        private void SetBottle(int limit, int pressure = 0)
        {
            if (random == null) random = new System.Random();
            if (model == null) model = new BottlePressureModel(limit, settings ? settings.probabilityExponent : 1.6, pressure);
            else model.BeginBottle(limit, pressure);
            BottleNumber++;
            if (items) items.OnBottleReplaced();
            RestorePoses();
            RefreshView(true);
            if (Application.IsPlaying(gameObject) && (!burstPresentation || burstPresentation.Stage == ColaBurstPresentation.BurstStage.Idle))
                ColaTabletopAudio.Cue(this, "NewBottle", bottle.position, .55f);
        }

        private void BeginNextBottle()
        {
            int minimum = Mathf.Clamp(settings.minimumBottleLimit, 1, 999);
            int maximum = Mathf.Clamp(settings.maximumBottleLimit, minimum, 999);
            int limit = random.Next(minimum, maximum + 1);
            if (maximum > minimum && BottleNumber > 0 && limit == model.Limit)
                limit = minimum + (limit - minimum + 1) % (maximum - minimum + 1);
            SetBottle(limit);
        }

        public bool SelectStrength(int strength)
        {
            var sheet = items ? items.GetComponent<ColaOrderSheet>() : null;
            if (sheet && sheet.BlocksGameplay) return false;
            if (!InputEnabled || !HasBindings || !Application.IsPlaying(gameObject) || !isActiveAndEnabled || IsBusy || model == null || strength < 1 || strength > 3) return false;
            if (!model.SelectStrength(strength)) return false;
            RefreshView();
            ColaTabletopAudio.Cue(this, "Button", strengthButtons[strength - 1].position, .65f);
            return true;
        }

        public bool ConfirmShake()
        {
            var sheet = items ? items.GetComponent<ColaOrderSheet>() : null;
            if (sheet && sheet.BlocksGameplay) return false;
            if (!InputEnabled || !HasBindings || !Application.IsPlaying(gameObject) || !isActiveAndEnabled || IsBusy || model == null) return false;
            if (!CommitSelected(false, out var result)) return false;
            busy = true;
            StartCoroutine(ResolveSequence(result, sessionVersion));
            return true;
        }

        private bool CommitSelected(bool boss, out ShakeResult result)
        {
            model.ForceBurst = ForceBurst;
            var modifiers = items ? items.PreviewAction(boss) : ItemActionModifiers.None;
            if (!model.TryCommit(() => random.NextDouble(), out result, modifiers.PressureBonus, pressureMultiplier: modifiers.PressureMultiplier)) return false;
            LastItemModifiers = items ? items.CaptureAction(boss) : modifiers;
            CommittedActionCount++;
            LastResult = result;
            LastActionWasBoss = boss;
            ReleaseButtons();
            gauge.Show(model.Pressure, model.Limit, null, result.Prediction.BurstProbability);
            return true;
        }

        public bool HandlePointer(Vector2 screenPoint)
        {
            if (!HasBindings || !Application.IsPlaying(gameObject)) return false;
            var sheet = items ? items.GetComponent<ColaOrderSheet>() : null;
            if (sheet && sheet.BlocksGameplay) return sheet.HandlePointer(screenPoint);
            Physics.SyncTransforms();
            if (!Physics.Raycast(playerCamera.ScreenPointToRay(screenPoint), out var hit, 8, ~0, QueryTriggerInteraction.Ignore)) return false;
            var bellTarget = hit.collider.GetComponent<ColaWaiterBellTarget>();
            if (bellTarget && bellTarget.Bell) return bellTarget.Bell.TryRing();
            if (!InputEnabled || IsBusy) return false;
            var orderTarget = hit.collider.GetComponent<ColaOrderTarget>();
            if (orderTarget && sheet) return sheet.HandleTarget(orderTarget);
            var itemCube = hit.collider.GetComponent<ColaItemCube>();
            if (itemCube && items) return !itemCube.IsBoss && items.TryUseSlot(itemCube.Slot);
            for (int i = 0; i < 3; i++)
                if (hit.transform == strengthButtons[i] || hit.transform.IsChildOf(strengthButtons[i])) return SelectStrength(i + 1);
            if (hit.collider == bottleCollider || hit.transform == bottle || hit.transform.IsChildOf(bottle)) return ConfirmShake();
            return false;
        }

        private void Update()
        {
            if (!Application.IsPlaying(gameObject) || !HasBindings || !isActiveAndEnabled) return;
            if (model == null) ResetSession();
            if (model.ForceBurst != ForceBurst)
            {
                model.ForceBurst = ForceBurst;
                if (!IsBusy) RefreshView();
            }
            var keyboard = Keyboard.current;
            var itemDisplay = items ? items.GetComponent<ColaItemPresentation>() : null;
            if (keyboard != null && InputEnabled && !IsBusy && (!itemDisplay || !itemDisplay.OrderOpen))
            {
                if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) SelectStrength(1);
                if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) SelectStrength(2);
                if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) SelectStrength(3);
                if (keyboard.spaceKey.wasPressedThisFrame) ConfirmShake();
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame &&
                (!EventSystem.current || !EventSystem.current.IsPointerOverGameObject()))
                HandlePointer(mouse.position.ReadValue());
        }

        public void RefreshPrediction() => RefreshView();

        private void RefreshView(bool immediate = false)
        {
            if (model == null || !gauge) return;
            model.ForceBurst = ForceBurst;
            if (model.SelectedStrength > 0)
            {
                var modifiers = items ? items.PreviewAction(false) : ItemActionModifiers.None;
                var prediction = model.Predict(model.SelectedStrength, modifiers.PressureBonus, pressureMultiplier: modifiers.PressureMultiplier);
                gauge.Show(model.Pressure, model.Limit, prediction.PressureAfter, prediction.BurstProbability, immediate);
            }
            else gauge.Show(model.Pressure, model.Limit, null, null, immediate);
            for (int i = 0; i < 3; i++)
                strengthButtons[i].localPosition = buttonRest[i] - Vector3.up * (model.SelectedStrength == i + 1 ? .012f : 0);
            // Coroutine turn completion happens after Update; show the fresh menu with the refreshed controls.
            if (items) items.GetComponent<ColaOrderSheet>()?.RefreshAvailability();
        }

        private bool IsCurrent(int version) => version == sessionVersion && isActiveAndEnabled;

        public void RevealNewRound()
        {
            if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled || IsBusy) return;
            if (!burstPresentation) burstPresentation = ColaBurstPresentation.EnsureFor(this);
            busy = true;
            StartCoroutine(RevealRound(sessionVersion));
        }

        private IEnumerator RevealRound(int version)
        {
            yield return burstPresentation.RevealAfterBurst();
            if (!IsCurrent(version)) yield break;
            busy = false;
            RefreshView();
        }

        private IEnumerator ResolveSequence(ShakeResult result, int version)
        {
            yield return ResolveAnimation(result, false, version);
            if (!IsCurrent(version)) yield break;
            yield return ResolveBossTurn(version);
        }

        public bool PassPlayerTurn()
        {
            var sheet = items ? items.GetComponent<ColaOrderSheet>() : null;
            if (sheet && sheet.BlocksGameplay) return false;
            if (!InputEnabled || !HasBindings || !Application.IsPlaying(gameObject) || !isActiveAndEnabled || IsBusy || IsBossTurn) return false;
            model.ClearSelection();
            ReleaseButtons();
            RefreshView();
            busy = true;
            StartCoroutine(ResolveBossTurn(sessionVersion));
            return true;
        }

        private IEnumerator ResolveBossTurn(int version)
        {
            if (automaticBossTurns && InputEnabled && IsCurrent(version))
            {
                IsBossTurn = true;
                ColaTabletopAudio.Cue(this, "Turn", bottle.position, .30f);
                BossPassedTurn = false;
                BossItemPhase = true;
                RefreshView();
                int strength = bossChoiceRandom.Next(1, 4);
                PlannedBossStrength = strength;
                yield return new WaitForSecondsRealtime(Mathf.Max(0, bossThinkSeconds));
                if (!IsCurrent(version)) yield break;
                var ai = items ? items.GetComponent<ColaBossItemAI>() : null;
                if (ai && ai.isActiveAndEnabled && ai.automaticItemUse && !BossPassedTurn)
                    yield return ai.PrepareTurn(strength);
                while (IsCurrent(version) && items && items.IsUsingItem) yield return null;
                if (!IsCurrent(version)) yield break;
                BossItemPhase = false;
                if (!BossPassedTurn && model.SelectStrength(strength) && CommitSelected(true, out var bossResult))
                    yield return ResolveAnimation(bossResult, true, version);
                if (!IsCurrent(version)) yield break;
            }
            busy = false;
            IsBossTurn = false;
            BossItemPhase = BossPassedTurn = false;
            RefreshView();
        }

        public bool PassBossTurn()
        {
            if (!items || !items.CanBossAct) return false;
            BossPassedTurn = true;
            model.ClearSelection();
            return true;
        }

        private IEnumerator ResolveAnimation(ShakeResult result, bool boss, int version)
        {
            if (!burstPresentation) burstPresentation = ColaBurstPresentation.EnsureFor(this);
            burstPresentation.BeginShake(boss);
            bool usesAnimator = shakeAnimator && shakeAnimator.isActiveAndEnabled && shakeAnimator.CanPlay(boss, result.Prediction.Strength);
            var audio = ColaTabletopAudio.EnsureFor(this);
            audio.BeginShake(result.Prediction.Strength, (float)model.Pressure / Mathf.Max(1, model.Limit), usesAnimator, settings.shakeSeconds);
            float duration;
            float started;
            if (usesAnimator)
            {
                // The character clip owns its full reach, shake and set-down duration.
                yield return shakeAnimator.PlayShake(boss, result.Prediction.Strength,
                    result.Burst ? () => burstPresentation.PlayBurst(boss) : (Func<IEnumerator>)null);
                if (!IsCurrent(version)) yield break;
                if (!shakeAnimator.LastPlaybackCompleted)
                {
                    sessionVersion++;
                    busy = false;
                    IsBossTurn = false;
                    BossItemPhase = BossPassedTurn = false;
                    burstPresentation.CancelAndRestore();
                    audio.EndShake();
                    RestorePoses();
                    yield break;
                }
            }
            else
            {
                duration = Mathf.Max(.1f, settings.shakeSeconds);
                started = Time.unscaledTime;
                while (Time.unscaledTime - started < duration)
                {
                    float t = (Time.unscaledTime - started) / duration;
                    float wave = Mathf.Sin(t * Mathf.PI * 12) * Mathf.Sin(t * Mathf.PI);
                    float amplitude = 1 + result.Prediction.Strength * .3f;
                    bottle.localPosition = bottleRestPosition + Vector3.right * (wave * .006f * amplitude);
                    bottle.localRotation = bottleRestRotation * Quaternion.Euler(wave * 2, 0, wave * 5 * amplitude);
                    yield return null;
                }
                if (result.Burst) yield return burstPresentation.PlayBurst(boss);
            }
            if (!IsCurrent(version)) yield break;
            audio.EndShake();
            burstPresentation.FinishShake();
            RestorePoses();
            if (!result.Burst) ColaTabletopAudio.Cue(this, "Safe", bottle.position, .40f);
            if (result.Burst) BeginNextBottle();
            else RefreshView();
            ShakeResolved?.Invoke(result);
            if (result.Burst && IsCurrent(version)) yield return burstPresentation.RevealAfterBurst();
        }

        private void ReleaseButtons()
        {
            if (!captured) return;
            for (int i = 0; i < 3; i++) if (strengthButtons[i]) strengthButtons[i].localPosition = buttonRest[i];
        }

        private void RestorePoses()
        {
            if (!captured) return;
            ReleaseButtons();
            if (bottle)
            {
                bottle.localPosition = bottleRestPosition;
                bottle.localRotation = bottleRestRotation;
                bottle.localScale = bottleRestScale;
            }
            if (cap) cap.localPosition = capRestPosition;
        }

        private void OnDisable()
        {
            CancelPendingActions();
            if (gauge && model != null) gauge.Show(model.Pressure, model.Limit, null, null, true);
        }
    }
}
