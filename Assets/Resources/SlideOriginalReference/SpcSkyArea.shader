// Original-reference SkyArea Variant, source ed82e4186748 forward PS blob 3.
// See OriginalReference/SkyArea for its mesh contract and original-GPU fixtures.
Shader "Spc/SkyArea"
{
    Properties
    {
        _Color ("Source body color", Color) = (0.68834537,0.62793696,0.97169811,1)
        [HDR] _EdgeCenterColor ("Source edge center", Color) = (1.35792816,1.15355456,4.23709488,1)
        [HDR] _EdgeGlowColor ("Source edge outside", Color) = (0.36470589,0.19607843,0.85098040,0)
        _ColorRed ("Source danger body", Color) = (0.83962262,0.50298148,0.62967426,1)
        [HDR] _EdgeCenterColorRed ("Source danger edge center", Color) = (3.65158081,0.80954856,0.94217658,1)
        [HDR] _EdgeGlowColorRed ("Source danger edge outside", Color) = (0.36470589,0.19607843,0.85098040,0)
        // Source CPU writes these colors, but the compiled forward program
        // does not read them. Keep their serialized API without inventing caps.
        [HideInInspector] _FrontBackColor ("Unused source front/back", Color) = (0.49622411,0.44152722,0.77358490,0.12941177)
        [HideInInspector] _FrontBackColorRed ("Unused source danger front/back", Color) = (0.90980393,0.70196080,1,0.09803922)
        _Danger ("Danger before source QuadIn", Range(0,1)) = 0
        [NoScaleOffset] _ArchTexture ("Original ARCHITECT-LINES alpha", 2D) = "black" {}
        [NoScaleOffset] _DotTexture ("Original DOT-GRID alpha", 2D) = "black" {}
        _VisualStart ("Absolute source visual start", Float) = 0
        _VisualChartProgress ("Absolute source visual chart progress", Float) = 0
        _MissingRange ("First/last horizontal overlap bounds", Vector) = (0,1,0,1)
        _IsFirstOfGroup ("Source first segment", Float) = 0
        _IsLastOfGroup ("Source last segment", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off ZTest LEqual Cull Off
        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_ArchTexture);
            TEXTURE2D(_DotTexture);
            // Original inline sampler 2 = trilinear repeat, already declared
            // by URP Core's GlobalSamplers.hlsl. Both original textures have
            // one mip, so LOD 0 equals source sample_b at any bias.
            #define SPC_SKY_SAMPLE(textureName, uv) SAMPLE_TEXTURE2D_LOD(textureName,sampler_TrilinearRepeat,uv,0)
            #include "Reference/SpcSourceSkyArea.hlsl"
            #include "SlideVisibility.hlsl"
            #include "Reference/SpcSourceOklab.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color, _EdgeCenterColor, _EdgeGlowColor;
                float4 _ColorRed, _EdgeCenterColorRed, _EdgeGlowColorRed;
                float4 _MissingRange;
                float _Danger, _VisualStart, _VisualChartProgress;
                float _IsFirstOfGroup, _IsLastOfGroup;
            CBUFFER_END
            struct Attributes
            {
                float3 positionOS:POSITION;
                float2 uv0:TEXCOORD0; // Absolute normalized x, normalized progress.
                float2 uv1:TEXCOORD1; // Local x, visual distance along this segment.
                float2 uv2:TEXCOORD2; // Normalized row width, total visual length.
            };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float2 uv0:TEXCOORD0;
                float2 uv1:TEXCOORD1;
                float2 uv2:TEXCOORD2;
                float worldZ:TEXCOORD3;
                float clipZ:TEXCOORD7;
                nointerpolation float4 mainColor:TEXCOORD4;
                nointerpolation float4 edgeCenter:TEXCOORD5;
                nointerpolation float4 edgeGlow:TEXCOORD6;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world=TransformObjectToWorld(input.positionOS);
                output.positionCS=TransformWorldToHClip(world);
                output.worldZ=-1.5 - world.z * (1.62 / 8.5);
                output.clipZ=world.z;
                output.uv0=input.uv0; output.uv1=input.uv1; output.uv2=input.uv2;
                // Source SkySafeArea first squares danger, then $cf.$LBA blends raw Color
                // components in Oklab. Do the uniform work per vertex, not per pixel.
                // Gamma is the verified original-reference pipeline; no extra color conversion.
                float danger=saturate(_Danger); danger*=danger;
                output.mainColor=SpcSourceOklabLerp(_Color,_ColorRed,danger);
                output.edgeCenter=SpcSourceOklabLerp(_EdgeCenterColor,_EdgeCenterColorRed,danger);
                output.edgeGlow=SpcSourceOklabLerp(_EdgeGlowColor,_EdgeGlowColorRed,danger);
                return output;
            }
            float4 Frag(Varyings input):SV_Target
            {
                float visibility=SlideVisibility(input.clipZ);
                float4 color=SpcSourceSkyArea(input.uv0,input.uv1,input.uv2,input.worldZ,
                    _VisualStart,_VisualChartProgress,_IsFirstOfGroup,_IsLastOfGroup,_MissingRange,
                    input.mainColor,input.edgeCenter,input.edgeGlow);
                color.a*=visibility;
                return color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
