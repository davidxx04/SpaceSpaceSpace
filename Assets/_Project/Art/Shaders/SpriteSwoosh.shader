// Creciente / media luna procedural para el cuerpo del Swoosh (la forma de "onda" que pidió el usuario).
// Se dibuja por SDF: zona DENTRO de un círculo exterior y FUERA de un círculo interior desplazado
// (lune). Trabaja en UV [0,1] -> al estirarse a la caja (thickness x width) el creciente encaja en el
// hitbox automáticamente (su eje largo = ancho del swoosh, su bulto = grosor). Se tinta con el color
// de vértice (= SpriteRenderer.color, el color de verbo) como SpriteGlow, así no necesita material por
// instancia (batching-friendly). Transparente normal (no additive): se lee como hoja sólida.
//
// (Para builds: añadir "SpaceSpaceSpace/SpriteSwoosh" a Always Included Shaders, como SpriteGlow.)
Shader "SpaceSpaceSpace/SpriteSwoosh"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _Softness ("Edge Softness", Range(0.002, 0.25)) = 0.04
        _OuterX ("Outer Center X", Float) = 0.0
        _OuterR ("Outer Radius", Float) = 1.0
        _InnerX ("Inner Center X", Float) = -0.55
        _InnerR ("Inner Radius", Float) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

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

            fixed4 _Color;
            float _Softness, _OuterX, _OuterR, _InnerX, _InnerR;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // UV [0,1] -> [-1,1], centrado. El bulto va por X, las puntas por Y.
                float2 p = (i.uv - 0.5) * 2.0;
                float dO = length(p - float2(_OuterX, 0.0)) - _OuterR;   // disco exterior (neg = dentro)
                float dI = length(p - float2(_InnerX, 0.0)) - _InnerR;   // disco interior (el "mordisco")
                float d  = max(dO, -dI);                                 // creciente = dentro de O y fuera de I
                float aa = max(_Softness, 1e-4);
                float mask = 1.0 - smoothstep(-aa, aa, d);

                fixed4 col = i.color;
                col.a *= mask;
                return col;
            }
            ENDCG
        }
    }
}
