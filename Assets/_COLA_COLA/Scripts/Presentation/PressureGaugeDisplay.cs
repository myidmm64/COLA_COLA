using System;
using System.Globalization;
using UnityEngine;

namespace ColaCola.Presentation
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class PressureGaugeDisplay : MonoBehaviour
    {
        public const string GlyphCharacters = "0123456789.-%<";
        public Mesh[] glyphMeshes;
        public Transform needlePivot, previewNeedlePivot;
        public MeshFilter[] limitDigits, probabilityDigits;
        public Vector3 limitTextOrigin = new Vector3(.064f, -.029f, -.057f);
        public Vector3 probabilityTextOrigin = new Vector3(0, -.153f, -.065f);
        public float limitPixelSize = .0038f, probabilityPixelSize = .00355f;
        [Min(.05f)] public float forecastTransitionSeconds = .38f;
        [SerializeField] private int pressure, limit = 20, predictedPressure;
        [SerializeField] private bool hasPrediction, hasProbability;
        [SerializeField] private double probability;
        private float needleAngle = 225;
        private float previewAngle = 225, previewStartAngle, previewStarted;
        private double displayedProbability, previewStartProbability;
        private bool previewAnimating;

        public int CurrentPressure => pressure;
        public int CurrentLimit => limit;
        public int PredictedPressure => predictedPressure;
        public bool HasPrediction => hasPrediction;
        public string ProbabilityText => hasProbability ? FormatProbability(probability) : "--%";
        public string DisplayedProbabilityText => hasProbability ? FormatProbability(displayedProbability) : "--%";
        public double DisplayedProbability => displayedProbability;
        public float DisplayedPreviewAngle => previewAngle;
        public bool IsPreviewAnimating => previewAnimating;

        public void Show(int currentPressure, int bottleLimit, int? expectedPressure,
            double? expectedProbability, bool immediate = false)
        {
            if (Application.IsPlaying(gameObject)) AdvancePreview();
            bool wasPrediction = hasPrediction;
            bool changed = !wasPrediction || limit != bottleLimit || predictedPressure != expectedPressure ||
                hasProbability != expectedProbability.HasValue || Math.Abs(probability - (expectedProbability ?? 0)) > .00000001;
            pressure = Mathf.Max(0, currentPressure);
            limit = Mathf.Clamp(bottleLimit, 1, 999);
            hasPrediction = expectedPressure.HasValue;
            predictedPressure = expectedPressure ?? pressure;
            hasProbability = expectedProbability.HasValue;
            probability = expectedProbability ?? 0;
            bool snap = immediate || !Application.IsPlaying(gameObject) || !hasPrediction;
            if (snap)
            {
                previewAnimating = false;
                previewAngle = Angle(predictedPressure);
                displayedProbability = probability;
            }
            else if (changed)
            {
                if (!wasPrediction) { previewAngle = Angle(pressure); displayedProbability = 0; }
                previewStartAngle = previewAngle;
                previewStartProbability = displayedProbability;
                previewStarted = Time.unscaledTime;
                previewAnimating = true;
            }
            Refresh(immediate || !Application.IsPlaying(gameObject));
        }

        public bool TryValidateBindings(out string message)
        {
            bool valid = needlePivot && previewNeedlePivot && glyphMeshes != null &&
                glyphMeshes.Length == GlyphCharacters.Length && Array.TrueForAll(glyphMeshes, m => m) &&
                limitDigits != null && limitDigits.Length == 3 && Array.TrueForAll(limitDigits, m => m) &&
                probabilityDigits != null && probabilityDigits.Length == 6 && Array.TrueForAll(probabilityDigits, m => m);
            message = valid ? "Dynamic limit, probability and both needles are bound." : "Incomplete gauge bindings.";
            return valid;
        }

        private void OnEnable()
        {
            previewAnimating = false;
            previewAngle = Angle(predictedPressure);
            displayedProbability = probability;
            Refresh(true);
        }

        private void OnDisable() { previewAnimating = false; }

        private void Update()
        {
            if (!needlePivot) return;
            float target = Angle(pressure);
            needleAngle = Application.IsPlaying(gameObject)
                ? Mathf.MoveTowards(needleAngle, target, Time.unscaledDeltaTime * 420) : target;
            needlePivot.localRotation = Quaternion.Euler(0, 0, needleAngle);
            if (previewAnimating)
            {
                AdvancePreview();
                Refresh(false);
            }
        }

        private void AdvancePreview()
        {
            if (!previewAnimating) return;
            float t = Mathf.Clamp01((Time.unscaledTime - previewStarted) / Mathf.Max(.05f, forecastTransitionSeconds));
            float eased = t * t * (3 - 2 * t);
            // Keep the authored 270-degree sweep; LerpAngle would wrap across the empty quadrant.
            previewAngle = Mathf.Lerp(previewStartAngle, Angle(predictedPressure), eased);
            displayedProbability = previewStartProbability + (probability - previewStartProbability) * eased;
            if (t >= 1) { previewAnimating = false; displayedProbability = probability; }
        }

        private float Angle(int value) => 225f - 270f * Mathf.Clamp01((float)value / Math.Max(1, limit));

        private void Refresh(bool immediate)
        {
            Write(limitDigits, limit.ToString(CultureInfo.InvariantCulture), limitTextOrigin, limitPixelSize);
            Write(probabilityDigits, DisplayedProbabilityText, probabilityTextOrigin, probabilityPixelSize);
            if (needlePivot && immediate)
            {
                needleAngle = Angle(pressure);
                needlePivot.localRotation = Quaternion.Euler(0, 0, needleAngle);
            }
            if (previewNeedlePivot)
            {
                previewNeedlePivot.gameObject.SetActive(hasPrediction);
                previewNeedlePivot.localRotation = Quaternion.Euler(0, 0, previewAngle);
            }
        }

        private void Write(MeshFilter[] slots, string text, Vector3 origin, float pixelSize)
        {
            if (slots == null || glyphMeshes == null) return;
            float step = 6 * pixelSize;
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i]) continue;
                var renderer = slots[i].GetComponent<Renderer>();
                bool used = i < text.Length;
                if (renderer) renderer.enabled = used;
                if (!used) continue;
                int glyph = GlyphCharacters.IndexOf(text[i]);
                if (glyph < 0 || glyph >= glyphMeshes.Length) continue;
                slots[i].sharedMesh = glyphMeshes[glyph];
                slots[i].transform.localScale = Vector3.one * pixelSize;
                slots[i].transform.localPosition = origin + Vector3.right * ((i - (text.Length - 1) * .5f) * step);
            }
        }

        public static string FormatProbability(double value)
        {
            value = Math.Max(0, Math.Min(1, value));
            if (value >= 1) return "100%";
            if (value > 0 && value * 100 < .1) return "<0.1%";
            double percent = Math.Min(99.9, Math.Round(value * 100, 1, MidpointRounding.AwayFromZero));
            return percent.ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }
    }
}
