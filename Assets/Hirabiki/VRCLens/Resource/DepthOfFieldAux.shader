Shader "Hirabiki/VRCLens/ScreenSpace Aux Cutout"
{
	Properties
	{
		_AuxExpTex ("Aux Exposure Texture", 2D) = "black" {}
		_AuxFocusTex ("Aux Focus Texture", 2D) = "black" {}
		_DepthAvatarTex ("Avatar Depth Texture", 2D) = "black" {}
		_MainTex ("Shader blocked texture", 2D) = "white" {}
		[Enum(VR,0,Desktop,1)] _IsDesktopMode ("Usage Mode", float) = 0
		[Enum(Disabled,0,Enabled,1)] _IsAvatarDetectAF ("Avatar AF", float) = 0
		[HideInInspector] [Enum(Disabled,0,Enabled,1)] _ExposureLock ("Lock Exposure", float) = 0
		
		[HideInInspector] _FocusOffsetH ("Focus offset H", range(-1.0, 1.0)) = 0.0
		[HideInInspector] _FocusOffsetV ("Focus offset V", range(-1.0, 1.0)) = 0.0
	}
	SubShader
	{
		Tags
		{
			"Queue" = "Transparent+1000"
			"DisableBatching" = "True"
			"IgnoreProjector" = "True"
		}
		ZTest Always
		Blend SrcAlpha OneMinusSrcAlpha
		//Cull Front
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
				UNITY_VERTEX_OUTPUT_STEREO //SPS-I
            };
			
			sampler2D _AuxExpTex, _AuxFocusTex, _DepthAvatarTex;
			uniform float _FocusOffsetH, _FocusOffsetV;
			uniform bool _IsDesktopMode, _IsAvatarDetectAF, _ExposureLock;
			
			v2f vert(appdata v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v); //SPS-I
				UNITY_INITIALIZE_OUTPUT(v2f, o); //SPS-I
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); //SPS-I
#if defined(USING_STEREO_MATRICES)
				bool isCam = 0.0;
#else
				bool isCam = all(_ScreenParams.xy == half2(8, 16)) || all(_ScreenParams.xy == half2(8, 8));
