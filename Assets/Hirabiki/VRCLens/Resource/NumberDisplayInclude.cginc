#ifndef _NumberDisplayInclude
#define _NumberDisplayInclude

// Generic draw function
fixed4 drawNumber(sampler2D numTex, half2 uv, float rawValue, float numDigits, float decimalPlaces) {
	const float4 divisor84 = float4(10000000.0, 1000000.0, 100000.0, 10000.0);
	const float4 divisor30 = float4(1000.0, 100.0, 10.0, 1.0);
	
	float dp = decimalPlaces - max(0, 1 + floor(log10(abs(rawValue))) - numDigits + decimalPlaces );
	float numUV = (decimalPlaces > 0.0) * 0.5 + numDigits;
	decimalPlaces = dp;
	float decPos = 8.0 - decimalPlaces;
	float decSkipPos = 8.0 - decimalPlaces + (decimalPlaces > 0.0) * 0.5;
	float2 dX = ddx(uv * float2(0.0625 * numUV, 1.00));
	float2 dY = ddy(uv * float2(0.0625 * numUV, 1.00));
	
	// -- Extract digits
	float dispValue = round(pow(10.0, decimalPlaces)) * abs(rawValue) + 0.5;
	float4 digit84 = round(floor(dispValue / divisor84) % 10.0);
	float4 digit30 = round(floor(dispValue / divisor30) % 10.0);
	
	float halfSkip = (uv.x * numUV + (8.0 - numDigits) > decSkipPos) * 0.5;
	float2 duv = frac(uv * float2(numUV, 1.0) + float2(halfSkip, 0.0));
	float pos = floor(uv.x * numUV + (8.0 - numDigits) + halfSkip); // Digit selection
	
	float noBlankPos = (pos<decPos-1.0);
	float dPos = (pos>=decPos);
	float isDecPos = (pos==decPos);
	
	// !! I could pack this as two dot products
	float blank = // Fix edge for pos == 0.0
		(pos <= 0.0+dPos) * (dispValue < 10000000.0) +
		(pos == 1.0+dPos) * (dispValue < 1000000.0) +
		(pos == 2.0+dPos) * (dispValue < 100000.0) +
		(pos == 3.0+dPos) * (dispValue < 10000.0) +
		(pos == 4.0+dPos) * (dispValue < 1000.0) +
		(pos == 5.0+dPos) * (dispValue < 100.0) +
		(pos == 6.0+dPos) * (dispValue < 10.0) +
		(pos == 7.0+dPos) * (dispValue < 1.0);
	
	blank *= noBlankPos;
	
	// !! This too, dot product it
	// -- Digit position happens here (Y-axis unaffected)
	float2 off = float2(
		isDecPos == 1.0 ? 15.0 :
		(pos == 0.0+dPos) * digit84.x + (pos == 1.0+dPos) * digit84.y +
		(pos == 2.0+dPos) * digit84.z + (pos == 3.0+dPos) * digit84.w +
		(pos == 4.0+dPos) * digit30.x + (pos == 5.0+dPos) * digit30.y +
		(pos == 6.0+dPos) * digit30.z + (pos == 7.0+dPos) * digit30.w, 0.0
	);
	
	float2 finalUV = (duv + off) * float2(0.0625, 1.0);// + float2(8.0 - numDigits, 0.0);
	
	fixed4 c = tex2Dgrad(numTex, finalUV, dX, dY);
	c.rgb = rawValue < 0.0 ? (1.0-c.rgb)*(1.0-c.rgb) : c.rgb;
	c.a *= 1.0 - blank;
	return c;
}
#endif