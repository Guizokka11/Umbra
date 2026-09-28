// Umbra — Sprite visto num espelho: só aparece dentro do retângulo do espelho (em coordenadas de mundo, X e Y),
// um pouco apagado e sem escrever profundidade. Usado no susto do espelho (ScareFlash, tipo Espelho):
// o reflexo da Luma e a Inspetora atrás dela no reflexo.
// _Espelho = (x mínimo, x máximo, y mínimo, y máximo) do vidro, preenchido pelo ScareFlash.
Shader "Umbra/Sprite Reflexo"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tinta", Color) = (0.75, 0.78, 0.85, 0.8)
        _Espelho ("Retângulo do espelho (xmin, xmax, ymin, ymax)", Vector) = (-1, 1, 0, 2)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Unlit"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _Espelho;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                float2 mundo      : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
            };

            Varyings vert (Attributes input)
            {
                Varyings o;
                float3 w = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(w);
                o.mundo = w.xy;
                o.uv = input.uv;
                o.color = input.color * _Color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                // Fora do vidro: não existe.
                clip(i.mundo.x - _Espelho.x);
                clip(_Espelho.y - i.mundo.x);
                clip(i.mundo.y - _Espelho.z);
                clip(_Espelho.w - i.mundo.y);
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                c.rgb = MixFog(c.rgb, i.fogFactor);
                return c;
            }
            ENDHLSL
        }
    }
}
