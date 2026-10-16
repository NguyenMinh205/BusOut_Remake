Shader "BusOut/Character"
{
    Properties
    {
        _Color ("Màu khách", Color) = (1,1,1,1)
        _MainTex ("Suit Vip (RGB vest, A mặt nạ vest)", 2D) = "white" {}
        [Toggle] _VipPerson ("Khách VIP (hiện vest)", Float) = 0
        _HighLight ("HighLight", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+2" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _Color; float _VipPerson; float _HighLight;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 t = tex2D(_MainTex, i.uv);
                float suitMask = t.a;

                float lum = dot(t.rgb, float3(0.299, 0.587, 0.114));
                float shade = lerp(saturate(0.35 + lum * 1.1), 1.0, suitMask);
                float3 body = _Color.rgb * shade;

                float3 col = lerp(body, t.rgb, suitMask * _VipPerson);
                col += _HighLight * 0.3;
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
