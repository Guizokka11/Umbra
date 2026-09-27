// Umbra — Fundo 3D: a pintura do cenário projetada em chão e paredes de verdade.
// Desenhado primeiro. Escreve profundidade (padrão): uma parede mais perto cobre a de trás
// (ex.: bloco das portas do Corredor 1 na frente do nicho) e cobre quem passa atrás dela.
// _ZWrite = 0: não escreve (usado nos degraus da escada 3D, para os degraus não cortarem os pés da Luma).
Shader "Umbra/Fundo Projetado"
{
    Properties
    {
        _MainTex ("Pintura", 2D) = "white" {}
        _Color ("Tinta", Color) = (1,1,1,1)
        [Enum(Off,0,On,1)] _ZWrite ("Escreve profundidade", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Background+10" "IgnoreProjector"="True" }
        Cull Off
        ZWrite [_ZWrite]
        ZTest LEqual

        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fogFactor : TEXCOORD1; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color;
                // Fora da pintura: escuro (o cômodo "continua" na escuridão em vez de esticar a borda).
                float2 o = max(max(-i.uv, i.uv - 1.0), 0.0);
                c.rgb *= saturate(1.0 - max(o.x, o.y) * 400.0);
                c.rgb = MixFog(c.rgb, i.fogFactor);
                return half4(c.rgb, 1);
            }
            ENDHLSL
        }
    }
}
