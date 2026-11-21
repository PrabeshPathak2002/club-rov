Shader "ClubROV/Pool Ceramic"
{
    Properties
    {
        _BaseColor("Tile color", Color) = (0.78,0.88,0.89,1)
        _Floor("Floor markings", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _Floor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float fog:TEXCOORD2; };
            Varyings Vert(Attributes a)
            {
                Varyings o; o.world=TransformObjectToWorld(a.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.world); o.normal=TransformObjectToWorldNormal(a.normalOS);
                o.fog=ComputeFogFactor(o.positionCS.z); return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float3 n=normalize(i.normal);
                float2 uv=abs(n.y)>0.7 ? i.world.xz : abs(n.x)>0.7 ? i.world.zy : i.world.xy;
                float2 cell=uv*5; // 20 cm ceramic tiles, consistent across walls and floor.
                float2 edge=min(frac(cell),1-frac(cell));
                float grout=1-smoothstep(0.015,0.035,min(edge.x,edge.y));
                float variation=frac(sin(dot(floor(cell),float2(12.9898,78.233)))*43758.5453);
                half3 color=_BaseColor.rgb*(0.95+0.08*variation);
                color=lerp(color,half3(0.34,0.48,0.50),grout*0.7);
                if (_Floor>0.5 && abs(n.y)>0.7)
                {
                    float lane=abs(frac((i.world.x+1.25)/2.5)-0.5)*2.5;
                    float stripe=1-smoothstep(0.10,0.115,lane);
                    // Standard dark lane stripe, with crossbar near either end.
                    float endFade=1-smoothstep(9.8,10.0,abs(i.world.z));
                    float crossbar=(1-smoothstep(0.10,0.12,abs(abs(i.world.z)-9.65)))*(1-smoothstep(0.5,0.52,lane));
                    color=lerp(color,half3(0.025,0.12,0.22),max(stripe*endFade,crossbar)*0.88);
                }
                else if (abs(n.y)<0.7)
                {
                    float band=step(-0.32,i.world.y)*step(i.world.y,-0.08);
                    color=lerp(color,half3(0.025,0.26,0.48),band*0.85);
                }
                float t=_Time.y*0.32;
                float2 w=i.world.xz+float2(sin(i.world.z*1.7+t),cos(i.world.x*1.4-t))*0.24;
                float c=sin(w.x*8+t+sin(w.y*4-t))*sin(w.y*7-t+cos(w.x*3+t));
                float caustic=pow(saturate(1-abs(c)*5),5)*0.18;
                caustic*=saturate(-i.world.y*2)*saturate((i.world.y+5)/5);
                Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
                half diffuse=saturate(dot(n,light.direction));
                half3 lit=color*(SampleSH(n)*0.8+light.color*(0.24+0.76*diffuse)*light.shadowAttenuation);
                lit+=caustic*half3(0.55,0.85,0.9);
                return half4(MixFog(lit,i.fog),1);
            }
            ENDHLSL
        }
    }
}
