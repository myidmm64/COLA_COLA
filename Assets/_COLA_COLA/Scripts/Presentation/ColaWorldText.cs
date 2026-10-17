using UnityEngine;
using UnityEngine.Rendering;

namespace ColaCola.Presentation
{
    /// <summary>World lettering respects opaque geometry and follows dynamic font atlas rebuilds.</summary>
    [DisallowMultipleComponent]
    public sealed class ColaWorldText : MonoBehaviour
    {
        private Font font;
        private Material material;

        public static void Bind(TextMesh text)
        {
            var lettering = text.GetComponent<ColaWorldText>();
            if (!lettering) lettering = text.gameObject.AddComponent<ColaWorldText>();
            lettering.font = text.font;
            if (!lettering.material)
                lettering.material = new Material(Shader.Find("COLA_COLA/WorldText"))
                { name = "DepthTested_" + text.font.name, hideFlags = HideFlags.DontSave };
            lettering.RefreshAtlas();
            var renderer = text.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = lettering.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void OnEnable() => Font.textureRebuilt += OnAtlasRebuilt;
        private void OnDisable() => Font.textureRebuilt -= OnAtlasRebuilt;
        private void OnAtlasRebuilt(Font rebuilt) { if (rebuilt == font) RefreshAtlas(); }
        private void LateUpdate() => RefreshAtlas();
        private void RefreshAtlas()
        {
            if (font && material && material.mainTexture != font.material.mainTexture)
                material.mainTexture = font.material.mainTexture;
        }
        private void OnDestroy() { if (material) Destroy(material); }
    }
}
