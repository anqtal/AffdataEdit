Shader "Arcade/NoteIndirect"
{
    Properties
    {
        _MainTex ("Skin", 2D) = "white" {}
        _ZWrite ("Depth write", Float) = 0
        _ZTest ("Depth test", Float) = 4
        _ArchTexture ("Slide lines", 2D) = "black" {}
        _DotTexture ("Slide dots", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Cull Off ZWrite [_ZWrite] ZTest [_ZTest]
        Blend One OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_ArchTexture);
            TEXTURE2D(_DotTexture);
            #define SPC_SKY_SAMPLE(textureName, uv) SAMPLE_TEXTURE2D_LOD(textureName,sampler_TrilinearRepeat,uv,0)
            #include "SlideOriginalReference/Reference/SpcSourceSkyArea.hlsl"
            #include "SlideOriginalReference/SlideVisibility.hlsl"
            struct NoteInstance
            {
                float4x4 transform;
                float4 highColor, lowColor;
                float4 uvTransform;
                float4 clipHeight; // from, to, from height, to height
                float4 options; // mode, alpha, unused, selected
                float4 slide; // visual start, chart progress, group head, group tail
                float4 overlap;
            };
            StructuredBuffer<NoteInstance> _Notes;
            uint _NoteOffset;
            float _NoteSelection;
            struct Attributes
            {
                float3 position:POSITION;
                float2 uv:TEXCOORD0;
                float2 uv1:TEXCOORD1;
                float2 uv2:TEXCOORD2;
                uint instanceID:SV_InstanceID;
            };
            struct Varyings
            {
                float4 position:SV_POSITION;
                float2 uv:TEXCOORD0;
                float2 uv1:TEXCOORD1;
                float2 uv2:TEXCOORD2;
                float worldZ:TEXCOORD3;
                nointerpolation uint instanceID:TEXCOORD4;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                // Every command starts at instance zero; the CPU supplies the run offset.
                uint id = _NoteOffset + input.instanceID;
                NoteInstance note = _Notes[id];
                float3 world = mul(note.transform, float4(input.position, 1)).xyz;
                output.position = TransformWorldToHClip(world);
                output.worldZ = world.z;
                output.uv = input.uv;
                output.uv1 = input.uv1;
                output.uv2 = input.uv2;
                output.instanceID = id;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                NoteInstance n = _Notes[input.instanceID];
                if (_NoteSelection > .5) clip(n.options.w - .5);
                int mode = (int)n.options.x;
                float2 uv = input.uv * n.uvTransform.xy + n.uvTransform.zw;
                if (mode == 1 || mode == 2 || mode == 4)
                    if (input.uv.y < n.clipHeight.x || input.uv.y > n.clipHeight.y) return 0;
                if (mode == 1)
                    uv.y = (input.uv.y - n.clipHeight.x) / max(n.clipHeight.y - n.clipHeight.x, .000001);
                float4 c;
                if (mode == 4 || mode == 8) c = n.highColor;
                else if (mode == 6)
                {
                    c = SpcSourceSkyArea(input.uv,input.uv1,input.uv2,-1.5-input.worldZ*(1.62/8.5),
                        n.slide.x,n.slide.y,n.slide.z,n.slide.w,n.overlap,n.highColor,
                        float4(1.35792816,1.15355456,4.23709488,1),float4(.36470589,.19607843,.85098040,0));
                }
                else
                {
                    c = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv);
                    if (mode == 1 || mode == 3) c.a = 1;
                    if (mode == 2)
                        c *= lerp(n.lowColor,n.highColor,saturate(abs(lerp(n.clipHeight.z,n.clipHeight.w,input.uv.y))));
                    else c *= n.highColor;
                    c.a *= n.options.y;
                }
                if (mode == 6 || mode == 7 || mode == 8) c.a *= SlideVisibility(input.worldZ);
                c.rgb *= c.a;
                return c;
            }
            ENDHLSL
        }
    }
}
