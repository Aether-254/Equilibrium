Shader "Custom/GridShader"
{
    Properties
    {
        _GridColor ("Grid Color", Color) = (1, 1, 1, 0.2)
        _GridSize ("Grid Size", Float) = 1.0
        _LineWidth ("Line Width", Float) = 0.02
        _GridOffset ("Grid Offset", Vector) = (0.5, 0.5, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            float4 _GridColor;
            float _GridSize;
            float _LineWidth;
            float4 _GridOffset;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 worldPos = IN.positionWS.xy / _GridSize + _GridOffset.xy;
                float2 distanceToLine = abs(frac(worldPos) - 0.5);
                float2 antialias = max(fwidth(worldPos), 0.00001);
                float2 line = 1.0 - smoothstep(
                    _LineWidth,
                    _LineWidth + antialias,
                    distanceToLine
                );
                float alpha = max(line.x, line.y);

                return half4(_GridColor.rgb, _GridColor.a * alpha);
            }
            ENDHLSL
        }
    }
}
