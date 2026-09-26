Shader "Hidden/MAZE/VectorPaintWorld"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _ControlMap ("Control Map", 2D) = "gray" {}
        _ControlScroll ("Control Scroll", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "MazeVectorPaintWorld"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _ControlMap;
            float4 _ControlScroll;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
            };

            float2 ControlUv(float2 uv)
            {
                return uv + _ControlScroll.xy * _Time.y;
            }

            v2f vert(appdata input)
            {
                v2f output;
                float4 control = tex2Dlod(_ControlMap, float4(ControlUv(input.uv0.xy), 0, 0));
                input.vertex.xy += (control.rg * 2.0 - 1.0) * input.uv1.xy;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.uv0 = input.uv0;
                output.uv1 = input.uv1;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float4 control = tex2D(_ControlMap, ControlUv(input.uv0.xy));
                float2 sourceUv = input.uv0.xy + (control.rg * 2.0 - 1.0) * input.uv0.w;
                fixed4 sampleColor = tex2D(_MainTex, sourceUv);
                fixed4 paint = fixed4(1, 1, 1, 1);
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
                    paint.a = dot(sampleColor.rgb, fixed3(0.2126, 0.7152, 0.0722));
                }
                paint.rgb *= max(0.0, 1.0 + (control.b * 2.0 - 1.0) * input.uv1.z);
                paint.a *= lerp(1.0, control.a, saturate(input.uv1.w));
                return input.color * paint;
            }
            ENDCG
        }
    }
}
