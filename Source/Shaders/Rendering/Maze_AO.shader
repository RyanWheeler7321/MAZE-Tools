Shader "Hidden/Maze/Rendering/AO"
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float4 _MazeAO_ParamsA;
            float4 _MazeAO_ParamsB;
            float4 _MazeAO_ParamsC;
            float4 _MazeAO_ParamsD;
            TEXTURE2D_X(_MazeAODepthTexture);

            float MazeAOSampleDepth(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_MazeAODepthTexture, sampler_PointClamp, uv).r;
            }

            float MazeAOEyeDepth(float2 uv)
            {
                float depth = MazeAOSampleDepth(uv);
                if (MazeIsSkyDepth(depth))
                {
                    return -1.0;
                }

                return LinearEyeDepth(depth, _ZBufferParams);
            }

            float3 MazeAOWorld(float2 uv, float depth)
            {
                return MazeReconstructWorldPosition(uv, depth);
            }

            float3 MazeAONormal(float2 uv, float depth)
            {
                float3 normalWS = SampleSceneNormals(uv);
                float lengthSq = dot(normalWS, normalWS);
                if (lengthSq < 0.0001)
                {
                    return float3(0.0, 1.0, 0.0);
                }

                return normalize(normalWS);
            }

            float MazeAORaw(float2 uv)
            {
                if (_MazeAO_ParamsA.x <= 0.5)
                {
                    return 1.0;
                }

                float centerDeviceDepth = MazeAOSampleDepth(uv);
                if (MazeIsSkyDepth(centerDeviceDepth))
                {
                    return 1.0;
                }

                float centerEye = LinearEyeDepth(centerDeviceDepth, _ZBufferParams);
                float fade = 1.0 - smoothstep(_MazeAO_ParamsD.x, _MazeAO_ParamsD.y, centerEye);
                if (fade <= 0.0001)
                {
                    return 1.0;
                }

                float3 centerWorld = MazeAOWorld(uv, centerDeviceDepth);
                float3 normalWS = MazeAONormal(uv, centerDeviceDepth);
                float radiusPixels = max(0.1, _MazeAO_ParamsB.x);
                float radiusWorld = max(0.001, _MazeAO_ParamsB.y);
                float bias = saturate(_MazeAO_ParamsB.z);
                float thickness = max(0.001, _MazeAO_ParamsB.w);
                int samples = (int)max(0.0, _MazeAO_ParamsC.x);
                float2 texel = max(_BlitTexture_TexelSize.xy, 1.0 / _ScreenParams.xy);
                float occlusion = 0.0;
                float weightSum = 0.0;
                const float golden = 2.39996323;

                [loop]
                for (int i = 0; i < 40; i++)
                {
                    if (i >= samples)
                    {
                        break;
                    }

                    float t = ((float)i + 0.5) / max(1.0, (float)samples);
                    float angle = t * samples * golden;
                    float r = sqrt(t) * radiusPixels;
                    float2 sampleUv = saturate(uv + float2(cos(angle), sin(angle)) * r * texel);
                    float sampleDeviceDepth = MazeAOSampleDepth(sampleUv);
                    if (MazeIsSkyDepth(sampleDeviceDepth))
                    {
                        continue;
                    }

                    float3 sampleWorld = MazeAOWorld(sampleUv, sampleDeviceDepth);
                    float3 toSample = sampleWorld - centerWorld;
                    float worldDistance = length(toSample);
                    if (worldDistance <= 0.0001 || worldDistance > radiusWorld)
                    {
                        continue;
                    }

                    float sampleEye = LinearEyeDepth(sampleDeviceDepth, _ZBufferParams);
                    float eyeDelta = abs(sampleEye - centerEye);
                    float thicknessWeight = saturate(1.0 - eyeDelta / thickness);
                    float hemisphere = saturate(dot(normalWS, toSample / max(0.0001, worldDistance)) - bias);
                    float rangeWeight = 1.0 - smoothstep(radiusWorld * 0.25, radiusWorld, worldDistance);
                    float weight = max(0.0001, rangeWeight * thicknessWeight);
                    occlusion += hemisphere * weight;
                    weightSum += weight;
                }

                float occ = weightSum > 0.0001 ? occlusion / weightSum : 0.0;
                occ = saturate(occ * _MazeAO_ParamsA.y * fade);
                float ao = pow(saturate(1.0 - occ), max(0.1, _MazeAO_ParamsA.z));
                return ao;
            }

            float MazeAODenoised(float2 uv)
            {
                float raw = MazeAORaw(uv);
                float strength = saturate(_MazeAO_ParamsC.y);
                float radius = max(0.0, _MazeAO_ParamsC.z);
                if (strength <= 0.001 || radius <= 0.001)
                {
                    return raw;
                }

                float centerEye = MazeAOEyeDepth(uv);
                if (centerEye < 0.0)
                {
                    return 1.0;
                }

                float2 texel = max(_BlitTexture_TexelSize.xy, 1.0 / _ScreenParams.xy);
                float depthReject = max(0.01, _MazeAO_ParamsD.z);
                float centerDeviceDepth = MazeAOSampleDepth(uv);
                float3 centerNormal = MazeAONormal(uv, centerDeviceDepth);
                float accum = raw * 2.0;
                float weightSum = 2.0;
                float2 offsets[4] = { float2(1.0, 0.0), float2(-1.0, 0.0), float2(0.0, 1.0), float2(0.0, -1.0) };

                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    float2 sampleUv = saturate(uv + offsets[i] * radius * texel);
                    float sampleEye = MazeAOEyeDepth(sampleUv);
                    if (sampleEye < 0.0)
                    {
                        continue;
                    }

                    float sampleDeviceDepth = MazeAOSampleDepth(sampleUv);
                    float3 sampleNormal = MazeAONormal(sampleUv, sampleDeviceDepth);
                    float depthWeight = saturate(1.0 - abs(sampleEye - centerEye) / depthReject);
                    float normalWeight = saturate(dot(centerNormal, sampleNormal));
                    normalWeight *= normalWeight;
                    float weight = depthWeight * normalWeight;
                    float sampleAO = MazeAORaw(sampleUv);
                    accum += sampleAO * weight;
                    weightSum += weight;
                }

                return lerp(raw, accum / max(0.0001, weightSum), strength);
            }

            half4 Frag(MazeFullscreenVaryings input) : SV_Target
            {
                float3 source = MazeSampleColorInput(input.uv);
                float raw = MazeAORaw(input.uv);
                float ao = MazeAODenoised(input.uv);
                float debugMode = _MazeAO_ParamsC.w;
                if (debugMode > 1.5)
                {
                    return half4(ao.xxx, 1.0);
                }

                if (debugMode > 0.5)
                {
                    return half4(raw.xxx, 1.0);
                }

                float directLight = saturate(_MazeAO_ParamsA.w);
                float finalAO = lerp(ao, 1.0, directLight);
                return half4(source * finalAO, 1.0);
            }
            ENDHLSL
        }
    }
}
