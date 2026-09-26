Shader "Hidden/Maze/Rendering/FilterObjectEdge"
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

            float4 _MazeFilterObjectEdge_Params;

            float MazeObjectEdgeSample(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, saturate(uv), 0).r;
            }

            float MazeObjectEdgeRadius(float2 uv, float pixels)
            {
                float2 screenTexel = 1.0 / max(float2(1.0, 1.0), _ScreenParams.xy);
                float2 radius = screenTexel * max(0.0, pixels);
                float mask = MazeObjectEdgeSample(uv);
                mask = max(mask, MazeObjectEdgeSample(uv + float2(radius.x, 0.0)));
                mask = max(mask, MazeObjectEdgeSample(uv + float2(-radius.x, 0.0)));
                mask = max(mask, MazeObjectEdgeSample(uv + float2(0.0, radius.y)));
                mask = max(mask, MazeObjectEdgeSample(uv + float2(0.0, -radius.y)));
                mask = max(mask, MazeObjectEdgeSample(uv + float2(radius.x, radius.y) * 0.7071));
                mask = max(mask, MazeObjectEdgeSample(uv + float2(-radius.x, radius.y) * 0.7071));
                mask = max(mask, MazeObjectEdgeSample(uv + float2(radius.x, -radius.y) * 0.7071));
                mask = max(mask, MazeObjectEdgeSample(uv + float2(-radius.x, -radius.y) * 0.7071));
                return saturate(mask);
            }

            half4 Frag(MazeFullscreenVaryings input) : SV_Target
            {
                float2 uv = input.uv;
                float raw = MazeObjectEdgeSample(uv);
                float extension = max(0.0, _MazeFilterObjectEdge_Params.x);
                float softness = max(0.25, _MazeFilterObjectEdge_Params.y);
                float expanded = max(raw, MazeObjectEdgeRadius(uv, extension * 0.25) * 0.9);
                expanded = max(expanded, MazeObjectEdgeRadius(uv, extension * 0.5) * 0.72);
                expanded = max(expanded, MazeObjectEdgeRadius(uv, extension) * 0.52);
                expanded = max(expanded, MazeObjectEdgeRadius(uv, extension + softness * 0.5) * 0.26);
                expanded = max(expanded, MazeObjectEdgeRadius(uv, extension + softness) * 0.08);
                return half4(raw, saturate(expanded), 0.0, 1.0);
            }
            ENDHLSL
        }
    }
}
