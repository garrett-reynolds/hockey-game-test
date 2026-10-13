// LED board (Video Board and Centre Board now, Ribbon Boards in #122): one shader for the screens' pictures and for the board words.
// The words are first written into a small texture with one texel per LED cell (VideoBoards, from the LED font), so a screen shows its
// words the same way as the Replay's picture: as a picture on a grid of square LED cells with a thin dark gap between them (_GapShare
// and _GapColour are set by LedBoardMaterial).
//
// The grid lives on the board's plane, in metres, measured from _GridOrigin (the screen's lower-left corner; LedBoardMaterial.Anchor
// sets it per renderer). Each fragment finds the centre of its cell and reads the picture once there, so a cell is one flat colour.
// The gap is dark. A cell only a few pixels wide would crawl and shimmer, so the gap fades away (to the full cell colour) as the cell
// shrinks on screen.
//
// Words as a picture: the texture is R8 with exactly one texel per cell (0 or 1), point filtered, its edges on the screen's cell grid,
// so the texel read at a cell's centre is that cell. The cell is lit (the words colour _BaseColor) when the texel is at least _LitAbove,
// otherwise _Backdrop. _LitAbove 0 means an ordinary picture: its colour times _BaseColor.
//
// _CellSize <= 0 switches the grid off: the picture is drawn plain. With _FontAtlas on, the material instead draws smooth text from a
// font atlas (the Ribbon Boards until #122). Depth-tested, writes depth, front faces only: a board in front hides the text and it is
// not mirrored from behind (Unity's built-in font material ignores depth and draws both faces).
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
        _LitAbove("Words: a cell is lit when its texel is at least this (0: ordinary picture)", Range(0, 1)) = 0
        _Backdrop("Words: colour of a cell with no words", Color) = (0,0,0,1)
        _FontAtlas("Draw smooth text from a font atlas (1) or a picture (0)", Float) = 0
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            SAMPLER(sampler_LedPointClamp); // an inline sampler state: point filter, clamped (the picture is read once per cell)
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseMap_TexelSize;
                half4 _BaseColor;
                float _CellSize;
                float _GapShare;
                half4 _GapColour;
                float4 _GridOrigin;
                float _LitAbove;
                half4 _Backdrop;
                float _FontAtlas;
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
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
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

            // Running total of lit length along one axis, in cells: each whole cell adds 1 - gap; inside a cell the lit part starts at gap/2.
            float LitLengthTo(float x, float gap)
            {
                float whole = floor(x);
                return whole * (1.0 - gap) + clamp(x - whole - gap * 0.5, 0.0, 1.0 - gap);
            }

            // Share of the pixel's footprint (x +- width/2, in cells) that is lit along one axis: 1 inside a dot, 0 in a gap, between on an edge.
            float DotMask(float x, float width, float gap)
            {
                float w = max(width, 1e-4);
                return (LitLengthTo(x + 0.5 * w, gap) - LitLengthTo(x - 0.5 * w, gap)) / w;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Derivatives first, outside any branch.
                float3 fromOrigin = input.positionWS - _GridOrigin.xyz;
                float2 p = float2(dot(fromOrigin, input.right), dot(fromOrigin, input.up)); // metres on the board plane
                float2 dpdx = ddx(p), dpdy = ddy(p), duvdx = ddx(input.uv), duvdy = ddy(input.uv);
                float2 q = p / max(_CellSize, 1e-5);   // in cells
                float2 pixelsInCells = fwidth(q);

                if (_FontAtlas > 0.5)
                {
                    half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * input.color.a;
                    clip(alpha - 0.01);
                    return half4(input.color.rgb, alpha);
                }
                if (_CellSize <= 0.0)
                    return half4((_BaseColor * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv)).rgb, 1);

                // The cell this fragment is in, and the move (in screen steps a, b) from here to the cell's centre.
                float2 cellId = floor(q);
                float2 cellUV = q - cellId;
                float2 toCentre = (cellId + 0.5) * _CellSize - p;
                float det = dpdx.x * dpdy.y - dpdx.y * dpdy.x;
                float2 steps = 0;
                if (abs(det) > 1e-14)
                    steps = float2(toCentre.x * dpdy.y - toCentre.y * dpdy.x, dpdx.x * toCentre.y - dpdx.y * toCentre.x) / det;
                float2 uvCentre = input.uv + duvdx * steps.x + duvdy * steps.y;

                // Square dot: lit inside the cell with half the gap trimmed from each side. The dot's edge is filtered over the pixel's
                // footprint (a box filter, width fwidth(q) cells), so a gap that falls between pixel centres still darkens the pixels it
                // covers: the frame's average brightness stays steady at every cell size and camera position (a hard step made it jump).
                float lit = DotMask(q.x, pixelsInCells.x, _GapShare) * DotMask(q.y, pixelsInCells.y, _GapShare);
                // A cell under about 2 to 3 pixels is only an average: fade to the full cell colour, so a far board keeps its colours.
                float tooSmall = smoothstep(0.3, 0.5, max(pixelsInCells.x, pixelsInCells.y));
                lit = lerp(lit, 1.0, tooSmall);

                half4 sampled = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_LedPointClamp, saturate(uvCentre), 0);
                half3 picture = _LitAbove > 0.0
                    ? lerp(_Backdrop.rgb, _BaseColor.rgb, step(_LitAbove, sampled.r))
                    : _BaseColor.rgb * sampled.rgb;
                return half4(lerp(_GapColour.rgb, picture, lit), 1);
            }
            ENDHLSL
        }
    }
}
