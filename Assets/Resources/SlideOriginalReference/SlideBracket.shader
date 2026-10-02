Shader "AffdataEdit/SlideBracket"
{
    Properties { _MainTex ("Bracket", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+11" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "SlideVisibility.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            struct A { float3 p:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; float z:TEXCOORD1; };
            V vert(A i) { V o; o.p=TransformObjectToHClip(i.p); o.uv=i.uv; o.z=TransformObjectToWorld(i.p).z; return o; }
            half4 frag(V i):SV_Target { half4 color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv); color.a*=SlideVisibility(i.z); return color; }
            ENDHLSL
        }
    }
}
