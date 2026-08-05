Shader "FruitMerge/SpriteDashFlow"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _FlowSpeed ("Flow Speed", Float) = 1.5
        _DotSpacing ("Dot Spacing (world units)", Float) = 0.35
        _DotRadius ("Dot Radius (world units)", Float) = 0.045
        _DotSoftness ("Dot Edge Softness", Float) = 0.015
        _Alpha ("Overall Alpha", Range(0,1)) = 0.4

        [Toggle] _RainbowMode ("Rainbow Mode", Float) = 0
        _ColorRunLength ("Dots Per Color", Float) = 2
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Paletteki en fazla renk sayısı. FruitDatabase 11 tier taşıyor (kiraz..karpuz) —
            // 16 bolca pay bırakıyor. DropIndicatorController bunu aşan bir sayı göndermez.
            #define MAX_PALETTE_COLORS 16

            struct Attributes
            {
                float3 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 localPos   : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _FlowSpeed;
                float _DotSpacing;
                float _DotRadius;
                float _DotSoftness;
                float _Alpha;
                float _RainbowMode;
                float _ColorRunLength;
                float _PaletteCount;
                float4 _PaletteColors[MAX_PALETTE_COLORS];
            CBUFFER_END

            // Dinamik dizin yerine sabit-boyutlu unroll'lanmış döngü: bazı eski mobil
            // GPU'larda (GLES) uniform dizilere dinamik indeksleme sorun çıkarabiliyor,
            // bu yol her platformda güvenli.
            half4 PaletteAt(int slot)
            {
                half4 result = half4(1.0, 1.0, 1.0, 1.0);

                [unroll]
                for (int k = 0; k < MAX_PALETTE_COLORS; k++)
                {
                    if (k == slot) result = _PaletteColors[k];
                }

                return result;
            }

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.localPos = v.positionOS.xy;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Doku kullanmadan prosedurel nokta deseni: atlas'a bagli degil,
                // her zaman kendi sprite'imiza ait, spacing/radius/alpha tamamen ayarlanabilir.
                float period = max(_DotSpacing, 0.0001);
                float phase = i.localPos.y / period + _Time.y * _FlowSpeed;
                float cellOffsetY = (frac(phase) - 0.5) * period;

                float dist = length(float2(i.localPos.x, cellOffsetY));
                float dotMask = 1.0 - smoothstep(_DotRadius, _DotRadius + _DotSoftness, dist);

                half4 col = _Color;

                if (_RainbowMode > 0.5)
                {
                    // phase'in tam kısmı = kaçıncı nokta hücresi. _FlowSpeed zaten phase'i
                    // zamanla kaydırıyor, yani renkler de noktalarla AYNI yönde/hızda akıyor —
                    // ayrı bir zaman hesabı gerekmiyor.
                    float dotIndex = floor(phase);
                    float run = max(round(_ColorRunLength), 1.0);
                    float count = max(round(_PaletteCount), 1.0);

                    float slot = fmod(floor(dotIndex / run), count);
                    if (slot < 0.0) slot += count;   // fmod negatifte işaret koruyor

                    col.rgb = PaletteAt((int)slot).rgb;
                }

                col.a *= dotMask * _Alpha;
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
