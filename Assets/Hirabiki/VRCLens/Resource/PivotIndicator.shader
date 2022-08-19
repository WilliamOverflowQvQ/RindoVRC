Shader "Hirabiki/VRCLens/Pivot Indicator"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_Color("Color", Color) = (1,1,1,1)
	}
	SubShader
	{
		Tags { "Queue" = "Transparent" }
		Blend SrcAlpha OneMinusSrcAlpha
		Cull Off
		ZWrite Off

		Pass
		{
			ZTest LEqual
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"
			#include "NumberDisplayInclude.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			fixed4 _Color;
			
			v2f vert (appdata v)
			{
				v2f o;
#if defined(USING_STEREO_MATRICES)
				float scale = distance((unity_StereoWorldSpaceCameraPos[0] + unity_StereoWorldSpaceCameraPos[1]) * 0.5, unity_ObjectToWorld._14_24_34);
				o.vertex = UnityObjectToClipPos(lerp(v.vertex, v.vertex * scale, 0.5));
#else
				float isNotCam = abs(unity_CameraProjection._m11 - 1.73205) < 0.00001;
				float scale = distance(_WorldSpaceCameraPos, unity_ObjectToWorld._14_24_34);
				o.vertex = UnityObjectToClipPos(isNotCam * v.vertex * scale);
#endif
				o.uv = v.uv;
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				// sample the texture
#if defined(USING_STEREO_MATRICES)
				float dist = distance((unity_StereoWorldSpaceCameraPos[0] + unity_StereoWorldSpaceCameraPos[1]) * 0.5, unity_ObjectToWorld._14_24_34);
#else
				float dist = distance(_WorldSpaceCameraPos, unity_ObjectToWorld._14_24_34);
#endif
				fixed4 col = i.uv.x < 1.125 ? drawNumber(_MainTex, i.uv, dist, 4, 0) * _Color : _Color;
				return col;
			}
			ENDCG
		}
		
		// Please copy-paste from above
		Pass
		{
			ZTest Greater
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"
			#include "NumberDisplayInclude.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			fixed4 _Color;
			
			v2f vert (appdata v)
			{
				v2f o;
#if defined(USING_STEREO_MATRICES)
				float scale = distance((unity_StereoWorldSpaceCameraPos[0] + unity_StereoWorldSpaceCameraPos[1]) * 0.5, unity_ObjectToWorld._14_24_34);
				o.vertex = UnityObjectToClipPos(lerp(v.vertex, v.vertex * scale, 0.5));
#else
				float isNotCam = abs(unity_CameraProjection._m11 - 1.73205) < 0.00001;
				float scale = distance(_WorldSpaceCameraPos, unity_ObjectToWorld._14_24_34);
				o.vertex = UnityObjectToClipPos(isNotCam * v.vertex * scale);
#endif
				o.uv = v.uv;
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				// sample the texture
#if defined(USING_STEREO_MATRICES)
				float dist = distance((unity_StereoWorldSpaceCameraPos[0] + unity_StereoWorldSpaceCameraPos[1]) * 0.5, unity_ObjectToWorld._14_24_34);
#else
				float dist = distance(_WorldSpaceCameraPos, unity_ObjectToWorld._14_24_34);
#endif
				fixed4 col = i.uv.x < 1.125 ? drawNumber(_MainTex, i.uv, dist, 4, 0) * _Color : _Color;
				col.a *= 0.1;
				return col;
			}
			ENDCG
		}
	}
}
