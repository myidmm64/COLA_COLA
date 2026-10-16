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
        private BottlePressureModel model;
        private System.Random random;
        private readonly Vector3[] buttonRest = new Vector3[3];
        private Vector3 bottleRestPosition, bottleRestScale, capRestPosition;
        private Quaternion bottleRestRotation;
        private bool captured, busy;
        public int CommittedActionCount { get; private set; }
        public int BottleNumber { get; private set; }
        public bool IsBusy => busy || (shakeAnimator && shakeAnimator.IsPlaying);
        public BottlePressureModel Model => model;
        public ShakeResult LastResult { get; private set; }
        public event Action<ShakeResult> ShakeResolved;

        private void OnEnable()
        {
            if (!HasBindings) return;
            CaptureRestPose();
            if (Application.IsPlaying(gameObject)) ResetSession();
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
            if (shakeAnimator) shakeAnimator.CancelAndRestore();
            StopAllCoroutines();
            busy = false;
            if (!captured) CaptureRestPose();
            RestorePoses();
            random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            model = new BottlePressureModel(20, settings.probabilityExponent);
            CommittedActionCount = 0;
            BottleNumber = 0;
            BeginNextBottle();
        }

        public void BeginBottle(int limit, int pressure = 0)
        {
            if (IsBusy) return;
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
            BeginBottle(limit);
        }

        public bool SelectStrength(int strength)
        {
            if (!HasBindings || !Application.IsPlaying(gameObject) || !isActiveAndEnabled || IsBusy || model == null || !model.SelectStrength(strength)) return false;
            RefreshView();
            return true;
        }

        public bool ConfirmShake()
        {
            if (!HasBindings || !Application.IsPlaying(gameObject) || !isActiveAndEnabled || IsBusy || model == null) return false;
            if (!model.TryCommit(() => random.NextDouble(), out var result)) return false;
            busy = true;
            CommittedActionCount++;
            LastResult = result;
            ReleaseButtons();
            gauge.Show(model.Pressure, model.Limit, null, result.Prediction.BurstProbability);
            StartCoroutine(ResolveAnimation(result));
            return true;
        }

        public bool HandlePointer(Vector2 screenPoint)
        {
            if (!HasBindings || IsBusy || !Application.IsPlaying(gameObject)) return false;
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

        private IEnumerator ResolveAnimation(ShakeResult result)
        {
            float duration;
            float started;
            if (shakeAnimator && shakeAnimator.isActiveAndEnabled && shakeAnimator.CanPlay(false, result.Prediction.Strength))
            {
                // The character clip owns its full reach, shake and set-down duration.
                yield return shakeAnimator.PlayShake(false, result.Prediction.Strength);
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
            }
            RestorePoses();
            if (result.Burst)
            {
                started = Time.unscaledTime;
                duration = Mathf.Max(.1f, settings.burstPauseSeconds);
                while (Time.unscaledTime - started < duration)
                {
                    float t = (Time.unscaledTime - started) / duration;
                    bottle.localScale = bottleRestScale * (1 + Mathf.Sin(t * Mathf.PI) * .045f);
                    if (cap) cap.position += Vector3.up * (Time.unscaledDeltaTime * .18f);
                    yield return null;
                }
            }
            RestorePoses();
            busy = false;
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
            if (shakeAnimator) shakeAnimator.CancelAndRestore();
            StopAllCoroutines();
            busy = false;
            RestorePoses();
            if (gauge && model != null) gauge.Show(model.Pressure, model.Limit, null, null, true);
        }
    }
}
