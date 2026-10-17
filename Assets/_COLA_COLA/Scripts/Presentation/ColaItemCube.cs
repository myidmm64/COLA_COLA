using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Hit target on a purchased placeholder prop, rather than a screen-space inventory icon.</summary>
    public sealed class ColaItemCube : MonoBehaviour
    {
        public int Slot { get; private set; }
        public bool IsBoss { get; private set; }
        public void Bind(int slot, bool boss = false) { Slot = slot; IsBoss = boss; }
    }
}
