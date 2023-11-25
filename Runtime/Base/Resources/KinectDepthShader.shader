/*
	Copyright © Carl Emil Carlsen 2023
	http://cec.dk
*/

Shader "KinectAzureTextureProvider/KinectDepthShader" {
	Properties {
		//_MainTex ("Base (RGB)", 2D) = "white" {}
	}
	SubShader {
		Pass {
			ZTest Always Cull Off ZWrite Off
			Fog { Mode off }
		
			CGPROGRAM
			#pragma target 5.0

			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			StructuredBuffer<uint> _DepthMap;

			uint _TexResX;
			uint _TexResY;
			uint _MinDepth;
			uint _MaxDepth;

			struct v2f {
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			
			v2f vert( appdata_base v )
			{
				v2f o;
				
				o.pos = UnityObjectToClipPos( v.vertex );
				o.uv = v.texcoord;
				
				return o;
			}

			
			float frag (v2f i) : COLOR
			{
				uint dx = (uint)(i.uv.x * _TexResX);
				uint dy = (uint)(i.uv.y * _TexResY);
				uint di = (dx + dy * _TexResX);

				uint depth2 = _DepthMap[di >> 1];
				uint depth = di & 1 != 0 ? depth2 >> 16 : depth2 & 0xffff;
				uint cDepth = clamp(depth, _MinDepth, _MaxDepth);

				float fDepth = ((float)cDepth - (float)_MinDepth) / (float)(_MaxDepth - _MinDepth);

				return fDepth * (depth != 0);
			}

			ENDCG
		}
	}

	Fallback Off
}