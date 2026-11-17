// Вода 3D-города: настоящие отражения (текстура зеркальной камеры City3DScene), рябь из двух слоёв
// карты нормалей, френель, блик солнца, тени мостов и туман. Без отражений (_CityReflectionOn = 0)
// вода отражает цвет неба.
Shader "CityHighways/City3D Water"
{
    Properties
    {
        [MainTexture] _BaseMap ("Flow texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (0.55, 0.74, 0.88, 1)
        _DeepColor ("Deep color", Color) = (0.10, 0.22, 0.32, 1)
        [NoScaleOffset] _WaveMap ("Wave normals", 2D) = "bump" {}
        _WaveTiling ("Wave tiling", Float) = 2.2
        _WaveStrength ("Wave strength", Range(0, 1)) = 0.35
        _Distortion ("Reflection distortion", Range(0, 0.1)) = 0.03
        _Reflectivity ("Reflectivity", Range(0, 1)) = 0.7
        _Specular ("Sun glint", Range(0, 4)) = 1.2
        _WaveOffset ("Wave offset", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _DeepColor;
            float _WaveTiling;
            half _WaveStrength;
            half _Distortion;
            half _Reflectivity;
            half _Specular;
            float4 _WaveOffset;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_WaveMap);            SAMPLER(sampler_WaveMap);
            TEXTURE2D(_CityReflectionTex);  SAMPLER(sampler_CityReflectionTex);
            // Глобальные значения от City3DScene.
            float4x4 _CityReflectionVP;
            half _CityReflectionOn;
            half4 _CitySkyColor;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            // Нормали ряби записаны кодом как RGB (A = 1): наклон — в R и G.
            half2 WaveSlope(float2 uv)
            {
                half4 packed = SAMPLE_TEXTURE2D(_WaveMap, sampler_WaveMap, uv);
                return packed.rg * 2.0h - 1.0h;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv1 = input.positionWS.xz * _WaveTiling + _WaveOffset.xy;
                float2 uv2 = input.positionWS.xz * (_WaveTiling * 1.73) + _WaveOffset.zw;
                half2 slope = (WaveSlope(uv1) + WaveSlope(uv2)) * _WaveStrength;
                half3 normalWS = normalize(half3(slope.x, 1.0h, slope.y));

                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                half facing = saturate(dot(normalWS, (half3)viewDirWS));
                half fresnel = pow(1.0h - facing, 3.0h);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow = mainLight.shadowAttenuation;

                half3 flow = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half3 albedo = lerp(_DeepColor.rgb, _BaseColor.rgb * flow, 0.55h);
                half ndl = saturate(dot(normalWS, (half3)mainLight.direction));
                half3 diffuse = albedo * (SampleSH(normalWS) + mainLight.color * ndl * shadow);

                half3 reflection = _CitySkyColor.rgb;
                if (_CityReflectionOn > 0.5h)
                {
                    // Точка воды лежит на плоскости зеркала, поэтому в текстуре отражений она там же,
                    // куда её ставит матрица зеркальной камеры.
                    float4 clip = mul(_CityReflectionVP, float4(input.positionWS, 1.0));
                    float2 reflectionUv = clip.xy / max(clip.w, 0.0001) * 0.5 + 0.5;
                    reflectionUv += slope * _Distortion;
                    reflection = SAMPLE_TEXTURE2D(_CityReflectionTex, sampler_CityReflectionTex, saturate(reflectionUv)).rgb;
                }

                half amount = _Reflectivity * lerp(0.35h, 1.0h, fresnel);
                half3 color = lerp(diffuse, reflection, amount);

                // Блик солнца ограничен, чтобы не пересвечивать Bloom; в тени моста его нет.
                half3 halfDir = normalize((half3)mainLight.direction + (half3)viewDirWS);
                half glint = pow(saturate(dot(normalWS, halfDir)), 180.0h) * _Specular * shadow;
                color += min(glint, 1.5h) * mainLight.color;
                color *= lerp(0.72h, 1.0h, shadow);

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            float4 DepthVert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half DepthFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
