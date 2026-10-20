Shader "COLA_COLA/PS1 Prop Patina"
{
    Properties
    {
        [MainTexture] _BaseMap("Original palette / albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Original color", Color) = (1,1,1,1)
        [NoScaleOffset] _PatinaMap("PS1 wear texture", 2D) = "gray" {}
        [NoScaleOffset] _SurfaceAtlas("Photographic material atlas (4 x 2)", 2D) = "white" {}
        _PhotoStrength("Photographic surface", Range(0,1)) = 0
        [Toggle] _ItemPalette("Classify item palette materials", Float) = 0
        _SurfaceMinAlbedo("Dark bottle detail visibility", Range(0,.05)) = 0
        _PatinaStrength("Surface wear", Range(0,1)) = .72
        _PatinaKind("0 Enamel 1 Metal 2 Paper 3 Wood 4 Plastic 5 Rubber 6 Cloth", Float) = 0
        _PatinaDensity("Surface texels per meter", Float) = 256
        _Smoothness("Smoothness", Range(0,1)) = .12
        _Metallic("Metallic", Range(0,1)) = 0
        _SpecColor("Specular", Color) = (.2,.2,.2,1)
        _MetallicGlossMap("Metallic map", 2D) = "white" {}
        _SpecGlossMap("Specular map", 2D) = "white" {}
        [Normal] _BumpMap("Normal", 2D) = "bump" {}
        _BumpScale("Normal scale", Float) = 1
        _OcclusionMap("Occlusion", 2D) = "white" {}
        _OcclusionStrength("Occlusion strength", Range(0,1)) = 1
        _EmissionMap("Emission", 2D) = "white" {}
        [HDR] _EmissionColor("Emission color", Color) = (0,0,0,0)
        _Cutoff("Cutoff", Range(0,1)) = .5
        [HideInInspector] _WorkflowMode("Workflow", Float) = 1
        [HideInInspector] _SmoothnessTextureChannel("Smoothness channel", Float) = 0
        [HideInInspector] _Parallax("Parallax", Float) = .005
        [HideInInspector] _ParallaxMap("Parallax map", 2D) = "black" {}
        [HideInInspector] _DetailMask("Detail mask", 2D) = "white" {}
        [HideInInspector] _DetailAlbedoMap("Detail", 2D) = "linearGrey" {}
        [HideInInspector] _DetailAlbedoMapScale("Detail scale", Float) = 1
        [HideInInspector] _DetailNormalMap("Detail normal", 2D) = "bump" {}
        [HideInInspector] _DetailNormalMapScale("Detail normal scale", Float) = 1
        [HideInInspector] _ClearCoatMask("Coat", Float) = 0
        [HideInInspector] _ClearCoatSmoothness("Coat smoothness", Float) = 0
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _Blend("Blend", Float) = 0
        [HideInInspector] _SrcBlend("Source", Float) = 1
        [HideInInspector] _DstBlend("Destination", Float) = 0
        [HideInInspector] _SrcBlendAlpha("Source alpha", Float) = 1
        [HideInInspector] _DstBlendAlpha("Destination alpha", Float) = 0
        [HideInInspector] _ZWrite("Depth write", Float) = 1
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _AlphaClip("Alpha clip", Float) = 0
        [HideInInspector] _AlphaToMask("Alpha mask", Float) = 0
        [HideInInspector] _BlendModePreserveSpecular("Preserve specular", Float) = 1
        [HideInInspector] _ReceiveShadows("Receive shadows", Float) = 1
        [HideInInspector] _SpecularHighlights("Specular", Float) = 0
        [HideInInspector] _EnvironmentReflections("Reflections", Float) = 0
        [HideInInspector] _QueueOffset("Queue", Float) = 0
        [HideInInspector] _AddPrecomputedVelocity("Velocity", Float) = 0
        [HideInInspector] _XRMotionVectorsPass("XR vectors", Float) = 1
        [HideInInspector] _MainTex("Legacy texture", 2D) = "white" {}
        [HideInInspector] _Color("Legacy color", Color) = (1,1,1,1)
        [HideInInspector][NoScaleOffset] unity_Lightmaps("Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd("Directional lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks("Shadow masks", 2DArray) = "" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Lit" }
        Pass
        {
            Name "ForwardPatina"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
            ZWrite [_ZWrite] Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LitPassVertex
            #pragma fragment PatinaFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON _ALPHAMODULATE_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            #include "COLA_COLA_PropPatina.hlsl"
            float _PatinaKind, _PatinaStrength, _PatinaDensity, _PhotoStrength, _ItemPalette, _SurfaceMinAlbedo;
            half4 PatinaFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SurfaceData surface;
                InitializeStandardLitSurfaceData(input.uv, surface);
                InputData lighting;
                InitializeInputData(input, surface.normalTS, lighting);
                float2 meters = ColaPatinaSurfaceMeters(input.positionWS, lighting.normalWS);
                if (_PhotoStrength > 0)
                {
                    float palette = ColaSurfacePaletteIndex(input.uv);
                    float kind = _ItemPalette > .5 ? ColaSurfacePaletteKind(palette) : _PatinaKind;
                    // True black graphic shapes use their original palette color.
                    float strength = _ItemPalette > .5 && palette < .5 ? 0 : _PhotoStrength;
                    surface.albedo = max(surface.albedo, _SurfaceMinAlbedo * half3(.80,.72,.58));
                    surface.albedo = ColaPhotographicSurface(surface.albedo, meters, kind, strength, _PatinaDensity);
                }
                else surface.albedo = ColaPatinaColor(surface.albedo, meters, _PatinaKind, _PatinaStrength, _PatinaDensity);
                surface.smoothness = min(surface.smoothness, .18h);
                surface.metallic = min(surface.metallic, .12h);
                InitializeBakedGIData(input, lighting);
                half4 color = UniversalFragmentPBR(lighting, surface);
                color.rgb = MixFog(color.rgb, lighting.fogCoord);
                color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent());
                return color;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
