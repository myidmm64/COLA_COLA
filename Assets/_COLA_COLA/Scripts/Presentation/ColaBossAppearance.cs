using System.Collections.Generic;
using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Stage names and cleanup for the retired costume system. All stages use the original boss model.</summary>
    [DisallowMultipleComponent]
    public sealed class ColaBossAppearance : MonoBehaviour
    {
        private static readonly string[] Names = { "단골 손님", "배달원", "주방장", "지배인", "사장" };
        public static string NameFor(int level) => ColaCola.Core.ColaLocalization.Get("boss." + Mathf.Clamp(level, 1, Names.Length), Names[Mathf.Clamp(level - 1, 0, Names.Length - 1)]);
        public int PartCount => 0;

        // Keep cleanup for an existing component when returning from an older live session.
        private readonly Dictionary<Renderer, Material[]> originals = new();
        private readonly List<GameObject> parts = new();
        private readonly List<Material> materials = new();
        private readonly List<Mesh> meshes = new();

        public static void EnsureFor(ColaDuelController match)
        {
            if (!match) return;
            var appearance = match.GetComponent<ColaBossAppearance>();
            if (!appearance) return;
            appearance.RestoreOriginal();
            appearance.enabled = false;
        }

        private void RestoreOriginal()
        {
            StopAllCoroutines();
            foreach (var pair in originals) if (pair.Key) pair.Key.sharedMaterials = pair.Value;
            foreach (var part in parts) if (part) { part.SetActive(false); Destroy(part); }
            foreach (var material in materials) if (material) Destroy(material);
            foreach (var mesh in meshes) if (mesh) Destroy(mesh);
            originals.Clear(); parts.Clear(); materials.Clear(); meshes.Clear();
        }

        private void OnDisable() => RestoreOriginal();
        private void OnDestroy() => RestoreOriginal();
    }
}
