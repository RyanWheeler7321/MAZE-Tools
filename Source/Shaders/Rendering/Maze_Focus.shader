Shader "Hidden/Maze/Rendering/Focus"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        HLSLINCLUDE
        #pragma vertex MazeFullscreenVert
        #include "MazeEnvironmentCommon.hlsl"

        TEXTURE2D_X(_MazeFocusDepthTexture);
        TEXTURE2D_X(_MazeFocusCloudMask);
        TEXTURE2D_X(_MazeFocusCloseTexture);
        TEXTURE2D_X(_MazeFocusFarTexture);

        float4 _MazeFocus_ParamsA; // depth on, max radius, debug, clouds use far band
        float4 _MazeFocus_ParamsB; // samples, silhouette spread, layer separation, downsample
        float4 _MazeFocus_ParamsC; // focus start/end, close/far transition
        float4 _MazeFocus_ParamsD; // close/far radius, sky uses far band
        float4 _MazeFocus_LensParamsA; // distortion on, chromatic on
        float4 _MazeFocus_LensParamsB; // center xy, distortion amount, crop scale
        float4 _MazeFocus_LensParamsC; // distortion x/y, falloff, chromatic pixels
        float4 _MazeFocus_LensParamsD; // chromatic start, falloff

        float MazeFocusRawDepth(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X_LOD(_MazeFocusDepthTexture, sampler_PointClamp, saturate(uv), 0).r;
        }

        float MazeFocusEyeDepthFromRaw(float rawDepth)
        {
            return MazeIsSkyDepth(rawDepth) ? _ProjectionParams.z : LinearEyeDepth(rawDepth, _ZBufferParams);
        }

        float MazeFocusCloudCoverage(float2 uv)
        {
            return saturate(SAMPLE_TEXTURE2D_X_LOD(_MazeFocusCloudMask, sampler_LinearClamp, saturate(uv), 0).r);
        }

        float MazeFocusSignedRadiusFromDepth(float eyeDepth, bool isSky)
        {
            if (_MazeFocus_ParamsA.x <= 0.5)
            {
                return 0.0;
            }

            if (isSky)
            {
                return _MazeFocus_ParamsD.z > 0.5 ? _MazeFocus_ParamsD.y : 0.0;
            }

            float focusStart = _MazeFocus_ParamsC.x;
            float focusEnd = _MazeFocus_ParamsC.y;
            float closeTransition = max(0.0005, _MazeFocus_ParamsC.z);
            float farTransition = max(0.0005, _MazeFocus_ParamsC.w);
            float closeAmount = 1.0 - smoothstep(focusStart - closeTransition, focusStart, eyeDepth);
            float farAmount = smoothstep(focusEnd, focusEnd + farTransition, eyeDepth);
            return -_MazeFocus_ParamsD.x * closeAmount + _MazeFocus_ParamsD.y * farAmount;
        }

        float MazeFocusSignedRadius(float2 uv)
        {
            float rawDepth = MazeFocusRawDepth(uv);
            float signedRadius = MazeFocusSignedRadiusFromDepth(MazeFocusEyeDepthFromRaw(rawDepth), MazeIsSkyDepth(rawDepth));
            float cloudCoverage = MazeFocusCloudCoverage(uv);
            float cloudRadius = _MazeFocus_ParamsA.w > 0.5 ? _MazeFocus_ParamsD.y : 0.0;
            return lerp(signedRadius, cloudRadius, cloudCoverage);
        }

        float MazeFocusBand01(float signedRadius)
        {
            if (signedRadius < -0.001)
            {
                return 0.0;
            }

            return signedRadius > 0.001 ? 1.0 : 0.5;
        }

        float3 MazeFocusBandColor(float band01)
        {
            float3 closeColor = float3(1.0, 0.24, 0.04);
            float3 focusColor = float3(0.08, 1.0, 0.22);
            float3 farColor = float3(0.04, 0.28, 1.0);
            return band01 < 0.5
                ? lerp(closeColor, focusColor, band01 * 2.0)
                : lerp(focusColor, farColor, (band01 - 0.5) * 2.0);
        }

        float2 MazeFocusLensFromCenter(float2 uv)
        {
            return uv - _MazeFocus_LensParamsB.xy;
        }

        float MazeFocusLensRadius01(float2 fromCenter)
        {
            float aspect = _ScreenParams.x / max(1.0, _ScreenParams.y);
            float2 aspectSpace = float2(fromCenter.x * aspect, fromCenter.y);
            float2 maxCorner = float2(max(_MazeFocus_LensParamsB.x, 1.0 - _MazeFocus_LensParamsB.x) * aspect,
                                      max(_MazeFocus_LensParamsB.y, 1.0 - _MazeFocus_LensParamsB.y));
            return saturate(length(aspectSpace) / max(0.0001, length(maxCorner)));
        }

        float2 MazeFocusLensDistortedUv(float2 uv, out float displacement)
        {
            float2 center = _MazeFocus_LensParamsB.xy;
            float2 fromCenter = MazeFocusLensFromCenter(uv) / max(1.0, _MazeFocus_LensParamsB.w);
            float radius01 = MazeFocusLensRadius01(fromCenter);
            float edge = pow(radius01, max(0.25, _MazeFocus_LensParamsC.z));
            float2 shape = max(0.0.xx, _MazeFocus_LensParamsC.xy);
            float2 warped = center + fromCenter * (1.0 + _MazeFocus_LensParamsB.z * edge * shape);
            displacement = length(warped - uv);
            return _MazeFocus_LensParamsA.x > 0.5 ? saturate(warped) : uv;
        }

        float MazeFocusChromaticEdge(float2 uv)
        {
            float radius01 = MazeFocusLensRadius01(MazeFocusLensFromCenter(uv));
            float start = saturate(_MazeFocus_LensParamsD.x);
            float edge = saturate((radius01 - start) / max(0.001, 1.0 - start));
            return pow(edge, max(0.25, _MazeFocus_LensParamsD.y));
        }

        struct MazeFocusPrefilterOutput
        {
            half4 closeSeed : SV_Target0;
            half4 farSeed : SV_Target1;
        };

        MazeFocusPrefilterOutput MazeFocusPrefilter(float2 uv)
        {
            MazeFocusPrefilterOutput output;
            float2 sourceTexel = max(_BlitTexture_TexelSize.xy, 1.0 / _ScreenParams.xy);
            float2 offset = sourceTexel * 0.5;
            float2 sampleUvs[4] =
            {
                saturate(uv + float2(-offset.x, -offset.y)),
                saturate(uv + float2( offset.x, -offset.y)),
                saturate(uv + float2(-offset.x,  offset.y)),
                saturate(uv + float2( offset.x,  offset.y)),
            };

            float maxRadius = max(0.001, _MazeFocus_ParamsA.y);
            float strongestClose = 0.0;
            float strongestFar = 0.0;
            float3 closeColor = 0.0;
            float3 farColor = 0.0;
            [unroll]
            for (int i = 0; i < 4; i++)
            {
                float signedRadius = MazeFocusSignedRadius(sampleUvs[i]);
                float3 color = MazeSampleColorInput(sampleUvs[i]);
                float closeRadius = max(0.0, -signedRadius);
                float farRadius = max(0.0, signedRadius);
                if (closeRadius > strongestClose)
                {
                    strongestClose = closeRadius;
                    closeColor = color;
                }

                if (farRadius > strongestFar)
                {
                    strongestFar = farRadius;
                    farColor = color;
                }
            }

            output.closeSeed = half4(closeColor, saturate(strongestClose / maxRadius));
            output.farSeed = half4(farColor, saturate(strongestFar / maxRadius));
            return output;
        }

        half4 MazeFocusGather(MazeFullscreenVaryings input, bool closeLayer)
        {
            float maxRadius = max(0.001, closeLayer ? _MazeFocus_ParamsD.x : _MazeFocus_ParamsD.y);
            int sampleCount = (int)clamp(_MazeFocus_ParamsB.x, 0.0, 32.0);
            if (sampleCount <= 0 || maxRadius <= 0.001)
            {
                return 0.0;
            }

            float targetDepth = MazeFocusEyeDepthFromRaw(MazeFocusRawDepth(input.uv));
            float silhouetteSpread = max(0.5, _MazeFocus_ParamsB.y);
            float separation = max(0.01, _MazeFocus_ParamsB.z);
            float searchRadius = maxRadius * silhouetteSpread;
            float2 fullResolutionTexel = 1.0 / _ScreenParams.xy;
            float4 centerSeed = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, input.uv, 0);
            float centerWeight = step(0.001, centerSeed.a);
            float3 colorSum = centerSeed.rgb * centerWeight;
            float weightSum = centerWeight;
            const float goldenAngle = 2.39996323;

            [loop]
            for (int i = 0; i < 32; i++)
            {
                if (i >= sampleCount)
                {
                    break;
                }

                float t = ((float)i + 0.5) / max(1.0, (float)sampleCount);
                float angle = ((float)i + 0.5) * goldenAngle;
                float sampleDistance = sqrt(t) * searchRadius;
                float2 sampleUv = saturate(input.uv + float2(cos(angle), sin(angle)) * sampleDistance * fullResolutionTexel);
                float4 seed = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, sampleUv, 0);
                float sourceRadius = seed.a * max(0.001, _MazeFocus_ParamsA.y);
                float support = sourceRadius * silhouetteSpread;
                float diskWeight = saturate(1.0 - sampleDistance / max(0.001, support));
                diskWeight *= diskWeight;
                float sourceDepth = MazeFocusEyeDepthFromRaw(MazeFocusRawDepth(sampleUv));
                float depthAllowed = sourceDepth <= targetDepth + separation ? 1.0 : 0.0;
                float weight = diskWeight * depthAllowed * step(0.001, seed.a);
                colorSum += seed.rgb * weight;
                weightSum += weight;
            }

            float coverage = saturate(weightSum / max(1.0, (float)sampleCount * 0.32));
            return half4(colorSum / max(0.0001, weightSum), coverage);
        }
        ENDHLSL

        Pass
        {
            Name "Band Prefilter"
            HLSLPROGRAM
            #pragma fragment Frag
            MazeFocusPrefilterOutput Frag(MazeFullscreenVaryings input)
            {
                return MazeFocusPrefilter(input.uv);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Close Silhouette Gather"
            HLSLPROGRAM
            #pragma fragment Frag
            half4 Frag(MazeFullscreenVaryings input) : SV_Target
            {
                return MazeFocusGather(input, true);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Far Gather"
            HLSLPROGRAM
            #pragma fragment Frag
            half4 Frag(MazeFullscreenVaryings input) : SV_Target
            {
                return MazeFocusGather(input, false);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Band Composite"
            HLSLPROGRAM
            #pragma fragment Frag
            half4 Frag(MazeFullscreenVaryings input) : SV_Target
            {
                float3 source = MazeSampleColorInput(input.uv);
                float rawDepth = MazeFocusRawDepth(input.uv);
                bool isSky = MazeIsSkyDepth(rawDepth);
                float eyeDepth = MazeFocusEyeDepthFromRaw(rawDepth);
                float signedRadius = MazeFocusSignedRadius(input.uv);
                float maxRadius = max(0.001, _MazeFocus_ParamsA.y);
                float debugMode = _MazeFocus_ParamsA.z;
                float4 closeLayer = SAMPLE_TEXTURE2D_X(_MazeFocusCloseTexture, sampler_LinearClamp, input.uv);
                float4 farLayer = SAMPLE_TEXTURE2D_X(_MazeFocusFarTexture, sampler_LinearClamp, input.uv);

                if (debugMode > 0.5 && debugMode < 1.5)
                {
#if UNITY_REVERSED_Z
                    float displayDepth = 1.0 - rawDepth;
#else
                    float displayDepth = rawDepth;
#endif
                    return half4(displayDepth.xxx, 1.0);
                }

                if (debugMode > 1.5 && debugMode < 2.5)
                {
                    float linear01 = saturate(eyeDepth / max(0.001, _ProjectionParams.z));
                    return half4(linear01.xxx, 1.0);
                }

                if (debugMode > 2.5 && debugMode < 3.5)
                {
                    return half4((isSky ? 1.0 : 0.0).xxx, 1.0);
                }

                if (debugMode > 3.5 && debugMode < 4.5)
                {
                    return half4(MazeFocusBandColor(MazeFocusBand01(signedRadius)), 1.0);
                }

                if (debugMode > 4.5 && debugMode < 5.5)
                {
                    float signed01 = saturate(abs(signedRadius) / maxRadius);
                    return half4(signedRadius < 0.0 ? float3(signed01, 0.12, 0.02) : float3(0.02, 0.25, signed01), 1.0);
                }

                if (debugMode > 5.5 && debugMode < 6.5)
                {
                    float coverage = MazeFocusCloudCoverage(input.uv);
                    return half4(coverage.xxx, 1.0);
                }

                if (debugMode > 6.5 && debugMode < 7.5)
                {
                    return half4(closeLayer.aaa, 1.0);
                }

                if (debugMode > 7.5 && debugMode < 8.5)
                {
                    return half4(farLayer.aaa, 1.0);
                }

                if (debugMode > 10.5)
                {
                    return half4(source, 1.0);
                }

                float farAmount = saturate(signedRadius / max(0.001, _MazeFocus_ParamsD.y));
                float farBlend = farAmount;
                float3 baseOrFar = lerp(source, farLayer.rgb, farBlend);
                float closeAmount = saturate(-signedRadius / max(0.001, _MazeFocus_ParamsD.x));
                float closeBlend = max(closeAmount, closeLayer.a);
                return half4(lerp(baseOrFar, closeLayer.rgb, closeBlend), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Lens"
            HLSLPROGRAM
            #pragma fragment Frag
            half4 Frag(MazeFullscreenVaryings input) : SV_Target
            {
                float displacement;
                float2 baseUv = MazeFocusLensDistortedUv(input.uv, displacement);
                float3 baseColor = MazeSampleColorInput(baseUv);
                float chromaticEdge = MazeFocusChromaticEdge(baseUv);
                float debugMode = _MazeFocus_ParamsA.z;

                if (debugMode > 8.5 && debugMode < 9.5)
                {
                    float display = saturate(displacement * max(_ScreenParams.x, _ScreenParams.y) * 0.25);
                    return half4(display, 0.15, 1.0 - display, 1.0);
                }

                if (debugMode > 9.5)
                {
                    return half4(chromaticEdge, 1.0 - chromaticEdge, 0.15, 1.0);
                }

                if (_MazeFocus_LensParamsA.y <= 0.5 || abs(_MazeFocus_LensParamsC.w) <= 0.0001)
                {
                    return half4(baseColor, 1.0);
                }

                float2 fromCenter = MazeFocusLensFromCenter(baseUv);
                float aspect = _ScreenParams.x / max(1.0, _ScreenParams.y);
                float2 aspectDirection = float2(fromCenter.x * aspect, fromCenter.y);
                float2 direction = dot(aspectDirection, aspectDirection) > 1e-8
                    ? normalize(aspectDirection)
                    : float2(1.0, 0.0);
                direction.x /= max(0.0001, aspect);
                float2 offset = direction * (1.0 / _ScreenParams.xy) * (_MazeFocus_LensParamsC.w * chromaticEdge);
                float red = MazeSampleColorInput(saturate(baseUv + offset)).r;
                float blue = MazeSampleColorInput(saturate(baseUv - offset)).b;
                return half4(red, baseColor.g, blue, 1.0);
            }
            ENDHLSL
        }
    }
}
