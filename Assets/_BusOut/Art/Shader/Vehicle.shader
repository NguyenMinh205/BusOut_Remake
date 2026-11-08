Shader "BusOut/Vehicle"
{
    Properties
    {
        _Color ("Body Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        [NoScaleOffset] _SupTex ("Mask (R paint/interior, G lamp, B paint/glass, A AO)", 2D) = "white" {}
        _HSV ("Shadow Control (x hue, y sat, z shadow, w -)", Vector) = (0,1,0.4,1)
        _Fill ("Roof Fill (1 = đóng nóc, 0 = mở nóc)", Range(0,1)) = 1
        _RoofCut ("Roof Cut Height (object space)", Float) = 0.85
        _HighLight ("HighLight", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+1" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            sampler2D _SupTex;
            float4 _HSV; float _RoofCut; float _HighLight;

            // ⚠️ CarView đổi _Color và _Fill từng xe bằng MaterialPropertyBlock, nên xe không vào được SRP Batcher.
            // Hai thuộc tính này phải nằm trong buffer instancing; để ngoài là xe khác màu không gộp chung lượt vẽ được.
            UNITY_INSTANCING_BUFFER_START(PerCar)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _Fill)
            UNITY_INSTANCING_BUFFER_END(PerCar)

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float objY : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.objY = v.vertex.y;
                return o;
            }

            fixed4 frag (v2f i, fixed facing : VFACE) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                if (UNITY_ACCESS_INSTANCED_PROP(PerCar, _Fill) < 0.5 && i.objY > _RoofCut) discard;
                fixed3 bodyColor = UNITY_ACCESS_INSTANCED_PROP(PerCar, _Color).rgb;

                fixed4 albedo = tex2D(_MainTex, i.uv);
                fixed4 mask = tex2D(_SupTex, i.uv);
                float paint = mask.r * mask.b;

                float lum = dot(albedo.rgb, float3(0.299, 0.587, 0.114));
                float3 lit = bodyColor * saturate(lum * 1.25);
                float3 shade = bodyColor * bodyColor * (1 - _HSV.z * 0.5);
                float3 painted = lerp(shade, lit, saturate(lum * 1.6));
                float3 col = lerp(albedo.rgb, painted, paint);
                col *= lerp(0.65, 1.0, mask.a);
                if (facing < 0) col *= 0.55;
                col += _HighLight * 0.3;
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
