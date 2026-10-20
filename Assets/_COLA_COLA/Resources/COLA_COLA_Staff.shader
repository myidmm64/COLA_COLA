Shader "COLA_COLA/Background Staff"
{
    Properties
    {
        _BaseMap("Original character texture", 2D) = "white" {}
        _BaseColor("Uniform tint", Color) = (0.68,0.74,0.64,1)
        _Visibility("Visibility", Range(0,1)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Culling", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _Visibility;
                float _Cull;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; float2 uv : TEXCOORD1; half fog : TEXCOORD2; float3 positionWS : TEXCOORD3; half3 vertexLight : TEXCOORD4; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.vertexLight = VertexLighting(output.positionWS, output.normalWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Screen-stable dissolve at the dark room edges, with no transparent depth blocking or per-frame noise.
                float threshold = frac(52.9829189 * frac(dot(floor(input.positionCS.xy), float2(.06711056,.00583715))));
                clip(_Visibility - threshold - .00001);
                half3 normal = normalize(input.normalWS);
                half3 illumination = SampleSH(normal) + half3(.025,.028,.024);
                Light mainLight = GetMainLight();
                illumination += mainLight.color * mainLight.distanceAttenuation * saturate(dot(normal, mainLight.direction));
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    illumination += input.vertexLight;
                #endif
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    uint lightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(lightCount)
                        Light roomLight = GetAdditionalLight(lightIndex, input.positionWS);
                        illumination += roomLight.color * roomLight.distanceAttenuation * saturate(dot(normal, roomLight.direction));
                    LIGHT_LOOP_END
                #endif
                half3 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb * min(illumination, 1.6h);
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }
    }
}
