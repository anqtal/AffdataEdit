// Ground-grid spacing and distance fade follow Arcade Alpha's Arcade/Grid shader.
Shader "AffdataEdit/FreeCameraGrid"
{
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend DstColor One
        ZWrite On
        ZTest LEqual
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Level;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float fade = 1 - saturate(distance(input.positionWS, _WorldSpaceCameraPos) / (30 * _Level));
                // AA's default opacity is 1 with additive blending and premultiplied RGB.
                return half4((2 * fade).xxx, 2 * fade);
            }
            ENDHLSL
        }
    }
}
