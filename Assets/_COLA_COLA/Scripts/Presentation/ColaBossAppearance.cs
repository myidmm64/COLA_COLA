using System.Collections.Generic;
using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Stage identity using the original boss atlas and the room's existing accent fixtures.</summary>
    [DisallowMultipleComponent]
    public sealed class ColaBossAppearance : MonoBehaviour
    {
        private static readonly string[] Names = { "단골 손님", "배달원", "주방장", "지배인", "사장" };
        private static readonly Color[] Shirts = {
            Color.white, new(.46f, .69f, .83f), new(.77f, .68f, .40f),
            new(.82f, .39f, .36f), new(.56f, .49f, .70f)
        };
        private static readonly Color[] Soda = {
            new(.20f, .66f, .57f), new(.20f, .53f, .78f), new(.39f, .57f, .42f),
            new(.36f, .48f, .47f), new(.28f, .42f, .76f)
        };
        private static readonly Color[] Kitchen = {
            new(.40f, .70f, .51f), new(.39f, .64f, .69f), new(.78f, .65f, .36f),
            new(.55f, .62f, .43f), new(.60f, .61f, .70f)
        };
        private static readonly Color[] Rim = {
            new(1f, .12f, .055f), new(.72f, .25f, .13f), new(.90f, .34f, .10f),
            new(.92f, .12f, .15f), new(.57f, .26f, .80f)
        };
        public static string NameFor(int level) => ColaCola.Core.ColaLocalization.Get("boss." + Mathf.Clamp(level, 1, Names.Length), Names[Mathf.Clamp(level - 1, 0, Names.Length - 1)]);
        public int PartCount => 0;
        public int AppliedStage { get; private set; }
        private readonly Dictionary<Renderer, Material[]> originals = new();
        private readonly List<Material> materials = new();
        private ColaDuelController owner;

        public static void EnsureFor(ColaDuelController match)
        {
            if (!match || !Application.IsPlaying(match.gameObject)) return;
            var appearance = match.GetComponent<ColaBossAppearance>();
            if (!appearance) appearance = match.gameObject.AddComponent<ColaBossAppearance>();
            appearance.enabled = true;
            appearance.Apply(match);
        }

        private void Apply(ColaDuelController match)
        {
            RestoreMaterials();
            owner = match;
            int index = Mathf.Clamp(match.Level - 1, 0, 4);
            AppliedStage = index + 1;
            var actor = match.interaction && match.interaction.shakeAnimator ? match.interaction.shakeAnimator.bossAnimator : null;
            var shader = Resources.Load<Shader>("COLA_COLA_Staff");
            if (actor && shader)
            {
                foreach (var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    // Face and glasses remain on their original materials.
                    if (renderer.name != "body" && renderer.name != "arms") continue;
                    var slots = renderer.sharedMaterials;
                    originals[renderer] = (Material[])slots.Clone();
                    for (int i = 0; i < slots.Length; i++)
                    {
                        var source = slots[i];
                        if (!source) continue;
                        var material = new Material(shader) { name = "Stage " + AppliedStage + " / Cotton", hideFlags = HideFlags.DontSave };
                        string map = source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                        if (source.HasProperty(map))
                        {
                            material.SetTexture("_BaseMap", source.GetTexture(map));
                            material.SetTextureScale("_BaseMap", source.GetTextureScale(map));
                            material.SetTextureOffset("_BaseMap", source.GetTextureOffset(map));
                        }
                        material.SetColor("_BaseColor", source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : Color.white);
                        material.SetColor("_ShirtTint", Shirts[index]);
                        material.SetFloat("_ShirtTintStrength", 1);
                        material.SetFloat("_Visibility", 1);
                        if (source.HasProperty("_Cull")) material.SetFloat("_Cull", source.GetFloat("_Cull"));
                        materials.Add(material);
                        slots[i] = material;
                    }
                    renderer.sharedMaterials = slots;
                }
            }
            ApplyRoom(index);
        }

        private void ApplyRoom(int index)
        {
            // Front key/fill and device lights retain the original readability values.
            // Only the existing background and rim fixtures change with stage identity.
            SetLamp("Soda_Cool_Fill", Soda[index], .95f);
            SetLamp("Kitchen_Warm", Kitchen[index], .50f);
            SetLamp("Horror_Kitchen_Fluorescent", Kitchen[index], index == 4 ? .75f : .85f);
            SetLamp("Horror_Boss_RedRim", Rim[index], index == 3 ? 2.5f : 2.1f);
            SetLamp("Horror_Emergency_WallGlow", Rim[index], index == 3 ? .80f : .65f);
        }

        private void SetLamp(string name, Color color, float intensity)
        {
            if (!owner) return;
            var fixture = owner.transform.root.Find("06_Lighting/" + name);
            if (!fixture || !fixture.TryGetComponent<Light>(out var light)) return;
            light.color = color;
            light.intensity = intensity;
            if (fixture.TryGetComponent<HorrorPracticalFlicker>(out var flicker)) flicker.baseIntensity = intensity;
        }

        private void RestoreMaterials()
        {
            foreach (var pair in originals) if (pair.Key) pair.Key.sharedMaterials = pair.Value;
            foreach (var material in materials)
                if (material) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
            originals.Clear(); materials.Clear(); AppliedStage = 0;
        }

        private void OnDisable()
        {
            RestoreMaterials();
            if (owner) TabletopReadability.ApplyLighting(owner.transform.root);
        }
        private void OnDestroy() => RestoreMaterials();
    }
}