#endif
				o.pos = mul(UNITY_MATRIX_P, v.vertex * float4(512,512,0,1) * isCam + float4(0.0, 0.0, -0.001 -_ProjectionParams.y, 0.0));
				
				float4 rawPos = ComputeNonStereoScreenPos(o.pos);
				float2 scrUVRaw = rawPos.xy / rawPos.w;
				
                o.uv = scrUVRaw;
                return o;
			}
			
			// UNITY_MATRIX_V._32 is pitch, works independently.
			// cos(asin(x)) == sqrt(1-x^2)
			float getPitch(float4x4 mat) {
				return -asin(mat._32);
			}
			float getRoll(float4x4 mat) {
				float roll = acos(mat._22 / sqrt(1.0-mat._32*mat._32));
				float rollSin = asin(mat._12 / sqrt(1.0-mat._32*mat._32));
				float rollOut = mat._12 >= 0.0 ? roll : -roll;
				rollOut = roll > UNITY_PI*0.25 ? rollOut : rollSin;
				return rollOut;
			}
			
			half2 evaluateExposure() {
				half3 r = 0.0, g = 0.0;
#define VRCLTEX(u, v, l) tex2Dlod(_AuxExpTex, half4(u * 0.03125, v * 0.0625, 0.00, l)).rgb
				/*
				for(int x = 1; x < 32; x++) {
					r = max(VRCLTEX(x, 1, 4), r);
					r = max(VRCLTEX(x, 2, 4), r);
					r = max(VRCLTEX(x, 3, 4), r);
					r = max(VRCLTEX(x, 4, 4), r);
					r = max(VRCLTEX(x, 5, 4), r);
					r = max(VRCLTEX(x, 6, 4), r);
					r = max(VRCLTEX(x, 7, 4), r);
					r = max(VRCLTEX(x, 8, 4), r);
					r = max(VRCLTEX(x, 9, 4), r);
					r = max(VRCLTEX(x,10, 4), r);
					r = max(VRCLTEX(x,11, 4), r);
				}
				*/
				g = max(VRCLTEX( 0, 0, 6) * .25, g);
				g = max(VRCLTEX( 4, 0, 6) * 0.5, g);
				g = max(VRCLTEX( 8, 0, 6) * 0.5, g);
				g = max(VRCLTEX(12, 0, 6) * 0.5, g);
				g = max(VRCLTEX(16, 0, 6) * 0.5, g);
				g = max(VRCLTEX(20, 0, 6) * 0.5, g);
				g = max(VRCLTEX(24, 0, 6) * 0.5, g);
				g = max(VRCLTEX(28, 0, 6) * 0.5, g);
				g = max(VRCLTEX(32, 0, 6) * .25, g);
				
				g = max(VRCLTEX( 0, 4, 6) * 0.5, g);
				g = max(VRCLTEX( 4, 4, 6), g);
				g = max(VRCLTEX( 8, 4, 6), g);
				g = max(VRCLTEX(12, 4, 6), g);
				g = max(VRCLTEX(16, 4, 6), g);
				g = max(VRCLTEX(20, 4, 6), g);
				g = max(VRCLTEX(24, 4, 6), g);
				g = max(VRCLTEX(28, 4, 6), g);
				g = max(VRCLTEX(32, 4, 6) * 0.5, g);
				
				g = max(VRCLTEX( 0, 8, 6) * 0.5, g);
				g = max(VRCLTEX( 4, 8, 6), g);
				g = max(VRCLTEX( 8, 8, 6), g);
				g = max(VRCLTEX(12, 8, 6), g);
				g = max(VRCLTEX(16, 8, 6), g);
				g = max(VRCLTEX(20, 8, 6), g);
				g = max(VRCLTEX(24, 8, 6), g);
				g = max(VRCLTEX(28, 8, 6), g);
				g = max(VRCLTEX(32, 8, 6) * 0.5, g);
				
				g = max(VRCLTEX( 0,12, 6) * 0.5, g);
				g = max(VRCLTEX( 4,12, 6), g);
				g = max(VRCLTEX( 8,12, 6), g);
				g = max(VRCLTEX(12,12, 6), g);
				g = max(VRCLTEX(16,12, 6), g);
				g = max(VRCLTEX(20,12, 6), g);
				g = max(VRCLTEX(24,12, 6), g);
				g = max(VRCLTEX(28,12, 6), g);
				g = max(VRCLTEX(32,12, 6) * 0.5, g);
				
				g = max(VRCLTEX( 0,16, 6) * .25, g);
				g = max(VRCLTEX( 4,16, 6) * 0.5, g);
				g = max(VRCLTEX( 8,16, 6) * 0.5, g);
				g = max(VRCLTEX(12,16, 6) * 0.5, g);
				g = max(VRCLTEX(16,16, 6) * 0.5, g);
				g = max(VRCLTEX(20,16, 6) * 0.5, g);
				g = max(VRCLTEX(24,16, 6) * 0.5, g);
				g = max(VRCLTEX(28,16, 6) * 0.5, g);
				g = max(VRCLTEX(32,16, 6) * .25, g);
				r = max(0.0, g);
				g = lerp(max(0.0, g) * 2.50, 1.0, 0.05); // 1/16 * 0.9
#undef VRCLTEX
				// Zero Guard
				r = any(r) ? r : 1.0;
				g = any(g) ? g : 1.0;
				// R = PEAK  G = AVG
				return half2(max(max(r.r, r.g), r.b), min(max(max(g.r, g.g), g.b), 1.5));
			}
			
			float4 evaluateAvatarFocus(half2 pos) {
				float z = tex2D(_DepthAvatarTex, pos).r * 256.0;
				return float4(0.0, 0.0, z, z != 0.0);
			}
			
			float4 frag(v2f i) : SV_Target
			{
				float3 objectPos = unity_ObjectToWorld._14_24_34;
				float distFromObject = distance(objectPos, _WorldSpaceCameraPos);
				
				half4 eval = half4(evaluateExposure(), 0.0, i.uv.y < 0.5);
				
				if(_IsAvatarDetectAF) {
					float2 focusPos = _IsDesktopMode ? float2(_FocusOffsetH, _FocusOffsetV) * 0.5 + 0.5 : tex2D(_AuxFocusTex, 0.0).rg + 0.5;
					eval = i.uv.y < 0.5 ? eval : evaluateAvatarFocus(focusPos);
				}
				
				// [Exposure : Focus] stiffness (less == smooth)
				half fade = pow(_ExposureLock ? 1.00 : 0.05, unity_DeltaTime.x) * (i.uv.y < 0.5);
				
				half4 col = 0.0;
				// Two cameras are rendered to two halves of this texture
				bool isOrth = unity_OrthoParams.w == 1.0;
				col = isOrth && unity_OrthoParams.y == -0.005 ? half4(eval.xyz, (1.0 - fade) * eval.w) : col;
				col = isOrth && unity_OrthoParams.y == -256.0 ? half4(distFromObject, getPitch(UNITY_MATRIX_V), getRoll(UNITY_MATRIX_V), 1.0) : col;
				return col;
			}

			ENDCG
		}
	}
}