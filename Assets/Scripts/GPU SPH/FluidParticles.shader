Shader "Custom/FluidParticles"
{
    Properties
    {
        _Scale ("Scale", Float) = 0.5
        _DensityMin ("Density Min", Float) = 0.9
        _DensityMax ("Density Max", Float) = 1.6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct particle
            {
                float density;
                float pressure;
                float3 pressureForce;
                float3 viscosityForce;
                float3 acceleration;
                float3 velocity;
                float3 position;
            };

            
            StructuredBuffer<particle> Particles;

            float _Scale;
            float _DensityMin;
            float _DensityMax;

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                uint instanceID : TEXCOORD0;
            };

            Varyings vert(Attributes IN, uint instanceID : SV_InstanceID)
            {
                Varyings OUT;
                OUT.instanceID = instanceID;

                float3 worldPos = Particles[instanceID].position;
                float3 scaled = IN.positionOS * _Scale + worldPos;
                OUT.positionHCS = TransformWorldToHClip(scaled);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float d = Particles[IN.instanceID].density;
                float t = saturate((d - _DensityMin) / (_DensityMax - _DensityMin));
                // Blue → Red gradient
                float3 color = lerp(float3(0.0, 0.0, 1.0), float3(1.0, 0.0, 0.0),t);
                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
