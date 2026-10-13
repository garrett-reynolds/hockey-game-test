// The 3D crowd's fans (Crowd.cs): one static body mesh drawn once per fan (Graphics.RenderMeshPrimitives over a slice of
// the per-fan buffer starting at _FanStart; GetIndirectInstanceID picks the fan). The vertex shader poses each vertex from
// the body's vertex animation texture (_Vat: column = vertex number from uv0.x; rows = body shape, then clip, then frame),
// blending the two nearest frames, at its zone's clip clock plus the fan's own phase; while a zone's new clip fades in it
// blends in the last clip too. It then turns, sizes and seats the fan. Each vertex's paint (uv0.y, an index into the outfit's
// _Palette) gives the fan's skin, hair or team top colour, or the outfit's own colour. Lit simply by the main light and the
// ambient light, then dimmed like the stands and by the zone's light (the Calm Area's still fans are dim). Casts no shadow.
// Numbers: CrowdBody.PaintKind, CrowdClip.
Shader "Hockey Game/Crowd Fan"
{
    Properties
    {
        _Light("Stands light", Float) = 0.28
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
        #include "UnityIndirect.cginc"
        #include "CrowdFanData.hlsl"

        TEXTURE2D(_Vat);
        float4 _ClipInfo[8];   // per CrowdClip: first row (within a shape), frames, frames per second, loops
        float _RowsPerShape;

        struct Attributes
        {
            float3 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;  // x vertex number, y paint
            uint instanceID : SV_InstanceID;
        };

        // Where the vertex is at clip's clock for this fan (body space).
        float3 Sample(uint clip, float clock, Fan f, uint vertex)
        {
            clip = FanClip(clip, f); // this fan's own clip (only some wave, some stay seated on a Goal, ...)
            float4 info = _ClipInfo[clip];
            uint frames = (uint)info.y;
            float seconds = info.y / info.z;
            uint f0, f1;
            float w;
            if (info.w > 0.5)
            {
                float t = frac(clock / seconds + f.shirt.w) * info.y;
                f0 = (uint)t % frames; f1 = (f0 + 1) % frames; w = frac(t);
            }
            else // plays once, each fan a little late
            {
                float t = clamp(clock - f.shirt.w * _OhSpread, 0, seconds) * info.z;
                f0 = min((uint)t, frames - 1); f1 = min(f0 + 1, frames - 1); w = frac(t);
            }
            uint row = (uint)(f.look.x + 0.5) * (uint)_RowsPerShape + (uint)info.x;
            return lerp(LOAD_TEXTURE2D_LOD(_Vat, uint2(vertex, row + f0), 0).xyz,
                        LOAD_TEXTURE2D_LOD(_Vat, uint2(vertex, row + f1), 0).xyz, w);
        }

        // The fan's posed vertex in world space, its normal and its colour (unlit, zone-dimmed).
        void Pose(Attributes v, out float3 positionWS, out float3 normalWS, out float3 colour, out uint zone)
        {
            Fan f = FanOf(v.instanceID);
            zone = FanZone(f);
            float4 z = _Zones[zone];
            float blend = _ZoneBlend[zone];
            uint vertex = (uint)(v.uv.x + 0.5);
            float3 p = Sample((uint)z.x, z.y, f, vertex);
            if (blend < 1) p = lerp(Sample((uint)z.z, z.w, f, vertex), p, blend);

            positionWS = FanToWorld(f, p);
            normalWS = FanNormalToWorld(f, v.normalOS);
            colour = FanColour(f, (uint)(v.uv.y + 0.5), zone);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 colour : TEXCOORD1;
                float fog : TEXCOORD2;
                nointerpolation uint zone : TEXCOORD3;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 positionWS;
                Pose(v, positionWS, o.normalWS, o.colour, o.zone);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 n = SafeNormalize(i.normalWS); // a zero normal stays 0 (normalize gives NaN, and a NaN pixel blooms into a white blob)
                Light main = GetMainLight();
                float3 lit = SampleSH(n) + main.color * saturate(dot(n, main.direction));
                float3 even = SampleSH(float3(0, 1, 0)) + main.color * 0.5;
                return half4(MixFog(i.colour * FanLit(lit, even, i.zone), i.fog), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Back
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 Vert(Attributes v) : SV_POSITION
            {
                float3 positionWS, normalWS, colour;
                uint zone;
                Pose(v, positionWS, normalWS, colour, zone);
                return TransformWorldToHClip(positionWS);
            }

            half Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 positionWS, colour;
                uint zone;
                Pose(v, positionWS, o.normalWS, colour, zone);
                o.positionCS = TransformWorldToHClip(positionWS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target { return DepthNormalsOutput(i.normalWS); }
            ENDHLSL
        }
    }
}
