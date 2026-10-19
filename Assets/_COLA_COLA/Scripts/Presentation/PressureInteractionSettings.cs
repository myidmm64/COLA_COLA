using UnityEngine;

namespace ColaCola.Presentation
{
    [CreateAssetMenu(menuName = "COLA COLA/Pressure Interaction Settings")]
    public sealed class PressureInteractionSettings : ScriptableObject
    {
        [Range(1, 999)] public int minimumBottleLimit = 25;
        [Range(1, 999)] public int maximumBottleLimit = 35;
        [Tooltip("1 is linear; values above 1 soften early risk. Lower values raise early burst probability.")]
        [Min(.1f)] public float probabilityExponent = 2f;
        [Min(.1f)] public float shakeSeconds = .55f;
        [Min(.1f)] public float burstPauseSeconds = .55f;

        private void OnValidate()
        {
            minimumBottleLimit = Mathf.Clamp(minimumBottleLimit, 1, 999);
            maximumBottleLimit = Mathf.Clamp(maximumBottleLimit, minimumBottleLimit, 999);
            probabilityExponent = Mathf.Max(.1f, probabilityExponent);
            shakeSeconds = Mathf.Max(.1f, shakeSeconds);
            burstPauseSeconds = Mathf.Max(.1f, burstPauseSeconds);
        }
    }
}
