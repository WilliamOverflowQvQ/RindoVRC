Shader "Hirabiki/VRCLens/Mesh Preview"
{
	Properties
	{
		_RenderTex ("Render Texture", 2D) = "black" {}
		_OutputTex ("Output Texture", 2D) = "black" {}
		_MainTex ("Shader blocked texture (None)", 2D) = "white" {}
		[Enum(Hide,1,Show,0)] _HideMonitor ("Head-Up Preview", float) = 1
		_OffsetPitch ("HUP Pitch offset", range(-45, 45)) = 0
		_OffsetYaw ("HUP Yaw offset", range(-45, 45)) = 0
		
		[Header(Toggles)]
		[Enum(VR,0,Desktop,1)] _IsDesktopMode ("Overlay Mode", float) = 0
		[Enum(Hidden,3,Center,0,Corner,1,Stream,2)] _PreviewPosMode ("Overlay View", float) = 0
		[Enum(Landscape,0,Portrait,1)] _PreviewOrientation ("Overlay Orientation", float) = 0
		
		[HideInInspector] [Enum(Disabled,0,Enabled,1)] _IsDirectStream ("Direct Cast (echo)", float) = 0.0
	}
	SubShader
	{
		Tags { "Queue" = "Transparent+1000" }
		
		
		// SHADOW PASS
		Pass
		{
			Tags { "LightMode" = "ShadowCaster" }
			ZClip False
			
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_shadowcaster
			#include "UnityCG.cginc"
			
			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID //SPS-I
			};
			
            struct v2f
            {
                float2 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
				UNITY_VERTEX_OUTPUT_STEREO //SPS-I
            };
			
			uniform float4 _OutputTex_TexelSize;
			uniform bool _IsDesktopMode, _PreviewOrientation, _IsDirectStream;
			uniform float _PreviewPosMode;

			v2f vert(appdata v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v); //SPS-I
				UNITY_INITIALIZE_OUTPUT(v2f, o); //SPS-I
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); //SPS-I
#ifdef USING_STEREO_MATRICES
				o.pos = UnityObjectToClipPos(v.vertex * float4(0.05, 0.05, 0, 1));
				o.uv = v.uv;
#else
				// Formula >> 1 / tan(x / 2 deg)
				bool isCam = (abs(unity_CameraProjection._m11 - _IsDesktopMode * 1.73205080757) < 0.00001 // 60 deg
					|| abs(unity_CameraProjection._m11 - _IsDesktopMode * 1.30322537284) < 0.00001 // 75 deg
					|| abs(unity_CameraProjection._m00 - 0.5714285) < 0.00001 // 120.51106 deg hori.
					|| abs(unity_CameraProjection._m00 - 1.2857140) < 0.00001 // 75.75076 deg hori.
					|| abs(unity_CameraProjection._m00 - 4.2857125) < 0.00001 // 26.26754 deg hori.
					)
					&& unity_OrthoParams.w == 0.0 && _ProjectionParams.y < 0.050001 && all(unity_CameraProjection._m20_m21 == 0.0) // AND is Perspective AND Far-Plane <= 0.05
					&& !_IsDirectStream; // AND DirectStream is off
				o.pos = mul(UNITY_MATRIX_P, v.vertex * float4(1,1,0,1) * isCam + float4(0.0, 0.0, -_ProjectionParams.y, 0.0));
				
				float4 rawPos = ComputeNonStereoScreenPos(o.pos);
				float2 scrUVRaw = rawPos.xy / rawPos.w;
				o.uv = scrUVRaw;
#endif
				return o;
			}
			
			bool bounds(half2 uv) {
				return abs(uv.x - 0.5) < 0.5 && abs(uv.y - 0.5) < 0.5;
			}

			float4 frag(v2f i) : SV_Target
			{
#ifdef USING_STEREO_MATRICES
#else
				bool isWindowed = _IsDesktopMode && _PreviewPosMode < 2.0; // Resist WriteDefaults off blend tree quirks
				bool isRender = (isWindowed &&
						all(_ScreenParams.xy == half2(1920, 1080)
						|| _ScreenParams.xy >= half2(3840, 2160)
						|| _ScreenParams.xy == _OutputTex_TexelSize.zw
						)
					)
					|| (!isWindowed && (any(_ScreenParams.xy != half2(1280, 720)) || _IsDirectStream) );
				
				half previewLerp = smoothstep(0, 1, _PreviewPosMode);
				half2 uv = isWindowed && !isRender ?
					lerp(lerp(
					(i.uv - 0.5) * half2(1.0, 1.777777777) * _ScreenParams.xy / _ScreenParams.y + 0.5,
					(i.uv - 1.0) * half2(1.8, 3.2) * _ScreenParams.xy / _ScreenParams.y + 1.0, previewLerp), lerp(
					(i.uv.yx * half2(1.0, -1.0) - half2(0.5, -0.5)) * half2(1.0, 1.777777777) * _ScreenParams.yx / _ScreenParams.y + 0.5,
					(i.uv.yx * half2(1.0, -1.0) - half2(1.0, -1.0)) * half2(1.8, 3.2) * _ScreenParams.yx / _ScreenParams.y + half2(1.0, 0.0), previewLerp), _PreviewOrientation)
					: i.uv;
				clip(bounds((uv-0.5)/half2(1.018,1.032)+0.5)-1);
#endif
				SHADOW_CASTER_FRAGMENT(i)
			}

			ENDCG
		}
		
		// PASS 1 (ZTest Always)
		// Camera: Overlay | VR: [NONE]
		Pass
		{
			ZClip False
			
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"
			#include "../Lib/HashWithoutSine/HashWithoutSine.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID //SPS-I
			};

            struct v2f
            {
                float2 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
				UNITY_VERTEX_OUTPUT_STEREO //SPS-I
            };
			
			sampler2D _MainTex, _RenderTex, _OutputTex;
			
			uniform float4 _OutputTex_TexelSize;
			uniform bool _IsDesktopMode, _PreviewOrientation, _IsDirectStream;
			uniform float _PreviewPosMode;
			
			v2f vert(appdata v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v); //SPS-I
				UNITY_INITIALIZE_OUTPUT(v2f, o); //SPS-I
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); //SPS-I
#ifdef USING_STEREO_MATRICES
				o.pos = float4(0,0,0,1);
				o.uv = v.uv;
