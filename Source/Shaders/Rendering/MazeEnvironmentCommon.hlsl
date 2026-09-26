#ifndef MAZE_ENVIRONMENT_COMMON_INCLUDED
#define MAZE_ENVIRONMENT_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

TEXTURE2D_X(_BlitTexture);
float4 _BlitTexture_TexelSize;

float _MazeEnv_Time;
float4 _MazeEnv_WindDirection;
float _MazeEnv_WindSpeed;
float4 _MazeEnv_SunDirectionWS;
float4 _MazeEnv_SunColor;
float _MazeEnv_SunIntensity;

float4 _MazeAtmosphere_SkyZenithColor;
float4 _MazeAtmosphere_SkyHorizonColor;
float4 _MazeAtmosphere_SkyGroundColor;
float4 _MazeAtmosphere_SkySunHaloColor;
float4 _MazeAtmosphere_SkyParams;
float4 _MazeAtmosphere_StarsParams;
float4 _MazeAtmosphere_StarsColor;
float4 _MazeAtmosphere_AuroraParams;
float4 _MazeAtmosphere_AuroraColorA;
float4 _MazeAtmosphere_AuroraColorB;
float _MazeAtmosphere_AuroraHorizonFade;
float4 _MazeAtmosphere_CloudParamsA;
float4 _MazeAtmosphere_CloudParamsB;
float4 _MazeAtmosphere_CloudLightingParams;
float4 _MazeAtmosphere_CloudAmbientColor;
float4 _MazeAtmosphere_CloudLightColor;
float4 _MazeAtmosphere_CloudShadowParams;
float4 _MazeAtmosphere_CloudShadowExtra;
float4 _MazeAtmosphere_CloudQualityParams;
float4 _MazeAtmosphere_CloudStyleParams;
TEXTURE3D(_MazeAtmosphere_LowerCloudTex);
SAMPLER(sampler_MazeAtmosphere_LowerCloudTex);
float4 _MazeAtmosphere_LowerCloudParams;
float4 _MazeAtmosphere_LowerCloudBoundsMin;
float4 _MazeAtmosphere_LowerCloudBoundsSize;
float _MazeRenderDebug_CloudMode;
float _MazeAtmosphere_SceneViewPreview;
TEXTURE2D(_MazeCloud_FakeShadowTex);
SAMPLER(sampler_MazeCloud_FakeShadowTex);
float4 _MazeCloud_FakeShadowParamsA;
float4 _MazeCloud_FakeShadowParamsB;
float4 _MazeCloud_FakeShadowTint;
float4 _MazeCloud_FakeShadowWind;

float4 _MazeWater_ShallowColor;
float4 _MazeWater_DeepColor;
float4 _MazeWater_EdgeColor;
float4 _MazeWater_FoamColor;
float4 _MazeWater_UnderwaterColor;
float4 _MazeWater_SurfaceParamsA;
float4 _MazeWater_SurfaceParamsB;
float4 _MazeWater_SurfaceParamsC;
float4 _MazeWater_ReflectionParams;
float4 _MazeWater_ReflectionExtra;
float4 _MazeWater_CloudParams;
float4 _MazeWater_ShorelineParams;
float4 _MazeWater_UnderwaterParams;
float4 _MazeWater_TransmissionParams;
float4 _MazeWater_TransmissionExtra;
float4 _MazeWater_CompositeParams;
float4 _MazeWater_VolumeParams;
float4 _MazeWater_VolumeExtra;
float _MazeWater_SurfaceY;
float _MazeRenderDebug_WaterActive;

float4 _MazeDistanceFog_Color;
float4 _MazeDistanceFog_Params;
float4 _MazeDistanceFog_HeightParams;

float4 _MazeVolumetricFog_Color;
float4 _MazeVolumetricFog_ParamsA;
float4 _MazeVolumetricFog_ParamsB;
float _MazeVolumetricFog_Anisotropy;
float _MazeVolumetricFog_LightResponse;

float4 _MazeLightShafts_Color;
float4 _MazeLightShafts_Params;
float _MazeLightShafts_SampleCount;
float _MazeLightShafts_BlurRadius;
float4 _MazeLightShafts_SunScreenPos;

float4 _MazeSsr_ParamsA;
float4 _MazeSsr_ParamsB;
float _MazeSsr_NormalReject;
float _MazeSsr_StrideJitter;

