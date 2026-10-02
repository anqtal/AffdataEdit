Shader "Arcade/UISprite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        [Space()]
        [Toggle(ENABLE_PROGRESS)] _ENABLE_PROGRESS ("Enable Progress", int) = 0
        [Toggle(REVERSE_PROGRESS)] _REVERSE_PROGRESS ("Reverse Progress", int) = 0
        _Progress ("Progress", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcFactor ("Src Factor", int) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstFactor ("Dst Factor", int) = 10
        [Toggle(IGNORE_TEXTURE_COLOR)] _IGNORE_TEXTURE_COLOR ("Ignore Texture Color", int) = 0
        [Toggle(IGNORE_PREMUL_ALPHA)] _IGNORE_PREMUL_ALPHA ("Ignore Premultiply Alpha", int) = 0
//
        [HideInInspector] _ClipRect ("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255

        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { 
            "Queue" = "Transparent" 
            "RenderType"="Transparent"
            "CanUseSpriteAtlas"="true" 
            "PreviewType"= "Plane"
        }
        
        Stencil {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        ZWrite Off
        Cull Off
        Blend [_SrcFactor] [_DstFactor]
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ ENABLE_PROGRESS
            #pragma multi_compile _ REVERSE_PROGRESS
            #pragma multi_compile _ IGNORE_TEXTURE_COLOR
            #pragma multi_compile _ IGNORE_PREMUL_ALPHA
            
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float4 _Color;
            float _Progress;
            
            float4 _ClipRect;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                #if ENABLE_PROGRESS
                    #if REVERSE_PROGRESS
                        if (i.uv.y < _Progress) return 0;
                    #else
                        if (i.uv.y > _Progress) return 0;
                    #endif
                #endif
                #if IGNORE_TEXTURE_COLOR
                    fixed4 c = i.color;
                    c.a *= tex2D(_MainTex, i.uv).a;
                #else
                    fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                #endif
                #ifdef UNITY_UI_CLIP_RECT
                    c.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                    clip(c.a - 0.001);
                #endif
                #if !IGNORE_PREMUL_ALPHA
                    c.rgb *= c.a;
                #endif
                return c;
            }
            ENDCG
        }
    }
}