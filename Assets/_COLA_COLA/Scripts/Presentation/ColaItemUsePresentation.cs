using System;
using System.Collections.Generic;
using ColaCola.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColaCola.Presentation
{
    /// <summary>Short, tactile tabletop prop gestures. Never applies item rules or consumes gameplay RNG.</summary>
    [DisallowMultipleComponent]
    public sealed class ColaItemUsePresentation : MonoBehaviour
    {
        private const float Duration = .68f;
        private const float ReachEnd = .32f, ActionEnd = .74f, ContactTime = (ReachEnd + ActionEnd) * .5f;
        private const int BitCount = 5;
        private ColaItemController items;
        private ColaItemPresentation inventory;
        private GameObject root, prop;
        private readonly Material[] materials = new Material[5];
        private readonly Bit[] bits = new Bit[BitCount];
        private Mesh chipMesh;
        private ColaItemId current;
        private Vector3 origin, stage, scale, localPivot;
        private Quaternion rotation;
        private float started;
        private bool impact, boss;
        public bool IsPlaying { get; private set; }
        private readonly Dictionary<Material, Material> fadeCopies = new();
        private bool fading;

        private sealed class Bit
        {
            public Transform transform;
            public Renderer renderer;
            public Vector3 position, velocity, spin, scale;
        }

        public void Bind(ColaItemController controller, ColaItemPresentation display)
        {
            Cancel();
            Unsubscribe();
            items = controller;
            inventory = display;
            items.ItemUsed += Play;
            items.PresentationReset += Cancel;
        }

        private void Unsubscribe()
        {
            if (!items) return;
            items.ItemUsed -= Play;
            items.PresentationReset -= Cancel;
        }

        private void Play(ColaItemId id, int slot, bool isBoss)
        {
            if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled || !inventory || !items || !items.match || !items.match.interaction) return;
            Cancel();
            EnsureResources();
            prop = inventory.TakeModelForUse(slot, isBoss);
            if (!prop)
            {
                ColaTabletopAudio.Cue(items.match.interaction, "Item_" + id, items.match.interaction.bottle.position, .78f, isBoss);
                return;
            }
            current = id;
            boss = isBoss;
            rotation = prop.transform.rotation;
            scale = prop.transform.lossyScale;
            prop.transform.SetParent(root.transform, true);
            foreach (var collider in prop.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            // Model assets have a bottom origin. Rotate around their visible centre, not the table contact.
            var renderers = prop.GetComponentsInChildren<Renderer>(true);
            var bounds = new Bounds(prop.transform.position, Vector3.zero);
            bool hasBounds = false;
            foreach (var renderer in renderers)
            {
                if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            localPivot = prop.transform.InverseTransformPoint(bounds.center);
            origin = prop.transform.TransformPoint(localPivot);
            stage = GesturePosition(id, isBoss);
            started = ColaGameClock.Time;
            impact = false;
            fading = false;
            IsPlaying = true;
            ColaInteractionCamera.Cue(items.match.interaction, "ItemReach", origin, isBoss);
        }

        private Vector3 GesturePosition(ColaItemId id, bool isBoss)
        {
            var owner = items.match.interaction;
            bool bottleItem = id == ColaItemId.IceCup || id == ColaItemId.FizzCandy ||
                id == ColaItemId.HotSauce || id == ColaItemId.SodaWater || id == ColaItemId.BottleOpener ||
                id == ColaItemId.SaltPacket || id == ColaItemId.CoolingPack || id == ColaItemId.FreshLid;
            if (bottleItem && owner.bottle)
            {
                var towardsBottle = owner.bottle.position - origin;
                towardsBottle.y = 0;
                return origin + Vector3.ClampMagnitude(towardsBottle, .15f) + Vector3.up * .065f;
            }
            var tray = inventory.StorageRoot(isBoss);
            // Small gestures beside the original compartment; no camera rush or trip across the gauge.
            return origin + Vector3.up * .065f + tray.forward * (isBoss ? .035f : -.045f) - tray.right * .02f;
        }

        private void Update()
        {
            if (!IsPlaying) return;
            if (!items || !items.isActiveAndEnabled || !inventory || !inventory.isActiveAndEnabled || !items.match ||
                !items.match.interaction || !items.match.interaction.isActiveAndEnabled ||
                items.match.State != ColaDuelController.DuelState.Playing || !prop)
            { Cancel(); return; }
            float elapsed = ColaGameClock.Time - started;
            float t = Mathf.Clamp01(elapsed / Duration);
            // Follow continuous elapsed time so the prop updates on every rendered frame.
            float reach = Ease(Mathf.Clamp01(t / ReachEnd));
            float action = Ease(Mathf.Clamp01((t - ReachEnd) / (ActionEnd - ReachEnd)));
            float exit = Ease(Mathf.Clamp01((t - ActionEnd) / (1 - ActionEnd)));
            Vector3 position = Vector3.Lerp(origin, stage, reach) + Vector3.up * (Mathf.Sin(reach * Mathf.PI) * .018f);
            Vector3 angles = Vector3.zero;
            float lean = Mathf.Sin(action * Mathf.PI);
            float side = boss ? -1 : 1;
            switch (current)
            {
                case ColaItemId.IceCup: angles.z = side * 32 * lean; break;
                case ColaItemId.FizzCandy:
                    angles.z = side * 12 * lean; position.y += lean * .004f; break;
                case ColaItemId.QueueTicket:
                    angles = new Vector3(0, side * 10 * lean, side * 12 * lean); break;
                case ColaItemId.Bill:
                    position.z += side * action * .025f; angles.x = -8 * lean; break;
                case ColaItemId.SpicyOrder:
                    position.y -= .012f * lean; angles.x = 9 * lean; break;
                case ColaItemId.SafetyCap:
                    position.y -= .010f * lean; angles.z = side * 6 * lean; break;
                case ColaItemId.PizzaSlice:
                    angles.x = (boss ? -1 : 1) * 12 * lean; angles.z = side * 5 * lean; break;
                case ColaItemId.HotSauce: angles.z = side * 34 * lean; break;
                case ColaItemId.DoubleStamp:
                    position.y -= .060f * lean; angles.z = side * 3 * lean; break;
                case ColaItemId.BottleOpener:
                    angles.z = side * 24 * lean; position.y += .012f * lean; break;
                case ColaItemId.Straw:
                    angles.x = 18 * lean; angles.z = side * 8 * lean; break;
                case ColaItemId.SodaWater:
                    angles.z = side * 30 * lean; break;
                case ColaItemId.CoinCup:
                    angles.z = side * 28 * lean; position.y -= .008f * lean; break;
                case ColaItemId.SwapTongs:
                    angles.y = side * 20 * lean; position.z += side * .025f * action; break;
                case ColaItemId.SaltPacket:
                    angles.z = side * 35 * lean; position.y += .008f * lean; break;
                case ColaItemId.CoolingPack:
                    angles.x = 8 * lean; position.y -= .012f * lean; break;
                case ColaItemId.TokenPunch:
                    position.y -= .030f * lean; angles.z = side * 4 * lean; break;
                case ColaItemId.FreshLid:
                    angles.y = side * 42 * lean; position.y -= .010f * lean; break;
                case ColaItemId.ExchangeCoupon:
                    angles.z = side * 14 * lean; position.z += side * .02f * action; break;
            }
            position += new Vector3(0, .014f, boss ? .045f : -.045f) * exit;
            var poseRotation = rotation * Quaternion.Euler(angles);
            prop.transform.localScale = scale;
            prop.transform.SetPositionAndRotation(position - poseRotation * Vector3.Scale(localPivot, scale), poseRotation);
            if (!impact && t >= ContactTime)
            {
                impact = true;
                ColaTabletopAudio.Cue(items.match.interaction, "Item_" + current, position, .78f, boss);
                Scatter();
            }
            if (t > ActionEnd) Fade(1 - exit);
            AnimateBits(elapsed);
            if (t >= 1) FinishGesture();
        }

        private static float Ease(float value) => value * value * (3 - 2 * value);

        private void Fade(float opacity)
        {
            if (!fading)
            {
                foreach (var renderer in prop.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var originals = renderer.sharedMaterials;
                    var copies = new Material[originals.Length];
                    for (int i = 0; i < originals.Length; i++)
                    {
                        var original = originals[i];
                        if (!original) continue;
                        if (!fadeCopies.TryGetValue(original, out var copy))
                        {
                            copy = new Material(original) { name = "ItemUse_ExitFade", hideFlags = HideFlags.DontSave };
                            copy.SetFloat("_Surface", 1);
                            copy.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                            copy.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                            copy.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                            copy.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                            copy.SetFloat("_ZWrite", 0);
                            copy.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                            copy.SetOverrideTag("RenderType", "Transparent");
                            copy.SetShaderPassEnabled("ShadowCaster", false);
                            copy.renderQueue = (int)RenderQueue.Transparent;
                            fadeCopies.Add(original, copy);
                        }
                        copies[i] = copy;
                    }
                    renderer.sharedMaterials = copies;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                fading = true;
            }
            foreach (var copy in fadeCopies.Values)
            {
                var color = copy.GetColor("_BaseColor"); color.a = opacity;
                copy.SetColor("_BaseColor", color);
            }
        }

        private int MaterialIndex => current switch
        {
            ColaItemId.FizzCandy or ColaItemId.HotSauce or ColaItemId.SpicyOrder => 1,
            ColaItemId.SafetyCap or ColaItemId.BottleOpener or ColaItemId.SwapTongs or
                ColaItemId.CoolingPack or ColaItemId.FreshLid => 3,
            ColaItemId.DoubleStamp or ColaItemId.CoinCup or ColaItemId.Straw or ColaItemId.TokenPunch => 4,
            _ => 0
        };

        private void Scatter()
        {
            bool liquid = current == ColaItemId.IceCup || current == ColaItemId.HotSauce || current == ColaItemId.SodaWater;
            bool paper = current == ColaItemId.QueueTicket || current == ColaItemId.Bill || current == ColaItemId.SpicyOrder || current == ColaItemId.ExchangeCoupon;
            int count = current switch
            {
                ColaItemId.IceCup => 5,
                ColaItemId.HotSauce or ColaItemId.QueueTicket or ColaItemId.PizzaSlice => 4,
                ColaItemId.FizzCandy => 3,
                ColaItemId.SodaWater => 4,
                ColaItemId.CoinCup => 3,
                ColaItemId.SaltPacket or ColaItemId.ExchangeCoupon => 3,
                _ => 0
            };
            var emissionPoint = prop.transform.TransformPoint(localPivot);
            for (int i = 0; i < bits.Length; i++)
            {
                var bit = bits[i];
                bit.transform.gameObject.SetActive(i < count);
                if (i >= count) continue;
                float angle = i * 2.399963f + (int)current * .37f;
                bit.position = emissionPoint + new Vector3(Mathf.Sin(angle) * .006f, 0, Mathf.Cos(angle) * .006f);
                bit.velocity = new Vector3(Mathf.Sin(angle) * .04f, liquid ? -.025f : .045f + i % 3 * .012f, Mathf.Cos(angle) * .035f);
                bit.spin = new Vector3(97 + i * 11, 61 - i * 7, 135 - i * 9);
                float size = .0038f + i % 3 * .0012f;
                bit.scale = paper ? new Vector3(size * 2.4f, .001f, size * 1.6f) :
                    liquid ? new Vector3(size, size * 1.8f, size) : Vector3.one * size;
                bit.renderer.sharedMaterial = materials[MaterialIndex];
            }
        }

        private void AnimateBits(float elapsed)
        {
            if (!impact) return;
            float age = Mathf.Max(0, elapsed - Duration * ContactTime);
            float size = 1 - Mathf.Clamp01(age / (Duration * (1 - ContactTime)));
            foreach (var bit in bits)
            {
                if (!bit.transform.gameObject.activeSelf) continue;
                bit.transform.position = bit.position + bit.velocity * age + Vector3.down * (.48f * age * age);
                bit.transform.rotation = Quaternion.Euler(bit.spin * age);
                bit.transform.localScale = bit.scale * size;
            }
        }

        private void EnsureResources()
        {
            if (root) return;
            root = new GameObject("ItemUse_MatteProps_Runtime") { hideFlags = HideFlags.DontSave };
            // An unscaled scene root keeps inventory props at their original world size after detaching.
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var colors = new[] { new Color(.66f, .64f, .44f), new Color(.50f, .15f, .085f),
                new Color(.37f, .42f, .38f), new Color(.16f, .41f, .34f), new Color(.12f, .075f, .045f) };
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "ItemUse_MatteChip_" + i, hideFlags = HideFlags.DontSave };
                materials[i].SetColor("_BaseColor", colors[i]);
                materials[i].SetFloat("_Smoothness", .05f); materials[i].SetFloat("_Metallic", 0);
            }
            chipMesh = MakeChipMesh();
            for (int i = 0; i < bits.Length; i++)
            {
                var chip = new GameObject("MatteChip_" + i, typeof(MeshFilter), typeof(MeshRenderer));
                chip.transform.SetParent(root.transform, false);
                chip.GetComponent<MeshFilter>().sharedMesh = chipMesh;
                var renderer = chip.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
                renderer.sharedMaterial = materials[0];
                bits[i] = new Bit { transform = chip.transform, renderer = renderer };
                chip.SetActive(false);
            }
        }

        private static Mesh MakeChipMesh()
        {
            var mesh = new Mesh { name = "ItemUse_FlatChip", hideFlags = HideFlags.DontSave };
            var vertices = new Vector3[24];
            var triangles = new int[36];
            var axes = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            for (int face = 0; face < axes.Length; face++)
            {
                var n = axes[face];
                var u = Vector3.Cross(n, Mathf.Abs(n.y) > .5f ? Vector3.forward : Vector3.up);
                var v = Vector3.Cross(n, u);
                int start = face * 4, index = face * 6;
                vertices[start] = (n - u - v) * .5f; vertices[start + 1] = (n + u - v) * .5f;
                vertices[start + 2] = (n + u + v) * .5f; vertices[start + 3] = (n - u + v) * .5f;
                triangles[index] = start; triangles[index + 1] = start + 1; triangles[index + 2] = start + 2;
                triangles[index + 3] = start; triangles[index + 4] = start + 2; triangles[index + 5] = start + 3;
            }
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private void FinishGesture()
        {
            IsPlaying = false;
            if (prop) { prop.SetActive(false); Destroy(prop); prop = null; }
            foreach (var material in fadeCopies.Values) if (material) Destroy(material);
            fadeCopies.Clear();
            fading = false;
            foreach (var bit in bits) if (bit != null && bit.transform) bit.transform.gameObject.SetActive(false);
        }

        public void Cancel()
        {
            FinishGesture();
        }

        private void OnDisable() => Cancel();

        private void OnDestroy()
        {
            Unsubscribe();
            Cancel();
            if (root) Destroy(root);
            if (chipMesh) Destroy(chipMesh);
            foreach (var material in materials) if (material) Destroy(material);
        }
    }
}
