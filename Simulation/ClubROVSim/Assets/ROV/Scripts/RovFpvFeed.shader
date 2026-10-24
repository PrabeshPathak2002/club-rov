Shader "ClubROV/FPV Camera Feed"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        float4 _FeedParams;
        float4 _DisplayParams;
        float Hash(float2 p) { return frac(sin(dot(p, float2(12.9898,78.233))) * 43758.5453); }
        half4 Sensor(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            float2 stepUV = rcp(_FeedParams.xy);
            half3 center = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
            half3 soft = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(stepUV.x,0)).rgb;
            soft += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(stepUV.x,0)).rgb;
            soft += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0,stepUV.y)).rgb;
            soft += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(0,stepUV.y)).rgb;
            half3 color = lerp(center, soft * 0.25, _DisplayParams.z);
            float luma = dot(color, float3(0.2126,0.7152,0.0722));
            float2 pixel = floor(uv * _FeedParams.xy);
            float noise = Hash(pixel + _FeedParams.z * 0.17) - 0.5;
            color += noise * _FeedParams.w * (1 + 0.8 * (1 - saturate(luma)));
            color += (Hash(floor(pixel / 8) + floor(_FeedParams.z / 3)) - 0.5) * 0.0004;
            color = floor(saturate(color) * 255 + 0.5) / 255;
            float2 vignette = uv * (1 - uv.yx);
            color *= lerp(0.84, 1, sqrt(sqrt(saturate(vignette.x * vignette.y * 16))));
            return half4(color, 1);
        }
        half4 Display(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            float aspectScale = _DisplayParams.y / _DisplayParams.x;
            if (aspectScale >= 1) uv.x = (uv.x - 0.5) * aspectScale + 0.5;
            else uv.y = (uv.y - 0.5) / aspectScale + 0.5;
            if (any(uv < 0) || any(uv > 1)) return half4(0,0,0,1);
            return half4(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb,1);
        }
        ENDHLSL
        Pass { Name "Sensor" HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Sensor
            ENDHLSL
        }
        Pass { Name "Display" HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Display
            ENDHLSL
        }
    }
}
