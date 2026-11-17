// Leaves, grass, flowers and kudzu that sway in the wind (ADR 0018). Simple foliage lighting: wrapped
// diffuse from the main light with its shadows, spherical-harmonics ambient, SSAO and fog. The wind moves
// vertices by their height above the object's pivot, so trunks and roots stay put while crowns and blade
// tips sway; gusts roll across the world. Works with GPU instancing (InstancedScatter).
// Wind values are per material and meant for tuning by eye.
// Leaf cards (ADR 0020): with Alpha clip on, the base map's alpha cuts out each leaf in every pass (so shadows
// and SSAO see the leaves, not the quads). With Vertex AO on, the mesh's vertex colour red channel darkens the
// inside of the crown (baked in Blender); the crown's normals are bent outwards there too, so it shades as
// one soft mass instead of loose cards.
Shader "Hearthline/Foliage"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _WindStrength ("Wind: sway per metre of height", Float) = 0.05
        _WindSpeed ("Wind: speed", Float) = 1.2
        _WindScale ("Wind: how fast it changes across the world", Float) = 0.07
        _GustStrength ("Wind: gust strength", Float) = 0.8
        _HeightStart ("Wind: height (m) where sway begins", Float) = 0.2
        _Flutter ("Wind: leaf flutter", Float) = 0.25
        _Wrap ("Light wrap (soft foliage light)", Range(0, 1)) = 0.65
        _Translucency ("Light through leaves", Range(0, 1)) = 0.35
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha clip (leaf cards)", Float) = 0
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.5
        [Toggle(_VERTEX_AO)] _VertexAO ("Vertex AO (baked crown shading)", Float) = 0
        _AOStrength ("Vertex AO strength", Range(0, 1)) = 0.8
        _ShadowStrength ("Received shadow strength (dense crowns shadow themselves)", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _WindStrength, _WindSpeed, _WindScale, _GustStrength, _HeightStart, _Flutter, _Wrap, _Translucency, _Cull;
            float _AlphaClip, _Cutoff, _VertexAO, _AOStrength, _ShadowStrength;
        CBUFFER_END
        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        void ClipLeaf(float2 uv)
        {
            #if defined(_ALPHATEST_ON)
            clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a - _Cutoff);
            #endif
        }

        // Object-space position -> swaying world-space position.
        float3 WindWorldPosition(float3 positionOS)
        {
            float3 pivotWS = TransformObjectToWorld(float3(0, 0, 0));
            float3 posWS = TransformObjectToWorld(positionOS);
            float height = max(0.0, posWS.y - pivotWS.y - _HeightStart);
            float t = _Time.y * _WindSpeed;
            float phase = dot(pivotWS.xz, float2(_WindScale, _WindScale * 0.71));
            float gust = 0.5 + 0.5 * sin(t * 0.37 - pivotWS.x * 0.021) * sin(t * 0.23 - pivotWS.z * 0.017);
            float sway = sin(t + phase) * 0.7 + sin(t * 2.3 + phase * 1.7) * 0.3;
            float flutter = sin(t * 7.0 + dot(posWS, float3(1.7, 1.3, 2.1))) * _Flutter;
            float amount = height * _WindStrength * (1.0 + _GustStrength * gust);
            float2 dir = normalize(float2(1.0, 0.4));
            posWS.xz += dir * (sway + 0.35) * amount + float2(flutter, -flutter * 0.6) * amount;
            posWS.y -= abs(sway) * amount * 0.12;
            return posWS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _VERTEX_AO
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                float ao : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionWS = WindWorldPosition(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                #if defined(_VERTEX_AO)
                output.ao = lerp(1.0, input.color.r, _AOStrength);
                #else
                output.ao = 1.0;
                #endif
                return output;
            }

            half4 Frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                #if defined(_ALPHATEST_ON)
                clip(texel.a - _Cutoff);
                #endif
                half3 albedo = texel.rgb * _BaseColor.rgb;
                #if defined(_VERTEX_AO)
                float3 normalWS = normalize(input.normalWS); // bent crown normals: both faces share them
                #else
                float3 normalWS = normalize(input.normalWS) * (isFrontFace ? 1.0 : -1.0);
                #endif

                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half ndl = dot(normalWS, light.direction);
                half diffuse = lerp(saturate(ndl), saturate((ndl + _Wrap) / (1.0 + _Wrap)), _Wrap);
                // Leaves lit from behind glow a little.
                half back = saturate(-ndl) * _Translucency;
                half3 direct = albedo * light.color * (diffuse + back) * lerp(1.0, light.shadowAttenuation, _ShadowStrength) * light.distanceAttenuation;
                half3 ambient = albedo * SampleSH(normalWS) * input.ao;
                direct *= lerp(1.0, input.ao, 0.5); // the inside of the crown sees less sun too

                #if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS));
                ambient *= ao.indirectAmbientOcclusion;
                direct *= ao.directAmbientOcclusion;
                #endif

                half3 color = MixFog(direct + ambient, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature_local _ALPHATEST_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = WindWorldPosition(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirection = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirection = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target { ClipLeaf(input.uv); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #pragma shader_feature_local _ALPHATEST_ON

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformWorldToHClip(WindWorldPosition(input.positionOS.xyz));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half Frag(Varyings input) : SV_Target { ClipLeaf(input.uv); return input.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #pragma shader_feature_local _ALPHATEST_ON

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float2 uv : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformWorldToHClip(WindWorldPosition(input.positionOS.xyz));
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                ClipLeaf(input.uv);
                float3 n = normalize(input.normalWS) * (isFrontFace ? 1.0 : -1.0);
                return half4(n, 0.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
