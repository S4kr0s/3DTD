// The Bullet Dispenser's ricochet dome, shown while the tower is selected: an additive neon shimmer with a
// bright rim, a latitude/longitude grid and a band sweeping upwards. _Intensity fades it (set per tower through
// a MaterialPropertyBlock). The mesh is the targetter's half sphere: object space y is up, radius 1.
Shader "3DTD/Ricochet Dome"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.3, 1, 0.4, 1)
        _Intensity ("Intensity", Range(0, 1)) = 1
        _GridLines ("Grid lines (latitude, longitude)", Vector) = (7, 18, 0, 0)
        _SweepSpeed ("Sweep speed", Float) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Unlit"
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                float4 _GridLines;
                float _SweepSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewWS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = GetWorldSpaceViewDir(position.positionWS);
                return output;
            }

            // 1 on a line every 1 / count of t, fading over about a pixel
            float Lines(float t, float count)
            {
                float f = abs(frac(t * count) - 0.5) * 2;
                float width = fwidth(t * count) * 1.5;
                return smoothstep(1 - width - 0.04, 1, f);
            }

            half4 Frag(Varyings input, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 normal = normalize(input.normalWS) * (front ? 1 : -1);
                float facing = saturate(abs(dot(normal, normalize(input.viewWS))));
                float rim = pow(1 - facing, 3);

                float3 direction = normalize(input.positionOS);
                float latitude = asin(clamp(direction.y, -1, 1)) / HALF_PI;     // 0 at the base, 1 at the top
                float longitude = atan2(direction.z, direction.x) / TWO_PI + 0.5;
                float grid = max(Lines(latitude, _GridLines.x), Lines(longitude, _GridLines.y)) * (1 - latitude * 0.6);

                float sweepAt = frac(_Time.y * _SweepSpeed);
                float sweep = exp(-abs(latitude - sweepAt) * 18) * (1 - sweepAt);

                half glow = 0.025 + rim * 0.4 + grid * 0.12 + sweep * 0.3;
                return half4(_Color.rgb * glow * _Intensity, 1);
            }
            ENDHLSL
        }
    }
}
