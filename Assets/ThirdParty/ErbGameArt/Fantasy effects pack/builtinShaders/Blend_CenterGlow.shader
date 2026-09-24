// Hand-converted to URP from the original Amplify/Built-in shader.
// Functionally identical: same properties, same alpha blend (SrcAlpha OneMinusSrcAlpha),
// same per-pixel math, same soft-depth fade (_Usedepth) and fog.
Shader "ERB/Particles/Blend_CenterGlow"
{
	Properties
	{
		_MainTex("MainTex", 2D) = "white" {}
		_Noise("Noise", 2D) = "white" {}
		_Flow("Flow", 2D) = "white" {}
		_Mask("Mask", 2D) = "white" {}
		_SpeedMainTexUVNoiseZW("Speed MainTex U/V + Noise Z/W", Vector) = (0,0,0,0)
		_DistortionSpeedXYPowerZ("Distortion Speed XY Power Z", Vector) = (0,0,0,0)
		_Emission("Emission", Float) = 2
		_Color("Color", Color) = (0.5,0.5,0.5,1)
		_Opacity("Opacity", Range( 0 , 1)) = 1
		[Toggle]_Usecenterglow("Use center glow?", Float) = 0
		[MaterialToggle] _Usedepth ("Use depth?", Float ) = 0
		_Depthpower ("Depth power", Float ) = 1
		[Enum(Cull Off,0, Cull Front,1, Cull Back,2)] _CullMode("Culling", Float) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
	}

	SubShader
	{
		Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
		Blend SrcAlpha OneMinusSrcAlpha
		ColorMask RGB
		Cull [_CullMode]
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

			TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
			TEXTURE2D(_Noise);   SAMPLER(sampler_Noise);
			TEXTURE2D(_Flow);    SAMPLER(sampler_Flow);
			TEXTURE2D(_Mask);    SAMPLER(sampler_Mask);

			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
				float4 _Noise_ST;
				float4 _Flow_ST;
				float4 _Mask_ST;
				float4 _SpeedMainTexUVNoiseZW;
				float4 _DistortionSpeedXYPowerZ;
				float4 _Color;
				float  _Emission;
				float  _Opacity;
				float  _Usecenterglow;
				float  _Usedepth;
				float  _Depthpower;
				float  _CullMode;
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

				float2 appendResult21 = float2(_SpeedMainTexUVNoiseZW.x , _SpeedMainTexUVNoiseZW.y);
				float2 uv0_MainTex = i.texcoord.xy * _MainTex_ST.xy + _MainTex_ST.zw;
				float2 panner107 = ( _Time.y * appendResult21 + uv0_MainTex );
				float2 appendResult100 = float2(_DistortionSpeedXYPowerZ.x , _DistortionSpeedXYPowerZ.y);
				float3 uv0_Flow = i.texcoord.xyz;
				uv0_Flow.xy = i.texcoord.xy * _Flow_ST.xy + _Flow_ST.zw;
				float2 panner110 = ( _Time.y * appendResult100 + uv0_Flow.xy );
				float2 uv_Mask = i.texcoord.xy * _Mask_ST.xy + _Mask_ST.zw;
				float4 tex2DNode33 = SAMPLE_TEXTURE2D( _Mask, sampler_Mask, uv_Mask );
				float Flowpower102 = _DistortionSpeedXYPowerZ.z;
				float4 tex2DNode13 = SAMPLE_TEXTURE2D( _MainTex, sampler_MainTex, ( panner107 - ( ( SAMPLE_TEXTURE2D( _Flow, sampler_Flow, panner110 ) * tex2DNode33 ).rg * Flowpower102 ) ) );
				float2 appendResult22 = float2(_SpeedMainTexUVNoiseZW.z , _SpeedMainTexUVNoiseZW.w);
				float2 uv0_Noise = i.texcoord.xy * _Noise_ST.xy + _Noise_ST.zw;
				float2 panner108 = ( _Time.y * appendResult22 + uv0_Noise );
				float4 tex2DNode14 = SAMPLE_TEXTURE2D( _Noise, sampler_Noise, panner108 );
				float3 temp_output_78_0 = ( tex2DNode13 * tex2DNode14 * _Color * i.color ).rgb;
				float4 temp_cast_0 = ((1.0 + (uv0_Flow.z - 0.0) * (0.0 - 1.0) / (1.0 - 0.0))).xxxx;
				float4 clampResult38 = clamp( ( tex2DNode33 - temp_cast_0 ) , float4( 0,0,0,0 ) , float4( 1,1,1,1 ) );
				float4 clampResult40 = clamp( ( tex2DNode33 * clampResult38 ) , float4( 0,0,0,0 ) , float4( 1,1,1,1 ) );
				float4 appendResult87 = float4( ( lerp(temp_output_78_0,( temp_output_78_0 * clampResult40.rgb ),_Usecenterglow) * _Emission ) , ( tex2DNode13.a * tex2DNode14.a * _Color.a * i.color.a * _Opacity ) );

				half4 col = appendResult87;
				col.rgb = MixFog(col.rgb, i.fogCoord);   // alpha-blend → fade to fog color (matches original)
				return col;
			}
			ENDHLSL
		}
	}
	Fallback Off
}
