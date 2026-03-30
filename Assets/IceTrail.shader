Shader "Unlit/IceTrail"
{
	Properties
	{
		_MainTex("Textura base del tablero", 2D) = "white" {}
		_TrailTex("Rastro de agua", 2D) = "black" {}
		_TrailColor("Color del rastro", Color) = (0.6, 0.88, 1.0, 1.0)
	}
		SubShader
		{
			Tags { "RenderType" = "Opaque" }
			LOD 100

			Pass
			{
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#include "UnityCG.cginc"

				struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
				struct v2f { float4 pos : SV_POSITION;  float2 uv : TEXCOORD0; };

				sampler2D _MainTex;
				sampler2D _TrailTex;
				float4 _TrailColor;

				v2f vert(appdata v)
				{
					v2f o;
					o.pos = UnityObjectToClipPos(v.vertex);
					o.uv = v.uv;
					return o;
				}

				fixed4 frag(v2f i) : SV_Target
				{
					fixed4 base = tex2D(_MainTex,  i.uv);
					fixed4 trail = tex2D(_TrailTex, i.uv);

					// Mezclar: donde hay rastro, oscurecer/colorear la textura base
					fixed3 wet = base.rgb * 0.75 + _TrailColor.rgb * 0.25;
					return fixed4(lerp(base.rgb, wet, trail.a), 1.0);
				}
				ENDCG
			}
		}
}
