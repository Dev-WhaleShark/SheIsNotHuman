Shader "SheIsNotHuman/Cube Face Lens"
{
    Properties
    {
        _BaseMap ("Face Texture", 2D) = "white" {}
        _LensCenter ("Lens Center", Vector) = (0.5, 0.5, 0, 0)
        _Distortion ("Distortion", Range(-1, 1)) = 0
        _Distortion2 ("Edge Distortion", Range(-1, 1)) = 0
        _Zoom ("Zoom", Range(0.5, 2)) = 1
        _ChromaticAberration ("Chromatic Aberration", Range(0, 0.05)) = 0
        _Vignette ("Vignette", Range(0, 1)) = 0
        [HideInInspector] _Aspect ("Aspect", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _LensCenter;
                float _Distortion;
                float _Distortion2;
                float _Zoom;
                float _ChromaticAberration;
                float _Vignette;
                float _Aspect;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 baseUv = input.uv;
                float safeAspect = max(_Aspect, 0.001);
                float2 centered = baseUv - _LensCenter.xy;
                float2 radial = float2(centered.x * safeAspect, centered.y);
                float radiusSquared = dot(radial, radial);
                float warp = 1.0 + (_Distortion * radiusSquared)
                    + (_Distortion2 * radiusSquared * radiusSquared);
                radial *= warp / max(_Zoom, 0.001);

                float2 warpedOffset = float2(radial.x / safeAspect, radial.y);
                float2 sampleUv = _LensCenter.xy + warpedOffset;
                float2 aberration = warpedOffset * _ChromaticAberration * (1.0 + radiusSquared);

                float2 redUv = saturate(sampleUv + aberration);
                float2 greenUv = saturate(sampleUv);
                float2 blueUv = saturate(sampleUv - aberration);

                half red = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, redUv).r;
                half green = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, greenUv).g;
                half blue = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, blueUv).b;

                float vignetteFactor = saturate(1.0 - (radiusSquared * 1.6));
                float vignette = lerp(1.0, vignetteFactor, saturate(_Vignette));
                return half4(half3(red, green, blue) * half(vignette), 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
