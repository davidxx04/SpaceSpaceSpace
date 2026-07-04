// Creciente / media luna procedural para el cuerpo del Swoosh, ahora con look de FUEGO THERMAL.
// La forma sigue siendo el lune SDF de siempre (dentro del círculo exterior, fuera del interior
// desplazado), estirado a la caja (thickness x width) para encajar en el hitbox. Encima, un
// "continuo de calor" gobernado por _Heat (por MaterialPropertyBlock):
//
//   _Heat = 1  -> fuego fluido marmoleado (fbm con domain-warp): núcleos crema, amarillo->naranja->
//                 rojo hacia el borde, huecos ahumados, lamidas hacia -X (borde de salida cóncavo).
//   _Heat -> 0 -> el residuo FLIR: toda la temperatura se desliza rampa abajo por la banda de
//                 residuo (cian -> índigo -> negro), con disolución por ruido, vetas anisótropas
//                 en el eje de viaje y grano de película crecientes. Es el rastro que deja el
//                 barrido (los fantasmas SwooshResidue empujan _Heat decreciente).
//
// El truco del continuo: T = _Heat * (_ResidueBand + (1-_ResidueBand) * campo). A heat=1 la T nunca
// baja de _ResidueBand, así el cuerpo VIVO no pisa los cianes (que son señal de parry); al enfriar,
// todo pasa por ellos de camino al negro, como una firma de calor en una cámara térmica.
//
// Dos paletas (verbo): _Palette 0 = cálida (esquiva), 1 = fría (parry, "fuego frío" azul). El color
// de vértice llega BLANCO con el alpha maestro (la rampa lleva ya el matiz); se sigue multiplicando
// por compatibilidad. Ruido anclado al CAMINO: coords en unidades de mundo (_QuadSize corrige la
// anisotropía del quad) + _WorldOffset = distancia recorrida -> el cuerpo barre a través de un campo
// fijo y cada fantasma congela el patrón que tenía debajo (handoff sin pop). _Seed por swoosh.
//
// (Para builds: "SpaceSpaceSpace/SpriteSwoosh" ya está en Always Included Shaders.)
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

        [Header(Campo de fuego)]
        _NoiseScale ("Noise Scale por unidad", Float) = 2.2
        _FlowSpeed ("Flow Speed lame hacia menos X", Float) = 0.6
        _Warp ("Domain Warp", Range(0, 2)) = 0.8
        _EdgeErode ("Edge Erode solo hacia dentro", Range(0, 0.3)) = 0.08
        _CoreDepth ("Core Depth norm SDF", Float) = 0.25
        _FieldBase ("Field Base", Float) = 0.45
        _FieldNoise ("Field Noise", Float) = 1.1
        _SmokeAmount ("Smoke Gaps", Range(0, 1)) = 0.5

        [Header(Rampa calida esquiva)]
        _WarmCore ("Warm Core crema", Color) = (1.0, 0.96, 0.85, 1)
        _WarmHot ("Warm Hot amarillo", Color) = (1.0, 0.78, 0.22, 1)
        _WarmMid ("Warm Mid naranja", Color) = (0.93, 0.38, 0.06, 1)
        _WarmDeep ("Warm Deep rojo profundo", Color) = (0.52, 0.06, 0.02, 1)

        [Header(Rampa fria parry)]
        _ColdCore ("Cold Core blanco", Color) = (0.93, 1.0, 1.0, 1)
        _ColdHot ("Cold Hot cian", Color) = (0.45, 0.93, 1.0, 1)
        _ColdMid ("Cold Mid azur", Color) = (0.12, 0.45, 0.95, 1)
        _ColdDeep ("Cold Deep indigo", Color) = (0.18, 0.10, 0.55, 1)

        [Header(Banda de residuo FLIR)]
        _ResidueCyan ("Residue Cyan", Color) = (0.15, 0.80, 0.90, 1)
        _ResidueIndigo ("Residue Indigo", Color) = (0.14, 0.08, 0.45, 1)
        _ResidueBand ("Residue Band suelo T en vivo", Range(0, 0.6)) = 0.32
        _ResidueAlpha ("Residue Max Alpha", Range(0, 1)) = 0.8

        [Header(Enfriado disolucion grano)]
        _DissolveSoft ("Dissolve Softness", Range(0.01, 0.5)) = 0.18
        _HeatFadeBand ("Alpha Fade Band heat", Range(0.02, 0.5)) = 0.18
        _GrainStrength ("Grain Strength", Range(0, 0.5)) = 0.14
        _GrainScale ("Grain Scale", Float) = 140
        _StreakFreq ("Streak Freq x largo y fino", Vector) = (0.6, 4.0, 0, 0)
        _StreakAmount ("Streak Amount", Range(0, 1)) = 0.35

        [Header(Por renderer via MaterialPropertyBlock)]
        _Heat ("Heat (MPB)", Range(0, 1)) = 1
        _Palette ("Palette 0 calida 1 fria (MPB)", Float) = 0
        _Seed ("Seed (MPB)", Float) = 0
        _WorldOffset ("World Offset X (MPB)", Float) = 0
        _QuadSize ("Quad Size w h (MPB)", Vector) = (0.8, 5, 0, 0)
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
            float _Softness, _OuterX, _OuterR, _InnerX, _InnerR;
            float _NoiseScale, _FlowSpeed, _Warp, _EdgeErode, _CoreDepth, _FieldBase, _FieldNoise, _SmokeAmount;
            fixed4 _WarmCore, _WarmHot, _WarmMid, _WarmDeep;
            fixed4 _ColdCore, _ColdHot, _ColdMid, _ColdDeep;
            fixed4 _ResidueCyan, _ResidueIndigo;
            float _ResidueBand, _ResidueAlpha;
            float _DissolveSoft, _HeatFadeBand, _GrainStrength, _GrainScale, _StreakAmount;
            float4 _StreakFreq;
            float _Heat, _Palette, _Seed, _WorldOffset;
            float4 _QuadSize;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            // --- Ruido por hash (idéntico a ThermalFlow/SpaceNebula: sin texturas) ---
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

            // 3 octavas: a este tamaño de quad no se distingue de 4 y es más barato (hay ~30 a la vez).
            float fbm(float2 p)
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

            fixed4 frag (v2f i) : SV_Target
            {
                // --- Lune SDF de siempre: UV [0,1] -> [-1,1], bulto por X, puntas por Y ---
                float2 p = (i.uv - 0.5) * 2.0;
                float dO = length(p - float2(_OuterX, 0.0)) - _OuterR;
                float dI = length(p - float2(_InnerX, 0.0)) - _InnerR;
                float d  = max(dO, -dI);

                // --- Coordenadas "de camino": isotrópicas en unidades de mundo y ancladas al recorrido ---
                float2 w = p * (_QuadSize.xy * 0.5);
                w.x += _WorldOffset;
                w += _Seed * float2(13.7, 71.3);          // swooshes simultáneos != patrón
                float t = _Time.y + _Seed * 17.0;
                float coolT = 1.0 - _Heat;

                // --- Marmoleado: fbm con domain-warp, derivando +X => el patrón LAME hacia -X ---
                float2 pn = w * _NoiseScale + float2(_FlowSpeed * t, 0.0);
                float2 q = float2(fbm(pn), fbm(pn + float2(5.2, 1.3)));
                float marble = fbm(pn + _Warp * q);

                // --- Borde que lame: erosión SOLO hacia dentro (nunca sobresale del hitbox) ---
                float dv = d + (1.0 - marble) * _EdgeErode;
                float aa = max(_Softness, 1e-4);
                float mask = 1.0 - smoothstep(-aa, aa, dv);

                // --- Campo de calor: profundidad SDF (núcleo caliente) marmoleada por el ruido ---
                float depth = saturate(-dv / max(_CoreDepth, 1e-4));
                float f = saturate(depth * (_FieldBase + _FieldNoise * marble));

                // --- Temperatura: el continuo de enfriado (a heat=1 el suelo es _ResidueBand) ---
                float T = _Heat * (_ResidueBand + (1.0 - _ResidueBand) * f);

                // --- Smear FLIR: vetas largas en X (eje de viaje) que perturban T solo al enfriar ---
                float streak = valueNoise(float2(w.x * _StreakFreq.x, w.y * _StreakFreq.y) + _Seed * 31.7);
                T += (streak - 0.5) * _StreakAmount * coolT;

                // --- Grano de película: perturba T y ensucia el color (más fuerte frío) ---
                float g = hash21(i.uv * _GrainScale + frac(t) * 371.0) - 0.5;
                T += g * _GrainStrength * coolT;

                // --- Rampa única extendida: paleta elegida ANTES (4 lerps), banda de residuo abajo ---
                float3 cCore = lerp(_WarmCore.rgb, _ColdCore.rgb, _Palette);
                float3 cHot  = lerp(_WarmHot.rgb,  _ColdHot.rgb,  _Palette);
                float3 cMid  = lerp(_WarmMid.rgb,  _ColdMid.rgb,  _Palette);
                float3 cDeep = lerp(_WarmDeep.rgb, _ColdDeep.rgb, _Palette);

                float3 col = float3(0.0, 0.0, 0.0);       // frío total = negro
                col = lerp(col, _ResidueIndigo.rgb, smoothstep(0.02, 0.09, T));
                col = lerp(col, _ResidueCyan.rgb,   smoothstep(0.09, 0.19, T));
                col = lerp(col, cDeep, smoothstep(0.19, 0.32, T));
                col = lerp(col, cMid,  smoothstep(0.32, 0.50, T));
                col = lerp(col, cHot,  smoothstep(0.50, 0.70, T));
                col = lerp(col, cCore, smoothstep(0.70, 0.92, T));

                // --- Huecos ahumados (solo en caliente) + grano aditivo ---
                col *= 1.0 - _SmokeAmount * 0.85 * smoothstep(0.35, 0.12, f) * _Heat;
                col += g * _GrainStrength * (0.25 + 0.75 * coolT);

                // --- Disolución: umbral que crece con coolT, remapeado para NO comer nada a heat=1 ---
                float n3 = valueNoise(w * _NoiseScale * 2.7 + _Seed * float2(7.1, 3.3));
                float th = lerp(-_DissolveSoft - 0.001, 1.0 + _DissolveSoft, coolT);
                mask *= smoothstep(th - _DissolveSoft, th + _DissolveSoft, lerp(n3, f, 0.35));

                // --- Alpha: máscara * fundido final por heat * techo de alpha del residuo ---
                float alpha = i.color.a * mask
                            * smoothstep(0.0, _HeatFadeBand, _Heat)
                            * lerp(_ResidueAlpha, 1.0, _Heat);

                return fixed4(col * i.color.rgb, alpha);
            }
            ENDCG
        }
    }
}
