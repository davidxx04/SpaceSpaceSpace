// Fondo del MENÚ: espacio + formas "térmicas" (tendencia heatmapping/Y2K) + acabado CRT.
// Mismo esqueleto que SpaceNebula (quad full-screen vía SpaceBackground, todo procedural en el
// fragment, animado por _Time, sin texturas). Tres capas:
//
//   1. Cielo casi negro + 2 capas de estrellas lentas (el menú debe estar CALMADO bajo la UI).
//   2. Manchas térmicas: fbm con domain-warp pasado por una RAMPA TÉRMICA (borde rojo -> ámbar ->
//      cian -> núcleo teal oscuro, como una cámara FLIR). El detalle interno re-cruza la rampa y
//      crea motas cálidas dentro del cuerpo teal. Una rotación de tono global MUY lenta hace que
//      todo el conjunto vire de color con el tiempo.
//   3. Destellos en cruz: 2 cruces finas que derivan/rotan lentísimo, pintadas con la MISMA rampa
//      (núcleo teal, flecos amarillo/rojo) para fundirse con las manchas al cruzarse.
//
// Acabado retro-CRT: scanlines sutiles + grano + viñeta. Tunea todo desde MenuThermal.mat.
Shader "SpaceSpaceSpace/ThermalFlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}   // el sprite es blanco; se ignora

        _SkyColor ("Sky (base)", Color) = (0.008, 0.010, 0.022, 1)

        [Header(Rampa termica borde a nucleo)]
        _EdgeColor ("Edge (rojo)", Color) = (0.72, 0.05, 0.05, 1)
        _FringeColor ("Fringe (ambar)", Color) = (1.0, 0.72, 0.10, 1)
        _BodyColor ("Body (cian)", Color) = (0.10, 0.72, 0.66, 1)
        _CoreColor ("Core (teal oscuro)", Color) = (0.02, 0.15, 0.21, 1)
        _HueCycleSpeed ("Hue Cycle Speed (vueltas/s)", Float) = 0.008

        [Header(Manchas)]
        _BlobScale ("Blob Scale", Float) = 1.7
        _BlobSpeed ("Blob Speed", Float) = 0.016
        _BlobWarp ("Blob Warp", Range(0, 2)) = 0.9
        _BlobCoverage ("Blob Coverage (umbral)", Range(0.2, 0.9)) = 0.58
        _BlobSoftness ("Blob Softness (ancho del fleco)", Range(0.02, 0.5)) = 0.22
        _BlobIntensity ("Blob Intensity", Range(0, 2)) = 1.0

        [Header(Destellos en cruz)]
        _FlareIntensity ("Flare Intensity", Range(0, 2)) = 1.0
        _FlareWidth ("Flare Width", Range(0.005, 0.15)) = 0.030
        _FlareLength ("Flare Length", Range(0.05, 1)) = 0.42
        _FlareSpeed ("Flare Drift Speed", Float) = 0.05

        [Header(Estrellas)]
        _StarDensity ("Star Grid Density", Float) = 10.0
        _StarAmount ("Star Amount (0..1)", Range(0, 0.5)) = 0.06
        _StarBrightness ("Star Brightness", Range(0, 3)) = 0.8
        _StarSpeed ("Star Scroll Speed", Float) = 0.05
        _TwinkleSpeed ("Star Twinkle Speed", Float) = 1.5
        _TwinkleAmount ("Star Twinkle Amount", Range(0, 1)) = 0.3

        [Header(Acabado CRT)]
        _ScanlineCount ("Scanline Count", Float) = 220.0
        _ScanlineStrength ("Scanline Strength", Range(0, 0.4)) = 0.07
        _GrainStrength ("Grain Strength", Range(0, 0.2)) = 0.035
        _Vignette ("Vignette", Range(0, 1)) = 0.32

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

            fixed4 _SkyColor, _EdgeColor, _FringeColor, _BodyColor, _CoreColor;
            float _HueCycleSpeed;
            float _BlobScale, _BlobSpeed, _BlobWarp, _BlobCoverage, _BlobSoftness, _BlobIntensity;
            float _FlareIntensity, _FlareWidth, _FlareLength, _FlareSpeed;
            float _StarDensity, _StarAmount, _StarBrightness, _StarSpeed, _TwinkleSpeed, _TwinkleAmount;
            float _ScanlineCount, _ScanlineStrength, _GrainStrength, _Vignette;
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

            // --- Ruido por hash (idéntico a SpaceNebula: sin texturas) ---
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
                float2 u = f * f * (3.0 - 2.0 * f);
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

            // --- Rampa térmica: t = 0 (fuera) -> negro; sube por rojo -> ámbar -> cian -> teal (núcleo).
            // Es el truco de las referencias: una forma suave leída por una paleta no monótona pinta
            // los FLECOS de colores calientes y el interior frío, como una cámara térmica.
            float3 thermalRamp(float t)
            {
                float3 c = float3(0.0, 0.0, 0.0);
                c = lerp(c, _EdgeColor.rgb,   smoothstep(0.02, 0.20, t));
                c = lerp(c, _FringeColor.rgb, smoothstep(0.20, 0.45, t));
                c = lerp(c, _BodyColor.rgb,   smoothstep(0.45, 0.72, t));
                c = lerp(c, _CoreColor.rgb,   smoothstep(0.72, 0.98, t));
                return c;
            }

            // Rotación de tono (Rodrigues alrededor del eje gris): vira TODA la rampa de color a la
            // vez, manteniendo la coherencia térmica (los flecos siempre contrastan con el núcleo).
            float3 hueShift(float3 c, float a)
            {
                const float3 k = float3(0.57735, 0.57735, 0.57735);
                float ca = cos(a);
                return c * ca + cross(k, c) * sin(a) + k * dot(k, c) * (1.0 - ca);
            }

            // --- Campo de manchas: fbm con domain-warp -> máscara suave (el fleco es _BlobSoftness)
            // multiplicada por detalle interno (re-cruza la rampa: motas cálidas dentro del teal).
            float blobField(float2 uv)
            {
                float2 drift = _Time.y * _BlobSpeed * float2(0.5, -0.3);
                float2 p = uv * _BlobScale + drift;
                float2 q = float2(fbm(p), fbm(p + float2(5.2, 1.3)));
                float n = fbm(p + _BlobWarp * q + float2(0.0, _Time.y * _BlobSpeed * 0.7));

                float blob = smoothstep(_BlobCoverage - _BlobSoftness, _BlobCoverage + _BlobSoftness, n);
                float detail = fbm(p * 2.3 - drift);
                return saturate(blob * (0.65 + 0.7 * detail) * _BlobIntensity);
            }

            // --- Un destello en cruz: centro orbitando lento, brazos finos con caída suave a lo
            // largo y a lo ancho. Devuelve 0..1 para leerse con la MISMA rampa térmica.
            float flareField(float2 uv, float seed)
            {
                float t = _Time.y * _FlareSpeed + seed * 37.0;
                float2 c = float2((0.5 + 0.36 * sin(t * 0.31 + seed * 4.7)) * _Aspect,
                                  0.5 + 0.32 * cos(t * 0.23 + seed * 2.9));

                float a = t * 0.15 + seed * 6.2831;      // rotación lentísima de la cruz
                float ca = cos(a), sa = sin(a);
                float2 p = uv - c;
                p = float2(ca * p.x - sa * p.y, sa * p.x + ca * p.y);

                // Brazo vertical (fino en x, largo en y) y horizontal (al revés); caída cuadrática.
                float lenV = saturate(1.0 - abs(p.y) / _FlareLength);
                float lenH = saturate(1.0 - abs(p.x) / _FlareLength);
                float armV = pow(saturate(1.0 - abs(p.x) / _FlareWidth), 2.0) * lenV * lenV;
                float armH = pow(saturate(1.0 - abs(p.y) / _FlareWidth), 2.0) * lenH * lenH;

                return max(armV, armH);
            }

            // --- Una capa de estrellas (igual que SpaceNebula) ---
            float starLayer(float2 uv, float density, float speed, float size)
            {
                float2 g = uv * density + float2(1.0, 0.0) * (speed * _Time.y);
                float2 cell = floor(g);
                float2 f = frac(g) - 0.5;

                float rnd = hash21(cell);
                float present = step(1.0 - _StarAmount, rnd);
                float2 off = (float2(hash21(cell + 11.3), hash21(cell + 23.7)) - 0.5) * 0.7;
                float d = length(f - off);

                float star = present * smoothstep(size, 0.0, d);
                float sin01 = 0.5 + 0.5 * sin(_Time.y * _TwinkleSpeed + rnd * 6.2831);
                float tw = 1.0 - _TwinkleAmount + _TwinkleAmount * sin01;
                float bright = lerp(0.5, 1.0, hash21(cell + 51.7));
                return star * tw * bright;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 rawUv = IN.texcoord;                 // sin corregir: scanlines/viñeta en pantalla
                float2 uv = rawUv;
                uv.x *= _Aspect;                            // corregida: formas y estrellas sin estirar

                // --- Campo térmico: manchas + cruces comparten rampa (se funden al cruzarse) ---
                float field = blobField(uv);
                field = max(field, saturate(flareField(uv, 0.37) * _FlareIntensity));
                field = max(field, saturate(flareField(uv, 0.71) * _FlareIntensity));

                float hue = _Time.y * _HueCycleSpeed * 6.2831;
                float3 thermal = hueShift(thermalRamp(field), hue);

                // --- Estrellas: 2 capas lentas, ocultas tras las formas (son "tinta", no gas) ---
                float stars = 0.0;
                stars += starLayer(uv, _StarDensity, _StarSpeed * 0.5, 0.05);
                stars += starLayer(uv, _StarDensity * 0.5, _StarSpeed, 0.08);
                stars *= _StarBrightness * (1.0 - saturate(field * 1.6));

                float3 col = _SkyColor.rgb + stars + thermal;

                // --- Acabado CRT ---
                float scan = 1.0 - _ScanlineStrength * (0.5 + 0.5 * sin(rawUv.y * _ScanlineCount * 6.2831));
                col *= scan;

                float grain = (hash21(rawUv * 713.7 + frac(_Time.y) * 371.0) - 0.5) * _GrainStrength;
                col += grain;

                float2 vc = rawUv - 0.5;
                float vd = dot(vc, vc) * 4.0;               // 0 centro -> 1 esquina aprox
                col *= 1.0 - _Vignette * smoothstep(0.35, 1.15, vd);

                return fixed4(col * IN.color.rgb, IN.color.a);
            }
            ENDCG
        }
    }
}
