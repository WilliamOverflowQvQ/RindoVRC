#ifndef VRCLENS_ADDON_A
#define VRCLENS_ADDON_A
#endif
uniform float _AnamorphicRatio;
uniform int _LensShapeMode;

float lengthOctagon(float2 p) {
	// Rotate 30 degrees
	p = mul(p, float2x2(
		0.866025404,-0.500000000,
		0.500000000, 0.866025404
	));

	const float3 k = float3(-0.9238795325, 0.3826834323, 0.4142135623);
	p = abs(p);
	p -= 2.0 * min(dot(float2( k.x, k.y), p), 0.0) * float2( k.x, k.y);
	p -= 2.0 * min(dot(float2(-k.x, k.y), p), 0.0) * float2(-k.x, k.y);
	p -= float2(p.x, 0.0);
	return length(p) * 1.08243579; // 2.613 / 2.414
}
float lengthHexagon(float2 p) {
	// Rotate 20 degrees
	p = mul(p, float2x2(
		0.939692621,-0.342020143,
		0.342020143, 0.939692621
	));
	
	const float3 k = float3(-0.866025404,0.5,0.577350269);
	p = abs(p);
	p -= 2.0 * min(dot(k.xy, p), 0.0) * k.xy;
	p -= float2(p.x, 0.0);
	return length(p) * 1.15470054;
}
float lengthSquare(float2 p) {
	// Rotate 30 degrees
	p = mul(p, float2x2(
		0.866025404,-0.500000000,
		0.500000000, 0.866025404
	));
	return max(abs(p.x), abs(p.y)) * 1.41421356;
}

float lengthLerp(float2 p) {
	float circleLength = length(p);
	return _LensShapeMode == 1 ? lengthHexagon(p)
		: _LensShapeMode == 2 ? lengthOctagon(p)
		: _LensShapeMode == 3 ? lengthSquare(p)
		: circleLength;
}

float2 scaleLerp(float2 p) {
	float circleLength = length(p);
	float finalLength = _LensShapeMode == 1 ? lengthHexagon(p)
		: _LensShapeMode == 2 ? lengthOctagon(p)
		: _LensShapeMode == 3 ? lengthSquare(p)
		: circleLength;
	
	return p * circleLength / finalLength;
}


half2 scaleSampling(half2 p) {
	return p * half2(_AnamorphicRatio * 0.000520833333, 0.000925925926);
}