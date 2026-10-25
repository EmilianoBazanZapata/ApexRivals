Shader "ApexRivals/VFX/Tire Marks"
{
    Properties { _BaseColor("Rubber Color", Color) = (0.035,0.03,0.025,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
        Pass
        {
            Name "TireMarks"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            float _MarkTime;
            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 timing : TEXCOORD1;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                half fog : TEXCOORD1;
                float2 timing : TEXCOORD2;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _BaseColor;
                output.uv = input.uv;
                output.timing = input.timing;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float lifetime = max(0.01, input.timing.y);
                float age = max(0.0, _MarkTime - input.timing.x);
                half fade = 1.0 - smoothstep(lifetime * 0.7, lifetime, age);
                // Straight geometric tire grooves, no photographic textures or decal renderer dependency.
                half grooves = lerp(0.55h, 1.0h, step(0.14, frac(input.uv.x * 4.0)));
                half alpha = input.color.a * fade * grooves;
                clip(alpha - 0.002h);
                return half4(MixFog(input.color.rgb, input.fog), alpha);
            }
            ENDHLSL
        }
    }
}
