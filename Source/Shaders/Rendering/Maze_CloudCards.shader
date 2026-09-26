Shader "Hidden/Maze/Environment/CloudCards"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent-100"
            "RenderType" = "Transparent"
        }

        HLSLINCLUDE
        #pragma target 4.5
        #pragma vertex Vert
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MazeCloudCards_Atlas);
        SAMPLER(sampler_MazeCloudCards_Atlas);

        float4 _MazeCloudCards_Wind;
        float4 _MazeCloudCards_Reveal;
        float4 _MazeCloudCards_Look;
        float4 _MazeCloudCards_Output;
        float4 _MazeCloudCards_ShadowTint;
        float4 _MazeCloudCards_BodyTint;
        float4 _MazeCloudCards_HighlightTint;
        float4 _MazeCloudCards_Fade;
        float _MazeCloudCards_Mode;
        float4 _MazeEnv_SunDirectionWS;

        struct Attributes
        {
            float3 corner : POSITION;
            float2 uv : TEXCOORD0;
            float4 centerActivation : TEXCOORD1;
            float4 rightSpeed : TEXCOORD2;
            float4 upPhase : TEXCOORD3;
            float4 motion : TANGENT;
            float4 color : COLOR;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float activation : TEXCOORD1;
            float opacity : TEXCOORD2;
            float3 worldPos : TEXCOORD3;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            float uniformity = saturate(_MazeCloudCards_Wind.w);
            float2 mainDirection = normalize(_MazeCloudCards_Wind.xy + 0.00001.xx);
            float2 randomDirection = normalize(input.motion.xy + 0.00001.xx);
            float2 direction = normalize(lerp(randomDirection, mainDirection, uniformity));
            float speedFactor = lerp(max(0.05, input.rightSpeed.w), 1.0, uniformity);
            float phase = input.upPhase.w * 6.2831853;
            float2 perpendicular = float2(-direction.y, direction.x);
            float2 drift = direction * (_Time.y * _MazeCloudCards_Wind.z * speedFactor);
            drift += perpendicular * sin(_Time.y * (0.035 + input.upPhase.w * 0.04) + phase) * input.motion.z * (1.0 - uniformity);

            float3 center = input.centerActivation.xyz;
            center.xz += drift;
            if (_MazeCloudCards_Mode < 0.5)
            {
                center.xz += _WorldSpaceCameraPos.xz;
            }
            else
            {
                center += _WorldSpaceCameraPos.xyz;
            }

            float3 worldPos = center + input.rightSpeed.xyz * input.corner.x + input.upPhase.xyz * input.corner.y;
            output.positionCS = TransformWorldToHClip(worldPos);
            output.uv = input.uv;
            output.activation = input.centerActivation.w;
            output.opacity = input.color.a;
            output.worldPos = worldPos;
            return output;
        }

        float MazeCloudCardAlpha(Varyings input, out half4 source)
        {
            float reveal = saturate(_MazeCloudCards_Reveal.x);
            float active = step(input.activation, reveal);
            if (active <= 0.001)
            {
                discard;
            }

            source = SAMPLE_TEXTURE2D(_MazeCloudCards_Atlas, sampler_MazeCloudCards_Atlas, input.uv);
            float alpha = source.a * input.opacity * _MazeCloudCards_Reveal.z * saturate(_MazeCloudCards_Reveal.y) * active;
            if (_MazeCloudCards_Mode < 0.5)
            {
                float distanceToCamera = distance(input.worldPos, _WorldSpaceCameraPos.xyz);
                alpha *= smoothstep(_MazeCloudCards_Fade.x, max(_MazeCloudCards_Fade.x + 1.0, _MazeCloudCards_Fade.y), distanceToCamera);
                alpha *= lerp(1.0, saturate(input.worldPos.y / max(1.0, distanceToCamera) * 5.0 + 0.35), saturate(_MazeCloudCards_Fade.z));
            }
            else
            {
                alpha *= smoothstep(0.0, max(1.0, _MazeCloudCards_Fade.x), input.worldPos.y - _WorldSpaceCameraPos.y);
            }

            if (alpha <= 0.002)
            {
                discard;
            }

            return alpha;
        }
        ENDHLSL

        Pass
        {
            Name "CloudCards"
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma fragment Frag
            half4 Frag(Varyings input) : SV_Target
            {
                half4 source;
                float alpha = MazeCloudCardAlpha(input, source);
                float luminance = dot(source.rgb, float3(0.2126, 0.7152, 0.0722));
                float sourceTone = saturate((luminance - 0.5) * 0.35 + 0.5);
                float lightingTone = saturate(input.uv.y * 0.7 + sourceTone * 0.3);
                float3 tint = lightingTone < 0.5
                    ? lerp(_MazeCloudCards_ShadowTint.rgb, _MazeCloudCards_BodyTint.rgb, smoothstep(0.0, 0.5, lightingTone))
                    : lerp(_MazeCloudCards_BodyTint.rgb, _MazeCloudCards_HighlightTint.rgb, smoothstep(0.5, 1.0, lightingTone));
                float3 sourceLit = source.rgb * tint;
                float3 stylized = tint * lerp(0.72, 1.1, luminance);
                float3 color = lerp(stylized, sourceLit, saturate(_MazeCloudCards_Look.y));
                float sunHeight = saturate(_MazeEnv_SunDirectionWS.y * 0.5 + 0.5);
                color *= max(0.05, _MazeCloudCards_Look.w + sunHeight * _MazeCloudCards_Look.z);
                color *= exp2(_MazeCloudCards_Look.x);
                color *= _MazeCloudCards_Output.x;
                color = min(color, _MazeCloudCards_Output.y.xxx);
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "FocusClassification"
            BlendOp Max
            Blend One One
            ColorMask R
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma fragment Frag
            half4 Frag(Varyings input) : SV_Target
            {
                half4 source;
                float alpha = MazeCloudCardAlpha(input, source);
                float coverage = smoothstep(0.02, 0.35, alpha);
                return half4(coverage, 0.0, 0.0, 1.0);
            }
            ENDHLSL
        }
    }
}
