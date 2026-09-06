Shader "SheIsNotHuman/Face Depth Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _PixelBlurRadius ("Pixel Blur Radius", Range(0, 3)) = 0
        _LocalWarp ("Local Warp", Range(-0.5, 0.5)) = 0
        _WarpCenter ("Warp Center", Vector) = (0.5, 0.5, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "CanUseSpriteAtlas" = "True"
        }

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }

            Cull Off
            ZWrite Off
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _PixelBlurRadius;
                float _LocalWarp;
                float4 _WarpCenter;
            CBUFFER_END

            float4 _MainTex_TexelSize;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            half4 SamplePremultiplied(float2 uv, half4 tint)
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(uv)) * tint;
                color.rgb *= color.a;
                return color;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv - _WarpCenter.xy;
                float radiusSquared = dot(centered, centered);
                float2 warpedUv = _WarpCenter.xy + centered * (1.0 + _LocalWarp * radiusSquared);

                float blurRadius = round(clamp(_PixelBlurRadius, 0.0, 3.0));
                if (blurRadius < 0.5)
                {
                    return SamplePremultiplied(warpedUv, input.color);
                }

                float2 texel = _MainTex_TexelSize.xy * blurRadius;
                half4 sum = 0.0h;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        sum += SamplePremultiplied(warpedUv + float2(x, y) * texel, input.color);
                    }
                }

                return sum / 9.0h;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Off
            ZWrite Off
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _PixelBlurRadius;
                float _LocalWarp;
                float4 _WarpCenter;
            CBUFFER_END

            float4 _MainTex_TexelSize;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            half4 SamplePremultiplied(float2 uv, half4 tint)
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(uv)) * tint;
                color.rgb *= color.a;
                return color;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv - _WarpCenter.xy;
                float radiusSquared = dot(centered, centered);
                float2 warpedUv = _WarpCenter.xy + centered * (1.0 + _LocalWarp * radiusSquared);
                float blurRadius = round(clamp(_PixelBlurRadius, 0.0, 3.0));

                if (blurRadius < 0.5)
                {
                    return SamplePremultiplied(warpedUv, input.color);
                }

                float2 texel = _MainTex_TexelSize.xy * blurRadius;
                half4 sum = 0.0h;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        sum += SamplePremultiplied(warpedUv + float2(x, y) * texel, input.color);
                    }
                }

                return sum / 9.0h;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
