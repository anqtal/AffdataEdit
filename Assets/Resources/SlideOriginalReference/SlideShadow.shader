Shader "AffdataEdit/SlideShadow" {
 Properties { _Color ("Arc shadow tint", Color) = (0,0,0,0.19607843) }
 SubShader {
 Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+6" }
 Blend One OneMinusSrcAlpha
 ZWrite Off Cull Off
 Pass {
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "SlideVisibility.hlsl"
 float4 _Color;
 struct A { float3 p:POSITION; }; struct V { float4 p:SV_POSITION; float z:TEXCOORD0; };
 V vert(A i) { V o; o.p=TransformObjectToHClip(i.p); o.z=TransformObjectToWorld(i.p).z; return o; }
 half4 frag(V i):SV_Target { half4 c = _Color; c.a *= SlideVisibility(i.z); c.rgb *= c.a; return c; }
 ENDHLSL
 }
 }
}
