Shader "COLA_COLA/Retro Screen"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 _SignalStyle, _NativePixel, _LensStyle;
            float _SignalTime, _GlitchStrength;
            float Hash(float x) { return frac(sin(x * 127.1) * 43758.5453); }
            half3 ReadColor(float2 uv) { return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, saturate(uv), 0).rgb; }
            half3 Highlight(half3 color)
            {
                half luma = dot(color, half3(.2126,.7152,.0722));
                return color * smoothstep(.48h, .88h, luma);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 screenUV = input.texcoord;
                float2 centered = screenUV - .5;
                // A fixed, very small lens curvature. Keep the corners inside the original image.
                float2 uv = .5 + centered * (1 + _LensStyle.x * (dot(centered, centered) - .5));
                half4 color = half4(ReadColor(uv), 1);

                // Share the same four small lens taps for restrained highlight halation and peripheral softness.
                float2 radius = _NativePixel.xy * 1.6;
                half3 left = ReadColor(uv - float2(radius.x,0));
                half3 right = ReadColor(uv + float2(radius.x,0));
                half3 above = ReadColor(uv + float2(0,radius.y));
                half3 below = ReadColor(uv - float2(0,radius.y));
                float edge = smoothstep(.30, .65, length((screenUV - .5) * float2(1,.85)));
                color.rgb = lerp(color.rgb, (left + right + above + below) * .25h, edge * _SignalStyle.z);
                color.rgb += (Highlight(left) + Highlight(right) + Highlight(above) + Highlight(below)) * (.25 * _SignalStyle.y);

                // Short horizontal highlight diffusion, never a whole-image bloom or a flashing overlay.
                half3 streak = (Highlight(ReadColor(uv + float2(_NativePixel.x * 7, 0))) +
                    Highlight(ReadColor(uv - float2(_NativePixel.x * 7, 0)))) * .35h;
                streak += (Highlight(ReadColor(uv + float2(_NativePixel.x * 18, 0))) +
                    Highlight(ReadColor(uv - float2(_NativePixel.x * 18, 0)))) * .15h;
                color.rgb += streak * _LensStyle.y;
                // Only the short result hold gently narrows attention; no camera translation or FOV changes.
                color.rgb *= 1 - edge * _LensStyle.z * _LensStyle.w;

                if (_GlitchStrength > .001)
                {
                    // One thin, stable band; protect the central decision area during ordinary idle noise.
                    float tick = floor(_SignalTime * 12);
                    float tearY = Hash(tick + 41);
                    float band = 1 - smoothstep(.002, .007, abs(uv.y - tearY));
                    float peripheral = smoothstep(.18, .48, abs(uv.x - .5));
                    float mask = band * lerp(peripheral, 1, smoothstep(.16,.30,_GlitchStrength));
                    float strength = mask * _GlitchStrength;
                    float displacement = (Hash(tick + 19) - .5) * 5 * _NativePixel.x;
                    half3 torn = ReadColor(uv + float2(displacement, 0));
                    // A native-resolution blend, with no spatial quantization.
                    color.rgb = lerp(color.rgb, torn, strength * .65);
                }
                return color;
            }
            ENDHLSL
        }
    }
}
