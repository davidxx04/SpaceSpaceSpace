// Hoja "halftone de ruido" para las pantallas de fin (Canvas/UI). Todo procedural, sin texturas:
// una rejilla de cuadraditos con esquinas redondeadas (celdas en px de canvas) cuyo TAMAÑO lo
// modula un campo fbm fluido que deriva lentamente — donde el campo es fuerte el punto se hincha
// y donde es débil desaparece (halftone real: área ∝ tono, por eso el radio va con sqrt(campo)).
// El color del punto sigue al campo por una rampa de 3 paradas (oscuro -> medio -> núcleo blanco
// incandescente, como la referencia granular), y _HaloAmount añade la "nube" suave de color bajo
// los racimos (la referencia clara). La granularidad es _CellSize: ~14 grueso, ~5 finísimo.
//
// Un solo shader = las tres pantallas (Victory/GameOver/Survived) son tres .mat con overrides.
//
// OJO tiempo: _Time del shader se CONGELA con timeScale = 0 (y el end screen vive congelado), así
// que EndScreenSheetFx empuja _UnscaledTime (Time.unscaledTime) cada frame a su copia de material
// y aquí se anima con t = _UnscaledTime + _Time.y (_Time solo aporta la preview en el editor).
// UI-compatible (Stencil + clip rect estándar, mismo boilerplate que NeonPlate/SpectrumSheet).
Shader "SpaceSpaceSpace/HalftoneNoise"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Hoja)]
        _BgColor ("Fondo de la hoja", Color) = (0.02, 0.02, 0.03, 1)
        _SheetOpacity ("Opacidad de la hoja", Range(0, 1)) = 1

        [Header(Rejilla halftone)]
        _CellSize ("Tamano de celda px", Float) = 8
        _MaxDot ("Punto maximo (fraccion de celda)", Range(0, 1)) = 0.92
        _CornerRadius ("Radio de esquina (fraccion)", Range(0, 0.5)) = 0.32
        _DotJitter ("Jitter de tamano por celda", Range(0, 1)) = 0.3

        [Header(Campo de ruido)]
        _FieldScale ("Escala del campo", Float) = 2.5
        _FieldSpeed ("Velocidad de deriva", Float) = 2000.60
        _MorphSpeed ("Velocidad de morph (formas)", Float) = 2000.35
        _WarpAmount ("Warp del dominio", Range(0, 2)) = 0.9
        _Threshold ("Umbral (bajo esto no hay punto)", Range(0, 1)) = 0.42
        _Gain ("Ganancia tras umbral", Float) = 2.4

        [Header(Color del punto)]
        _DotDeep ("Punto zona fria", Color) = (0.05, 0.12, 0.35, 1)
        _DotMid ("Punto zona media", Color) = (0.15, 0.55, 0.95, 1)
        _DotHot ("Punto nucleo caliente", Color) = (0.92, 0.98, 1, 1)
        _HaloAmount ("Halo bajo los racimos", Range(0, 1)) = 0

        [Header(Acabado)]
        _GrainStrength ("Grano", Range(0, 0.35)) = 0.1
        _Vignette ("Vineta", Range(0, 1)) = 2.3

        _Size ("Rect Size px (auto)", Vector) = (800, 600, 0, 0)
        _UnscaledTime ("Unscaled Time (auto)", Float) = 0

        // --- Estándar de UI (para máscaras/stencil) ---
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

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
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color, _BgColor;
            float _SheetOpacity;
            float _CellSize, _MaxDot, _CornerRadius, _DotJitter;
            float _FieldScale, _FieldSpeed, _MorphSpeed, _WarpAmount, _Threshold, _Gain;
            fixed4 _DotDeep, _DotMid, _DotHot;
            float _HaloAmount;
            float _GrainStrength, _Vignette;
            float4 _Size;
            float _UnscaledTime;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            // Mismo hash/ruido que el resto del proyecto (ThermalFlow): sin texturas.
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

            // 3 octavas bastan a este tamaño (mitad de coste que las 4 de ThermalFlow).
            float fbm3(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                [unroll]
                for (int i = 0; i < 3; i++)
                {
                    v += amp * valueNoise(p);
                    p *= 2.0;
                    amp *= 0.5;
                }
                return v;
            }

            // SDF de rectángulo redondeado (px). d < 0 dentro. (Copiado de NeonPlate.)
            float roundedBox(float2 p, float2 halfSize, float r)
            {
                float2 q = abs(p) - (halfSize - r);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            // Campo de calor 0..1 en un punto (px de canvas): fbm con deriva + warp, umbralizado.
            // Se llama con el CENTRO DE CELDA (lectura cuantizada = look halftone) y, si hay halo,
            // también con el px real (lectura suave = nube bajo los racimos).
            // Movimiento en dos capas: la base DERIVA (_FieldSpeed) y los dos campos del warp derivan
            // en direcciones distintas (_MorphSpeed) -> las manchas no solo se trasladan, CAMBIAN de
            // forma continuamente (el "efecto aleatorio" de la referencia).
            float fieldAt(float2 px2, float t)
            {
                float2 q = px2 / max(_Size.y, 1.0) * _FieldScale + t * _FieldSpeed * float2(0.31, -0.17);
                float2 w = float2(fbm3(q + float2(2.7, 9.1) + t * _MorphSpeed * float2(-0.23, 0.11)),
                                  fbm3(q + float2(8.3, 1.9) + t * _MorphSpeed * float2(0.17, 0.29)));
                float n = fbm3(q + _WarpAmount * (w - 0.5));
                return saturate((n - _Threshold) * _Gain);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float t = _UnscaledTime + _Time.y;   // unscaled lo empuja EndScreenSheetFx

                // --- Rejilla en px de canvas: celdas cuadradas, punto centrado en cada celda ---
                float2 pxPos = IN.texcoord * _Size.xy;
                float cellPx = max(_CellSize, 1.0);
                float2 cell = floor(pxPos / cellPx);
                float2 cellCenter = (cell + 0.5) * cellPx;
                float2 local = pxPos - cellCenter;

                // --- Campo leído en el centro de la celda + jitter por celda (rompe la perfección) ---
                float field = fieldAt(cellCenter, t);
                field *= lerp(1.0 - _DotJitter * 0.5, 1.0, hash21(cell + 7.31));

                // --- Punto: cuadrado redondeado; área ∝ campo (halftone real) ---
                float dotHalf = 0.5 * cellPx * _MaxDot * sqrt(field);
                float d = roundedBox(local, float2(dotHalf, dotHalf), _CornerRadius * dotHalf);
                float dotMask = 1.0 - smoothstep(-0.75, 0.75, d);   // AA ~1px

                // --- Fondo (+ nube suave de halo con el campo SIN cuantizar, referencia clara) ---
                float3 bg = _BgColor.rgb;
                if (_HaloAmount > 0.001)
                    bg = lerp(bg, _DotMid.rgb, _HaloAmount * fieldAt(pxPos, t));

                // --- Color del punto: rampa por campo (núcleos incandescentes de la referencia) ---
                float3 dotCol = _DotDeep.rgb;
                dotCol = lerp(dotCol, _DotMid.rgb, smoothstep(0.25, 0.60, field));
                dotCol = lerp(dotCol, _DotHot.rgb, smoothstep(0.60, 0.95, field));

                float3 col = lerp(bg, dotCol, dotMask);

                // --- Firma de familia: grano animado por frame + viñeta ---
                col += (hash21(IN.texcoord * 713.7 + frac(t) * 371.0) - 0.5) * _GrainStrength;

                float2 vc = IN.texcoord - 0.5;
                col *= 1.0 - _Vignette * smoothstep(0.35, 1.15, dot(vc, vc) * 4.0);

                fixed4 outCol = fixed4(col * IN.color.rgb, _SheetOpacity * IN.color.a);

                #ifdef UNITY_UI_CLIP_RECT
                outCol.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(outCol.a - 0.001);
                #endif

                return outCol;
            }
            ENDCG
        }
    }
}
