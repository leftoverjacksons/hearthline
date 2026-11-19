// The region's ground (ADR 0034). Looks like the URP Lit material it replaces (painted ground map, soil detail
// multiplied in at 2x, detail normal), and keeps the same property names so WeatherView can still darken and wet
// it. Desire paths: where the wear map (WearView) is high, the ground turns to trodden dirt. The edge is broken by
// the soil detail's own brightness, so a path frays into the grass instead of being painted on.
Shader "Hearthline/RegionGround"
{
    Properties
    {
        _BaseMap ("Ground map", 2D) = "white" {}
        _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.05
        _DetailAlbedoMap ("Detail albedo (x2)", 2D) = "grey" {}
        _DetailAlbedoMapScale ("Detail albedo strength", Range(0, 2)) = 0.55
        _DetailNormalMap ("Detail normal", 2D) = "bump" {}
        _DetailNormalMapScale ("Detail normal strength", Range(0, 2)) = 0.5
        _DirtDetail ("Detail strength on dirt", Range(0, 2)) = 1.0
        _DirtNormal ("Detail normal strength on dirt", Range(0, 2)) = 0.9
        _EdgeSharpness ("Path edge sharpness", Range(1, 20)) = 4
        _EdgeBreakup ("Path edge breakup", Range(0, 2)) = 0.7
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _DetailAlbedoMap_ST, _DetailNormalMap_ST;
            half4 _BaseColor;
            float _Smoothness, _DetailAlbedoMapScale, _DetailNormalMapScale, _DirtDetail, _DirtNormal, _EdgeSharpness, _EdgeBreakup;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DetailAlbedoMap); SAMPLER(sampler_DetailAlbedoMap);
            TEXTURE2D(_DetailNormalMap);
            TEXTURE2D(_HL_WearMap); SAMPLER(sampler_HL_WearMap);
            float4 _HL_WearRect;   // origin x, origin z, size; size 0 means none
            float4 _HL_WearLevels; // short, dirt, bare
            half4 _HL_DirtColor;
            float _HL_WearDebug; // cheat panel: show wear in red

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2; float fogFactor : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = input.uv; o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half3 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, TRANSFORM_TEX(i.uv, _BaseMap)).rgb;
                float2 duv = TRANSFORM_TEX(i.uv, _DetailAlbedoMap);
                half3 detail = SAMPLE_TEXTURE2D(_DetailAlbedoMap, sampler_DetailAlbedoMap, duv).rgb;
                half3 detailNormal = UnpackNormal(SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailAlbedoMap, TRANSFORM_TEX(i.uv, _DetailNormalMap)));

                // How trodden: none, then dirt from the "dirt" level to the "bare" level, frayed by the soil grain.
                half path = 0, wearSeen = 0;
                if (_HL_WearRect.z > 0.0)
                {
                    float2 wuv = (i.positionWS.xz - _HL_WearRect.xy) / _HL_WearRect.z;
                    if (all(wuv > 0.0) && all(wuv < 1.0))
                    {
                        half w = SAMPLE_TEXTURE2D(_HL_WearMap, sampler_HL_WearMap, wuv).r;
                        wearSeen = w;
                        half d = smoothstep(_HL_WearLevels.y - 0.15, _HL_WearLevels.z, w);
                        half grain = dot(detail, half3(0.33, 0.5, 0.17));
                        path = d > 0.001 ? saturate((d - 0.5 + (0.5 - grain) * _EdgeBreakup) * _EdgeSharpness + 0.5) : 0;
                    }
                }

                half3 ground = base * lerp(half3(1, 1, 1), detail * 2.0, _DetailAlbedoMapScale);
                half3 dirt = _HL_DirtColor.rgb * lerp(half3(1, 1, 1), detail * 2.0, _DirtDetail);
                half3 albedo = lerp(ground, dirt, path) * _BaseColor.rgb;
                albedo = lerp(albedo, half3(1, 0, 0), wearSeen * _HL_WearDebug);

                // The ground map runs u along x and v along z, so the tangent frame follows the world axes.
                float3 n = normalize(i.normalWS);
                float3 t = normalize(float3(1, 0, 0) - n * n.x);
                float3 b = cross(t, n);
                half strength = lerp(_DetailNormalMapScale, _DirtNormal, path);
                half3 nTS = normalize(half3(detailNormal.xy * strength, detailNormal.z));
                float3 normalWS = normalize(TransformTangentToWorld(nTS, float3x3(t, b, n)));

                InputData input = (InputData)0;
                input.positionWS = i.positionWS; input.normalWS = normalWS;
                input.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                input.fogCoord = i.fogFactor;
                input.bakedGI = SampleSH(normalWS);
                input.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask = half4(1, 1, 1, 1);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo; surface.alpha = 1; surface.occlusion = 1;
                surface.smoothness = _Smoothness * (1.0 - 0.3 * path);
                surface.normalTS = nTS;
                half4 color = UniversalFragmentPBR(input, surface);
                color.rgb = MixFog(color.rgb, i.fogFactor);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection, _LightPosition;
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; };
            V Vert(A i)
            {
                V o;
                float3 p = TransformObjectToWorld(i.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(i.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 dir = normalize(_LightPosition - p);
            #else
                float3 dir = _LightDirection;
            #endif
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(p, n, dir));
            #if UNITY_REVERSED_Z
                o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return o;
            }
            half4 Frag(V i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; };
            V Vert(A i) { V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); return o; }
            half Frag(V i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            V Vert(A i) { V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.normalWS = TransformObjectToWorldNormal(i.normalOS); return o; }
            half4 Frag(V i) : SV_Target { return half4(normalize(i.normalWS), 0); }
            ENDHLSL
        }
    }
    FallBack Off
}
