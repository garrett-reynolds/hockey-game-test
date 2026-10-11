// The Easy mode glow on the Gaps (GoalieView). Plain unlit colour with fog, like URP Unlit, but its depth test is a
// material setting: GoalieView sets _ZTest to Always before the Shot, so the glow draws over the goalie in its Ready
// Stance (which stands in front of some Gaps, seen from the shooter), and back to LessEqual once the goalie reacts, so the flying puck
// stays in front. Drawn after the other opaque objects and writes no depth.
Shader "Hockey Game/Easy Glow"
{
    Properties
    {
        [MainColor] _BaseColor("Colour", Color) = (0.3, 1, 0.3, 1)
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("Depth Test", Float) = 4
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+450" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Unlit"
            ZTest [_ZTest]
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _ZTest;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float fog : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(MixFog(_BaseColor.rgb, input.fog), 1);
            }
            ENDHLSL
        }
    }
}
