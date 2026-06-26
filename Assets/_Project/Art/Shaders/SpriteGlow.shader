// Glow radial procedural para halos de color (código de color del boss: rojo=esquiva / cian=parry).
// Unlit + additive: no necesita textura ni Bloom (aunque luce más con Bloom). El color sale del
// color de vértice (= SpriteRenderer.color), así cada instancia se tinta sin material por instancia.
Shader "SpaceSpaceSpace/SpriteGlow"
{
    Properties
    {
        _Power ("Falloff Power", Range(0.1, 8)) = 2
        _Intensity ("Intensity", Range(0, 4)) = 1.2
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
        Blend SrcAlpha One   // additive

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            float _Power;
            float _Intensity;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Caída radial desde el centro del sprite (UV 0.5,0.5): 0 en el centro, 1 en el borde.
                float dist = saturate(length(i.uv - 0.5) * 2.0);
                float glow = pow(saturate(1.0 - dist), _Power) * _Intensity;

                fixed4 col;
                col.rgb = i.color.rgb;          // tinte del verbo (rojo/cian)
                col.a = glow * i.color.a;        // additive: rgb*a se suma al fondo
                return col;
            }
            ENDCG
        }
    }
}
