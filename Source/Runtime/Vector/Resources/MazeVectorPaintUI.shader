Shader "Hidden/MAZE/VectorPaintUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Source", 2D) = "white" {}
        _ControlMap ("Control Map", 2D) = "gray" {}
        _ControlScroll ("Control Scroll", Vector) = (0,0,0,0)
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="False"
        }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "MazeVectorPaintUI"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            sampler2D _ControlMap;
            fixed4 _Color;
            float4 _ControlScroll;
            float4 _ClipRect;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                half4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float2 localPosition : TEXCOORD2;
                half outputIntensity : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 ControlUv(float2 uv)
            {
                return uv + _ControlScroll.xy * _Time.y;
            }

            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float4 control = tex2Dlod(_ControlMap, float4(ControlUv(input.uv0.xy), 0, 0));
                input.vertex.xy += (control.rg * 2.0 - 1.0) * input.uv1.xy;
                output.localPosition = input.vertex.xy;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color * _Color;
                output.uv0 = input.uv0;
                output.uv1 = input.uv1;
                output.outputIntensity = max(1.0h, input.uv2.x);
                return output;
            }

            half4 frag(v2f input) : SV_Target
            {
                float4 control = tex2D(_ControlMap, ControlUv(input.uv0.xy));
                float2 sourceUv = input.uv0.xy + (control.rg * 2.0 - 1.0) * input.uv0.w;
                half4 sampleColor = tex2D(_MainTex, sourceUv);
                half4 paint = half4(1, 1, 1, 1);
                if (input.uv0.z > 0.5 && input.uv0.z < 1.5)
                {
                    paint = sampleColor;
                }
                else if (input.uv0.z >= 1.5 && input.uv0.z < 2.5)
                {
                    paint.a = sampleColor.a;
                }
                else if (input.uv0.z >= 2.5)
                {
                    paint.a = dot(sampleColor.rgb, half3(0.2126, 0.7152, 0.0722));
                }

                paint.rgb *= max(0.0, 1.0 + (control.b * 2.0 - 1.0) * input.uv1.z);
                paint.a *= lerp(1.0, control.a, saturate(input.uv1.w));
                half4 color = input.color * paint;
                color.rgb *= input.outputIntensity;
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.localPosition, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