TEXTURE2D(_MazePlanarReflectionTexture);
SAMPLER(sampler_MazePlanarReflectionTexture);
float4 _MazePlanarReflectionParams;

struct MazeFullscreenAttributes
{
    uint vertexID : SV_VertexID;
};

struct MazeFullscreenVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
};

MazeFullscreenVaryings MazeFullscreenVert(MazeFullscreenAttributes input)
{
    MazeFullscreenVaryings output;
    output.uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
    output.positionCS = float4(output.uv * 2.0 - 1.0, 0.0, 1.0);
    output.uv.y = 1.0 - output.uv.y;
    return output;
}

float Hash11(float n)
{
    return frac(sin(n) * 43758.5453123);
}

float Hash21(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
}

float Hash31(float3 p)
{
    return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453123);
}

float InterleavedGradientNoise(float2 pixelCoord)
{
    return frac(52.9829189 * frac(dot(pixelCoord, float2(0.06711056, 0.00583715))));
}

float Noise2D(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float a = Hash21(i);
    float b = Hash21(i + float2(1.0, 0.0));
    float c = Hash21(i + float2(0.0, 1.0));
    float d = Hash21(i + float2(1.0, 1.0));
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float Noise3D(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float n = dot(i, float3(1.0, 57.0, 113.0));
    float3 u = f * f * (3.0 - 2.0 * f);

    float a = Hash11(n + 0.0);
    float b = Hash11(n + 1.0);
    float c = Hash11(n + 57.0);
    float d = Hash11(n + 58.0);
    float e = Hash11(n + 113.0);
    float f1 = Hash11(n + 114.0);
    float g = Hash11(n + 170.0);
    float h = Hash11(n + 171.0);

    return lerp(lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y), lerp(lerp(e, f1, u.x), lerp(g, h, u.x), u.y), u.z);
}

float Fbm2D(float2 p)
{
    float total = 0.0;
    float amplitude = 0.5;
    for (int i = 0; i < 5; i++)
    {
        total += Noise2D(p) * amplitude;
        p = p * 2.03 + float2(17.13, 11.7);
        amplitude *= 0.5;
    }

    return total;
}

float Fbm2DFast(float2 p)
{
    float total = 0.0;
    float amplitude = 0.55;
    [unroll]
    for (int i = 0; i < 3; i++)
    {
        total += Noise2D(p) * amplitude;
        p = p * 2.07 + float2(17.13, 11.7);
        amplitude *= 0.5;
    }

    return total;
}

float Fbm3D(float3 p)
{
    float total = 0.0;
    float amplitude = 0.5;
    for (int i = 0; i < 5; i++)
    {
        total += Noise3D(p) * amplitude;
        p = p * 2.01 + float3(13.1, 17.3, 19.7);
        amplitude *= 0.5;
    }

    return total;
}

float Fbm3DFast(float3 p)
{
    float total = 0.0;
    float amplitude = 0.55;
    [unroll]
    for (int i = 0; i < 3; i++)
    {
        total += Noise3D(p) * amplitude;
        p = p * 2.04 + float3(13.1, 17.3, 19.7);
        amplitude *= 0.5;
    }

    return total;
}

float PhaseHG(float cosTheta, float g)
{
    float gg = g * g;
    float denom = max(0.001, 1.0 + gg - 2.0 * g * cosTheta);
    return (1.0 - gg) / (4.0 * PI * pow(denom, 1.5));
}

bool MazeIsSkyDepth(float deviceDepth)
{
#if UNITY_REVERSED_Z
    // The cleared depth value is exactly zero. A larger fixed epsilon turns
    // valid distant geometry into sky based on the camera's near clip plane.
    return deviceDepth <= 0.0000001;
#else
    return deviceDepth >= 0.9999999;
#endif
}

float3 MazeReconstructWorldPosition(float2 uv, float deviceDepth)
{
    return ComputeWorldSpacePosition(uv, deviceDepth, UNITY_MATRIX_I_VP);
}

float3 MazeViewDirectionWS(float2 uv)
{
    float4 clip = float4(uv * 2.0 - 1.0, 1.0, 1.0);
    float4 view = mul(unity_CameraInvProjection, clip);
    float3 directionVS = normalize(view.xyz / max(view.w, 1e-5));
    return normalize(mul((float3x3)UNITY_MATRIX_I_V, directionVS));
}

float3 MazeSampleColorInput(float2 uv)
{
    return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
}

