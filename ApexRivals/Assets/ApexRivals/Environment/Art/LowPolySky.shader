Shader "ApexRivals/Environment/Low Poly Sky"
{
    Properties
    {
        _HorizonColor("Horizon / fog color", Color) = (0.65,0.83,0.94,1)
        _ZenithColor("Upper sky", Color) = (0.12,0.45,0.85,1)
        _CloudColor("Cloud light faces", Color) = (0.98,0.98,0.94,1)
        _CloudShadow("Cloud shadow faces", Color) = (0.65,0.76,0.84,1)
        _Coverage("Cloud coverage", Range(0,1)) = 0.48
        _CloudScale("Cloud pattern scale", Range(1,8)) = 3
        _Seed("Cloud layout", Float) = 1
        _SunDirection("Sun direction", Vector) = (0.3,0.65,0.4,0)
        _SunColor("Sun color", Color) = (1,0.96,0.76,1)
        _SunSize("Sun radius", Range(0,0.15)) = 0.045
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _HorizonColor, _ZenithColor, _CloudColor, _CloudShadow, _SunColor;
                float4 _SunDirection;
                float _Coverage, _CloudScale, _Seed, _SunSize;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }
            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1,311.7)) + _Seed * 17.13) * 43758.5453);
            }
            float PolygonLength(float2 p)
            {
                p = abs(p);
                return max(max(p.x,p.y), (p.x + p.y) * 0.7071068);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float height = saturate(direction.y);
                half3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, sqrt(height));

                // A small hexagonal sun, obscured by cloud cover in the storm variant.
                float3 sun = normalize(_SunDirection.xyz);
                float3 tangent = normalize(cross(sun, float3(0,0,1)));
                float3 bitangent = cross(sun, tangent);
                float2 disc = float2(dot(direction,tangent), dot(direction,bitangent));
                float hex = max(abs(disc.x) * 0.8660254 + abs(disc.y) * 0.5, abs(disc.y));
                float sunMask = step(hex, _SunSize) * step(0.9, dot(direction,sun)) * step(0.001, _SunSize);
                sky = lerp(sky, _SunColor.rgb, sunMask);

                // Angular cloud lobes with flat triangular colour facets. Projection onto
                // an overhead layer avoids a latitude/longitude texture seam.
                float2 plane = direction.xz / max(direction.y + 0.38, 0.08) * _CloudScale;
                float2 grid = plane * 5;
                float2 cell = floor(grid);
                float upper = step(1, frac(grid).x + frac(grid).y);
                float2 tile = floor(plane / 3);
                float cloud = 0;
                float face = Hash(cell + upper * 0.37);
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float2 id = tile + float2(x,y);
                    float random = Hash(id);
                    float2 center = (id + 0.5) * 3 + float2(random - 0.5, Hash(id + 8.3) - 0.5) * 0.8;
                    float2 offset = plane - center;
                    float radius = lerp(0.35,1.85,_Coverage) * lerp(0.8,1.15,random);
                    float left = PolygonLength((offset + float2(radius * 0.5,0)) / float2(1,0.65));
                    float middle = PolygonLength((offset - float2(0,radius * 0.18)) / float2(0.8,0.85));
                    float right = PolygonLength((offset - float2(radius * 0.55,0)) / float2(0.8,0.6));
                    cloud = max(cloud, step(min(left,min(middle,right)),radius));
                }
                half3 cloudColor = lerp(_CloudShadow.rgb,_CloudColor.rgb,0.55 + face * 0.45);
                // Clear horizon meets scene fog exactly, including in the storm sky.
                cloud *= smoothstep(0.035,0.22,height);
                return half4(lerp(sky,cloudColor,cloud),1);
            }
            ENDHLSL
        }
    }
}
