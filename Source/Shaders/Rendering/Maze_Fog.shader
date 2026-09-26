Shader "Hidden/Maze/Environment/Fog"
{
    Properties
    {
        [HideInInspector] _MazeFog_DebugView ("Debug View", Float) = 15
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex MazeFullscreenVert
            #pragma fragment Frag
            #include "MazeEnvironmentCommon.hlsl"

            #define MAZE_FOG_MAX_BANDS 8
            #define MAZE_FOG_CLOSE_DISTANCE 5.0
            #define MAZE_FOG_CLOSE_MAX_OPACITY 0.5
            #define MAZE_FOG_VERY_CLOSE_DISTANCE 1.25
            #define MAZE_FOG_VERY_CLOSE_MAX_OPACITY 0.65

            float4 _MazeFog_ParamsA;
            float4 _MazeFog_ParamsB;
            float4 _MazeFog_ParamsC;
            float4 _MazeFog_LocalParams;
            float4 _MazeFog_HeightParams;
            float4 _MazeFog_GroundSkyParams;
            float4 _MazeFog_NoiseParams;
            float4 _MazeFog_FarNoiseParams;
            float4 _MazeFog_NoiseDistanceParams;
            float4 _MazeFog_LightingParams;
            float4 _MazeFog_HorizonParams;
            float4 _MazeFog_FallbackSkyColor;
            float _MazeFog_BandCount;
            float4 _MazeFog_BandsA[MAZE_FOG_MAX_BANDS];
            float4 _MazeFog_BandsB[MAZE_FOG_MAX_BANDS];
            float4 _MazeFog_BandColors[MAZE_FOG_MAX_BANDS];
            float _MazeFog_DebugView;
            TEXTURE2D_X(_MazeFogDepthTexture);
            float3 MazeFogTintAtAuthoredLuminance(float3 authoredColor, float3 tintColor, float amount)
            {
                amount = saturate(amount);
                if (amount <= 0.0001)
                {
                    return authoredColor;
                }

                const float3 luminanceWeights = float3(0.2126, 0.7152, 0.0722);
                float authoredLuminance = dot(max(0.0.xxx, authoredColor), luminanceWeights);
                float tintLuminance = dot(max(0.0.xxx, tintColor), luminanceWeights);
                if (tintLuminance <= 0.0001)
                {
                    return authoredColor;
                }

                float3 luminanceMatchedTint = max(0.0.xxx, tintColor) * (authoredLuminance / tintLuminance);
                return lerp(authoredColor, luminanceMatchedTint, amount);
            }

            float3 MazeFogSkyTint(float3 viewDir, float3 sunDir)
            {
                float3 fallbackColor = max(0.0.xxx, _MazeFog_FallbackSkyColor.rgb);
                if (_MazeAtmosphere_SkyParams.w <= 0.5)
                {
                    return fallbackColor;
                }

                float3 atmosphereColor = MazeSkyColor(viewDir, sunDir);
                const float3 luminanceWeights = float3(0.2126, 0.7152, 0.0722);
                return dot(max(0.0.xxx, atmosphereColor), luminanceWeights) > 0.0001
                    ? atmosphereColor
                    : fallbackColor;
            }

            float MazeFogHeightTerm(float worldY);

            float MazeFogBandWeight(int bandIndex, float distanceValue)
            {
                if (bandIndex <= 0)
                {
                    return 1.0;
                }

                int bandCount = (int)min(max(0.0, _MazeFog_BandCount), (float)MAZE_FOG_MAX_BANDS);
                float transition = max(0.001, _MazeFog_ParamsA.w);
                float startDistance = _MazeFog_BandsA[bandIndex].y;
                float enter = smoothstep(startDistance - transition, startDistance + transition, distanceValue);
                if (bandIndex + 1 >= bandCount)
                {
                    return enter;
                }

                float nextStart = _MazeFog_BandsA[bandIndex + 1].y;
                float leave = 1.0 - smoothstep(nextStart - transition, nextStart + transition, distanceValue);
                return saturate(enter * leave);
            }

            void MazeFogMedium(float distanceValue, bool includeDistanceBands, out float density, out float3 color)
            {
                int bandCount = (int)min(max(0.0, _MazeFog_BandCount), (float)MAZE_FOG_MAX_BANDS);
                if (bandCount <= 0)
                {
                    density = 0.0;
                    color = 0.0.xxx;
                    return;
                }

                float baseDensity = max(0.0, _MazeFog_BandsA[0].x);
                float3 baseColor = _MazeFog_BandColors[0].rgb;
                density = baseDensity;
                float3 weightedColor = baseColor * baseDensity;

                if (includeDistanceBands)
                {
                    [loop]
                    for (int i = 1; i < MAZE_FOG_MAX_BANDS; i++)
                    {
                        if (i >= bandCount)
                        {
                            break;
                        }

                        float addedDensity = max(0.0, _MazeFog_BandsA[i].x) * MazeFogBandWeight(i, distanceValue);
                        density += addedDensity;
                        weightedColor += _MazeFog_BandColors[i].rgb * addedDensity;
                    }
                }

                color = density > 0.000001 ? weightedColor / density : baseColor;
            }

            float3 MazeFogBandDebugColor(float3 viewDir, float fogDistance)
            {
                float3 colors[MAZE_FOG_MAX_BANDS] = {
                    float3(1.0, 0.2, 0.1),
                    float3(1.0, 0.8, 0.1),
                    float3(0.2, 1.0, 0.2),
                    float3(0.1, 0.8, 1.0),
                    float3(0.3, 0.3, 1.0),
                    float3(0.8, 0.2, 1.0),
                    float3(1.0, 0.2, 0.7),
                    float3(1.0, 1.0, 1.0)
                };
                int bandCount = (int)min(max(0.0, _MazeFog_BandCount), (float)MAZE_FOG_MAX_BANDS);
                int stepCount = (int)max(1.0, _MazeFog_ParamsB.z);
                float3 result = 0.0.xxx;
                float totalWeight = 0.0;
                float previousDistance = 0.0;
                [loop]
                for (int stepIndex = 0; stepIndex < 64; stepIndex++)
                {
                    if (stepIndex >= stepCount)
                    {
                        break;
                    }

                    float segmentEnd01 = (stepIndex + 1.0) / max(1.0, (float)stepCount);
                    float segmentEnd = fogDistance * segmentEnd01 * segmentEnd01;
                    float stepSize = segmentEnd - previousDistance;
                    float sampleDistance = (previousDistance + segmentEnd) * 0.5;
                    float3 samplePos = _WorldSpaceCameraPos.xyz + viewDir * sampleDistance;
                    float heightWeight = MazeFogHeightTerm(samplePos.y);
                    [loop]
                    for (int bandIndex = 0; bandIndex < MAZE_FOG_MAX_BANDS; bandIndex++)
                    {
                        if (bandIndex >= bandCount)
                        {
                            break;
                        }

                        float4 paramsA = _MazeFog_BandsA[bandIndex];
                        float bandWeight = MazeFogBandWeight(bandIndex, sampleDistance);
                        float contribution = max(0.0, paramsA.x) * bandWeight * heightWeight * stepSize;
                        result += colors[bandIndex] * contribution;
                        totalWeight += contribution;
                    }

                    previousDistance = segmentEnd;
                }

                return totalWeight > 0.0001 ? result / totalWeight : 0.0.xxx;
            }

            float3 MazeFogViewDirectionWS(float2 uv)
            {
#if UNITY_REVERSED_Z
                const float farDeviceDepth = 0.0;
#else
                const float farDeviceDepth = 1.0;
#endif
                float3 farWorldPos = MazeReconstructWorldPosition(uv, farDeviceDepth);
                return normalize(farWorldPos - _WorldSpaceCameraPos.xyz);
            }

            float MazeFogGroundSkyMultiplier(float worldY)
            {
                float splitHeight = _MazeFog_GroundSkyParams.x;
                float transition = max(0.1, _MazeFog_GroundSkyParams.y);
                float groundThickness = max(0.0, _MazeFog_GroundSkyParams.z);
                float skyThickness = max(0.0, _MazeFog_GroundSkyParams.w);
                float skyWeight = smoothstep(splitHeight - transition * 0.5, splitHeight + transition * 0.5, worldY);
                return lerp(groundThickness, skyThickness, skyWeight);
            }

            float MazeFogHeightTerm(float worldY)
            {
                float baseHeight = _MazeFog_HeightParams.x;
                float thickness = max(0.001, _MazeFog_HeightParams.y);
                float falloff = max(0.001, _MazeFog_HeightParams.z);
                float groundBias = max(0.0, _MazeFog_HeightParams.w);
                float altitude = worldY - baseHeight;
                float band = saturate(1.0 - max(0.0, altitude) / thickness);
                float exponential = exp(-abs(altitude) * falloff);
                float ground = exp(-max(0.0, altitude) * falloff * 0.35);
                float volumeShape = max(exponential, band * 0.65) * lerp(1.0, ground, saturate(groundBias));
                return max(0.0, volumeShape * MazeFogGroundSkyMultiplier(worldY));
            }

            float MazeFogNoiseField(float3 worldPos, float scale, float strength, float contrast, float timeOffset)
            {
                if (strength <= 0.0001)
                {
                    return 1.0;
                }

                float n1 = Fbm3DFast(worldPos * scale + float3(timeOffset, timeOffset * 0.2, -timeOffset * 0.6));
                float n2 = Fbm2DFast(worldPos.xz * scale * 0.45 - _MazeEnv_WindDirection.xy * timeOffset * 0.4);
                float n = saturate(n1 * 0.72 + n2 * 0.38);
                n = pow(max(0.0001, n), contrast);
                return lerp(1.0, n * 1.35, strength);
            }

            void MazeFogNoiseValues(float3 worldPos, float distanceValue, out float nearNoise, out float farNoise, out float combinedNoise)
            {
                float t = _MazeEnv_Time * _MazeFog_NoiseParams.w;
                nearNoise = MazeFogNoiseField(worldPos, max(0.000001, _MazeFog_NoiseParams.x), saturate(_MazeFog_NoiseParams.y), max(0.25, _MazeFog_NoiseParams.z), t);
                farNoise = MazeFogNoiseField(worldPos + 73.1.xxx, max(0.000001, _MazeFog_FarNoiseParams.x), saturate(_MazeFog_FarNoiseParams.y), max(0.25, _MazeFog_FarNoiseParams.z), -t * 0.37);
                float farBlend = smoothstep(_MazeFog_FarNoiseParams.w, max(_MazeFog_FarNoiseParams.w + 0.001, _MazeFog_NoiseDistanceParams.x), distanceValue);
                combinedNoise = lerp(nearNoise, farNoise, farBlend);
            }

            float MazeFogWispMultiplier(float3 worldPos, float combinedNoise)
            {
                float amount = saturate(_MazeFog_LocalParams.x);
                if (amount <= 0.0001)
                {
                    return 1.0;
                }

                float granularity = saturate(_MazeFog_LocalParams.w);
                float wispScale = 0.028 * exp2(granularity * 2.75);
                float2 wispUv = float2(
                    worldPos.x + worldPos.z * 0.28,
                    worldPos.y * 2.4 + worldPos.z * 0.08) * wispScale;
                float wispTime = _MazeEnv_Time * _MazeFog_NoiseParams.w;
                wispUv += _MazeEnv_WindDirection.xy * (wispTime * 0.25);
                float sheetNoise = Fbm2DFast(wispUv);
                float textureNoise = saturate(combinedNoise / 1.35);
                float contourPhase = sheetNoise * 2.75 + textureNoise * 0.35 + wispUv.y * 0.45;
                float contour = 0.5 + 0.5 * cos(contourPhase * 6.2831853);
                float wispMask = smoothstep(0.7, 0.95, contour) * lerp(0.55, 1.0, saturate(sheetNoise * 1.4));
                return lerp(1.0, lerp(0.35, 1.0, wispMask), amount);
            }

            float MazeFogCloseGranularity(float3 worldPos, float3 viewDir)
            {
                float amount = saturate(_MazeFog_LocalParams.z);
                if (amount <= 0.0001)
                {
                    return 1.0;
                }

                float frequency = lerp(0.3, 1.4, amount);
                float3 samplePosition = worldPos + viewDir * 1.75;
                float grain = Fbm3DFast(samplePosition * frequency + float3(17.3, -9.1, 4.7));
                float grainShape = lerp(0.25, 1.0, smoothstep(0.22, 0.78, grain));
                return lerp(1.0, grainShape, amount);
            }

            float MazeFogCloseIntegral01(float distanceValue)
            {
                float x = saturate(distanceValue / MAZE_FOG_CLOSE_DISTANCE);
                return saturate(2.0 * x - 2.0 * x * x * x + x * x * x * x);
            }

            float MazeFogVeryCloseIntegral01(float distanceValue)
            {
                float x = saturate(distanceValue / MAZE_FOG_VERY_CLOSE_DISTANCE);
                return saturate(2.0 * x - 2.0 * x * x * x + x * x * x * x);
            }

            float MazeFogCloseLocalShape(float3 viewDir, float fogDistance, float maxSampleDistance)
            {
                float sampleDistance = min(fogDistance * 0.5, maxSampleDistance);
                float3 samplePos = _WorldSpaceCameraPos.xyz + viewDir * sampleDistance;
                float nearNoise;
                float farNoise;
                float combinedNoise;
                MazeFogNoiseValues(samplePos, sampleDistance, nearNoise, farNoise, combinedNoise);
                float localShape = MazeFogHeightTerm(samplePos.y) * lerp(1.0, nearNoise, 0.25);
                return localShape
                    * MazeFogWispMultiplier(samplePos, combinedNoise)
                    * MazeFogCloseGranularity(samplePos, viewDir);
            }

            float3 MazeApplyCloseFog(float3 source, float3 viewDir, float fogDistance, float opacityLimit, out float closeFogAmount)
            {
                closeFogAmount = 0.0;
                float slider = saturate(_MazeFog_ParamsC.y);
                float veryCloseSlider = saturate(_MazeFog_LocalParams.y);
                float intensity = saturate(_MazeFog_ParamsC.x);
                if ((slider <= 0.0001 && veryCloseSlider <= 0.0001) || intensity <= 0.0001 || fogDistance <= 0.01 || opacityLimit <= 0.0001)
                {
                    return source;
                }

                float sampleDistance = min(fogDistance * 0.5, MAZE_FOG_CLOSE_DISTANCE * 0.5);
                float3 samplePos = _WorldSpaceCameraPos.xyz + viewDir * sampleDistance;
                if (slider > 0.0001)
                {
                    float targetOpacity = MAZE_FOG_CLOSE_MAX_OPACITY * slider * slider;
                    float opticalDepth = -log(max(0.0001, 1.0 - targetOpacity)) * MazeFogCloseIntegral01(fogDistance);
                    float localShape = MazeFogCloseLocalShape(viewDir, fogDistance, MAZE_FOG_CLOSE_DISTANCE * 0.5);
                    closeFogAmount = saturate(1.0 - exp(-opticalDepth * intensity * localShape)) * opacityLimit;
                }

                if (veryCloseSlider > 0.0001)
                {
                    float targetOpacity = MAZE_FOG_VERY_CLOSE_MAX_OPACITY * veryCloseSlider * veryCloseSlider;
                    float opticalDepth = -log(max(0.0001, 1.0 - targetOpacity)) * MazeFogVeryCloseIntegral01(fogDistance);
                    float localShape = MazeFogCloseLocalShape(viewDir, fogDistance, MAZE_FOG_VERY_CLOSE_DISTANCE * 0.5);
                    float veryCloseFogAmount = saturate(1.0 - exp(-opticalDepth * intensity * localShape)) * opacityLimit;
                    closeFogAmount = 1.0 - (1.0 - closeFogAmount) * (1.0 - veryCloseFogAmount);
                    sampleDistance = min(sampleDistance, MAZE_FOG_VERY_CLOSE_DISTANCE * 0.5);
                    samplePos = _WorldSpaceCameraPos.xyz + viewDir * sampleDistance;
                }

                float density;
                float3 globalColor;
                MazeFogMedium(0.0, false, density, globalColor);
                float3 sunDir = normalize(_MazeEnv_SunDirectionWS.xyz);
                float3 skyColor = MazeFogSkyTint(viewDir, sunDir);
                float3 closeColor = MazeFogTintAtAuthoredLuminance(globalColor, skyColor, _MazeFog_ParamsB.y);
                float phase = saturate(PhaseHG(dot(viewDir, sunDir), saturate(_MazeFog_LightingParams.x * 0.75)) * 8.0);
                float directionalLighting = lerp(1.0, phase, saturate(_MazeFog_LightingParams.x));
                float lighting = saturate(_MazeFog_LightingParams.z + directionalLighting * max(0.0, _MazeFog_LightingParams.y));
                float cloudShadow = lerp(1.0, MazeSampleCloudShadow(samplePos, sunDir), saturate(_MazeFog_LightingParams.w));
                closeColor *= lighting * cloudShadow;
                return lerp(source, closeColor, closeFogAmount);
            }

            float MazeFogHorizonHaze(float fogDistance, float3 viewDir)
            {
                float strength = saturate(_MazeFog_HorizonParams.x) * saturate(_MazeFog_ParamsC.x);
                if (strength <= 0.0001)
                {
                    return 0.0;
                }

                float startDistance = max(0.0, _MazeFog_HorizonParams.y);
                float fullDistance = max(startDistance + 0.001, _MazeFog_HorizonParams.z);
                float distanceMask = smoothstep(startDistance, fullDistance, fogDistance);
                float verticalSize = max(0.001, _MazeFog_HorizonParams.w);
                float horizonMask = 1.0 - smoothstep(0.0, verticalSize, abs(normalize(viewDir).y));
                return distanceMask * horizonMask * strength;
            }

            float3 MazeApplyVolumeFog(float3 source, float3 viewDir, float fogDistance, bool includeDistanceBands, float opacityLimit, float rayWispMultiplier, out float volumeFogAmount)
            {
                volumeFogAmount = 0.0;
                float enabled = _MazeFog_ParamsA.x;
                float intensity = saturate(_MazeFog_ParamsC.x);
                int stepCount = (int)max(0.0, _MazeFog_ParamsB.z);
                if (enabled <= 0.5 || intensity <= 0.0001 || stepCount <= 0 || fogDistance <= 0.01 || _MazeFog_BandCount <= 0.5 || opacityLimit <= 0.0001)
                {
                    return source;
                }

                float3 sunDir = normalize(_MazeEnv_SunDirectionWS.xyz);
                float3 scattering = 0.0.xxx;
                float transmittance = 1.0;
                float previousDistance = 0.0;
                float anisotropy = saturate(_MazeFog_LightingParams.x);
                float lightResponse = max(0.0, _MazeFog_LightingParams.y);
                float ambientResponse = saturate(_MazeFog_LightingParams.z);
                float cloudShadowStrength = saturate(_MazeFog_LightingParams.w);

                [loop]
                for (int i = 0; i < 64; i++)
                {
                    if (i >= stepCount)
                    {
                        break;
                    }

                    float segmentEnd01 = (i + 1.0) / max(1.0, (float)stepCount);
                    float segmentEnd = fogDistance * segmentEnd01 * segmentEnd01;
                    float stepSize = segmentEnd - previousDistance;
                    float sampleDistance = (previousDistance + segmentEnd) * 0.5;
                    float3 samplePos = _WorldSpaceCameraPos.xyz + viewDir * sampleDistance;
                    float heightTerm = MazeFogHeightTerm(samplePos.y);
                    float density;
                    float3 localFogColor;
                    MazeFogMedium(sampleDistance, includeDistanceBands, density, localFogColor);
                    float nearNoise;
                    float farNoise;
                    float noise;
                    MazeFogNoiseValues(samplePos, sampleDistance, nearNoise, farNoise, noise);
                    density *= intensity * heightTerm * noise * rayWispMultiplier;
                    density = max(0.0, density);

                    float phase = saturate(PhaseHG(dot(viewDir, sunDir), saturate(anisotropy * 0.75)) * 8.0);
                    float directionalLighting = lerp(1.0, phase, anisotropy);
                    float cloudShadow = 1.0;
                    if (cloudShadowStrength > 0.0001)
                    {
                        cloudShadow = lerp(1.0, MazeSampleCloudShadow(samplePos, sunDir), cloudShadowStrength);
                    }
                    float extinction = exp(-density * stepSize);
                    float3 skyColor = MazeFogSkyTint(normalize(samplePos - _WorldSpaceCameraPos.xyz), sunDir);

                    float3 stepLight = MazeFogTintAtAuthoredLuminance(localFogColor, skyColor, _MazeFog_ParamsB.y);
                    float lighting = saturate(ambientResponse + directionalLighting * lightResponse);
                    stepLight *= lighting * cloudShadow;
                    float stepOpacity = saturate(1.0 - extinction);
                    scattering += transmittance * stepLight * stepOpacity;
                    transmittance *= extinction;
                    previousDistance = segmentEnd;
                }

                float3 fogged = source * transmittance + scattering;
                volumeFogAmount = saturate(1.0 - transmittance) * opacityLimit;
                return lerp(source, fogged, opacityLimit);
            }

            float3 MazeApplyHorizonHaze(float3 source, float3 viewDir, float fogDistance, bool includeDistanceBands, float opacityLimit, float rayWispMultiplier, out float horizonFogAmount)
            {
                horizonFogAmount = 0.0;
                if (_MazeFog_ParamsA.x <= 0.5 || _MazeFog_ParamsC.x <= 0.0001 || _MazeFog_HorizonParams.x <= 0.0001 || fogDistance <= 0.01 || opacityLimit <= 0.0001)
                {
                    return source;
                }

                float3 sunDir = normalize(_MazeEnv_SunDirectionWS.xyz);
                float factor = MazeFogHorizonHaze(fogDistance, viewDir) * opacityLimit;
                factor *= lerp(1.0, rayWispMultiplier, saturate(_MazeFog_LocalParams.x) * 0.8);
                float3 skyColor = MazeFogSkyTint(viewDir, sunDir);
                float density;
                float3 baseColor;
                MazeFogMedium(fogDistance, includeDistanceBands, density, baseColor);

                float3 fogColor = MazeFogTintAtAuthoredLuminance(baseColor, skyColor, _MazeFog_ParamsB.y);

                float style = _MazeFog_ParamsB.w;
                if (style > 0.5)
                {
                    factor = pow(saturate(factor), lerp(1.0, 0.72, saturate(style * 0.5)));
                }

                horizonFogAmount = saturate(factor);
                return lerp(source, fogColor, saturate(factor));
            }

            half4 Frag(MazeFullscreenVaryings input) : SV_Target
            {
                float3 source = MazeSampleColorInput(input.uv);
                if (_MazeRenderDebug_WaterActive > 0.5 || _MazeRenderDebug_CloudMode > 0.5)
                {
                    return half4(source, 1.0);
                }

                if (_MazeFog_DebugView > 10.5 && _MazeFog_DebugView < 11.5)
                {
                    return half4(source, 1.0);
                }

                if (_MazeFog_DebugView > 11.5 && _MazeFog_DebugView < 12.5)
                {
                    return half4(_MazeFog_BandColors[0].rgb, 1.0);
                }

                float depth = SAMPLE_TEXTURE2D_X(_MazeFogDepthTexture, sampler_PointClamp, input.uv).r;
                bool isSky = MazeIsSkyDepth(depth);
                float3 viewDir = MazeFogViewDirectionWS(input.uv);
                float maxDistance = max(1.0, _MazeFog_ParamsA.y);
                float sceneDistance = maxDistance;
                float3 worldPos = _WorldSpaceCameraPos.xyz + viewDir * maxDistance;

                if (!isSky)
                {
                    worldPos = MazeReconstructWorldPosition(input.uv, depth);
                    float3 cameraToSurface = worldPos - _WorldSpaceCameraPos.xyz;
                    sceneDistance = length(cameraToSurface);
                    viewDir = cameraToSurface / max(0.00001, sceneDistance);
                }

                float fogDistance = isSky ? maxDistance : MazeClampAirDistanceBeforeWater(viewDir, min(sceneDistance, maxDistance));
                if (fogDistance > 0.01)
                {
                    float3 fogWorldPos = _WorldSpaceCameraPos.xyz + viewDir * fogDistance;
                    float debugMode = _MazeFog_DebugView;
                    if (debugMode > 0.5 && debugMode < 1.5)
                    {
                        return half4(isSky ? float3(1.0, 0.0, 0.0) : float3(0.0, 1.0, 0.0), 1.0);
                    }
                    if (debugMode > 1.5 && debugMode < 2.5)
                    {
                        float distance01 = saturate(fogDistance / maxDistance);
                        return half4(distance01.xxx, 1.0);
                    }
                    if (debugMode > 2.5 && debugMode < 3.5)
                    {
                        return half4(viewDir * 0.5 + 0.5, 1.0);
                    }
                    if (debugMode > 3.5 && debugMode < 4.5)
                    {
                        float height01 = saturate(fogWorldPos.y / max(1.0, _MazeFog_HeightParams.y) * 0.5 + 0.5);
                        return half4(height01.xxx, 1.0);
                    }
                    if (debugMode > 4.5 && debugMode < 5.5)
                    {
                        return half4(MazeFogBandDebugColor(viewDir, fogDistance), 1.0);
                    }
                    if (debugMode > 5.5 && debugMode < 6.5)
                    {
                        return half4(MazeFogHeightTerm(fogWorldPos.y).xxx, 1.0);
                    }
                    if (debugMode > 7.5 && debugMode < 10.5)
                    {
                        float nearNoise;
                        float farNoise;
                        float combinedNoise;
                        MazeFogNoiseValues(fogWorldPos, fogDistance, nearNoise, farNoise, combinedNoise);
                        if (debugMode < 8.5)
                        {
                            return half4(nearNoise.xxx, 1.0);
                        }
                        if (debugMode < 9.5)
                        {
                            return half4(farNoise.xxx, 1.0);
                        }
                        return half4(combinedNoise.xxx, 1.0);
                    }

                    float volumeFogAmount;
                    float horizonFogAmount;
                    float closeFogAmount;
                    // Sky rays end at Max Distance and traverse the same authored medium as far terrain.
                    bool includeDistanceBands = true;
                    float opacityLimit = saturate(_MazeFog_ParamsB.x) * (isSky ? saturate(_MazeFog_ParamsA.z) : 1.0);
                    float wispReferenceDistance = min(fogDistance, 45.0);
                    float3 wispReferencePosition = _WorldSpaceCameraPos.xyz + viewDir * wispReferenceDistance;
                    float rayWispMultiplier = MazeFogWispMultiplier(wispReferencePosition, 0.5);
                    source = MazeApplyVolumeFog(source, viewDir, fogDistance, includeDistanceBands, opacityLimit, rayWispMultiplier, volumeFogAmount);
                    if (debugMode > 12.5 && debugMode < 13.5)
                    {
                        return half4(source, 1.0);
                    }
                    source = MazeApplyHorizonHaze(source, viewDir, fogDistance, includeDistanceBands, opacityLimit, rayWispMultiplier, horizonFogAmount);
                    if (debugMode > 13.5 && debugMode < 14.5)
                    {
                        return half4(source, 1.0);
                    }
                    source = MazeApplyCloseFog(source, viewDir, fogDistance, opacityLimit, closeFogAmount);
                    if (debugMode > 14.5 && debugMode < 15.5)
                    {
                        return half4(source, 1.0);
                    }
                    if (debugMode > 6.5 && debugMode < 7.5)
                    {
                        float combinedFogAmount = 1.0 - (1.0 - volumeFogAmount) * (1.0 - horizonFogAmount) * (1.0 - closeFogAmount);
                        return half4(combinedFogAmount.xxx, 1.0);
                    }
                }

                return half4(source, 1.0);
            }
            ENDHLSL
        }
    }
}
