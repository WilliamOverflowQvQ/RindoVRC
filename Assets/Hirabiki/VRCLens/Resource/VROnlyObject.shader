Shader "Hirabiki/VRCLens/Standard VR-Only Object"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Color ("Color", Color) = (1,1,1,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
		[Toggle(_EMISSION)] _EnableEmission("Enable Emission", int) = 0
		[NoScaleOffset] _EmissionMap("Emission", 2D) = "white" {}
		[HDR]_EmissionColor("Emission Color", Color) = (1,1,1)
		
		_ShowInVRCLens ("Shown in cameras", float) = 0.0
		
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard addshadow
		//fullforwardshadows

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0
		#pragma multi_compile _ _EMISSION

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
		bool _ShowInVRCLens; // Set to TRUE remotely, FALSE locally

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
		// put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)
		
		UNITY_DECLARE_TEX2D(_EmissionMap);
		half4 _EmissionColor;
		
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
#ifndef USING_STEREO_MATRICES
			if(abs(unity_CameraProjection._m11 - 1.73205) > 0.00001 // If it's not 60.000 degree camera
				&& (_ScreenParams.y / _ScreenParams.x == 0.5625 || _ScreenParams.y / _ScreenParams.x == 1.125) // And is 16:9 or 8:9 (3D)
				&& all(unity_CameraProjection._m20_m21 == 0.0) // And is not in mirror
				&& (!_ShowInVRCLens)) { // And is set to NOT be shown in VRCLens
				discard; // Make it disappear
			}
#endif
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
#ifdef _EMISSION
			o.Emission = UNITY_SAMPLE_TEX2D(_EmissionMap, IN.uv_MainTex) * _EmissionColor;
#endif
        }
        ENDCG
    }
    FallBack "Diffuse"
}
