Shader "AutoChess/SpriteOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0,1,0,1)
        _OutlineSize ("Outline Size", Range(0,4)) = 1.5
        _AlphaThreshold ("Alpha Threshold", Range(0,1)) = 0.05
        _SpriteUVMinMax ("Sprite UV Min Max", Vector) = (0,0,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OutlineSize;
            float _AlphaThreshold;
            float4 _SpriteUVMinMax;

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            float SampleSpriteAlpha(float2 uv)
            {
                if (uv.x < _SpriteUVMinMax.x || uv.x > _SpriteUVMinMax.z ||
                    uv.y < _SpriteUVMinMax.y || uv.y > _SpriteUVMinMax.w)
                {
                    return 0.0;
                }

                return tex2D(_MainTex, uv).a;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 spriteColor = tex2D(_MainTex, input.texcoord) * input.color;
                if (spriteColor.a > _AlphaThreshold)
                    return spriteColor;

                float2 step = _MainTex_TexelSize.xy * _OutlineSize;
                float neighborAlpha = 0.0;
                neighborAlpha = max(neighborAlpha, SampleSpriteAlpha(input.texcoord + float2(-step.x, -step.y)));
                neighborAlpha = max(neighborAlpha, SampleSpriteAlpha(input.texcoord + float2(0.0, -step.y)));
                neighborAlpha = max(neighborAlpha, SampleSpriteAlpha(input.texcoord + float2(step.x, -step.y)));
                neighborAlpha = max(neighborAlpha, SampleSpriteAlpha(input.texcoord + float2(-step.x, 0.0)));
                neighborAlpha = max(neighborAlpha, SampleSpriteAlpha(input.texcoord + float2(step.x, 0.0)));
                neighborAlpha = max(neighborAlpha, SampleSpriteAlpha(input.texcoord + float2(-step.x, step.y)));
                neighborAlpha = max(neighborAlpha, SampleSpriteAlpha(input.texcoord + float2(0.0, step.y)));
                neighborAlpha = max(neighborAlpha, SampleSpriteAlpha(input.texcoord + float2(step.x, step.y)));

                if (neighborAlpha <= _AlphaThreshold)
                    return fixed4(0, 0, 0, 0);

                fixed4 outline = _OutlineColor;
                outline.a *= input.color.a;
                return outline;
            }
            ENDCG
        }
    }
}