#else

				bool isCam = (abs(unity_CameraProjection._m11 - _IsDesktopMode * 1.73205080757) < 0.00001 // 60 deg
					|| abs(unity_CameraProjection._m11 - _IsDesktopMode * 1.30322537284) < 0.00001 // 75 deg
					|| abs(unity_CameraProjection._m00 - 0.5714285) < 0.00001 // 120.51106 deg hori.
					|| abs(unity_CameraProjection._m00 - 1.2857140) < 0.00001 // 75.75076 deg hori.
					|| abs(unity_CameraProjection._m00 - 4.2857125) < 0.00001 // 26.26754 deg hori.
					)
					&& unity_OrthoParams.w == 0.0 && _ProjectionParams.y < 0.050001 && all(unity_CameraProjection._m20_m21 == 0.0) // AND is Perspective AND Far-Plane <= 0.05
					&& !_IsDirectStream; // AND DirectStream is off
				o.pos = mul(UNITY_MATRIX_P, v.vertex * float4(16,16,0,1) * isCam + float4(0.0, 0.0, -_ProjectionParams.y, 0.0));
				
				float4 rawPos = ComputeNonStereoScreenPos(o.pos);
				float2 scrUVRaw = rawPos.xy / rawPos.w;
				o.uv = scrUVRaw;
