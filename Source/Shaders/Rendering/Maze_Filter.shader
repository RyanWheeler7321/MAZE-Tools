Shader "Hidden/Maze/Rendering/Filter"
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

            #define MAZE_FILTER_MAX_LAYERS 24 // Keep in sync with MazeFilterProfile.MaxLayers.
            #define MAZE_FILTER_MAX_TARGETS 8

            float _MazeFilter_LayerCount;
            float _MazeFilter_TargetMode;
            float _MazeFilter_TargetCount;
            float _MazeFilter_Sampling;
            float _MazeFilter_Time;
            float4 _MazeFilter_MaskA;
            float4 _MazeFilter_MaskB;
            float4 _MazeFilter_MaskC;
            float4 _MazeFilter_MaskD;
            float4 _MazeFilter_MaskE;
            float4 _MazeFilter_LayersA[MAZE_FILTER_MAX_LAYERS];
            float4 _MazeFilter_LayersB[MAZE_FILTER_MAX_LAYERS];
            float4 _MazeFilter_LayersC[MAZE_FILTER_MAX_LAYERS];
            float4 _MazeFilter_LayersD[MAZE_FILTER_MAX_LAYERS];
            float4 _MazeFilter_LayersE[MAZE_FILTER_MAX_LAYERS];
            float4 _MazeFilter_LayersF[MAZE_FILTER_MAX_LAYERS];
            float4 _MazeFilter_LayersG[MAZE_FILTER_MAX_LAYERS];
            float4 _MazeFilter_Targets[MAZE_FILTER_MAX_TARGETS];
            TEXTURE2D_X(_MazeFilter_ObjectMaskTex);

            float3 MazeFilterSampleColor(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, saturate(uv), 0).rgb;
            }

            float MazeFilterLuma(float3 color)
            {
                return dot(color, float3(0.2126, 0.7152, 0.0722));
            }

            float2 MazeFilterAngleVector(float degrees)
            {
                float radians = degrees * 0.01745329252;
                return float2(cos(radians), sin(radians));
            }

            float3 MazeFilterContrast(float3 color, float contrast)
            {
                return saturate((color - 0.5) * max(0.0, contrast) + 0.5);
            }

            float3 MazeFilterHueShift(float3 color, float shift)
            {
                float angle = shift * 6.2831853;
                float s = sin(angle);
                float c = cos(angle);
                float3 yiq;
                yiq.x = dot(color, float3(0.299, 0.587, 0.114));
                yiq.y = dot(color, float3(0.596, -0.274, -0.322));
                yiq.z = dot(color, float3(0.211, -0.523, 0.312));
                float i = yiq.y * c - yiq.z * s;
                float q = yiq.y * s + yiq.z * c;
                return saturate(float3(
                    yiq.x + 0.956 * i + 0.621 * q,
                    yiq.x - 0.272 * i - 0.647 * q,
                    yiq.x - 1.106 * i + 1.703 * q));
            }

            float MazeFilterBayer4(float2 pixel)
            {
                float2 p = floor(fmod(pixel, 4.0));
                float x = p.x;
                float row0 = x < 0.5 ? 0.0 : (x < 1.5 ? 8.0 : (x < 2.5 ? 2.0 : 10.0));
                float row1 = x < 0.5 ? 12.0 : (x < 1.5 ? 4.0 : (x < 2.5 ? 14.0 : 6.0));
                float row2 = x < 0.5 ? 3.0 : (x < 1.5 ? 11.0 : (x < 2.5 ? 1.0 : 9.0));
                float row3 = x < 0.5 ? 15.0 : (x < 1.5 ? 7.0 : (x < 2.5 ? 13.0 : 5.0));
                float value = p.y < 0.5 ? row0 : (p.y < 1.5 ? row1 : (p.y < 2.5 ? row2 : row3));
                return (value + 0.5) / 16.0;
            }

            float2 MazeFilterRotate(float2 value, float degrees)
            {
                float radians = degrees * 0.01745329252;
                float s = sin(radians);
                float c = cos(radians);
                return float2(c * value.x - s * value.y, s * value.x + c * value.y);
            }

            float MazeFilterPatternScale()
            {
                return max(0.1, min(_ScreenParams.x, _ScreenParams.y) / 1080.0);
            }

            float2 MazeFilterPatternScreenSize()
            {
                return max(float2(1.0, 1.0), _ScreenParams.xy / MazeFilterPatternScale());
            }

            float2 MazeFilterPatternPixel(float2 uv)
            {
                return uv * MazeFilterPatternScreenSize();
            }

            float MazeFilterValueNoise(float2 p, float seed)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash11(dot(i, float2(127.1, 311.7)) + seed);
                float b = Hash11(dot(i + float2(1.0, 0.0), float2(127.1, 311.7)) + seed);
                float c = Hash11(dot(i + float2(0.0, 1.0), float2(127.1, 311.7)) + seed);
                float d = Hash11(dot(i + float2(1.0, 1.0), float2(127.1, 311.7)) + seed);
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float2 MazeFilterEyeDepth(float2 uv)
            {
                uint2 pixel = (uint2)(saturate(uv) * max(float2(1.0, 1.0), _ScreenParams.xy - float2(1.0, 1.0)));
                float depth = LoadSceneDepth(pixel);
                if (MazeIsSkyDepth(depth))
                {
                    return float2(_ProjectionParams.z, 1.0);
                }

                return float2(LinearEyeDepth(depth, _ZBufferParams), 0.0);
            }

            float MazeFilterTargetMask(float2 uv)
            {
                float mask = 0.0;
                int count = (int)min(max(0.0, _MazeFilter_TargetCount), (float)MAZE_FILTER_MAX_TARGETS);
                [loop]
                for (int i = 0; i < MAZE_FILTER_MAX_TARGETS; i++)
                {
                    if (i >= count)
                    {
                        break;
                    }

                    float4 target = _MazeFilter_Targets[i];
                    float distanceToTarget = length(uv - target.xy);
                    float radius = max(0.0001, target.z);
                    float softness = max(0.00001, target.w);
                    mask = max(mask, 1.0 - smoothstep(radius, radius + softness, distanceToTarget));
                }

                return saturate(mask);
            }


            float2 MazeFilterSampleObjectMaskData(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_MazeFilter_ObjectMaskTex, sampler_LinearClamp, saturate(uv), 0).rg;
            }

            float MazeFilterObjectSilhouetteMask(float2 uv)
            {
                float2 maskData = MazeFilterSampleObjectMaskData(uv);
                float raw = maskData.r;
                float mode = _MazeFilter_MaskE.y;
                if (mode < 0.5)
                {
                    return raw;
                }

                float expanded = max(raw, maskData.g);
                expanded = pow(saturate(expanded), max(0.2, _MazeFilter_MaskA.w));

                if (mode < 1.5)
                {
                    return saturate(expanded - raw);
                }

                return expanded;
            }

            float MazeFilterScreenEdgeMask(float2 uv)
            {
                float minDimension = max(1.0, min(_ScreenParams.x, _ScreenParams.y));
                float edgeDistancePixels = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y)) * minDimension;
                float width = max(0.0, _MazeFilter_MaskB.x);
                float softness = max(0.001, _MazeFilter_MaskB.y);
                return 1.0 - smoothstep(width, width + softness, edgeDistancePixels);
            }

            float MazeFilterDepthBandMask(float2 uv)
            {
                float2 depthInfo = MazeFilterEyeDepth(uv);
                float eyeDepth = depthInfo.x;
                float isSky = depthInfo.y;
                float startDepth = min(_MazeFilter_MaskA.x, _MazeFilter_MaskA.y);
                float endDepth = max(_MazeFilter_MaskA.x, _MazeFilter_MaskA.y);
                float feather = max(0.001, _MazeFilter_MaskA.z);
                float enter = smoothstep(startDepth - feather, startDepth + feather, eyeDepth);
                float exit = 1.0 - smoothstep(endDepth - feather, endDepth + feather, eyeDepth);
                float depthMask = saturate(enter * exit);
                return lerp(depthMask, saturate(_MazeFilter_MaskA.w), isSky);
            }

            float MazeFilterRegionMask(float2 uv)
            {
                float2 center = _MazeFilter_MaskC.xy;
                float2 size = max(_MazeFilter_MaskC.zw, float2(0.0001, 0.0001));
                float softness = max(0.0001, _MazeFilter_MaskB.w);
                float shape = _MazeFilter_MaskB.z;
                float2 regionUv = uv - center;
                regionUv.x *= _ScreenParams.x / max(1.0, _ScreenParams.y);
                regionUv = MazeFilterRotate(regionUv, -_MazeFilter_MaskE.x);
                float2 regionSize = size;
                regionSize.x *= _ScreenParams.x / max(1.0, _ScreenParams.y);

                if (shape > 5.5)
                {
                    float halfWidth = max(0.0001, min(regionSize.x, regionSize.y) * 0.5);
                    float d = abs(regionUv.x);
                    return 1.0 - smoothstep(halfWidth, halfWidth + softness, d);
                }

                if (shape > 4.5)
                {
                    float radius = max(0.0001, min(regionSize.x, regionSize.y) * 0.5);
                    float d = abs(length(regionUv) - radius);
                    return 1.0 - smoothstep(softness, softness * 2.0, d);
                }

                if (shape > 3.5)
                {
                    float2 diamondSize = max(regionSize * 0.5, float2(0.0001, 0.0001));
                    float d = abs(regionUv.x / diamondSize.x) + abs(regionUv.y / diamondSize.y);
                    return 1.0 - smoothstep(1.0, 1.0 + softness * 4.0, d);
                }

                if (shape > 2.5)
                {
                    float halfWidth = max(0.0001, regionSize.x * 0.5);
                    float d = abs(regionUv.x);
                    return 1.0 - smoothstep(halfWidth, halfWidth + softness, d);
                }

                if (shape > 1.5)
                {
                    float halfHeight = max(0.0001, regionSize.y * 0.5);
                    float d = abs(regionUv.y);
                    return 1.0 - smoothstep(halfHeight, halfHeight + softness, d);
                }

                if (shape > 0.5)
                {
                    float radius = max(0.0001, min(regionSize.x, regionSize.y) * 0.5);
                    float d = length(regionUv);
                    return 1.0 - smoothstep(radius, radius + softness, d);
                }

                float2 halfSize = regionSize * 0.5;
                float2 d = abs(regionUv) - halfSize;
                float outside = length(max(d, float2(0.0, 0.0)));
                float inside = min(max(d.x, d.y), 0.0);
                float rectDistance = outside + inside;
                return 1.0 - smoothstep(0.0, softness, rectDistance);
            }

            float MazeFilterMask(float2 uv)
            {
                float mode = _MazeFilter_TargetMode;
                float mask = 1.0;
                if (mode < 0.5)
                {
                    mask = 1.0;
                }
                else if (mode < 1.5)
                {
                    mask = MazeFilterTargetMask(uv);
                }
                else if (mode < 2.5)
                {
                    mask = MazeFilterScreenEdgeMask(uv);
                }
                else if (mode < 3.5)
                {
                    mask = MazeFilterDepthBandMask(uv);
                }
                else if (mode < 4.5)
                {
                    mask = MazeFilterRegionMask(uv);
                }
                else
                {
                    mask = MazeFilterObjectSilhouetteMask(uv);
                }

                float sourceMask = saturate(mask);
                mask = saturate((sourceMask + _MazeFilter_MaskD.z - 0.5) * max(0.01, _MazeFilter_MaskD.y) + 0.5);
                mask = sourceMask <= 0.0001 ? 0.0 : mask;
                mask = lerp(mask, 1.0 - mask, saturate(_MazeFilter_MaskD.w));
                return saturate(mask * max(0.0, _MazeFilter_MaskD.x));
            }

            float2 MazePrismWarpPixel(float2 pixel, float cellSize, float distortionAmount, float motionRange, float seed, float timeSpeed)
            {
                float t = _MazeFilter_Time * max(0.0, timeSpeed);
                float relativeDistortion = distortionAmount / 32.0;
                float amountPixels = cellSize * relativeDistortion;
                float motionPixels = cellSize * motionRange * sin(t);

                if (abs(amountPixels) <= 0.001 && abs(motionPixels) <= 0.001)
                {
                    return pixel;
                }

                float2 p = pixel / max(2.0, cellSize);
                float2 noise = float2(
                    MazeFilterValueNoise(p * 0.7 + float2(seed * 0.017, t * 0.13), seed + 41.0),
                    MazeFilterValueNoise(p * 0.7 + float2(t * 0.11, seed * 0.023), seed + 79.0));
                float wave = sin((p.y + noise.y * 1.7) * 2.2 + seed * 0.17 + t) * 0.35;
                float2 motion = float2(cos(seed * 0.37 + t * 0.73), sin(seed * 0.23 + t * 0.61)) * motionPixels;
                return pixel
                    + (noise - 0.5) * amountPixels * 2.0
                    + float2(wave, -wave * 0.45) * amountPixels
                    + motion;
            }

            float2 MazePrismApplyAspect(float2 pixel, float2 center, float aspect)
            {
                float a = max(0.15, aspect);
                float stretch = sqrt(a);
                float2 local = pixel - center;
                return center + float2(local.x * stretch, local.y / stretch);
            }

            float2 MazePrismRemoveAspect(float2 pixel, float2 center, float aspect)
            {
                float a = max(0.15, aspect);
                float stretch = sqrt(a);
                float2 local = pixel - center;
                return center + float2(local.x / stretch, local.y * stretch);
            }

            float MazePrismTileSizeScale(float2 pixel, float cellSize, float sizeVariation, float seed, float minScale, float maxScale)
            {
                float amount = max(0.0, sizeVariation);
                if (amount <= 0.001)
                {
                    return 1.0;
                }

                float minTileScale = clamp(minScale, 1.0, 8.0);
                float maxTileScale = clamp(maxScale, minTileScale, 8.0);
                float strength = saturate(amount / 10.0);
                float allow2 = step(minTileScale, 2.0) * step(2.0, maxTileScale);
                float allow4 = step(minTileScale, 4.0) * step(4.0, maxTileScale);
                float allow8 = step(minTileScale, 8.0) * step(8.0, maxTileScale);

                float2 c8 = floor(pixel / (cellSize * 8.0));
                float h8 = Hash11(dot(c8, float2(17.1, 39.7)) + seed + 801.0);
                if (allow8 > 0.5 && h8 < saturate((amount - 6.0) / 4.0) * 0.38)
                {
                    return 8.0;
                }

                float2 c4 = floor(pixel / (cellSize * 4.0));
                float h4 = Hash11(dot(c4, float2(23.3, 61.9)) + seed + 401.0);
                if (allow4 > 0.5 && h4 < saturate((amount - 3.0) / 7.0) * 0.48)
                {
                    return 4.0;
                }

                float2 c2 = floor(pixel / (cellSize * 2.0));
                float h2 = Hash11(dot(c2, float2(43.1, 29.9)) + seed + 211.0);
                if (allow2 > 0.5 && h2 < strength * 0.62)
                {
                    return 2.0;
                }

                return 1.0;
            }

            float2 MazePrismVoronoiSamplePixel(float2 pixel, float cellSize, float scatter, float seed, float dirty)
            {
                float2 grid = pixel / cellSize;
                float2 baseCell = floor(grid);
                float2 bestCenter = (baseCell + 0.5) * cellSize;
                float bestDistance = 100000000.0;
                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 c = baseCell + float2((float)x, (float)y);
                        float h = dot(c, float2(23.7, 51.1)) + seed;
                        float2 jitter = (float2(Hash11(h), Hash11(h + 17.0)) - 0.5) * saturate(scatter + dirty * 0.35);
                        float2 center = (c + 0.5 + jitter) * cellSize;
                        float d = dot(pixel - center, pixel - center);
                        if (d < bestDistance)
                        {
                            bestDistance = d;
                            bestCenter = center;
                        }
                    }
                }

                return bestCenter;
            }

            float2 MazePrismHexSamplePixel(float2 pixel, float cellSize)
            {
                float size = max(2.0, cellSize * 0.58);
                float q = (0.57735026919 * pixel.x - 0.33333333333 * pixel.y) / size;
                float r = (0.66666666667 * pixel.y) / size;
                float3 cube = float3(q, -q - r, r);
                float3 rounded = floor(cube + 0.5);
                float3 diff = abs(rounded - cube);
                if (diff.x > diff.y && diff.x > diff.z)
                {
                    rounded.x = -rounded.y - rounded.z;
                }
                else if (diff.y > diff.z)
                {
                    rounded.y = -rounded.x - rounded.z;
                }
                else
                {
                    rounded.z = -rounded.x - rounded.y;
                }

                float rq = rounded.x;
                float rr = rounded.z;
                return float2(size * 1.73205080757 * (rq + rr * 0.5), size * 1.5 * rr);
            }

            float2 MazePrismDiamondSamplePixel(float2 pixel, float cellSize, float aspect)
            {
                float a = max(0.15, aspect);
                float2 b0 = float2(cellSize * a, cellSize * 0.5);
                float2 b1 = float2(-cellSize * a, cellSize * 0.5);
                float det = max(0.0001, b0.x * b1.y - b0.y * b1.x);
                float2 lattice = float2(
                    (pixel.x * b1.y - pixel.y * b1.x) / det,
                    (-pixel.x * b0.y + pixel.y * b0.x) / det);
                float2 cell = floor(lattice);
                return b0 * (cell.x + 0.5) + b1 * (cell.y + 0.5);
            }

            float2 MazePrismTriangleSamplePixel(float2 pixel, float cellSize)
            {
                float2 b0 = float2(cellSize, 0.0);
                float2 b1 = float2(cellSize * 0.5, cellSize * 0.86602540378);
                float det = max(0.0001, b0.x * b1.y - b0.y * b1.x);
                float2 lattice = float2(
                    (pixel.x * b1.y - pixel.y * b1.x) / det,
                    (-pixel.x * b0.y + pixel.y * b0.x) / det);
                float2 cell = floor(lattice);
                float2 f = frac(lattice);
                float upper = step(1.0, f.x + f.y);
                float2 triCenter = lerp(float2(0.3333333, 0.3333333), float2(0.6666667, 0.6666667), upper);
                return b0 * (cell.x + triCenter.x) + b1 * (cell.y + triCenter.y);
            }

            float2 MazePrismSquareTriangleSamplePixel(float2 pixel, float cellSize, float seed)
            {
                float2 grid = pixel / cellSize;
                float2 cell = floor(grid);
                float2 f = frac(grid);
                float flip = Hash11(dot(cell, float2(11.0, 47.0)) + seed) > 0.5 ? 1.0 : 0.0;
                float upper = flip < 0.5 ? step(1.0, f.x + f.y) : step(f.y, f.x);
                float2 a = flip < 0.5 ? float2(0.333, 0.333) : float2(0.667, 0.333);
                float2 b = flip < 0.5 ? float2(0.667, 0.667) : float2(0.333, 0.667);
                return (cell + lerp(a, b, upper)) * cellSize;
            }

            float2 MazePrismTessellatedSamplePixelCore(float2 pixel, float shape, float cellSize, float scatter, float seed, float aspect)
            {
                if (shape < 0.5)
                {
                    float2 cell = floor(pixel / cellSize);
                    return (cell + 0.5) * cellSize;
                }

                if (shape < 1.5)
                {
                    return MazePrismTriangleSamplePixel(pixel, cellSize);
                }

                if (shape < 2.5)
                {
                    return MazePrismVoronoiSamplePixel(pixel, cellSize, scatter * 0.45, seed, 0.0);
                }

                if (shape < 3.5)
                {
                    return MazePrismDiamondSamplePixel(pixel, cellSize, aspect);
                }

                if (shape < 4.5)
                {
                    return MazePrismHexSamplePixel(pixel, cellSize);
                }

                if (shape < 5.5)
                {
                    float2 grid = pixel / cellSize;
                    float row = floor(grid.y);
                    grid.x += fmod(abs(row), 2.0) * 0.5;
                    float2 cell = floor(grid);
                    return float2((cell.x + 0.5 - fmod(abs(row), 2.0) * 0.5) * cellSize, (row + 0.5) * cellSize);
                }

                if (shape < 6.5)
                {
                    float row = floor(pixel.y / cellSize);
                    float width = cellSize * lerp(0.55, 1.65, Hash11(row * 31.7 + seed));
                    float x = floor(pixel.x / width);
                    return float2((x + 0.5) * width, (row + 0.5) * cellSize);
                }

                if (shape < 7.5)
                {
                    float tendrilWidth = cellSize * lerp(0.32, 0.68, Hash11(seed + 3.0));
                    float tendrilHeight = cellSize * 2.1;
                    float2 warped = pixel;
                    warped.x += sin(pixel.y / cellSize * 1.7 + seed * 0.31) * cellSize * (0.35 + scatter);
                    float2 cell = floor(float2(warped.x / tendrilWidth, warped.y / tendrilHeight));
                    return float2((cell.x + 0.5) * tendrilWidth - sin((cell.y + 0.5) * tendrilHeight / cellSize * 1.7 + seed * 0.31) * cellSize * (0.35 + scatter), (cell.y + 0.5) * tendrilHeight);
                }

                if (shape < 8.5)
                {
                    return MazePrismVoronoiSamplePixel(pixel, cellSize, max(scatter, 0.45), seed, 1.0);
                }

                return MazePrismSquareTriangleSamplePixel(pixel, cellSize, seed);
            }

            float2 MazePrismTessellatedSamplePixel(float2 pixel, float2 center, float shape, float cellSize, float scatter, float seed, float aspect)
            {
                if (shape > 2.5 && shape < 3.5)
                {
                    return MazePrismTessellatedSamplePixelCore(pixel, shape, cellSize, scatter, seed, aspect);
                }

                float2 aspectPixel = MazePrismApplyAspect(pixel, center, aspect);
                float2 sampleAspectPixel = MazePrismTessellatedSamplePixelCore(aspectPixel, shape, cellSize, scatter, seed, 1.0);
                return MazePrismRemoveAspect(sampleAspectPixel, center, aspect);
            }

            float3 MazePrismSampleShape(float2 patternPixel, float2 center, float2 screenSize, float cellSize, float sampleCellSize, float shape, float scatter, float seed, float aspect, float shear, float rotation, float colorDrift)
            {
                float2 samplePatternPixel = MazePrismTessellatedSamplePixel(patternPixel, center, shape, sampleCellSize, scatter, seed, aspect);
                float sampleSeed = dot(floor(samplePatternPixel / max(2.0, sampleCellSize)), float2(23.7, 51.1)) + seed + shape * 37.0;
                float2 offset = (float2(Hash11(sampleSeed), Hash11(sampleSeed + 13.0)) - 0.5) * scatter * cellSize * 0.35;
                samplePatternPixel += offset;
                samplePatternPixel.x -= samplePatternPixel.y * shear;
                float2 samplePixel = MazeFilterRotate(samplePatternPixel - center, -rotation) + center;
                float3 sampled = MazeFilterSampleColor(samplePixel / screenSize);
                float drift = (Hash11(sampleSeed + 7.0) - 0.5) * 2.0;
                float brightness = lerp(1.0, lerp(0.82, 1.18, Hash11(sampleSeed + 19.0)), scatter);
                return saturate(MazeFilterHueShift(sampled, drift * colorDrift) * brightness);
            }

            float3 MazePrismPixelation(float2 uv, float3 current, float4 layerA, float4 layerB, float4 layerC, float4 layerD, float4 layerE, float forcedShape)
            {
                float cellSize = max(2.0, layerB.x);
                float2 screenSize = MazeFilterPatternScreenSize();
                float2 center = screenSize * 0.5;
                float rotation = layerE.x;
                float shear = layerD.w * 0.25;
                float2 patternPixel = MazeFilterRotate(MazeFilterPatternPixel(uv) - center, rotation) + center;
                patternPixel.x += patternPixel.y * shear;
                patternPixel = MazePrismWarpPixel(patternPixel, cellSize, layerC.w, layerE.z, layerB.z, layerB.w);
                float shape = forcedShape >= 0.0 ? forcedShape : layerA.y;
                float scatter = saturate(layerB.y);
                float sizeVariation = layerC.y;
                float sampleCellSize = cellSize * MazePrismTileSizeScale(MazePrismApplyAspect(patternPixel, center, layerE.y), cellSize, sizeVariation, layerB.z, layerC.x, layerD.x);
                float3 sampled = MazePrismSampleShape(patternPixel, center, screenSize, cellSize, sampleCellSize, shape, scatter, layerB.z, layerE.y, shear, rotation, layerD.y);
                float soft = saturate(layerC.z * 2.0);
                if (soft > 0.001 && _MazeFilter_Sampling >= 0.5)
                {
                    float radius = cellSize * max(0.01, layerC.z) * 0.65;
                    float3 softSample = sampled;
                    softSample += MazePrismSampleShape(patternPixel + float2(radius, 0.0), center, screenSize, cellSize, sampleCellSize, shape, scatter, layerB.z, layerE.y, shear, rotation, layerD.y);
                    softSample += MazePrismSampleShape(patternPixel - float2(radius, 0.0), center, screenSize, cellSize, sampleCellSize, shape, scatter, layerB.z, layerE.y, shear, rotation, layerD.y);
                    if (_MazeFilter_Sampling >= 0.8)
                    {
                        softSample += MazePrismSampleShape(patternPixel + float2(0.0, radius), center, screenSize, cellSize, sampleCellSize, shape, scatter, layerB.z, layerE.y, shear, rotation, layerD.y);
                        softSample += MazePrismSampleShape(patternPixel - float2(0.0, radius), center, screenSize, cellSize, sampleCellSize, shape, scatter, layerB.z, layerE.y, shear, rotation, layerD.y);
                        sampled = lerp(sampled, softSample * 0.2, soft);
                    }
                    else
                    {
                        sampled = lerp(sampled, softSample / 3.0, soft);
                    }
                }

                return sampled;
            }

            float3 MazeFilterBlur(float2 uv, float3 current, float radiusPixels, float samplesF)
            {
                int samples = (int)clamp(floor(samplesF + 0.5), 0.0, 12.0);
                if (samples <= 0 || radiusPixels <= 0.001)
                {
                    return current;
                }

                float2 texel = max(_BlitTexture_TexelSize.xy, 1.0 / _ScreenParams.xy);
                float3 accum = current * 2.0;
                float weightSum = 2.0;
                const float golden = 2.39996323;
                [loop]
                for (int i = 0; i < 12; i++)
                {
                    if (i >= samples)
                    {
                        break;
                    }

                    float t = ((float)i + 0.5) / max(1.0, (float)samples);
                    float angle = t * (float)samples * golden;
                    float radius = sqrt(t) * radiusPixels;
                    float2 sampleUv = saturate(uv + float2(cos(angle), sin(angle)) * radius * texel);
                    accum += MazeFilterSampleColor(sampleUv);
                    weightSum += 1.0;
                }

                return accum / max(0.0001, weightSum);
            }

            float2 MazeGridWarpUv(float2 uv, float4 layerB, float4 layerC)
            {
                float cellSize = max(1.0, layerB.x);
                float2 virtualScreenSize = MazeFilterPatternScreenSize();
                float2 gridUv = uv * virtualScreenSize / cellSize;
                float2 local = frac(gridUv);
                float edgeDistance = min(min(local.x, 1.0 - local.x), min(local.y, 1.0 - local.y));
                float edge = 1.0 - smoothstep(0.0, max(0.001, layerC.z), edgeDistance);
                float2 cell = floor(gridUv);
                float seed = dot(cell, float2(19.1, 41.7)) + layerB.z + _MazeFilter_Time * layerB.w;
                float2 direction = normalize(float2(Hash11(seed) - 0.5, Hash11(seed + 23.0) - 0.5) + float2(0.0001, 0.0001));
                float2 axis = abs(local.x - 0.5) > abs(local.y - 0.5) ? float2(sign(local.x - 0.5), 0.0) : float2(0.0, sign(local.y - 0.5));
                float2 push = normalize(lerp(axis, direction, saturate(layerB.y)) + float2(0.0001, 0.0001)) * layerC.w * edge;
                return saturate(uv + push / virtualScreenSize);
            }

            float3 MazePaletteCrush(float3 color, float4 layerD)
            {
                float steps = max(2.0, layerD.x);
                float3 crushed = floor(MazeFilterContrast(color, max(0.01, layerD.y)) * steps + 0.5) / steps;
                return saturate(crushed);
            }

            float3 MazeOrderedDither(float2 uv, float3 color, float4 layerD)
            {
                float steps = max(2.0, layerD.x);
                float threshold = (MazeFilterBayer4(MazeFilterPatternPixel(uv)) - 0.5) * max(0.0, layerD.z);
                return saturate(floor(color * steps + threshold + 0.5) / steps);
            }

            float3 MazeInkClamp(float3 color, float4 layerD)
            {
                float luma = MazeFilterLuma(color);
                float threshold = saturate(0.5 + (layerD.y - 1.0) * 0.12);
                float soft = max(0.001, layerD.z);
                float ink = smoothstep(threshold - soft, threshold + soft, luma);
                float3 clamped = lerp(color * 0.08, saturate(color * (1.0 + layerD.y * 0.4)), ink);
                return saturate(clamped);
            }

            float3 MazePosterBands(float3 color, float4 layerD)
            {
                float luma = max(0.0001, MazeFilterLuma(color));
                float steps = max(2.0, layerD.x);
                float band = floor(luma * steps + 0.5) / steps;
                return saturate(color * (band / luma));
            }

            void MazeSlipPalette(float palette, out float3 a, out float3 b)
            {
                a = float3(1.0, 0.18, 0.08);
                b = float3(0.0, 0.85, 1.0);
                if (palette > 0.5 && palette < 1.5)
                {
                    a = float3(1.0, 0.0, 0.85);
                    b = float3(0.0, 1.0, 0.35);
                }
                else if (palette > 1.5 && palette < 2.5)
                {
                    a = float3(0.1, 0.35, 1.0);
                    b = float3(1.0, 0.8, 0.0);
                }
                else if (palette > 2.5 && palette < 3.5)
                {
                    a = float3(1.0, 0.42, 0.12);
                    b = float3(0.15, 0.75, 1.0);
                }
                else if (palette > 3.5 && palette < 4.5)
                {
                    a = float3(0.7, 0.18, 1.0);
                    b = float3(1.0, 0.72, 0.05);
                }
            }

            float3 MazeChromaticSlip(float2 uv, float4 layerA, float4 layerC, float4 layerD)
            {
                float2 dir = MazeFilterAngleVector(layerD.w);
                float2 offset = dir * layerC.w / max(float2(1.0, 1.0), _ScreenParams.xy);
                float3 center = MazeFilterSampleColor(uv);

                if (layerA.y > 4.5)
                {
                    float r = MazeFilterSampleColor(uv + offset).r;
                    float g = center.g;
                    float b = MazeFilterSampleColor(uv - offset).b;
                    return float3(r, g, b);
                }

                float3 colorA;
                float3 colorB;
                MazeSlipPalette(layerA.y, colorA, colorB);
                float3 forward = MazeFilterSampleColor(uv + offset);
                float3 backward = MazeFilterSampleColor(uv - offset);
                float strength = saturate(layerD.y);
                float hold = lerp(0.25, 1.0, saturate(layerD.z));
                float3 slipped = center * hold;
                slipped += colorA * MazeFilterLuma(forward) * strength * 0.75;
                slipped += colorB * MazeFilterLuma(backward) * strength * 0.75;
                return saturate(lerp(center, slipped / max(0.5, hold + strength * 0.75), strength));
            }

            float MazeSobelLuma(float2 uv, float radiusPixels, out float2 gradient)
            {
                float2 texel = max(_BlitTexture_TexelSize.xy, 1.0 / _ScreenParams.xy) * max(1.0, radiusPixels);
                float l = MazeFilterLuma(MazeFilterSampleColor(uv - float2(texel.x, 0.0)));
                float r = MazeFilterLuma(MazeFilterSampleColor(uv + float2(texel.x, 0.0)));
                float d = MazeFilterLuma(MazeFilterSampleColor(uv - float2(0.0, texel.y)));
                float u = MazeFilterLuma(MazeFilterSampleColor(uv + float2(0.0, texel.y)));
                gradient = float2(r - l, u - d);
                return saturate(length(gradient) * 3.0);
            }

            float3 MazeEdgeBurn(float2 uv, float3 color, float4 layerC, float4 layerD)
            {
                float2 gradient;
                float edge = MazeSobelLuma(uv, max(1.0, layerC.x), gradient);
                edge = smoothstep(max(0.001, layerD.z), 1.0, edge * max(0.1, layerD.y));
                return saturate(color * (1.0 - edge * 0.75));
            }

            float MazeHalftoneShapeMask(float2 local, float shape, float size, float softness, float seed, float aspect)
            {
                float soft = max(0.001, softness * 0.35);
                float a = max(0.15, aspect);

                if (shape < 0.5)
                {
                    return 1.0 - smoothstep(size - soft, size + soft, length(local));
                }

                if (shape < 1.5)
                {
                    float d = max(abs(local.x), abs(local.y));
                    return 1.0 - smoothstep(size - soft, size + soft, d);
                }

                if (shape < 2.5)
                {
                    float2 p = local + 0.5;
                    p.y = Hash11(seed + 5.0) > 0.5 ? p.y : 1.0 - p.y;
                    float triangleEdge = (size * 2.0 - abs(p.x * 2.0 - 1.0)) - abs(p.y - 0.5) * 1.6;
                    return smoothstep(-soft, soft, triangleEdge);
                }

                if (shape < 3.5)
                {
                    float2 p = float2(local.x / a, local.y);
                    return 1.0 - smoothstep(size - soft, size + soft, abs(p.x) + abs(p.y));
                }

                return 0.0;
            }

            float3 MazeHalftoneColor(float2 uv, float3 color, float ink, float4 layerE, float4 layerF, float4 layerG)
            {
                float mode = layerE.z;
                if (mode < 0.5)
                {
                    return color * (1.0 - ink * 0.9);
                }

                float3 primary = layerF.rgb;
                float3 secondary = layerG.rgb;
                if (mode < 1.5)
                {
                    return lerp(color, primary, ink * saturate(layerF.a));
                }

                float2 dir = MazeFilterAngleVector(layerE.w);
                float gradient = saturate(dot(uv - 0.5, dir) + 0.5 + layerG.a);
                float3 gradientColor = lerp(primary, secondary, smoothstep(0.0, 1.0, gradient));
                if (mode < 2.5)
                {
                    return lerp(color, gradientColor, ink * max(saturate(layerF.a), 0.001));
                }

                if (mode < 3.5)
                {
                    return lerp(secondary, primary, ink);
                }

                return saturate(lerp(color, color * (primary * 2.0), ink * saturate(layerF.a)));
            }

            float3 MazeHalftoneShade(float2 uv, float3 color, float4 layerA, float4 layerB, float4 layerC, float4 layerD, float4 layerE, float4 layerF, float4 layerG)
            {
                float cellSize = max(2.0, layerB.x);
                float2 screenSize = MazeFilterPatternScreenSize();
                float2 center = screenSize * 0.5;
                float2 rotatedPixel = MazeFilterRotate(MazeFilterPatternPixel(uv) - center, layerE.x) + center;
                float2 grid = rotatedPixel / cellSize;
                grid.x += grid.y * layerD.w;
                float2 cell = floor(grid);
                float2 local = frac(grid) - 0.5;
                float seed = dot(cell, float2(17.0, 43.0)) + layerB.z;
                float luma = MazeFilterLuma(color);

                float toneMode = layerC.y;
                float areaThreshold = saturate(layerC.x);
                float areaSoftness = max(0.001, layerC.w);
                float thresholdLow = areaThreshold - areaSoftness;
                float thresholdHigh = areaThreshold + areaSoftness;
                float shade = 1.0 - luma;
                float applyMask = 1.0;
                if (toneMode < 0.5)
                {
                    applyMask = 1.0 - smoothstep(thresholdLow, thresholdHigh, luma);
                    shade = applyMask;
                }
                else if (toneMode < 1.5)
                {
                    applyMask = smoothstep(thresholdLow, thresholdHigh, luma);
                    shade = applyMask;
                }
                else if (toneMode < 2.5)
                {
                    float2 gradient;
                    float edge = MazeSobelLuma(uv, 1.0, gradient);
                    float edgeThreshold = lerp(0.04, 0.32, areaThreshold);
                    float edgeSoftness = max(0.001, areaSoftness * 0.35);
                    applyMask = 1.0 - smoothstep(edgeThreshold - edgeSoftness, edgeThreshold + edgeSoftness, edge);
                    shade = applyMask;
                }
                else if (toneMode < 3.5)
                {
                    float midDistance = abs(luma - areaThreshold);
                    float midWidth = max(0.001, areaSoftness * 0.5);
                    applyMask = 1.0 - smoothstep(midWidth, midWidth + max(0.001, areaSoftness * 0.25), midDistance);
                    shade = applyMask;
                }

                float printStrength = max(0.0, layerD.y);
                float size = lerp(0.04, 0.5, saturate(shade * printStrength));

                if (layerA.y > 3.5)
                {
                    float2 pixel = grid;
                    float2 axis = MazeFilterAngleVector(layerE.x);
                    float2 cross = float2(-axis.y, axis.x);
                    float lineWidth = max(0.02, 0.12 * max(0.1, printStrength));
                    float lineA = 1.0 - smoothstep(lineWidth, lineWidth + max(0.01, layerD.z), abs(frac(dot(pixel, axis)) - 0.5));
                    float lineB = 1.0 - smoothstep(lineWidth, lineWidth + max(0.01, layerD.z), abs(frac(dot(pixel, cross)) - 0.5));
                    float hatch = saturate(max(lineA * smoothstep(0.2, 0.85, shade), lineB * smoothstep(0.55, 1.0, shade)));
                    return lerp(color, MazeHalftoneColor(uv, color, hatch * applyMask, layerE, layerF, layerG), applyMask);
                }

                float shapeMask = MazeHalftoneShapeMask(local, layerA.y, size, layerD.z, seed, layerE.y);
                float ink = saturate(shapeMask * applyMask);
                return lerp(color, MazeHalftoneColor(uv, color, ink, layerE, layerF, layerG), applyMask);
            }

            float2 MazeSignalTearUv(float2 uv, float4 layerB, float4 layerC)
            {
                float bandHeight = max(2.0, layerB.x);
                float2 virtualScreenSize = MazeFilterPatternScreenSize();
                float band = floor(uv.y * virtualScreenSize.y / bandHeight);
                float seed = band * 13.37 + layerB.z + floor(_MazeFilter_Time * max(0.0, layerB.w) * 12.0);
                float gate = step(1.0 - saturate(layerB.y), Hash11(seed + 19.0));
                float offset = (Hash11(seed) - 0.5) * layerC.w * gate;
                return saturate(uv + float2(offset / max(1.0, virtualScreenSize.x), 0.0));
            }


            float2 MazeHeatRippleUv(float2 uv, float4 layerB, float4 layerC)
            {
                float waveScale = max(2.0, layerB.x) * 0.02;
                float t = _MazeFilter_Time * max(0.0, layerB.w);
                float wave = sin((uv.y + uv.x * 0.35) * waveScale * 80.0 + t * 3.1 + layerB.z);
                float wave2 = sin((uv.x - uv.y * 0.5) * waveScale * 63.0 - t * 2.3 + layerB.z * 1.7);
                return saturate(uv + float2(wave, wave2) * layerC.w / max(float2(1.0, 1.0), _ScreenParams.xy));
            }

            float3 MazeGhostEcho(float2 uv, float3 color, float4 layerC, float4 layerD)
            {
                int samples = (int)clamp(floor(layerC.y + 0.5), 1.0, 12.0);
                float2 dir = MazeFilterAngleVector(layerD.w);
                float2 stepUv = dir * layerC.x / max(float2(1.0, 1.0), _ScreenParams.xy);
                float3 accum = color;
                float weightSum = 1.0;
                [loop]
                for (int i = 1; i <= 12; i++)
                {
                    if (i > samples)
                    {
                        break;
                    }

                    float weight = exp2(-(float)i * 0.55);
                    accum += MazeFilterSampleColor(uv - stepUv * (float)i) * weight;
                    weightSum += weight;
                }

                return accum / max(0.0001, weightSum);
            }

            float3 MazePaperGrainWash(float2 uv, float3 color, float4 layerB, float4 layerD, float colorShift)
            {
                float2 pixel = floor(MazeFilterPatternPixel(uv));
                float grain = Hash11(dot(pixel, float2(0.173, 0.719)) + layerB.z + floor(_MazeFilter_Time * layerB.w));
                float luma = MazeFilterLuma(color);
                float3 washed = lerp(float3(luma, luma, luma), color, 0.72 + colorShift * 0.2);
                washed = MazeFilterContrast(washed, max(0.01, layerD.y));
                return saturate(washed + (grain - 0.5) * layerB.y);
            }

            float3 MazeKuwahara(float2 uv, float4 layerC)
            {
                float radius = max(1.0, layerC.x);
                float2 texel = max(_BlitTexture_TexelSize.xy, 1.0 / _ScreenParams.xy) * radius;
                float3 center = MazeFilterSampleColor(uv);
                if (_MazeFilter_Sampling < 0.75)
                {
                    float3 cross = center * 2.0;
                    cross += MazeFilterSampleColor(uv + float2(texel.x, 0.0));
                    cross += MazeFilterSampleColor(uv - float2(texel.x, 0.0));
                    cross += MazeFilterSampleColor(uv + float2(0.0, texel.y));
                    cross += MazeFilterSampleColor(uv - float2(0.0, texel.y));
                    return cross / 6.0;
                }

                float bestVariance = 100000.0;
                float3 bestColor = center;

                [unroll]
                for (int q = 0; q < 4; q++)
                {
                    float sx = (q == 0 || q == 2) ? -1.0 : 1.0;
                    float sy = (q < 2) ? -1.0 : 1.0;
                    float3 sum = 0.0;
                    float3 sumSq = 0.0;
                    float2 o0 = float2(0.0, 0.0);
                    float2 o1 = float2(sx, 0.0);
                    float2 o2 = float2(0.0, sy);
                    float2 o3 = float2(sx, sy);
                    float3 c0 = MazeFilterSampleColor(uv + o0 * texel);
                    float3 c1 = MazeFilterSampleColor(uv + o1 * texel);
                    float3 c2 = MazeFilterSampleColor(uv + o2 * texel);
                    float3 c3 = MazeFilterSampleColor(uv + o3 * texel);
                    sum = c0 + c1 + c2 + c3;
                    sumSq = c0 * c0 + c1 * c1 + c2 * c2 + c3 * c3;
                    float3 mean = sum * 0.25;
                    float3 variance = abs(sumSq * 0.25 - mean * mean);
                    float v = dot(variance, float3(1.0, 1.0, 1.0));
                    if (v < bestVariance)
                    {
                        bestVariance = v;
                        bestColor = mean;
                    }
                }

                return bestColor;
            }

            float3 MazeBlurSlope(float2 uv, float3 color, float4 layerC, float4 layerD)
            {
                float2 gradient;
                MazeSobelLuma(uv, 1.0, gradient);
                float2 dir = length(gradient) > 0.0001 ? normalize(float2(-gradient.y, gradient.x)) : MazeFilterAngleVector(layerD.w);
                int samples = (int)clamp(floor(layerC.y + 0.5), 1.0, 6.0);
                float2 stepUv = dir * layerC.x / max(float2(1.0, 1.0), _ScreenParams.xy);
                float3 accum = color;
                float weightSum = 1.0;
                [loop]
                for (int i = 1; i <= 6; i++)
                {
                    if (i > samples)
                    {
                        break;
                    }

                    float weight = 1.0 - (float)i / 7.0;
                    accum += MazeFilterSampleColor(uv + stepUv * (float)i) * weight;
                    accum += MazeFilterSampleColor(uv - stepUv * (float)i) * weight;
                    weightSum += weight * 2.0;
                }

                return accum / max(0.0001, weightSum);
            }

            float3 MazeSobelSketch(float2 uv, float3 color, float4 layerC, float4 layerD)
            {
                float2 gradient;
                float edge = MazeSobelLuma(uv, max(1.0, layerC.x), gradient);
                edge = smoothstep(max(0.001, layerD.z), 1.0, edge * max(0.1, layerD.y));
                float paper = saturate(MazeFilterLuma(color) * 0.35 + 0.72);
                return lerp(float3(paper, paper, paper), float3(0.02, 0.018, 0.014), edge);
            }

            float3 MazeWatercolorBleed(float2 uv, float3 color, float4 layerB, float4 layerC, float4 layerD, float colorShift)
            {
                float3 blur = MazeFilterBlur(uv, color, layerC.x, min(layerC.y, 6.0));
                float3 shifted = MazeFilterHueShift(blur, colorShift * 0.25);
                float steps = max(2.0, layerD.x);
                float3 pooled = floor(shifted * steps + 0.5) / steps;
                float scale = max(3.0, layerB.x * 0.35);
                float noiseA = MazeFilterValueNoise(uv * scale, layerB.z);
                float noiseB = MazeFilterValueNoise(uv * scale * 2.7 + 19.3, layerB.z + 37.0);
                float paper = noiseA * 0.7 + noiseB * 0.3;
                float poolMix = saturate(0.28 + layerD.z * 0.45);
                return saturate(lerp(shifted, pooled, poolMix) + (paper - 0.5) * layerB.y * 0.16);
            }


            float3 MazeFilterBlend(float3 baseColor, float3 blendColor, float mode)
            {
                if (mode < 0.5) return blendColor;
                if (mode < 1.5) return saturate(baseColor + blendColor);
                if (mode < 2.5) return saturate(baseColor * blendColor);
                if (mode < 3.5) return saturate(1.0 - (1.0 - baseColor) * (1.0 - blendColor));
                if (mode < 4.5)
                {
                    return lerp(2.0 * baseColor * blendColor, 1.0 - 2.0 * (1.0 - baseColor) * (1.0 - blendColor), step(0.5, baseColor));
                }
                if (mode < 5.5)
                {
                    return saturate((1.0 - 2.0 * blendColor) * baseColor * baseColor + 2.0 * blendColor * baseColor);
                }
                if (mode < 6.5) return min(baseColor, blendColor);
                if (mode < 7.5) return max(baseColor, blendColor);
                return saturate(baseColor + 2.0 * (blendColor - 0.5));
            }

            float3 MazeClarityHighPass(float2 uv, float3 color, float4 layerA, float4 layerC, float4 layerD)
            {
                float radius = max(0.0, layerC.x);
                float2 texel = 1.0 / max(float2(1.0, 1.0), _ScreenParams.xy);
                float2 r = texel * radius;
                float3 blur = color * 4.0;
                blur += MazeFilterSampleColor(uv + float2(r.x, 0.0));
                blur += MazeFilterSampleColor(uv + float2(-r.x, 0.0));
                blur += MazeFilterSampleColor(uv + float2(0.0, r.y));
                blur += MazeFilterSampleColor(uv + float2(0.0, -r.y));
                if (_MazeFilter_Sampling >= 0.6)
                {
                    blur += MazeFilterSampleColor(uv + float2(r.x, r.y) * 0.7071);
                    blur += MazeFilterSampleColor(uv + float2(-r.x, r.y) * 0.7071);
                    blur += MazeFilterSampleColor(uv + float2(r.x, -r.y) * 0.7071);
                    blur += MazeFilterSampleColor(uv + float2(-r.x, -r.y) * 0.7071);
                    blur *= 1.0 / 12.0;
                }
                else
                {
                    blur *= 1.0 / 8.0;
                }

                float strength = max(0.0, layerD.y);
                float3 detail = color - blur;
                float3 detailForLayer = detail;
                if (layerD.x > 0.5)
                {
                    detailForLayer = MazeFilterLuma(detail).xxx;
                }

                float3 highPassLayer = 0.5 + detailForLayer * strength;
                float detailEnergy = MazeFilterLuma(abs(detail));
                float edgeFloor = max(0.0, layerC.z);
                float softness = max(0.0001, layerC.w);
                float gate = edgeFloor <= 0.0001 ? 1.0 : smoothstep(edgeFloor, edgeFloor + softness, detailEnergy);
                float3 blended = MazeFilterBlend(color, highPassLayer, layerA.y);
                return lerp(color, blended, gate);
            }

            float3 MazeColorFill(float2 uv, float3 color, float4 layerA, float4 layerC, float4 layerD, float4 layerE, float4 layerF, float4 layerG)
            {
                float3 primary = saturate(layerF.rgb);
                float3 secondary = saturate(layerG.rgb);
                float gradientMode = layerC.y;
                float3 fill = primary;
                if (gradientMode > 0.5 && gradientMode < 1.5)
                {
                    float2 dir = MazeFilterAngleVector(layerE.w);
                    float t = saturate(dot(uv - 0.5, dir) + 0.5 + layerG.a);
                    fill = lerp(primary, secondary, smoothstep(0.0, 1.0, t));
                }
                else if (gradientMode > 1.5)
                {
                    float t = saturate(length(uv - 0.5) * 2.0 + layerG.a);
                    fill = lerp(primary, secondary, smoothstep(0.0, 1.0, t));
                }

                fill *= max(0.0, layerF.a);
                return MazeFilterBlend(color, fill, layerA.y);
            }

            float3 MazeApplyFilter(float2 uv, float3 current, float4 layerA, float4 layerB, float4 layerC, float4 layerD, float4 layerE, float4 layerF, float4 layerG)
            {
                float effect = layerA.x;
                float3 result = current;
                [branch]
                if (effect < 0.5)
                {
                    result = MazePrismPixelation(uv, current, layerA, layerB, layerC, layerD, layerE, -1.0);
                }
                else if (effect < 1.5)
                {
                    result = MazeFilterBlur(uv, current, layerC.x, layerC.y);
                }
                else if (effect < 2.5)
                {
                    result = MazeFilterSampleColor(MazeGridWarpUv(uv, layerB, layerC));
                }
                else if (effect < 3.5)
                {
                    result = MazePaletteCrush(current, layerD);
                }
                else if (effect < 4.5)
                {
                    result = MazeOrderedDither(uv, current, layerD);
                }
                else if (effect < 5.5)
                {
                    result = MazeInkClamp(current, layerD);
                }
                else if (effect < 6.5)
                {
                    result = MazePosterBands(current, layerD);
                }
                else if (effect < 7.5)
                {
                    result = MazeChromaticSlip(uv, layerA, layerC, layerD);
                }
                else if (effect < 8.5)
                {
                    result = MazeEdgeBurn(uv, current, layerC, layerD);
                }
                else if (effect < 9.5)
                {
                    result = MazeHalftoneShade(uv, current, layerA, layerB, layerC, layerD, layerE, layerF, layerG);
                }
                else if (effect < 10.5)
                {
                    result = MazeFilterSampleColor(MazeSignalTearUv(uv, layerB, layerC));
                }
                else if (effect < 11.5)
                {
                    result = MazeFilterSampleColor(MazeHeatRippleUv(uv, layerB, layerC));
                }
                else if (effect < 12.5)
                {
                    result = MazeGhostEcho(uv, current, layerC, layerD);
                }
                else if (effect < 13.5)
                {
                    result = MazePaperGrainWash(uv, current, layerB, layerD, layerA.y);
                }
                else if (effect < 14.5)
                {
                    result = MazeKuwahara(uv, layerC);
                }
                else if (effect < 15.5)
                {
                    result = MazeBlurSlope(uv, current, layerC, layerD);
                }
                else if (effect < 16.5)
                {
                    result = MazeSobelSketch(uv, current, layerC, layerD);
                }
                else if (effect < 17.5)
                {
                    result = MazeWatercolorBleed(uv, current, layerB, layerC, layerD, layerA.y);
                }
                else if (effect < 18.5)
                {
                    result = MazeClarityHighPass(uv, current, layerA, layerC, layerD);
                }
                else
                {
                    result = MazeColorFill(uv, current, layerA, layerC, layerD, layerE, layerF, layerG);
                }

                return result;
            }

            half4 Frag(MazeFullscreenVaryings input) : SV_Target
            {
                float2 uv = input.uv;
                float3 current = MazeFilterSampleColor(uv);
                float mask = MazeFilterMask(uv);
                int layerCount = (int)min(max(0.0, _MazeFilter_LayerCount), (float)MAZE_FILTER_MAX_LAYERS);

                [loop]
                for (int i = 0; i < MAZE_FILTER_MAX_LAYERS; i++)
                {
                    if (i >= layerCount)
                    {
                        break;
                    }

                    float4 layerA = _MazeFilter_LayersA[i];
                    float strength = saturate(mask * layerA.z * layerA.w);
                    if (strength <= 0.0001)
                    {
                        continue;
                    }

                    float3 filtered = MazeApplyFilter(uv, current, layerA, _MazeFilter_LayersB[i], _MazeFilter_LayersC[i], _MazeFilter_LayersD[i], _MazeFilter_LayersE[i], _MazeFilter_LayersF[i], _MazeFilter_LayersG[i]);
                    current = lerp(current, filtered, strength);
                }

                return half4(current, 1.0);
            }
            ENDHLSL
        }
    }
}
