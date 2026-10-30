// Skybox Menu: tranh trời vẽ sẵn (có công trình biểu tượng) + lớp mây trôi ngang; bộ thứ hai (Blend*) để chuyển dần
// sang trời của theme khác bằng _BlendFactor 0 → 1. Viết lại thay shader giả của AssetRipper (bản gốc KIM/Skybox Custom
// không extract được) — tên Properties giữ y như gốc để material cũ dùng tiếp.
//
// Toạ độ trước Tiling/Offset (theo hướng nhìn, không theo màn hình → trời đứng yên khi camera đổi tỉ lệ màn hình):
//   u = góc ngang / 360°, 0 = nhìn thẳng +Z  → Offset X 0.5 = giữa tranh ở ngay trước mặt; Tiling X = số lần lặp quanh 360°.
//   v = dir.y * 0.5 + 0.5, 0.5 = đường chân trời → Tiling Y càng lớn tranh càng thấp; Offset Y dời tranh lên / xuống.
// Số gốc trục Y (2.2, −0.65) với cách tính v này cho tranh trải từ ~−24° tới ~+30° — vừa khung camera Menu (nghiêng 7°, FOV 70).
Shader "BusOut/Skybox" {
	Properties {
		_SkyTex ("Sky Texture", 2D) = "white" {}
		[NoScaleOffset] _CloudTex ("Cloud Texture", 2D) = "white" {}
		_BlendSkyTex ("Blend Sky Texture", 2D) = "white" {}
		[NoScaleOffset] _BlendCloudTex ("Blend Cloud Texture", 2D) = "white" {}
		_BlendFactor ("Blend Factor", Range(0, 1)) = 0
		_Speed ("Speed", Float) = 0
	}

	SubShader {
		Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
		Cull Off
		ZWrite Off

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			TEXTURE2D(_SkyTex);
			TEXTURE2D(_CloudTex);
			TEXTURE2D(_BlendSkyTex);
			TEXTURE2D(_BlendCloudTex);

			CBUFFER_START(UnityPerMaterial)
				float4 _SkyTex_ST;
				float4 _BlendSkyTex_ST;
				float _BlendFactor;
				float _Speed;
			CBUFFER_END

			struct Attributes
			{
				float4 positionOS : POSITION;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float3 direction : TEXCOORD0;
			};

			Varyings vert(Attributes input)
			{
				Varyings output;
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				// Lưới skybox đặt tâm ở camera nên toạ độ đỉnh chính là hướng nhìn.
				output.direction = input.positionOS.xyz;
				return output;
			}

			// Trời + mây của một bộ theme. Mây dùng chung toạ độ với tranh trời của nó (gốc để mây [NoScaleOffset]).
			half3 SampleSky(TEXTURE2D_PARAM(skyTex, skySampler), TEXTURE2D_PARAM(cloudTex, cloudSampler),
				float2 rawUV, float2 rawDx, float2 rawDy, float4 skyST, float cloudScroll)
			{
				float2 uv = rawUV * skyST.xy + skyST.zw;
				float2 uvDx = rawDx * skyST.xy;
				float2 uvDy = rawDy * skyST.xy;

				// Trời: lặp ngang bằng frac, kẹp dọc (trên tranh = màu trời trên cùng, dưới tranh = hàng cuối).
				// ⚠️ Lấy mẫu bằng sampler Clamp của shader, KHÔNG theo Wrap Mode của texture: Repeat theo chiều dọc thì vùng
				// trên tranh (v kẹp = 1) bị trộn nửa với hàng dưới cùng (toà nhà) → cả bầu trời ngả màu.
				// Đạo hàm lấy từ uv TRƯỚC frac, không thì có vạch ở chỗ tranh nối nhau (khi texture bật mipmap).
				float2 skyUV = float2(frac(uv.x), saturate(uv.y));
				half3 sky = SAMPLE_TEXTURE2D_GRAD(skyTex, skySampler, skyUV, uvDx, uvDy).rgb;

				// Mây: Repeat để trôi liên tục; hàng trên / dưới của tranh mây trong suốt nên kẹp dọc không lộ vệt.
				float2 cloudUV = float2(uv.x + cloudScroll, saturate(uv.y));
				half4 cloud = SAMPLE_TEXTURE2D_GRAD(cloudTex, cloudSampler, cloudUV, uvDx, uvDy);

				return lerp(sky, cloud.rgb, cloud.a);
			}

			half4 frag(Varyings input) : SV_Target
			{
				float3 direction = normalize(input.direction);
				// ⚠️ atan2 nhảy từ +0.5 về −0.5 ở ngay sau lưng camera → một vạch mảnh ở đó. Camera Menu không quay ra sau
				// nên để vậy; nếu sau này có góc nhìn ra sau thì phải xử lý chỗ nối.
				float2 rawUV = float2(atan2(direction.x, direction.z) / TWO_PI, direction.y * 0.5 + 0.5);
				// frac giữ số nhỏ: _Time tăng mãi, nhân thẳng vào uv thì lâu dần mây giật do mất độ chính xác.
				float cloudScroll = frac(_Time.y * _Speed);
				// Đạo hàm tính ở đây, ngoài nhánh if bên dưới (đạo hàm trong nhánh rẽ: trình biên dịch báo lỗi / không xác định).
				float2 rawDx = ddx(rawUV);
				float2 rawDy = ddy(rawUV);

				half3 color = SampleSky(TEXTURE2D_ARGS(_SkyTex, sampler_TrilinearClamp),
					TEXTURE2D_ARGS(_CloudTex, sampler_TrilinearRepeat), rawUV, rawDx, rawDy, _SkyTex_ST, cloudScroll);

				// Chỉ lấy mẫu bộ thứ hai khi đang chuyển theme (phần lớn thời gian _BlendFactor = 0).
				UNITY_BRANCH
				if (_BlendFactor > 0)
				{
					half3 blendColor = SampleSky(TEXTURE2D_ARGS(_BlendSkyTex, sampler_TrilinearClamp),
						TEXTURE2D_ARGS(_BlendCloudTex, sampler_TrilinearRepeat), rawUV, rawDx, rawDy, _BlendSkyTex_ST, cloudScroll);
					color = lerp(color, blendColor, _BlendFactor);
				}

				return half4(color, 1);
			}

			ENDHLSL
		}
	}
	Fallback Off
}
