// Shader unlit para las estelas (afterimages): pinta la FORMA del sprite (su alpha) con un
// color plano, en vez de mostrar los colores originales. Así la estela es una silueta sólida
// del color elegido. El color por estela llega por el vertex color del SpriteRenderer
// (es decir, por SpriteRenderer.color), que aquí controla el componente Afterimage.
Shader "SpaceSpaceSpace/AfterimageSolid"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
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
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Silueta sólida: RGB del color elegido; alpha = (alpha del color) * (forma del sprite).
                fixed spriteAlpha = tex2D(_MainTex, IN.texcoord).a;
                return fixed4(IN.color.rgb, IN.color.a * spriteAlpha);
            }
            ENDCG
        }
    }
}
