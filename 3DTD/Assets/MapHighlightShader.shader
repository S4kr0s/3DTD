Shader "Custom/MapHighlightShader" {
    Properties{
        _Color("Color", Color) = (1,1,1,1)
        _MainTex("Texture", 2D) = "white" {}
    }

        SubShader{
            Tags { "RenderType" = "Opaque" }
            LOD 100

            Pass {
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #include "UnityCG.cginc"

                struct appdata {
                    float4 vertex : POSITION;
                    float2 uv : TEXCOORD0;
                };

                struct v2f {
                    float2 uv : TEXCOORD0;
                    float4 vertex : SV_POSITION;
                };

                sampler2D _MainTex;
                float4 _MainTex_ST;
                float4 _Color;
                float4x4 unity_ObjectToWorld;
                float4x4 unity_WorldToObject;

                v2f vert(appdata v) {
                    v2f o;
                    o.vertex = UnityObjectToClipPos(v.vertex);
                    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                    return o;
                }

                fixed4 frag(v2f i) : SV_Target {
                    fixed4 col = tex2D(_MainTex, i.uv) * _Color;

                // Check if the current fragment is inside the collision of something tagged "Map"
                float depth = LinearEyeDepth(i.vertex.z / i.vertex.w);
                float4 worldPos = mul(UNITY_MATRIX_VP, float4(i.vertex.xyz, 1.0));
                float4 localPos = mul(unity_WorldToObject, worldPos);
                bool insideMap = UNITY_SAMPLE_DEPTH_TEXTURE_LOD(unity_CameraDepthTexture, localPos.xy / localPos.w, 0) >= localPos.z / localPos.w;

                if (insideMap) {
                    col = col * float4(1, 0, 0, 1); // Set the color to red if inside the collision of "Map"
                }

                return col;
            }
            ENDCG
        }
    }
        FallBack "Diffuse"
}