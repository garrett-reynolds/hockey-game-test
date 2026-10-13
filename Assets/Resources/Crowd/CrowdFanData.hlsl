// What the crowd's two shaders (CrowdFan: the bodies; CrowdCard: the far fans' flat cards) share: the per-fan buffer
// (Crowd.cs FanData), each zone's clips (CrowdMotion), and how a fan is placed in the world and coloured.
// Include after URP's Core.hlsl and UnityIndirect.cginc (with UNITY_INDIRECT_DRAW_ARGS defined).
#ifndef CROWD_FAN_DATA_INCLUDED
#define CROWD_FAN_DATA_INCLUDED

#define CLIP_SIT_IDLE 0
#define CLIP_WAVE 1
#define CLIP_STAND_CHEER 2
#define CLIP_OH 3
#define CLIP_CLAP 4
#define CLIP_MISS 5
#define CLIP_STAND_CHEER_RIGHT 6
#define CLIP_OH_BACK 7
#define PAINT_SKIN 0
#define PAINT_TEAM_TOP 1
#define PAINT_HAIR 2
#define PAINT_OWN 3

struct Fan
{
    float4 place; // xyz seat (crowd space), w yaw
    float4 shirt; // the team top: rgb, w phase
    float4 skin;  // rgb, w zone
    float4 hair;  // rgb, w height
    float4 look;  // x body shape, z waves (1) or not (0), w team top (1) or not (0)
    float4 acts;  // the fan's own picks for a Goal and an Oh (not used yet)
};
StructuredBuffer<Fan> _Fans;
float _FanStart;          // this draw's first fan in _Fans
float4x4 _CrowdToWorld;
float4 _Zones[3];         // per ArenaZone: clip, its clock (s), the clip fading out, its clock
float4 _ZoneBlend;        // per ArenaZone: how far the clip has faded in (1 = alone)
float4 _ZoneLight;        // per ArenaZone: brightness (1 = as lit)
float4 _ZoneContrast;     // per ArenaZone: how much of the fans' own colour and shading shows (1 = all)
float4 _FlatTone;         // what low-contrast fans go toward
float4 _Palette[16];      // the outfit's paints (CrowdBody.Palette): linear rgb, w the kind (PAINT_*)
float _Light, _OhSpread;

Fan FanOf(uint svInstanceID)
{
    InitIndirectDrawArgs(0);
    return _Fans[(uint)_FanStart + GetIndirectInstanceID(svInstanceID)];
}

uint FanZone(Fan f) { return min(2u, (uint)(f.skin.w + 0.5)); }

float3 FanTurn(Fan f, float3 p)
{
    float s, c;
    sincos(f.place.w, s, c);
    return float3(p.x * c + p.z * s, p.y, -p.x * s + p.z * c);
}

float3 FanToWorld(Fan f, float3 bodyPoint)
{
    return mul(_CrowdToWorld, float4(FanTurn(f, bodyPoint * f.hair.w) + f.place.xyz, 1)).xyz;
}

float3 FanNormalToWorld(Fan f, float3 normal)
{
    return normalize(mul((float3x3)_CrowdToWorld, FanTurn(f, normal)));
}

// A region colour at the zone's contrast, dimmed like the stands and by the zone's light.
float3 FanTone(float3 colour, uint zone)
{
    return lerp(_FlatTone.rgb, colour, _ZoneContrast[zone]) * _Light * _ZoneLight[zone];
}

// The colour of a palette entry (CrowdBody.Palette) on this fan, before the zone's tone: by the entry's kind, the fan's
// skin, its team top (the home jersey for the fans who wear one, else the entry's own colour), its hair, or the entry's own.
float3 PaintColour(float3 skin, float3 teamTop, float wearsTeamTop, float3 hair, uint paint)
{
    float4 entry = _Palette[paint];
    uint kind = (uint)(entry.w + 0.5);
    return kind == PAINT_SKIN ? skin
         : kind == PAINT_TEAM_TOP ? (wearsTeamTop > 0.5 ? teamTop : entry.rgb)
         : kind == PAINT_HAIR ? hair : entry.rgb;
}

float3 FanPaint(Fan f, uint paint)
{
    return PaintColour(f.skin.rgb, f.shirt.rgb, f.look.w, f.hair.rgb, paint);
}

// The colour of a palette entry on this fan, at its zone's contrast and light.
float3 FanColour(Fan f, uint paint, uint zone)
{
    return FanTone(FanPaint(f, paint), zone);
}

// Lighting at the zone's contrast: low-contrast fans are lit more evenly.
float3 FanLit(float3 lit, float3 even, uint zone)
{
    return lerp(even, lit, _ZoneContrast[zone]);
}

// URP's depth-normals output (as its Lit shader's DepthNormals pass).
half4 DepthNormalsOutput(float3 normalWS)
{
#if defined(_GBUFFER_NORMALS_OCT)
    float3 n = normalize(normalWS);
    float2 octNormalWS = PackNormalOctQuadEncode(n);
    float2 remapped = saturate(octNormalWS * 0.5 + 0.5);
    return half4(PackFloat2To888(remapped), 0.0);
#else
    return half4(NormalizeNormalPerPixel(normalWS), 0.0);
#endif
}
#endif
