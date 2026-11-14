// Shared by BrushStroke.shader and BrushStrokeCull.compute. Must match GpuStroke in StrokeGenerator.cs.
#ifndef BRUSH_STROKE_COMMON_INCLUDED
#define BRUSH_STROKE_COMMON_INCLUDED

#define KIND_SURFACE 0
#define KIND_GRASS 1
#define KIND_SKY 2
#define KIND_RESTLESS 3
#define KIND_TRAIL 4

struct Stroke
{
    float3 position;
    uint normal;
    uint tangent;
    uint color;
    uint size;
    uint misc;
};

float _StrokeLodNear;
float _StrokeLodMin;

uint StrokeBone(Stroke s) { return s.misc & 0xFFFF; }
uint StrokeKind(Stroke s) { return (s.misc >> 16) & 0xF; }
float StrokeLift(Stroke s) { return ((s.misc >> 20) & 0xFF) * 0.001; }

float3 UnpackDirection(uint v)
{
    float3 d = float3(v & 1023, (v >> 10) & 1023, (v >> 20) & 1023) / 1023.0 * 2.0 - 1.0;
    return normalize(d + float3(0, 1e-5, 0));
}

float4 UnpackColor(uint v)
{
    return float4(v & 255, (v >> 8) & 255, (v >> 16) & 255, v >> 24) / 255.0;
}

float2 UnpackHalfSize(uint v)
{
    return float2(f16tof32(v & 0xFFFF), f16tof32(v >> 16));
}

uint StrokeHash(uint x)
{
    x ^= x >> 16;
    x *= 0x7feb352dU;
    x ^= x >> 15;
    x *= 0x846ca68bU;
    x ^= x >> 16;
    return x;
}

// A stable random number in 0..1 for stroke `id`; `salt` picks which one.
float StrokeRandom(uint id, uint salt)
{
    return (StrokeHash(id * 0x9E3779B9U + salt * 0x85EBCA6BU) & 0xFFFFFF) / 16777215.0;
}

// Share of strokes kept at distance d: all of them up close, then falling with the square of distance.
float LodFraction(float d)
{
    float f = _StrokeLodNear / max(d, 0.001);
    return clamp(f * f, _StrokeLodMin, 1.0);
}

bool BoneHidden(float4x4 m) { return m._m33 == 0.0; }

#endif
