Shader "Custom/Plane"
{   
    Properties
    {
        _MinY ("Min Y", Float) = -1
        _MaxY ("Max Y", Float) = 1

        _BrownColor ("Brown", Color) = (0.35, 0.22, 0.1, 1)
        _SandColor  ("Sand",  Color) = (0.76, 0.7, 0.5, 1)
        _GreenColor ("Green", Color) = (0.2, 0.6, 0.25, 1)

        _SandHeight ("Sand Height (0–1)", Range(0,1)) = 0.25
        _SandBlend  ("Sand Thickness", Range(0.01, 0.5)) = 0.3
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


            struct boundaryParticle
            {
                float3 position;
            };

            StructuredBuffer<boundaryParticle> boundaryParticles;


            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float y : TEXCOORD0;
            };



            float _MinY;
            float _MaxY;
            float4 _BrownColor;
            float4 _SandColor;
            float4 _GreenColor;
            float _SandHeight;
            float _SandBlend;

            Varyings vert (Attributes v, uint vertexID : SV_VertexID)
            {
                Varyings o;

                // Original object position
                float3 posOS = v.positionOS.xyz;

                // Get world position from boundary buffer
                float3 boundaryWS = boundaryParticles[vertexID].position;

                // Convert world → object space
                float3 boundaryOS = TransformWorldToObject(boundaryWS);

                // Replace only Y in object space
                posOS.y = boundaryOS.y;

                o.positionHCS = TransformObjectToHClip(posOS);
                o.y = boundaryWS.y; // use world height for coloring

                return o;
            }   

            half4 frag (Varyings i) : SV_Target
            {
                float t = saturate((i.y - _MinY) / (_MaxY - _MinY));

                // brown → sand → green transition at ONE location
                float sandToGreen = smoothstep(
                    _SandHeight,
                    _SandHeight + _SandBlend,
                    t
                );

                float brownToSand = smoothstep(
                    _SandHeight - _SandBlend,
                    _SandHeight,
                    t
                );

                float3 color = lerp(
                    lerp(_BrownColor.rgb, _SandColor.rgb, brownToSand),
                    _GreenColor.rgb,
                    sandToGreen
                );

                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}

