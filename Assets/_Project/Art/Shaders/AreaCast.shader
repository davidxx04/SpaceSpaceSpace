// Telegrafía "cast" del área del boss: malla de PUNTOS formando anillos concéntricos CUADRADOS
// (distancia Chebyshev en UV normalizado) que nacen en el centro y viajan hacia fuera, más una
// LÍNEA DE BARRIDO que cruza el área distorsionando la malla a su paso (ondulación senoidal).
// Todo procedural en un solo quad — sin texturas, sin partículas, sin fbm: 1 hash + smoothsteps.
// Es deliberadamente MUCHO más barato que SpriteSwoosh (~60-70 ALU vs varios cientos).
//
// El reloj lo empuja BossArea por MaterialPropertyBlock (_Progress 0..1 = t/fillSeconds del SO):
//   - El frente de onda está en r = _Progress; como r = max(|x|,|y|) normalizado vale 1 en las
//     CUATRO paredes a la vez, el anillo líder toca el borde EXACTAMENTE en el frame del impacto.
//   - Los anillos nacen en el centro cuando P cruza k/_RingCount y viajan a la velocidad del
//     frente, espaciados 1/_RingCount. Delante del frente: vacío (+ malla tenue _FieldFaint).
//   - La línea de barrido recorre el eje _SweepAxis (el fillAxisY del ataque) de pared a pared
//     en el MISMO tiempo, desplazando la retícula (el "wobble" de la referencia).
//
// Verbo por paleta (_Palette por MPB, mismo convenio que SpriteSwoosh): 0 = cálida (esquiva:
// rojo/naranja/amarillo), 1 = fría (parry: índigo/azul/verde, viridis). El matiz vive en las
// rampas; el vertex color llega blanco con el alpha maestro del SO. _Flash = 1 funde a losa
// SÓLIDA (x vertex rgb = impactColor) para que la ventana de daño se lea inequívoca.
//
// (Para builds: "SpaceSpaceSpace/AreaCast" está en Always Included Shaders.)
Shader "SpaceSpaceSpace/AreaCast"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Anillos)]
        _RingCount ("Ring Count bandas centro a pared", Float) = 5
        _RingWidth ("Ring Width fraccion de fase", Range(0.02, 0.5)) = 0.18
        _LeadWidth ("Lead Ring Width r norm", Range(0.005, 0.2)) = 0.05
        _LeadBoost ("Lead Ring Brightness", Float) = 1.6
        _TrailFade ("Trail Fade tras el frente", Range(0, 1)) = 0.45

        [Header(Puntos)]
        _DotSpacing ("Dot Spacing unidades mundo", Float) = 0.08
        _DotRadius ("Dot Radius fraccion celda", Range(0.1, 0.6)) = 0.12
        _DotSoft ("Dot Softness celda", Range(0.02, 0.3)) = 0.1
        _DotVariation ("Dot Variation por hash", Range(0, 1)) = 0.35
        _FieldFaint ("Faint Field malla en reposo", Range(0, 0.3)) = 0.05

        [Header(Fondo del area por verbo)]
        _BgCold ("Bg Cold azul oscuro", Color) = (0.12, 0.13, 0.45, 1)
        _BgWarm ("Bg Warm rojo oscuro", Color) = (0.32, 0.05, 0.06, 1)
        _BgOpacity ("Bg Opacity", Range(0, 1)) = 0.6

        [Header(Linea de barrido)]
        _SweepWidth ("Sweep Width medio ancho norm", Range(0.02, 0.6)) = 0.18
        _SweepDisplace ("Sweep Displacement norm", Float) = 0.07
        _SweepWobbleFreq ("Sweep Wobble Freq", Float) = 9
        _SweepBoost ("Sweep Brightness Lift", Range(0, 2)) = 0.8
        _SweepFaint ("Sweep dots fuera de anillos", Range(0, 1)) = 0.35

        [Header(Rampa fria parry viridis)]
        _ColdA ("Cold Deep indigo", Color) = (0.25, 0.15, 0.55, 1)
        _ColdB ("Cold Mid azul", Color) = (0.15, 0.40, 0.85, 1)
        _ColdC ("Cold Hot verde", Color) = (0.35, 0.90, 0.45, 1)

        [Header(Rampa calida esquiva)]
        _WarmA ("Warm Deep rojo", Color) = (0.60, 0.05, 0.05, 1)
        _WarmB ("Warm Mid naranja", Color) = (1.00, 0.45, 0.05, 1)
        _WarmC ("Warm Hot amarillo", Color) = (1.00, 0.85, 0.30, 1)

        [Header(Por renderer via MaterialPropertyBlock)]
        _Progress ("Progress 0 a 1 (MPB)", Range(0, 1)) = 0
        _SweepAxis ("Sweep Axis 0 X 1 Y (MPB)", Float) = 1
        _Palette ("Palette 0 calida 1 fria (MPB)", Float) = 0
        _Flash ("Flash impacto (MPB)", Range(0, 1)) = 0
        _QuadSize ("Quad Size w h (MPB)", Vector) = (4, 4, 0, 0)
        _Seed ("Seed (MPB)", Float) = 0
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
            #pragma target 3.0
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
            float _RingCount, _RingWidth, _LeadWidth, _LeadBoost, _TrailFade;
            float _DotSpacing, _DotRadius, _DotSoft, _DotVariation, _FieldFaint;
            fixed4 _BgCold, _BgWarm;
            float _BgOpacity;
            float _SweepWidth, _SweepDisplace, _SweepWobbleFreq, _SweepBoost, _SweepFaint;
            fixed4 _ColdA, _ColdB, _ColdC;
            fixed4 _WarmA, _WarmB, _WarmC;
            float _Progress, _SweepAxis, _Palette, _Flash, _Seed;
            float4 _QuadSize;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            // Mismo hash que el resto del proyecto (ThermalFlow/SpriteSwoosh): sin texturas.
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // UV [0,1] -> [-1,1]: r Chebyshev = 1 en las 4 paredes a la vez.
                float2 p = (i.uv - 0.5) * 2.0;
                float P = _Progress;

                // --- Línea de barrido: cruza el eje elegido de -1 a +1 con el MISMO reloj ---
                float sAxis = lerp(p.x, p.y, _SweepAxis);   // coordenada de viaje
                float sPerp = lerp(p.y, p.x, _SweepAxis);   // coordenada A LO LARGO de la línea
                float sLine = lerp(-1.0, 1.0, P);
                float bump = 1.0 - smoothstep(0.0, _SweepWidth, abs(sAxis - sLine));
                bump *= bump;                                // caída pseudo-gaussiana barata

                // Distorsión: la línea desplaza la malla senoidalmente (la ondulación de la foto).
                float ripple = sin(sPerp * _SweepWobbleFreq + _Seed * 7.0);
                float2 sweepDir = lerp(float2(1.0, 0.0), float2(0.0, 1.0), _SweepAxis);
                float2 pd = p + sweepDir * (bump * ripple * _SweepDisplace);

                // --- Anillos cuadrados sobre el radio Chebyshev distorsionado ---
                float rr = max(abs(pd.x), abs(pd.y));
                float lead = 1.0 - smoothstep(_LeadWidth * 0.5, _LeadWidth, abs(rr - P));

                // Fase de anillos: nacen en el centro cuando P cruza k/_RingCount y viajan al borde.
                float ph = (P - rr) * _RingCount;
                float dphase = abs(frac(ph + 0.5) - 0.5);
                float ring = 1.0 - smoothstep(_RingWidth * 0.5, _RingWidth, dphase);
                ring *= smoothstep(0.0, 0.03, P - rr);              // solo TRAS el frente
                ring *= 1.0 - _TrailFade * saturate(P - rr);        // los viejos se van apagando

                // --- Retícula de puntos en UNIDADES DE MUNDO (redondos en cualquier rect) ---
                float2 cell = (pd * _QuadSize.xy * 0.5) / max(_DotSpacing, 1e-3);
                float2 cid = floor(cell);
                float2 cf = frac(cell) - 0.5;
                float h = hash21(cid + _Seed);
                float radDot = _DotRadius * (1.0 - _DotVariation * h);
                float dotMask = (1.0 - smoothstep(radDot, radDot + _DotSoft, length(cf))) * (0.75 + 0.25 * h);

                // --- Intensidad: puntos x (bandas + realce del barrido) + malla tenue en reposo.
                // La banda del barrido enseña puntos también donde rr > P: la línea cruza de pared
                // a pared mientras los anillos aún no han llegado ahí.
                float bands = max(ring, lead * _LeadBoost);
                float intensity = dotMask * saturate(bands * (1.0 + bump * _SweepBoost)
                                                     + bump * _SweepFaint + _FieldFaint);

                // --- Color: verdes/amarillos cerca del frente y la línea, índigo/rojo en reposo ---
                float heat = saturate(0.25 + 0.55 * bump + 0.45 * lead);
                float3 A = lerp(_WarmA.rgb, _ColdA.rgb, _Palette);
                float3 B = lerp(_WarmB.rgb, _ColdB.rgb, _Palette);
                float3 C = lerp(_WarmC.rgb, _ColdC.rgb, _Palette);
                float3 col = lerp(A, B, smoothstep(0.0, 0.55, heat));
                col = lerp(col, C, smoothstep(0.55, 1.0, heat));

                // --- Fondo del área por verbo: campo oscuro que cubre TODO el rect desde t=0
                // (sustituye al 'prep' gris; los puntos, más claros, destacan encima) ---
                float3 bg = lerp(_BgWarm.rgb, _BgCold.rgb, _Palette);
                float presence = saturate(intensity);
                col = lerp(bg, col, presence);

                // --- Impacto: losa sólida (blanco x vertex rgb = impactColor) a la alpha del SO ---
                col = lerp(col, float3(1.0, 1.0, 1.0), _Flash);
                float alpha = i.color.a * lerp(max(presence, _BgOpacity), 1.0, _Flash);

                return fixed4(col * i.color.rgb, alpha);
            }
            ENDCG
        }
    }
}
