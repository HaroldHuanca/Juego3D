Shader "JuegoTerror/VHS Sutil"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "VHS Sutil"
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 _VHSParams; // intensidad, tiempo, tamaño de píxel x/y

            float Ruido(float2 p)
            {
                float3 v = frac(float3(p.xyx) * 0.1031);
                v += dot(v, v.yzx + 33.33);
                return frac((v.x + v.y) * v.z);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float fuerza = saturate(_VHSParams.x);
                float tiempo = _VHSParams.y;
                float2 pixel = _VHSParams.zw;
                float borde = lerp(0.2, 1.0, smoothstep(0.05, 0.7, length(uv - 0.5)));
                // Desplazamiento subpíxel suave: no hay golpes ni sacudidas.
                float deriva = sin(uv.y * 21.0 + tiempo * 0.65) *
                    sin(tiempo * 0.43) * 0.6 * fuerza * borde;
                uv.x += deriva * pixel.x;
                half4 base = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);
                float separacion = 0.65 * fuerza * borde * pixel.x;
                half3 color = base.rgb;
                color.r = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + float2(separacion, 0), 0).r;
                color.b = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv - float2(separacion, 0), 0).b;
                float luminancia = dot(color, float3(0.2126, 0.7152, 0.0722));
                color = lerp(color, luminancia.xxx, 0.08 * fuerza);
                float grano = Ruido(floor(uv / pixel) + floor(tiempo * 24.0) * float2(17.0, 31.0)) - 0.5;
                float sombras = lerp(0.2, 1.0, smoothstep(0.01, 0.25, luminancia));
                color += grano * 0.018 * fuerza * sombras;
                float lineas = sin(uv.y / pixel.y * 3.14159265);
                color *= 1.0 - (0.5 + 0.5 * lineas) * 0.008 * fuerza;
                return half4(max(color, 0.0), base.a);
            }
            ENDHLSL
        }
    }
}
