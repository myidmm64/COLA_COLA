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
        private int basePredictedPressure, increasedPressure, reducedPressure;
        private bool hasIncrease, hasReduction;
        private float basePreviewAngle = 225, increasePreviewAngle = 225, reductionPreviewAngle = 225;
        private float baseStartAngle, increaseStartAngle, reductionStartAngle;
        private Transform increaseNeedlePivot, reductionNeedlePivot;
        private Material increaseNeedleMaterial, reductionNeedleMaterial;
        private Transform baseMarkerPivot;
        private Material baseMarkerMaterial;
        private Mesh baseMarkerMesh;

        public int CurrentPressure => pressure;
        public int CurrentLimit => limit;
        public int PredictedPressure => predictedPressure;
        public bool HasPrediction => hasPrediction;
        public string ProbabilityText => hasProbability ? FormatProbability(probability) : "--%";
        public string DisplayedProbabilityText => hasProbability ? FormatProbability(displayedProbability) : "--%";
        public double DisplayedProbability => displayedProbability;
        public float DisplayedPreviewAngle => previewAngle;
        public bool IsPreviewAnimating => previewAnimating;
        public bool HasPressureIncrease => hasPrediction && hasIncrease;
        public bool HasPressureReduction => hasPrediction && hasReduction;

        public void Show(int currentPressure, int bottleLimit, int? expectedPressure,
            double? expectedProbability, bool immediate = false, int? baseExpectedPressure = null,
            int? increasedExpectedPressure = null, int? reducedExpectedPressure = null)
        {
            if (Application.IsPlaying(gameObject)) AdvancePreview();
            bool wasPrediction = hasPrediction;
            bool changed = !wasPrediction || limit != bottleLimit || predictedPressure != expectedPressure ||
                basePredictedPressure != (baseExpectedPressure ?? expectedPressure ?? currentPressure) ||
                hasIncrease != increasedExpectedPressure.HasValue || hasReduction != reducedExpectedPressure.HasValue ||
                increasedPressure != (increasedExpectedPressure ?? currentPressure) ||
                reducedPressure != (reducedExpectedPressure ?? currentPressure) ||
                hasProbability != expectedProbability.HasValue || Math.Abs(probability - (expectedProbability ?? 0)) > .00000001;
            if (!hasIncrease) increasePreviewAngle = basePreviewAngle;
            if (!hasReduction) reductionPreviewAngle = basePreviewAngle;
            pressure = Mathf.Max(0, currentPressure);
            limit = Mathf.Clamp(bottleLimit, 1, 999);
            hasPrediction = expectedPressure.HasValue;
            predictedPressure = expectedPressure ?? pressure;
            basePredictedPressure = baseExpectedPressure ?? predictedPressure;
            increasedPressure = increasedExpectedPressure ?? pressure;
            reducedPressure = reducedExpectedPressure ?? pressure;
            hasIncrease = hasPrediction && increasedExpectedPressure.HasValue;
            hasReduction = hasPrediction && reducedExpectedPressure.HasValue;
            hasProbability = expectedProbability.HasValue;
            probability = expectedProbability ?? 0;
            bool snap = immediate || !Application.IsPlaying(gameObject) || !hasPrediction;
            if (snap)
            {
                previewAnimating = false;
                previewAngle = Angle(predictedPressure);
                basePreviewAngle = Angle(basePredictedPressure);
                increasePreviewAngle = Angle(increasedPressure);
                reductionPreviewAngle = Angle(reducedPressure);
                displayedProbability = probability;
            }
            else if (changed)
            {
                if (!wasPrediction)
                {
                    previewAngle = basePreviewAngle = increasePreviewAngle = reductionPreviewAngle = Angle(pressure);
                    displayedProbability = 0;
                }
                previewStartAngle = previewAngle;
                baseStartAngle = basePreviewAngle;
                increaseStartAngle = increasePreviewAngle;
                reductionStartAngle = reductionPreviewAngle;
                previewStartProbability = displayedProbability;
                previewStarted = ColaGameClock.Time;
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
            basePreviewAngle = Angle(basePredictedPressure);
            increasePreviewAngle = Angle(increasedPressure);
            reductionPreviewAngle = Angle(reducedPressure);
            displayedProbability = probability;
            Refresh(true);
        }

        private void OnDisable()
        {
            previewAnimating = false;
            if (increaseNeedlePivot) increaseNeedlePivot.gameObject.SetActive(false);
            if (reductionNeedlePivot) reductionNeedlePivot.gameObject.SetActive(false);
            if (baseMarkerPivot) baseMarkerPivot.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            ReleaseRuntimeObject(increaseNeedlePivot ? increaseNeedlePivot.gameObject : null);
            ReleaseRuntimeObject(reductionNeedlePivot ? reductionNeedlePivot.gameObject : null);
            ReleaseRuntimeObject(increaseNeedleMaterial);
            ReleaseRuntimeObject(reductionNeedleMaterial);
            ReleaseRuntimeObject(baseMarkerPivot ? baseMarkerPivot.gameObject : null);
            ReleaseRuntimeObject(baseMarkerMaterial);
            ReleaseRuntimeObject(baseMarkerMesh);
        }

        private void Update()
        {
            if (!needlePivot) return;
            float target = Angle(pressure);
            needleAngle = Application.IsPlaying(gameObject)
                ? Mathf.MoveTowards(needleAngle, target, ColaGameClock.Delta * 420) : target;
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
            float t = Mathf.Clamp01((ColaGameClock.Time - previewStarted) / Mathf.Max(.05f, forecastTransitionSeconds));
            float eased = t * t * (3 - 2 * t);
            // Keep the authored 270-degree sweep; LerpAngle would wrap across the empty quadrant.
            previewAngle = Mathf.Lerp(previewStartAngle, Angle(predictedPressure), eased);
            basePreviewAngle = Mathf.Lerp(baseStartAngle, Angle(basePredictedPressure), eased);
            increasePreviewAngle = Mathf.Lerp(increaseStartAngle, Angle(increasedPressure), eased);
            reductionPreviewAngle = Mathf.Lerp(reductionStartAngle, Angle(reducedPressure), eased);
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
                previewNeedlePivot.localRotation = Quaternion.Euler(0, 0, basePreviewAngle);
            }
            if (Application.IsPlaying(gameObject))
            {
                if (hasIncrease && !increaseNeedlePivot)
                    increaseNeedlePivot = CreateEffectNeedle("PressureIncreaseForecast", new Color(.82f, .29f, .13f), .95f, .001f, out increaseNeedleMaterial);
                if (hasReduction && !reductionNeedlePivot)
                    reductionNeedlePivot = CreateEffectNeedle("PressureReductionForecast", new Color(.19f, .64f, .65f), .87f, .002f, out reductionNeedleMaterial);
                if (hasPrediction && (hasIncrease || hasReduction) && !baseMarkerPivot) CreateBaseMarker();
            }
            bool showBaseMarker = hasPrediction && (hasIncrease || hasReduction) && baseMarkerPivot;
            if (previewNeedlePivot) previewNeedlePivot.gameObject.SetActive(hasPrediction && !showBaseMarker);
            RefreshEffectNeedle(baseMarkerPivot, showBaseMarker, basePreviewAngle);
            RefreshEffectNeedle(increaseNeedlePivot, hasPrediction && hasIncrease, increasePreviewAngle);
            RefreshEffectNeedle(reductionNeedlePivot, hasPrediction && hasReduction, reductionPreviewAngle);
        }

        private void CreateBaseMarker()
        {
            baseMarkerPivot = CreateEffectNeedle("BasePressureMarker", new Color(.96f, .52f, .12f), 1, .003f, out baseMarkerMaterial);
            if (!baseMarkerPivot) return;
            // Exactly one authored-size dash: no shaft and no arrowhead.
            baseMarkerMesh = new Mesh { name = "BasePressureSingleDash", hideFlags = HideFlags.DontSave };
            baseMarkerMesh.vertices = new[]
            {
                new Vector3(.0165f, -.0015f, -.001f), new Vector3(.0355f, -.0015f, -.001f),
                new Vector3(.0355f, .0015f, -.001f), new Vector3(.0165f, .0015f, -.001f)
            };
            baseMarkerMesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            baseMarkerMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            baseMarkerMesh.RecalculateBounds();
            foreach (var filter in baseMarkerPivot.GetComponentsInChildren<MeshFilter>(true)) filter.sharedMesh = baseMarkerMesh;
        }

        // Reuse the authored dashed mesh, so existing scenes need no prefab rebuild.
        // These objects/materials exist only in Play and never become saved assets.
        private Transform CreateEffectNeedle(string name, Color color, float radialScale, float depth, out Material material)
        {
            material = null;
            if (!previewNeedlePivot) return null;
            var source = previewNeedlePivot.GetComponentInChildren<Renderer>(true);
            if (!source || !source.sharedMaterial) return null;
            material = new Material(source.sharedMaterial) { name = name, hideFlags = HideFlags.DontSave };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            var pointer = Instantiate(previewNeedlePivot.gameObject, previewNeedlePivot.parent).transform;
            pointer.name = name;
            pointer.localPosition = previewNeedlePivot.localPosition + Vector3.back * depth;
            pointer.localScale = Vector3.Scale(previewNeedlePivot.localScale, new Vector3(radialScale, radialScale, 1));
            foreach (var child in pointer.GetComponentsInChildren<Transform>(true)) child.gameObject.hideFlags = HideFlags.DontSave;
            foreach (var renderer in pointer.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return pointer;
        }

        private static void RefreshEffectNeedle(Transform pointer, bool visible, float angle)
        {
            if (!pointer) return;
            pointer.gameObject.SetActive(visible);
            pointer.localRotation = Quaternion.Euler(0, 0, angle);
        }

        private static void ReleaseRuntimeObject(UnityEngine.Object obj)
        {
            if (!obj) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
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
