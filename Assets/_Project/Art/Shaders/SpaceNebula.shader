// Fondo espacial procedural para un quad full-screen (SpriteRenderer, cámara FIJA de arena).
// Todo se genera en el fragment (sin texturas): nebulosa/aurora por fbm con domain-warp que fluye
// lento en una rampa azul->morado->rosa, + 3 capas de estrellas con parpadeo a DISTINTA velocidad
// (el "parallax": como la cámara no se mueve, la profundidad la da el scroll diferencial por capa).
// Animado por _Time.y (coste CPU nulo). Ajusta el feel desde el material SpaceNebula.mat.
//
// El componente SpaceBackground escala el quad a la vista y pasa _Aspect (ancho/alto) por
// MaterialPropertyBlock para que estrellas y ruido no salgan estirados.
Shader "SpaceSpaceSpace/SpaceNebula"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}   // el sprite es blanco; se ignora

        _SkyColor ("Sky (base)", Color) = (0.02, 0.02, 0.05, 1)
        _ColorDeep ("Nebula Deep (azul)", Color) = (0.05, 0.10, 0.35, 1)
        _ColorMid ("Nebula Mid (morado)", Color) = (0.35, 0.12, 0.55, 1)
        _ColorHot ("Nebula Hot (rosa)", Color) = (0.85, 0.25, 0.55, 1)

        _NebulaScale ("Nebula Scale", Float) = 3.0
        _NebulaSpeed ("Nebula Speed", Float) = 0.03
        _NebulaIntensity ("Nebula Intensity", Range(0, 3)) = 0.9
        _NebulaContrast ("Nebula Contrast", Range(0.5, 4)) = 1.6
        _WarpStrength ("Nebula Warp", Range(0, 2)) = 0.6
        _ColorCycleSpeed ("Nebula Color Cycle Speed", Float) = 0.03
        _ColorSpread ("Nebula Color Spread", Float) = 0.6

        _StarDensity ("Star Grid Density", Float) = 12.0
        _StarAmount ("Star Amount (0..1)", Range(0, 0.5)) = 0.08
        _StarBrightness ("Star Brightness", Range(0, 3)) = 1.0
        _StarSpeed ("Star Scroll Speed", Float) = 0.2
        _TwinkleSpeed ("Star Twinkle Speed", Float) = 2.0
        _TwinkleAmount ("Star Twinkle Amount", Range(0, 1)) = 0.25

        _Aspect ("Aspect (auto)", Float) = 1.3333
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
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _SkyColor, _ColorDeep, _ColorMid, _ColorHot;
            float _NebulaScale, _NebulaSpeed, _NebulaIntensity, _NebulaContrast, _WarpStrength;
            float _ColorCycleSpeed, _ColorSpread;
            float _StarDensity, _StarAmount, _StarBrightness, _StarSpeed, _TwinkleSpeed, _TwinkleAmount;
            float _Aspect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color;
                return OUT;
            }

            // --- Ruido por hash (sin texturas) ---
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);            // suavizado
                float a = hash21(i + float2(0.0, 0.0));
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    v += amp * valueNoise(p);
                    p *= 2.0;
                    amp *= 0.5;
                }
                return v;
            }

            // Rampa CÍCLICA de color: t (0..1) -> deep(azul) -> mid(morado) -> hot(rosa) -> deep.
            // El tramo 3 cierra el bucle (hot->deep) para que el vira de color no dé saltos.
            float3 ramp3(float t)
            {
                t = frac(t) * 3.0;
                float3 a, b;
                float f;
                if (t < 1.0)      { a = _ColorDeep.rgb; b = _ColorMid.rgb;  f = t; }
                else if (t < 2.0) { a = _ColorMid.rgb;  b = _ColorHot.rgb;  f = t - 1.0; }
                else              { a = _ColorHot.rgb;  b = _ColorDeep.rgb; f = t - 2.0; }
                return lerp(a, b, smoothstep(0.0, 1.0, f));
            }

            // Una capa de estrellas: rejilla con desplazamiento propio -> parallax por velocidad.
            float starLayer(float2 uv, float density, float speed, float size)
            {
                float2 g = uv * density + float2(1.0, 0.0) * (speed * _Time.y);   // scroll horizontal: derecha -> izquierda
                float2 cell = floor(g);
                float2 f = frac(g) - 0.5;

                float rnd = hash21(cell);
                float present = step(1.0 - _StarAmount, rnd);              // solo algunas celdas tienen estrella
                float2 off = (float2(hash21(cell + 11.3), hash21(cell + 23.7)) - 0.5) * 0.7;
                float d = length(f - off);

                float star = present * smoothstep(size, 0.0, d);          // punto con borde suave
                float sin01 = 0.5 + 0.5 * sin(_Time.y * _TwinkleSpeed + rnd * 6.2831);
                float tw = 1.0 - _TwinkleAmount + _TwinkleAmount * sin01;  // parpadeo acotado (sutil)
                float bright = lerp(0.5, 1.0, hash21(cell + 51.7));       // brillo variado
                return star * tw * bright;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // uv centrada + corrección de aspecto (celdas/ruido cuadrados, no estirados)
                float2 uv = IN.texcoord;
                uv.x *= _Aspect;

                // --- Nebulosa: fbm con domain warp, deriva diagonal lenta ---
                float2 drift = _Time.y * _NebulaSpeed * float2(0.6, 0.35);
                float2 uvn = uv * _NebulaScale + drift;
                float2 q = float2(fbm(uvn), fbm(uvn + float2(5.2, 1.3)));
                float n = fbm(uvn + _WarpStrength * q);
                n = saturate(pow(n, _NebulaContrast));

                // máscara de baja frecuencia -> la nebulosa queda en manchas, no full-screen
                float mask = fbm(uv * (_NebulaScale * 0.35) + drift * 0.3);
                mask = smoothstep(0.35, 0.95, mask);
                n *= mask;

                // Color DESACOPLADO de la densidad: degradado espacial de baja frecuencia + deriva
                // temporal MUY lenta -> vira suave y sutil entre azul/morado/rosa. La forma la da n.
                float cidx = fbm(uv * _ColorSpread + drift * 0.5) + _Time.y * _ColorCycleSpeed;
                float3 neb = ramp3(cidx) * (n * _NebulaIntensity);

                // --- Estrellas: 3 capas (lejos->cerca) a distinta densidad/velocidad/tamaño ---
                float stars = 0.0;
                stars += starLayer(uv, _StarDensity * 1.00, _StarSpeed * 0.35, 0.05); // lejanas: densas, lentas, pequeñas
                stars += starLayer(uv, _StarDensity * 0.60, _StarSpeed * 0.70, 0.07); // medias
                stars += starLayer(uv, _StarDensity * 0.35, _StarSpeed * 1.30, 0.09); // cercanas: escasas, rápidas, grandes
                stars *= _StarBrightness;

                float3 col = _SkyColor.rgb + neb + stars;
                return fixed4(col * IN.color.rgb, IN.color.a);
            }
            ENDCG
        }
    }
}
