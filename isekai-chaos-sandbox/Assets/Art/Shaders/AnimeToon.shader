// Anime toon shader for URP 14 (Unity 2022.3), aimed at a Genshin-like look.
//  * 2-band cel ramp (lit / shadow) with a soft, tintable terminator
//  * Optional SDF face shadow (_FACE_SDF): smooth nose and cheek shadows that follow the sun
//  * Fresnel rim light + screen-space-free "back light" tint
//  * Hair "angel ring" specular band (_HAIR_SPEC)
//  * Inverted-hull outline pass with width constant in screen space
//  * Emission (glowing runes, eyes), main light shadows, additional lights (cheap ramp)
// SRP Batcher compatible: all material properties live in UnityPerMaterial.
Shader "IsekaiChaos/AnimeToon"
{
    Properties
    {
        [Header(Base)]
        _BaseMap ("Base Color", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _ShadowColor ("Shadow Tint (multiplied)", Color) = (0.78, 0.70, 0.86, 1)

        [Header(Cel Ramp)]
        _ShadowThreshold ("Shadow Threshold", Range(-1,1)) = 0.05
        _ShadowSoftness ("Terminator Softness", Range(0.001,0.5)) = 0.04
        [NoScaleOffset] _LightMap ("Light Map (R: AO bias, G: spec mask, B: ramp offset)", 2D) = "gray" {}

        [Header(Face SDF)]
        [Toggle(_FACE_SDF)] _FaceSdf ("Use Face SDF", Float) = 0
        [NoScaleOffset] _FaceSdfMap ("Face SDF (R)", 2D) = "white" {}
        _FaceForward ("Face Forward (set by script)", Vector) = (0,0,1,0)
        _FaceRight ("Face Right (set by script)", Vector) = (1,0,0,0)

        [Header(Rim)]
        _RimColor ("Rim Color", Color) = (1, 0.93, 0.85, 1)
        _RimPower ("Rim Power", Range(0.5, 10)) = 4
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.6

        [Header(Hair Specular)]
        [Toggle(_HAIR_SPEC)] _HairSpec ("Hair Angel Ring", Float) = 0
        _SpecColor ("Spec Color", Color) = (1, 0.97, 0.9, 1)
        _SpecSize ("Spec Size", Range(0, 1)) = 0.12

        [Header(Emission)]
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,1)
        [NoScaleOffset] _EmissionMap ("Emission Mask", 2D) = "black" {}

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0.22, 0.14, 0.20, 1)
        _OutlineWidth ("Outline Width (px-ish)", Range(0, 4)) = 1.2
        _OutlineMaxDistance ("Outline Fade Distance", Float) = 25
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half4  _ShadowColor;
            half   _ShadowThreshold;
            half   _ShadowSoftness;
            float4 _FaceForward;
            float4 _FaceRight;
            half4  _RimColor;
            half   _RimPower;
            half   _RimStrength;
            half4  _SpecColor;
            half   _SpecSize;
            half4  _EmissionColor;
            half4  _OutlineColor;
            half   _OutlineWidth;
            float  _OutlineMaxDistance;
        CBUFFER_END

        TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
        TEXTURE2D(_LightMap);     SAMPLER(sampler_LightMap);
        TEXTURE2D(_FaceSdfMap);   SAMPLER(sampler_FaceSdfMap);
        TEXTURE2D(_EmissionMap);  SAMPLER(sampler_EmissionMap);
        ENDHLSL

        // ------------------------------------------------------------------ Forward lit
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _FACE_SDF
            #pragma shader_feature_local _HAIR_SPEC
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                half   fog        : TEXCOORD3;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS   = TransformObjectToWorldNormal(v.normalOS);
                o.uv         = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fog        = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half CelRamp(half ndl, half offset)
            {
                return smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, ndl + offset);
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                half4 lm     = SAMPLE_TEXTURE2D(_LightMap, sampler_LightMap, i.uv);
                float3 N = normalize(i.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float3 L = mainLight.direction;
                half shadowAtten = mainLight.shadowAttenuation;

                // ---- main light lit factor
                half lit;
            #if defined(_FACE_SDF)
                // Project the light onto the face's horizontal plane and compare with the SDF.
                float3 fwd = normalize(_FaceForward.xyz);
                float3 right = normalize(_FaceRight.xyz);
                float2 l2 = normalize(float2(dot(L, right), dot(L, fwd)) + 1e-5);
                bool fromLeft = l2.x < 0;
                float2 sdfUV = fromLeft ? float2(1 - i.uv.x, i.uv.y) : i.uv;
                half sdf = SAMPLE_TEXTURE2D(_FaceSdfMap, sampler_FaceSdfMap, sdfUV).r;
                half threshold = 1 - (l2.y * 0.5 + 0.5);          // 0 when lit from front, 1 from behind
                lit = smoothstep(threshold - _ShadowSoftness, threshold + _ShadowSoftness, sdf);
                lit *= lerp(1, shadowAtten, 0.5);                // soften cast shadows on faces
            #else
                half ndl = dot(N, L);
                half rampOffset = (lm.b - 0.5) * 0.5 + (lm.r - 0.5) * 0.5;
                lit = CelRamp(ndl, rampOffset) * shadowAtten;
            #endif

                half3 lightCol = mainLight.color;
                half3 shaded   = albedo.rgb * _ShadowColor.rgb;
                half3 color    = lerp(shaded, albedo.rgb, lit) * lerp(half3(1,1,1), lightCol, 0.85);

                // ---- ambient (flat SH, keeps shadows colorful rather than black)
                color += albedo.rgb * SampleSH(float3(0,1,0)) * 0.25;

                // ---- additional lights: cheap 1-band ramp
            #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint li = 0u; li < count; ++li)
                {
                    Light a = GetAdditionalLight(li, i.positionWS);
                    half al = step(0.1, dot(N, a.direction)) * a.distanceAttenuation;
                    color += albedo.rgb * a.color * al * 0.6;
                }
            #endif

                // ---- hair angel ring (view-dependent band)
            #if defined(_HAIR_SPEC)
                float3 H = normalize(L + V);
                half band = smoothstep(1 - _SpecSize, 1 - _SpecSize * 0.6, dot(N, H));
                color += _SpecColor.rgb * band * lm.g * lit;
            #endif

                // ---- rim light, stronger on the lit side
                half rim = pow(saturate(1 - dot(N, V)), _RimPower) * _RimStrength;
                rim *= saturate(lit + 0.35);
                color += _RimColor.rgb * rim;

                // ---- emission
                color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).r * _EmissionColor.rgb;

                color = MixFog(color, i.fog);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ Outline (inverted hull)
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; };
            struct Varyings   { float4 positionCS : SV_POSITION; half fog : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float4 cs = TransformObjectToHClip(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
                float3 nCS = mul((float3x3)UNITY_MATRIX_VP, nWS);
                // Screen-space constant width; vertex color alpha lets artists thin lines (fingers, hair tips).
                float dist = cs.w;
                float fade = saturate(1 - dist / max(_OutlineMaxDistance, 0.01));
                float2 offset = normalize(nCS.xy) * (_OutlineWidth * 0.0025) * cs.w * v.color.a * fade;
                offset.x *= _ScreenParams.y / _ScreenParams.x;   // correct aspect
                cs.xy += offset;
                o.positionCS = cs;
                o.fog = ComputeFogFactor(cs.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                return half4(MixFog(_OutlineColor.rgb, i.fog), 1);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ Shadow caster
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 pWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
                float4 cs = TransformWorldToHClip(ApplyShadowBias(pWS, nWS, _LightDirection));
            #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
            #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.positionCS = cs;
                return o;
            }

            half4 frag (Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        // ------------------------------------------------------------------ Depth only (for SSAO / depth texture)
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };
            Varyings vert (Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half frag (Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }
    }
    FallBack Off
}
