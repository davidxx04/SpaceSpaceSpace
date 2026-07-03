// Placa NEÓN estilo "primer plano de CRT" para botones de UI (Canvas). Todo procedural, sin
// sprites: rectángulo redondeado por SDF en PÍXELES (NeonButtonFx pasa _Size del RectTransform)
// con borde difuminado tipo bloom, cruzado por scanlines finísimas oscuras que DERIVAN hacia
// abajo y PARPADEAN por línea (fallo de emisión), más caídas de brillo globales ocasionales.
//
// Estados (los empuja NeonButtonFx por instancia de material, la UI no soporta PropertyBlocks):
//   _Focus 0..1  -> mezcla _BaseColor (naranja) a _FocusColor (rojo) con parpadeo nervioso.
//   _PressTime   -> destello blanco con decay al pulsar.
//   _LinesOnly   -> modo overlay: SOLO pinta las líneas (capa sutil por ENCIMA de las letras).
//
// OJO tiempo: _Time del shader se CONGELA con timeScale = 0 (y la pantalla de nickname vive
// congelada), así que NeonButtonFx empuja _UnscaledTime (Time.unscaledTime) cada frame a su
// instancia de material y aquí se anima con eso.
// UI-compatible (Stencil + clip rect estándar, mismo boilerplate que UIBarCaustic).
Shader "SpaceSpaceSpace/NeonPlate"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _BaseColor ("Base (naranja neon)", Color) = (1.0, 0.42, 0.06, 1)
        _FocusColor ("Focus (placa encendida)", Color) = (1.0, 0.90, 0.68, 1)

        [Header(Borde de foco)]
        _FocusBorderColor ("Focus Border", Color) = (1, 1, 1, 1)
        _FocusBorderWidth ("Focus Border Width px", Float) = 2.5
        _BorderFlickerRate ("Border Flicker Rate (Hz)", Float) = 7
        _BorderFlickerMin ("Border Flicker Min", Range(0, 1)) = 0.4

        [Header(Placa SDF en pixeles)]
        _Size ("Rect Size px (auto)", Vector) = (320, 64, 0, 0)
        _CornerRadius ("Corner Radius px", Float) = 14
        _BloomWidth ("Bloom Width px", Float) = 10
        _RimShade ("Rim Shade", Range(0, 1)) = 0.25

        [Header(Scanlines)]
        _LinePeriod ("Line Period px", Float) = 6
        _LineThickness ("Line Thickness px", Float) = 1.6
        _LineStrength ("Line Strength", Range(0, 1)) = 0.45
        _ScrollSpeed ("Line Scroll px/s (abajo)", Float) = 4
        _FlickerAmount ("Line Flicker Amount", Range(0, 1)) = 0.6
        _FlickerRate ("Line Flicker Rate (Hz)", Float) = 9

        [Header(Fallos de emision globales)]
        _DropAmount ("Drop Amount", Range(0, 1)) = 0.22
        _DropRate ("Drop Rate (Hz)", Float) = 2.2
        _DropChance ("Drop Chance (0..1)", Range(0, 1)) = 0.18

        [Header(Estados)]
        _Focus ("Focus (auto)", Range(0, 1)) = 0
        _FocusFlickerSpeed ("Focus Flicker (Hz)", Float) = 22
        _PressTime ("Press Time (auto)", Float) = -100

        [Header(Modo overlay)]
        [Toggle] _LinesOnly ("Lines Only (overlay sobre texto)", Float) = 0

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
            #pragma target 2.0

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

            fixed4 _Color, _BaseColor, _FocusColor, _FocusBorderColor;
            float _FocusBorderWidth, _BorderFlickerRate, _BorderFlickerMin;
            float4 _Size;
            float _CornerRadius, _BloomWidth, _RimShade;
            float _LinePeriod, _LineThickness, _LineStrength, _ScrollSpeed, _FlickerAmount, _FlickerRate;
            float _DropAmount, _DropRate, _DropChance;
            float _Focus, _FocusFlickerSpeed, _PressTime;
            float _LinesOnly, _UnscaledTime;
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

            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                return frac(p * (p + p));
            }

            // SDF de rectángulo redondeado (px). d < 0 dentro.
            float roundedBox(float2 p, float2 halfSize, float r)
            {
                float2 q = abs(p) - (halfSize - r);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // --- Forma: coordenadas en píxeles centradas; el bloom cabe DENTRO del rect ---
                float2 px = (IN.texcoord - 0.5) * _Size.xy;
                float2 hs = _Size.xy * 0.5 - _BloomWidth;
                float d = roundedBox(px, hs, _CornerRadius);

                // Núcleo sólido con borde difuminado (dentro poco, fuera el ancho del bloom).
                float shape = 1.0 - smoothstep(-_BloomWidth * 0.35, _BloomWidth, d);

                float t = _UnscaledTime;   // lo empuja NeonButtonFx (sobrevive a timeScale = 0)

                // --- Scanlines: banda oscura por periodo, derivando hacia abajo ---
                float yy = px.y + t * _ScrollSpeed;            // +t => el patrón baja
                float lineIdx = floor(yy / _LinePeriod);
                float fy = frac(yy / _LinePeriod);
                float halfT = 0.5 * _LineThickness / _LinePeriod;
                float soft = 0.6 / _LinePeriod;                // ~0.6 px de suavizado
                float band = smoothstep(0.5 - halfT - soft, 0.5 - halfT, fy)
                           * (1.0 - smoothstep(0.5 + halfT, 0.5 + halfT + soft, fy));

                // Parpadeo POR LÍNEA: cada 1/_FlickerRate s se re-sortea la visibilidad de cada línea.
                float slice = floor(t * _FlickerRate);
                float r = hash11(lineIdx * 7.31 + slice * 113.13);
                float lineVis = lerp(1.0 - _FlickerAmount, 1.0, r);
                float darken = band * _LineStrength * lineVis;

                // --- Modo overlay: solo las líneas, recortadas a la placa (van sobre las letras) ---
                if (_LinesOnly > 0.5)
                {
                    fixed4 lcol = fixed4(0.0, 0.0, 0.0, darken * shape * IN.color.a);
                    #ifdef UNITY_UI_CLIP_RECT
                    lcol.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                    #endif
                    #ifdef UNITY_UI_ALPHACLIP
                    clip(lcol.a - 0.001);
                    #endif
                    return lcol;
                }

                // --- Color: base -> foco (rojo) con parpadeo nervioso e irregular en foco ---
                float fslice = floor(t * _FocusFlickerSpeed);
                float focusFlick = lerp(0.68, 1.0, hash11(fslice * 5.77));
                float3 col = lerp(_BaseColor.rgb, _FocusColor.rgb * focusFlick, saturate(_Focus));

                // --- Fallos de emisión globales: de vez en cuando la placa pierde brillo un instante ---
                float gslice = floor(t * _DropRate);
                float gr = hash11(gslice * 17.71);
                float dropping = step(1.0 - _DropChance, gr);
                float buzz = 0.7 + 0.3 * sin(t * 120.0 + gr * 40.0);   // zumbido durante el fallo
                float emission = 1.0 - _DropAmount * dropping * buzz;

                // --- Rim: leve oscurecido hacia el borde (el fósforo pierde fuerza en el canto) ---
                float rim = 1.0 - _RimShade * smoothstep(-_BloomWidth * 2.0, 0.0, d);

                // --- Destello de pulsación ---
                float press = exp(-max(0.0, t - _PressTime) * 8.0) * step(0.0, _PressTime);

                col = col * emission * rim * (1.0 - darken) + press * 0.8;

                // --- Borde blanco de foco (parpadea): señal INEQUÍVOCA de selección ---
                // edge = 1 justo en el canto de la placa (d≈0), cayendo en _FocusBorderWidth px.
                float edge = 1.0 - smoothstep(0.0, _FocusBorderWidth, abs(d));
                float bslice = floor(t * _BorderFlickerRate);
                float bflick = lerp(_BorderFlickerMin, 1.0, hash11(bslice * 3.17 + 0.7));
                float border = saturate(_Focus) * edge * bflick;
                col = lerp(col, _FocusBorderColor.rgb, border);

                // El borde también manda en el alfa: nítido en el canto aunque la placa se difumine ahí.
                float a = max(shape, border) * IN.color.a;
                fixed4 outCol = fixed4(col * IN.color.rgb, a);

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