float MazeClampAirDistanceBeforeWater(float3 rayDirWS, float sceneDistance)
{
    if (_MazeWater_SurfaceParamsA.x <= 0.5 || sceneDistance <= 0.0)
    {
        return sceneDistance;
    }

    float cameraToWater = _MazeWater_SurfaceY - _WorldSpaceCameraPos.y;
    float rayToWater = cameraToWater / min(rayDirWS.y, -1e-4);
    bool cameraAboveWater = _WorldSpaceCameraPos.y > _MazeWater_SurfaceY + 0.01;
    bool rayEntersWater = rayDirWS.y < -1e-4 && rayToWater > 0.0 && rayToWater < sceneDistance;

    return cameraAboveWater && rayEntersWater ? rayToWater : sceneDistance;
}

bool MazeRayHitsWaterBeforeScene(float3 rayDirWS, float sceneDistance)
{
    float clampedDistance = MazeClampAirDistanceBeforeWater(rayDirWS, sceneDistance);
    return clampedDistance + 0.01 < sceneDistance;
}

float MazeNightFactor(float3 sunDirWS)
{
    return saturate((-sunDirWS.y + 0.08) * 4.0);
}

float3 MazeSkyColor(float3 rayDirWS, float3 sunDirWS)
{
    float dayFactor = saturate(sunDirWS.y * 0.45 + 0.55);
    float nightFactor = MazeNightFactor(sunDirWS);
    float upMask = saturate(rayDirWS.y * 0.5 + 0.5);
    float horizonMask = pow(saturate(1.0 - abs(rayDirWS.y)), max(0.1, _MazeAtmosphere_SkyParams.x));
    float groundMask = saturate(-rayDirWS.y * 3.0);

    float3 zenith = lerp(_MazeAtmosphere_SkyZenithColor.rgb * 0.16, _MazeAtmosphere_SkyZenithColor.rgb, dayFactor);
    float3 horizon = lerp(_MazeAtmosphere_SkyHorizonColor.rgb * 0.08, _MazeAtmosphere_SkyHorizonColor.rgb, dayFactor);
    float3 ground = _MazeAtmosphere_SkyGroundColor.rgb;

    float3 sky = lerp(ground, zenith, upMask);
    sky = lerp(sky, horizon, horizonMask);
    sky = lerp(sky, ground, groundMask);

    float sunMask = pow(saturate(dot(rayDirWS, sunDirWS)), max(0.01, _MazeAtmosphere_SkyParams.z));
    float moonMask = pow(saturate(dot(rayDirWS, -sunDirWS)), 18.0) * nightFactor;
    sky += _MazeAtmosphere_SkySunHaloColor.rgb * sunMask * _MazeAtmosphere_SkyParams.y;
    sky += _MazeAtmosphere_StarsColor.rgb * moonMask * 0.08;
    return sky;
}

float3 MazeStars(float3 rayDirWS)
{
    float intensity = _MazeAtmosphere_StarsParams.x;
    if (intensity <= 0.0001)
    {
        return 0.0.xxx;
    }

    float2 sphereUV = rayDirWS.xz / max(0.12, rayDirWS.y + 1.2);
    float2 drift = _MazeEnv_WindDirection.xy * (_MazeAtmosphere_StarsParams.w * _MazeEnv_Time);
    float2 p = sphereUV * 240.0 * _MazeAtmosphere_StarsParams.y + drift;
    float2 cell = floor(p);
    float2 local = frac(p) - 0.5;

    float starHash = Hash21(cell);
    float starMask = smoothstep(0.992, 0.9995, starHash);
    float starFalloff = saturate(1.0 - dot(local, local) * 4.0);
    float twinkle = 0.75 + 0.25 * sin(_MazeEnv_Time * (1.5 + starHash * 4.0) + starHash * 27.0) * _MazeAtmosphere_StarsParams.z;

    float giantLayer = smoothstep(0.998, 0.9998, Hash21(cell * 0.37 + 13.0)) * 0.5;
    float star = (starMask * starFalloff + giantLayer) * twinkle * intensity * saturate(rayDirWS.y);
    return _MazeAtmosphere_StarsColor.rgb * star;
}

