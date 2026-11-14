// Draws the painting's brushstrokes (see BrushStrokeWorld.cs). One instance per visible stroke, six
// vertices each, all built in the vertex shader from the stroke buffer:
//
//   Surface   a daub lying on a wall, the ground or a face. Its colour is read from the lit world
//             under it (the camera's opaque texture), so lights, shadows and fog still work.
//   Grass     an upright blade turned toward you, swaying.
//   Sky       huge strokes on a dome around the camera, behind everything.
//   Restless  the entity: never-dry strokes that crawl over its body.
//   Trail     black drips where it walked.
//
// _PaintAttention (PaintAttention.cs) says how finished the paint is in each direction. Where you have
// not looked, strokes are wet: bigger, running downhill, raw umber and bare canvas. As you look, each
// stroke dries at its own moment, so the world paints itself in stroke by stroke.
Shader "HorrorGame/BrushStroke"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "BrushStrokes"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "BrushStrokeCommon.hlsl"

            StructuredBuffer<Stroke> _Strokes;
            StructuredBuffer<uint> _Visible;
            StructuredBuffer<float4x4> _Bones;
            float4 _StrokeAmbient;
            float _PaintAttention[64];
            float _PaintDread;

            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0; // x: along the stroke -1..1, y: across
                nointerpolation float4 color : TEXCOORD1; // rgb paint, a random
                nointerpolation float4 info : TEXCOORD2; // x kind, y wet
            };

            float AttentionAt(float yawDegrees)
            {
                float f = frac(yawDegrees / 360.0) * 64.0;
                uint a = (uint)floor(f) % 64u;
                uint b = (a + 1u) % 64u;
                return lerp(_PaintAttention[a], _PaintAttention[b], frac(f));
            }

            float Hash12(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash12(i), Hash12(i + float2(1, 0)), f.x),
                            lerp(Hash12(i + float2(0, 1)), Hash12(i + float2(1, 1)), f.x), f.y);
            }

            float3 SampleWorld(float3 positionWS)
            {
                float4 cs = TransformWorldToHClip(positionWS);
                float w = max(cs.w, 1e-4);
                float4 ndc = cs * 0.5;
                float2 uv = (float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w) / w;
                return SAMPLE_TEXTURE2D_X_LOD(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, saturate(uv), 0).rgb;
            }

            Varyings Vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                Varyings o = (Varyings)0;
                uint id = _Visible[instanceID];
                Stroke s = _Strokes[id];
                uint kind = StrokeKind(s);
                float4x4 bone = _Bones[StrokeBone(s)];

                float3 root = mul(bone, float4(s.position, 1.0)).xyz;
                float3 n = normalize(mul((float3x3)bone, UnpackDirection(s.normal)) + float3(0, 1e-6, 0));
                float3 t = mul((float3x3)bone, UnpackDirection(s.tangent));
                float2 halfSize = UnpackHalfSize(s.size);
                float4 paint = UnpackColor(s.color);
                bool tintOnWorld = paint.a > 0.5;
                float r0 = StrokeRandom(id, 1);
                float r1 = StrokeRandom(id, 2);
                float r2 = StrokeRandom(id, 3);
                float time = _Time.y;
                float dread = saturate(_PaintDread);

                float3 toCam = _WorldSpaceCameraPos - root;
                float dist = max(length(toCam), 1e-3);
                float3 viewDir = toCam / dist;

                // How finished the paint is this way. Each stroke dries at its own moment.
                float finished = 1.0;
                if (kind != KIND_TRAIL)
                    finished = AttentionAt(degrees(atan2(-toCam.x, -toCam.z)) + (r1 - 0.5) * 8.0);
                float wet = 1.0 - saturate((finished - r0 * 0.85) * 6.0);
                if (kind == KIND_RESTLESS)
                    wet = max(wet, 0.25);
                if (kind == KIND_TRAIL)
                    wet = 0.0;

                // Distant strokes are thinned out by the cull, so the survivors are drawn bigger.
                float lod = kind == KIND_SKY ? 1.0 : min(3.5, rsqrt(LodFraction(dist)));

                float3 down = float3(0, -1, 0);
                float halfLen = halfSize.x;
                float halfWid = halfSize.y;
                float3 along;
                float3 side;
                float3 center;
                if (kind == KIND_GRASS)
                {
                    float3 wind = float3(sin(time * 0.9 + root.x * 0.3 + root.z * 0.1), 0.0, cos(time * 0.7 + root.z * 0.3)) * 0.12;
                    along = normalize(n + t * (0.15 + 0.3 * r1) + wind + t * wet * 0.6);
                    side = cross(along, viewDir);
                    side = dot(side, side) > 1e-6 ? normalize(side) : t;
                    halfLen *= sqrt(lod) * (1.0 + wet * 0.15);
                    halfWid *= lod;
                    center = root + along * halfLen;
                }
                else
                {
                    along = t - n * dot(t, n);
                    along = dot(along, along) > 1e-8 ? normalize(along) : normalize(cross(n, float3(0.31, 0.72, 0.62)));
                    // Wet paint runs: strokes turn to follow gravity and slide down.
                    float3 g = down - n * dot(down, n);
                    bool canRun = dot(g, g) > 1e-4 && kind != KIND_SKY;
                    if (canRun)
                    {
                        g = normalize(g);
                        along = normalize(lerp(along, g * (dot(along, g) >= 0.0 ? 1.0 : -1.0), wet * 0.65) + n * 1e-4);
                    }
                    float wobble = sin(time * 13.0 + r1 * 40.0) * dread * 0.3;
                    if (kind == KIND_RESTLESS)
                        wobble += sin(time * 4.0 + r2 * 30.0) * 0.22;
                    side = cross(n, along);
                    along = along * cos(wobble) + side * sin(wobble);
                    side = cross(n, along);
                    halfLen *= lod * (1.0 + wet * 0.9);
                    halfWid *= lod * (1.0 + wet * 0.35);
                    float lift = kind == KIND_SKY ? 0.0 : StrokeLift(s) + r2 * 0.004 + dist * 0.0012;
                    center = root + n * lift;
                    if (canRun)
                        center += g * wet * r1 * halfLen * 0.8 * frac(time * 0.07 + r2);
                    if (kind == KIND_RESTLESS)
                        center += along * sin(time * 6.0 + r1 * 20.0) * halfLen * 0.25 + n * abs(sin(time * 9.0 + r2 * 10.0)) * 0.015;
                }

                // Two triangles: (-1,-1) (1,-1) (1,1) and (-1,-1) (1,1) (-1,1).
                float cx = (vertexID == 1 || vertexID == 2 || vertexID == 4) ? 1.0 : -1.0;
                float cy = (vertexID == 2 || vertexID == 4 || vertexID == 5) ? 1.0 : -1.0;
                float3 positionWS;
                if (kind == KIND_GRASS)
                {
                    positionWS = center + along * cy * halfLen + side * cx * halfWid * (cy > 0.0 ? 0.4 : 1.0);
                    o.uv = float2(cy, cx);
                }
                else
                {
                    positionWS = center + along * cx * halfLen + side * cy * halfWid;
                    o.uv = float2(cx, cy);
                }
                o.positionCS = TransformWorldToHClip(positionWS);
                if (kind == KIND_SKY)
                {
                    // On the far plane: behind everything, never clipped by it.
                    #if UNITY_REVERSED_Z
                    o.positionCS.z = o.positionCS.w * 1e-6;
                    #else
                    o.positionCS.z = o.positionCS.w * (1.0 - 1e-6);
                    #endif
                }

                float3 colour;
                if (tintOnWorld)
                {
                    colour = SampleWorld(root + n * 0.02) * paint.rgb * 2.0;
                }
                else if (kind == KIND_SKY)
                {
                    // Melts into the fog at the horizon, so the field fades into the sky.
                    float elevation = saturate(-n.y);
                    colour = lerp(unity_FogColor.rgb, paint.rgb, smoothstep(0.02, 0.3, elevation));
                }
                else
                {
                    Light mainLight = GetMainLight(TransformWorldToShadowCoord(root));
                    float3 lightNormal = kind == KIND_GRASS ? normalize(float3(0, 1, 0) + viewDir * 0.3) : n;
                    float ndl = saturate(dot(lightNormal, mainLight.direction) * 0.6 + 0.4);
                    colour = paint.rgb * (mainLight.color * ndl * mainLight.shadowAttenuation + _StrokeAmbient.rgb);
                    colour = MixFog(colour, ComputeFogFactor(TransformWorldToHClip(root).z));
                }

                // Unfinished: raw umber underpainting, and bare canvas showing through.
                if (kind != KIND_RESTLESS && kind != KIND_TRAIL)
                {
                    float luma = dot(colour, float3(0.299, 0.587, 0.114));
                    float3 umber = float3(0.32, 0.2, 0.11) * (0.25 + luma * 1.6);
                    float3 canvas = float3(0.55, 0.49, 0.38) * 0.55;
                    colour = lerp(colour, r2 < 0.3 ? canvas : umber, smoothstep(0.15, 0.9, wet) * 0.85);
                }
                // Dread: everything sours toward oxblood.
                float3 blood = float3(0.35, 0.03, 0.02) * (0.3 + dot(colour, float3(0.299, 0.587, 0.114)) * 2.0);
                colour = lerp(colour, blood, dread * 0.18);

                o.color = float4(colour, r1);
                o.info = float4(kind, wet, 0.0, 0.0);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                float r = i.color.a;
                uint kind = (uint)(i.info.x + 0.5);
                float along01 = uv.x * 0.5 + 0.5;
                float rag = (ValueNoise(float2(uv.x * 5.0 + r * 37.0, r * 11.0)) - 0.5) * 0.35;

                float shape;
                if (kind == KIND_GRASS)
                {
                    shape = abs(uv.y) + rag * 0.5;
                }
                else
                {
                    // A fat start where the loaded brush touched down, thinning toward the lift-off.
                    float body = lerp(0.6, 1.0, smoothstep(0.0, 0.2, along01)) * lerp(1.0, 0.7, smoothstep(0.6, 1.0, along01));
                    float across = abs(uv.y) / body + rag;
                    float ends = max(abs(uv.x) - 0.75, 0.0) / 0.25;
                    shape = length(float2(ends, across));
                }
                clip(1.0 - shape);

                // Bristle streaks run the length of the stroke; the brush runs dry toward the end.
                float bristle = ValueNoise(float2(uv.y * 7.0 + r * 19.0, uv.x * 0.7 + r * 3.0));
                if (kind != KIND_GRASS && kind != KIND_SKY)
                    clip(bristle - saturate(along01 - 0.7) * 2.0);

                float3 c = i.color.rgb * (0.8 + 0.38 * bristle) + 0.012 * bristle;
                if (kind == KIND_GRASS)
                    c *= lerp(0.35, 1.05, along01);
                if (kind == KIND_TRAIL || kind == KIND_RESTLESS)
                    c += pow(bristle, 6.0) * 0.1; // wet sheen
                return half4(c, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
