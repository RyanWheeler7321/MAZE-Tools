Shader "MAZE/Transition/GpuGrid"
{
    Properties
    {
        _MovingColor ("Moving Color", Color) = (1,0,1,0)
        _SettledColor ("Settled Color", Color) = (0,0,0,1)
        _OutColor ("Out Color", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+500" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _MovingColor;
            fixed4 _SettledColor;
            fixed4 _OutColor;
            float4 _ScreenSize;
            float4 _Grid;
            float4 _Timing;
            float4 _Runtime;
            float4 _Origin;
            float4 _Motion;
            float4 _Order;
            float4 _Delay;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            float hash11(float n)
            {
                return frac(sin(n) * 43758.5453123);
            }

            float easeValue(float t, float mode)
            {
                t = saturate(t);
                if (mode < 0.5)
                {
                    return t;
                }
                if (mode < 1.5)
                {
                    return t * t * (3.0 - 2.0 * t);
                }
                if (mode < 2.5)
                {
                    return t * t;
                }
                return 1.0 - (1.0 - t) * (1.0 - t);
            }

            float orderMetric(float order, float column, float row, float columns, float rows, float2 centerNormalized, float2 origin, float seed)
            {
                float total = max(1.0, columns * rows - 1.0);
                if (order > 0.5 && order < 1.5)
                {
                    return (column + row * columns) / total;
                }
                if (order > 1.5 && order < 2.5)
                {
                    return (row + column * rows) / total;
                }
                if (order > 2.5 && order < 3.5)
                {
                    return ((columns - 1.0 - column) + (rows - 1.0 - row) * columns) / total;
                }
                if (order > 3.5 && order < 4.5)
                {
                    return ((rows - 1.0 - row) + (columns - 1.0 - column) * rows) / total;
                }
                if (order > 4.5 && order < 5.5)
                {
                    return hash11(seed + column * 73856093.0 + row * 19349663.0);
                }
                return saturate(distance(centerNormalized, origin) / 1.41421356);
            }

            float spreadMetric(float direction, float2 centerNormalized, float2 origin, float seed, float directionOut)
            {
                if (direction > 11.5)
                {
                    direction = 1.0 + floor(hash11(seed + directionOut * 117.0) * 8.0);
                }

                float x = centerNormalized.x;
                float y = centerNormalized.y;
                if (direction > 0.5 && direction < 1.5) return x;
                if (direction > 1.5 && direction < 2.5) return 1.0 - x;
                if (direction > 2.5 && direction < 3.5) return y;
                if (direction > 3.5 && direction < 4.5) return 1.0 - y;
                if (direction > 4.5 && direction < 5.5) return (x + (1.0 - y)) * 0.5;
                if (direction > 5.5 && direction < 6.5) return ((1.0 - x) + (1.0 - y)) * 0.5;
                if (direction > 6.5 && direction < 7.5) return (x + y) * 0.5;
                if (direction > 7.5 && direction < 8.5) return ((1.0 - x) + y) * 0.5;
                if (direction > 8.5 && direction < 9.5) return saturate(distance(centerNormalized, float2(0.5, 0.5)) / 0.70710678);
                if (direction > 9.5 && direction < 10.5) return saturate(min(min(x, 1.0 - x), min(y, 1.0 - y)) * 2.0);
                if (direction > 10.5 && direction < 11.5)
                {
                    return saturate(min(min(x + y, (1.0 - x) + y), min(x + (1.0 - y), (1.0 - x) + (1.0 - y))));
                }
                return saturate(distance(centerNormalized, origin) / 1.41421356);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float columns = max(1.0, round(_Grid.x));
                float rows = max(1.0, round(_Grid.y));
                float overscan = max(0.0, _Grid.z);
                float gap = max(0.0, _Grid.w);
                float2 screen = max(float2(1.0, 1.0), _ScreenSize.xy);
                float2 pixel = i.uv * screen;
                float fullWidth = screen.x + overscan * 2.0;
                float fullHeight = screen.y + overscan * 2.0;
                float tileWidth = max(1.0, (fullWidth - gap * (columns - 1.0)) / columns);
                float tileHeight = max(1.0, (fullHeight - gap * (rows - 1.0)) / rows);
                float cellStepX = tileWidth + gap;
                float cellStepY = tileHeight + gap;
                float2 gridPixel = pixel + overscan;
                float column = clamp(floor(gridPixel.x / cellStepX), 0.0, columns - 1.0);
                float row = clamp(floor(gridPixel.y / cellStepY), 0.0, rows - 1.0);
                float2 localPixel = gridPixel - float2(column * cellStepX, row * cellStepY);
                if (localPixel.x > tileWidth || localPixel.y > tileHeight)
                {
                    discard;
                }

                float index = row * columns + column;
                float seed = _Runtime.w;
                float pattern = _Runtime.z;
                float directionOut = _Runtime.y;
                float staggerSeconds = max(0.0, _Delay.x);
                float randomDelayJitter = max(0.0, _Delay.y);
                float2 centerNormalized = float2((column + 0.5) / columns, (row + 0.5) / rows);
                float2 origin = _Origin.z > 0.5 ? _Origin.xy / screen : _Origin.xy;
                float delay;
                if (pattern > 0.5)
                {
                    delay = hash11(seed + index * 97.0) * randomDelayJitter;
                }
                else
                {
                    float metric = orderMetric(_Order.x, column, row, columns, rows, centerNormalized, origin, seed);
                    delay = saturate(metric) * staggerSeconds + hash11(seed + index * 97.0) * randomDelayJitter;
                }
                delay += easeValue(spreadMetric(_Order.y, centerNormalized, origin, seed, directionOut), _Delay.w) * max(0.0, _Delay.z);

                float localTime = _Runtime.x - delay;
                float moveSeconds = max(0.001, _Timing.x);
                float fadeSeconds = max(0.001, _Timing.y);
                float moveT = easeValue(localTime / moveSeconds, _Motion.z);
                float fadeT = easeValue(localTime / fadeSeconds, _Motion.w);
                float2 local01 = localPixel / max(float2(1.0, 1.0), float2(tileWidth, tileHeight));
                float scale = lerp(saturate(_Motion.x), 1.0, fadeT);
                float box = max(abs(local01.x - 0.5), abs(local01.y - 0.5));
                if (directionOut < 0.5)
                {
                    if (localTime <= 0.0 || box > 0.5 * max(0.001, scale))
                    {
                        discard;
                    }

                    fixed4 color = lerp(_MovingColor, _SettledColor, fadeT);
                    float shimmer = 1.0 + sin(localTime * 18.0 + hash11(seed + index * 8191.0) * 6.2831853) * _Motion.y * (1.0 - fadeT);
                    color.rgb *= shimmer;
                    color.a = lerp(_MovingColor.a, _SettledColor.a, fadeT);
                    return color * i.color;
                }

                if (localTime <= 0.0)
                {
                    return _SettledColor * i.color;
                }

                fixed4 outColor = lerp(_SettledColor, _OutColor, fadeT);
                outColor.a = lerp(_SettledColor.a, _OutColor.a, fadeT);
                if (outColor.a <= 0.001)
                {
                    discard;
                }
                return outColor * i.color;
            }
            ENDCG
        }
    }
}
