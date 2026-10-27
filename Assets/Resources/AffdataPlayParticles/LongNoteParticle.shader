Shader "Gameplay/LongNoteParticle"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		// _Alpha ("Alpha", Float) = 1.0
	}
	SubShader
	{
		Tags { "IgnoreProjector" = "True" "Queue" = "Overlay" }

		Blend SrcAlpha One
		ZWrite Off
		ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				fixed4 color : COLOR;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				fixed4 color : COLOR;
				float2 uv : TEXCOORD0;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			
			uniform float _Alpha;

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.color = v.color;
				// o.color.a += 0.05;
				// o.color.a = min(1.25, o.color.a);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 c = tex2D(_MainTex,i.uv) * i.color;
				c.rgb *= c.a;
				// c.a *= _Alpha;
				return c;
			}
			ENDCG
		}
	}
}
