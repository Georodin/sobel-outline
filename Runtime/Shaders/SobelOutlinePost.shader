// URP fullscreen Sobel outline. Depth gives silhouettes, normals give interior creases.
// Include mask: objects opted in via SobelOutlineInclude or Cubus/SobelTransparent.
// Adapted from https://www.vertexfragment.com/ramblings/unity-postprocessing-sobel-outline/
Shader "Hidden/Cubus/SobelOutlinePost"
{
    Properties
    {
        _OutlineThickness ("Width", Float) = 1.2
        _OutlineWeight ("Weight", Float) = 1
        _OutlineDepthMultiplier ("Depth Weight", Float) = 1
        _OutlineDepthBias ("Depth Bias", Float) = 1
        _OutlineNormalMultiplier ("Normal Weight", Float) = 1
        _OutlineNormalBias ("Normal Bias", Float) = 10
        _OutlineColor ("Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _SobelDepthTex ("Depth", 2D) = "white" {}
        [HideInInspector] _SobelNormalTex ("Normal", 2D) = "white" {}
        [HideInInspector] _SobelIncludeTex ("Include", 2D) = "black" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
        }

        Pass
        {
            Name "SobelOutline"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X_FLOAT(_SobelDepthTex);
            TEXTURE2D_X_FLOAT(_SobelNormalTex);
            TEXTURE2D(_SobelIncludeTex);

            float _OutlineThickness;
            float _OutlineWeight;
            float _OutlineDepthMultiplier;
            float _OutlineDepthBias;
            float _OutlineNormalMultiplier;
            float _OutlineNormalBias;
            float4 _OutlineColor;

            float SampleRawDepth(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_SobelDepthTex, sampler_PointClamp, uv).r;
            }

            float SampleEyeDepth(float2 uv)
            {
                return LinearEyeDepth(SampleRawDepth(uv), _ZBufferParams);
            }

            float3 SampleNormal(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_SobelNormalTex, sampler_PointClamp, uv).xyz;
            }

            float SampleLuma(float2 uv)
            {
                float3 rgb = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                return dot(rgb, float3(0.2126, 0.7152, 0.0722));
            }

            float4 SampleInclude(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_SobelIncludeTex, sampler_PointClamp, uv);
            }

            float SobelSampleLuma(float2 uv, float3 offset)
            {
                float pixelCenter = SampleLuma(uv);
                float pixelLeft = SampleLuma(uv - offset.xz);
                float pixelRight = SampleLuma(uv + offset.xz);
                float pixelUp = SampleLuma(uv + offset.zy);
                float pixelDown = SampleLuma(uv - offset.zy);
                return abs(pixelLeft - pixelCenter) + abs(pixelRight - pixelCenter) +
                       abs(pixelUp - pixelCenter) + abs(pixelDown - pixelCenter);
            }

            float SobelSampleDepth(float2 uv, float3 offset)
            {
                float pixelCenter = SampleEyeDepth(uv);
                float pixelLeft = SampleEyeDepth(uv - offset.xz);
                float pixelRight = SampleEyeDepth(uv + offset.xz);
                float pixelUp = SampleEyeDepth(uv + offset.zy);
                float pixelDown = SampleEyeDepth(uv - offset.zy);
                return abs(pixelLeft - pixelCenter) + abs(pixelRight - pixelCenter) +
                       abs(pixelUp - pixelCenter) + abs(pixelDown - pixelCenter);
            }

            float3 SobelSampleNormal(float2 uv, float3 offset)
            {
                float3 pixelCenter = SampleNormal(uv);
                float3 pixelLeft = SampleNormal(uv - offset.xz);
                float3 pixelRight = SampleNormal(uv + offset.xz);
                float3 pixelUp = SampleNormal(uv + offset.zy);
                float3 pixelDown = SampleNormal(uv - offset.zy);
                return abs(pixelLeft - pixelCenter) + abs(pixelRight - pixelCenter) +
                       abs(pixelUp - pixelCenter) + abs(pixelDown - pixelCenter);
            }

            float SobelSampleIncludeCoverage(float2 uv, float3 offset)
            {
                float pixelCenter = SampleInclude(uv).a;
                float pixelLeft = SampleInclude(uv - offset.xz).a;
                float pixelRight = SampleInclude(uv + offset.xz).a;
                float pixelUp = SampleInclude(uv + offset.zy).a;
                float pixelDown = SampleInclude(uv - offset.zy).a;
                return abs(pixelLeft - pixelCenter) + abs(pixelRight - pixelCenter) +
                       abs(pixelUp - pixelCenter) + abs(pixelDown - pixelCenter);
            }

            float3 IncludeNormal(float4 includeSample)
            {
                return includeSample.a > 0.01 ? includeSample.rgb * 2.0 - 1.0 : 0.0;
            }

            float3 SobelSampleIncludeNormal(float2 uv, float3 offset)
            {
                float3 pixelCenter = IncludeNormal(SampleInclude(uv));
                float3 pixelLeft = IncludeNormal(SampleInclude(uv - offset.xz));
                float3 pixelRight = IncludeNormal(SampleInclude(uv + offset.xz));
                float3 pixelUp = IncludeNormal(SampleInclude(uv + offset.zy));
                float3 pixelDown = IncludeNormal(SampleInclude(uv - offset.zy));
                return abs(pixelLeft - pixelCenter) + abs(pixelRight - pixelCenter) +
                       abs(pixelUp - pixelCenter) + abs(pixelDown - pixelCenter);
            }

            float DilatedInclude(float2 uv, float3 offset)
            {
                float includeMask = SampleInclude(uv).a;
                includeMask = max(includeMask, SampleInclude(uv - offset.xz).a);
                includeMask = max(includeMask, SampleInclude(uv + offset.xz).a);
                includeMask = max(includeMask, SampleInclude(uv + offset.zy).a);
                includeMask = max(includeMask, SampleInclude(uv - offset.zy).a);
                return saturate(includeMask);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                float4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float2 texelSize = 1.0 / _ScreenParams.xy;
                float3 offset = float3(texelSize * max(_OutlineThickness, 0.0001), 0.0);

                float sobelIncludeCoverage = saturate(SobelSampleIncludeCoverage(uv, offset) * 2.0);
                float3 includeNormalVec = SobelSampleIncludeNormal(uv, offset);
                float sobelIncludeNormal = saturate((includeNormalVec.x + includeNormalVec.y + includeNormalVec.z) * 1.5);
                float outline = saturate(max(sobelIncludeCoverage, sobelIncludeNormal));
                outline *= DilatedInclude(uv, offset);
                outline = saturate(outline * (0.5 + _OutlineWeight * 2.0));

                float3 outlineColor = lerp(sceneColor.rgb, _OutlineColor.rgb, saturate(_OutlineColor.a));
                float3 color = lerp(sceneColor.rgb, outlineColor, outline);
                return float4(color, sceneColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
