Shader "Hirabiki/VRCLens/DepthOfField Cutout"
{
	Properties
	{
		[Toggle(DEPTH_OF_FIELD)] _EnableDoF ("Enable Depth of Field", float) = 0
		_FocusDistance ("Focus (m)", range(0.5, 100)) = 1.5
		_DoFStrength ("F-number", range(1.0, 32.0)) = 4.0
		_ExposureValue ("Exposure Value", range(-4.0, 4.0)) = 0.0
		_FocusPeakingColor ("Focus Peaking Color", Color) = (1,0,0,1)
		[HideInInspector] _FocusOffsetH ("Focus offset H", range(-1.0, 1.0)) = 0.0
		[HideInInspector] _FocusOffsetV ("Focus offset V", range(-1.0, 1.0)) = 0.0
		
		[Header(Toggles)]
		[Enum(VR,0,Desktop,1)] _IsDesktopMode ("Overlay Mode", float) = 0
		[Enum(Hidden,3,Center,0,Corner,1,Stream,2)] _PreviewPosMode ("Overlay View", float) = 0
		[Enum(Landscape,0,Portrait,1)] _PreviewOrientation ("Overlay Orientation", float) = 0
		[Enum(Normal,0,Selfie,1,Avatar,2)] _IsExternalFocus ("AF Mode", int) = 0
		[Enum(Off,0,On,1)] _AperturePriority ("Av (Aperture Priority)", float) = 0
		[Enum(Manual,0,Locked,1,Auto,2)] _ExposureMode ("Exposure Mode", float) = 0
		[Enum(Disabled,0,Enabled,1)] _ShowOverlay ("Show Overlay", float) = 1
		_WhiteBalance ("White Balance", range(-1.0, 1.0)) = 0
		_TonemapMode ("Tonemap Mode", float) = 0
		_TonemapLerp ("Tonemap Strength", range(0.0, 1.0)) = 0.0
		_SensorScale ("Sensor Scaling", range(0.0, 2.0)) = 1.0
		_TutorialDisplay ("Quick Start Display", range(0.0, 1.0)) = 1.0
		[Enum(Normal,0,Cool,1,Cute,2)] _ImageFilter ("Picture Filter", int) = 0
		
		[Enum(Disabled,0,Enabled,1)] _ShowFocusPeeking ("Focus Peeking", float) = 0
		[Enum(Disabled,0,Enabled,1)] _ShowZebra ("Overexposure Zebra", float) = 0
		[Enum(Disabled,0,Enabled,1)] _ShowRuleOfThirds ("Rule of Thirds grid", float) = 0
		[Enum(Disabled,0,Enabled,1)] _ShowLeveler ("Horizon Level", float) = 0
		[Header(Advanced)]
		_LensShapeMode ("Lens Blur Shape", int) = 0
		_AnamorphicRatio ("Anamorphic Ratio", range(0, 1)) = 1
		_BlurSamples ("Blur Samples", range(0, 348)) = 228
		
		[HideInInspector] _FocalLength("Focal Length (echo)", range(10.0, 1000.0)) = 18.0
		[HideInInspector] _SensorType("SensorType (echo)", float) = 0.0
		[HideInInspector] [Enum(Disabled,0,Enabled,1,Straightened,2)] _IsImageStabilize("Optical IS (echo)", float) = 0.0
		[HideInInspector] [Enum(Disabled,0,Enabled,1)] _IsDirectStream ("Direct Cast (echo)", float) = 0.0
		[HideInInspector] [Enum(Disabled,0,Enabled,1)] _IsSideBySide3D ("SBS Stereo (echo)", float) = 0.0
		
		[Header(Textures)]
		_RenderTex ("Render Texture", 2D) = "black" {}
		_DepthTex ("Depth Texture", 2D) = "black" {}
		_PreviewTex ("Preview Texture", 2D) = "black" {}
		_AuxExpTex ("Auxiliary Texture", 2D) = "black" {}
		_AuxFocusTex ("Aux Focus Texture", 2D) = "black" {}
		_FocusTex ("Focus Point Texture", 2D) = "black" {}
		_SymbolTex0 ("Symbols Texture", 2D) = "black" {}
		_SymbolTex1 ("Indicators Texture", 2D) = "black" {}
		_NumTex ("Number Sprite", 2D) = "black" {}
		_MainTex ("Shader blocked texture", 2D) = "white" {}
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

		Pass
		{
			//ZTest LEqual
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#include "UnityCG.cginc"
			
			
			struct appdata
			{
				float2 uv : TEXCOORD0;
				float4 vertex : POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID //SPS-I
			};

            struct v2f
            {
                float2 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
				UNITY_VERTEX_OUTPUT_STEREO //SPS-I
            };
			
			sampler2D _PreviewTex, _RenderTex, _DepthTex, _AuxFocusTex, _AuxExpTex;
			uniform float4 _RenderTex_TexelSize;
			uniform bool _EnableDoF, _AperturePriority;
			uniform bool _IsDesktopMode, _PreviewOrientation;
			uniform int _IsDirectStream;
			uniform float _FocusDistance, _DoFStrength, _FocalLength, _ExposureValue, _SensorScale;
			
			uniform float _FocusOffsetH, _FocusOffsetV, _PreviewPosMode;
			uniform int _IsExternalFocus, _ImageFilter, _TonemapMode, _ExposureMode, _IsSideBySide3D;
			
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
				float isCam = abs(unity_CameraProjection._m11 - 1.73205) < 0.00001;
				isCam *= unity_OrthoParams.w == 0.0 && _ProjectionParams.y < 0.050001;
				isCam += unity_OrthoParams.w == 1.0 &&(unity_OrthoParams.y == -0.007 || unity_OrthoParams.y == -0.006); // Preview+Exposure passthrough
				isCam *= all(unity_CameraProjection._m20_m21 == 0.0);
				
				o.pos = mul(UNITY_MATRIX_P, v.vertex * float4(16,16,0,1) * isCam + float4(0.0, 0.0, -0.001 -_ProjectionParams.y, 0.0));
				float4 rawPos = ComputeNonStereoScreenPos(o.pos);
				float2 scrUVRaw = rawPos.xy / rawPos.w;
                o.uv = scrUVRaw;
#endif
                return o;
			}

			
			float getBlurSize(float x, float fLen, float FNum, float focusDist) {
				// f = fLen
				// s = focusDist
				// F = (F-Number)
				// w = filmHeight (35mm film height)
				// Coeff = (cf)^2/2csFw
				float k = fLen*fLen / (2.0 * focusDist * FNum * 0.020); // It's actually 0.02025 but a tiny crop makes sense
				float coc = _SensorScale * k * (x - focusDist) / x;
				//float coc = (1.0 / focusDist - 1.0 / depth) * focusScale;
				//return coc; // Unit: Normalized (0, 1)
				return coc;
				/*
				const float maxCoC = (44.0 + 0.5) * 0.000925925926;
				return clamp(coc, -maxCoC, maxCoC);
				*/
				// (9 * RADIUS SCALE + 0.5) / 1080
			}
			
#define FAR_PLANE 8000
#define NEAR_PLANE 0.08
			float sampleEyeDepth(float rawdepth) {
				float x, y, z, w;
				x = 1.0 - NEAR_PLANE / FAR_PLANE;
				y = NEAR_PLANE / FAR_PLANE;
				z = x / NEAR_PLANE;
				w = y / NEAR_PLANE;
				return 1.0 / (z * rawdepth + w);
			}
#undef FAR_PLANE
#undef NEAR_PLANE
			
			half smin(half a, half b, half k)
			{
				half h = max(k - abs(a - b), 0.0) / k;
				return min(a, b) - h*h * k * 0.25;
			}
			float mapValue(float a, float b, float x)
			{
				return saturate((x - a)/(b - a));
			}
			bool3 isNotNumber(half3 col) {
				return ( col < 0.0 || col > 0.0 || col == 0.0 ) ? false : true;
			}
			bool bounds(half2 uv) {
				return abs(uv.x - 0.5) < 0.5 && abs(uv.y - 0.5) < 0.5;
			}
			
			half4 frag(v2f i) : SV_Target
			{
#ifdef USING_STEREO_MATRICES
				return half4(0,0,0,0);
#else
				// ---- Desktop Focus Control ----
				
				float2 focusPos = tex2D(_AuxFocusTex, 0.0).rg + 0.5;
				float2 focusOffset = _PreviewOrientation == 0 ? float2(_FocusOffsetH, _FocusOffsetV) : float2(_FocusOffsetV, _FocusOffsetH * -1.0);
				focusPos = _IsDesktopMode ? focusOffset * 0.5 + 0.5 : focusPos;
				focusPos.x = _IsSideBySide3D > 0 ? min(focusPos.x - 0.25, 0.499479167) : focusPos.x; // 959/1920
				bool isAutoFocus = _FocusDistance < 0.5001;
				//float realAperture = _DoFStrength * lerp(sqrt(_FocalLength / lerp(48.0, 50.0, mapValue(24.0, 50.0, _FocalLength))), 1.0, _AperturePriority);
				float realAperture = _DoFStrength * lerp(_FocalLength * 0.02, 1.0, _AperturePriority);
				
				half4 cSampleUV = tex2D(_RenderTex, i.uv) * half4(1.0, 1.0, 1.0, 0.00390625);
				half4 cSamplePoint = tex2D(_RenderTex, focusPos) * half4(1.0, 1.0, 1.0, 0.00390625);
				// Use separate depth
				cSampleUV.a = SAMPLE_DEPTH_TEXTURE(_DepthTex, i.uv);
				cSamplePoint.a = SAMPLE_DEPTH_TEXTURE(_DepthTex, focusPos);
				
				float focusDist = lerp(_FocusDistance, min(sampleEyeDepth(cSamplePoint.a), 4096.0), isAutoFocus);
				if(isAutoFocus && _IsExternalFocus == 1) focusDist = tex2Dlod(_AuxExpTex, half4(.75,.25,.0,.0)).x;
				if(isAutoFocus && _IsExternalFocus == 2) focusDist = sampleEyeDepth(tex2Dlod(_AuxExpTex, half4(.25,.75,.0,.0)).z * 0.00390625);
				
				// Rendering
				bool isWindowed = _IsDesktopMode && _PreviewPosMode < 2.0; // Resist WriteDefaults off blend tree quirks
				bool isExtCam = unity_OrthoParams.w == 1.0 && (unity_OrthoParams.y == -0.007 || unity_OrthoParams.y == -0.006);
				bool isRender = (isWindowed && all(_ScreenParams.xy == half2(1920, 1080) || _ScreenParams.xy == _RenderTex_TexelSize.zw))
					|| (!isWindowed && (any(_ScreenParams.xy != half2(1024, 512)) || _IsDirectStream == 1) );
				
				half previewLerp = smoothstep(0, 1, _PreviewPosMode);
				half2 uv = isWindowed && !isExtCam && !isRender ?
					lerp(lerp(
					(i.uv - 0.5) * half2(1.0, 1.777777777) * _ScreenParams.xy / _ScreenParams.y + 0.5,
					(i.uv - 1.0) * half2(1.8, 3.2) * _ScreenParams.xy / _ScreenParams.y + 1.0, previewLerp), lerp(
					(i.uv.yx * half2(1.0, -1.0) - half2(0.5, -0.5)) * half2(1.0, 1.777777777) * _ScreenParams.yx / _ScreenParams.y + 0.5,
					(i.uv.yx * half2(1.0, -1.0) - half2(1.0, -1.0)) * half2(1.8, 3.2) * _ScreenParams.yx / _ScreenParams.y + half2(1.0, 0.0), previewLerp), _PreviewOrientation)
					: i.uv;
				clip(bounds((uv-0.5)/half2(1.018,1.032)+0.5)-1);
				
				half3 incol = isExtCam || isRender ? cSampleUV.rgb : tex2D(_PreviewTex, uv).rgb;
				
				float eyeDepthUV = sampleEyeDepth(cSampleUV.a);
				half4 col = half4(isNotNumber(incol) ? 0.0 : clamp(incol, 0.0, 64000.0),
					getBlurSize(eyeDepthUV, 0.001 * _FocalLength, realAperture, focusDist) * 1080.0);
				
				// Full or preview render filters
				if((isExtCam || isRender) && any(_ScreenParams.xy != half2(512, 256))) { // Sensor Copy Resolution
					half2 expData = _ExposureMode ? tex2Dlod(_AuxExpTex, half4(.25,.25,.0,.0)).rg : 1.0; // R=Peak G=Mean
					expData.g = _ExposureMode ? smin(_TonemapMode ? 1.0 : 1.25, expData.g, 0.25) : 1.0;
					expData  *= exp2(-_ExposureValue);
					
					half3 c = col.rgb;
					half m = dot(c, half3(0.2126, 0.7152, 0.0722));
					
					half d1 = smoothstep(0.2, 0.4, abs(focusDist - eyeDepthUV));
					half d2 = smoothstep(0.0, 1.6, abs(focusDist - eyeDepthUV)) * 0.4 + 0.15;
					half d3 = smoothstep(0.2, 0.4, abs(focusDist - eyeDepthUV)) * 0.25;
					c = _ImageFilter != 1 ? c : lerp(c, m * 0.666666666, d1);
					c = _ImageFilter != 2 ? c : lerp(c, expData.g * lerp(c, lerp(m, 1.0, 0.75), 0.5) * half3(1.25, 0.25, 1.0), d2);
					c = _ImageFilter != 3 ? c : lerp(c, expData.g * 1.0, d3);
					
					col.rgb = c;
				} else {
					col.a = 1.0;
				}
				
				col = bounds(uv) ? col : half4(0.8, 0.8, 0.8, 1.0);
				
				return col;
#endif
			}
			
			ENDCG
		}
		
		GrabPass
		{
			"_HirabikiVRCLensPassTex_One"
		}
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#include "UnityCG.cginc"

			sampler2D _HirabikiVRCLensPassTex_One;
			uniform bool _EnableDoF;
			uniform float _SensorScale;
			uniform int _IsSideBySide3D;
			#include "DepthOfFieldInclude.cginc"
			//VRCLXT
			#include "DoF_LensShape.cginc"
			//
			
			// All alignments: 7, 20, 39, 64, 96, 134, 178, 228, [285], 348
			uniform int _BlurSamples;
			static const half2 diskKernel[348] = { // Generated in software
				//*
				half2( 0.2915, 0.9566), half2( 0.9618, 0.2736), half2( 0.8307,-0.5567), half2( 0.0701,-0.9975), half2(-0.7297,-0.6838), half2(-0.9883, 0.1525), half2(-0.4932, 0.8699), 
				half2(-0.0401, 1.9996), half2( 0.9380, 1.7664), half2( 1.5927, 1.2096), half2( 1.9767, 0.3042), half2( 1.8761,-0.6931), half2( 1.3799,-1.4477), half2( 0.5391,-1.9260), half2(-0.3840,-1.9628), half2(-1.2645,-1.5495), half2(-1.8300,-0.8070), half2(-1.9902, 0.1979), half2(-1.6712, 1.0987), half2(-1.0148, 1.7234), 
				half2( 0.8369, 2.8809), half2( 1.6837, 2.4830), half2( 2.4530, 1.7271), half2( 2.8689, 0.8771), half2( 2.9966,-0.1429), half2( 2.8079,-1.0562), half2( 2.3039,-1.9214), half2( 1.5617,-2.5615), half2( 0.6011,-2.9392), half2(-0.3258,-2.9823), half2(-1.2928,-2.7072), half2(-2.1451,-2.0973), half2(-2.6887,-1.3307), half2(-2.9779,-0.3637), half2(-2.9440, 0.5770), half2(-2.5923, 1.5100), half2(-1.9465, 2.2828), half2(-1.1460, 2.7725), half2(-0.1807, 2.9946), 
				half2( 0.9160, 3.8937), half2( 1.8341, 3.5547), half2( 2.6801, 2.9693), half2( 3.3118, 2.2431), half2( 3.7776, 1.3153), half2( 3.9905, 0.2755), half2( 3.9319,-0.7349), half2( 3.6232,-1.6948), half2( 3.1224,-2.5001), half2( 2.4290,-3.1780), half2( 1.5789,-3.6752), half2( 0.6034,-3.9542), half2(-0.4031,-3.9796), half2(-1.4274,-3.7367), half2(-2.2646,-3.2972), half2(-3.0540,-2.5832), half2(-3.6137,-1.7151), half2(-3.9201,-0.7956), half2(-3.9968, 0.1608), half2(-3.8118, 1.2127), half2(-3.4317, 2.0552), half2(-2.7793, 2.8767), half2(-1.9313, 3.5028), half2(-1.1106, 3.8427), half2(-0.1019, 3.9987), 
				half2( 0.8806, 4.9218), half2( 1.8064, 4.6623), half2( 2.6856, 4.2175), half2( 3.3921, 3.6734), half2( 4.0498, 2.9324), half2( 4.5555, 2.0609), half2( 4.8675, 1.1432), half2( 4.9987, 0.1149), half2( 4.9292,-0.8386), half2( 4.6695,-1.7878), half2( 4.2567,-2.6231), half2( 3.6270,-3.4416), half2( 2.9338,-4.0488), half2( 2.0104,-4.5780), half2( 1.1202,-4.8729), half2( 0.1392,-4.9981), half2(-0.8404,-4.9289), half2(-1.7846,-4.6707), half2(-2.7061,-4.2044), half2(-3.4550,-3.6142), half2(-4.0763,-2.8954), half2(-4.5707,-2.0270), half2(-4.8639,-1.1587), half2(-4.9962,-0.1956), half2(-4.9389, 0.7790), half2(-4.6696, 1.7873), half2(-4.2527, 2.6295), half2(-3.6162, 3.4529), half2(-2.8514, 4.1072), half2(-2.0910, 4.5418), half2(-1.0698, 4.8842), half2(-0.2015, 4.9959), 
				half2( 0.3987, 5.9867), half2( 1.4628, 5.8190), half2( 2.4083, 5.4954), half2( 3.2049, 5.0723), half2( 4.0344, 4.4411), half2( 4.7182, 3.7066), half2( 5.2350, 2.9317), half2( 5.6435, 2.0373), half2( 5.9057, 1.0596), half2( 5.9994, 0.0872), half2( 5.9191,-0.9818), half2( 5.7031,-1.8639), half2( 5.2967,-2.8187), half2( 4.7960,-3.6054), half2( 4.1200,-4.3619), half2( 3.3542,-4.9749), half2( 2.4662,-5.4697), half2( 1.5039,-5.8085), half2( 0.5234,-5.9771), half2(-0.4568,-5.9826), half2(-1.3964,-5.8352), half2(-2.3888,-5.5040), half2(-3.2445,-5.0471), half2(-3.9837,-4.4866), half2(-4.7291,-3.6926), half2(-5.2718,-2.8651), half2(-5.6450,-2.0332), half2(-5.9152,-1.0054), half2(-5.9999,-0.0423), half2(-5.9365, 0.8707), half2(-5.7005, 1.8719), half2(-5.3328, 2.7497), half2(-4.7487, 3.6674), half2(-4.1418, 4.3411), half2(-3.3761, 4.9601), half2(-2.4440, 5.4797), half2(-1.5726, 5.7903), half2(-0.5104, 5.9783), 
				half2( 0.4730, 6.9840), half2( 1.4283, 6.8527), half2( 2.3878, 6.5802), half2( 3.3235, 6.1607), half2( 4.1739, 5.6195), half2( 4.9667, 4.9327), half2( 5.5949, 4.2068), half2( 6.1197, 3.3985), half2( 6.5543, 2.4579), half2( 6.8364, 1.5046), half2( 6.9821, 0.4999), half2( 6.9801,-0.5272), half2( 6.8485,-1.4484), half2( 6.5542,-2.4582), half2( 6.1295,-3.3808), half2( 5.6367,-4.1506), half2( 4.9724,-4.9270), half2( 4.1780,-5.6165), half2( 3.4116,-6.1123), half2( 2.4360,-6.5625), half2( 1.5049,-6.8363), half2( 0.5506,-6.9783), half2(-0.4611,-6.9848), half2(-1.4136,-6.8558), half2(-2.4801,-6.5459), half2(-3.3031,-6.1716), half2(-4.2045,-5.5966), half2(-4.9506,-4.9489), half2(-5.6027,-4.1964), half2(-6.1295,-3.3807), half2(-6.5484,-2.4735), half2(-6.8368,-1.5027), half2(-6.9826,-0.4927), half2(-6.9791, 0.5407), half2(-6.8471, 1.4551), half2(-6.5589, 2.4457), half2(-6.1280, 3.3833), half2(-5.6093, 4.1875), half2(-4.9526, 4.9469), half2(-4.2400, 5.5698), half2(-3.3333, 6.1554), half2(-2.4538, 6.5558), half2(-1.5637, 6.8231), half2(-0.5008, 6.9821), 
				half2( 0.8383, 7.9560), half2( 1.8313, 7.7876), half2( 2.7671, 7.5062), half2( 3.7715, 7.0552), half2( 4.5295, 6.5942), half2( 5.3222, 5.9728), half2( 6.1036, 5.1716), half2( 6.6438, 4.4564), half2( 7.1876, 3.5126), half2( 7.5419, 2.6684), half2( 7.8260, 1.6596), half2( 7.9719, 0.6694), half2( 7.9945,-0.2961), half2( 7.8746,-1.4111), half2( 7.6576,-2.3156), half2( 7.3232,-3.2204), half2( 6.8580,-4.1192), half2( 6.2819,-4.9535), half2( 5.6228,-5.6907), half2( 4.8449,-6.3661), half2( 3.9331,-6.9664), half2( 3.0947,-7.3772), half2( 2.1243,-7.7128), half2( 1.1030,-7.9236), half2( 0.1895,-7.9978), half2(-0.8610,-7.9535), half2(-1.8659,-7.7794), half2(-2.8332,-7.4815), half2(-3.7218,-7.0815), half2(-4.5583,-6.5743), half2(-5.3950,-5.9071), half2(-6.0512,-5.2328), half2(-6.6660,-4.4232), half2(-7.1852,-3.5176), half2(-7.5724,-2.5806), half2(-7.8191,-1.6916), half2(-7.9764,-0.6143), half2(-7.9925, 0.3469), half2(-7.8893, 1.3265), half2(-7.6376, 2.3805), half2(-7.2953, 3.2830), half2(-6.8207, 4.1806), half2(-6.2438, 5.0014), half2(-5.5420, 5.7694), half2(-4.8478, 6.3639), half2(-3.9908, 6.9335), half2(-3.0615, 7.3910), half2(-2.0766, 7.7258), half2(-1.1547, 7.9162), half2(-0.0918, 7.9995), 
				half2(-0.0532, 8.9998), half2( 0.9119, 8.9537), half2( 1.8932, 8.7986), half2( 2.9267, 8.5108), half2( 3.7465, 8.1831), half2( 4.7218, 7.6619), half2( 5.5010, 7.1231), half2( 6.2076, 6.5166), half2( 6.8920, 5.7879), half2( 7.5092, 4.9610), half2( 7.9996, 4.1239), half2( 8.3981, 3.2359), half2( 8.7009, 2.3010), half2( 8.8999, 1.3383), half2( 8.9955, 0.2849), half2( 8.9691,-0.7454), half2( 8.8434,-1.6714), half2( 8.6180,-2.5944), half2( 8.2701,-3.5504), half2( 7.8446,-4.4116), half2( 7.3054,-5.2565), half2( 6.6905,-6.0197), half2( 5.9036,-6.7932), half2( 5.1857,-7.3558), half2( 4.3674,-7.8693), half2( 3.3895,-8.3373), half2( 2.4597,-8.6574), half2( 1.4805,-8.8774), half2( 0.5513,-8.9831), half2(-0.4462,-8.9889), half2(-1.4679,-8.8795), half2(-2.4294,-8.6659), half2(-3.3124,-8.3683), half2(-4.2618,-7.9270), half2(-5.1112,-7.4078), half2(-5.8851,-6.8093), half2(-6.5585,-6.1632), half2(-7.2374,-5.3498), half2(-7.7899,-4.5074), half2(-8.2085,-3.6906), half2(-8.5791,-2.7202), half2(-8.8280,-1.7510), half2(-8.9676,-0.7625), half2(-8.9971, 0.2273), half2(-8.9159, 1.2275), half2(-8.7290, 2.1920), half2(-8.4602, 3.0701), half2(-8.0476, 4.0293), half2(-7.5526, 4.8948), half2(-7.0109, 5.6434), half2(-6.3446, 6.3833), half2(-5.5390, 7.0936), half2(-4.7090, 7.6698), half2(-3.8296, 8.1446), half2(-2.9439, 8.5049), half2(-2.0236, 8.7696), half2(-1.0463, 8.9390), 
				half2( 0.2034, 9.9979), half2( 1.1253, 9.9365), half2( 2.1849, 9.7584), half2( 3.1327, 9.4966), half2( 3.9819, 9.1730), half2( 4.8902, 8.7228), half2( 5.7644, 8.1714), half2( 6.5533, 7.5534), half2( 7.2952, 6.8396), half2( 7.9182, 6.1076), half2( 8.5045, 5.2606), half2( 8.9920, 4.3753), half2( 9.3556, 3.5317), half2( 9.6561, 2.5998), half2( 9.8794, 1.5481), half2( 9.9852, 0.5431), half2( 9.9913,-0.4169), half2( 9.8948,-1.4467), half2( 9.7042,-2.4141), half2( 9.4185,-3.3603), half2( 9.0318,-4.2926), half2( 8.5966,-5.1087), half2( 7.9966,-6.0045), half2( 7.4139,-6.7107), half2( 6.6800,-7.4416), half2( 5.8833,-8.0862), half2( 5.0547,-8.6284), half2( 4.2200,-9.0660), half2( 3.2756,-9.4483), half2( 2.3539,-9.7190), half2( 1.3855,-9.9036), half2( 0.2889,-9.9958), half2(-0.6905,-9.9761), half2(-1.6733,-9.8590), half2(-2.5828,-9.6607), half2(-3.5317,-9.3556), half2(-4.5273,-8.9165), half2(-5.3315,-8.4602), half2(-6.1903,-7.8537), half2(-6.8834,-7.2538), half2(-7.5628,-6.5424), half2(-8.2309,-5.6791), half2(-8.7411,-4.8572), half2(-9.1639,-4.0030), half2(-9.5159,-3.0737), half2(-9.7885,-2.0458), half2(-9.9437,-1.0600), half2(-9.9998,-0.0612), half2(-9.9595, 0.8990), half2(-9.8175, 1.9018), half2(-9.5935, 2.8221), half2(-9.2440, 3.8142), half2(-8.8323, 4.6893), half2(-8.2864, 5.5978), half2(-7.7101, 6.3683), half2(-7.0496, 7.0925), half2(-6.2983, 7.7673), half2(-5.5358, 8.3280), half2(-4.6334, 8.8618), half2(-3.6988, 9.2908), half2(-2.8058, 9.5983), half2(-1.7937, 9.8378), half2(-0.8154, 9.9667)
				/*/
				half2( 0.5093, 0.8606), half2( 0.9904, 0.1383), half2( 0.7257,-0.6881), half2(-0.0855,-0.9963), half2(-0.8323,-0.5544), half2(-0.9523, 0.3051), half2(-0.3553, 0.9348), 
				half2( 0.1447, 1.9948), half2( 1.0551, 1.6990), half2( 1.7239, 1.0141), half2( 1.9977, 0.0968), half2( 1.8138,-0.8426), half2( 1.2145,-1.5891), half2( 0.3369,-1.9714), half2(-0.6179,-1.9022), half2(-1.4311,-1.3971), half2(-1.9164,-0.5721), half2(-1.9628, 0.3841), half2(-1.5595, 1.2522), half2(-0.7989, 1.8335), 
				half2( 0.7667, 2.9004), half2( 1.6669, 2.4943), half2( 2.3865, 1.8179), half2( 2.8475, 0.9445), half2( 2.9998,-0.0313), half2( 2.8271,-1.0036), half2( 2.3481,-1.8672), half2( 1.6146,-2.5285), half2( 0.7061,-2.9157), half2(-0.2789,-2.9870), half2(-1.2337,-2.7346), half2(-2.0547,-2.1859), half2(-2.6532,-1.4003), half2(-2.9641,-0.4629), half2(-2.9538, 0.5246), half2(-2.6234, 1.4553), half2(-2.0087, 2.2282), half2(-1.1764, 2.7597), half2(-0.2165, 2.9922), 
				half2( 0.8938, 3.8989), half2( 1.8353, 3.5541), half2( 2.6615, 2.9860), half2( 3.3205, 2.2303), half2( 3.7708, 1.3345), half2( 3.9842, 0.3548), half2( 3.9473,-0.6472), half2( 3.6623,-1.6085), half2( 3.1472,-2.4688), half2( 2.4344,-3.1739), half2( 1.5686,-3.6796), half2( 0.6043,-3.9541), half2(-0.3981,-3.9801), half2(-1.3754,-3.7561), half2(-2.2663,-3.2961), half2(-3.0148,-2.6289), half2(-3.5738,-1.7966), half2(-3.9084,-0.8513), half2(-3.9973, 0.1474), half2(-3.8351, 1.1368), half2(-3.4319, 2.0548), half2(-2.8130, 2.8438), half2(-2.0174, 3.4540), half2(-1.0951, 3.8472), half2(-0.1039, 3.9987), 
				half2( 0.8333, 4.9301), half2( 1.7791, 4.6728), half2( 2.6565, 4.2359), half2( 3.4318, 3.6363), half2( 4.0753, 2.8969), half2( 4.5622, 2.0462), half2( 4.8737, 1.1168), half2( 4.9979, 0.1445), half2( 4.9301,-0.8333), half2( 4.6728,-1.7791), half2( 4.2359,-2.6565), half2( 3.6363,-3.4318), half2( 2.8969,-4.0753), half2( 2.0462,-4.5622), half2( 1.1168,-4.8737), half2( 0.1445,-4.9979), half2(-0.8333,-4.9301), half2(-1.7791,-4.6728), half2(-2.6565,-4.2359), half2(-3.4318,-3.6363), half2(-4.0753,-2.8969), half2(-4.5622,-2.0462), half2(-4.8737,-1.1168), half2(-4.9979,-0.1445), half2(-4.9301, 0.8333), half2(-4.6728, 1.7791), half2(-4.2359, 2.6565), half2(-3.6363, 3.4318), half2(-2.8969, 4.0753), half2(-2.0462, 4.5622), half2(-1.1168, 4.8737), half2(-0.1445, 4.9979), 
				half2( 0.3855, 5.9876), half2( 1.3657, 5.8425), half2( 2.3087, 5.5380), half2( 3.1888, 5.0825), half2( 3.9818, 4.4883), half2( 4.6663, 3.7717), half2( 5.2234, 2.9522), half2( 5.6381, 2.0522), half2( 5.8990, 1.0962), half2( 5.9990, 0.1103), half2( 5.9353,-0.8786), half2( 5.7098,-1.8435), half2( 5.3285,-2.7582), half2( 4.8018,-3.5976), half2( 4.1442,-4.3389), half2( 3.3735,-4.9618), half2( 2.5108,-5.4494), half2( 1.5796,-5.7883), half2( 0.6053,-5.9694), half2(-0.3855,-5.9876), half2(-1.3657,-5.8425), half2(-2.3087,-5.5380), half2(-3.1888,-5.0825), half2(-3.9818,-4.4883), half2(-4.6663,-3.7717), half2(-5.2234,-2.9522), half2(-5.6381,-2.0522), half2(-5.8990,-1.0962), half2(-5.9990,-0.1103), half2(-5.9353, 0.8786), half2(-5.7098, 1.8435), half2(-5.3285, 2.7582), half2(-4.8018, 3.5976), half2(-4.1442, 4.3389), half2(-3.3735, 4.9618), half2(-2.5108, 5.4494), half2(-1.5796, 5.7883), half2(-0.6053, 5.9694), 
				half2( 0.4761, 6.9838), half2( 1.4651, 6.8450), half2( 2.4243, 6.5668), half2( 3.3342, 6.1549), half2( 4.1762, 5.6178), half2( 4.9332, 4.9662), half2( 5.5898, 4.2136), half2( 6.1325, 3.3752), half2( 6.5504, 2.4681), half2( 6.8350, 1.5108), half2( 6.9805, 0.5227), half2( 6.9838,-0.4761), half2( 6.8450,-1.4651), half2( 6.5668,-2.4243), half2( 6.1549,-3.3342), half2( 5.6178,-4.1762), half2( 4.9662,-4.9332), half2( 4.2136,-5.5898), half2( 3.3752,-6.1325), half2( 2.4681,-6.5504), half2( 1.5108,-6.8350), half2( 0.5227,-6.9805), half2(-0.4761,-6.9838), half2(-1.4651,-6.8450), half2(-2.4243,-6.5668), half2(-3.3342,-6.1549), half2(-4.1762,-5.6178), half2(-4.9332,-4.9662), half2(-5.5898,-4.2136), half2(-6.1325,-3.3752), half2(-6.5504,-2.4681), half2(-6.8350,-1.5108), half2(-6.9805,-0.5227), half2(-6.9838, 0.4761), half2(-6.8450, 1.4651), half2(-6.5668, 2.4243), half2(-6.1549, 3.3342), half2(-5.6178, 4.1762), half2(-4.9662, 4.9332), half2(-4.2136, 5.5898), half2(-3.3752, 6.1325), half2(-2.4681, 6.5504), half2(-1.5108, 6.8350), half2(-0.5227, 6.9805), 
				half2( 0.3091, 7.9940), half2( 1.3086, 7.8923), half2( 2.2874, 7.6660), half2( 3.2302, 7.3189), half2( 4.1220, 6.8563), half2( 4.9488, 6.2856), half2( 5.6976, 5.6158), half2( 6.3565, 4.8574), half2( 6.9152, 4.0225), half2( 7.3648, 3.1240), half2( 7.6983, 2.1763), half2( 7.9103, 1.1943), half2( 7.9977, 0.1935), half2( 7.9588,-0.8104), half2( 7.7945,-1.8015), half2( 7.5073,-2.7642), half2( 7.1016,-3.6834), half2( 6.5840,-4.5444), half2( 5.9625,-5.3337), half2( 5.2470,-6.0390), half2( 4.4487,-6.6490), half2( 3.5803,-7.1541), half2( 2.6554,-7.5464), half2( 1.6887,-7.8197), half2( 0.6953,-7.9697), half2(-0.3091,-7.9940), half2(-1.3086,-7.8923), half2(-2.2874,-7.6660), half2(-3.2302,-7.3189), half2(-4.1220,-6.8563), half2(-4.9488,-6.2856), half2(-5.6976,-5.6158), half2(-6.3565,-4.8574), half2(-6.9152,-4.0224), half2(-7.3648,-3.1240), half2(-7.6983,-2.1763), half2(-7.9103,-1.1943), half2(-7.9977,-0.1935), half2(-7.9588, 0.8104), half2(-7.7945, 1.8015), half2(-7.5073, 2.7642), half2(-7.1016, 3.6834), half2(-6.5840, 4.5444), half2(-5.9625, 5.3337), half2(-5.2470, 6.0390), half2(-4.4487, 6.6490), half2(-3.5803, 7.1541), half2(-2.6554, 7.5464), half2(-1.6887, 7.8197), half2(-0.6953, 7.9697), 
				half2( 0.7854, 8.9657), half2( 1.7669, 8.8249), half2( 2.7270, 8.5769), half2( 3.6540, 8.2249), half2( 4.5366, 7.7730), half2( 5.3642, 7.2267), half2( 6.1266, 6.5928), half2( 6.8147, 5.8788), half2( 7.4200, 5.0934), half2( 7.9353, 4.2463), half2( 8.3543, 3.3475), half2( 8.6718, 2.4082), half2( 8.8841, 1.4396), half2( 8.9886, 0.4535), half2( 8.9839,-0.5380), half2( 8.8702,-1.5231), half2( 8.6488,-2.4896), half2( 8.3224,-3.4260), half2( 7.8950,-4.3207), half2( 7.3718,-5.1630), half2( 6.7591,-5.9426), half2( 6.0643,-6.6501), half2( 5.2960,-7.2769), half2( 4.4633,-7.8153), half2( 3.5765,-8.2589), half2( 2.6462,-8.6022), half2( 1.6838,-8.8411), half2( 0.7010,-8.9727), half2(-0.2903,-8.9953), half2(-1.2781,-8.9088), half2(-2.2504,-8.7141), half2(-3.1953,-8.4137), half2(-4.1015,-8.0111), half2(-4.9579,-7.5113), half2(-5.7541,-6.9203), half2(-6.4805,-6.2453), half2(-7.1282,-5.4945), half2(-7.6894,-4.6769), half2(-8.1572,-3.8027), half2(-8.5260,-2.8822), half2(-8.7913,-1.9268), half2(-8.9499,-0.9480), half2(-8.9999, 0.0423), half2(-8.9406, 1.0321), half2(-8.7728, 2.0094), half2(-8.4985, 2.9623), half2(-8.1211, 3.8792), half2(-7.6450, 4.7491), half2(-7.0762, 5.5613), half2(-6.4215, 6.3059), half2(-5.6888, 6.9741), half2(-4.8870, 7.5576), half2(-4.0260, 8.0493), half2(-3.1161, 8.4433), half2(-2.1683, 8.7349), half2(-1.1942, 8.9204), half2(-0.2057, 8.9976), 
				half2( 0.9075, 9.9587), half2( 1.8946, 9.8189), half2( 2.8628, 9.5815), half2( 3.8026, 9.2488), half2( 4.7046, 8.8242), half2( 5.5598, 8.3119), half2( 6.3598, 7.7171), half2( 7.0966, 7.0455), half2( 7.7628, 6.3039), half2( 8.3519, 5.4996), half2( 8.8580, 4.6407), half2( 9.2760, 3.7357), half2( 9.6019, 2.7935), half2( 9.8323, 1.8236), half2( 9.9650, 0.8356), half2( 9.9987,-0.1608), half2( 9.9330,-1.1555), half2( 9.7686,-2.1388), half2( 9.5071,-3.1008), half2( 9.1511,-4.0320), half2( 8.7042,-4.9231), half2( 8.1707,-5.7653), half2( 7.5561,-6.5502), half2( 6.8664,-7.2700), half2( 6.1084,-7.9176), half2( 5.2897,-8.4864), half2( 4.4184,-8.9709), half2( 3.5033,-9.3663), half2( 2.5533,-9.6685), half2( 1.5779,-9.8747), half2( 0.5869,-9.9828), half2(-0.4100,-9.9916), half2(-1.4028,-9.9011), half2(-2.3817,-9.7122), half2(-3.3369,-9.4268), half2(-4.2589,-9.0477), half2(-5.1386,-8.5787), half2(-5.9672,-8.0245), half2(-6.7366,-7.3905), half2(-7.4389,-6.6830), half2(-8.0674,-5.9091), half2(-8.6156,-5.0765), half2(-9.0783,-4.1934), half2(-9.4507,-3.2687), half2(-9.7292,-2.3114), half2(-9.9110,-1.3312), half2(-9.9943,-0.3378), half2(-9.9783, 0.6590), half2(-9.8631, 1.6492), half2(-9.6498, 2.6231), half2(-9.3407, 3.5709), half2(-8.9388, 4.4831), half2(-8.4480, 5.3509), half2(-7.8732, 6.1654), half2(-7.2202, 6.9187), half2(-6.4954, 7.6032), half2(-5.7061, 8.2122), half2(-4.8601, 8.7395), half2(-3.9658, 9.1800), half2(-3.0320, 9.5293), half2(-2.0682, 9.7838), half2(-1.0837, 9.9411), half2(-0.0885, 9.9996)
				*/
			};
			
			half3 depthOfField(half2 texCoord) {
#define RENDER_RAD_SCALE 4.000000000
				// Adapted from http://tuxedolabs.blogspot.com/2018/05/bokeh-depth-of-field-in-single-pass.html
				half4 color = tex2D(_HirabikiVRCLensPassTex_One, texCoord);
				half centerSize = abs(color.a);
				half acc = 1.0;
				
				int finalCount = all(_ScreenParams.xy == half2(1024, 512)) ? 64 : _BlurSamples;
				half radiusScale = all(_ScreenParams.xy == half2(1024, 512)) ? RENDER_RAD_SCALE * 2.25 : RENDER_RAD_SCALE;
				
				half borderClamp = 0.5 + sign(texCoord.x - 0.5) * 0.000520833333; // 0.5 + 0.5 * 1/1920
				for (int i = 0; i < finalCount; i++) {
#ifdef VRCLENS_ADDON_A
					half2 tc = texCoord + scaleSampling(scaleLerp(diskKernel[i])) * radiusScale;
#else
					half2 tc = texCoord + diskKernel[i] * radiusScale * half2(0.000520833333, 0.000925925926); // 1920, 1080
#endif
					
					tc.x = _IsSideBySide3D > 0 ? lerp(min(tc.x, borderClamp), max(tc.x, borderClamp), borderClamp > 0.5) : tc.x;
					
					half4 sampled = tex2D(_HirabikiVRCLensPassTex_One, tc);
					float sampleSize = lerp(abs(sampled.a), min(abs(sampled.a), centerSize * 2.0), sampled.a > centerSize);
					
					//float radius = lengthLerp(diskKernel[i]) * radiusScale;
					float radius = length(diskKernel[i]) * radiusScale;
					
					half2 edge = half2(max(0.0, radius - radiusScale * 0.5), radius + radiusScale * 0.5);
					float m = saturate((sampleSize - edge.x) / (edge.y - edge.x));
					color.rgb += lerp(color.rgb / acc, sampled.rgb, m);
					acc += 1.0;
				}
				return color.rgb / acc;
			}
			
			half4 frag(v2f i) : SV_Target
			{
#ifdef USING_STEREO_MATRICES
				return half4(0,0,0,0);
#else
				half4 rawColor = tex2D(_HirabikiVRCLensPassTex_One, i.uv);
				half4 col = rawColor;
				if(_EnableDoF) {
					col = half4(depthOfField(i.uv), rawColor.a);
				}
				return col;
#endif
			}
			
			ENDCG
		}
		
		GrabPass
		{
			"_HirabikiVRCLensPassTexture"
		}
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#include "UnityCG.cginc"
			#include "NumberDisplayInclude.cginc"
			#include "../Lib/Filament/EVILS.cginc"
			
			sampler2D _MainTex, _HirabikiVRCLensPassTexture, _RenderTex, _DepthTex, _AuxFocusTex, _AuxExpTex;
			sampler2D _FocusTex, _SymbolTex0, _SymbolTex1, _NumTex;
			uniform bool _EnableDoF, _AperturePriority;
			uniform bool _ShowOverlay, _PreviewOrientation;
			uniform float _FocusDistance, _DoFStrength, _TonemapLerp, _ExposureValue, _WhiteBalance, _SensorScale;
			uniform bool _ShowFocusPeeking, _ShowZebra, _ShowRuleOfThirds, _ShowLeveler;
			uniform float _FocusOffsetH, _FocusOffsetV;
			uniform int _IsExternalFocus, _ImageFilter, _TonemapMode, _ExposureMode, _SensorType, _IsSideBySide3D;
			uniform float _IsImageStabilize;
			
			uniform float _FocalLength;
			uniform half4 _FocusPeakingColor;
			
			uniform float _TutorialDisplay;
			
			static const float displayMargins = 0.0625;
