Shader "COLA_COLA/Interaction Outline"
{
    Properties
    {
        _OutlineColor("Outline color", Color) = (0.86, 0.74, 0.13, 0.92)
        _OutlinePixels("Outline width in pixels", Float) = 2.5
        [HideInInspector] _OutlineDirection("Screen offset direction", Vector) = (1,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Geometry+20" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            // The earlier silhouette mask removes the interior, including transparent bottle surfaces.
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            Stencil
            {
                Ref 1
                ReadMask 1
                WriteMask 0
                Comp NotEqual
                Pass Keep
            }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlinePixels;
                float4 _OutlineDirection;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float4 clip = TransformObjectToHClip(input.positionOS.xyz);
                // Translate the complete silhouette, never fan individual bottom vertices outwards.
                // Eight neighboring copies plus the shared interior mask form a bounded, rounded border.
                clip.xy += _OutlineDirection.xy * (_OutlinePixels * 2 / _ScreenParams.xy) * clip.w;
                output.positionCS = clip;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}
