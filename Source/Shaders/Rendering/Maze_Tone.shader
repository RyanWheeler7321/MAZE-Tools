Shader "Hidden/Maze/Rendering/Tone"
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
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            float4 _MazeTone_ParamsA; // x mode, y strength, z exposure multiplier, w dither strength
            float4 _MazeTone_ParamsB; // x neutral contrast, y neutral white point, z neutral white clip, w aces contrast
            float4 _MazeTone_ParamsC; // x aces saturation, y reinhard white point, z gt max brightness, w gt contrast
            float4 _MazeTone_ParamsD; // x gt linear start, y gt linear length, z gt black tightness, w quality

            float3 MazeToneLuma(float3 color)
            {
                return dot(color, float3(0.2126, 0.7152, 0.0722)).xxx;
            }

            float3 MazeToneInputContrast(float3 color, float contrast)
            {
                contrast = max(0.001, contrast);
                if (abs(contrast - 1.0) <= 0.0001)
                {
                    return color;
                }

                const float midGrey = 0.18;
                return midGrey * pow(max(color, 0.0) / midGrey, contrast);
            }

            float3 MazeNeutralTonemap(float3 color)
            {
                color = MazeToneInputContrast(max(color, 0.0), _MazeTone_ParamsB.x);

                const real a = 0.2;
                const real b = 0.29;
                const real c = 0.24;
                const real d = 0.272;
                const real e = 0.02;
                const real f = 0.3;
                real whiteLevel = max(0.1, _MazeTone_ParamsB.y);
                real whiteClip = max(0.1, _MazeTone_ParamsB.z);

                #if REAL_IS_HALF
                    color = min(color, TONEMAPPING_CLAMP_MAX);
                #endif

                real3 whiteScale = (1.0).xxx / NeutralCurve(whiteLevel, a, b, c, d, e, f);
                real3 result = NeutralCurve(color * whiteScale, a, b, c, d, e, f);
                result *= whiteScale;
                result /= whiteClip.xxx;
                return result;
            }

            float3 MazeAcesTonemap(float3 color)
            {
                color = MazeToneInputContrast(max(color, 0.0), _MazeTone_ParamsB.w);
                if (_MazeTone_ParamsD.w < 1.5)
                {
                    const float a = 2.51;
                    const float b = 0.03;
                    const float c = 2.43;
                    const float d = 0.59;
                    const float e = 0.14;
                    float3 fast = (color * (a * color + b)) / (color * (c * color + d) + e);
                    float3 fastLuma = MazeToneLuma(fast);
                    return lerp(fastLuma, fast, saturate(_MazeTone_ParamsC.x));
                }

                float3 aces = unity_to_ACES(color);
                float3 result = AcesTonemap(aces);
                float3 luma = MazeToneLuma(result);
                return lerp(luma, result, saturate(_MazeTone_ParamsC.x));
            }

            float3 MazeReinhardExtended(float3 color)
            {
                color = max(color, 0.0);
                float whitePoint = max(0.1, _MazeTone_ParamsC.y);
                return color * (1.0 + color / (whitePoint * whitePoint)) / (1.0 + color);
            }

            float3 MazeGtTonemap(float3 color)
            {
                color = max(color, 0.0);
                float m = max(0.001, _MazeTone_ParamsD.x);
                float P = max(max(0.1, _MazeTone_ParamsC.z), m + 0.001);
                float a = max(0.1, _MazeTone_ParamsC.w);
                float l = max(0.001, _MazeTone_ParamsD.y);
                float c = max(0.1, _MazeTone_ParamsD.z);
                float b = 0.0;
                float l0 = min(((P - m) * l) / max(0.001, a), (P - m) / max(0.001, a) * 0.95);
                float S0 = m + l0;
                float S1 = m + a * l0;
                float C2 = (a * P) / max(0.001, P - S1);
                float CP = -C2 / P;

                float3 w0 = 1.0 - smoothstep(0.0, m, color);
                float3 w2 = step(m + l0, color);
                float3 w1 = 1.0 - w0 - w2;

                float3 toe = m * pow(max(color, 0.0) / max(0.001, m), c) + b;
                float3 midSegment = m + a * (color - m);
                float3 shoulder = P - (P - S1) * exp(CP * (color - S0));
                return toe * w0 + midSegment * w1 + shoulder * w2;
            }

            float3 MazeApplyTone(float3 exposed)
            {
                float mode = _MazeTone_ParamsA.x;
                if (mode < 0.5)
                {
                    return exposed;
                }

                if (mode < 1.5)
                {
                    return MazeNeutralTonemap(exposed);
                }

                if (mode < 2.5)
                {
                    return MazeAcesTonemap(exposed);
                }

                if (mode < 3.5)
                {
                    return MazeReinhardExtended(exposed);
                }

                return MazeGtTonemap(exposed);
            }

            float MazeToneDither(float2 uv)
            {
                float2 pixel = floor(uv * max(float2(1.0, 1.0), _ScreenParams.xy));
                return InterleavedGradientNoise(pixel) - 0.5;
            }

            half4 Frag(MazeFullscreenVaryings input) : SV_Target
            {
                float3 source = MazeSampleColorInput(input.uv);
                float exposure = max(0.0, _MazeTone_ParamsA.z);
                float3 exposed = max(0.0, source) * exposure;
                float3 linearClamp = saturate(exposed);
                float3 toned = saturate(MazeApplyTone(exposed));
                float3 result = lerp(linearClamp, toned, saturate(_MazeTone_ParamsA.y));

                if (_MazeTone_ParamsA.w > 0.0001)
                {
                    result += MazeToneDither(input.uv).xxx * (_MazeTone_ParamsA.w / 255.0);
                }

                return half4(saturate(result), 1.0);
            }
            ENDHLSL
        }
    }
}
