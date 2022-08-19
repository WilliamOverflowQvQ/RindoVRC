/*
 * Copyright (C) 2021 The Android Open Source Project
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
 
/*
 * This derivative work was modified by Hirabiki in order to
 * adapt the source code from C++ to HLSL and to inline constants.
 */

float genericTonemap(float x, float contrast, float shoulder,
float midGreyIn, float midGreyOut, float hdrMax) {
    // Lottes, 2016,"Advanced Techniques and Optimization of VDR Color Pipelines"
    // https://gpuopen.com/wp-content/uploads/2016/03/GdcVdrLottes.pdf
    float mc = pow(midGreyIn, contrast);
    float mcs = pow(mc, shoulder);

    float hc = pow(hdrMax, contrast);
    float hcs = pow(hc, shoulder);

    float b1 = -mc + hc * midGreyOut;
    float b2 = (hcs - mcs) * midGreyOut;
    float b = b1 / b2;

    float c1 = hcs * mc - hc * mcs * midGreyOut;
    float c2 = (hcs - mcs) * midGreyOut;
    float c = c1 / c2;

    float xc = pow(x, contrast);
    return saturate(xc / (pow(xc, shoulder) * b + c));
}

float3 EVILS(float3 x) {
    // Troy Sobotka, 2021, "EVILS - Exposure Value Invariant Luminance Scaling"
    // https://colab.research.google.com/drive/1iPJzNNKR7PynFmsqSnQm3bCZmQ3CvAJ-#scrollTo=psU43hb-BLzB

    // TODO: These constants were chosen to match our ACES tone mappers as closely as possible
    //       in terms of compression. We should expose these parameters to users via an API.
    //       We must however carefully validate exposed parameters as it is easy to get the
    //       generic tonemapper to produce invalid curves.
	
	float FLOAT_MIN = 1.17549435E-38;
	// RGB to luma coefficients for Rec.709, from sRGB_to_XYZ
	float3 LUMA_REC709 = float3(0.2126730, 0.7151520, 0.0721750);

    float contrast = 1.6;
    float shoulder = 1.0;
    float midGreyIn = 0.18;
    float midGreyOut = 0.227;
    float hdrMax = 64.0;

    // We assume an input compatible with Rec.709 luminance weights
    float luminanceIn = dot(x, LUMA_REC709);
    float luminanceOut = genericTonemap(luminanceIn, contrast, shoulder, midGreyIn, midGreyOut, hdrMax);

    float peak = max(max(x.x, x.y), x.z);
    float3 chromaRatio = max(x / peak, 0.0);

    float chromaRatioLuminance = dot(chromaRatio, LUMA_REC709);

    float3 maxReserves = 1.0 - chromaRatio;
    float maxReservesLuminance = dot(maxReserves, LUMA_REC709);

    float luminanceDifference = max(luminanceOut - chromaRatioLuminance, 0.0);
    float scaledLuminanceDifference =
            luminanceDifference / max(maxReservesLuminance, FLOAT_MIN);

    float chromaScale = (luminanceOut - luminanceDifference) /
            max(chromaRatioLuminance, FLOAT_MIN);

    return saturate(chromaScale * chromaRatio + scaledLuminanceDifference * maxReserves);
}