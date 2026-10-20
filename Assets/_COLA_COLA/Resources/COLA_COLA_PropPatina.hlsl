#ifndef COLA_COLA_PROP_PATINA_INCLUDED
#define COLA_COLA_PROP_PATINA_INCLUDED

TEXTURE2D(_PatinaMap);
SAMPLER(sampler_PatinaMap);
TEXTURE2D(_SurfaceAtlas);
SAMPLER(sampler_SurfaceAtlas);
float4 _SurfaceAtlas_TexelSize;

// Item OBJ UVs identify a color cell in the original 8 x 4 palette. Keep the
// palette and printed shapes intact while choosing a real material per face.
float ColaSurfacePaletteIndex(float2 uv)
{
    return floor(saturate(uv.x) * 8) + floor(saturate(1 - uv.y) * 4) * 8;
}

float ColaSurfacePaletteKind(float index)
{
    if (index == 1 || index == 2 || index == 14) return 2; // Paper / kraft.
    if (index == 10 || index == 11 || index == 12 || index == 23) return 1;
    if (index == 13) return 3;
    if (index == 15 || index == 16 || index == 17 || index == 19) return 7;
    if (index == 22) return 5;
    if (index == 7 || index == 8 || index == 9 || index == 20 || index == 21) return 4;
    return 0;
}

half3 ColaPhotographicSurface(half3 baseColor, float2 meters, float kind, float strength, float density)
{
    float tile = clamp(floor(kind + .5), 0, 7);
    float2 tilePixels = max(_SurfaceAtlas_TexelSize.zw / float2(4,2), 32);
    float2 repeat = meters * density / tilePixels;
    // Compute LOD before wrapping, then keep an eight-texel inset at every tile.
    // The two-level cap prevents distant mipmaps mixing wood into paper, etc.
    float2 dx = ddx(repeat) * tilePixels, dy = ddy(repeat) * tilePixels;
    float lod = clamp(.5 * log2(max(max(dot(dx,dx), dot(dy,dy)), .0001)), 0, 2);
    float2 inside = (frac(repeat) * (tilePixels - 16) + 8) / tilePixels;
    float2 uv = (inside + float2(fmod(tile,4), 1 - floor(tile/4))) / float2(4,2);
    half3 scan = SAMPLE_TEXTURE2D_LOD(_SurfaceAtlas, sampler_SurfaceAtlas, uv, lod).rgb;
    // Normalize the clean material's average color, keeping natural fibers,
    // molded grain and food pores without adding dirt or aged color casts.
    half3 reference = half3(.54,.56,.50);
    if (tile == 1) reference = half3(.30,.30,.30);
    else if (tile == 2) reference = half3(.78,.69,.53);
    else if (tile == 3) reference = half3(.29,.12,.045);
    else if (tile == 4) reference = half3(.27,.28,.30);
    else if (tile == 5) reference = half3(.032,.034,.037);
    else if (tile == 6) reference = half3(.41,.35,.26);
    else if (tile == 7) reference = half3(.64,.31,.10);
    half3 photographic = baseColor * clamp(scan / reference, .18, 2.4);
    return lerp(baseColor, min(photographic, .92h), strength);
}

// kind: 0 enamel/mixed palette, 1 metal, 2 paper, 3 wood, 4 plastic, 5 rubber, 6 cloth.
half3 ColaPatinaColor(half3 baseColor, float2 meters, float kind, float strength, float density)
{
    float2 p = meters * density;
    float2 cell = floor(p);
    float fine = 1 - smoothstep(1.2, 3.5, max(length(ddx(p)), length(ddy(p))));
    // Legacy surfaces and generated uniforms retain only a tiny neutral grain.
    // No stains, exposed undercoat, scratches, brown tint or oxidized patches.
    float grain = SAMPLE_TEXTURE2D(_PatinaMap, sampler_PatinaMap, p / 128).r;
    half variation = 1 + clamp(grain - .47, -.2, .2) * strength * .12 * fine;
    if (kind > 5.5)
        variation += (fmod(abs(cell.x) + abs(cell.y), 2) - .5) * strength * fine * .025;
    return max(0, baseColor * variation);
}

float2 ColaPatinaSurfaceMeters(float3 positionWS, half3 normalWS)
{
    float3 local = TransformWorldToObject(positionWS);
    float3 scale = float3(length(GetObjectToWorldMatrix()._m00_m10_m20),
        length(GetObjectToWorldMatrix()._m01_m11_m21), length(GetObjectToWorldMatrix()._m02_m12_m22));
    local *= scale;
    half3 normal = abs(mul(normalWS, (float3x3)GetObjectToWorldMatrix()));
    if (normal.y >= normal.x && normal.y >= normal.z) return local.xz;
    return normal.x > normal.z ? local.zy : local.xy;
}
#endif
