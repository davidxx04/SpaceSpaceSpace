// Hoja "espectro retro" para el popup del leaderboard (Canvas/UI). Todo procedural, sin texturas:
// un abanico de palas arcoíris discretas (violeta -> rojo) irradiando desde FUERA de la esquina
// superior derecha, con hueco entre palas y bordes suaves, fundido radial (mueren hacia el centro),
// vaivén global lentísimo y "respiración" de brillo por pala. Encima, grano de película animado por
// frame sobre TODA la hoja y viñeta (mismo acabado que ThermalFlow).
//
// La hoja es DUEÑA del píxel: pinta base casi negra + haces + grano con _SheetOpacity alto, encima
// del Dim del panel. Así el grano cubre toda la hoja (look póster) y quitar el efecto devuelve el
// Dim plano de siempre. La zona central se oscurece (_CenterDarken) para que las filas se lean.
//
// _Size lo empuja LeaderboardSheetFx desde el RectTransform (la UI no soporta PropertyBlocks; se
// usa instancia de material, patrón NeonButtonFx). El menú corre a timeScale 1, así que _Time.y.
// UI-compatible (Stencil + clip rect estándar, mismo boilerplate que NeonPlate/UIBarCaustic).
Shader "SpaceSpaceSpace/SpectrumSheet"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Hoja base)]
        _SheetColor ("Base de la hoja (casi negro)", Color) = (0.015, 0.02, 0.035, 1)
        _SheetOpacity ("Opacidad de la hoja", Range(0, 1)) = 0.9

        [Header(Abanico)]
        _FanOrigin ("Origen del abanico (UV, fuera de esquina)", Vector) = (1.06, 1.10, 0, 0)
        _AngleStart ("Angulo inicio (grados)", Float) = 188
        _AngleEnd ("Angulo fin (grados)", Float) = 264
        _BladeCount ("Numero de palas", Float) = 12
        _BladeFill ("Relleno de pala (resto = hueco)", Range(0.1, 1)) = 0.72
        _EdgeSoft ("Suavidad del borde de pala", Range(0.01, 0.5)) = 0.10
        _BeamIntensity ("Intensidad de los haces", Range(0, 2)) = 0.85
        _FadeStart ("Fundido radial inicio", Float) = 0.25
        _FadeEnd ("Fundido radial fin", Float) = 1.15

        [Header(Colores del espectro)]
        _SpecA ("Violeta", Color) = (0.55, 0.15, 0.95, 1)
        _SpecB ("Cian", Color) = (0.10, 0.70, 0.95, 1)
        _SpecC ("Verde", Color) = (0.15, 0.85, 0.35, 1)
        _SpecD ("Amarillo", Color) = (1.0, 0.85, 0.15, 1)
        _SpecE ("Rojo", Color) = (0.95, 0.20, 0.10, 1)

        [Header(Animacion)]
        _SwayAmplitude ("Vaiven del abanico (grados)", Float) = 2.0
        _SwaySpeed ("Velocidad vaiven (Hz)", Float) = 0.06
        _BreathAmount ("Respiracion por pala", Range(0, 1)) = 0.25
        _BreathSpeed ("Velocidad respiracion", Float) = 0.5

        [Header(Legibilidad)]
        _CenterDarken ("Oscurecido zona de filas", Range(0, 1)) = 0.75
        _CenterR0 ("Radio interior oscuro", Float) = 0.28
        _CenterR1 ("Radio exterior (fin oscuro)", Float) = 0.85

        [Header(Grano y acabado)]
        _GrainStrength ("Fuerza del grano", Range(0, 0.35)) = 0.13
        _Vignette ("Vineta", Range(0, 1)) = 0.3

        _Size ("Rect Size px (auto)", Vector) = (800, 600, 0, 0)

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

            fixed4 _Color, _SheetColor;
            float _SheetOpacity;
            float4 _FanOrigin;
            float _AngleStart, _AngleEnd, _BladeCount, _BladeFill, _EdgeSoft, _BeamIntensity;
            float _FadeStart, _FadeEnd;
            fixed4 _SpecA, _SpecB, _SpecC, _SpecD, _SpecE;
            float _SwayAmplitude, _SwaySpeed, _BreathAmount, _BreathSpeed;
            float _CenterDarken, _CenterR0, _CenterR1;
            float _GrainStrength, _Vignette;
            float4 _Size;
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

            // Mismo hash que ThermalFlow/SpaceNebula: sin texturas.
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            // Rampa espectral: 5 paradas tuneables (violeta -> cian -> verde -> amarillo -> rojo).
            // El azul y el naranja emergen de la interpolación (patrón thermalRamp de ThermalFlow).
            float3 spectrumRamp(float t)
            {
                float3 c = _SpecA.rgb;
                c = lerp(c, _SpecB.rgb, smoothstep(0.02, 0.28, t));
                c = lerp(c, _SpecC.rgb, smoothstep(0.28, 0.52, t));
                c = lerp(c, _SpecD.rgb, smoothstep(0.52, 0.76, t));
                c = lerp(c, _SpecE.rgb, smoothstep(0.76, 0.98, t));
                return c;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 raw = IN.texcoord;
                float aspect = _Size.x / max(_Size.y, 1.0);

                // --- Polar respecto al origen del abanico (espacio corregido de aspecto) ---
                float2 p = float2((raw.x - _FanOrigin.x) * aspect, raw.y - _FanOrigin.y);
                float r = length(p);
                float ang = degrees(atan2(p.y, p.x));
                if (ang < 0.0) ang += 360.0;   // la pantalla queda en ~[184, 266], lejos de la costura 0/360
                ang += _SwayAmplitude * sin(_Time.y * _SwaySpeed * 6.2831853);

                // --- Posición dentro del abanico -> pala discreta con hueco y bordes suaves ---
                float t = (ang - _AngleStart) / max(_AngleEnd - _AngleStart, 0.001);
                float inFan = smoothstep(0.0, 0.02, t) * (1.0 - smoothstep(0.98, 1.0, t));
                float bladeF = saturate(t) * _BladeCount;
                float idx = floor(bladeF);
                float f = frac(bladeF);

                float blade = smoothstep(0.0, _EdgeSoft, f)
                            * (1.0 - smoothstep(_BladeFill, _BladeFill + _EdgeSoft, f));

                // Color muestreado en el CENTRO de la pala: cada pala es de UN color (look póster).
                float3 spec = spectrumRamp((idx + 0.5) / max(_BladeCount, 1.0));

                // --- Fundido radial + respiración de brillo por pala (fase por índice) ---
                float radial = 1.0 - smoothstep(_FadeStart, _FadeEnd, r);
                float breath = 1.0 - _BreathAmount * (0.5 + 0.5 * sin(_Time.y * _BreathSpeed + idx * 1.93));
                float beam = blade * inFan * radial * breath * _BeamIntensity;

                // --- Legibilidad: disco central (donde viven las filas) apaga los haces ---
                float2 c2 = float2((raw.x - 0.5) * aspect, raw.y - 0.5);
                beam *= 1.0 - _CenterDarken * (1.0 - smoothstep(_CenterR0, _CenterR1, length(c2)));

                float3 col = _SheetColor.rgb + spec * beam;

                // --- Grano de película animado por frame, sobre TODA la hoja (idéntico a ThermalFlow) ---
                col += (hash21(raw * 713.7 + frac(_Time.y) * 371.0) - 0.5) * _GrainStrength;

                // --- Viñeta ---
                float2 vc = raw - 0.5;
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