float3 MazeAurora(float3 rayDirWS)
{
    float intensity = _MazeAtmosphere_AuroraParams.x;
    if (intensity <= 0.0001 || rayDirWS.y <= 0.01)
    {
        return 0.0.xxx;
    }

    float horizonFade = saturate(_MazeAtmosphere_AuroraHorizonFade);
    float heightMask = saturate((rayDirWS.y - horizonFade) / max(0.02, 1.0 - horizonFade));
    float2 uv = rayDirWS.xz / max(0.08, rayDirWS.y + 0.24);
    uv *= max(0.1, _MazeAtmosphere_AuroraParams.y);
    uv += _MazeEnv_WindDirection.xy * (_MazeAtmosphere_AuroraParams.z * _MazeEnv_Time);

    float curtain = Fbm2D(uv * 0.8 + float2(0.0, _MazeEnv_Time * 0.035));
    float ribbons = Fbm2D(float2(uv.x * 2.6, uv.y * 0.55 - _MazeEnv_Time * 0.02));
    float flutter = Fbm2D(float2(uv.x * 5.0, uv.y * 0.8 + _MazeEnv_Time * _MazeAtmosphere_AuroraParams.w * 0.03));

    float band = smoothstep(0.42, 0.9, curtain);
    float streak = smoothstep(0.3, 0.95, ribbons);
    float detail = lerp(0.75, 1.3, flutter);
    float mask = band * streak * detail * heightMask * heightMask;

    float colorLerp = saturate(ribbons * 0.75 + flutter * 0.25);
    float3 auroraColor = lerp(_MazeAtmosphere_AuroraColorA.rgb, _MazeAtmosphere_AuroraColorB.rgb, colorLerp);
    return auroraColor * mask * intensity;
}

float MazeCloudEnvelope(float worldY)
{
    float baseHeight = _MazeAtmosphere_CloudParamsB.z;
    float thickness = max(1.0, _MazeAtmosphere_CloudParamsB.w);
    float heightRatio = abs(worldY - baseHeight) / thickness;
    float threshold = saturate(heightRatio);
    threshold *= threshold;
    threshold *= threshold;
    threshold *= threshold;
    return threshold;
}

float MazeCloudEnvelopeStyled(float3 worldPos)
{
    float baseHeight = _MazeAtmosphere_CloudParamsB.z;
    float thickness = max(1.0, _MazeAtmosphere_CloudParamsB.w);
    float billowStrength = saturate(_MazeAtmosphere_CloudStyleParams.z);
    float billowScale = max(0.1, _MazeAtmosphere_CloudStyleParams.w);
    if (billowStrength > 0.0001)
    {
        float2 safeWind = _MazeEnv_WindDirection.xy;
        if (dot(safeWind, safeWind) < 0.0001)
        {
            safeWind = float2(1.0, 0.0);
        }

        float2 windDir = normalize(safeWind);
        float windTime = _MazeEnv_Time * _MazeEnv_WindSpeed * _MazeAtmosphere_CloudShadowExtra.y;
        float2 uv = worldPos.xz * _MazeAtmosphere_CloudParamsB.x * billowScale + windDir * windTime * 0.00035;
        float localLift = (Fbm2DFast(uv) - 0.5) * billowStrength * 2.0;
        baseHeight += localLift * thickness * 0.72;
        thickness *= lerp(1.0, 1.18, saturate(Fbm2DFast(uv * 0.7 + 5.1) * billowStrength));
    }

    float heightRatio = abs(worldPos.y - baseHeight) / thickness;
    float threshold = saturate(heightRatio);
    threshold *= threshold;
    threshold *= threshold;
    threshold *= threshold;
    return threshold;
}

float MazeLowerCloudDensity(float3 worldPos)
{
    if (_MazeAtmosphere_LowerCloudParams.x <= 0.5)
    {
        return 0.0;
    }

    float3 size = max(_MazeAtmosphere_LowerCloudBoundsSize.xyz, 1.0.xxx);
    float3 uvw = (worldPos - _MazeAtmosphere_LowerCloudBoundsMin.xyz) / size;
    if (any(uvw < 0.0.xxx) || any(uvw > 1.0.xxx))
    {
        return 0.0;
    }

    return saturate(SAMPLE_TEXTURE3D(_MazeAtmosphere_LowerCloudTex, sampler_MazeAtmosphere_LowerCloudTex, uvw).r * _MazeAtmosphere_LowerCloudParams.y);
}

