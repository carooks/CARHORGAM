// Full-screen oil-paint effect for the inside of the haunted painting.
// Used by the "PaintedRealm" Full Screen Pass renderer feature (Before Rendering Post Processing).
//
// 1. A rotated, stretched Kuwahara filter flattens the image into daubs of paint that follow a
//    brush direction tied to the world (so strokes stay put when you turn your head).
// 2. How FINISHED each pixel is comes from _PaintAttention (see PaintAttention.cs): a ring of
//    64 values around the viewer. Where you have just turned to, the world is still wet
//    underpainting (big smeared strokes, raw umber, bare canvas, running drips) and paints itself
//    in over about half a second.
// 3. _PaintDread (0..1, how close the entity is) makes the smears heavier, the paint run, and red bleed in.
Shader "HorrorGame/PaintedRealm"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "PaintedRealm"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #define PAINT_BINS 64
            #define KR 4

            float _PaintAttention[PAINT_BINS];
            float3 _PaintCamRight;
            float3 _PaintCamUp;
            float3 _PaintCamForward;
            float2 _PaintTanHalfFov;
            float _PaintDread;
            float _PaintStrength;

            float Hash21(float2 p)
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
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Attention(float yawDegrees)
            {
                float f = frac(yawDegrees / 360.0) * PAINT_BINS;
                uint a = (uint)floor(f) % (uint)PAINT_BINS;
                uint b = (a + 1u) % (uint)PAINT_BINS;
                return lerp(_PaintAttention[a], _PaintAttention[b], frac(f));
            }

            float3 Sample(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, saturate(uv), 0).rgb;
            }

            float Luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 screen = max(_ScreenParams.xy, float2(1, 1));
                float2 texel = 1.0 / screen;
                // Stroke sizes are authored for 1080p and scale with resolution.
                float pxScale = screen.y / 1080.0;
                float t = _Time.y;
                float dread = saturate(_PaintDread);

                // World direction of this pixel, and how finished the paint is that way.
                float2 ndc = uv * 2.0 - 1.0;
                float3 dir = normalize(_PaintCamForward + _PaintCamRight * ndc.x * _PaintTanHalfFov.x
                                                        + _PaintCamUp * ndc.y * _PaintTanHalfFov.y);
                float yaw = degrees(atan2(dir.x, dir.z));
                float pitch = degrees(asin(clamp(dir.y, -1.0, 1.0)));
                float2 worldCoord = float2(yaw, pitch);
                // Ragged edge between finished and wet paint, so it is never a clean line.
                float edgeNoise = ValueNoise(worldCoord * 0.35 + t * 0.15) - 0.5;
                float finished = saturate(Attention(yaw + edgeNoise * 10.0) * 1.15 + edgeNoise * 0.25);
                float wet = 1.0 - finished;

                // Wet paint runs downward in uneven columns. Dread makes everything run.
                float column = floor(uv.x * screen.x / (6.0 * pxScale));
                float dripSpeed = 0.02 + Hash21(float2(column, 7.0)) * 0.08;
                float drip = frac(Hash21(float2(column, 3.0)) + t * dripSpeed);
                // The brushstrokes do their own running now; this is only a faint extra.
                float run = (wet * 0.012 + dread * 0.012) * drip * Hash21(float2(column, 11.0));
                // The whole canvas breathes slightly, like wet oil under a lamp.
                float2 wobble = float2(ValueNoise(uv * 3.0 + t * 0.2), ValueNoise(uv * 3.0 - t * 0.17)) - 0.5;
                float2 baseUv = uv + float2(0.0, run) + wobble * (0.002 + 0.006 * dread + 0.004 * wet);

                // Brush direction: follows the world, gently curling.
                float angle = ValueNoise(worldCoord * 0.08) * 6.2832 + ValueNoise(worldCoord * 0.3) * 1.2;
                float2 along = float2(cos(angle), sin(angle));
                float2 across = float2(-along.y, along.x);
                float stretch = lerp(2.0, 3.4, wet);
                // Even finished paint is thick, visible strokes; wet paint is smeared much further.
                float stepPx = lerp(1.7, 2.9, saturate(wet + dread * 0.5)) * pxScale;
                float2 stepAlong = along * stepPx * stretch * texel;
                float2 stepAcross = across * (stepPx / stretch) * texel;

                // Kuwahara: average and variance of four overlapping quadrants; keep the calmest.
                float3 m0 = 0, m1 = 0, m2 = 0, m3 = 0;
                float3 s0 = 0, s1 = 0, s2 = 0, s3 = 0;
                [unroll] for (int j = -KR; j <= KR; j++)
                {
                    [unroll] for (int i = -KR; i <= KR; i++)
                    {
                        float3 c = Sample(baseUv + stepAlong * i + stepAcross * j);
                        float3 c2 = c * c;
                        if (i <= 0 && j <= 0) { m0 += c; s0 += c2; }
                        if (i >= 0 && j <= 0) { m1 += c; s1 += c2; }
                        if (i <= 0 && j >= 0) { m2 += c; s2 += c2; }
                        if (i >= 0 && j >= 0) { m3 += c; s3 += c2; }
                    }
                }
                const float n = (KR + 1) * (KR + 1);
                m0 /= n; m1 /= n; m2 /= n; m3 /= n;
                float v0 = dot(abs(s0 / n - m0 * m0), float3(1, 1, 1));
                float v1 = dot(abs(s1 / n - m1 * m1), float3(1, 1, 1));
                float v2 = dot(abs(s2 / n - m2 * m2), float3(1, 1, 1));
                float v3 = dot(abs(s3 / n - m3 * m3), float3(1, 1, 1));
                // Soft pick (weights fall off steeply with variance) avoids blocky seams.
                float w0 = 1.0 / (1e-5 + v0 * v0), w1 = 1.0 / (1e-5 + v1 * v1);
                float w2 = 1.0 / (1e-5 + v2 * v2), w3 = 1.0 / (1e-5 + v3 * v3);
                float3 paint = (m0 * w0 + m1 * w1 + m2 * w2 + m3 * w3) / (w0 + w1 + w2 + w3);

                // Impasto: ridges across each stroke catch the light.
                float2 px = uv * screen / pxScale;
                float ridge = sin(dot(px, across) * 0.8 + ValueNoise(px * 0.05) * 9.0);
                paint *= 1.0 + ridge * lerp(0.06, 0.1, wet);

                // Unfinished paint: a raw umber underpainting with bare canvas showing through.
                float3 umber = float3(0.32, 0.2, 0.11) * (0.25 + Luma(paint) * 1.6);
                float3 canvas = float3(0.55, 0.49, 0.38);
                float bare = smoothstep(0.55, 0.75, ValueNoise(worldCoord * 0.6) + wet * 0.35) * smoothstep(0.35, 0.9, wet);
                float3 under = lerp(umber, canvas * (0.35 + 0.4 * ValueNoise(px * 0.3)), bare * 0.7);
                paint = lerp(paint, under, smoothstep(0.1, 0.85, wet));

                // Dread: colours sour toward oxblood and the edges of the view close in.
                float vig = saturate(length(ndc * float2(0.85, 1.0)));
                float3 blood = float3(0.35, 0.03, 0.02) * (0.3 + Luma(paint) * 2.0);
                paint = lerp(paint, blood, dread * (0.25 + vig * 0.6));
                paint *= 1.0 - dread * smoothstep(0.4, 1.1, vig) * 0.8;

                // The canvas weave you are looking through.
                float weave = sin(px.x * 2.1) * sin(px.y * 2.1);
                paint *= 1.0 + weave * 0.025;

                float3 original = Sample(uv);
                return float4(lerp(original, paint, saturate(_PaintStrength)), 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
