using UnityEngine;
using ColaCola.Core;

namespace ColaCola.Presentation
{
    /// <summary>Always-available in-match service with the shared price for a random item pack.</summary>
    [DisallowMultipleComponent]
    public sealed class ColaWaiterBell : MonoBehaviour
    {
        private ColaItemController items;
        private ColaItemModelLibrary models;
        private GameObject root;
        private Transform plunger, bellBody;
        private Quaternion bodyRotation;
        private Vector3 bodyPosition;
        private AudioSource audioSource;
        private AudioClip ding;
        private ColaWaiterBellPress handPress;
        private float pressedAt = -10;
        public Transform ModelRoot => root ? root.transform : null;
        public Collider HitTarget { get; private set; }
        public bool CanRing => isActiveAndEnabled && items && items.CanOrderWaiter() && (!handPress || !handPress.IsPlaying);
        public Vector3 PressSurface
        {
            get
            {
                float elapsed = ColaGameClock.Time - pressedAt;
                float depression = elapsed < .045f ? Mathf.Clamp01(elapsed / .045f) : Mathf.Exp(-(elapsed - .045f) * 22);
                return root.transform.TransformPoint(new Vector3(0, .096f - .011f * depression, 0));
            }
        }
        public bool CanHover => isActiveAndEnabled && items && items.CanInspectWaiter;
        public string HoverDescription
        {
            get
            {
                int cost = items ? items.WaiterCostFor() : ColaItemController.WaiterCost;
                int count = items ? items.WaiterCountFor() : ColaItemController.WaiterItemCount;
                string description = string.Format(ColaLocalization.Get("ui.bell_offer", "웨이터벨 · {0}토큰으로 무작위 아이템 {1}개"), cost, count);
                if (!items) return description;
                int free = items.CapacityFor() - items.InventoryCount;
                string reason = free < count ? string.Format(ColaLocalization.Get("ui.tray_full", "빈 칸 부족 ({0}/{1})"), free, count) : "";
                if (!items.match.CanAffordTokens(cost))
                    reason += (reason.Length > 0 ? " · " : "") + string.Format(ColaLocalization.Get("ui.tokens_required", "{0}토큰 필요"), cost);
                return reason.Length > 0 ? description + "\n" + reason : description;
            }
        }

        public void Bind(ColaItemController controller)
        {
            if (items)
            {
                items.WaiterOrdered -= OnOrder;
                items.PresentationReset -= RestoreBell;
            }
            items = controller;
            if (!root) Build();
            handPress = ColaWaiterBellPress.EnsureFor(items.match.interaction);
            items.WaiterOrdered += OnOrder;
            items.PresentationReset += RestoreBell;
            RestoreBell();
        }

        public bool TryRing()
        {
            if (CanRing) return (handPress && handPress.Press(this)) || items.TryOrderWaiter();
            if (CanHover) ColaTabletopAudio.Cue(items.match.interaction, "Blocked", root.transform.position, .45f);
            return false;
        }

        internal bool CompleteHandPress() => isActiveAndEnabled && items && items.TryOrderWaiter();

        private void OnOrder(int[] slots, bool boss)
        {
            if (boss || !isActiveAndEnabled) return;
            pressedAt = ColaGameClock.Time;
            ColaInteractionCamera.Cue(items.match.interaction, "Bell", root.transform.position);
            // Keep the existing bell timbre; repeated orders restart this source instead of stacking it.
            audioSource.Stop();
            audioSource.PlayOneShot(ding, .48f);
        }

        private void Update()
        {
            if (!Application.IsPlaying(gameObject) || !plunger) return;
            float elapsed = ColaGameClock.Time - pressedAt;
            float press = elapsed < .045f ? Mathf.Clamp01(elapsed / .045f) : Mathf.Exp(-(elapsed - .045f) * 22);
            float ring = elapsed < .42f ? Mathf.Sin(elapsed * 92) * Mathf.Exp(-elapsed * 13) : 0;
            plunger.localPosition = Vector3.down * (.011f * press) + Vector3.up * (ring * .0006f);
            if (bellBody)
            {
                bellBody.localPosition = bodyPosition + Vector3.down * (.0015f * press);
                bellBody.localRotation = bodyRotation * Quaternion.Euler(ring * 1.5f, 0, ring * .8f);
            }
        }

        private void RestoreBell()
        {
            if (handPress) handPress.Cancel();
            pressedAt = -10;
            if (plunger) plunger.localPosition = Vector3.zero;
            if (bellBody) { bellBody.localPosition = bodyPosition; bellBody.localRotation = bodyRotation; }
            if (audioSource) audioSource.Stop();
        }

        private void Build()
        {
            root = new GameObject("WaiterBell_Runtime") { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(items.transform.root, false);
            root.transform.SetPositionAndRotation(new Vector3(.29f, .833f, -.37f), Quaternion.Euler(0, -8, 0));
            models = new ColaItemModelLibrary();
            bellBody = models.CreateBellPart("WaiterBell", root.transform).transform;
            bodyPosition = bellBody.localPosition;
            bodyRotation = bellBody.localRotation;
            plunger = models.CreateBellPart("WaiterBellPlunger", root.transform).transform;
            var hit = root.AddComponent<BoxCollider>();
            hit.center = new Vector3(0, .056f, 0);
            hit.size = new Vector3(.174f, .112f, .174f);
            HitTarget = hit;
            var target = root.AddComponent<ColaWaiterBellTarget>();
            target.Bell = this;

            audioSource = root.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = .35f;
            audioSource.minDistance = .7f;
            audioSource.maxDistance = 5;
            audioSource.dopplerLevel = 0;
            const int rate = 24000, samples = rate / 2;
            var waveform = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)rate;
                float attack = Mathf.Min(1, t * 600);
                waveform[i] = attack * (.35f * Mathf.Sin(2 * Mathf.PI * 1480 * t) * Mathf.Exp(-t * 9) +
                    .20f * Mathf.Sin(2 * Mathf.PI * 2353 * t) * Mathf.Exp(-t * 14) +
                    .10f * Mathf.Sin(2 * Mathf.PI * 3517 * t) * Mathf.Exp(-t * 22));
            }
            ding = AudioClip.Create("WaiterBell_Ding_Runtime", samples, 1, rate, false);
            ding.SetData(waveform, 0);
        }

        private void OnDisable() => RestoreBell();
        private void OnDestroy()
        {
            if (items)
            {
                items.WaiterOrdered -= OnOrder;
                items.PresentationReset -= RestoreBell;
            }
            if (root) Destroy(root);
            if (ding) Destroy(ding);
            models?.Dispose();
        }
    }
}
