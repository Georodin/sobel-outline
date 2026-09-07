Shader "Hidden/Cubus/SobelCoverage"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
        }

        Pass
        {
            Name "Coverage"
            ZWrite On
            ZTest LEqual
            Cull Off
            Offset -1, -1
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                return float4(normal * 0.5 + 0.5, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "CopySceneDepth"
            ZWrite On
            ZTest Always
            Cull Off
            ColorMask 0
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X_FLOAT(_SobelSceneDepthTex);

            float Frag(Varyings input, out float outDepth : SV_Depth) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                outDepth = SAMPLE_TEXTURE2D_X(_SobelSceneDepthTex, sampler_PointClamp, input.texcoord).r;
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
