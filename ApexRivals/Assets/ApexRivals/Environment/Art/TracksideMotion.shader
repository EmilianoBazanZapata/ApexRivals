Shader "ApexRivals/Environment/Trackside Motion"
{
    Properties
    {
        _MotionAmount("Motion amplitude", Range(0,1)) = 0.12
        _MotionAxis("Motion direction", Vector) = (1,0.3,0,0)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [HideInInspector] _EnvironmentTime("Scene clock", Float) = 0
        [HideInInspector] _Cheer("Crowd excitement", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "TracksideMotion"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _MotionAxis;
                float _MotionAmount, _EnvironmentTime, _Cheer, _Cull;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; float2 motion : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; half4 color : COLOR; half fog : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float phase = input.motion.y + UNITY_MATRIX_M._m03 * 0.13 + UNITY_MATRIX_M._m23 * 0.07;
                float wave = sin(_EnvironmentTime * (2.5 + _Cheer) + phase);
                input.positionOS.xyz += _MotionAxis.xyz * wave * input.motion.x * _MotionAmount * (1 + _Cheer);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                Light light = GetMainLight();
                half shade = 0.5h + 0.5h * saturate(dot(normalize(input.normalWS), light.direction));
                return half4(MixFog(input.color.rgb * shade, input.fog), 1);
            }
            ENDHLSL
        }
    }
}
