Shader "Hidden/Maze/Rendering/Bloom"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        HLSLINCLUDE
        #pragma vertex MazeFullscreenVert
        #include "MazeEnvironmentCommon.hlsl"

        TEXTURE2D_X(_MazeBloom_Texture);
        TEXTURE2D_X(_MazeBloom_HighTexture);
        float4 _MazeBloom_ParamsA;
        float4 _MazeBloom_ParamsB;
        float4 _MazeBloom_Tint;

        float MazeBloomLuminance(float3 color)
        {
            return max(max(color.r, color.g), color.b);
        }

        float3 MazeBloomSafe(float3 color)
        {
            float clampValue = _MazeBloom_ParamsB.x;
            if (clampValue > 0.0001)
            {
                color = min(color, clampValue.xxx);
            }
            return max(0.0.xxx, color);
        }

        float3 MazeBloomThreshold(float3 color)
        {
            color = MazeBloomSafe(color);
            float threshold = max(0.0, _MazeBloom_ParamsA.x);
            float knee = threshold * saturate(_MazeBloom_ParamsA.y) + 1e-5;
            float brightness = MazeBloomLuminance(color);
            float softness = clamp(brightness - threshold + knee, 0.0, 2.0 * knee);
            softness = softness * softness / (4.0 * knee + 1e-5);
            float contribution = max(brightness - threshold, softness) / max(brightness, 1e-5);
            return color * saturate(contribution);
        }

        float3 MazeBloomSampleBox(TEXTURE2D_X_PARAM(tex, samplerTex), float2 uv, float2 texel)
        {
            float3 c = 0.0.xxx;
            c += SAMPLE_TEXTURE2D_X(tex, samplerTex, uv + texel * float2(-1.0, -1.0)).rgb;
            c += SAMPLE_TEXTURE2D_X(tex, samplerTex, uv + texel * float2( 1.0, -1.0)).rgb;
            c += SAMPLE_TEXTURE2D_X(tex, samplerTex, uv + texel * float2(-1.0,  1.0)).rgb;
            c += SAMPLE_TEXTURE2D_X(tex, samplerTex, uv + texel * float2( 1.0,  1.0)).rgb;
            return c * 0.25;
        }
        ENDHLSL

        Pass
        {
            Name "Threshold"
            HLSLPROGRAM
            #pragma fragment Frag
            half4 Frag(MazeFullscreenVaryings input) : SV_Target
            {
                float3 source = MazeSampleColorInput(input.uv);
                return half4(MazeBloomThreshold(source), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Downsample"
            HLSLPROGRAM
            #pragma fragment Frag
            half4 Frag(MazeFullscreenVaryings input) : SV_Target
            {
                float2 texel = max(_BlitTexture_TexelSize.xy, 1.0 / _ScreenParams.xy);
                float3 color = MazeBloomSampleBox(TEXTURE2D_X_ARGS(_BlitTexture, sampler_LinearClamp), input.uv, texel);
                return half4(MazeBloomSafe(color), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Upsample"
            HLSLPROGRAM
            #pragma fragment Frag
            half4 Frag(MazeFullscreenVaryings input) : SV_Target
            {
                float2 texel = max(_BlitTexture_TexelSize.xy, 1.0 / _ScreenParams.xy);
                float3 low = MazeBloomSampleBox(TEXTURE2D_X_ARGS(_BlitTexture, sampler_LinearClamp), input.uv, texel);
                float3 high = SAMPLE_TEXTURE2D_X(_MazeBloom_HighTexture, sampler_LinearClamp, input.uv).rgb;
                float scatter = saturate(_MazeBloom_ParamsA.w);
                return half4(lerp(high, high + low, scatter), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Composite"
            HLSLPROGRAM
            #pragma fragment Frag
            half4 Frag(MazeFullscreenVaryings input) : SV_Target
            {
                float3 source = MazeSampleColorInput(input.uv);
                if (_MazeRenderDebug_WaterActive > 0.5 || _MazeRenderDebug_CloudMode > 0.5)
                {
                    return half4(source, 1.0);
                }

                float3 bloom = SAMPLE_TEXTURE2D_X(_MazeBloom_Texture, sampler_LinearClamp, input.uv).rgb;
                float streakIntensity = _MazeBloom_ParamsB.y;
                if (streakIntensity > 0.0001)
                {
                    float angle = _MazeBloom_ParamsB.w;
                    float2 dir = float2(cos(angle), sin(angle));
                    float2 texel = max(_BlitTexture_TexelSize.xy, 1.0 / _ScreenParams.xy) * _MazeBloom_ParamsB.z;
                    float3 streak = 0.0.xxx;
                    streak += SAMPLE_TEXTURE2D_X(_MazeBloom_Texture, sampler_LinearClamp, saturate(input.uv + dir * texel *  2.0)).rgb * 0.34;
                    streak += SAMPLE_TEXTURE2D_X(_MazeBloom_Texture, sampler_LinearClamp, saturate(input.uv - dir * texel *  2.0)).rgb * 0.34;
                    streak += SAMPLE_TEXTURE2D_X(_MazeBloom_Texture, sampler_LinearClamp, saturate(input.uv + dir * texel *  5.0)).rgb * 0.2;
                    streak += SAMPLE_TEXTURE2D_X(_MazeBloom_Texture, sampler_LinearClamp, saturate(input.uv - dir * texel *  5.0)).rgb * 0.2;
                    bloom += streak * streakIntensity;
                }

                bloom *= _MazeBloom_Tint.rgb * _MazeBloom_ParamsA.z;
                return half4(source + bloom, 1.0);
            }
            ENDHLSL
        }
    }
}
