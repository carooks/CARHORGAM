// The canvas of the haunted portrait in the Foreman's office, painted procedurally so it needs no
// texture file. A dark oil portrait of a tall figure whose face has been smeared away.
//   _Wake  0..1  rises as the player comes close: the smear moves, two pale eyes open in it.
//   _Empty 0..1  the figure has stepped out of the frame (after the player enters the painting).
//   _Burnt 0..1  the entity is banished: the canvas is charred and the figure gone.
Shader "HorrorGame/HauntedPortrait"
{
    Properties
    {
        _Wake ("Wake", Range(0, 1)) = 0
        _Empty ("Empty", Range(0, 1)) = 0
        _Burnt ("Burnt", Range(0, 1)) = 0
        _Brightness ("Brightness", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Wake;
                float _Empty;
                float _Burnt;
                float _Brightness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fog : TEXCOORD1; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), f.x),
                            lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), f.x), f.y);
            }

            float Ellipse(float2 p, float2 c, float2 r) { return length((p - c) / r); }

            float4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                float t = _Time.y;
                // Brush strokes: streaky noise along a slight diagonal.
                float2 sp = float2(uv.x * 40.0 + uv.y * 8.0, uv.y * 7.0);
                float strokes = Noise(sp) * 0.6 + Noise(sp * 2.3) * 0.4;

                // Background: dusk ochre at the top fading to near black, with a red glow low left (the fire).
                float3 col = lerp(float3(0.05, 0.035, 0.03), float3(0.38, 0.26, 0.12), smoothstep(0.2, 1.0, uv.y));
                col += float3(0.4, 0.08, 0.02) * smoothstep(0.45, 0.0, length(uv - float2(0.15, 0.25))) * 0.8;
                col *= 0.75 + strokes * 0.45;

                // The figure: shoulders, long body, a head. Fades out when it has left the frame.
                float body = 1.0 - smoothstep(0.95, 1.05, Ellipse(uv, float2(0.5, 0.25), float2(0.3, 0.42)));
                float head = 1.0 - smoothstep(0.95, 1.05, Ellipse(uv, float2(0.5, 0.7), float2(0.11, 0.14)));
                float figure = saturate(body + head) * (1.0 - _Empty);
                float3 coat = float3(0.03, 0.03, 0.035) * (0.7 + strokes * 0.6);
                col = lerp(col, coat, figure);

                // The face is smeared away: pale flesh dragged sideways by a thumb. It moves when awake.
                float2 fp = (uv - float2(0.5, 0.7)) / float2(0.11, 0.14);
                float drag = Noise(float2(fp.y * 6.0, t * 0.6 * _Wake)) * 0.6;
                float smear = (1.0 - smoothstep(0.6, 1.0, length(float2(fp.x - drag * 0.5, fp.y * 1.4))))
                              * (0.6 + 0.4 * Noise(float2(fp.x * 3.0 - t * 0.3 * _Wake, fp.y * 14.0)));
                col = lerp(col, float3(0.62, 0.52, 0.42) * (0.6 + strokes * 0.5), smear * (1.0 - _Empty) * 0.85);

                // Eyes open in the smear as the player comes close.
                float eyes = 0.0;
                eyes += 1.0 - smoothstep(0.08, 0.16, length((fp - float2(-0.35, 0.15)) * float2(1.0, 2.2)));
                eyes += 1.0 - smoothstep(0.08, 0.16, length((fp - float2(0.35, 0.18)) * float2(1.0, 2.2)));
                col = lerp(col, float3(0.02, 0.0, 0.0), eyes * smoothstep(0.2, 0.9, _Wake) * (1.0 - _Empty));

                // Burnt: blackened from the edges in, glowing embers in the cracks.
                float edge = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
                float char = smoothstep(0.0, 0.25, _Burnt * 1.3 - edge + (Noise(uv * 12.0) - 0.5) * 0.3);
                float ember = smoothstep(0.75, 0.95, Noise(uv * 30.0 + t * 0.3)) * char * (1.0 - _Burnt * 0.6);
                col = lerp(col, float3(0.015, 0.012, 0.01), char);
                col += float3(1.0, 0.25, 0.05) * ember * 2.0;

                col *= _Brightness;
                col = MixFog(col, i.fog);
                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
