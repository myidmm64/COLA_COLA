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
        public int CommittedActionCount { get; private set; }
        public int BottleNumber { get; private set; }
        public bool IsBusy => busy || (shakeAnimator && shakeAnimator.IsPlaying);
        public BottlePressureModel Model => model;
        public ShakeResult LastResult { get; private set; }
        public bool LastActionWasBoss { get; private set; }
        public bool IsBossTurn { get; private set; }
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
            CancelPendingActions();
            if (!captured) CaptureRestPose();
            RestorePoses();
            random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            bossChoiceRandom = seed.HasValue ? new System.Random(seed.Value ^ 0x43A1) : new System.Random();
            IsBossTurn = false;
            LastActionWasBoss = false;
            LastResult = default;
            model = new BottlePressureModel(20, settings.probabilityExponent);
            CommittedActionCount = 0;
            BottleNumber = 0;
            BeginNextBottle();
        }

        public void CancelPendingActions()
        {
            sessionVersion++;
            if (shakeAnimator) shakeAnimator.CancelAndRestore();
            if (burstPresentation) burstPresentation.CancelAndRestore();
            StopAllCoroutines();
            busy = false;
            IsBossTurn = false;
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
            if (model == null) model = new BottlePressureModel(limit, settings ? settings.probabilityExponent : 2, pressure);
            else model.BeginBottle(limit, pressure);
            BottleNumber++;
            RestorePoses();
            RefreshView(true);
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
            if (!InputEnabled || !HasBindings || !Application.IsPlaying(gameObject) || !isActiveAndEnabled || IsBusy || model == null || !model.SelectStrength(strength)) return false;
            RefreshView();
            return true;
        }

        public bool ConfirmShake()
        {
            if (!InputEnabled || !HasBindings || !Application.IsPlaying(gameObject) || !isActiveAndEnabled || IsBusy || model == null) return false;
            if (!CommitSelected(false, out var result)) return false;
            busy = true;
            StartCoroutine(ResolveSequence(result, sessionVersion));
            return true;
        }

        private bool CommitSelected(bool boss, out ShakeResult result)
        {
            if (!model.TryCommit(() => random.NextDouble(), out result)) return false;
            CommittedActionCount++;
            LastResult = result;
            LastActionWasBoss = boss;
            ReleaseButtons();
            gauge.Show(model.Pressure, model.Limit, null, result.Prediction.BurstProbability);
            return true;
        }

        public bool HandlePointer(Vector2 screenPoint)
        {
            if (!InputEnabled || !HasBindings || IsBusy || !Application.IsPlaying(gameObject)) return false;
            Physics.SyncTransforms();
            if (!Physics.Raycast(playerCamera.ScreenPointToRay(screenPoint), out var hit, 8, ~0, QueryTriggerInteraction.Ignore)) return false;
            for (int i = 0; i < 3; i++)
                if (hit.transform == strengthButtons[i] || hit.transform.IsChildOf(strengthButtons[i])) return SelectStrength(i + 1);
            if (hit.collider == bottleCollider || hit.transform == bottle || hit.transform.IsChildOf(bottle)) return ConfirmShake();
            return false;
        }

        private void Update()
        {
            if (!Application.IsPlaying(gameObject) || !HasBindings || !isActiveAndEnabled) return;
            if (model == null) ResetSession();
            var keyboard = Keyboard.current;
            if (keyboard != null && InputEnabled && !IsBusy)
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

        private void RefreshView(bool immediate = false)
        {
            if (model == null || !gauge) return;
            if (model.SelectedStrength > 0)
            {
                var prediction = model.Predict(model.SelectedStrength);
                gauge.Show(model.Pressure, model.Limit, prediction.PressureAfter, prediction.BurstProbability, immediate);
            }
            else gauge.Show(model.Pressure, model.Limit, null, null, immediate);
            for (int i = 0; i < 3; i++)
                strengthButtons[i].localPosition = buttonRest[i] - Vector3.up * (model.SelectedStrength == i + 1 ? .012f : 0);
        }

        private bool IsCurrent(int version) => version == sessionVersion && isActiveAndEnabled;

        private IEnumerator ResolveSequence(ShakeResult result, int version)
        {
            yield return ResolveAnimation(result, false, version);
            if (!IsCurrent(version)) yield break;
            if (automaticBossTurns)
            {
                IsBossTurn = true;
                yield return new WaitForSecondsRealtime(Mathf.Max(0, bossThinkSeconds));
                if (!IsCurrent(version)) yield break;
                int strength = bossChoiceRandom.Next(1, 4);
                if (model.SelectStrength(strength) && CommitSelected(true, out var bossResult))
                    yield return ResolveAnimation(bossResult, true, version);
                if (!IsCurrent(version)) yield break;
            }
            busy = false;
            IsBossTurn = false;
            RefreshView();
        }

        private IEnumerator ResolveAnimation(ShakeResult result, bool boss, int version)
        {
            if (!burstPresentation) burstPresentation = ColaBurstPresentation.EnsureFor(this);
            burstPresentation.BeginShake(boss);
            float duration;
            float started;
            if (shakeAnimator && shakeAnimator.isActiveAndEnabled && shakeAnimator.CanPlay(boss, result.Prediction.Strength))
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
                    burstPresentation.CancelAndRestore();
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
            burstPresentation.FinishShake();
            RestorePoses();
            if (result.Burst) BeginNextBottle();
            else RefreshView();
            ShakeResolved?.Invoke(result);
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
