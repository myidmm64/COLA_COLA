using UnityEngine;

namespace ColaCola.Presentation
{
    [CreateAssetMenu(menuName = "COLA COLA/Pressure Interaction Settings")]
    public sealed class PressureInteractionSettings : ScriptableObject
    {
        [Range(1, 999)] public int minimumBottleLimit = 18;
        [Range(1, 999)] public int maximumBottleLimit = 24;
        [Min(.1f)] public float probabilityExponent = 2;
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