#endif
                return o;
			}
			
			half4 dither(half2 uv, half4 c) {
				half3 color = sqrt(max(0.0, c.rgb));
				half3 rand = hash33(half3(uv * _ScreenParams.xy, frac(_Time.y) * 256.0)) * 0.0032 - 0.0016;
				half3 mix = color + rand;
				return half4(mix * mix, c.a);
			}
			
			bool bounds(half2 uv) {
				return abs(uv.x - 0.5) < 0.5 && abs(uv.y - 0.5) < 0.5;
			}
			
			half4 frag(v2f i) : SV_Target
			{
#ifdef USING_STEREO_MATRICES
				return half4(0,0,0,0);
#else
				bool isWindowed = _IsDesktopMode && _PreviewPosMode < 2.0; // Resist WriteDefaults off blend tree quirks
				bool isRender = (isWindowed &&
						all(_ScreenParams.xy == half2(1920, 1080)
						|| _ScreenParams.xy >= half2(3840, 2160)
						|| _ScreenParams.xy == _OutputTex_TexelSize.zw
						)
					)
					|| (!isWindowed && (any(_ScreenParams.xy != half2(1280, 720)) || _IsDirectStream) );
				
				half previewLerp = smoothstep(0, 1, _PreviewPosMode);
				half2 uv = isWindowed && !isRender ?
					lerp(lerp(
					(i.uv - 0.5) * half2(1.0, 1.777777777) * _ScreenParams.xy / _ScreenParams.y + 0.5,
					(i.uv - 1.0) * half2(1.8, 3.2) * _ScreenParams.xy / _ScreenParams.y + 1.0, previewLerp), lerp(
					(i.uv.yx * half2(1.0, -1.0) - half2(0.5, -0.5)) * half2(1.0, 1.777777777) * _ScreenParams.yx / _ScreenParams.y + 0.5,
					(i.uv.yx * half2(1.0, -1.0) - half2(1.0, -1.0)) * half2(1.8, 3.2) * _ScreenParams.yx / _ScreenParams.y + half2(1.0, 0.0), previewLerp), _PreviewOrientation)
					: i.uv;
				clip(bounds((uv-0.5)/half2(1.018,1.032)+0.5)-1);
				
				half4 col = isRender ? dither(i.uv, tex2D(_OutputTex, uv)) : tex2D(_RenderTex, uv);
				
				col = bounds(uv) ? col : half4(0.8, 0.8, 0.8, 1.0);
				return col;
#endif
			}

			ENDCG
		}
		
		// PASS 2 ---- it should be possible to optimize this and use texture's uv
		// Camera: [NONE] | VR: Normal Viewfinder
		Pass
		{
			
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID //SPS-I
			};

            struct v2f
            {
                float2 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
				UNITY_VERTEX_OUTPUT_STEREO //SPS-I
            };
			
			sampler2D _MainTex, _RenderTex;
			
			v2f vert(appdata v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v); //SPS-I
				UNITY_INITIALIZE_OUTPUT(v2f, o); //SPS-I
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); //SPS-I
				o.uv = v.uv;
#ifdef USING_STEREO_MATRICES
				// Make the mesh preview only visible in VR, and is scaled down
				o.pos = UnityObjectToClipPos(v.vertex * float4(0.05, 0.05, 0, 1));
#else
				o.pos = float4(0,0,0,1);
