// Spc/JudgementGrid
// Re-authoring of "Unlit/TestMeshJudgementLineEffectEnlarge", the material on JUDGE_SKY/TestGrid.
// Transcribed from a disassembly of the shipped DXBC, which is short enough to quote in full:
//
//   dcl_resource_structured t0, 4          // _TriId_To_Opacity, StructuredBuffer<float>
//   dcl_input               v0.xyz         // POSITION
//   dcl_input_sgv           v2.x, vertex_id
//   mul  r0.xyz, v0.xyzx, l(1.7, 1.7, 1.0, 0.0)
//   mov  r0.w,   l(1.0)
//   add  o0,     r0, l(-0.85, -0.85, -0.0, -0.0)
//   udiv r0.x, null, v2.x, l(3)            // triangle index = vertexID / 3
//   ld_structured o1.w, r0.x, l(0), t0.xxxx
//   mov  o1.xyz, l(1.0, 0.0, 0.0)
//   ...and the fragment shader is "mov o0, v1; ret".
//
// Two consequences worth stating, because they constrain the mesh and the consumer:
//
//  * The vertex shader multiplies no matrices at all - it writes clip space directly, so the
//    TestGrid transform in the original scene has no effect on the image and neither does ours.
//    The grid occupies the middle 85% of the render target, leaving a 7.5% margin for the blur.
//  * The triangle index is SV_VertexID / 3, so the mesh must NOT share vertices between triangles
//    and its index buffer must be the identity sequence. SpcJudgementGrid.BuildMesh keeps that.
//
// RGB is a constant (1, 0, 0) and the per-triangle opacity rides in alpha. The red is not
// decorative: the sky glow shader downstream samples only the red channel of the blurred result,
// which under SrcAlpha/OneMinusSrcAlpha over a cleared target gives red = opacity.
//
// Cull Off is required. The mesh alternates triangle winding row by row, so any single-sided cull
// would drop half the grid. (The original's pass serialises no culling state; this is the reading
// that makes its own mesh render completely.)
Shader "Spc/JudgementGrid"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float> _TriId_To_Opacity;

            struct Attributes
            {
                float3 positionOS : POSITION;
                uint   vertexID   : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = float4(v.positionOS.x * 1.7 - 0.85,
                                      v.positionOS.y * 1.7 - 0.85,
                                      v.positionOS.z,
                                      1.0);
                o.color = float4(1.0, 0.0, 0.0, _TriId_To_Opacity[v.vertexID / 3]);
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                return i.color;
            }
            ENDHLSL
        }
    }
}
