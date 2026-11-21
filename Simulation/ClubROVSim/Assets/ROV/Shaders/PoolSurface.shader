Shader "ClubROV/Pool Surface"
{
    Properties { _BaseColor("Water tint", Color)=(0.12,0.55,0.65,0.25) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float fog:TEXCOORD1; };
            Varyings Vert(Attributes a)
            {
                Varyings o; o.world=TransformObjectToWorld(a.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.world);o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float t=_Time.y;
                float2 slope=float2(cos(i.world.x*3+t)*0.09+cos(i.world.z*5-t)*0.05,
                    sin(i.world.z*3.5+t*0.8)*0.09+sin(i.world.x*4-t)*0.05);
                float3 n=normalize(float3(slope.x,1,slope.y));
                float3 v=normalize(GetWorldSpaceViewDir(i.world));
                float fresnel=pow(1-abs(dot(n,v)),4);
                Light light=GetMainLight();
                float sparkle=pow(saturate(abs(dot(n,normalize(light.direction+v)))),100)*0.55;
                half3 color=lerp(_BaseColor.rgb,half3(0.65,0.82,0.86),fresnel*0.65)+sparkle;
                return half4(MixFog(color,i.fog),saturate(_BaseColor.a+fresnel*0.45));
            }
            ENDHLSL
        }
    }
}
