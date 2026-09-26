Shader "Hidden/Maze/Environment/LightFX"
{
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

            float4 _MazeLightFX_ShaftColor;
            float4 _MazeLightFX_ShaftParamsA;
            float4 _MazeLightFX_ShaftParamsB;
            float4 _MazeLightFX_ShaftParamsC;
            float4 _MazeLightFX_SourceScreenPos;
            float4 _MazeLightFX_FlareColor;
            float4 _MazeLightFX_FlareParamsA;
            float4 _MazeLightFX_FlareParamsB;
            float4 _MazeLightFX_FlareParamsC;

            float3 MazeApplyLightFXShafts(float3 source, float2 uv, float2 pixelCoord)
            {
                if (_MazeLightFX_ShaftParamsA.x <= 0.0001 || _MazeLightFX_SourceScreenPos.z <= 0.5)
                {
                    return source;
                }

                float2 toLight = _MazeLightFX_SourceScreenPos.xy - uv;
                float samplesFloat = max(1.0, _MazeLightFX_ShaftParamsB.x);
                float2 delta = toLight * _MazeLightFX_ShaftParamsB.y / samplesFloat;
                float2 cursor = uv;
                float dither = _MazeLightFX_ShaftParamsC.w > 0.5 ? InterleavedGradientNoise(pixelCoord) : 0.5;
                cursor += delta * (dither - 0.5);
                float illuminationDecay = 1.0;
                float shaft = 0.0;
                int samples = (int)max(8.0, samplesFloat);
                float occlusion = saturate(_MazeLightFX_ShaftParamsB.z);
                float sharpness = max(0.25, _MazeLightFX_ShaftParamsB.w);
                float shaftiness = saturate(_MazeLightFX_ShaftParamsC.x);
                float granularity = saturate(_MazeLightFX_ShaftParamsC.y);
                float noiseScale = max(0.001, _MazeLightFX_ShaftParamsC.z);

                [loop]
                for (int i = 0; i < 96; i++)
                {
                    if (i >= samples)
                    {
                        break;
                    }

                    cursor += delta;
                    if (any(cursor <= 0.0) || any(cursor >= 1.0))
                    {
                        break;
                    }

                    float depth = SampleSceneDepth(cursor);
                    float visibility = lerp(1.0, MazeIsSkyDepth(depth) ? 1.0 : 0.0, occlusion);
                    if (shaftiness > 0.0001 || granularity > 0.0001)
                    {
                        float2 fromLight = cursor - _MazeLightFX_SourceScreenPos.xy;
                        float rayAngle = atan2(fromLight.y, fromLight.x);
                        float rayDistance = length(fromLight);
                        float rayFrequency = lerp(2.0, 24.0, shaftiness);
                        float rayNoise = Fbm2DFast(float2(rayAngle * rayFrequency, rayDistance * noiseScale - _MazeEnv_Time * 0.04));
                        rayNoise = pow(max(0.0001, rayNoise), lerp(0.8, 4.0, saturate(shaftiness + granularity * 0.5)));
                        visibility *= lerp(1.0, rayNoise * 1.65, saturate(max(shaftiness, granularity)));
                    }

                    shaft += visibility * illuminationDecay * _MazeLightFX_ShaftParamsA.z;
                    illuminationDecay *= _MazeLightFX_ShaftParamsA.y;
                }

                float radialMask = saturate(1.0 - length(toLight) * 1.35);
                radialMask = pow(radialMask, sharpness * 2.0);
                float centerGlow = pow(saturate(1.0 - length(toLight) * 2.1), 6.0 * sharpness);
                float depth = SampleSceneDepth(uv);
                float localVisibility = MazeIsSkyDepth(depth) ? 1.0 : 0.0;
                float3 shafts = _MazeLightFX_ShaftColor.rgb * (shaft * radialMask + centerGlow * localVisibility * 0.4);
                shafts *= _MazeLightFX_ShaftParamsA.w * _MazeLightFX_ShaftParamsA.x;
                return source + shafts;
            }


            float MazeLightFXSourceVisibility(float2 uv)
            {
                if (_MazeLightFX_SourceScreenPos.z <= 0.5 || any(_MazeLightFX_SourceScreenPos.xy <= 0.0) || any(_MazeLightFX_SourceScreenPos.xy >= 1.0))
                {
                    return 0.0;
                }

                float depth = SampleSceneDepth(saturate(_MazeLightFX_SourceScreenPos.xy));
                float visible = MazeIsSkyDepth(depth) ? 1.0 : 0.0;
                return lerp(1.0, visible, saturate(_MazeLightFX_FlareParamsB.w));
            }

            float MazeLightFXStar(float2 toLight, float angle, float spikeCount, float starLength)
            {
                float dist = max(0.0001, length(toLight));
                float a = atan2(toLight.y, toLight.x) + angle;
                float spoke = abs(cos(a * max(1.0, spikeCount) * 0.5));
                spoke = pow(saturate(spoke), 48.0);
                float falloff = pow(saturate(1.0 - dist / max(0.01, starLength)), 3.0);
                return spoke * falloff / max(0.18, dist * 7.0);
            }

            float3 MazeApplyLightFXFlare(float3 source, float2 uv)
            {
                if (_MazeLightFX_FlareParamsA.x <= 0.0001)
                {
                    return source;
                }

                float visibility = MazeLightFXSourceVisibility(uv);
                if (visibility <= 0.0001)
                {
                    return source;
                }

                float2 lightUv = _MazeLightFX_SourceScreenPos.xy;
                float2 toLight = lightUv - uv;
                float dist = length(toLight);
                float halo = pow(saturate(1.0 - dist / max(0.01, _MazeLightFX_FlareParamsA.y)), 2.2) * _MazeLightFX_FlareParamsA.z;
                float star = MazeLightFXStar(toLight, _MazeLightFX_FlareParamsB.z, _MazeLightFX_FlareParamsB.x, _MazeLightFX_FlareParamsB.y) * _MazeLightFX_FlareParamsA.w;

                float ghostAccum = 0.0;
                float2 center = float2(0.5, 0.5);
                float2 axis = center - lightUv;
                int ghostCount = (int)min(max(0.0, _MazeLightFX_FlareParamsC.y), 8.0);
                [loop]
                for (int i = 0; i < 8; i++)
                {
                    if (i >= ghostCount)
                    {
                        break;
                    }

                    float t = ((float)i + 1.0) * _MazeLightFX_FlareParamsC.z;
                    float2 ghostUv = lightUv + axis * t;
                    float ghostDist = length(uv - ghostUv);
                    float ghost = pow(saturate(1.0 - ghostDist / (0.055 + 0.018 * i)), 3.0);
                    ghostAccum += ghost * (1.0 - (float)i / max(1.0, (float)ghostCount)) * _MazeLightFX_FlareParamsC.x;
                }

                float3 flare = _MazeLightFX_FlareColor.rgb * (halo + star + ghostAccum) * _MazeLightFX_FlareParamsA.x * visibility;

                float chromaPixels = _MazeLightFX_FlareParamsC.w;
                if (chromaPixels > 0.001)
                {
                    float2 dir = dist > 0.0001 ? normalize(-toLight) : float2(1.0, 0.0);
                    float chroma = saturate((halo + star + ghostAccum) * 0.5);
                    flare.r *= 1.0 + chroma * 0.18;
                    flare.b *= 1.0 + chroma * 0.32;
                    flare += _MazeLightFX_FlareColor.rgb * chroma * 0.04 * chromaPixels;
                }

                return source + flare;
            }

            half4 Frag(MazeFullscreenVaryings input) : SV_Target
            {
                float3 source = MazeSampleColorInput(input.uv);
                if (_MazeRenderDebug_WaterActive > 0.5 || _MazeRenderDebug_CloudMode > 0.5)
                {
                    return half4(source, 1.0);
                }

                source = MazeApplyLightFXShafts(source, input.uv, input.positionCS.xy);
                source = MazeApplyLightFXFlare(source, input.uv);
                return half4(source, 1.0);
            }
            ENDHLSL
        }
    }
}
