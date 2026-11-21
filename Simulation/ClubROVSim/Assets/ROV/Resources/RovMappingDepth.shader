Shader "Hidden/ClubROV/MappingDepth"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Depth
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float Depth(Varyings input) : SV_Target
            {
                float raw = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, input.texcoord).r;
                float metres = LinearEyeDepth(raw, _ZBufferParams);
                return metres <= 12.0 ? metres : 0.0;
            }
            ENDHLSL
        }
    }
}
