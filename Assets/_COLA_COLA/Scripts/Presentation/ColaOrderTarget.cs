using ColaCola.Core;
using UnityEngine;

namespace ColaCola.Presentation
{
    public sealed class ColaOrderTarget : MonoBehaviour
    {
        public enum Action { Open, Close, Buy, Inspect, Remove, Checkout, RecordAbilities, RecordAchievements, RecordBossAbilities }
        public Action Kind { get; private set; }
        public ColaItemId Item { get; private set; }
        public void Bind(Action kind, ColaItemId item = default) { Kind = kind; Item = item; }
    }
}
