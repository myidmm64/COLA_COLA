Shader "COLA_COLA/Interaction Silhouette Mask"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ColorMask 0
            ZWrite Off
            // Cover the complete interior even where raised digits or an opaque label sit in front.
            // The colored outline still depth-tests normally, so this does not reveal hidden objects.
            ZTest Always
            // Reserve only one user stencil bit; leave the pipeline's other bits untouched.
            Stencil
            {
                Ref 1
                ReadMask 1
                WriteMask 1
                Comp Always
                Pass Replace
            }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
