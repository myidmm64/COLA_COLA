using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Ownership and input on a purchased physical item. The legacy class name preserves scene bindings.</summary>
    public sealed class ColaItemCube : MonoBehaviour
    {
        public int Slot { get; private set; }
        public bool IsBoss { get; private set; }
        public void Bind(int slot, bool boss = false) { Slot = slot; IsBoss = boss; }
    }
}
