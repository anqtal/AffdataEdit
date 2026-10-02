// Spc/Blur
// Re-authoring of the original's "CustomEffects/Blur", used by both the judgement-line triangle
// grid and the background diamond layer. Taken from a disassembly of the shipped DXBC rather than
// guessed: a 5-tap linear-sampled gaussian, separable, with the classic weights
//
//   W0 = 0.2270270270 (centre)   W1 = 0.3162162162 (+/- near)   W2 = 0.0702702703 (+/- far)
//
// summing to exactly 1. The near/far tap offsets in the disassembly appear as 1.3846153846 and
// 3.2307692308, folded against the far one so the ratios are 3/7 and 1.
//
// Two deliberate deviations from the original, both documented because they are visible in code:
//
//  1. The original derives its tap offset from _ScreenParams, so its blur radius scales with the
//     window size (4K blurs twice as far as 1080p). We take an explicit radius in texels instead,
//     so an offscreen capture is reproducible. This costs nothing in fidelity: at the original's
//     own settings the outermost tap lands 0.2 texels away, i.e. inside the centre texel, and the
//     softening you actually see comes from the 1/2 -> 1/4 -> 1/2 bilinear resample pyramid, not
//     from these taps.
//  2. Pass 2 of the original (a plain copy) is not reproduced - the blur chains never call it.
//
// The original forces alpha to 1 in both blur passes. That is preserved: downstream shaders read
// only the red channel, but the behaviour should match if anything ever reads alpha.
Shader "Spc/Blur"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Source", 2D) = "black" {}
        _BlurTexels ("Blur radius, outermost tap, in source texels", Float) = 0.108
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }

        Blend Off
        ZWrite Off
        ZTest Always
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
        struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

        TEXTURE2D(_MainTex);
        // sampler_LinearClamp comes from URP's Core.hlsl - do not redeclare it.
        float4 _MainTex_TexelSize;   // (1/w, 1/h, w, h)
        float  _BlurTexels;

        static const float W0 = 0.2270270270;
        static const float W1 = 0.3162162162;
        static const float W2 = 0.0702702703;
        static const float NEAR = 0.4285714286;   // 1.3846153846 / 3.2307692308
        static const float FAR  = 1.0;

        Varyings Vert(Attributes v)
        {
            Varyings o;
            o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
            o.uv = v.uv;
            return o;
        }

        float4 Blur(float2 uv, float2 axis)
        {
            // axis carries the per-texel step for the axis being blurred; the other component is 0.
            float2 step = axis * _BlurTexels;
            float4 c  = SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv) * W0;
            c += SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv + step * NEAR) * W1;
            c += SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv - step * NEAR) * W1;
            c += SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv + step * FAR)  * W2;
            c += SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv - step * FAR)  * W2;
            c.a = 1.0;   // matches the original: both passes write alpha 1
            return c;
        }
        ENDHLSL

        // Pass 0 - vertical, matching CustomEffects/Blur pass 0.
        Pass
        {
            Name "Blur Vertical"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragV
            float4 FragV(Varyings i) : SV_Target
            {
                return Blur(i.uv, float2(0.0, _MainTex_TexelSize.y));
            }
            ENDHLSL
        }

        // Pass 1 - horizontal, matching CustomEffects/Blur pass 1.
        Pass
        {
            Name "Blur Horizontal"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragH
            float4 FragH(Varyings i) : SV_Target
            {
                return Blur(i.uv, float2(_MainTex_TexelSize.x, 0.0));
            }
            ENDHLSL
        }
    }
}