float2 MazeTraceLowerCloudBounds(float3 rayOriginWS, float3 rayDirWS, float maxTraceDistance)
{
    if (_MazeAtmosphere_LowerCloudParams.x <= 0.5)
    {
        return float2(1.0, 0.0);
    }

    float3 boundsMin = _MazeAtmosphere_LowerCloudBoundsMin.xyz;
    float3 boundsMax = boundsMin + _MazeAtmosphere_LowerCloudBoundsSize.xyz;
    float3 safeDir = max(abs(rayDirWS), 0.00001.xxx) * lerp(-1.0.xxx, 1.0.xxx, step(0.0.xxx, rayDirWS));
    float3 invDir = rcp(safeDir);
    float3 t0 = (boundsMin - rayOriginWS) * invDir;
    float3 t1 = (boundsMax - rayOriginWS) * invDir;
    float3 nearT = min(t0, t1);
    float3 farT = max(t0, t1);
    float entry = max(max(nearT.x, nearT.y), nearT.z);
    float exit = min(min(farT.x, farT.y), farT.z);
    return float2(max(0.0, entry), min(exit, maxTraceDistance));
}

float MazeCloudTraceLowerY()
{
    float mainLower = _MazeAtmosphere_CloudParamsB.z - max(6.0, _MazeAtmosphere_CloudParamsB.w);
    if (_MazeAtmosphere_LowerCloudParams.x <= 0.5)
    {
        return mainLower;
    }

    return min(mainLower, _MazeAtmosphere_LowerCloudBoundsMin.y);
}

float MazeCloudTraceUpperY()
{
    float mainUpper = _MazeAtmosphere_CloudParamsB.z + max(6.0, _MazeAtmosphere_CloudParamsB.w);
    if (_MazeAtmosphere_LowerCloudParams.x <= 0.5)
    {
        return mainUpper;
    }

    return max(mainUpper, _MazeAtmosphere_LowerCloudBoundsMin.y + _MazeAtmosphere_LowerCloudBoundsSize.y);
}

float MazeCloudNoiseField(float3 worldPos)
{
    float2 safeWind = _MazeEnv_WindDirection.xy;
    if (dot(safeWind, safeWind) < 0.0001)
    {
        safeWind = float2(1.0, 0.0);
    }

    float2 windDir = normalize(safeWind);
    float windTime = _MazeEnv_Time * _MazeEnv_WindSpeed * _MazeAtmosphere_CloudShadowExtra.y;
    float2 wind = windDir * windTime * 0.0006;

    float2 baseUv = worldPos.xz * _MazeAtmosphere_CloudParamsB.x + wind;
    float primary = Fbm2D(baseUv * 1.35);
    float broad = Fbm2D(baseUv * 0.42 + 13.4);
    float wisps = Fbm2D(baseUv * 2.7 - 7.1);

    float3 detailPos = float3(worldPos.xz * _MazeAtmosphere_CloudParamsB.y, worldPos.y * 0.017 + windTime * 0.03);
    float detail = Fbm3D(detailPos);
    float breakup = Fbm3D(detailPos * 1.9 + 11.0);

    float coverage = saturate(_MazeAtmosphere_CloudParamsA.z);
    float density = max(primary * 0.65 + broad * 0.35, wisps * 0.5);
    float coverageThreshold = lerp(0.7, 0.16, coverage);
    density = saturate(density - coverageThreshold);
    density += (detail - 0.5) * _MazeAtmosphere_CloudLightingParams.x;
    density -= (breakup - 0.45) * (_MazeAtmosphere_CloudLightingParams.x * 0.45);
    density *= _MazeAtmosphere_CloudParamsA.w;

    float threshold = MazeCloudEnvelopeStyled(worldPos);
    float verticalMask = saturate(1.0 - threshold);
    verticalMask = verticalMask * verticalMask * (3.0 - 2.0 * verticalMask);
    return saturate(density) * verticalMask;
}