#define SAFEAREA ((!_IsDesktopMode) * displayMargins)
			
			
			#include "DepthOfFieldInclude.cginc"
			//VRCLXT
			#include "DoF_LensShape.cginc"
			//
			
			static const half3x3 rgb2yuv = {
				 0.2126,  0.7152,  0.0722,
				-0.09991,-0.33609, 0.436,
				 0.615,  -0.55861,-0.05639};
			static const half3x3 yuv2rgb = {
				1, 0,       1.28033,
				1,-0.21482,-0.38059,
				1, 2.12798, 0};
			//*
			static const float3 blurKernel[69] = {
				                                        float3(-2, 4,   28), float3(-1, 4,  56), float3(0, 4,  70), float3(1, 4,  56), float3(2, 4,  28),
				                    float3(-3, 3,  64), float3(-2, 3,  224), float3(-1, 3, 448), float3(0, 3, 560), float3(1, 3, 448), float3(2, 3, 224), float3(3, 3,  64), 
				float3(-4, 2,  28), float3(-3, 2, 224), float3(-2, 2,  784), float3(-1, 2,1568), float3(0, 2,1960), float3(1, 2,1568), float3(2, 2, 784), float3(3, 2, 224), float3(4, 2,  28), 
				float3(-4, 1,  56), float3(-3, 1, 448), float3(-2, 1, 1568), float3(-1, 1,3136), float3(0, 1,3920), float3(1, 1,3136), float3(2, 1,1568), float3(3, 1, 448), float3(4, 1,  56), 
				float3(-4, 0,  70), float3(-3, 0, 560), float3(-2, 0, 1960), float3(-1, 0,3920), float3(0, 0,4900), float3(1, 0,3920), float3(2, 0,1960), float3(3, 0, 560), float3(4, 0,  70), 
				float3(-4,-1,  56), float3(-3,-1, 448), float3(-2,-1, 1568), float3(-1,-1,3136), float3(0,-1,3920), float3(1,-1,3136), float3(2,-1,1568), float3(3,-1, 448), float3(4,-1,  56), 
				float3(-4,-2,  28), float3(-3,-2, 224), float3(-2,-2,  784), float3(-1,-2,1568), float3(0,-2,1960), float3(1,-2,1568), float3(2,-2, 784), float3(3,-2, 224), float3(4,-2,  28), 
				                    float3(-3,-3,  64), float3(-2,-3,  224), float3(-1,-3, 448), float3(0,-3, 560), float3(1,-3, 448), float3(2,-3, 224), float3(3,-3,  64),
				                                        float3(-2,-4,   28), float3(-1,-4,  56), float3(0,-4,  70), float3(1,-4,  56), float3(2,-4,  28)
			};
			/*/
			static const float2 blurKernel[37] = {
				                              float2(-1, 3), float2(0, 3), float2(1, 3),
				               float2(-2, 2), float2(-1, 2), float2(0, 2), float2(1, 2), float2(2, 2),
				float2(-3, 1), float2(-2, 1), float2(-1, 1), float2(0, 1), float2(1, 1), float2(2, 1), float2(3, 1),
				float2(-3, 0), float2(-2, 0), float2(-1, 0), float2(0, 0), float2(1, 0), float2(2, 0), float2(3, 0),
				float2(-3,-1), float2(-2,-1), float2(-1,-1), float2(0,-1), float2(1,-1), float2(2,-1), float2(3,-1),
				               float2(-2,-2), float2(-1,-2), float2(0,-2), float2(1,-2), float2(2,-2),
				                              float2(-1,-3), float2(0,-3), float2(1,-3)
			};
			*/
			
			half3 depthOfField(half2 texCoord) {
				half4 color = tex2D(_HirabikiVRCLensPassTexture, texCoord);
				float centerSize = abs(color.a);
				float acc = 1.0;
				half hiResOffset = any(_RenderTex_TexelSize.zw != half2(1920, 1080)) * 0.25;
				half borderClamp = 0.5 + sign(texCoord.x - 0.5) * 0.000520833333; // 0.5 + 0.5 * 1/1920
				UNITY_LOOP for (int i = 0; i < 69; i++) {
#ifdef VRCLENS_ADDON_A
					half2 tc = texCoord + scaleSampling(blurKernel[i].xy + hiResOffset);
#else
					half2 tc = texCoord + (blurKernel[i].xy + hiResOffset) * half2(0.000520833333, 0.000925925926); // 1920, 1080
#endif
					tc.x = _IsSideBySide3D > 0 ? lerp(min(tc.x, borderClamp), max(tc.x, borderClamp), borderClamp > 0.5) : tc.x;
					
					half4 sampled = tex2D(_HirabikiVRCLensPassTexture, tc);
					float sampleSize = lerp(abs(sampled.a), min(abs(sampled.a), centerSize * 2.0), sampled.a > centerSize);
					
					float radius = length(blurKernel[i].xy);
					
					half2 edge = half2(radius - 0.25, radius + 0.25);
					float m = saturate((sampleSize - edge.x) / (edge.y - edge.x));
					
					float mult = blurKernel[i].z;
					color.rgb += mult * lerp(color.rgb / acc, sampled.rgb, m);
					//color.rgb = max(lerp(color.rgb, sampled.rgb, m), color.rgb);
					acc += mult;
				}
				return color.rgb / acc;
				return color.rgb;
			}
			
			half smin(half a, half b, half k)
			{
				half h = max(k - abs(a - b), 0.0) / k;
				return min(a, b) - h*h * k * 0.25;
			}
			half4 smin4(half4 a, half4 b, half k)
			{
				half4 h = max(k - abs(a - b), 0.0) / k;
				return min(a, b) - h*h * k * 0.25;
			}

			half3 tm_ACES(half3 col) {
				half lum = dot(col, half3(0.2126, 0.7152, 0.0722));
				half4 x = half4(lerp(col, lum, smoothstep(1.0, 9.0, lum)), lum);
				
				float a = 2.51;
				float b = 0.03;
				float c = 2.43;
				float d = 0.59;
				float e = 0.14;
				half4 y = saturate((x * (a * x + b)) / (x * (c * x + d) + e));
				return lerp(y.rgb, x.rgb * (y.a / x.a), 0.333333333); // RGB/Luminance mix
			}
			half3 tm_HLG(half3 col) {
				float paperWhite = 1.0;
				
				//half lum = dot(col, half3(0.2126, 0.7152, 0.0722));
				half lum = (col.r + col.g + col.b) * 0.333333333;
				half y = 4.0 * lum / paperWhite;
				
				// LINEAR TO HLG
				const float r = 0.5;
				const float a = 0.17883277, b = 0.28466892, c = 0.55991073;
				
				half lumaHLG = y <= 1.0 ? r * sqrt(y) : a*log(y-b)+c;
				// HLG interpreted as sRGB to LINEAR
				half luma = lumaHLG * lumaHLG * paperWhite * 4.0;
				
				half sat = luma / y; // pow(x, sqrt(saturation));
				
                return sat * lerp(lum, col, sat);
			}
			half3 tm_Reinhard(half3 col, half white)
			{
				half lum = dot(col, half3(0.2126, 0.7152, 0.0722));
				half4 x = half4(lerp(col, lum, smoothstep(1.0, 9.0, lum)), lum);
				
				half4 y = (1.0 + x / (white*white)) / (1.0 + x);
                return lerp(x.rgb * y.rgb, x.rgb * y.a, 0.333333333); // RGB/Luminance mix
			}
			half3 tm_softClamp(half3 col)
			{
				half4 x1 = half4(col, dot(col, half3(0.2126, 0.7152, 0.0722)));
				half4 x2 = (x1 + 3.0) * 0.2;
				
				half4 y = smin4(x1, x2, 0.2);
				return lerp(y.rgb, col * y.a / x1.a, 0.25);
			}
			
			half3 tonemap(half3 col, uint mode) {
				half3 c = col;
				uint tm = _TonemapMode;
				c = tm != 1 ? c : tm_ACES(c);
				c = tm != 2 ? c : EVILS(c);
				c = tm != 3 ? c : tm_HLG(c);
				c = tm != 4 ? c : tm_softClamp(c);//tm_Reinhard(c, 3.0);
				return c;
			}
			
			float mapValue(float a, float b, float x)
			{
				return saturate((x - a)/(b - a));
			}
			bool bounds(half2 uv) {
				return abs(uv.x - 0.5) < 0.5 && abs(uv.y - 0.5) < 0.5;
			}
			
			half edgeDetect(half3 src, half2 uv, half scale, half strength, half threshold) {
				half3 s1 = tex2D(_HirabikiVRCLensPassTexture, uv + half2(-1.0,-1.0) / _ScreenParams.xy);
				half3 s2 = tex2D(_HirabikiVRCLensPassTexture, uv + half2( 1.0,-1.0) / _ScreenParams.xy);
				half3 s3 = tex2D(_HirabikiVRCLensPassTexture, uv + half2(-1.0, 1.0) / _ScreenParams.xy);
				half3 s4 = tex2D(_HirabikiVRCLensPassTexture, uv + half2( 1.0, 1.0) / _ScreenParams.xy);
				half d1 = length(sqrt(max(src, 0.0)) - sqrt(max(s1, 0.0)));
				half d2 = length(sqrt(max(src, 0.0)) - sqrt(max(s2, 0.0)));
				half d3 = length(sqrt(max(src, 0.0)) - sqrt(max(s3, 0.0)));
				half d4 = length(sqrt(max(src, 0.0)) - sqrt(max(s4, 0.0)));
				return saturate(max(max(d1, d2), max(d3, d4)) * rsqrt(scale) - threshold) * strength;
			}
			
			half4 printNumber(float value, float digits, float decimals, half4 col, half2 numUV) {
				half4 numCol = drawNumber(_NumTex, numUV, value, digits, decimals);
				return lerp(col, numCol, numCol.a * bounds(numUV));
			}
			
			half4 drawWhiteLine(half4 src, half2 offset, half thinness) {
				half diff = min(abs(offset.x) * 1.777777777 * (1.0 - (_IsSideBySide3D > 0) * 0.5), abs(offset.y));
				half4 col = lerp(half4(1,1,1,1), half4(0,0,0,1), smoothstep(0.0, 0.333333333, diff * thinness));
				return lerp(col, src, smoothstep(0.333333333, 1.0, diff * thinness));
			}
			
			half4 drawMiddleDisplay(half4 src, half2 iuv) {
				half2 uv = half2((iuv.x - 0.5) * 1.777777777 + 0.5, iuv.y - (!_IsDesktopMode) * 0.0703125); // [36/512]
				uv.x = uv.y < 0.0625 ? uv.x - _ExposureValue * 0.125 * 0.75 : uv.x;
				half4 col = tex2D(_SymbolTex1, uv);
				col = printNumber(_FocusDistance, 3, 2, col, (uv - half2(0.72, 0.125)) * 10.0 * half2(0.571428571, 1.0));
				
				col.a = uv.y < 0.125 ? col.a
					: uv.y < 0.25 && _FocusDistance > 0.5001 ? col.a
					: uv.y < 1.00 ? col.a * _TutorialDisplay
					: 0.0;
				
				return lerp(src, col, col.a);
			}
			
			half4 drawLevelMeter(half4 src, half2 iuv, half2 pr) {
				half2 uv = half2((iuv.x - 0.5) * 1.777777777, iuv.y - 0.5);
				half2 rotUV = half2(
					dot(uv, half2(cos(pr.y),-sin(pr.y))),
					dot(uv, half2(sin(pr.y), cos(pr.y)))
				);
				
				// iRad --  Inside is drawn
				// oRad -- Outside is drawn
				half4 oRad0 = half4(
					0,
					smoothstep(0.399, 0.401, length(uv)),
					smoothstep(0.319, 0.321, length(uv)),
					smoothstep(0.249, 0.251, length(uv))
				);
				half4 oRad1 = half4(
					0,
					0,
					smoothstep(0.319, 0.321, length(uv)),
					smoothstep(0.229, 0.231, length(uv))
				);
				half4 iRad0 = 1.0 - oRad0;
				half4 iRad1 = 1.0 - oRad1;
				
				half4 lineRad = 1.0 - half4(
					smoothstep(0.274, 0.276, length(uv)),
					smoothstep(0.289, 0.291, length(uv)),
					smoothstep(0.299, 0.301, length(uv)),
					smoothstep(0.309, 0.311, length(uv))
				);
				
				half ring = smoothstep(0.004, 0.000, abs(length(uv) - 0.250)); // Ring
				
				half4 baseRing = half4(0, 0, 0, iRad0.z * oRad0.w * 0.8);
				//half4 baseInner = half4(0, 0, 0, iRad1.w * 0.8);
				half4 baseBand = half4(1, 1, 1, ring);
				//half4 baseInnerLine = half4(1, 1, 1, smoothstep(0.003, 0.000, abs(uv.y)) * iRad1.w);
				half4 baseLineH = half4(1, 1, 1, smoothstep(0.008, 0.004, abs(uv.y)) * lineRad.w * oRad0.w);
				half4 baseLineV = half4(1, 1, 1, smoothstep(0.008, 0.004, abs(uv.x)) * lineRad.w * oRad0.w);
				
				half4 lineRoll  = half4(1,0.1,0.1, smoothstep(0.008, 0.004, abs(rotUV.y)) * oRad0.w * iRad0.z);
				half4 linePitch = half4(1,1,1, smoothstep(0.008, 0.004, abs(rotUV.y)) * smoothstep(0.000, 0.004, abs(rotUV.x) - 0.07) * iRad1.w);
				
				half4 col = src;
				col.rgb = lerp(col, baseRing.rgb, baseRing.a);
				//col.rgb = lerp(col, baseInner.rgb, baseInner.a);
				col.rgb = lerp(col, baseBand.rgb, baseBand.a);
				//col.rgb = lerp(col, baseInnerLine.rgb, baseInnerLine.a);
				
				col.rgb = lerp(col, baseLineH.rgb, baseLineH.a);
				col.rgb = lerp(col, baseLineV.rgb, baseLineV.a);
				UNITY_LOOP for(uint i = 1; i < 36; i++) {
					half rot = i * 5.0 * 0.0174532925;
					half2 offUV = half2(
						dot(uv, half2(cos(rot),-sin(rot))),
						dot(uv, half2(sin(rot), cos(rot)))
					);
					uint major = i % 3 == 0;
					major += i % 6 == 0;
					bool draw = i % 18 != 0 && (i % 18 <= 6 || i % 18 >= 12 || i % 18 == 9);
					
					half4 notch = half4(1, 1, 1, draw * smoothstep(0.004, 0.000, abs(offUV.y)) * lineRad[major] * oRad0.w);
					col.rgb = lerp(col, notch.rgb, notch.a);
				}
				UNITY_LOOP for(i = 0; i <= 12; i++) {
					half yLine = ((i - 6) * 15.0 * 0.0174532925 - pr.x) * 0.14;
					uint major = i % 2 == 0;
					major += i % 6 == 0;
					float len = 0.03 + major * 0.02;
					half3 stem = smoothstep(0.004, 0.000, abs(rotUV.x) - len + 0.012);
					
					float weight = i % 6 == 0 ? smoothstep(0.008, 0.004, abs(rotUV.y - yLine)) : smoothstep(0.004, 0.000, abs(rotUV.y - yLine));
					half4 notch = half4(stem, weight * smoothstep(0.004, 0.000, abs(rotUV.x) - len) * iRad0.w);
					col.rgb = lerp(col, notch.rgb, notch.a);
				}
				//half yLine = -pr.x * 0.1;
				//col.rgb = lerp(col, 
				
				col.rgb = lerp(col, lineRoll.rgb, lineRoll.a);
				col.rgb = lerp(col, linePitch.rgb, linePitch.a);
				
				col.rgb = printNumber(abs(pr.y * 57.2957795), 3, 0, col,
					(rotUV - half2(-0.48, 0.0)) * 10.0 * half2(0.666666667, 1.0) + half2(0.0, 0.50));
				col.rgb = printNumber(abs(pr.x * 57.2957795), 3, 0, col,
					(rotUV - half2(-0.22, 0.0)) * 10.0 * half2(0.666666667, 1.0) - half2(0.0, 0.08));
				
				return col;
			}
			half3 colorTemp(half val) {
				//half t = sign(val) * sqrt(abs(val));
				//half t = sign(val) * (1 - (1 - abs(val)) * (1 - abs(val)));
				half t = lerp(val, smoothstep(-1, 1, val) * 2.0 - 1.0, 0.85);
				
				half3 col = t < 0.0
					? half3(1.0, 1.0 + t*sqrt(-t) * 0.7, 1.0 + t * 0.9)
					: half3(1.0 - t * 0.85, 1.0 - pow(t, 1.25) * 0.675, 1.0);
				col *= 1.0 + abs(t*t) + max(0.0, t*t) * 0.6;
				return col;
			}
			
			half2 shiftSymbols(half2 uv) {
				half y = uv.y * 6.0;
				half2 shift = 0.0;
				
				shift.x = uv.x < 0.09375 + SAFEAREA // 0.125 * 3/4
				  ? y < 1 ? +0
				  : y < 2 ? _IsDirectStream - 1
				  : y < 3 ? _SensorType - 1
				  : y < 4 ? +0
				  : y < 5 ? _IsSideBySide3D - 1
				  : +0 : 0.0;
				shift.x = (1 - uv.x) < 0.09375 + SAFEAREA
				  ? y < 1 ? -0
				  : y < 2 ? -0
				  : y < 3 ? -0
				  : y < 4 ? -0
				  : y < 5 ? 1 - _TonemapMode
				  : -_AperturePriority : shift.x;
				
				return shift * half2(0.125, 1.0);
			}
			half4 maskSymbols(half2 uv) {
				half y = uv.y * 8.0;
				half4 c = 1.0;
				half3 yellow = half3(1.0, 0.7, 0.05);
				half3 green = half3(0.05, 1.0, 0.1);
				c.a = uv.x < 0.5
				  ? y < 1 ? _EnableDoF
				  : y < 2 ? (frac(_Time.y * 0.666666666) < 0.666666666) * (_IsDirectStream != 0)
				  : y < 3 ? _SensorType != 0
				  : y < 4 ? _WhiteBalance != 0.0
				  : y < 5 ? _IsSideBySide3D > 0
				  : _ExposureMode != 0
				  : y < 1 ? true
				  : y < 2 ? _FocusDistance > 0.5001
				  : y < 3 ? _IsExternalFocus != 0
				  : y < 4 ? round(_IsImageStabilize) != 0
				  : y < 5 ? _TonemapMode != 0
				  : _EnableDoF;
				c.rgb = uv.x < 0.5
				  ? y < 1 ? 1.0
				  : y < 2 ? _IsDirectStream == 1 ? green : yellow
				  : y < 3 ? 1.0
				  : y < 4 ? colorTemp(_WhiteBalance) * 0.666666666
				  : y < 5 ? 1.0
				  : _ExposureMode == 1 ? yellow : 1.0
				  : y < 2 ? 1.0
				  : y < 3 ? _IsExternalFocus == 1 ? yellow : 1.0
				  : y < 4 ? round(_IsImageStabilize) == 2 ? yellow : 1.0
				  : y < 5 ? 1.0
				  : 1.0;
				c.a *= (abs(uv.x - 0.0625) < 0.0625) + (abs(uv.x - 0.9375) < 0.0625);
				return c;
			}
			
