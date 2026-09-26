Shader "Hidden/Maze/Rendering/FilterObjectMask"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 4
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest [_ZTest]
        Pass
        {
            Name "Mask"
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float4 _MazeFilterObjectMask_DepthParams;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (_MazeFilterObjectMask_DepthParams.x > 0.5)
                {
                    float2 uv = saturate(input.screenPos.xy / max(0.000001, input.screenPos.w));
                    float sceneRawDepth = SampleSceneDepth(uv);
                    float sceneEyeDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
                    float objectEyeDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                    clip((sceneEyeDepth + _MazeFilterObjectMask_DepthParams.y) - objectEyeDepth);
                }

                return half4(1.0, 0.0, 0.0, 1.0);
            }
            ENDHLSL
        }
    }
}
