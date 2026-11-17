// Ground that wears from grass to dirt (style test, ADR 0022). Two tiling texture sets (albedo + normal),
// blended by the mesh's vertex colour red channel (0 grass, 1 dirt), with the edge broken up by the grass
// texture's own brightness so worn patches look trodden, not painted. Main light with shadows, SH ambient,
// SSAO and fog. Values are for tuning by eye.
Shader "Hearthline/GroundBlend"
{
    Properties
    {
        _GrassMap ("Grass albedo", 2D) = "white" {}
        _GrassNormal ("Grass normal", 2D) = "bump" {}
        _DirtMap ("Dirt albedo", 2D) = "white" {}
        _DirtNormal ("Dirt normal", 2D) = "bump" {}
        _GrassTint ("Grass tint", Color) = (1, 1, 1, 1)
        _DirtTint ("Dirt tint", Color) = (1, 1, 1, 1)
        _Tiling ("Tiling (per metre)", Float) = 0.25
        _EdgeSharpness ("Edge sharpness", Range(1, 20)) = 6
        _NormalStrength ("Normal strength", Range(0, 2)) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.08
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _GrassMap_ST, _DirtMap_ST;
            half4 _GrassTint, _DirtTint;
            float _Tiling, _EdgeSharpness, _NormalStrength, _Smoothness;
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
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_GrassMap); SAMPLER(sampler_GrassMap);
            TEXTURE2D(_GrassNormal); TEXTURE2D(_DirtMap); TEXTURE2D(_DirtNormal);

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2; float mask : TEXCOORD3; float fogFactor : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS; o.normalWS = n.normalWS;
                o.tangentWS = float4(n.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                o.mask = input.color.r; o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.positionWS.xz * _Tiling;
                half3 grass = SAMPLE_TEXTURE2D(_GrassMap, sampler_GrassMap, uv).rgb * _GrassTint.rgb;
                half3 dirt = SAMPLE_TEXTURE2D(_DirtMap, sampler_GrassMap, uv * 1.3).rgb * _DirtTint.rgb;
                // Break the edge with the grass's own detail: tufts survive longest at the edge of a worn patch.
                half tuft = dot(grass, half3(0.33, 0.5, 0.17)) / max(0.05, dot(_GrassTint.rgb, half3(0.33, 0.5, 0.17)));
                half w = saturate((i.mask - 0.5 + (0.5 - tuft) * 0.6) * _EdgeSharpness + 0.5);
                half3 albedo = lerp(grass, dirt, w);
                half3 nGrass = UnpackNormalScale(SAMPLE_TEXTURE2D(_GrassNormal, sampler_GrassMap, uv), _NormalStrength);
                half3 nDirt = UnpackNormalScale(SAMPLE_TEXTURE2D(_DirtNormal, sampler_GrassMap, uv * 1.3), _NormalStrength);
                half3 nTS = normalize(lerp(nGrass, nDirt, w));
                float3 bitangent = i.tangentWS.w * cross(i.normalWS, i.tangentWS.xyz);
                float3 normalWS = normalize(TransformTangentToWorld(nTS, float3x3(i.tangentWS.xyz, bitangent, i.normalWS)));

                InputData input = (InputData)0;
                input.positionWS = i.positionWS; input.normalWS = normalWS;
                input.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                input.fogCoord = i.fogFactor;
                input.bakedGI = SampleSH(normalWS);
                input.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask = half4(1, 1, 1, 1);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo; surface.alpha = 1; surface.smoothness = _Smoothness; surface.occlusion = 1;
                surface.normalTS = nTS;
                half4 color = UniversalFragmentPBR(input, surface);
                color.rgb = MixFog(color.rgb, i.fogFactor);
                return color;
            }
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
