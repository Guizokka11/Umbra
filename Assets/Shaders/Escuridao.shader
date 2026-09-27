// Umbra: véu de escuridão na frente da cena, com "buracos" suaves onde há luz acesa.
// Usado pelo DarknessOverlay (preso na câmera). Estilo Little Nightmares: o escuro engole o cenário
// e a própria Luma; perto das lâmpadas tudo reaparece.
Shader "Umbra/Escuridao"
{
    Properties
    {
        _Color ("Cor do escuro", Color) = (0.015, 0.01, 0.025, 1)
        _Darkness ("Quanto escurece", Range(0, 1)) = 0.7
        _Soft ("Suavidade da borda da luz", Range(0.05, 1)) = 0.65
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+200" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _Darkness;
            float _Soft;
            CBUFFER_END
            float4 _Lights[16];   // xy = posição na tela (0..1), z = raio (em altura de tela), w = força
            float _LightCount;
            float _Aspect;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 screen : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.screen = ComputeScreenPos(o.positionCS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 uv = i.screen.xy / i.screen.w;
                float lit = 0;
                [loop] for (int k = 0; k < 16; k++)
                {
                    if (k >= (int)_LightCount) break;
                    float2 d = uv - _Lights[k].xy;
                    d.x *= _Aspect;
                    float r = max(_Lights[k].z, 0.001);
                    float l = 1 - smoothstep(r * (1 - _Soft), r, length(d));
                    lit = max(lit, l * _Lights[k].w);
                }
                float a = _Darkness * (1 - saturate(lit));
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
}
