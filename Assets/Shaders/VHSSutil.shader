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
                // Una interferencia corta por ventana de 8 s, con inicio variable.
                // El fundido evita cortes; la banda no cruza la zona central de visión.
                float ventana = floor(tiempo / 8.0);
                float semilla = Ruido(float2(ventana, 13.0));
                float inicio = 1.0 + 3.0 * semilla;
                float duracion = lerp(0.18, 0.32, Ruido(float2(ventana, 41.0)));
                float fase = saturate((tiempo - ventana * 8.0 - inicio) / duracion);
                float pulso = sin(fase * 3.14159265);
                pulso *= pulso;
                float posicion = lerp(0.12, 0.27, Ruido(float2(ventana, 71.0)));
                posicion = semilla < 0.5 ? posicion : 1.0 - posicion;
                posicion += (fase - 0.5) * 0.04;
                float banda = 1.0 - smoothstep(0.008, 0.03, abs(uv.y - posicion));
                float interferencia = banda * pulso * fuerza;
                // Desplazamiento subpíxel suave: no hay golpes ni sacudidas.
                float deriva = sin(uv.y * 21.0 + tiempo * 0.65) *
                    sin(tiempo * 0.43) * 0.6 * fuerza * borde;
                float tracking = sin(uv.y * 150.0 + tiempo * 18.0) * 2.5 * interferencia;
                uv.x += (deriva + tracking) * pixel.x;
                half4 base = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);
                float separacion = (0.65 * fuerza * borde + interferencia) * pixel.x;
                half3 color = base.rgb;
                color.r = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + float2(separacion, 0), 0).r;
                color.b = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv - float2(separacion, 0), 0).b;
                float luminancia = dot(color, float3(0.2126, 0.7152, 0.0722));
                color = lerp(color, luminancia.xxx, 0.08 * fuerza);
                float grano = Ruido(floor(uv / pixel) + floor(tiempo * 24.0) * float2(17.0, 31.0)) - 0.5;
                float sombras = lerp(0.2, 1.0, smoothstep(0.01, 0.25, luminancia));
                color += grano * (0.04 * fuerza + 0.12 * interferencia) * sombras;
                float lineas = sin(uv.y / pixel.y * 3.14159265);
                color *= 1.0 - (0.5 + 0.5 * lineas) * 0.018 * fuerza;
                color *= 1.0 - 0.035 * interferencia;
                return half4(max(color, 0.0), base.a);
            }
            ENDHLSL
        }
    }
}