#define LOGICALXOR(p, q) ((p || q) && !(p && q))
			half4 frag(v2f i) : SV_Target
			{
#ifdef USING_STEREO_MATRICES
				return half4(0,0,0,0);
#else
				bool isPreview = _ShowOverlay && all(_ScreenParams.xy == half2(1024, 512));
				bool isSBSTrue = _IsSideBySide3D > 0;
				
				half2 uv = i.uv;
				half2 sbsUV0 = isPreview && isSBSTrue ? half2(frac(i.uv.x - 0.25), i.uv.y) : i.uv; // UNTESTED
				half sbsUV0Mask = isPreview && isSBSTrue ? i.uv.x - 0.25 : i.uv.x; // UNTESTED
				half2 sbsUV1 = isPreview
					? half2(saturate(i.uv.x * (1 + isSBSTrue) - isSBSTrue * 0.5), i.uv.y)
					: half2(frac(i.uv.x * (1 + isSBSTrue)), i.uv.y);
				
				half4 rawColor = tex2D(_HirabikiVRCLensPassTexture, sbsUV0);
				
				float2 focusPos = tex2D(_AuxFocusTex, 0.0).rg + 0.5;
				float2 focusOffset = _PreviewOrientation == 0 ? float2(_FocusOffsetH, _FocusOffsetV) : float2(_FocusOffsetV, _FocusOffsetH * -1.0);
				focusPos = _IsDesktopMode ? focusOffset * 0.5 + 0.5 : focusPos;
				bool isAutoFocus = _FocusDistance < 0.5001;
				//float realAperture = _DoFStrength * lerp(sqrt(_FocalLength / lerp(48.0, 50.0, mapValue(24.0, 50.0, _FocalLength))), 1.0, _AperturePriority);
				float realAperture = _DoFStrength * lerp(_FocalLength * 0.02, 1.0, _AperturePriority);
				
				half4 col = half4(rawColor.rgb, 1.0);
				if(_EnableDoF && any(_ScreenParams.xy != half2(1024, 512))) {
					col = half4(depthOfField(sbsUV0), 1.0);
				}
				
				half3 posData = tex2Dlod(_AuxExpTex, half4(.75,.25,.0,.0)).rgb; // Distance from head, Pitch, Roll
				// ---- EXPOSURE VALUE + AUTO EXPOSURE ---- //
				half2 expData = _ExposureMode ? tex2Dlod(_AuxExpTex, half4(.25,.25,.0,.0)).rg : 1.0; // R=Peak G=Mean
				expData.g = _ExposureMode ? smin(_TonemapMode ? 1.0 : 1.25, expData.g, 0.25) : 1.0;
				
				// ---- FINAL EXPOSURE ---- //
				half3 finalExp = half3(expData.g, max(1.0, expData.r / expData.g), expData.r) * exp2(-_ExposureValue);
				// X = average exposure  Z = peak exposure  Y = Z/X
				col.rgb = max(0.00001, col.rgb / finalExp.x);
				
				// ---- WHITE BALANCE ---- //
				col.rgb *= colorTemp(-_WhiteBalance);
				
				// ---- TONE MAPPING ---- //
				col.rgb = tonemap(col.rgb, _TonemapMode);
				// ---- FILTER POST-PROCESS ---- //
				col.rgb = _ImageFilter != 2 ? col.rgb : col.rgb + pow(length((1.6 * sbsUV1 - 0.8) * half2(1.0, 0.666666666)), 3.0) * lerp(half3(1.25, 0.25, 1.0), 1.0, 0.333333333);
				
				// ---- Greenscreen? ---- //
				col.rgb = _IsSideBySide3D < 0 ? lerp(half3(0, _IsSideBySide3D == -1, _IsSideBySide3D == -2), col.rgb,
					max(saturate(tex2D(_RenderTex, sbsUV0).a), min(1.0, SAMPLE_DEPTH_TEXTURE(_DepthTex, sbsUV0) * 256.0))) : col.rgb;
				// Some avatar have transparency when there is none (Trensparency in opaque textures so this might be one way to mitigate it)
				
				
				// ---- Exposure Zebra ---- //
				col = isPreview && _ShowZebra ? lerp(col, half4(0,0,0,1), saturate(cos(UNITY_PI * (uv.y + uv.x + _Time.y*0.2) * 64.0) * 2.0)
					* step(3.0, min(1.5,col.r) + min(1.5,col.g) + min(1.5,col.b))) : col;
				// ---- FOCUS PEAKING ---- //
				col = isPreview && _EnableDoF && LOGICALXOR(_ShowFocusPeeking, !isAutoFocus)
					? lerp(col, _FocusPeakingColor, edgeDetect(rawColor.rgb, sbsUV0, finalExp.x, smoothstep(1.0, 0.5, abs(rawColor.a)) * 10.0, 0.02)) : col;
				// ---- Rule of Thirds grid ---- //
				col = isPreview && _ShowRuleOfThirds ? drawWhiteLine(col, abs(abs(sbsUV1 - 0.5) - 0.1666666666), 100) : col;
				// ---- Image cropping ---- //
				col.rgb *= isPreview && isSBSTrue ? lerp(abs(sbsUV0Mask - 0.25) < 0.25, 1.0, 0.05) : 1.0; // UNTESTED
				
				// ---- Viewfinder Display ---- //
				col = isPreview && _ShowLeveler ? drawLevelMeter(col, uv, posData.yz) : col;
				col = isPreview ? drawMiddleDisplay(col, uv) : col;
				
				half2 symUV = uv * half2(1.333333333, 0.75);
				symUV.x = uv.x < 0.5 ? symUV.x - SAFEAREA : symUV.x - 0.333333333 + SAFEAREA;
				// Optional adjustment to make middle portion usable
				// symUV.x = abs(symUV.x - 0.5) >= 0.375 ? symUV.x : clamp((uv.x - 0.5) * 1.333333333 + 0.5, 0.12501, 0.87499);
				half2 symShiftUV = shiftSymbols(uv);
				
				half4 synCol = tex2D(_SymbolTex0, symUV + symShiftUV);
				half4 synMask = maskSymbols(symUV);
				col = isPreview ? lerp(col, half4(synCol.rgb * synMask.rgb, 1), synCol.a * synMask.a) : col;
				// UV calc: half2(32 / NumberWidth, 9.0)
				col = isPreview ? printNumber(_FocalLength * _SensorScale, 4, 0, col, (uv - half2(0.7725 - SAFEAREA * 0.75, -0.001)) * 0.88 * half2(8.0, 9.0)) : col;
				col = isPreview && _EnableDoF ?  printNumber(realAperture, 3, 1, col, (uv - half2(0.0450 + SAFEAREA * 0.75, -0.001)) * 0.88 * half2(9.14285714, 9.0)) : col;
				// Debug numbers of interest
				// col = isPreview ? printNumber(inalExp.x), 2, 0, col, (uv - half2(0.333, 0.100)) * 1.111 * half2(16.0, 9.0)) : col;
				// col = isPreview ? printNumber(finalExp.x, 6, 4, col, (uv - half2(0.333, 0.000)) * 1.111 * half2(4.92307692, 9.0)) : col;
				// col = isPreview ? printNumber(posData.x, 6, 3, col, (uv - half2(0.333, 0.200)) * 1.111 * half2(4.92307692, 9.0)) : col;
				// col = isPreview ? printNumber(posData.y, 6, 3, col, (uv - half2(0.333, 0.100)) * 1.111 * half2(4.92307692, 9.0)) : col;
				// col = isPreview ? printNumber(posData.z, 6, 3, col, (uv - half2(0.333, 0.000)) * 1.111 * half2(4.92307692, 9.0)) : col;
				// ---- Autofocus Point ---- //
				half2 focusUV = (uv - focusPos) * half2(12.8, 7.2) + 0.5;
				half4 focusCol = tex2D(_FocusTex, focusUV);
				col = isPreview ? lerp(col, half4(focusCol.rgb,1), focusCol.a * bounds(focusUV)) : col;
				
				// Watermarking code
				half4 mark = tex2D(_MainTex, uv);
				col.rgb = lerp(col.rgb, mark.rgb, mark.a * !isPreview);
				col.a = 1.0; //[Some worlds force transparency on VRChat photo camera]
				return col;
#endif
			}

			ENDCG
		}
	}
}