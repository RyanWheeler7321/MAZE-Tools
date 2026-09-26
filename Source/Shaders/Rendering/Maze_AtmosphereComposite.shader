Shader "Hidden/Maze/Environment/AtmosphereComposite"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        HLSLINCLUDE
        #include "MazeEnvironmentCommon.hlsl"
        TEXTURE2D_X(_MazeAtmosphere_TemporalTexture);
            struct MazeCloudRenderResult
            {
                float3 color;
                float alpha;
            };

            struct MazeCloudTraceSegment
            {
                float startDistance;
                float endDistance;
                float valid;
            };

            struct MazeCloudSegmentSample
            {
                float3 colorPremul;
                float alpha;
            };

            MazeCloudTraceSegment MakeInvalidCloudSegment()
            {
                MazeCloudTraceSegment segment;
                segment.startDistance = 0.0;
                segment.endDistance = 0.0;
                segment.valid = 0.0;
                return segment;
            }

            MazeCloudTraceSegment TraceMainCloudLayer(float3 rayOriginWS, float3 rayDirWS, float maxTraceDistance)
            {
                if (_MazeAtmosphere_CloudParamsA.x <= 0.5 || rayDirWS.y <= 0.01)
                {
                    return MakeInvalidCloudSegment();
                }

                float halfThickness = max(6.0, _MazeAtmosphere_CloudParamsB.w);
                float lowerPlane = _MazeAtmosphere_CloudParamsB.z - halfThickness;
                float upperPlane = _MazeAtmosphere_CloudParamsB.z + halfThickness;
                float lowerPlaneDistance = (lowerPlane - rayOriginWS.y) / rayDirWS.y;
                float upperPlaneDistance = (upperPlane - rayOriginWS.y) / rayDirWS.y;

                MazeCloudTraceSegment segment;
                segment.startDistance = max(0.0, min(lowerPlaneDistance, upperPlaneDistance));
                segment.endDistance = min(max(lowerPlaneDistance, upperPlaneDistance), maxTraceDistance);
                segment.valid = segment.endDistance > segment.startDistance ? 1.0 : 0.0;
                return segment;
            }

            MazeCloudTraceSegment TraceLowerCloudBounds(float3 rayOriginWS, float3 rayDirWS, float maxTraceDistance)
            {
                if (_MazeAtmosphere_LowerCloudParams.x <= 0.5)
                {
                    return MakeInvalidCloudSegment();
                }

                float2 cloudRange = MazeTraceLowerCloudBounds(rayOriginWS, rayDirWS, maxTraceDistance);

                MazeCloudTraceSegment segment;
                segment.startDistance = cloudRange.x;
                segment.endDistance = cloudRange.y;
                segment.valid = segment.endDistance > segment.startDistance ? 1.0 : 0.0;
                return segment;
            }

            float MainCloudDensityOnly(float3 worldPos)
            {
                return _MazeAtmosphere_CloudQualityParams.x > 0.5 ? MazeCloudNoiseFieldPerformance(worldPos) : MazeCloudNoiseField(worldPos);
            }

            MazeCloudSegmentSample RenderCloudSegment(float3 rayOriginWS, float3 rayDirWS, float3 skyColor, float2 pixelCoord, MazeCloudTraceSegment segment, float lowerShape)
            {
                MazeCloudSegmentSample result;
                result.colorPremul = 0.0.xxx;
                result.alpha = 0.0;

                if (segment.valid < 0.5)
                {
                    return result;
                }

                float traceDistance = segment.endDistance - segment.startDistance;
                float maxSteps = lowerShape > 0.5 ? min(10.0, max(5.0, _MazeAtmosphere_CloudParamsA.y * 0.45)) : max(8.0, _MazeAtmosphere_CloudParamsA.y);
                maxSteps = lerp(maxSteps, lowerShape > 0.5 ? 3.0 : 4.0, saturate(_MazeAtmosphere_SceneViewPreview));
                float steps = max(4.0, maxSteps);
                float stepSize = max(lowerShape > 0.5 ? 5.0 : 8.0, traceDistance / steps);
                float ditherStrength = lowerShape > 0.5 ? min(_MazeAtmosphere_CloudShadowExtra.z, 0.45) : _MazeAtmosphere_CloudShadowExtra.z;
                ditherStrength = lerp(ditherStrength, 0.18, saturate(_MazeAtmosphere_SceneViewPreview));
                float dither = (InterleavedGradientNoise(pixelCoord) - 0.5) * ditherStrength + 0.5;
                float3 tracePos = rayOriginWS + rayDirWS * (segment.startDistance + stepSize * dither);
                float3 traceAdd = rayDirWS * stepSize;
                float3 sunDir = normalize(_MazeEnv_SunDirectionWS.xyz);
                float mainLower = _MazeAtmosphere_CloudParamsB.z - max(6.0, _MazeAtmosphere_CloudParamsB.w);
                float mainUpper = _MazeAtmosphere_CloudParamsB.z + max(6.0, _MazeAtmosphere_CloudParamsB.w);

                [loop]
                for (int i = 0; i < 48; i++)
                {
                    if (i >= steps || result.alpha > 0.97)
                    {
                        break;
                    }

                    float density = lowerShape > 0.5 ? MazeLowerCloudDensity(tracePos) : MainCloudDensityOnly(tracePos);
                    if (_MazeRenderDebug_CloudMode > 0.5)
                    {
                        float debugDensity = _MazeRenderDebug_CloudMode < 1.5 ? MazeLowerCloudDensity(tracePos) : (_MazeRenderDebug_CloudMode < 2.5 ? max(MainCloudDensityOnly(tracePos), MazeLowerCloudDensity(tracePos)) : MazeCloudShadowDensity(tracePos));
                        float alpha = debugDensity * (1.0 - result.alpha);
                        result.colorPremul += debugDensity.xxx * alpha;
                        result.alpha += alpha;
                        tracePos += traceAdd;
                        continue;
                    }

                    if (density <= 0.00001)
                    {
                        tracePos += traceAdd;
                        continue;
                    }

                    float opacityFactor = lowerShape > 0.5 ? min(1.0, density * 6.2) : min(1.0, density * 8.0);
                    float layerLower = lowerShape > 0.5 ? _MazeAtmosphere_LowerCloudBoundsMin.y : mainLower;
                    float layerUpper = lowerShape > 0.5 ? _MazeAtmosphere_LowerCloudBoundsMin.y + _MazeAtmosphere_LowerCloudBoundsSize.y : mainUpper;
                    float cloudShading = saturate((tracePos.y - layerLower) / max(1.0, layerUpper - layerLower));

                    float cheapLighting = max(_MazeAtmosphere_CloudQualityParams.z, saturate(_MazeAtmosphere_SceneViewPreview));
                    float lighting = cheapLighting > 0.5
                        ? saturate(0.82 - density * 0.28 + cloudShading * 0.18)
                        : saturate(1.0 - MazeCloudBaseDensity(tracePos + sunDir * 18.0) * 0.85);
                    float phase = saturate(PhaseHG(dot(rayDirWS, sunDir), 0.45) * _MazeAtmosphere_CloudLightingParams.w * 10.0);
                    float distanceRatio = saturate((4000.0 - length(tracePos.xz - rayOriginWS.xz)) / 4000.0);
                    float cloudFogFactor = saturate(distanceRatio * distanceRatio * sqrt(distanceRatio));

                    float3 colorSample = _MazeAtmosphere_CloudAmbientColor.rgb * (0.4 + 0.6 * cloudShading);
                    colorSample += _MazeAtmosphere_CloudLightColor.rgb * cloudShading * saturate(lighting + phase);
                    colorSample = lerp(skyColor, colorSample, cloudFogFactor * 0.72);

                    float alpha = opacityFactor * pow(max(0.01, distanceRatio), 0.55);
                    alpha *= 1.0 - result.alpha;
                    result.colorPremul += colorSample * alpha;
                    result.alpha += alpha;
                    tracePos += traceAdd;
                }

                return result;
            }

            void CompositeCloudSegment(inout float3 colorPremul, inout float alpha, MazeCloudSegmentSample segment)
            {
                colorPremul += segment.colorPremul * (1.0 - alpha);
                alpha += segment.alpha * (1.0 - alpha);
            }

            MazeCloudRenderResult RenderClouds(float3 rayOriginWS, float3 rayDirWS, float3 skyColor, float2 pixelCoord, float maxTraceDistance, float forceOpaqueSky)
            {
                MazeCloudRenderResult result;
                result.color = skyColor;
                result.alpha = forceOpaqueSky;

                if (_MazeAtmosphere_CloudParamsA.x <= 0.5)
                {
                    return result;
                }

                MazeCloudTraceSegment mainSegment = TraceMainCloudLayer(rayOriginWS, rayDirWS, maxTraceDistance);
                MazeCloudTraceSegment lowerSegment = TraceLowerCloudBounds(rayOriginWS, rayDirWS, maxTraceDistance);
                if (mainSegment.valid < 0.5 && lowerSegment.valid < 0.5)
                {
                    return result;
                }

                float3 accumColorPremul = 0.0.xxx;
                float accumAlpha = 0.0;

                if (lowerSegment.valid > 0.5 && (mainSegment.valid < 0.5 || lowerSegment.startDistance <= mainSegment.startDistance))
                {
                    CompositeCloudSegment(accumColorPremul, accumAlpha, RenderCloudSegment(rayOriginWS, rayDirWS, skyColor, pixelCoord, lowerSegment, 1.0));
                    CompositeCloudSegment(accumColorPremul, accumAlpha, RenderCloudSegment(rayOriginWS, rayDirWS, skyColor, pixelCoord, mainSegment, 0.0));
                }
                else
                {
                    CompositeCloudSegment(accumColorPremul, accumAlpha, RenderCloudSegment(rayOriginWS, rayDirWS, skyColor, pixelCoord, mainSegment, 0.0));
                    CompositeCloudSegment(accumColorPremul, accumAlpha, RenderCloudSegment(rayOriginWS, rayDirWS, skyColor, pixelCoord, lowerSegment, 1.0));
                }

                if (_MazeRenderDebug_CloudMode > 0.5)
                {
                    result.color = accumAlpha > 0.0001 ? accumColorPremul / max(accumAlpha, 0.0001) : 0.0.xxx;
                    result.alpha = forceOpaqueSky > 0.5 ? 1.0 : saturate(accumAlpha);
                    return result;
                }

                result.alpha = forceOpaqueSky > 0.5 ? 1.0 : saturate(accumAlpha);
                if (forceOpaqueSky > 0.5)
                {
                    result.color = accumColorPremul + skyColor * (1.0 - accumAlpha);
                    return result;
                }

                result.color = accumAlpha > 0.0001 ? accumColorPremul / max(accumAlpha, 0.0001) : skyColor;
                return result;
            }

            half4 FragAtmosphere(MazeFullscreenVaryings input) : SV_Target
            {
                float depth = SampleSceneDepth(input.uv);
                float isSky = MazeIsSkyDepth(depth) ? 1.0 : 0.0;
                float maxTraceDistance = 1000000.0;
                if (isSky < 0.5)
                {
                    float3 scenePosWS = MazeReconstructWorldPosition(input.uv, depth);
                    maxTraceDistance = max(0.0, distance(_WorldSpaceCameraPos.xyz, scenePosWS) - 0.25);
                }

                float3 rayDirWS = MazeViewDirectionWS(input.uv);
                float3 sunDirWS = normalize(_MazeEnv_SunDirectionWS.xyz);
                float3 sky = MazeSkyColor(rayDirWS, sunDirWS);
                float nightFactor = MazeNightFactor(sunDirWS);
                sky += MazeStars(rayDirWS) * nightFactor;
                sky += MazeAurora(rayDirWS) * nightFactor;
                if (isSky < 0.5)
                {
                    sky = MazeSampleColorInput(input.uv);
                }

                MazeCloudRenderResult clouds = RenderClouds(_WorldSpaceCameraPos.xyz, rayDirWS, sky, input.positionCS.xy, maxTraceDistance, isSky);
                return half4(clouds.color, clouds.alpha);
            }

            half4 FragSkyOnly(MazeFullscreenVaryings input) : SV_Target
            {
                float depth = SampleSceneDepth(input.uv);
                if (!MazeIsSkyDepth(depth))
                {
                    return half4(0.0, 0.0, 0.0, 0.0);
                }

                float3 rayDirWS = MazeViewDirectionWS(input.uv);
                float3 sunDirWS = normalize(_MazeEnv_SunDirectionWS.xyz);
                float3 sky = MazeSkyColor(rayDirWS, sunDirWS);
                float nightFactor = MazeNightFactor(sunDirWS);
                sky += MazeStars(rayDirWS) * nightFactor;
                sky += MazeAurora(rayDirWS) * nightFactor;
                return half4(sky, 1.0);
            }


        half4 FragTemporalComposite(MazeFullscreenVaryings input) : SV_Target
        {
            return SAMPLE_TEXTURE2D_X(_MazeAtmosphere_TemporalTexture, sampler_LinearClamp, input.uv);
        }
        ENDHLSL

        Pass
        {
            Name "AtmosphereBackground"
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex MazeFullscreenVert
            #pragma fragment FragAtmosphere
            ENDHLSL
        }

        Pass
        {
            Name "AtmosphereBackgroundRaw"
            Blend One Zero
            HLSLPROGRAM
            #pragma vertex MazeFullscreenVert
            #pragma fragment FragAtmosphere
            ENDHLSL
        }

        Pass
        {
            Name "AtmosphereTemporalComposite"
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex MazeFullscreenVert
            #pragma fragment FragTemporalComposite
            ENDHLSL
        }

        Pass
        {
            Name "CloudShadowOverlay"
            Blend DstColor Zero
            HLSLPROGRAM
            #pragma vertex MazeFullscreenVert
            #pragma fragment FragCloudShadow

            half4 FragCloudShadow(MazeFullscreenVaryings input) : SV_Target
            {
                if (_MazeRenderDebug_WaterActive > 0.5 || _MazeRenderDebug_CloudMode > 0.5)
                {
                    return half4(1.0, 1.0, 1.0, 1.0);
                }

                float depth = SampleSceneDepth(input.uv);
                if (MazeIsSkyDepth(depth))
                {
                    return half4(1.0, 1.0, 1.0, 1.0);
                }

                float3 worldPos = MazeReconstructWorldPosition(input.uv, depth);
                float sceneDistance = distance(_WorldSpaceCameraPos.xyz, worldPos);
                float3 viewDir = normalize(worldPos - _WorldSpaceCameraPos.xyz);
                if (MazeRayHitsWaterBeforeScene(viewDir, sceneDistance))
                {
                    return half4(1.0, 1.0, 1.0, 1.0);
                }

                float3 sunDirection = normalize(_MazeEnv_SunDirectionWS.xyz);
                float shadow = MazeSampleCloudShadow(worldPos, sunDirection);
                float3 fakeShadow = MazeSampleFakeCloudShadow(worldPos, sunDirection);
                return half4(fakeShadow * shadow, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "AtmosphereSkyOnly"
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex MazeFullscreenVert
            #pragma fragment FragSkyOnly
            ENDHLSL
        }
    }
}
