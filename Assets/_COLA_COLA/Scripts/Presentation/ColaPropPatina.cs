using System;
using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Clean photographic low-resolution materials; original palette, ink and lit visibility are preserved.</summary>
    public static class ColaPropPatina
    {
        public static bool Configure(Material material)
        {
            if (!material || !material.shader || Protected(material.name)) return false;
            if (material.shader.name != "Universal Render Pipeline/Lit" && material.shader.name != "COLA_COLA/PS1 Prop Patina") return false;
            var shader = Resources.Load<Shader>("COLA_COLA_PropPatina");
            if (!shader) return false;
            material.shader = shader;
            int kind = Kind(material.name);
            material.SetFloat("_PatinaKind", kind);
            BindWearMap(material);
            material.SetTexture("_SurfaceAtlas", Resources.Load<Texture2D>("COLA_COLA_SurfaceAtlas"));
            bool label = Has(material.name, "Printed_Label");
            bool cap = Has(material.name, "Red_Plastic_Cap");
            material.SetFloat("_PhotoStrength", label ? .25f : cap ? .35f : .95f);
            if (label || cap)
            {
                var print = Resources.Load<Texture2D>("COLA_COLA_BottlePrint");
                if (print) { material.SetTexture("_BaseMap", print); material.SetTexture("_MainTex", print); }
            }
            material.SetFloat("_ItemPalette", Has(material.name, "SharedMattePalette") ? 1 : 0);
            bool bottle = label || cap || Has(material.name, "PET_Plastic") || Has(material.name, "Cola_Liquid");
            material.SetFloat("_SurfaceMinAlbedo", Has(material.name, "Cola_Liquid") ? .025f : 0);
            material.SetFloat("_PatinaDensity", bottle ? 384 : Has(material.name, "SharedMattePalette") ? 512 : 256);
            material.SetFloat("_PatinaStrength", kind == 2 ? .48f : kind == 3 ? .70f : kind == 5 ? .40f : .72f);
            material.SetFloat("_SpecularHighlights", 0);
            material.SetFloat("_EnvironmentReflections", 0);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            return true;
        }
        public static void BindWearMap(Material material)
        {
            if (!material || !material.HasProperty("_PatinaMap")) return;
            var texture = Resources.Load<Texture2D>("COLA_COLA_PropWear");
            if (texture) material.SetTexture("_PatinaMap", texture);
        }
        private static bool Has(string name, string part) => name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        private static bool Protected(string name) => Has(name, "CRT_") || Has(name, "Marking") || Has(name, "Readout") ||
            Has(name, "Needle") || Has(name, "Forecast") || Has(name, "Digit") || Has(name, "Ink") ||
            Has(name, "Phosphor") || Has(name, "Glow") || Has(name, "Fluorescent") || Has(name, "EmergencyRed") ||
            Has(name, "Console_Charcoal");
        private static int Kind(string name)
        {
            if (Has(name, "Printed_Label")) return 2;
            if (Has(name, "PET_Plastic") || Has(name, "Cola_Liquid") || Has(name, "Red_Plastic_Cap")) return 4;
            if (Has(name, "Wood") || Has(name, "DeliveryCrate_DarkRed")) return 3;
            if (Has(name, "Paper") || Has(name, "Liner") || Has(name, "Dial_Cream")) return 2;
            if (Has(name, "Steel") || Has(name, "Brass") || Has(name, "Metal")) return 1;
            if (Has(name, "Rubber") || Has(name, "Charcoal")) return 5;
            if (Has(name, "ColdBlue") || Has(name, "Amber")) return 4;
            return 0;
        }
    }
}
