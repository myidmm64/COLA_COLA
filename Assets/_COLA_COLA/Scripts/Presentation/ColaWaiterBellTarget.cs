using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Routes the bell's world hit target to its owning service without bypassing payment rules.</summary>
    public sealed class ColaWaiterBellTarget : MonoBehaviour
    {
        public ColaWaiterBell Bell { get; internal set; }
    }
}
