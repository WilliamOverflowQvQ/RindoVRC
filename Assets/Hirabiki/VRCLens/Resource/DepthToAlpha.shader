Shader "Hirabiki/VRCLens/Depth To Alpha"
{
	Properties {
	}
	SubShader
	{
		// Draw ourselves after all opaque geometry
		Tags {
			"Queue" = "Overlay-1"
			"IgnoreProjector" = "True"
		}
		ZTest Always
		ZWrite Off
		
		GrabPass
		{
			"_HirabikiVRCLensGrabTexture"
		}
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			
			struct appdata
			{
				float4 vertex : POSITION;
			};

            struct v2f
            {
                float2 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
            };
			
			UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
			sampler2D _HirabikiVRCLensGrabTexture;
			
			v2f vert(appdata v)
			{
				v2f o;
#if defined(USING_STEREO_MATRICES)
				bool isCam = 0.0;
#else
				bool isCam = abs(unity_CameraProjection._m11 - 1.73205) > 0.00001 // is not 60 deg
					&& all(_ScreenParams.xy == half2(1920, 1080)) // is 1920x1080
					&& all(unity_CameraProjection._m20_m21 == 0.0); // is not in mirror
#endif
				o.pos = mul(UNITY_MATRIX_P, v.vertex * float4(1,1,0,1) * isCam + float4(0.0, 0.0, -0.001 -_ProjectionParams.y, 0.0));
				
				float4 rawPos = ComputeNonStereoScreenPos(o.pos);
				float2 scrUVRaw = rawPos.xy / rawPos.w;
				
                o.uv = scrUVRaw;
                return o;
			}
			
			half4 frag(v2f i) : SV_Target
			{
#if defined(USING_STEREO_MATRICES)
				return half4(0,0,0,0);
#else
				half4 col = tex2D(_HirabikiVRCLensGrabTexture, i.uv);
				col.a = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv) * 256.0;
				return col;
#endif
			}
			
			ENDCG
		}
    }
}