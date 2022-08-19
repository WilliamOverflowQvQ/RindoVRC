Shader "Hirabiki/VRCLens/ScreenSpace AuxFocus Cutout"
{
	Properties
	{
		_MainTex ("Shader blocked texture", 2D) = "white" {}
		_Color ("Pointer Color", color) = (1,1,1,1)
	}
	SubShader
	{
		Tags
		{
			"Queue" = "Transparent"
			"DisableBatching" = "True"
			"IgnoreProjector" = "True"
		}
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID //SPS-I
			};

            struct v2f
            {
                float2 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
				float4 vertex : TEXCOORD1;
				UNITY_VERTEX_OUTPUT_STEREO //SPS-I
            };
			
			half4 _Color;
			
			v2f vert(appdata v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v); //SPS-I
				UNITY_INITIALIZE_OUTPUT(v2f, o); //SPS-I
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); //SPS-I
#if defined(USING_STEREO_MATRICES)
				bool isCam = 0.0;
				o.pos = UnityObjectToClipPos(v.vertex);
#else
				//It'll will only be injected to the screen when it's 32x16 and orthographic
				bool isCam = all(_ScreenParams.xy == half2(16, 9)) && unity_OrthoParams.w == 1.0;
				o.pos = mul(UNITY_MATRIX_P, v.vertex * float4(16,16,0,1) * isCam + float4(0.0, 0.0, -_ProjectionParams.y, 0.0)); // EXPERIMENTAL -0.000
#endif
				
				float4 rawPos = ComputeNonStereoScreenPos(o.pos);
				float2 scrUVRaw = rawPos.xy / rawPos.w;
				
                o.uv = scrUVRaw;
				o.vertex = v.vertex;
                return o;
			}
			
			half4 frag(v2f i) : SV_Target
			{
#if defined(USING_STEREO_MATRICES)
				half4 col = half4(i.vertex.z < -0.2 ? _Color : 1.0);
				return col;
#else
				float3 pos = unity_ObjectToWorld._14_24_34;
				pos = pos - _WorldSpaceCameraPos;
				// WorldToCamera is just View Matrix
				float3 toCam = mul(unity_WorldToCamera, pos);
				toCam.xy /= unity_OrthoParams.xy * 2.0;
				toCam.xy = clamp(toCam.xy, -0.5, 0.5);
				
				half4 col = half4(toCam, abs(toCam.z) < 0.01);
				return col;
#endif
			}

			ENDCG
		}
	}
}