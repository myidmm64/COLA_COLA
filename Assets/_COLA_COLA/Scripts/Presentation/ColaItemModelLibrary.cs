using System;
using ColaCola.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColaCola.Presentation
{
    /// <summary>Shared original mesh assets and palette for both actors' physical inventory props.</summary>
    internal sealed class ColaItemModelLibrary : IDisposable
    {
        private static readonly string[] ModelNames =
        {
            "01_IceCup", "02_FizzCandy", "04_QueueTicket", "05_Bill",
            "06_SpicyOrder", "07_SafetyCap", "08_PizzaSlice", "09_HotSauce", "10_DoubleStamp",
            "11_BottleOpener", "12_Straw", "13_SodaWater", "14_CoinCup", "15_SwapTongs",
            "16_SaltPacket", "17_CoolingPack", "18_TokenPunch", "19_FreshLid", "20_ExchangeCoupon"
        };
        private readonly GameObject[] sources = new GameObject[ModelNames.Length];
        private readonly Material surface;

        public ColaItemModelLibrary()
        {
            var palette = Resources.Load<Texture2D>("ItemModels/PizzeriaItemPalette");
            if (!palette) throw new InvalidOperationException("COLA_COLA item palette is missing.");
            surface = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                name = "PizzeriaItems_SharedMattePalette", hideFlags = HideFlags.DontSave
            };
            surface.SetTexture("_BaseMap", palette);
            surface.SetColor("_BaseColor", Color.white);
            surface.SetFloat("_Smoothness", .16f);
            surface.SetFloat("_Metallic", .02f);
            ColaPropPatina.Configure(surface);
        }

        public GameObject Create(ColaItemId id, Transform parent)
        {
            int index = ColaItemCatalog.IndexOf(id);
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (!sources[index]) sources[index] = Resources.Load<GameObject>("ItemModels/" + ModelNames[index]);
            if (!sources[index]) throw new InvalidOperationException("COLA_COLA item model is missing: " + ModelNames[index]);
            return InstantiateModel(sources[index], parent, ModelNames[index] + "_Model");
        }

        public GameObject CreateBellPart(string name, Transform parent)
        {
            var source = Resources.Load<GameObject>("ItemModels/" + name);
            if (!source) throw new InvalidOperationException("COLA_COLA waiter bell model is missing: " + name);
            return InstantiateModel(source, parent, name);
        }

        private GameObject InstantiateModel(GameObject source, Transform parent, string name)
        {
            var model = UnityEngine.Object.Instantiate(source, parent, false);
            model.name = name;
            model.hideFlags = HideFlags.DontSave;
            model.transform.localPosition = Vector3.zero;
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = surface;
                renderer.sharedMaterials = materials.Length == 0 ? new[] { surface } : materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            // Only the owning slot receives input, so detail geometry never steals clicks.
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(collider);
            return model;
        }

        public static void FitHitTarget(Transform slot, BoxCollider target)
        {
            var bounds = new Bounds();
            bool initialized = false;
            foreach (var filter in slot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh) continue;
                var local = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var offset = Vector3.Scale(local.extents, new Vector3((corner & 1) == 0 ? -1 : 1,
                        (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    var point = slot.InverseTransformPoint(filter.transform.TransformPoint(local.center + offset));
                    if (!initialized) { bounds = new Bounds(point, Vector3.zero); initialized = true; }
                    else bounds.Encapsulate(point);
                }
            }
            var size = bounds.size + Vector3.one * .008f;
            size = new Vector3(Mathf.Max(.07f, size.x), Mathf.Max(.05f, size.y), Mathf.Max(.07f, size.z));
            target.size = size;
            target.center = new Vector3(bounds.center.x, bounds.min.y - .004f + size.y / 2, bounds.center.z);
        }

        public void Dispose() { if (surface) UnityEngine.Object.Destroy(surface); }
    }
}
