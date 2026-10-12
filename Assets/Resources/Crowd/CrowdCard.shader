// The far fans' flat cards (Crowd.cs, when Crowd.FarCards is on: the crowd's first cut if it goes over 2 ms GPU). Each far
// fan is one quad standing on its seat and turned round the vertical to face the camera, showing the far body's card
// picture (_Cards: R8, 0 empty, else region + 1; seated on the left half, standing on the right) painted in the fan's own
// colours. A zone playing stand-cheer shows the standing picture, bouncing a little; every other clip the seated one.
// Same per-fan buffer, zones and colours as the bodies (CrowdFanData.hlsl). Casts no shadow.
Shader "Hockey Game/Crowd Card"
{
    Properties
    {
        _Light("Stands light", Float) = 0.28
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
        #include "UnityIndirect.cginc"
        #include "CrowdFanData.hlsl"

        TEXTURE2D(_Cards);
        float4 _CardSize;      // x, y: the card in metres; z, w: one picture in pixels
        float _CardSeatedTop;  // how much of the card's height the seated picture fills: a seated card is cut to it
        float4 _ClipInfo[5];   // per CrowdClip: first row, frames, frames per second, loops (the stand-cheer's length)

        struct Attributes
        {
            float3 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            uint instanceID : SV_InstanceID;
        };

        struct Card
        {
            float4 positionCS;
            float3 normalWS;
            float3 colourSkin, colourShirt, colourScalp, colourPants, colourShoes; // the fan's colours
            float dim;                                    // like the stands and by the zone's light
            float contrast;                               // the zone's
            float2 pixel;                                 // in _Cards
        };

        Card Place(Attributes v)
        {
            Card o;
            Fan f = FanOf(v.instanceID);
            uint zone = FanZone(f);
            float4 z = _Zones[zone];
            float blend = _ZoneBlend[zone];
            float standing = ((uint)z.x == CLIP_STAND_CHEER ? blend : 0) + ((uint)z.z == CLIP_STAND_CHEER ? 1 - blend : 0);
            uint frame = standing > 0.5 ? 1 : 0;
            float4 cheer = _ClipInfo[CLIP_STAND_CHEER];
            float clock = (uint)z.x == CLIP_STAND_CHEER ? z.y : z.w;
            float bounce = frame * 0.05 * max(0, sin(TWO_PI * 2 * (clock * cheer.z / cheer.y + f.shirt.w)));

            float3 seat = mul(_CrowdToWorld, float4(f.place.xyz, 1)).xyz;
            float3 toCamera = _WorldSpaceCameraPos - seat;
            toCamera.y = 0;
            toCamera = normalize(toCamera + float3(1e-5, 0, 0));
            float3 right = float3(-toCamera.z, 0, toCamera.x);
            float up = v.uv.y * (frame == 1 ? 1 : _CardSeatedTop);  // 0..1 of the card's full height
            float3 positionWS = seat + right * (v.uv.x - 0.5) * _CardSize.x * f.hair.w
                              + float3(0, up * _CardSize.y * f.hair.w + bounce, 0);
            o.positionCS = TransformWorldToHClip(positionWS);
            o.normalWS = toCamera;
            o.colourSkin = f.skin.rgb;
            o.colourShirt = f.shirt.rgb;
            o.colourPants = f.pants.rgb;
            o.colourShoes = f.shoes.rgb;
            o.colourScalp = (uint)(f.look.y + 0.5) == HAIR_BALD ? f.skin.rgb : f.hair.rgb;
            o.dim = _Light * _ZoneLight[zone];
            o.contrast = _ZoneContrast[zone];
            o.pixel = float2((v.uv.x + frame) * _CardSize.z, up * _CardSize.w);
            return o;
        }

        // The card's colour at pixel (alpha 0 where it is empty).
        float4 Paint(float2 pixel, float3 skin, float3 shirt, float3 scalp, float3 pants, float3 shoes)
        {
            uint code = (uint)(LOAD_TEXTURE2D_LOD(_Cards, uint2(min(pixel, float2(_CardSize.z * 2, _CardSize.w) - 0.5)), 0).r * 255 + 0.5);
            if (code == 0) return 0;
            uint region = code - 1;
            float3 c = region == 0 ? skin : region == 1 ? shirt : region == 2 ? pants : region == 3 ? shoes : scalp;
            return float4(c, 1);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
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
                float3 skin : TEXCOORD1;
                float3 shirt : TEXCOORD2;
                float3 scalp : TEXCOORD3;
                float2 pixel : TEXCOORD4;
                float fog : TEXCOORD5;
                float dim : TEXCOORD6;
                float contrast : TEXCOORD7;
                float3 pants : TEXCOORD8;
                float3 shoes : TEXCOORD9;
            };

            Varyings Vert(Attributes v)
            {
                Card c = Place(v);
                Varyings o;
                o.positionCS = c.positionCS;
                o.normalWS = c.normalWS;
                o.skin = c.colourSkin; o.shirt = c.colourShirt; o.scalp = c.colourScalp; o.pants = c.colourPants; o.shoes = c.colourShoes;
                o.pixel = c.pixel;
                o.fog = ComputeFogFactor(c.positionCS.z);
                o.dim = c.dim;
                o.contrast = c.contrast;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float4 c = Paint(i.pixel, i.skin, i.shirt, i.scalp, i.pants, i.shoes);
                clip(c.a - 0.5);
                Light main = GetMainLight();
                float3 lit = SampleSH(i.normalWS) + main.color * saturate(dot(i.normalWS, main.direction)) * 0.6;
                float3 even = SampleSH(float3(0, 1, 0)) + main.color * 0.3;
                float3 colour = lerp(_FlatTone.rgb, c.rgb, i.contrast) * i.dim * lerp(even, lit, i.contrast);
                return half4(MixFog(colour, i.fog), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Off
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            struct Varyings { float4 positionCS : SV_POSITION; float2 pixel : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Card c = Place(v);
                Varyings o;
                o.positionCS = c.positionCS;
                o.pixel = c.pixel;
                return o;
            }

            half Frag(Varyings i) : SV_Target
            {
                clip(Paint(i.pixel, 0, 0, 0, 0, 0).a - 0.5);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float2 pixel : TEXCOORD1; };

            Varyings Vert(Attributes v)
            {
                Card c = Place(v);
                Varyings o;
                o.positionCS = c.positionCS;
                o.normalWS = c.normalWS;
                o.pixel = c.pixel;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                clip(Paint(i.pixel, 0, 0, 0, 0, 0).a - 0.5);
                return DepthNormalsOutput(i.normalWS);
            }
            ENDHLSL
        }
    }
}