float MazeCloudNoiseFieldPerformance(float3 worldPos)
{
    float2 safeWind = _MazeEnv_WindDirection.xy;
    if (dot(safeWind, safeWind) < 0.0001)
    {
        safeWind = float2(1.0, 0.0);
    }

    float2 windDir = normalize(safeWind);
    float windTime = _MazeEnv_Time * _MazeEnv_WindSpeed * _MazeAtmosphere_CloudShadowExtra.y;
    float2 wind = windDir * windTime * 0.0006;
    float2 baseUv = worldPos.xz * _MazeAtmosphere_CloudParamsB.x + wind;
    float primary = Fbm2DFast(baseUv * 1.28);
    float broad = Fbm2DFast(baseUv * 0.38 + 13.4);
    float wisps = Noise2D(baseUv * 2.4 - 7.1);

    float3 detailPos = float3(worldPos.xz * (_MazeAtmosphere_CloudParamsB.y * 0.82), worldPos.y * 0.014 + windTime * 0.026);
    float detail = Fbm3DFast(detailPos);

    float coverage = saturate(_MazeAtmosphere_CloudParamsA.z);
    float density = max(primary * 0.66 + broad * 0.34, wisps * 0.42);
    float coverageThreshold = lerp(0.68, 0.15, coverage);
    density = saturate(density - coverageThreshold);
    density += (detail - 0.5) * (_MazeAtmosphere_CloudLightingParams.x * 0.72);
    float granularityStrength = saturate(_MazeAtmosphere_CloudStyleParams.x);
    if (granularityStrength > 0.0001)
    {
        float granularityScale = max(0.1, _MazeAtmosphere_CloudStyleParams.y);
        float2 grainUv = baseUv * granularityScale + windDir * windTime * 0.0002;
        float grain = Noise2D(grainUv) * 0.65 + Noise2D(grainUv * 2.11 + 19.7) * 0.35;
        density += (grain - 0.5) * granularityStrength;
    }

    density *= _MazeAtmosphere_CloudParamsA.w;

    float threshold = MazeCloudEnvelopeStyled(worldPos);
    float verticalMask = saturate(1.0 - threshold);
    verticalMask = verticalMask * verticalMask * (3.0 - 2.0 * verticalMask);
    return saturate(density) * verticalMask;
}

float MazeCloudRenderDensity(float3 worldPos)
{
    float mainDensity = 0.0;
    if (_MazeAtmosphere_CloudQualityParams.x > 0.5)
    {
        mainDensity = MazeCloudNoiseFieldPerformance(worldPos);
    }
    else
    {
        mainDensity = MazeCloudNoiseField(worldPos);
    }

    return saturate(max(mainDensity, MazeLowerCloudDensity(worldPos)));
}

float MazeCloudBaseDensity(float3 worldPos)
{
    if (_MazeAtmosphere_CloudParamsA.x <= 0.5)
    {
        return 0.0;
    }

    return saturate(max(MazeCloudNoiseField(worldPos) * 1.2, MazeLowerCloudDensity(worldPos)));
}

float MazeCloudShadowDensityFast(float3 worldPos)
{
    if (_MazeAtmosphere_CloudParamsA.x <= 0.5)
    {
        return 0.0;
    }

    float2 safeWind = _MazeEnv_WindDirection.xy;
    if (dot(safeWind, safeWind) < 0.0001)
    {
        safeWind = float2(1.0, 0.0);
    }

    float2 windDir = normalize(safeWind);
    float windTime = _MazeEnv_Time * _MazeEnv_WindSpeed * _MazeAtmosphere_CloudShadowExtra.y;
    float2 wind = windDir * windTime * 0.0006;
    float2 baseUv = worldPos.xz * _MazeAtmosphere_CloudParamsB.x + wind;
    float primary = Fbm2DFast(baseUv * 1.25);
    float broad = Fbm2DFast(baseUv * 0.36 + 13.4);
    float coverage = saturate(_MazeAtmosphere_CloudParamsA.z);
    float density = primary * 0.68 + broad * 0.42;
    float coverageThreshold = lerp(0.72, 0.18, coverage);
    density = saturate(density - coverageThreshold);
    density *= _MazeAtmosphere_CloudParamsA.w * 1.45;
    return saturate(max(density, MazeLowerCloudDensity(worldPos)));
}

float MazeCloudShadowDensity(float3 worldPos)
{
    if (_MazeAtmosphere_CloudQualityParams.w > 0.5)
    {
        return MazeCloudShadowDensityFast(worldPos);
    }

    return MazeCloudBaseDensity(worldPos);
}

