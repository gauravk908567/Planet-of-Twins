// Hand-converted to URP from the original Built-in/Amplify surface shader (ERB/WaterOrb).
// Surface Standard -> URP Forward Lit; GrabPass refraction -> SampleSceneColor (requires
// "Opaque Texture" ON in the URP asset). Same properties, same vertex waves, same normal-map
// distortion + emissive-refraction look. PBR lighting is URP's (very close to Built-in Standard,
// not guaranteed pixel-identical). albedo = 0 (the original set only Emission/Metallic/Smoothness).
Shader "ERB/WaterOrb"
{
	Properties
	{
		_NormalMap("NormalMap", 2D) = "bump" {}
		_NormalScale("NormalScale", Float) = 0.5
		_Color("Color", Color) = (0.07450981,0.09019608,0.1019608,1)
		_Metallic("Metallic", Range( 0 , 1)) = 1
		_Gloss("Gloss", Range( 0 , 1)) = 0.8
		_Opacity("Opacity", Range( 0 , 1)) = 0.3
		_Distortionpower("Distortion power", Float) = 0
		_Numberofwaves("Number of waves", Float) = 1
		_WavesspeedsizeXYTwistspeedsizeZW("Waves speed-size XY Twist speed-size ZW", Vector) = (-1,0.2,4,0.6)
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
	}

	SubShader
	{
		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }

		Pass
		{
			Name "ForwardLit"
			Tags { "LightMode"="UniversalForward" }
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Cull Back

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0

			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
			#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile_fragment _ _SHADOWS_SOFT
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile_fog
			#pragma multi_compile_instancing

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

			TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

			CBUFFER_START(UnityPerMaterial)
				float4 _NormalMap_ST;
				float4 _Color;
				float4 _WavesspeedsizeXYTwistspeedsizeZW;
				float  _NormalScale;
				float  _Metallic;
				float  _Gloss;
				float  _Opacity;
				float  _Distortionpower;
				float  _Numberofwaves;
			CBUFFER_END

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS   : NORMAL;
				float4 tangentOS  : TANGENT;
				float2 uv         : TEXCOORD0;
				half4  color      : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings
			{
				float4 positionHCS : SV_POSITION;
				float3 positionWS  : TEXCOORD0;
				float3 normalWS    : TEXCOORD1;
				float4 tangentWS   : TEXCOORD2;   // xyz + bitangent sign
				float2 uv          : TEXCOORD3;
				float4 screenPos   : TEXCOORD4;
				half4  color       : TEXCOORD5;
				float  fogCoord    : TEXCOORD6;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			// Same wave + twist displacement as the original vertexDataFunc.
			float3 WaterWaves(float3 posOS, float3 nOS, float2 uv)
			{
				// PI is already defined by URP's Common.hlsl — do not redeclare it.
				float V        = uv.y;
				float mulWave  = _Time.y * _WavesspeedsizeXYTwistspeedsizeZW.x;
				float mulTwist = _Time.y * _WavesspeedsizeXYTwistspeedsizeZW.z;
				float3 twist = float3(
					sin(3.0 * (posOS.y + mulTwist) * PI) * V,
					0.0,
					V * sin(3.0 * (mulTwist + posOS.y + (PI / 2.0)) * PI));
				float waveAmt = V * sin(_Numberofwaves * (posOS.y + mulWave) * PI);
				return posOS + nOS * waveAmt * _WavesspeedsizeXYTwistspeedsizeZW.y
				             + _WavesspeedsizeXYTwistspeedsizeZW.w * twist;
			}

			Varyings vert (Attributes IN)
			{
				Varyings o = (Varyings)0;
				UNITY_SETUP_INSTANCE_ID(IN);

				float3 posOS = WaterWaves(IN.positionOS.xyz, IN.normalOS, IN.uv);
				VertexPositionInputs vp = GetVertexPositionInputs(posOS);
				VertexNormalInputs   nv = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

				o.positionHCS = vp.positionCS;
				o.positionWS  = vp.positionWS;
				o.normalWS    = nv.normalWS;
				o.tangentWS   = float4(nv.tangentWS, IN.tangentOS.w * GetOddNegativeScale());
				o.uv          = TRANSFORM_TEX(IN.uv, _NormalMap);
				o.screenPos   = vp.positionNDC;
				o.color       = IN.color;
				o.fogCoord    = ComputeFogFactor(vp.positionCS.z);
				return o;
			}

			half4 frag (Varyings IN) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(IN);

				// Animated normal map (panner 0.3,0.1), same as the original.
				float2 nuv = IN.uv + _Time.y * float2(0.3, 0.1);
				half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, nuv), _NormalScale);

				float3 bitangent = IN.tangentWS.w * cross(IN.normalWS, IN.tangentWS.xyz);
				float3x3 tbn = float3x3(IN.tangentWS.xyz, bitangent, IN.normalWS);
				float3 normalWS = normalize(mul(normalTS, tbn));

				// Refraction: offset screen UV by a distortion-strength normal, sample opaque scene.
				half3 distNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, nuv), _Distortionpower);
				float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
				half3 sceneColor = SampleSceneColor(screenUV + distNormal.xy);

				SurfaceData sd = (SurfaceData)0;
				sd.albedo     = half3(0, 0, 0);                 // original set no Albedo
				sd.metallic   = _Metallic;
				sd.smoothness = _Gloss;
				sd.normalTS   = normalTS;
				sd.emission   = sceneColor + _Color.rgb;        // o.Emission = screenColor + _Color
				sd.occlusion  = 1.0;
				sd.alpha      = IN.color.a * _Opacity;

				InputData id = (InputData)0;
				id.positionWS      = IN.positionWS;
				id.normalWS        = normalWS;
				id.viewDirectionWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
				id.shadowCoord     = TransformWorldToShadowCoord(IN.positionWS);
				id.fogCoord        = IN.fogCoord;
				id.bakedGI         = SampleSH(normalWS);

				half4 col = UniversalFragmentPBR(id, sd);
				col.rgb = MixFog(col.rgb, IN.fogCoord);
				col.a = sd.alpha;
				return col;
			}
			ENDHLSL
		}
	}
	Fallback Off
}
