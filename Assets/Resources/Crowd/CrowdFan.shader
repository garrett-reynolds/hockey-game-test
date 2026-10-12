// The 3D crowd's fans (Crowd.cs): one static body mesh drawn once per fan (Graphics.RenderMeshPrimitives, SV_InstanceID
// picks the fan). The vertex shader poses each vertex from the clip's vertex animation texture (_Vat: column = vertex
// number from uv0.x, row = frame), blending the two nearest frames, at its zone's clip clock plus the fan's own phase, then
// turns, sizes and seats the fan from the per-fan buffer. Each body region (uv0.y) takes the fan's shirt, skin or hair
// colour, or the dark pants and shoes. Lit simply by the main light and the ambient light, then dimmed like the stands and
// by the zone's light (the Calm Area's still fans are dim). Casts no shadow. Region numbers: CrowdBody.Region.
Shader "Hockey Game/Crowd Fan"
{
    Properties
    {
        _Light("Stands light", Float) = 0.28
        _Pants("Pants", Color) = (0.035, 0.035, 0.045, 1)
        _Shoes("Shoes", Color) = (0.02, 0.02, 0.025, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        struct Fan
        {
            float4 place; // xyz seat (crowd space), w yaw
            float4 shirt; // rgb, w phase
            float4 skin;  // rgb, w zone
            float4 hair;  // rgb, w scale
        };
        StructuredBuffer<Fan> _Fans;
        TEXTURE2D(_Vat);
        float4x4 _CrowdToWorld;
        float4 _ZoneClock, _ZoneLight;
        float _ClipSeconds, _ClipFrames, _FanCount, _CopyStep;
        float _Light;
        float4 _Pants, _Shoes;

        struct Attributes
        {
            float3 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            uint instanceID : SV_InstanceID;
        };

        // The fan's posed vertex in world space, its normal and its colour (unlit, zone-dimmed).
        void Pose(Attributes v, out float3 positionWS, out float3 normalWS, out float3 colour)
        {
            uint count = max(1u, (uint)_FanCount);
            uint id = v.instanceID % count;
            uint copy = v.instanceID / count; // extra copies (the GPU cost tool), side by side
            Fan f = _Fans[id];
            uint zone = min(2u, (uint)f.skin.w);
            float clock = zone == 0 ? _ZoneClock.x : zone == 1 ? _ZoneClock.y : _ZoneClock.z;
            float light = zone == 0 ? _ZoneLight.x : zone == 1 ? _ZoneLight.y : _ZoneLight.z;

            float t = frac(clock / _ClipSeconds + f.shirt.w) * _ClipFrames;
            uint frames = (uint)_ClipFrames;
            uint f0 = (uint)t % frames, f1 = (f0 + 1) % frames;
            uint vertex = (uint)(v.uv.x + 0.5);
            float3 p = lerp(LOAD_TEXTURE2D_LOD(_Vat, uint2(vertex, f0), 0).xyz,
                            LOAD_TEXTURE2D_LOD(_Vat, uint2(vertex, f1), 0).xyz, frac(t));

            float s, c;
            sincos(f.place.w, s, c);
            float3 turned = float3(p.x * c + p.z * s, p.y, -p.x * s + p.z * c) * f.hair.w;
            float3 n = float3(v.normalOS.x * c + v.normalOS.z * s, v.normalOS.y, -v.normalOS.x * s + v.normalOS.z * c);
            float side = (copy & 1u) ? 1.0 : -1.0;
            float3 seat = f.place.xyz + float3(side * ((copy + 1u) / 2u) * _CopyStep, 0, 0);

            positionWS = mul(_CrowdToWorld, float4(turned + seat, 1)).xyz;
            normalWS = normalize(mul((float3x3)_CrowdToWorld, n));

            uint region = (uint)(v.uv.y + 0.5);
            colour = region == 0 ? f.skin.rgb : region == 1 ? f.shirt.rgb : region == 2 ? _Pants.rgb
                   : region == 3 ? _Shoes.rgb : f.hair.rgb;
            colour *= _Light * light;
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
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 positionWS;
                Pose(v, positionWS, o.normalWS, o.colour);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                Light main = GetMainLight();
                float3 lit = SampleSH(n) + main.color * saturate(dot(n, main.direction));
                return half4(MixFog(i.colour * lit, i.fog), 1);
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
                Pose(v, positionWS, normalWS, colour);
                return TransformWorldToHClip(positionWS);
            }

            half Frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
