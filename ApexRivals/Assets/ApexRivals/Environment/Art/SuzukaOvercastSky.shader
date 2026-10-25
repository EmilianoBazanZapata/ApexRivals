Shader "ApexRivals/Environment/Overcast Sky"
{
    Properties
    {
        _HorizonColor("Horizon / fog color", Color) = (0.58,0.64,0.67,1)
        _ZenithColor("Cloud cover color", Color) = (0.36,0.42,0.47,1)
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
                half4 _HorizonColor;
                half4 _ZenithColor;
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
            half4 Frag(Varyings input) : SV_Target
            {
                // Meet the distance fog at the horizon without a sunny sky behind the rainy circuit.
                half height = saturate(normalize(input.direction).y);
                return half4(lerp(_HorizonColor.rgb, _ZenithColor.rgb, sqrt(height)), 1);
            }
            ENDHLSL
        }
    }
}
