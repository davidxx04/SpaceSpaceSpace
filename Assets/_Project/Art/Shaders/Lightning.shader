// Shader de glow para rayos (lightning). NO genera la forma del rayo: eso lo hace por código el
// componente LightningBurst, que construye la geometría con LineRenderers. Este shader solo da el
// "aspecto eléctrico": usa la coordenada de ANCHURA de la línea (uv.y, de 0 a 1 a lo ancho) para
// pintar un núcleo blanco caliente en el centro que se difumina hacia el color (azulado) en los
// bordes. Blend aditivo -> brilla/bloom sobre fondos oscuros.
//
// _Color lo controla LightningBurst por instancia (MaterialPropertyBlock) para el fundido de vida.
// El color base y el afilado de la punta llegan por el vertex color del LineRenderer.
Shader "SpaceSpaceSpace/Lightning"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Softness ("Edge Softness", Range(0.1, 8)) = 2
        _CoreBoost ("Core Hotness", Range(0.1, 8)) = 3
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha One   // aditivo: glow

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

            fixed4 _Color;
            float _Softness;
            float _CoreBoost;

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
                // uv.y atraviesa la ANCHURA de la línea: 0 y 1 = bordes, 0.5 = centro.
                float edge = abs(IN.texcoord.y * 2.0 - 1.0);     // 0 centro .. 1 borde
                float core = saturate(1.0 - edge);

                float intensity = pow(core, _Softness);          // glow suave a lo ancho
                // Núcleo caliente: hacia el centro tiende a blanco; en bordes, el color base (azulado).
                float3 hot = lerp(IN.color.rgb, float3(1, 1, 1), pow(core, _CoreBoost));

                float a = IN.color.a * intensity;
                return fixed4(hot, a);
            }
            ENDCG
        }
    }
}