float MazeSampleCloudShadow(float3 worldPos, float3 sunDirWS)
{
    float strength = _MazeAtmosphere_CloudShadowParams.x;
    if (strength <= 0.0001 || _MazeAtmosphere_CloudParamsA.x <= 0.5)
    {
        return 1.0;
    }

    float sunY = max(0.08, sunDirWS.y);
    float cloudHeight = _MazeAtmosphere_CloudParamsB.z + _MazeAtmosphere_CloudParamsB.w * 0.55;
    float t = (cloudHeight - worldPos.y) / sunY;
    float3 samplePos = worldPos + sunDirWS * t;
    float scaleRatio = max(0.25, _MazeAtmosphere_CloudShadowParams.y / max(0.0001, _MazeAtmosphere_CloudParamsB.x));
    samplePos.xz *= scaleRatio;
    samplePos.xz += normalize(_MazeEnv_WindDirection.xy) * (_MazeEnv_Time * _MazeAtmosphere_CloudShadowParams.z * 2.5);

    float softness = max(0.01, _MazeAtmosphere_CloudShadowParams.w);
    float contrast = max(0.01, _MazeAtmosphere_CloudShadowExtra.x);
    float radius = softness * 8.0;
    float density = 0.0;
    if (_MazeAtmosphere_CloudQualityParams.w > 0.5)
    {
        density += MazeCloudShadowDensity(samplePos);
    }
    else
    {
        density += MazeCloudShadowDensity(samplePos);
        density += MazeCloudShadowDensity(samplePos + float3(radius, 0.0, radius));
        density += MazeCloudShadowDensity(samplePos + float3(-radius, 0.0, -radius));
        density *= 0.333333;
    }

    if (_MazeAtmosphere_LowerCloudParams.x > 0.5 && _MazeAtmosphere_LowerCloudParams.z > 0.0001)
    {
        float lowerMidY = _MazeAtmosphere_LowerCloudBoundsMin.y + _MazeAtmosphere_LowerCloudBoundsSize.y * 0.55;
        float lowerT = (lowerMidY - worldPos.y) / sunY;
        float3 lowerSamplePos = worldPos + sunDirWS * lowerT;
        float lowerDensity = MazeLowerCloudDensity(lowerSamplePos);
        if (_MazeAtmosphere_CloudQualityParams.w <= 0.5)
        {
            lowerDensity += MazeLowerCloudDensity(lowerSamplePos + float3(radius, 0.0, radius));
            lowerDensity += MazeLowerCloudDensity(lowerSamplePos + float3(-radius, 0.0, -radius));
            lowerDensity *= 0.333333;
        }

        density = max(density, lowerDensity * _MazeAtmosphere_LowerCloudParams.z);
    }

    density = pow(saturate(density), contrast);
    return lerp(1.0, 1.0 - strength, density);
}

float3 MazeSampleFakeCloudShadow(float3 worldPos, float3 sunDirWS)
{
    float strength = _MazeCloud_FakeShadowParamsA.x;
    if (strength <= 0.0001)
    {
        return 1.0.xxx;
    }

    float worldSize = max(1.0, _MazeCloud_FakeShadowParamsA.y);
    float cloudAltitude = _MazeCloud_FakeShadowParamsA.z;
    float sunY = max(0.08, sunDirWS.y);
    float projectionDistance = max(0.0, cloudAltitude - worldPos.y) / sunY;
    float2 projected = worldPos.xz + sunDirWS.xz * projectionDistance;
    float2 wind = _MazeCloud_FakeShadowWind.xy;
    if (dot(wind, wind) < 0.0001)
    {
        wind = float2(1.0, 0.0);
    }

    projected += normalize(wind) * (_MazeEnv_Time * _MazeCloud_FakeShadowWind.z);
    projected += float2(frac(sin(_MazeCloud_FakeShadowWind.w * 12.9898) * 43758.5453), frac(sin(_MazeCloud_FakeShadowWind.w * 78.233) * 43758.5453)) * worldSize;
    float2 uv = frac(projected / worldSize);
    float coverage = SAMPLE_TEXTURE2D(_MazeCloud_FakeShadowTex, sampler_MazeCloud_FakeShadowTex, uv).r;
    coverage = saturate((coverage - _MazeCloud_FakeShadowParamsB.y) / max(0.001, 1.0 - _MazeCloud_FakeShadowParamsB.y));
    float softenedContrast = lerp(max(0.1, _MazeCloud_FakeShadowParamsB.x), 0.65, saturate(_MazeCloud_FakeShadowParamsA.w * 0.2));
    coverage = pow(coverage, softenedContrast);
    return lerp(1.0.xxx, _MazeCloud_FakeShadowTint.rgb, coverage * strength);
}

#endif
