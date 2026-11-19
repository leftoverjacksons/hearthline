// Grass blades placed by GrassBlades.compute and drawn by GrassField (ADR 0033). Each blade is built here from
// its record: a tapering strip curved along a Bezier, leaning, swaying in the wind and pushed aside by the
// player. Seen edge-on, a blade turns a little toward the camera so the field never looks thin.
// Colour comes from the painted ground under the blade, lighter toward the tip and toward a dry gold where the
// grass map says it's dry. Lighting uses the same PBR lighting as the ground, with the blade's normal bent toward
// the ground's (more of it with distance), so the meadow and the earth under it read as one surface. Light shows
// through the tips when backlit. Values come from GrassField's serialized fields.
Shader "Hearthline/GrassBlade"
{
    Properties
    {
        _DryTip ("Dry tip colour", Color) = (0.66, 0.6, 0.3, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        #define SEGMENTS 4
        static const float2 kCorner[6] = { float2(0, 0), float2(1, 0), float2(1, 1), float2(0, 0), float2(1, 1), float2(0, 1) };

        struct Blade
        {
            float4 rootFacing;
            float4 shape;
            float4 extra;
        };
        StructuredBuffer<Blade> _Blades;

        CBUFFER_START(UnityPerMaterial)
            half4 _DryTip;
            float _TipLift, _RootShade, _DryAmount, _ClumpVariation, _BladeVariation, _Translucency, _RootAO, _AOFadeDistance;
            float _GroundNormalNear, _GroundNormalFar, _CameraFacing, _Roundness, _Radius;
            float _WindStrength, _WindSpeed, _WindScale, _GustStrength, _Trample;
            float4 _Pusher; // position, radius
        CBUFFER_END

        TEXTURE2D(_HL_GroundMap); SAMPLER(sampler_HL_GroundMap);
        float4 _HL_GroundMapRect;
        float _HL_WindBoost, _HL_Wetness;
        // Matting (TrampleField): r how hard the grass was pressed, gb the way it was pushed (0.5 is none).
        TEXTURE2D(_HL_TrampleMap); SAMPLER(sampler_HL_TrampleMap);
        float4 _HL_TrampleRect; // origin x, origin z, size; size 0 means none
        // Desire paths (WearView): r wear 0..1; levels are short, dirt, bare (WearRules).
        TEXTURE2D(_HL_WearMap); SAMPLER(sampler_HL_WearMap);
        float4 _HL_WearRect;
        float4 _HL_WearLevels;
        half4 _HL_DirtColor;

        float TrampleAt(float2 xz, out float2 dir)
        {
            dir = 0;
            if (_HL_TrampleRect.z <= 0.0) return 0;
            float2 uv = (xz - _HL_TrampleRect.xy) / _HL_TrampleRect.z;
            if (any(uv <= 0.0) || any(uv >= 1.0)) return 0;
            float4 tr = SAMPLE_TEXTURE2D_LOD(_HL_TrampleMap, sampler_HL_TrampleMap, uv, 0);
            dir = tr.gb * 2.0 - 1.0;
            return tr.r;
        }

        float WearAt(float2 xz)
        {
            if (_HL_WearRect.z <= 0.0) return 0;
            float2 uv = (xz - _HL_WearRect.xy) / _HL_WearRect.z;
            if (any(uv <= 0.0) || any(uv >= 1.0)) return 0;
            return SAMPLE_TEXTURE2D_LOD(_HL_WearMap, sampler_HL_WearMap, uv, 0).r;
        }

        struct BladeVertex
        {
            float3 positionWS;
            float3 normalWS;
            float3 groundNormalWS;
            float t;
        };

        BladeVertex BuildBlade(uint vertexID, uint instanceID, out Blade b)
        {
            b = _Blades[instanceID];
            uint quad = vertexID / 6;
            float2 corner = kCorner[vertexID % 6];
            float t = (quad + corner.y) / SEGMENTS;
            float side = corner.x - 0.5;

            float3 root = b.rootFacing.xyz;
            float h = b.shape.x;
            float s, c;
            sincos(b.rootFacing.w, s, c);
            float2 right = float2(c, s);

            // Edge-on to the camera: turn toward it.
            float2 toCam = normalize(_WorldSpaceCameraPos.xz - root.xz + 1e-4);
            float2 fwd = float2(-right.y, right.x);
            float edge = 1.0 - abs(dot(fwd, toCam));
            float2 face = float2(toCam.y, -toCam.x);
            if (dot(face, right) < 0.0) face = -face;
            right = normalize(lerp(right, face, edge * _CameraFacing));
            fwd = float2(-right.y, right.x);

            // Lean, wind (gusts roll across the world, as in Foliage.shader) and the player's push.
            float time = _Time.y * _WindSpeed;
            float2 windDir = normalize(float2(1.0, 0.4));
            float phase = dot(root.xz, windDir) * _WindScale;
            float gust = 0.5 + 0.5 * sin(time * 0.37 - root.x * 0.021) * sin(time * 0.23 - root.z * 0.017);
            float sway = sin(time - phase) * 0.6 + sin(time * 2.7 - phase * 1.9 + b.shape.w * 6.2831853) * 0.25 + 0.55;
            float wind = _WindStrength * (1.0 + _HL_WindBoost) * (1.0 + _GustStrength * gust);
            float2 tip = fwd * b.shape.z * h + windDir * sway * wind * h;
            float2 away = root.xz - _Pusher.xz;
            float pushDist = length(away);
            float push = saturate(1.0 - pushDist / max(_Pusher.w, 0.01)) * step(abs(root.y - _Pusher.y), 2.5);
            tip += away / max(pushDist, 1e-3) * push * h * 1.1;

            // Matted where something walked through: laid over the way it was pushed, each blade a little off
            // that line so it doesn't look combed, and stood back up as the trample map recovers.
            float2 mat;
            float press = saturate(TrampleAt(root.xz, mat) * _Trample);
            if (dot(mat, mat) < 0.01) mat = fwd;
            float sa, ca;
            sincos((b.shape.w - 0.5) * 1.4, sa, ca);
            mat = normalize(float2(mat.x * ca - mat.y * sa, mat.x * sa + mat.y * ca));
            tip = lerp(tip, mat * h * 0.9, press);

            float bendAmount = length(tip);
            float tipY = sqrt(max(h * h - bendAmount * bendAmount * 0.6, h * h * 0.08)); // keeps the blade's length
            tipY *= lerp(1.0, 0.22, press);

            float3 p0 = root;
            float3 p1 = root + float3(0.0, tipY * 0.55, 0.0);
            float3 p2 = root + float3(tip.x, tipY, tip.y);
            float u = 1.0 - t;
            float3 onCurve = u * u * p0 + 2.0 * u * t * p1 + t * t * p2;
            float3 tangent = normalize(2.0 * u * (p1 - p0) + 2.0 * t * (p2 - p1));
            float3 right3 = float3(right.x, 0.0, right.y);
            float width = b.shape.y * pow(saturate(1.0 - t), 0.7);

            BladeVertex v;
            v.positionWS = onCurve + right3 * side * width;
            float3 n = normalize(cross(right3, tangent));
            v.normalWS = normalize(n + right3 * side * 2.0 * _Roundness); // a rounded blade, not a flat card
            v.groundNormalWS = float3(b.extra.x, sqrt(saturate(1.0 - b.extra.x * b.extra.x - b.extra.y * b.extra.y)), b.extra.y);
            v.t = t;
            return v;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            // No SSAO on blades: a dense meadow would shade itself black; root shade comes from _RootAO.
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 groundNormalWS : TEXCOORD2;
                half3 ground : TEXCOORD3;
                float4 data : TEXCOORD4; // t, dryness, clump random, blade random
                float2 distFog : TEXCOORD5;
            };

            Varyings Vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                Blade b;
                BladeVertex v = BuildBlade(vertexID, instanceID, b);
                Varyings o;
                o.positionWS = v.positionWS;
                o.positionCS = TransformWorldToHClip(v.positionWS);
                o.normalWS = v.normalWS;
                o.groundNormalWS = v.groundNormalWS;
                float2 groundUv = (b.rootFacing.xz - _HL_GroundMapRect.xy) / max(_HL_GroundMapRect.z, 1.0);
                o.ground = SAMPLE_TEXTURE2D_LOD(_HL_GroundMap, sampler_HL_GroundMap, groundUv, 0).rgb;
                // On a worn path the ground is dirt, so the roots there are dusty too (as RegionGround.shader).
                float worn = smoothstep(_HL_WearLevels.x, _HL_WearLevels.z, WearAt(b.rootFacing.xz));
                o.ground = lerp(o.ground, _HL_DirtColor.rgb, worn * 0.85);
                o.data = float4(v.t, b.extra.z, b.extra.w, b.shape.w);
                o.distFog = float2(distance(_WorldSpaceCameraPos, b.rootFacing.xyz), ComputeFogFactor(o.positionCS.z));
                return o;
            }

            half4 Frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float t = i.data.x;
                float dist = i.distFog.x;
                half3 ground = i.ground;

                // Colour: the ground's at the root, lighter and (where dry) golder toward the tip; far blades fade
                // into the ground's colour so there's no ring where the field ends.
                half3 tipColour = lerp(ground * _TipLift, _DryTip.rgb, saturate(i.data.y * _DryAmount));
                half3 albedo = lerp(ground * _RootShade, tipColour, pow(abs(t), 0.8));
                albedo *= 1.0 + (i.data.z - 0.5) * _ClumpVariation + (i.data.w - 0.5) * _BladeVariation;
                albedo = lerp(albedo, ground, saturate((dist - _Radius * 0.45) / (_Radius * 0.5)));
                albedo *= 1.0 - 0.3 * _HL_Wetness;
                half ao = lerp(lerp(_RootAO, 1.0, t), 1.0, saturate(dist / max(_AOFadeDistance, 0.01)));

                // Light: the blade's own normal (facing the viewer), bent toward the ground's.
                float3 n = normalize(i.normalWS) * (front ? 1.0 : -1.0);
                // The lower half is lit exactly like the ground it grows from, so roots and earth meet without a seam.
                float groundWeight = lerp(lerp(1.0, _GroundNormalNear, saturate(t * 2.0 - 0.3)), 1.0, saturate(dist / max(_GroundNormalFar, 0.01)));
                n = normalize(lerp(n, normalize(i.groundNormalWS), groundWeight));

                InputData input = (InputData)0;
                input.positionWS = i.positionWS;
                input.normalWS = n;
                input.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                input.fogCoord = i.distFog.y;
                input.bakedGI = SampleSH(n);
                input.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask = half4(1, 1, 1, 1);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.alpha = 1;
                surface.smoothness = lerp(0.05, 0.22, t);
                surface.occlusion = ao;
                half4 colour = UniversalFragmentPBR(input, surface);

                // Backlit tips glow.
                Light sun = GetMainLight(input.shadowCoord);
                half back = pow(saturate(dot(input.viewDirectionWS, -sun.direction)), 4.0) * t;
                colour.rgb += albedo * sun.color * sun.shadowAttenuation * back * _Translucency;

                colour.rgb = MixFog(colour.rgb, i.distFog.y);
                return colour;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID) : SV_POSITION
            {
                Blade b;
                return TransformWorldToHClip(BuildBlade(vertexID, instanceID, b).positionWS);
            }
            half Frag(float4 positionCS : SV_POSITION) : SV_Target { return positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            V Vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                Blade b;
                BladeVertex v = BuildBlade(vertexID, instanceID, b);
                V o;
                o.positionCS = TransformWorldToHClip(v.positionWS);
                o.normalWS = v.groundNormalWS; // SSAO sees the meadow as ground, not as thousands of edges
                return o;
            }
            half4 Frag(V i) : SV_Target { return half4(normalize(i.normalWS), 0); }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float4 Vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID) : SV_POSITION
            {
                Blade b;
                BladeVertex v = BuildBlade(vertexID, instanceID, b);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(v.positionWS, v.normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }
            half4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}
