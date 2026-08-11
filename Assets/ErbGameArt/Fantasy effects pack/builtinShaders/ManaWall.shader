// Hand-converted to URP from the original Amplify/Built-in shader.
// Functionally identical: same properties, same alpha blend (SrcAlpha OneMinusSrcAlpha),
// same per-pixel math, same soft-depth fade (_Usedepth) and fog. Cull Off (two-sided).
Shader "ERB/Particles/ManaWall"
{
	Properties
	{
		_Tex1("Tex1", 2D) = "white" {}
		_Tex2("Tex2", 2D) = "white" {}
		_Mask("Mask", 2D) = "white" {}
		_SpeedTex1("Speed Tex1", Vector) = (0,0,0,0)
		_SpeedTex2XYEmission("Speed Tex2 XY / Emission", Vector) = (0,0,0,0)
		_Color2("Color 2", Color) = (1,0,0,1)
		_Color1("Color 1", Color) = (1,0.5423229,0,1)
		_Opacity("Opacity", Range( 0 , 3)) = 1
		[MaterialToggle] _Usedepth ("Use depth?", Float ) = 0
		_Depthpower ("Depth power", Float ) = 1
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
	}

	SubShader
	{
		Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
		Blend SrcAlpha OneMinusSrcAlpha
		ColorMask RGB
		Cull Off
		ZWrite Off
		ZTest LEqual

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 2.0
			#pragma multi_compile_fog
			#pragma multi_compile_instancing

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

			TEXTURE2D(_Tex1); SAMPLER(sampler_Tex1);
			TEXTURE2D(_Tex2); SAMPLER(sampler_Tex2);
			TEXTURE2D(_Mask); SAMPLER(sampler_Mask);

			CBUFFER_START(UnityPerMaterial)
				float4 _Tex1_ST;
				float4 _Tex2_ST;
				float4 _Mask_ST;
				float4 _SpeedTex1;
				float4 _SpeedTex2XYEmission;
				float4 _Color1;
				float4 _Color2;
				float  _Opacity;
				float  _Usedepth;
				float  _Depthpower;
			CBUFFER_END

			struct Attributes
			{
				float4 positionOS : POSITION;
				half4  color      : COLOR;
				float4 texcoord   : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings
			{
				float4 positionHCS : SV_POSITION;
				half4  color       : COLOR;
				float4 texcoord    : TEXCOORD0;
				float  fogCoord    : TEXCOORD1;
				float4 projPos     : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			Varyings vert (Attributes v)
			{
				Varyings o = (Varyings)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				VertexPositionInputs vp = GetVertexPositionInputs(v.positionOS.xyz);
				o.positionHCS = vp.positionCS;
				o.projPos = ComputeScreenPos(vp.positionCS);
				o.projPos.z = -vp.positionVS.z;
				o.color = v.color;
				o.texcoord = v.texcoord;
				o.fogCoord = ComputeFogFactor(vp.positionCS.z);
				return o;
			}

			half4 frag (Varyings i) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

				float sceneZ = LinearEyeDepth(SampleSceneDepth(i.projPos.xy / i.projPos.w), _ZBufferParams);
				float partZ = i.projPos.z;
				float fade = saturate((sceneZ - partZ) / _Depthpower);
				float lp = lerp(1, fade, _Usedepth);
				i.color.a *= lp;

				float Emission39 = _SpeedTex2XYEmission.z;
				float2 appendResult16 = float2(_SpeedTex1.x , _SpeedTex1.y);
				float2 uv0_Tex1 = i.texcoord.xy * _Tex1_ST.xy + _Tex1_ST.zw;
				float2 panner7 = ( _Time.y * appendResult16 + uv0_Tex1 );
				float4 uv0_Tex2 = i.texcoord;
				uv0_Tex2.xy = i.texcoord.xy * _Tex2_ST.xy + _Tex2_ST.zw;
				float2 appendResult14 = float2(_SpeedTex1.z , _SpeedTex1.w);
				float2 panner8 = ( _Time.y * appendResult14 + uv0_Tex1 );
				float2 appendResult52 = float2(uv0_Tex2.z , uv0_Tex2.w);
				float4 tex2DNode4 = SAMPLE_TEXTURE2D( _Tex1, sampler_Tex1, ( panner8 + appendResult52 ) );
				float2 appendResult21 = float2(_SpeedTex2XYEmission.x , _SpeedTex2XYEmission.y);
				float2 panner20 = ( _Time.y * appendResult21 + uv0_Tex2.xy );
				float4 tex2DNode5 = SAMPLE_TEXTURE2D( _Tex2, sampler_Tex2, ( panner20 + appendResult52 ) );
				float2 uv_Mask = i.texcoord.xy * _Mask_ST.xy + _Mask_ST.zw;
				float4 tex2DNode6 = SAMPLE_TEXTURE2D( _Mask, sampler_Mask, uv_Mask );
				float temp_output_94_0 = saturate( ( ( ( ( ( SAMPLE_TEXTURE2D( _Tex1, sampler_Tex1, ( panner7 + uv0_Tex2.z ) ).r + tex2DNode4.r ) * tex2DNode4.r * tex2DNode5.g ) + ( tex2DNode6.b * 0.5 ) ) * tex2DNode5.g ) * tex2DNode6.b ) );
				float4 lerpResult33 = lerp( _Color1 , _Color2 , temp_output_94_0 );
				float4 appendResult91 = float4( ( Emission39 * lerpResult33 * i.color ).rgb , saturate( ( _Color1.a * _Color2.a * temp_output_94_0 * i.color.a * _Opacity ) ) );

				half4 col = appendResult91;
				col.rgb = MixFog(col.rgb, i.fogCoord);
				return col;
			}
			ENDHLSL
		}
	}
	Fallback Off
}
