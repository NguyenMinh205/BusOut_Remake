Shader "BusOut/Water" {
	Properties {
		_Color ("Tint", Vector) = (1,1,1,0.5)
		_FoamC ("Foam", Vector) = (1,1,1,0.5)
		_MainTex ("Main Texture", 2D) = "white" {}
		_TextureDistort ("Texture Wobble", Range(0, 1)) = 0.1
		_NoiseTex ("Extra Wave Noise", 2D) = "white" {}
		_Speed ("Wave Speed", Range(0, 1)) = 0.5
		_Amount ("Wave Amount", Range(0, 1)) = 0.6
		_Scale ("Scale", Range(0, 1)) = 0.5
		_Height ("Wave Height", Range(0, 1)) = 0.1
		_Foam ("Foamline Thickness", Range(0, 10)) = 8
	}
	
	SubShader{
		Tags { "RenderType"="Opaque" }
		LOD 200

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			// ⚠️ Không tự khai báo unity_ObjectToWorld / unity_MatrixVP: nằm ngoài UnityPerDraw của Core.hlsl là
			// shader rớt khỏi SRP Batcher, mỗi vật lại tốn một lần đổi trạng thái đầy đủ.
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
				float4 _Color;
			CBUFFER_END

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Vertex_Stage_Output
			{
				float2 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.uv = (input.uv.xy * _MainTex_ST.xy) + _MainTex_ST.zw;
				output.pos = TransformObjectToHClip(input.pos.xyz);
				return output;
			}

			Texture2D<float4> _MainTex;
			SamplerState sampler_MainTex;

			struct Fragment_Stage_Input
			{
				float2 uv : TEXCOORD0;
			};

			float4 frag(Fragment_Stage_Input input) : SV_TARGET
			{
				return _MainTex.Sample(sampler_MainTex, input.uv.xy) * _Color;
			}

			ENDHLSL
		}
	}
}