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
            CBUFFER_END

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
                float dot = 1.0 - smoothstep(_DotRadius, _DotRadius + _DotSoftness, dist);

                half4 col = _Color;
                col.a *= dot * _Alpha;
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