#endif
				return o;
			}
			
			half4 frag(v2f i) : SV_Target
			{
#ifdef USING_STEREO_MATRICES
				half4 col = tex2D(_RenderTex, i.uv);
				return col;
#else
				return half4(0,0,0,0);
#endif
			}

			ENDCG
		}
		
		// PASS 3 ---- Should be totally possible to merge with PASS 2
		// Camera: [NONE] | VR: Head-Up Display
		Pass
		{
			
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID //SPS-I
			};

            struct v2f
            {
                float2 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
				UNITY_VERTEX_OUTPUT_STEREO //SPS-I
            };
			
			sampler2D _MainTex, _RenderTex;
			uniform float _OffsetPitch, _OffsetYaw;
			uniform bool _HideMonitor;
			uniform bool _PreviewOrientation;
			
			float angleAverage(float a, float b) {
				float threshold = 0.7854; // 45 degrees
				a = abs(a - b > threshold) ? abs(a) * sign(b) : a;
				return (a + b) * 0.5;
			}
			float4 vertexRotPitch(float4 p, float t) {
				return mul(float4x4(
					 1.0000, 0.0000, 0.0000, 0.0,
					 0.0000, cos(t), sin(t), 0.0,
					 0.0000,-sin(t), cos(t), 0.0,
					 0.0000, 0.0000, 0.0000, 1.0
				), p);
			}
			float4 vertexRotYaw(float4 p, float t) {
				return mul(float4x4(
					 cos(t), 0.0000, sin(t), 0.0,
					 0.0000, 1.0000, 0.0000, 0.0,
					-sin(t), 0.0000, cos(t), 0.0,
					 0.0000, 0.0000, 0.0000, 1.0
				), p);
			}
			float getPitch(float4x4 mat) {
				return -asin(mat._32);
			}
			float getYaw(float4x4 mat) {
				float yaw = UNITY_PI - acos(clamp(mat._33 / sqrt(1.0-mat._32*mat._32), -1.0, 1.0));
				yaw = mat._31 > 0.0 ? -yaw : yaw;
				return yaw;
			}
			
			v2f vert(appdata v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v); //SPS-I
				UNITY_INITIALIZE_OUTPUT(v2f, o); //SPS-I
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); //SPS-I
                o.uv = v.uv;
#ifdef USING_STEREO_MATRICES
				if(_HideMonitor) {
					o.pos = float4(0,0,0,1);
					return o;
				}
				float4 orient = _PreviewOrientation ? float4(0.384, -0.216, 0, 1) : float4(0.384, 0.216, 0, 1);
				
				float ipd = distance(unity_StereoMatrixV[0]._m03_m13_m23, unity_StereoMatrixV[1]._m03_m13_m23);
				float4 avgPos = float4(unity_StereoWorldSpaceCameraPos[0] + unity_StereoWorldSpaceCameraPos[1], 0.0) * 0.5;
				float4 vPos = v.vertex * orient;
				vPos.xy = _PreviewOrientation ? vPos.yx : vPos.xy;
				float4 vOff = float4(0.0, 0.0, ipd * 12, 0.0);
				
				float pitchOffset = 0.0; //0.418879;// 24 degrees up  //-0.104719755; // 6 deg
				//float viewPitch = smoothstep(-0.7854, 0.7854, getPitch(unity_StereoMatrixV[1]) + pitchOffset) * 2.0944 - 1.0472 + pitchOffset;
				
				float viewPitch = angleAverage(getPitch(unity_StereoMatrixV[0]), getPitch(unity_StereoMatrixV[1])) + _OffsetPitch * 0.0174532925;
				//viewPitch += sign(viewPitch) * 0.5236 + pitchOffset;
				float viewYaw = angleAverage(getYaw(unity_StereoMatrixV[0]), getYaw(unity_StereoMatrixV[1])) + _OffsetYaw * 0.0174532925;
				//viewYaw = round(viewYaw * 1.90985932) * 0.523598776 + _OffsetYaw * 0.0174532925; // round(Red -> Deg/30) Deg/30 -> Rad
				
				vPos = vertexRotPitch(vPos, viewPitch);
				vPos = vertexRotYaw(vPos, viewYaw);
				
				vOff = vertexRotPitch(vOff, viewPitch);
				vPos += vertexRotYaw(vOff, viewYaw);
				vPos += avgPos;
				o.pos = mul(UNITY_MATRIX_VP, vPos);
#else
				o.pos = float4(0,0,0,1);
#endif
                return o;
			}
			
			half4 frag(v2f i) : SV_Target
			{
#ifdef USING_STEREO_MATRICES
				half4 col = tex2D(_RenderTex, i.uv);
				return col;
#endif
				return half4(0,0,0,0);
			}

			ENDCG
		}
	}
}