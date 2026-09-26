Shader "Hidden/MAZE/TextureModify"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _CompareTex ("Compare", 2D) = "white" {}
        _BlurTex ("Blur", 2D) = "black" {}
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        sampler2D _CompareTex;
        sampler2D _BlurTex;
        float4 _MainTex_TexelSize;
        float _TileMode;
        float _MedianLightness;
        float _Exposure;
        float _Lightness;
        float _Contrast;
        float _Saturation;
        float _Vibrance;
        float _Temperature;
        float _Tint;
        float _Hue;
        float _Shadows;
        float _Highlights;
        float _BlackPoint;
        float _WhitePoint;
        float _Fade;
        float _Posterize;
        float _Invert;
        float2 _Direction;
        float _Radius;
        float _Amount;
        float _Channel;
        float _Wipe;
        float _Background;
        float _Neutral;

        float2 SampleUv(float2 uv)
        {
            return _TileMode > 0.5 ? frac(uv) : saturate(uv);
        }

        float SignedCubeRoot(float value)
        {
            return sign(value) * pow(abs(value), 1.0 / 3.0);
        }

        float3 LinearToOklab(float3 color)
        {
            float l = 0.4122214708 * color.r + 0.5363325363 * color.g + 0.0514459929 * color.b;
            float m = 0.2119034982 * color.r + 0.6806995451 * color.g + 0.1073969566 * color.b;
            float s = 0.0883024619 * color.r + 0.2817188376 * color.g + 0.6299787005 * color.b;
            float lRoot = SignedCubeRoot(l);
            float mRoot = SignedCubeRoot(m);
            float sRoot = SignedCubeRoot(s);
            return float3(
                0.2104542553 * lRoot + 0.7936177850 * mRoot - 0.0040720468 * sRoot,
                1.9779984951 * lRoot - 2.4285922050 * mRoot + 0.4505937099 * sRoot,
                0.0259040371 * lRoot + 0.7827717662 * mRoot - 0.8086757660 * sRoot);
        }

        float3 OklabToLinear(float3 lab)
        {
            float lRoot = lab.x + 0.3963377774 * lab.y + 0.2158037573 * lab.z;
            float mRoot = lab.x - 0.1055613458 * lab.y - 0.0638541728 * lab.z;
            float sRoot = lab.x - 0.0894841775 * lab.y - 1.2914855480 * lab.z;
            float l = lRoot * lRoot * lRoot;
            float m = mRoot * mRoot * mRoot;
            float s = sRoot * sRoot * sRoot;
            return float3(
                 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
                -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
                -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s);
        }

        float3 GamutMap(float3 lab)
        {
            float3 rgb = OklabToLinear(lab);
            [unroll]
            for (int iteration = 0; iteration < 6; iteration++)
            {
                if (all(rgb >= 0.0) && all(rgb <= 1.0))
                {
                    break;
                }
                lab.yz *= 0.82;
                rgb = OklabToLinear(lab);
            }
            return saturate(rgb);
        }

        float AdjustContrast(float lightness, float pivot, float amount)
        {
            float exponent = exp2(amount * 2.0);
            pivot = clamp(pivot, 0.08, 0.92);
            if (lightness < pivot)
            {
                return pivot * pow(saturate(lightness / pivot), exponent);
            }
            return 1.0 - (1.0 - pivot) * pow(saturate((1.0 - lightness) / (1.0 - pivot)), exponent);
        }

        float3 ApplyColor(float3 rgb)
        {
            rgb *= exp2(_Exposure);
            if (_Invert > 0.5)
            {
                rgb = 1.0 - rgb;
            }

            float temperature = _Temperature * 0.14;
            float tint = _Tint * 0.08;
            rgb *= max(float3(0.05, 0.05, 0.05), float3(1.0 + temperature + tint * 0.15, 1.0 + tint, 1.0 - temperature + tint * 0.15));

            float3 lab = LinearToOklab(max(rgb, 0.0));
            float blackPoint = saturate(_BlackPoint * 0.45);
            float whitePoint = clamp(1.0 - _WhitePoint * 0.45, blackPoint + 0.02, 1.0);
            lab.x = saturate((lab.x - blackPoint) / (whitePoint - blackPoint));
            lab.x = AdjustContrast(lab.x, _MedianLightness, _Contrast);

            if (_Lightness >= 0.0)
            {
                lab.x += _Lightness * (1.0 - lab.x);
            }
            else
            {
                lab.x *= 1.0 + _Lightness;
            }

            float shadowMask = 1.0 - smoothstep(0.18, 0.62, lab.x);
            float highlightMask = smoothstep(0.38, 0.86, lab.x);
            lab.x = saturate(lab.x + _Shadows * shadowMask * 0.28 + _Highlights * highlightMask * 0.28);

            float angle = radians(_Hue);
            float sineValue;
            float cosineValue;
            sincos(angle, sineValue, cosineValue);
            lab.yz = float2(lab.y * cosineValue - lab.z * sineValue, lab.y * sineValue + lab.z * cosineValue);

            float chroma = length(lab.yz);
            float saturationScale = _Saturation < 0.0 ? 1.0 + _Saturation : 1.0 + _Saturation * 2.0;
            float vibranceScale = 1.0 + _Vibrance * (1.0 - saturate(chroma / 0.24)) * 1.5;
            lab.yz *= max(0.0, saturationScale * vibranceScale);

            if (_Fade > 0.0)
            {
                lab.x = lerp(lab.x, 0.12 + lab.x * 0.74, _Fade);
                lab.yz *= 1.0 - _Fade * 0.28;
            }

            if (_Posterize > 0.001)
            {
                float levels = round(lerp(32.0, 2.0, saturate(_Posterize)));
                lab.x = round(lab.x * (levels - 1.0)) / max(1.0, levels - 1.0);
            }

            return GamutMap(lab);
        }

        fixed4 FragmentColor(v2f_img input) : SV_Target
        {
            float4 source = tex2D(_MainTex, SampleUv(input.uv));
            if (_Neutral > 0.5)
            {
                return source;
            }
            return float4(ApplyColor(source.rgb), source.a);
        }

        fixed4 FragmentBlur(v2f_img input) : SV_Target
        {
            float2 offset = _Direction * _MainTex_TexelSize.xy * max(0.0, _Radius);
            float4 color = tex2D(_MainTex, SampleUv(input.uv)) * 0.2270270270;
            color += tex2D(_MainTex, SampleUv(input.uv + offset * 1.3846153846)) * 0.3162162162;
            color += tex2D(_MainTex, SampleUv(input.uv - offset * 1.3846153846)) * 0.3162162162;
            color += tex2D(_MainTex, SampleUv(input.uv + offset * 3.2307692308)) * 0.0702702703;
            color += tex2D(_MainTex, SampleUv(input.uv - offset * 3.2307692308)) * 0.0702702703;
            color.a = tex2D(_MainTex, SampleUv(input.uv)).a;
            return color;
        }

        fixed4 FragmentDetail(v2f_img input) : SV_Target
        {
            float2 uv = SampleUv(input.uv);
            float4 source = tex2D(_MainTex, uv);
            float3 blurred = tex2D(_BlurTex, uv).rgb;
            return float4(source.rgb + (source.rgb - blurred) * _Amount, source.a);
        }

        fixed4 FragmentView(v2f_img input) : SV_Target
        {
            float4 source = tex2D(_MainTex, SampleUv(input.uv));
            if (_Channel < 0.5)
            {
                float checker = fmod(floor(input.uv.x * 32.0) + floor(input.uv.y * 32.0), 2.0);
                float3 background = lerp(0.14.xxx, 0.24.xxx, checker);
                if (_Background > 0.5 && _Background < 1.5) background = 0.0.xxx;
                else if (_Background > 1.5 && _Background < 2.5) background = 0.35.xxx;
                else if (_Background > 2.5) background = 1.0.xxx;
                return float4(source.rgb * source.a + background * (1.0 - source.a), 1.0);
            }
            if (_Channel < 1.5) return float4(source.rrr, 1.0);
            if (_Channel < 2.5) return float4(source.ggg, 1.0);
            if (_Channel < 3.5) return float4(source.bbb, 1.0);
            if (_Channel < 4.5) return float4(source.aaa, 1.0);
            float luminance = dot(source.rgb, float3(0.2126, 0.7152, 0.0722));
            return float4(luminance.xxx, 1.0);
        }

        fixed4 FragmentDifference(v2f_img input) : SV_Target
        {
            float2 uv = SampleUv(input.uv);
            float4 current = tex2D(_MainTex, uv);
            float4 preview = tex2D(_CompareTex, uv);
            float3 difference = abs(current.rgb - preview.rgb) * 4.0;
            return float4(difference, 1.0);
        }

        fixed4 FragmentWipe(v2f_img input) : SV_Target
        {
            float2 uv = SampleUv(input.uv);
            float4 current = tex2D(_MainTex, uv);
            float4 preview = tex2D(_CompareTex, uv);
            float edge = abs(input.uv.x - _Wipe);
            float4 result = input.uv.x < _Wipe ? current : preview;
            if (edge < 0.0025)
            {
                result = float4(0.89, 0.0, 0.55, 1.0);
            }
            return result;
        }
        ENDCG

        Pass
        {
            Name "Color"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragmentColor
            ENDCG
        }

        Pass
        {
            Name "Blur"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragmentBlur
            ENDCG
        }

        Pass
        {
            Name "Detail"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragmentDetail
            ENDCG
        }

        Pass
        {
            Name "View"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragmentView
            ENDCG
        }

        Pass
        {
            Name "Difference"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragmentDifference
            ENDCG
        }

        Pass
        {
            Name "Wipe"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragmentWipe
            ENDCG
        }
    }
}
