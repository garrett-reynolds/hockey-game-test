// LED board (Video Board and Centre Board now, Ribbon Boards in #122): one shader for the screens' pictures and for the board
// words, so both sit on the same grid of square LED cells with a thin dark gap between them (_GapShare and _GapColour are set by LedBoardMaterial).
//
// The grid lives on the board's plane, in metres, measured from _GridOrigin (the screen's lower-left corner; LedBoardMaterial.Anchor
// sets it per renderer, so a screen and its two words objects share one grid). Each fragment finds the centre of its cell and
// reads the picture (or the font atlas) once there, so a cell is one flat colour. The gap is dark. A cell only a few pixels wide
// would crawl and shimmer, so the gap fades away (to the full cell colour) as the cell shrinks on screen.
//
// _CellSize <= 0 switches the grid off: the picture is drawn plain and the words as plain smooth text (the Ribbon Boards until #122).
//
// Words mode (_LED_WORDS) is the board words: the font atlas coloured by the TextMesh colour, drawn like a solid surface.
// Unity's built-in font material ignores depth and draws both faces, so words showed through boards and mirrored from behind.
// This shader is depth-tested (a board in front hides it), writes depth and draws front faces only.
Shader "Hockey Game/LED Board"
{
    Properties
    {
        [MainTexture] _BaseMap("Picture or font atlas", 2D) = "white" {}
        [MainColor] _BaseColor("Colour", Color) = (1,1,1,1)
        _CellSize("LED cell size (m)", Float) = 0.1
        _GapShare("Gap share of a cell's side", Range(0, 0.9)) = 0.2
        _GapColour("Gap colour", Color) = (0.02,0.02,0.02,1)
        _GridOrigin("Grid origin (world)", Vector) = (0,0,0,0)
        [HideInInspector] _SrcBlend("Source blend", Float) = 1
        [HideInInspector] _DstBlend("Destination blend", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "LedBoard"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZTest LEqual
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ _LED_WORDS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            SAMPLER(sampler_LedPointClamp); // an inline sampler state: point filter, clamped (the picture is read once per cell)
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _CellSize;
                float _GapShare;
                half4 _GapColour;
                float4 _GridOrigin;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 right : TEXCOORD2;
                float3 up : TEXCOORD3;
                float2 corner : TEXCOORD4;   // words: where this fragment sits in its glyph's quad, 0 to 1 each way
            };

            Varyings Vert(Attributes input, uint vertexId : SV_VertexID)
            {
                Varyings output;
                // TextMesh draws each letter as a quad of 4 vertices in turn round the corners, so the vertex number says which corner.
                uint k = vertexId & 3u;
                output.corner = float2(k == 1u || k == 2u ? 1.0 : 0.0, k >= 2u ? 1.0 : 0.0);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                float4x4 m = GetObjectToWorldMatrix();
                output.right = normalize(float3(m._m00, m._m10, m._m20));
                output.up = normalize(float3(m._m01, m._m11, m._m21));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Derivatives first, outside any branch.
                float3 fromOrigin = input.positionWS - _GridOrigin.xyz;
                float2 p = float2(dot(fromOrigin, input.right), dot(fromOrigin, input.up)); // metres on the board plane
                float2 dpdx = ddx(p), dpdy = ddy(p), duvdx = ddx(input.uv), duvdy = ddy(input.uv);
                float2 dcdx = ddx(input.corner), dcdy = ddy(input.corner);
                float2 q = p / max(_CellSize, 1e-5);   // in cells
                float2 pixelsInCells = fwidth(q);

                if (_CellSize <= 0.0)
                {
                    #ifdef _LED_WORDS
                        half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * input.color.a;
                        clip(alpha - 0.01);
                        return half4(input.color.rgb, alpha);
                    #else
                        return half4((_BaseColor * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv)).rgb, 1);
                    #endif
                }

                // The cell this fragment is in, and the move (in screen steps a, b) from here to the cell's centre.
                float2 cellId = floor(q);
                float2 cellUV = q - cellId;
                float2 toCentre = (cellId + 0.5) * _CellSize - p;
                float det = dpdx.x * dpdy.y - dpdx.y * dpdy.x;
                float2 steps = 0;
                if (abs(det) > 1e-14)
                    steps = float2(toCentre.x * dpdy.y - toCentre.y * dpdy.x, dpdx.x * toCentre.y - dpdx.y * toCentre.x) / det;
                float2 uvCentre = input.uv + duvdx * steps.x + duvdy * steps.y;

                // Square dot: lit inside the cell with half the gap trimmed from each side.
                float2 inside = step(_GapShare * 0.5, cellUV) * step(cellUV, 1.0 - _GapShare * 0.5);
                float lit = inside.x * inside.y;
                // A cell under about 2 to 4 pixels would crawl: fade the gap away to the full cell colour, so a far board keeps its colours.
                float tooSmall = smoothstep(0.25, 0.5, max(pixelsInCells.x, pixelsInCells.y));
                lit = lerp(lit, 1.0, tooSmall);

                #ifdef _LED_WORDS
                    // The cell is lit only where its centre is inside a letter; elsewhere the board behind shows.
                    // A cell on the edge of a letter's quad whose centre is outside the quad would read the neighbouring letter's pixels.
                    float2 cornerCentre = input.corner + dcdx * steps.x + dcdy * steps.y;
                    // Round letters (O, S, 6) are drawn 1 pixel below the baseline and above the cap line, which a cell centre would light as a
                    // stray cell; the quad is also trimmed by 1.5 of its 50 pixels (0.03) top and bottom, so only a real letter row shows.
                    float2 trim = float2(0.0, 0.03);
                    float2 outside = step(cornerCentre, trim) + step(1.0 - trim, cornerCentre);
                    half glyph = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uvCentre, 0).a * input.color.a;
                    clip(glyph - 0.5 - (outside.x + outside.y));
                    return half4(lerp(_GapColour.rgb, input.color.rgb, lit), 1);
                #else
                    half3 picture = _BaseColor.rgb * SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_LedPointClamp, saturate(uvCentre), 0).rgb;
                    return half4(lerp(_GapColour.rgb, picture, lit), 1);
                #endif
            }
            ENDHLSL
        }
    }
}
